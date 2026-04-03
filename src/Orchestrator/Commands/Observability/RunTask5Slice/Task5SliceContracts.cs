using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Observability.RunTask5Slice;

internal sealed record Task5SliceManifest
{
    [JsonPropertyName("sliceKey")]
    public string SliceKey { get; init; } = string.Empty;

    [JsonPropertyName("sliceKind")]
    public string SliceKind { get; init; } = string.Empty;

    [JsonPropertyName("sampleMethod")]
    public string SampleMethod { get; init; } = string.Empty;

    [JsonPropertyName("communityContext")]
    public string CommunityContext { get; init; } = string.Empty;

    [JsonPropertyName("sourcePoolKey")]
    public string SourcePoolKey { get; init; } = string.Empty;

    [JsonPropertyName("canonicalDatasetName")]
    public string CanonicalDatasetName { get; init; } = string.Empty;

    [JsonPropertyName("sliceDatasetName")]
    public string SliceDatasetName { get; init; } = string.Empty;

    [JsonPropertyName("competition")]
    public string Competition { get; init; } = string.Empty;

    [JsonPropertyName("season")]
    public string Season { get; init; } = string.Empty;

    [JsonPropertyName("sampleSeed")]
    public int? SampleSeed { get; init; }

    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; init; }

    [JsonPropertyName("selectedItemIds")]
    public IReadOnlyList<string> SelectedItemIds { get; init; } = [];

    [JsonPropertyName("selectedItemIdsHash")]
    public string SelectedItemIdsHash { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<Task5SliceManifestItem> Items { get; init; } = [];
}

internal sealed record Task5SliceManifestItem
{
    [JsonPropertyName("canonicalDatasetItemId")]
    public string CanonicalDatasetItemId { get; init; } = string.Empty;

    [JsonPropertyName("sliceDatasetItemId")]
    public string SliceDatasetItemId { get; init; } = string.Empty;

    [JsonPropertyName("homeTeam")]
    public string HomeTeam { get; init; } = string.Empty;

    [JsonPropertyName("awayTeam")]
    public string AwayTeam { get; init; } = string.Empty;

    [JsonPropertyName("matchday")]
    public int Matchday { get; init; }

    [JsonPropertyName("startsAt")]
    public string StartsAt { get; init; } = string.Empty;
}

internal sealed record Task5EvaluationTimestampPolicyMetadata
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("offset")]
    public string? Offset { get; init; }
}

internal sealed record Task5RunMetadata
{
    [JsonPropertyName("runner")]
    public string? Runner { get; init; }

    [JsonPropertyName("task")]
    public string? TaskName { get; init; }

    [JsonPropertyName("communityContext")]
    public string? CommunityContext { get; init; }

    [JsonPropertyName("competition")]
    public string? Competition { get; init; }

    [JsonPropertyName("canonicalDatasetName")]
    public string? CanonicalDatasetName { get; init; }

    [JsonPropertyName("datasetName")]
    public string? DatasetName { get; init; }

    [JsonPropertyName("promptKey")]
    public string? PromptKey { get; init; }

    [JsonPropertyName("sliceKind")]
    public string? SliceKind { get; init; }

    [JsonPropertyName("sliceKey")]
    public string? SliceKey { get; init; }

    [JsonPropertyName("sourcePoolKey")]
    public string? SourcePoolKey { get; init; }

    [JsonPropertyName("selectedItemIdsHash")]
    public string? SelectedItemIdsHash { get; init; }

    [JsonPropertyName("selectedItemIdsCount")]
    public int SelectedItemIdsCount { get; init; }

    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; init; }

    [JsonPropertyName("evaluationTimestampPolicyKey")]
    public string? EvaluationTimestampPolicyKey { get; init; }

    [JsonPropertyName("evaluationTimestampPolicy")]
    public Task5EvaluationTimestampPolicyMetadata? EvaluationTimestampPolicy { get; init; }

    [JsonPropertyName("evaluationTime")]
    public string? EvaluationTime { get; init; }

    [JsonPropertyName("startedAtUtc")]
    public string? StartedAtUtc { get; init; }

    [JsonPropertyName("sampleSeed")]
    public int? SampleSeed { get; init; }

    [JsonPropertyName("sampleMethod")]
    public string? SampleMethod { get; init; }

    [JsonPropertyName("includeJustification")]
    public bool IncludeJustification { get; init; }

    [JsonPropertyName("promptVersion")]
    public string? PromptVersion { get; init; }

    [JsonPropertyName("sourceDatasetKind")]
    public string? SourceDatasetKind { get; init; }

    [JsonPropertyName("datasetItemIdMap")]
    public IReadOnlyDictionary<string, string> DatasetItemIdMap { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; init; }
}

internal sealed record Task5ItemScores(
    [property: JsonPropertyName("kicktipp_points")] int KicktippPoints);

internal sealed record Task5AggregateScores(
    [property: JsonPropertyName("total_kicktipp_points")] double TotalKicktippPoints,
    [property: JsonPropertyName("avg_kicktipp_points")] double AvgKicktippPoints);

internal sealed record Task5SliceExecutionSummary(
    string DatasetItemId,
    string SourceDatasetItemId,
    string RunName,
    string TraceId,
    Prediction Prediction,
    Task5ItemScores Scores,
    IReadOnlyList<string> TraceTags,
    Task5TokenUsageSummary? Usage = null);

internal sealed record Task5SliceDatasetRunSummary(
    int Repetition,
    string RunName,
    string DatasetRunId,
    int RunItemCount,
    Task5AggregateScores AggregateScores,
    Task5SliceExecutionSummary? FirstExecution,
    Task5SliceExecutionSummary? LastExecution);

internal sealed record Task5SliceRunSummary(
    string DatasetName,
    string RunName,
    string RunFamilyName,
    string Model,
    bool DeletedExistingRun,
    int SampleSize,
    int BatchSize,
    int ExecutionCount,
    int RunCount,
    Task5AggregateScores AggregateScores,
    IReadOnlyList<Task5SliceDatasetRunSummary> DatasetRuns,
    Task5SliceExecutionSummary? FirstExecution,
    Task5SliceExecutionSummary? LastExecution);

internal sealed record Task5TokenUsageSummary(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);
