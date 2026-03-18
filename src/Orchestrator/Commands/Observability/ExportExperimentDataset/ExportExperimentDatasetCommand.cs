using System.Globalization;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.ExportExperimentDataset;

public sealed class ExportExperimentDatasetCommand : AsyncCommand<ExportExperimentDatasetSettings>
{
    private const string Season = "2025/2026";
    private static readonly DateTimeZone BundesligaTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
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
        var tippSpielId = outcome.TippSpielId ?? throw new InvalidOperationException(
            $"Persisted outcome for {outcome.HomeTeam} vs {outcome.AwayTeam} is missing tippspielId.");

        var promptMatch = RehydrateForPromptOutput(outcome);
        using var matchJsonDocument = JsonDocument.Parse(PredictionPromptComposer.CreateMatchJson(promptMatch));

        return new HostedMatchExperimentDatasetItem(
            BuildItemId(outcome.Competition, outcome.CommunityContext, tippSpielId),
            matchJsonDocument.RootElement.Clone(),
            new HostedMatchExperimentExpectedOutput(
                outcome.HomeGoals!.Value,
                outcome.AwayGoals!.Value),
            new HostedMatchExperimentMetadata(
                outcome.Competition,
                Season,
                outcome.CommunityContext,
                outcome.Matchday,
                $"md{outcome.Matchday:00}",
                outcome.HomeTeam,
                outcome.AwayTeam,
                tippSpielId));
    }

    private static Match RehydrateForPromptOutput(PersistedMatchOutcome outcome)
    {
        var instant = outcome.StartsAt.ToInstant();
        var offset = BundesligaTimeZone.GetUtcOffset(instant);
        var localizedStartsAt = instant.InZone(DateTimeZone.ForOffset(offset));
        return new Match(outcome.HomeTeam, outcome.AwayTeam, localizedStartsAt, outcome.Matchday);
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
        return $"match-predictions/bundesliga-2025-26/{communityContext}";
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
            $"{Slugify(settings.CommunityContext)}.json"));
    }

    private static string BuildItemId(string competition, string communityContext, string tippSpielId)
    {
        return string.Join(
            "__",
            Slugify(competition),
            Slugify(communityContext),
            $"ts{Slugify(tippSpielId)}");
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length == 0 || builder[^1] == '-')
            {
                continue;
            }

            builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }
}
