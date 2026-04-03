using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Observability.ExportExperimentDataset;
using Orchestrator.Commands.Observability.RunTask5Slice;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Observability.PrepareTask5Slice;

public sealed class PrepareTask5SliceCommand : AsyncCommand<PrepareTask5SliceSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAnsiConsole _console;
    private readonly ILogger<PrepareTask5SliceCommand> _logger;

    public PrepareTask5SliceCommand(IAnsiConsole console, ILogger<PrepareTask5SliceCommand> logger)
    {
        _console = console;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PrepareTask5SliceSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var dataset = await LoadJsonFileAsync<ExportedExperimentDataset>(settings.InputPath, cancellationToken);
            if (dataset.Items.Count == 0)
            {
                throw new InvalidOperationException("The canonical dataset artifact does not contain any items.");
            }

            var sampleSeed = settings.SampleSeed ?? int.Parse(
                DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
            var sliceKey = string.IsNullOrWhiteSpace(settings.SliceKey)
                ? $"random-{settings.SampleSize}-seed-{sampleSeed}"
                : settings.SliceKey.Trim();
            var sourcePoolKey = string.IsNullOrWhiteSpace(settings.SourcePoolKey)
                ? "all-matchdays"
                : settings.SourcePoolKey.Trim();
            var selectedItems = SelectRandomItems(dataset.Items, settings.SampleSize, sampleSeed)
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList();

            var communityContext = selectedItems[0].Metadata.CommunityContext;
            var sliceDatasetName = settings.DatasetName
                ?? $"{dataset.DatasetName}/slices/{sourcePoolKey}/{sliceKey}";
            var outputDirectory = ResolveOutputDirectory(settings.OutputDirectory, communityContext, sourcePoolKey, sliceKey);
            var canonicalSourceArtifactPath = Path.Combine(outputDirectory, "canonical-source.json");
            var sliceArtifactPath = Path.Combine(outputDirectory, "slice-dataset.json");
            var sliceManifestPath = Path.Combine(outputDirectory, "slice-manifest.json");

            Directory.CreateDirectory(outputDirectory);
            CopyCanonicalSourceArtifact(settings.InputPath, canonicalSourceArtifactPath);

            var sourceItems = selectedItems.Select(item => new Task5SliceSourceItem(
                    item.Id,
                    ExperimentArtifactSupport.BuildSliceDatasetItemId(item.Id, sliceKey),
                    item.Id,
                    item.Metadata.Competition,
                    item.Metadata.Season,
                    item.Metadata.CommunityContext,
                    item.Metadata.Matchday,
                    item.Metadata.MatchdayLabel,
                    item.Metadata.HomeTeam,
                    item.Metadata.AwayTeam,
                    GetStartsAt(item),
                    item.Metadata.TippSpielId,
                    item.ExpectedOutput.HomeGoals,
                    item.ExpectedOutput.AwayGoals))
                .ToList();

            var bundle = Task5SliceBundleBuilder.Build(
                sourceItems,
                communityContext,
                dataset.DatasetName,
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
                mode = "sampled-slice",
                canonicalDatasetName = dataset.DatasetName,
                datasetName = bundle.Manifest.SliceDatasetName,
                bundle.Manifest.CommunityContext,
                bundle.Manifest.SourcePoolKey,
                bundle.Manifest.SliceKey,
                bundle.Manifest.SliceKind,
                bundle.Manifest.SampleMethod,
                bundle.Manifest.SampleSize,
                bundle.Manifest.SampleSeed,
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
            _logger.LogError(ex, "Error preparing Task 5 sampled slice");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static void CopyCanonicalSourceArtifact(string sourcePath, string destinationPath)
    {
        var absoluteSourcePath = Path.GetFullPath(sourcePath);
        var absoluteDestinationPath = Path.GetFullPath(destinationPath);

        if (string.Equals(absoluteSourcePath, absoluteDestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(absoluteSourcePath, absoluteDestinationPath, overwrite: true);
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

    private static IReadOnlyList<HostedMatchExperimentDatasetItem> SelectRandomItems(
        IReadOnlyList<HostedMatchExperimentDatasetItem> items,
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

    private static async Task<T> LoadJsonFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(path);
        var raw = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var value = JsonSerializer.Deserialize<T>(raw, OutputJsonOptions);
        return value ?? throw new InvalidOperationException($"JSON file '{absolutePath}' could not be deserialized.");
    }

    private static Task WriteJsonFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, OutputJsonOptions), cancellationToken);
    }
}
