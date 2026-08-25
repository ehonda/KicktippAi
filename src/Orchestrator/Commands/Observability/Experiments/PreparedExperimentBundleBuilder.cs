using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Observability.Experiments;

internal static class PreparedExperimentBundleBuilder
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonElement InputSchema = ParseJsonElement(
        """
        {
          "type": "object",
          "properties": {
            "fixture": {
              "type": "string",
              "minLength": 1,
              "description": "Home team vs away team in football display order"
            },
            "startsAt": {
              "type": "string",
              "minLength": 1,
              "description": "Localized match start timestamp string emitted by the .NET exporter"
            }
          },
          "required": ["fixture", "startsAt"],
          "additionalProperties": false
        }
        """);

    private static readonly JsonElement ExpectedOutputSchema = ParseJsonElement(
        """
        {
          "type": "object",
          "properties": {
            "score": {
              "type": "string",
              "minLength": 3,
              "description": "Completed match score in home:away order"
            }
          },
          "required": ["score"],
          "additionalProperties": false
        }
        """);

    public static PreparedExperimentBundle Build(
        IReadOnlyList<PreparedExperimentSourceItem> sourceItems,
        string communityContext,
        string sourceDatasetName,
        string sliceDatasetName,
        string sliceKey,
        string sliceKind,
        string sampleMethod,
        string sourcePoolKey,
        int? sampleSeed,
        string? datasetDescription = null,
        IReadOnlyDictionary<string, object?>? extraDatasetMetadata = null,
        int? matchCount = null,
        int? repetitions = null,
        PreparedHistoricalExperimentCompatibility? historicalCompatibility = null,
        string? startsAfter = null)
    {
        if (sourceItems.Count == 0)
        {
            throw new InvalidOperationException("At least one slice source item is required.");
        }

        var first = sourceItems[0];
        var selectedItemIds = sourceItems
            .Select(item => item.SelectedItemId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var selectedItemIdsHash = ExperimentArtifactSupport.ComputeSelectedItemIdsHash(selectedItemIds);

        var artifactItems = sourceItems.Select(item => new PreparedExperimentDatasetItem(
                item.SliceDatasetItemId,
                JsonSerializer.SerializeToElement(new
                {
                    fixture = $"{item.HomeTeam} vs {item.AwayTeam}",
                    item.StartsAt
                }, OutputJsonOptions),
                JsonSerializer.SerializeToElement(new
                {
                    score = $"{item.ExpectedHomeGoals}:{item.ExpectedAwayGoals}"
                }, OutputJsonOptions),
                JsonSerializer.SerializeToElement(new
                {
                    item.Competition,
                    item.Season,
                    item.CommunityContext,
                    item.Matchday,
                    item.MatchdayLabel,
                    item.HomeTeam,
                    item.AwayTeam,
                    item.TippSpielId,
                    item.FixtureIndex,
                    item.RepetitionIndex,
                    item.ResolvedContextManifest,
                    item.PredictionCreatedAt
                }, OutputJsonOptions)))
            .ToList();

        var manifestItems = sourceItems.Select(item => new PreparedExperimentManifestItem
        {
            SourceDatasetItemId = item.SourceDatasetItemId,
            SliceDatasetItemId = item.SliceDatasetItemId,
            HomeTeam = item.HomeTeam,
            AwayTeam = item.AwayTeam,
            Matchday = item.Matchday,
            StartsAt = item.StartsAt,
            TippSpielId = item.TippSpielId,
            FixtureIndex = item.FixtureIndex,
            RepetitionIndex = item.RepetitionIndex,
            ResolvedContextManifest = item.ResolvedContextManifest,
            PredictionCreatedAt = item.PredictionCreatedAt,
            HistoricalContextManifest = item.HistoricalContextManifest,
            ExpectedHomeGoals = item.HistoricalContextManifest is null ? null : item.ExpectedHomeGoals,
            ExpectedAwayGoals = item.HistoricalContextManifest is null ? null : item.ExpectedAwayGoals
        }).ToList();

        var datasetMetadataNode = JsonSerializer.SerializeToNode(new
        {
            first.Competition,
            communityContext,
            scope = string.Equals(sliceKind, "single-match", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sliceKind, "repeated-match", StringComparison.OrdinalIgnoreCase)
                ? "repeated-match"
                : string.Equals(sliceKind, "repeated-match-slice", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sampleMethod, "repeated-match-slice", StringComparison.OrdinalIgnoreCase)
                    ? "repeated-match-slice"
                : string.Equals(sliceKind, "community-to-date", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sampleMethod, "community-to-date", StringComparison.OrdinalIgnoreCase)
                    ? "community-to-date"
                : "match-slice",
            first.Season,
            sliceKey,
            sliceKind,
            sampleMethod,
            sampleSeed,
            sampleSize = sourceItems.Count,
            matchCount,
            repetitions,
            sourceDatasetName,
            sourcePoolKey
        }, OutputJsonOptions) as JsonObject ?? new JsonObject();
        AddDatasetMetadata(datasetMetadataNode, extraDatasetMetadata);
        var datasetMetadata = JsonSerializer.SerializeToElement(datasetMetadataNode, OutputJsonOptions);

        var artifact = new PreparedExperimentDataset(
            sliceDatasetName,
            string.IsNullOrWhiteSpace(datasetDescription)
                ? $"{sliceKind} dataset for {sourceItems.Count} item(s) on {sliceKey}"
                : datasetDescription.Trim(),
            datasetMetadata,
            InputSchema,
            ExpectedOutputSchema,
            artifactItems);

        var manifest = new PreparedExperimentManifest
        {
            TaskType = historicalCompatibility is null ? null : sliceKind,
            SliceKey = sliceKey,
            SliceKind = sliceKind,
            SampleMethod = sampleMethod,
            CommunityContext = communityContext,
            SourcePoolKey = sourcePoolKey,
            SourceDatasetName = sourceDatasetName,
            SliceDatasetName = sliceDatasetName,
            Competition = first.Competition,
            Season = first.Season,
            SampleSeed = sampleSeed,
            SampleSize = sourceItems.Count,
            MatchCount = matchCount,
            Repetitions = repetitions,
            HistoricalCompatibility = historicalCompatibility,
            StartsAfter = startsAfter,
            SelectedItemIds = selectedItemIds,
            SelectedItemIdsHash = selectedItemIdsHash,
            Items = manifestItems
        };
        if (historicalCompatibility is not null)
        {
            manifest = manifest with
            {
                HistoricalArtifactSha256 = PreparedExperimentCommandSupport.ComputeHistoricalArtifactSha256(manifest)
            };
        }

        // All prepare commands build their complete bundle before they write either JSON
        // artifact. Validate the manifest here so an outcome-only 2026/27 source cannot
        // leave a dataset without the required immutable prediction provenance on disk.
        return new PreparedExperimentBundle(
            artifact,
            PreparedExperimentCommandSupport.ValidateManifest(manifest));
    }

    private static void AddDatasetMetadata(JsonObject datasetMetadata, IReadOnlyDictionary<string, object?>? extraDatasetMetadata)
    {
        if (extraDatasetMetadata is null)
        {
            return;
        }

        foreach (var (key, value) in extraDatasetMetadata)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            datasetMetadata[key] = JsonSerializer.SerializeToNode(value, OutputJsonOptions);
        }
    }

    private static JsonElement ParseJsonElement(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}

