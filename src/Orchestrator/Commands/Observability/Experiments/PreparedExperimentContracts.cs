using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Observability.Experiments;

internal sealed record PreparedExperimentManifest
{
    [JsonPropertyName("task")]
    public string? TaskType { get; init; }

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

    [JsonPropertyName("sourceDatasetName")]
    public string SourceDatasetName { get; init; } = string.Empty;

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

    [JsonPropertyName("cutoffMatchday")]
    public int? CutoffMatchday { get; init; }

    [JsonPropertyName("participants")]
    public IReadOnlyList<PreparedExperimentParticipantManifest> Participants { get; init; } = [];

    [JsonPropertyName("items")]
    public IReadOnlyList<PreparedExperimentManifestItem> Items { get; init; } = [];
}

internal sealed record PreparedExperimentParticipantManifest
{
    [JsonPropertyName("participantId")]
    public string ParticipantId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("predictions")]
    public IReadOnlyList<PreparedExperimentParticipantPrediction> Predictions { get; init; } = [];
}

internal sealed record PreparedExperimentParticipantPrediction
{
    [JsonPropertyName("sourceDatasetItemId")]
    public string SourceDatasetItemId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("homeGoals")]
    public int? HomeGoals { get; init; }

    [JsonPropertyName("awayGoals")]
    public int? AwayGoals { get; init; }

    [JsonPropertyName("kicktippPoints")]
    public int KicktippPoints { get; init; }
}

internal sealed record PreparedExperimentManifestItem
{
    [JsonPropertyName("sourceDatasetItemId")]
    public string SourceDatasetItemId { get; init; } = string.Empty;

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

    [JsonPropertyName("tippSpielId")]
    public string? TippSpielId { get; init; }
}

internal sealed record PreparedExperimentEvaluationTimestampPolicyMetadata
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("offset")]
    public string? Offset { get; init; }
}

internal sealed record PreparedExperimentRunMetadata
{
    [JsonPropertyName("runner")]
    public string? Runner { get; init; }

    [JsonPropertyName("task")]
    public string? TaskType { get; init; }

    [JsonPropertyName("communityContext")]
    public string? CommunityContext { get; init; }

    [JsonPropertyName("competition")]
    public string? Competition { get; init; }

    [JsonPropertyName("sourceDatasetName")]
    public string? SourceDatasetName { get; init; }

    [JsonPropertyName("datasetName")]
    public string? DatasetName { get; init; }

    [JsonPropertyName("promptKey")]
    public string? PromptKey { get; init; }

    [JsonPropertyName("promptSource")]
    public string? PromptSource { get; init; }

    [JsonPropertyName("langfusePromptName")]
    public string? LangfusePromptName { get; init; }

    [JsonPropertyName("langfusePromptLabel")]
    public string? LangfusePromptLabel { get; init; }

    [JsonPropertyName("langfusePromptVersion")]
    public int? LangfusePromptVersion { get; init; }

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
    public PreparedExperimentEvaluationTimestampPolicyMetadata? EvaluationTimestampPolicy { get; init; }

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

    [JsonPropertyName("observationName")]
    public string? ObservationName { get; init; }

    [JsonPropertyName("runSubjectKind")]
    public string? RunSubjectKind { get; init; }

    [JsonPropertyName("runSubjectId")]
    public string? RunSubjectId { get; init; }

    [JsonPropertyName("runSubjectDisplayName")]
    public string? RunSubjectDisplayName { get; init; }

    [JsonPropertyName("batchStrategy")]
    public string? BatchStrategy { get; init; }

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; init; }

    [JsonPropertyName("batchCount")]
    public int? BatchCount { get; init; }
}

internal sealed record ExperimentItemScores(
    [property: JsonPropertyName("kicktipp_points")] int KicktippPoints);

internal sealed record ExperimentAggregateScores(
    [property: JsonPropertyName("total_kicktipp_points")] double TotalKicktippPoints,
    [property: JsonPropertyName("avg_kicktipp_points")] double AvgKicktippPoints);

internal sealed record PreparedExperimentExecutionSummary(
    string DatasetItemId,
    string SourceDatasetItemId,
    string RunName,
    string TraceId,
    Prediction? Prediction,
    ExperimentItemScores Scores,
    IReadOnlyList<string> TraceTags,
    PreparedExperimentTokenUsageSummary? Usage = null,
    string PredictionStatus = "placed");

internal sealed record PreparedExperimentDatasetRunSummary(
    int Repetition,
    string RunName,
    string DatasetRunId,
    int RunItemCount,
    ExperimentAggregateScores AggregateScores,
    PreparedExperimentExecutionSummary? FirstExecution,
    PreparedExperimentExecutionSummary? LastExecution);

internal sealed record PreparedExperimentRunSummary(
    string DatasetName,
    string RunName,
    string RunFamilyName,
    string TaskType,
    string Model,
    bool DeletedExistingRun,
    int SampleSize,
    string BatchStrategy,
    int? BatchSize,
    int? BatchCount,
    int ExecutionCount,
    int RunCount,
    ExperimentAggregateScores AggregateScores,
    IReadOnlyList<PreparedExperimentDatasetRunSummary> DatasetRuns,
    PreparedExperimentExecutionSummary? FirstExecution,
    PreparedExperimentExecutionSummary? LastExecution);

internal sealed record PreparedExperimentTokenUsageSummary(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

internal sealed record PreparedExperimentExecutionRequest(
    PreparedExperimentManifest Manifest,
    PreparedExperimentRunMetadata RunMetadata,
    string Model,
    string RunName,
    string? RunDescription,
    bool ReplaceRun,
    IReadOnlyList<IReadOnlyList<PreparedExperimentManifestItem>> Batches);
