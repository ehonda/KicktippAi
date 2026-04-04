using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareRepeatedMatch;

public sealed class PrepareRepeatedMatchCommand : AsyncCommand<PrepareRepeatedMatchSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<PrepareRepeatedMatchCommand> _logger;

    public PrepareRepeatedMatchCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<PrepareRepeatedMatchCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PrepareRepeatedMatchSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
            var outcome = await LoadCompletedOutcomeAsync(matchOutcomeRepository, settings, cancellationToken);
            var sourceDatasetName = ExperimentArtifactSupport.BuildSourceDatasetName(settings.CommunityContext);
            var sourceItem = ExperimentArtifactSupport.BuildHostedDatasetItem(outcome);
            var sliceKey = string.IsNullOrWhiteSpace(settings.SliceKey)
                ? $"repeat-{settings.SampleSize}"
                : settings.SliceKey.Trim();
            var sourcePoolKey = string.IsNullOrWhiteSpace(settings.SourcePoolKey)
                ? ExperimentArtifactSupport.BuildRepeatedMatchSourcePoolKey(settings.Matchday!.Value, settings.HomeTeam, settings.AwayTeam)
                : settings.SourcePoolKey.Trim();
            var datasetName = settings.DatasetName
                ?? $"{sourceDatasetName}/repeated-match/{sourcePoolKey}/{sliceKey}";
            var outputDirectory = ResolveOutputDirectory(settings.OutputDirectory, settings.CommunityContext, sourcePoolKey, sliceKey);
            var sliceArtifactPath = Path.Combine(outputDirectory, "slice-dataset.json");
            var sliceManifestPath = Path.Combine(outputDirectory, "slice-manifest.json");

            Directory.CreateDirectory(outputDirectory);

            var startsAt = GetStartsAt(sourceItem);
            var sourceItems = Enumerable.Range(1, settings.SampleSize)
                .Select(index =>
                {
                    var sliceDatasetItemId = ExperimentArtifactSupport.BuildRepeatedSliceDatasetItemId(
                        sourceItem.Id,
                        sliceKey,
                        index,
                        settings.SampleSize);

                    return new PreparedExperimentSourceItem(
                        sourceItem.Id,
                        sliceDatasetItemId,
                        sourceItem.Id,
                        sourceItem.Metadata.Competition,
                        sourceItem.Metadata.Season,
                        sourceItem.Metadata.CommunityContext,
                        sourceItem.Metadata.Matchday,
                        sourceItem.Metadata.MatchdayLabel,
                        sourceItem.Metadata.HomeTeam,
                        sourceItem.Metadata.AwayTeam,
                        startsAt,
                        sourceItem.Metadata.TippSpielId,
                        sourceItem.ExpectedOutput.HomeGoals,
                        sourceItem.ExpectedOutput.AwayGoals);
                })
                .ToList();

            var bundle = PreparedExperimentBundleBuilder.Build(
                sourceItems,
                settings.CommunityContext,
                sourceDatasetName,
                datasetName,
                sliceKey,
                "repeated-match",
                "repeated-match",
                sourcePoolKey,
                null);

            await WriteJsonFileAsync(sliceArtifactPath, bundle.Artifact, cancellationToken);
            await WriteJsonFileAsync(sliceManifestPath, bundle.Manifest, cancellationToken);

            var summary = new
            {
                mode = "repeated-match",
                settings.CommunityContext,
                datasetName = bundle.Manifest.SliceDatasetName,
                bundle.Manifest.SourceDatasetName,
                bundle.Manifest.SourcePoolKey,
                bundle.Manifest.SliceKey,
                bundle.Manifest.SampleSize,
                settings.HomeTeam,
                settings.AwayTeam,
                Matchday = settings.Matchday,
                bundle.Manifest.SelectedItemIds,
                bundle.Manifest.SelectedItemIdsHash,
                outputDirectory,
                sliceArtifactPath,
                sliceManifestPath
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, PreparedExperimentCommandSupport.JsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing repeated-match experiment artifact");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static string GetStartsAt(HostedMatchExperimentDatasetItem item)
    {
        if (item.Input.ValueKind != JsonValueKind.Object
            || !item.Input.TryGetProperty("startsAt", out var startsAt)
            || startsAt.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(startsAt.GetString()))
        {
            throw new InvalidOperationException($"Dataset item '{item.Id}' is missing input.startsAt.");
        }

        return startsAt.GetString()!;
    }

    private static async Task<PersistedMatchOutcome> LoadCompletedOutcomeAsync(
        IMatchOutcomeRepository matchOutcomeRepository,
        PrepareRepeatedMatchSettings settings,
        CancellationToken cancellationToken)
    {
        var outcomes = await matchOutcomeRepository.GetMatchdayOutcomesAsync(
            settings.Matchday!.Value,
            settings.CommunityContext,
            cancellationToken);

        var outcome = outcomes.FirstOrDefault(candidate =>
            string.Equals(candidate.HomeTeam, settings.HomeTeam, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.AwayTeam, settings.AwayTeam, StringComparison.OrdinalIgnoreCase));

        if (outcome is null)
        {
            throw new InvalidOperationException(
                $"No persisted match outcome was found for {settings.HomeTeam} vs {settings.AwayTeam} on matchday {settings.Matchday}.");
        }

        if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
        {
            throw new InvalidOperationException(
                $"The selected match does not have a completed persisted outcome yet: {settings.HomeTeam} vs {settings.AwayTeam}.");
        }

        return outcome;
    }

    private static string ResolveOutputDirectory(
        string? outputDirectoryOverride,
        string communityContext,
        string sourcePoolKey,
        string sliceKey)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectoryOverride))
        {
            return Path.GetFullPath(outputDirectoryOverride);
        }

        return Path.GetFullPath(Path.Combine(
            "artifacts",
            "langfuse-experiments",
            "repeated-match",
            ExperimentArtifactSupport.Slugify(communityContext),
            sourcePoolKey,
            sliceKey));
    }

    private static Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PreparedExperimentCommandSupport.JsonOptions), cancellationToken);
    }
}
