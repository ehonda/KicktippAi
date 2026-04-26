using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchestrator.Commands.Observability.Experiments;

internal sealed record PreparedExperimentRunOptions(
    string Model,
    string PromptKey,
    bool IncludeJustification,
    string? EvaluationTime,
    string? EvaluationPolicyKind,
    string? EvaluationPolicyOffset,
    string? DatasetName,
    string PromptSource,
    string? LangfusePromptName,
    string? LangfusePromptLabel,
    int? LangfusePromptVersion,
    string BatchStrategy,
    int? BatchSize = null,
    int? BatchCount = null,
    string? ReasoningEffort = null);

internal static class PreparedExperimentCommandSupport
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<T> LoadJsonFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(path);
        var raw = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
        return value ?? throw new InvalidOperationException($"JSON file '{absolutePath}' could not be deserialized.");
    }

    public static PreparedExperimentRunMetadata NormalizeRunMetadata(
        PreparedExperimentRunMetadata runMetadata,
        PreparedExperimentManifest manifest,
        PreparedExperimentRunOptions options)
    {
        if (!string.IsNullOrWhiteSpace(runMetadata.Model)
            && !string.Equals(runMetadata.Model, options.Model, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Run metadata model '{runMetadata.Model}' does not match requested model '{options.Model}'.");
        }

        var normalizedReasoningEffort = string.IsNullOrWhiteSpace(runMetadata.ReasoningEffort)
            ? options.ReasoningEffort
            : runMetadata.ReasoningEffort.Trim().ToLowerInvariant();
        var runSubjectId = string.IsNullOrWhiteSpace(runMetadata.RunSubjectId)
            ? string.IsNullOrWhiteSpace(normalizedReasoningEffort)
                ? options.Model
                : $"{options.Model}:reasoning-effort:{normalizedReasoningEffort}"
            : runMetadata.RunSubjectId;
        var runSubjectDisplayName = string.IsNullOrWhiteSpace(runMetadata.RunSubjectDisplayName)
            ? PreparedExperimentSupport.BuildRunSubjectDisplayName(options.Model, normalizedReasoningEffort)
            : runMetadata.RunSubjectDisplayName;

        return runMetadata with
        {
            Runner = string.IsNullOrWhiteSpace(runMetadata.Runner) ? "match-experiment-runner" : runMetadata.Runner,
            TaskType = string.IsNullOrWhiteSpace(runMetadata.TaskType)
                ? PreparedExperimentSupport.ResolveTaskType(manifest)
                : runMetadata.TaskType,
            CommunityContext = string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
                ? manifest.CommunityContext
                : runMetadata.CommunityContext,
            Model = options.Model,
            Competition = string.IsNullOrWhiteSpace(runMetadata.Competition) ? manifest.Competition : runMetadata.Competition,
            SourceDatasetName = string.IsNullOrWhiteSpace(runMetadata.SourceDatasetName)
                ? manifest.SourceDatasetName
                : runMetadata.SourceDatasetName,
            DatasetName = string.IsNullOrWhiteSpace(runMetadata.DatasetName)
                ? options.DatasetName ?? manifest.SliceDatasetName
                : runMetadata.DatasetName,
            PromptKey = string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? options.PromptKey : runMetadata.PromptKey,
            PromptSource = string.IsNullOrWhiteSpace(runMetadata.PromptSource) ? options.PromptSource : runMetadata.PromptSource,
            LangfusePromptName = string.IsNullOrWhiteSpace(runMetadata.LangfusePromptName) ? options.LangfusePromptName : runMetadata.LangfusePromptName,
            LangfusePromptLabel = string.IsNullOrWhiteSpace(runMetadata.LangfusePromptLabel) ? options.LangfusePromptLabel : runMetadata.LangfusePromptLabel,
            LangfusePromptVersion = runMetadata.LangfusePromptVersion ?? options.LangfusePromptVersion,
            ReasoningEffort = normalizedReasoningEffort,
            SliceKind = string.IsNullOrWhiteSpace(runMetadata.SliceKind) ? manifest.SliceKind : runMetadata.SliceKind,
            SliceKey = string.IsNullOrWhiteSpace(runMetadata.SliceKey) ? manifest.SliceKey : runMetadata.SliceKey,
            SourcePoolKey = string.IsNullOrWhiteSpace(runMetadata.SourcePoolKey) ? manifest.SourcePoolKey : runMetadata.SourcePoolKey,
            SelectedItemIdsCount = runMetadata.SelectedItemIdsCount > 0
                ? runMetadata.SelectedItemIdsCount
                : manifest.SelectedItemIds.Count > 0 ? manifest.SelectedItemIds.Count : manifest.Items.Count,
            SelectedItemIdsHash = string.IsNullOrWhiteSpace(runMetadata.SelectedItemIdsHash)
                ? string.IsNullOrWhiteSpace(manifest.SelectedItemIdsHash)
                    ? ExperimentArtifactSupport.ComputeSelectedItemIdsHash(
                        manifest.SelectedItemIds.Count > 0
                            ? manifest.SelectedItemIds
                            : manifest.Items.Select(item => item.SliceDatasetItemId))
                    : manifest.SelectedItemIdsHash
                : runMetadata.SelectedItemIdsHash,
            SampleSize = runMetadata.SampleSize > 0 ? runMetadata.SampleSize : manifest.SampleSize > 0 ? manifest.SampleSize : manifest.Items.Count,
            SampleSeed = runMetadata.SampleSeed ?? manifest.SampleSeed,
            SampleMethod = string.IsNullOrWhiteSpace(runMetadata.SampleMethod) ? manifest.SampleMethod : runMetadata.SampleMethod,
            PromptVersion = string.IsNullOrWhiteSpace(runMetadata.PromptVersion)
                ? string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? options.PromptKey : runMetadata.PromptKey
                : runMetadata.PromptVersion,
            SourceDatasetKind = string.IsNullOrWhiteSpace(runMetadata.SourceDatasetKind)
                ? PreparedExperimentSupport.ResolveTaskType(manifest)
                : runMetadata.SourceDatasetKind,
            DatasetItemIdMap = runMetadata.DatasetItemIdMap.Count > 0
                ? runMetadata.DatasetItemIdMap
                : PreparedExperimentSupport.CreateDatasetItemIdMap(manifest),
            BatchStrategy = string.IsNullOrWhiteSpace(runMetadata.BatchStrategy) ? options.BatchStrategy : runMetadata.BatchStrategy,
            BatchSize = options.BatchSize ?? runMetadata.BatchSize,
            BatchCount = options.BatchCount ?? runMetadata.BatchCount,
            RunSubjectId = runSubjectId,
            RunSubjectDisplayName = runSubjectDisplayName
        };
    }

    public static DateTimeOffset? ParseExplicitEvaluationTime(PreparedExperimentRunMetadata runMetadata)
    {
        if (string.IsNullOrWhiteSpace(runMetadata.EvaluationTime))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                runMetadata.EvaluationTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedRoundtrip))
        {
            return parsedRoundtrip;
        }

        return Commands.Observability.EvaluationTimeParser.Parse(runMetadata.EvaluationTime);
    }

    public static EvaluationTimestampPolicy ParseEvaluationTimestampPolicy(PreparedExperimentRunMetadata runMetadata)
    {
        if (runMetadata.EvaluationTimestampPolicy is null)
        {
            throw new InvalidOperationException("Run metadata must contain evaluationTimestampPolicy.");
        }

        if (!string.Equals(
                runMetadata.EvaluationTimestampPolicy.Reference,
                EvaluationTimestampPolicy.StartsAtReference,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Evaluation policy reference must be '{EvaluationTimestampPolicy.StartsAtReference}'.");
        }

        return EvaluationTimestampPolicyParser.Parse(
            runMetadata.EvaluationTimestampPolicy.Kind,
            runMetadata.EvaluationTimestampPolicy.Offset);
    }

    public static void ValidateManifest(PreparedExperimentManifest manifest)
    {
        if (manifest.Items.Count == 0)
        {
            throw new InvalidOperationException("Slice manifest must contain at least one item.");
        }

        var seenHostedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.SourceDatasetItemId))
            {
                throw new InvalidOperationException("Each slice manifest item must contain sourceDatasetItemId.");
            }

            if (string.IsNullOrWhiteSpace(item.SliceDatasetItemId))
            {
                throw new InvalidOperationException("Each slice manifest item must contain sliceDatasetItemId.");
            }

            if (!seenHostedIds.Add(item.SliceDatasetItemId))
            {
                throw new InvalidOperationException($"Duplicate slice dataset item id '{item.SliceDatasetItemId}' found in manifest.");
            }

            if (string.IsNullOrWhiteSpace(item.HomeTeam) || string.IsNullOrWhiteSpace(item.AwayTeam))
            {
                throw new InvalidOperationException("Each slice manifest item must contain non-empty homeTeam and awayTeam values.");
            }

            if (item.Matchday < 1)
            {
                throw new InvalidOperationException($"Slice manifest item '{item.SliceDatasetItemId}' has an invalid matchday.");
            }
        }
    }

    public static void EnsureTaskType(PreparedExperimentManifest manifest, string expectedTaskType)
    {
        var actualTaskType = PreparedExperimentSupport.ResolveTaskType(manifest);
        if (!string.Equals(actualTaskType, expectedTaskType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The manifest describes a '{actualTaskType}' dataset, but this command expects '{expectedTaskType}'.");
        }
    }
}
