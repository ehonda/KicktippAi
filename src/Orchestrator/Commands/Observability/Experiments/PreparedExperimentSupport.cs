using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Observability.Experiments;

internal static class PreparedExperimentSupport
{
    public static IReadOnlyList<IReadOnlyList<T>> CreateBatchChunks<T>(IReadOnlyList<T> items, int batchSize)
    {
        var chunks = new List<IReadOnlyList<T>>();
        for (var index = 0; index < items.Count; index += batchSize)
        {
            chunks.Add(items.Skip(index).Take(batchSize).ToList());
        }

        return chunks;
    }

    public static IReadOnlyList<IReadOnlyList<T>> CreateWarmupThenBatchChunks<T>(IReadOnlyList<T> items, int batchCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (batchCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchCount), batchCount, "Batch count must be at least 1.");
        }

        if (items.Count == 0)
        {
            return [];
        }

        var chunks = new List<IReadOnlyList<T>>
        {
            new List<T> { items[0] }
        };

        if (items.Count == 1)
        {
            return chunks;
        }

        var remainingItems = items.Skip(1).ToList();
        var actualBatchCount = Math.Min(batchCount, remainingItems.Count);
        var baseBatchSize = remainingItems.Count / actualBatchCount;
        var remainder = remainingItems.Count % actualBatchCount;
        var startIndex = 0;

        for (var batchIndex = 0; batchIndex < actualBatchCount; batchIndex += 1)
        {
            var currentBatchSize = baseBatchSize + (batchIndex < remainder ? 1 : 0);
            chunks.Add(remainingItems.Skip(startIndex).Take(currentBatchSize).ToList());
            startIndex += currentBatchSize;
        }

        return chunks;
    }

    public static ExperimentItemScores CalculateScores(Prediction prediction, int expectedHomeGoals, int expectedAwayGoals)
    {
        var predictedDifference = prediction.HomeGoals - prediction.AwayGoals;
        var expectedDifference = expectedHomeGoals - expectedAwayGoals;
        var predictedTendency = Math.Sign(predictedDifference);
        var expectedTendency = Math.Sign(expectedDifference);

        var exactHit = prediction.HomeGoals == expectedHomeGoals
            && prediction.AwayGoals == expectedAwayGoals;
        var outcomeCorrect = predictedTendency == expectedTendency;

        var kicktippPoints = 0;
        if (exactHit)
        {
            kicktippPoints = 4;
        }
        else if (outcomeCorrect && predictedDifference == expectedDifference && expectedTendency != 0)
        {
            kicktippPoints = 3;
        }
        else if (outcomeCorrect)
        {
            kicktippPoints = 2;
        }

        return new ExperimentItemScores(kicktippPoints);
    }

    public static ExperimentAggregateScores SummarizeScores(IReadOnlyList<ExperimentItemScores> scoreEntries)
    {
        var total = scoreEntries.Sum(entry => entry.KicktippPoints);
        var average = scoreEntries.Count == 0 ? 0d : (double)total / scoreEntries.Count;
        return new ExperimentAggregateScores(total, average);
    }

    public static string DeriveExperimentName(PreparedExperimentRunMetadata runMetadata, string runName)
    {
        var parts = new[]
            {
                runMetadata.TaskType,
                runMetadata.CommunityContext,
                runMetadata.SliceKey
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ExperimentArtifactSupport.Slugify(value!))
            .ToArray();

        return parts.Length >= 2
            ? string.Join("__", parts)
            : runName;
    }

    public static JsonElement BuildLangfuseExperimentMetadata(
        PreparedExperimentRunMetadata runMetadata,
        string experimentName,
        string experimentRunName,
        IReadOnlyDictionary<string, string?>? extraFields = null)
    {
        var node = JsonSerializer.SerializeToNode(runMetadata, PreparedExperimentCommandSupport.JsonOptions)
                   as JsonObject
                   ?? new JsonObject();

        node["experiment_name"] = experimentName;
        node["experiment_run_name"] = experimentRunName;

        if (extraFields is not null)
        {
            foreach (var (key, value) in extraFields)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    node[key] = value;
                }
            }
        }

        return JsonSerializer.SerializeToElement(node, PreparedExperimentCommandSupport.JsonOptions);
    }

    public static string CreateScoreId(string scoreName, params string?[] components)
    {
        var joined = string.Join(
            "\n",
            new[] { scoreName }
                .Concat(components.Where(component => !string.IsNullOrWhiteSpace(component)).Select(component => component!.Trim())));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return $"exp-score-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static PreparedExperimentRunMetadata BuildRunMetadata(
        PreparedExperimentManifest manifest,
        PreparedExperimentRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);

        var explicitEvaluationTime = EvaluationTimeParser.ParseOrNull(options.EvaluationTime);
        EvaluationTimestampPolicy? evaluationTimestampPolicy = null;
        string evaluationTimestampPolicyKey;

        if (explicitEvaluationTime is null)
        {
            var kind = string.IsNullOrWhiteSpace(options.EvaluationPolicyKind)
                ? EvaluationTimestampPolicy.RelativeKind
                : options.EvaluationPolicyKind;
            var offset = string.IsNullOrWhiteSpace(options.EvaluationPolicyOffset)
                ? "-12:00:00"
                : options.EvaluationPolicyOffset;
            evaluationTimestampPolicy = EvaluationTimestampPolicyParser.Parse(kind, offset);
            evaluationTimestampPolicyKey = ExperimentArtifactSupport.BuildRelativeEvaluationPolicyKey(evaluationTimestampPolicy);
        }
        else
        {
            evaluationTimestampPolicyKey = "exact-time";
        }

        return new PreparedExperimentRunMetadata
        {
            Runner = "match-experiment-runner",
            TaskType = ResolveTaskType(manifest),
            CommunityContext = ResolveCommunityContext(manifest),
            Competition = manifest.Competition,
            SourceDatasetName = manifest.SourceDatasetName,
            DatasetName = options.DatasetName ?? manifest.SliceDatasetName,
            PromptKey = options.PromptKey,
            SliceKind = ResolveSliceKind(manifest),
            SliceKey = manifest.SliceKey,
            SourcePoolKey = manifest.SourcePoolKey,
            SelectedItemIdsHash = string.IsNullOrWhiteSpace(manifest.SelectedItemIdsHash)
                ? ExperimentArtifactSupport.ComputeSelectedItemIdsHash(
                    manifest.SelectedItemIds.Count > 0
                        ? manifest.SelectedItemIds
                        : manifest.Items.Select(item => item.SliceDatasetItemId))
                : manifest.SelectedItemIdsHash,
            SelectedItemIdsCount = manifest.SelectedItemIds.Count > 0 ? manifest.SelectedItemIds.Count : manifest.Items.Count,
            SampleSize = manifest.SampleSize > 0 ? manifest.SampleSize : manifest.Items.Count,
            EvaluationTimestampPolicyKey = evaluationTimestampPolicyKey,
            EvaluationTimestampPolicy = evaluationTimestampPolicy is null
                ? null
                : new PreparedExperimentEvaluationTimestampPolicyMetadata
                {
                    Kind = evaluationTimestampPolicy.Kind,
                    Reference = evaluationTimestampPolicy.Reference,
                    Offset = evaluationTimestampPolicy.Offset.ToTimeSpan().ToString("c", CultureInfo.InvariantCulture)
                },
            EvaluationTime = explicitEvaluationTime?.ToString("O", CultureInfo.InvariantCulture),
            StartedAtUtc = ExperimentArtifactSupport.FormatStartedAtUtc(DateTimeOffset.UtcNow),
            SampleSeed = manifest.SampleSeed,
            SampleMethod = ResolveSampleMethod(manifest),
            IncludeJustification = options.IncludeJustification,
            PromptVersion = options.PromptKey,
            SourceDatasetKind = DeriveSourceDatasetKind(manifest),
            DatasetItemIdMap = CreateDatasetItemIdMap(manifest),
            Model = options.Model,
            ObservationName = "predict-match",
            RunSubjectKind = "model",
            RunSubjectId = options.Model,
            RunSubjectDisplayName = options.Model,
            BatchStrategy = options.BatchStrategy,
            BatchSize = options.BatchSize,
            BatchCount = options.BatchCount
        };
    }

    public static IReadOnlyDictionary<string, string> CreateDatasetItemIdMap(PreparedExperimentManifest manifest)
    {
        var groupedItems = manifest.Items
                .GroupBy(item => item.SourceDatasetItemId, StringComparer.Ordinal)
            .ToList();

        if (groupedItems.Any(group => group.Count() != 1))
        {
            return new Dictionary<string, string>();
        }

        return groupedItems.ToDictionary(
            group => group.Key,
            group => group.Single().SliceDatasetItemId,
            StringComparer.Ordinal);
    }

    public static IReadOnlyList<string> DeriveTraceTags(PreparedExperimentRunMetadata runMetadata)
    {
        var tags = new List<string>();

        if (!string.IsNullOrWhiteSpace(runMetadata.TaskType))
        {
            tags.Add($"task:{runMetadata.TaskType}");
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.CommunityContext))
        {
            tags.Add($"community:{runMetadata.CommunityContext}");
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.SliceKey))
        {
            tags.Add($"slice:{runMetadata.SliceKey}");
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.Model))
        {
            tags.Add($"model:{runMetadata.Model}");
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.SliceKind))
        {
            tags.Add($"slice-kind:{runMetadata.SliceKind}");
        }

        if (!string.IsNullOrWhiteSpace(runMetadata.PromptKey))
        {
            tags.Add($"prompt:{runMetadata.PromptKey}");
        }

        return tags.Distinct(StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyDictionary<string, string> DerivePropagatedMetadata(PreparedExperimentRunMetadata runMetadata)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfValid(metadata, "communityContext", runMetadata.CommunityContext);
        AddIfValid(metadata, "evaluationTime", runMetadata.EvaluationTime);
        AddIfValid(metadata, "evaluationTimestampPolicyKey", runMetadata.EvaluationTimestampPolicyKey);
        AddIfValid(metadata, "model", runMetadata.Model);
        AddIfValid(metadata, "promptKey", runMetadata.PromptKey);
        AddIfValid(metadata, "sampleMethod", runMetadata.SampleMethod);
        AddIfValid(metadata, "selectedItemIdsHash", runMetadata.SelectedItemIdsHash);
        AddIfValid(metadata, "sliceKind", runMetadata.SliceKind);
        AddIfValid(metadata, "sliceKey", runMetadata.SliceKey);
        AddIfValid(metadata, "startedAtUtc", runMetadata.StartedAtUtc);
        AddIfValid(metadata, "task", runMetadata.TaskType);
        AddIfValid(metadata, "observationName", runMetadata.ObservationName);
        AddIfValid(metadata, "runSubjectKind", runMetadata.RunSubjectKind);
        AddIfValid(metadata, "runSubjectId", runMetadata.RunSubjectId);
        AddIfValid(metadata, "runSubjectDisplayName", runMetadata.RunSubjectDisplayName);
        return metadata;
    }

    public static string ResolveTaskType(PreparedExperimentManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.TaskType))
        {
            return manifest.TaskType;
        }

        var sliceKind = ResolveSliceKind(manifest);
        var sampleMethod = ResolveSampleMethod(manifest);

        if (string.Equals(sliceKind, "community-to-date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sampleMethod, "community-to-date", StringComparison.OrdinalIgnoreCase))
        {
            return "community-to-date";
        }

        if (string.Equals(sliceKind, "single-match", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sliceKind, "repeated-match", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sampleMethod, "repeat-single-match", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sampleMethod, "repeated-match", StringComparison.OrdinalIgnoreCase))
        {
            return "repeated-match";
        }

        return "slice";
    }

    public static void ReportProgress(string message)
    {
        Console.Error.WriteLine($"[progress] {message}");
    }

    private static void AddIfValid(IDictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length <= 200)
        {
            metadata[key] = value;
        }
    }

    private static string DeriveSourceDatasetKind(PreparedExperimentManifest manifest)
    {
        return ResolveTaskType(manifest);
    }

    private static string ResolveCommunityContext(PreparedExperimentManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.CommunityContext))
        {
            return manifest.CommunityContext;
        }

        if (string.IsNullOrWhiteSpace(manifest.SourceDatasetName))
        {
            throw new InvalidOperationException("Slice manifest must contain communityContext or sourceDatasetName.");
        }

        return manifest.SourceDatasetName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();
    }

    private static string ResolveSampleMethod(PreparedExperimentManifest manifest)
    {
        return string.IsNullOrWhiteSpace(manifest.SampleMethod)
            ? "random-sample"
            : manifest.SampleMethod;
    }

    private static string ResolveSliceKind(PreparedExperimentManifest manifest)
    {
        return string.IsNullOrWhiteSpace(manifest.SliceKind)
            ? "random-sample"
            : manifest.SliceKind;
    }
}
