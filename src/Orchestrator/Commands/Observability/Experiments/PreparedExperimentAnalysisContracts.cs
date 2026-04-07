using System.Text.Json.Serialization;

namespace Orchestrator.Commands.Observability.Experiments;

internal sealed record PreparedExperimentAnalysisBundle(
    [property: JsonPropertyName("datasetName")] string DatasetName,
    [property: JsonPropertyName("taskType")] string TaskType,
    [property: JsonPropertyName("primaryMetricName")] string PrimaryMetricName,
    [property: JsonPropertyName("exportedAtUtc")] string ExportedAtUtc,
    [property: JsonPropertyName("runs")] IReadOnlyList<PreparedExperimentAnalysisRun> Runs,
    [property: JsonPropertyName("rows")] IReadOnlyList<PreparedExperimentAnalysisRow> Rows);

internal sealed record PreparedExperimentAnalysisRun(
    [property: JsonPropertyName("runName")] string RunName,
    [property: JsonPropertyName("datasetRunId")] string DatasetRunId,
    [property: JsonPropertyName("taskType")] string TaskType,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("promptKey")] string? PromptKey,
    [property: JsonPropertyName("sliceKind")] string? SliceKind,
    [property: JsonPropertyName("sliceKey")] string? SliceKey,
    [property: JsonPropertyName("sourcePoolKey")] string? SourcePoolKey,
    [property: JsonPropertyName("selectedItemIdsHash")] string? SelectedItemIdsHash,
    [property: JsonPropertyName("selectedItemIdsCount")] int SelectedItemIdsCount,
    [property: JsonPropertyName("sampleSize")] int SampleSize,
    [property: JsonPropertyName("evaluationTimestampPolicyKey")] string? EvaluationTimestampPolicyKey,
    [property: JsonPropertyName("evaluationTime")] string? EvaluationTime,
    [property: JsonPropertyName("startedAtUtc")] string? StartedAtUtc,
    [property: JsonPropertyName("aggregateScores")] ExperimentAggregateScores AggregateScores,
    [property: JsonPropertyName("primaryMetricValue")] double PrimaryMetricValue,
    [property: JsonPropertyName("rowCount")] int RowCount,
    [property: JsonPropertyName("runSubjectKind")] string? RunSubjectKind = null,
    [property: JsonPropertyName("runSubjectId")] string? RunSubjectId = null,
    [property: JsonPropertyName("runSubjectDisplayName")] string? RunSubjectDisplayName = null);

internal sealed record PreparedExperimentAnalysisRow(
    [property: JsonPropertyName("pairingKey")] string PairingKey,
    [property: JsonPropertyName("datasetRunId")] string DatasetRunId,
    [property: JsonPropertyName("runName")] string RunName,
    [property: JsonPropertyName("taskType")] string TaskType,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("promptKey")] string? PromptKey,
    [property: JsonPropertyName("sliceKind")] string? SliceKind,
    [property: JsonPropertyName("sliceKey")] string? SliceKey,
    [property: JsonPropertyName("sourcePoolKey")] string? SourcePoolKey,
    [property: JsonPropertyName("datasetItemId")] string DatasetItemId,
    [property: JsonPropertyName("sourceDatasetItemId")] string SourceDatasetItemId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("observationId")] string? ObservationId,
    [property: JsonPropertyName("matchday")] int Matchday,
    [property: JsonPropertyName("homeTeam")] string HomeTeam,
    [property: JsonPropertyName("awayTeam")] string AwayTeam,
    [property: JsonPropertyName("startsAt")] string StartsAt,
    [property: JsonPropertyName("tippSpielId")] string? TippSpielId,
    [property: JsonPropertyName("predictedHomeGoals")] int? PredictedHomeGoals,
    [property: JsonPropertyName("predictedAwayGoals")] int? PredictedAwayGoals,
    [property: JsonPropertyName("expectedHomeGoals")] int ExpectedHomeGoals,
    [property: JsonPropertyName("expectedAwayGoals")] int ExpectedAwayGoals,
    [property: JsonPropertyName("kicktippPoints")] int KicktippPoints,
    [property: JsonPropertyName("predictionStatus")] string PredictionStatus = "placed",
    [property: JsonPropertyName("runSubjectKind")] string? RunSubjectKind = null,
    [property: JsonPropertyName("runSubjectId")] string? RunSubjectId = null,
    [property: JsonPropertyName("runSubjectDisplayName")] string? RunSubjectDisplayName = null);
