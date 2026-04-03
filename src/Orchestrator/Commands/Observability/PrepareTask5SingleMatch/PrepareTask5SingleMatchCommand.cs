using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Commands.Observability.RunTask5Slice;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareTask5SingleMatch;

public sealed class PrepareTask5SingleMatchCommand : AsyncCommand<PrepareTask5SingleMatchSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<PrepareTask5SingleMatchCommand> _logger;

    public PrepareTask5SingleMatchCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<PrepareTask5SingleMatchCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PrepareTask5SingleMatchSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
            var outcome = await LoadCompletedOutcomeAsync(matchOutcomeRepository, settings, cancellationToken);
            var canonicalDatasetName = ExperimentArtifactSupport.BuildCanonicalDatasetName(settings.CommunityContext);
            var canonicalItem = ExperimentArtifactSupport.BuildHostedDatasetItem(outcome);
            var sliceKey = string.IsNullOrWhiteSpace(settings.SliceKey)
                ? $"repeat-{settings.SampleSize}"
                : settings.SliceKey.Trim();
            var sourcePoolKey = string.IsNullOrWhiteSpace(settings.SourcePoolKey)
                ? ExperimentArtifactSupport.BuildSingleMatchSourcePoolKey(settings.Matchday!.Value, settings.HomeTeam, settings.AwayTeam)
                : settings.SourcePoolKey.Trim();
            var datasetName = settings.DatasetName
                ?? $"{canonicalDatasetName}/single-match/{sourcePoolKey}/{sliceKey}";
            var outputDirectory = ResolveOutputDirectory(settings.OutputDirectory, settings.CommunityContext, sourcePoolKey, sliceKey);
            var canonicalSourceArtifactPath = Path.Combine(outputDirectory, "canonical-source.json");
            var sliceArtifactPath = Path.Combine(outputDirectory, "slice-dataset.json");
            var sliceManifestPath = Path.Combine(outputDirectory, "slice-manifest.json");

            Directory.CreateDirectory(outputDirectory);

            var canonicalSource = new ExportedExperimentDataset(canonicalDatasetName, [canonicalItem]);
            await WriteJsonFileAsync(canonicalSourceArtifactPath, canonicalSource, cancellationToken);

            var startsAt = GetStartsAt(canonicalItem);
            var sourceItems = Enumerable.Range(1, settings.SampleSize)
                .Select(index =>
                {
                    var sliceDatasetItemId = ExperimentArtifactSupport.BuildRepeatedSliceDatasetItemId(
                        canonicalItem.Id,
                        sliceKey,
                        index,
                        settings.SampleSize);

                    return new Task5SliceSourceItem(
                        canonicalItem.Id,
                        sliceDatasetItemId,
                        sliceDatasetItemId,
                        canonicalItem.Metadata.Competition,
                        canonicalItem.Metadata.Season,
                        canonicalItem.Metadata.CommunityContext,
                        canonicalItem.Metadata.Matchday,
                        canonicalItem.Metadata.MatchdayLabel,
                        canonicalItem.Metadata.HomeTeam,
                        canonicalItem.Metadata.AwayTeam,
                        startsAt,
                        canonicalItem.Metadata.TippSpielId,
                        canonicalItem.ExpectedOutput.HomeGoals,
                        canonicalItem.ExpectedOutput.AwayGoals);
                })
                .ToList();

            var bundle = Task5SliceBundleBuilder.Build(
                sourceItems,
                settings.CommunityContext,
                canonicalDatasetName,
                datasetName,
                sliceKey,
                "single-match",
                "repeat-single-match",
                sourcePoolKey,
                null);

            await WriteJsonFileAsync(sliceArtifactPath, bundle.Artifact, cancellationToken);
            await WriteJsonFileAsync(sliceManifestPath, bundle.Manifest, cancellationToken);

            var summary = new
            {
                mode = "single-match",
                settings.CommunityContext,
                datasetName = bundle.Manifest.SliceDatasetName,
                bundle.Manifest.CanonicalDatasetName,
                bundle.Manifest.SourcePoolKey,
                bundle.Manifest.SliceKey,
                bundle.Manifest.SampleSize,
                settings.HomeTeam,
                settings.AwayTeam,
                Matchday = settings.Matchday,
                bundle.Manifest.SelectedItemIds,
                bundle.Manifest.SelectedItemIdsHash,
                outputDirectory,
                canonicalSourceArtifactPath,
                sliceArtifactPath,
                sliceManifestPath
            };

            _console.WriteLine(JsonSerializer.Serialize(summary, OutputJsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing Task 5 repeated single-match dataset");
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
        PrepareTask5SingleMatchSettings settings,
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
            "single-match",
            ExperimentArtifactSupport.Slugify(communityContext),
            sourcePoolKey,
            sliceKey));
    }

    private static Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, OutputJsonOptions), cancellationToken);
    }
}
