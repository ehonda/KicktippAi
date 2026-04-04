using System.Globalization;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareSlice;

public sealed class PrepareSliceCommand : AsyncCommand<PrepareSliceSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<PrepareSliceCommand> _logger;

    public PrepareSliceCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<PrepareSliceCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PrepareSliceSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
            var matchdays = ParseMatchdays(settings.Matchdays);
            var availableItems = await LoadSourceItemsAsync(
                matchOutcomeRepository,
                settings.CommunityContext,
                matchdays,
                cancellationToken);

            if (availableItems.Count == 0)
            {
                throw new InvalidOperationException("No completed historical matches were found for the requested slice scope.");
            }

            var sampleSeed = settings.SampleSeed ?? int.Parse(
                DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            var sliceKey = string.IsNullOrWhiteSpace(settings.SliceKey)
                ? $"random-{settings.SampleSize}-seed-{sampleSeed}"
                : settings.SliceKey.Trim();
            var sourcePoolKey = string.IsNullOrWhiteSpace(settings.SourcePoolKey)
                ? BuildDefaultSourcePoolKey(matchdays)
                : settings.SourcePoolKey.Trim();
            var selectedItems = SelectRandomItems(availableItems, settings.SampleSize, sampleSeed)
                .OrderBy(item => item.SourceDatasetItemId, StringComparer.Ordinal)
                .Select(item => item with
                {
                    SliceDatasetItemId = ExperimentArtifactSupport.BuildSliceDatasetItemId(item.SourceDatasetItemId, sliceKey)
                })
                .ToList();

            var sourceDatasetName = ExperimentArtifactSupport.BuildSourceDatasetName(settings.CommunityContext);
            var sliceDatasetName = settings.DatasetName
                ?? $"{sourceDatasetName}/slices/{sourcePoolKey}/{sliceKey}";
            var outputDirectory = ResolveOutputDirectory(settings.OutputDirectory, settings.CommunityContext, sourcePoolKey, sliceKey);
            var sliceArtifactPath = Path.Combine(outputDirectory, "slice-dataset.json");
            var sliceManifestPath = Path.Combine(outputDirectory, "slice-manifest.json");

            Directory.CreateDirectory(outputDirectory);

            var bundle = PreparedExperimentBundleBuilder.Build(
                selectedItems,
                settings.CommunityContext,
                sourceDatasetName,
                sliceDatasetName,
                sliceKey,
                settings.SliceKind.Trim(),
                settings.SampleMethod.Trim(),
                sourcePoolKey,
                sampleSeed);

            await WriteJsonFileAsync(sliceArtifactPath, bundle.Artifact, cancellationToken);
            await WriteJsonFileAsync(sliceManifestPath, bundle.Manifest, cancellationToken);

            var summary = new
            {
                mode = "slice",
                sourceDatasetName,
                datasetName = bundle.Manifest.SliceDatasetName,
                bundle.Manifest.CommunityContext,
                bundle.Manifest.SourcePoolKey,
                bundle.Manifest.SliceKey,
                bundle.Manifest.SliceKind,
                bundle.Manifest.SampleMethod,
                bundle.Manifest.SampleSize,
                bundle.Manifest.SampleSeed,
                matchdays,
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
            _logger.LogError(ex, "Error preparing slice experiment artifact");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static async Task<IReadOnlyList<PreparedExperimentSourceItem>> LoadSourceItemsAsync(
        IMatchOutcomeRepository matchOutcomeRepository,
        string communityContext,
        IReadOnlyList<int> matchdays,
        CancellationToken cancellationToken)
    {
        var sourceItems = new List<PreparedExperimentSourceItem>();

        foreach (var matchday in matchdays)
        {
            var outcomes = await matchOutcomeRepository.GetMatchdayOutcomesAsync(matchday, communityContext, cancellationToken);
            foreach (var outcome in outcomes)
            {
                if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
                {
                    continue;
                }

                var datasetItem = ExperimentArtifactSupport.BuildHostedDatasetItem(outcome);
                sourceItems.Add(new PreparedExperimentSourceItem(
                    datasetItem.Id,
                    datasetItem.Id,
                    datasetItem.Id,
                    datasetItem.Metadata.Competition,
                    datasetItem.Metadata.Season,
                    datasetItem.Metadata.CommunityContext,
                    datasetItem.Metadata.Matchday,
                    datasetItem.Metadata.MatchdayLabel,
                    datasetItem.Metadata.HomeTeam,
                    datasetItem.Metadata.AwayTeam,
                    GetStartsAt(datasetItem),
                    datasetItem.Metadata.TippSpielId,
                    datasetItem.ExpectedOutput.HomeGoals,
                    datasetItem.ExpectedOutput.AwayGoals));
            }
        }

        return sourceItems
            .OrderBy(item => item.SourceDatasetItemId, StringComparer.Ordinal)
            .ToList();
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

    private static IReadOnlyList<PreparedExperimentSourceItem> SelectRandomItems(
        IReadOnlyList<PreparedExperimentSourceItem> items,
        int count,
        int seed)
    {
        if (items.Count < count)
        {
            throw new InvalidOperationException(
                $"Requested sample size {count} exceeds available dataset item count {items.Count}.");
        }

        var buffer = items.ToList();
        var random = new Random(seed);
        for (var index = buffer.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = random.Next(index + 1);
            (buffer[index], buffer[swapIndex]) = (buffer[swapIndex], buffer[index]);
        }

        return buffer.Take(count).ToList();
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

    private static string BuildDefaultSourcePoolKey(IReadOnlyList<int> matchdays)
    {
        return matchdays.SequenceEqual(Enumerable.Range(1, 34))
            ? "all-matchdays"
            : $"matchdays-{string.Join('-', matchdays)}";
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
            "slices",
            ExperimentArtifactSupport.Slugify(communityContext),
            sourcePoolKey,
            sliceKey));
    }

    private static Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, PreparedExperimentCommandSupport.JsonOptions), cancellationToken);
    }
}