internal sealed record PreparedExperimentSourceItem(
    string SourceDatasetItemId,
    string SliceDatasetItemId,
    string SelectedItemId,
    string Competition,
    string Season,
    string CommunityContext,
    int Matchday,
    string MatchdayLabel,
    string HomeTeam,
    string AwayTeam,
    string StartsAt,
    string TippSpielId,
    int ExpectedHomeGoals,
    int ExpectedAwayGoals,
    int? FixtureIndex = null,
    int? RepetitionIndex = null,
    ResolvedMatchContextManifest? ResolvedContextManifest = null,
    DateTimeOffset? PredictionCreatedAt = null,
    ResolvedHistoricalExperimentContextManifest? HistoricalContextManifest = null);

internal sealed record PreparedExperimentBundle(
    PreparedExperimentDataset Artifact,
    PreparedExperimentManifest Manifest);

internal sealed record PreparedExperimentDataset(
    [property: JsonPropertyName("datasetName")] string DatasetName,
    [property: JsonPropertyName("datasetDescription")] string DatasetDescription,
    [property: JsonPropertyName("datasetMetadata")] JsonElement DatasetMetadata,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema,
    [property: JsonPropertyName("expectedOutputSchema")] JsonElement ExpectedOutputSchema,
    [property: JsonPropertyName("items")] IReadOnlyList<PreparedExperimentDatasetItem> Items);

internal sealed record PreparedExperimentDatasetItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("input")] JsonElement Input,
    [property: JsonPropertyName("expectedOutput")] JsonElement ExpectedOutput,
    [property: JsonPropertyName("metadata")] JsonElement Metadata);
