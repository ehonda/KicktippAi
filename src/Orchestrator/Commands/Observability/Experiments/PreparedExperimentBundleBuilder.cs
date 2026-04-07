using System.Text.Json;
using System.Text.Json.Serialization;

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
        int? sampleSeed)
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
                    item.TippSpielId
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
            TippSpielId = item.TippSpielId
        }).ToList();

        var datasetMetadata = JsonSerializer.SerializeToElement(new
        {
            first.Competition,
            communityContext,
            scope = string.Equals(sliceKind, "single-match", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sliceKind, "repeated-match", StringComparison.OrdinalIgnoreCase)
                ? "repeated-match"
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
            sourceDatasetName,
            sourcePoolKey
        }, OutputJsonOptions);

        var artifact = new PreparedExperimentDataset(
            sliceDatasetName,
            $"{sliceKind} dataset for {sourceItems.Count} item(s) on {sliceKey}",
            datasetMetadata,
            InputSchema,
            ExpectedOutputSchema,
            artifactItems);

        var manifest = new PreparedExperimentManifest
        {
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
            SelectedItemIds = selectedItemIds,
            SelectedItemIdsHash = selectedItemIdsHash,
            Items = manifestItems
        };

        return new PreparedExperimentBundle(artifact, manifest);
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
    int ExpectedAwayGoals);

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
