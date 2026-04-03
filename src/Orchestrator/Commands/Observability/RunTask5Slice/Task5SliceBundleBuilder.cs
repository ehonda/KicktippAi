using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchestrator.Commands.Observability.RunTask5Slice;

internal static class Task5SliceBundleBuilder
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

    public static Task5PreparedSliceBundle Build(
        IReadOnlyList<Task5SliceSourceItem> sourceItems,
        string communityContext,
        string canonicalDatasetName,
        string sliceDatasetName,
        string sliceKey,
        string sliceKind,
        string sampleMethod,
        string sourcePoolKey,
        int? sampleSeed)
    {
        if (sourceItems.Count == 0)
        {
            throw new InvalidOperationException("At least one slice source item is required.");
        }

        var first = sourceItems[0];
        var selectedItemIds = sourceItems.Select(item => item.SelectedItemId).ToList();
        var selectedItemIdsHash = ExperimentArtifactSupport.ComputeSelectedItemIdsHash(selectedItemIds);

        var artifactItems = sourceItems.Select(item => new Task5PreparedSliceDatasetItem(
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
                    item.TippSpielId
                }, OutputJsonOptions)))
            .ToList();

        var manifestItems = sourceItems.Select(item => new Task5SliceManifestItem
        {
            CanonicalDatasetItemId = item.CanonicalDatasetItemId,
            SliceDatasetItemId = item.SliceDatasetItemId,
            HomeTeam = item.HomeTeam,
            AwayTeam = item.AwayTeam,
            Matchday = item.Matchday,
            StartsAt = item.StartsAt
        }).ToList();

        var datasetMetadata = JsonSerializer.SerializeToElement(new
        {
            first.Competition,
            communityContext,
            scope = string.Equals(sliceKind, "single-match", StringComparison.OrdinalIgnoreCase) ? "single-match" : "match-slice",
            first.Season,
            sliceKey,
            sliceKind,
            sampleMethod,
            sampleSeed,
            sampleSize = sourceItems.Count,
            sourceDatasetName = canonicalDatasetName,
            sourcePoolKey
        }, OutputJsonOptions);

        var artifact = new Task5PreparedSliceDataset(
            sliceDatasetName,
            $"Task 5 {sliceKind} dataset for {sourceItems.Count} item(s) on {sliceKey}",
            datasetMetadata,
            InputSchema,
            ExpectedOutputSchema,
            artifactItems);

        var manifest = new Task5SliceManifest
        {
            SliceKey = sliceKey,
            SliceKind = sliceKind,
            SampleMethod = sampleMethod,
            CommunityContext = communityContext,
            SourcePoolKey = sourcePoolKey,
            CanonicalDatasetName = canonicalDatasetName,
            SliceDatasetName = sliceDatasetName,
            Competition = first.Competition,
            Season = first.Season,
            SampleSeed = sampleSeed,
            SampleSize = sourceItems.Count,
            SelectedItemIds = selectedItemIds,
            SelectedItemIdsHash = selectedItemIdsHash,
            Items = manifestItems
        };

        return new Task5PreparedSliceBundle(artifact, manifest);
    }

    private static JsonElement ParseJsonElement(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}

internal sealed record Task5SliceSourceItem(
    string CanonicalDatasetItemId,
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
    int ExpectedAwayGoals);

internal sealed record Task5PreparedSliceBundle(
    Task5PreparedSliceDataset Artifact,
    Task5SliceManifest Manifest);

internal sealed record Task5PreparedSliceDataset(
    [property: JsonPropertyName("datasetName")] string DatasetName,
    [property: JsonPropertyName("datasetDescription")] string DatasetDescription,
    [property: JsonPropertyName("datasetMetadata")] JsonElement DatasetMetadata,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema,
    [property: JsonPropertyName("expectedOutputSchema")] JsonElement ExpectedOutputSchema,
    [property: JsonPropertyName("items")] IReadOnlyList<Task5PreparedSliceDatasetItem> Items);

internal sealed record Task5PreparedSliceDatasetItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("input")] JsonElement Input,
    [property: JsonPropertyName("expectedOutput")] JsonElement ExpectedOutput,
    [property: JsonPropertyName("metadata")] JsonElement Metadata);
