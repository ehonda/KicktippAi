using System.Globalization;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentDataset;

public sealed class ExportExperimentDatasetCommand : AsyncCommand<ExportExperimentDatasetSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<ExportExperimentDatasetCommand> _logger;

    public ExportExperimentDatasetCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<ExportExperimentDatasetCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ExportExperimentDatasetSettings settings)
    {
        try
        {
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
            var matchdays = ParseMatchdays(settings.Matchdays);
            var datasetName = BuildDatasetName(settings.CommunityContext);

            _console.MarkupLine($"[green]Exporting hosted experiment dataset:[/] [yellow]{Markup.Escape(datasetName)}[/]");

            var items = new List<HostedMatchExperimentDatasetItem>();

            foreach (var matchday in matchdays)
            {
                var outcomes = await matchOutcomeRepository.GetMatchdayOutcomesAsync(matchday, settings.CommunityContext);

                foreach (var outcome in outcomes)
                {
                    if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
                    {
                        continue;
                    }

                    items.Add(BuildItem(outcome));
                }
            }

            items.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

            var export = new ExportedExperimentDataset(datasetName, items.AsReadOnly());
            var outputPath = ResolveOutputPath(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(export, OutputJsonOptions));

            _console.MarkupLine($"[green]Wrote dataset artifact:[/] [yellow]{Markup.Escape(outputPath)}[/]");
            _console.MarkupLine($"[blue]Exported items:[/] {items.Count}");

            if (items.Count > 0)
            {
                _console.MarkupLine($"[blue]First item id:[/] {Markup.Escape(items[0].Id)}");
                _console.MarkupLine($"[blue]Last item id:[/] {Markup.Escape(items[^1].Id)}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting hosted experiment dataset");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static HostedMatchExperimentDatasetItem BuildItem(PersistedMatchOutcome outcome)
    {
        return ExperimentArtifactSupport.BuildHostedDatasetItem(outcome);
    }

    private static IReadOnlyList<int> ParseMatchdays(string? matchdays)
    {
        if (string.IsNullOrWhiteSpace(matchdays))
        {
            return Enumerable.Range(1, 34).ToList().AsReadOnly();
        }

        return matchdays
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => int.Parse(segment, CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(matchday => matchday)
            .ToList()
            .AsReadOnly();
    }

    private static string BuildDatasetName(string communityContext)
    {
        return ExperimentArtifactSupport.BuildCanonicalDatasetName(communityContext);
    }

    private static string ResolveOutputPath(ExportExperimentDatasetSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            return Path.GetFullPath(settings.OutputPath);
        }

        return Path.GetFullPath(Path.Combine(
            "artifacts",
            "langfuse-dataset",
            $"{ExperimentArtifactSupport.Slugify(settings.CommunityContext)}.json"));
    }
}
