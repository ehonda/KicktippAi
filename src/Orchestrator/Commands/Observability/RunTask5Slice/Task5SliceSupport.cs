using System.Globalization;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Observability.RunTask5Slice;

internal static class Task5SliceSupport
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

    public static Task5ItemScores CalculateScores(Prediction prediction, int expectedHomeGoals, int expectedAwayGoals)
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

        return new Task5ItemScores(kicktippPoints);
    }

    public static Task5AggregateScores SummarizeScores(IReadOnlyList<Task5ItemScores> scoreEntries)
    {
        var total = scoreEntries.Sum(entry => entry.KicktippPoints);
        var average = scoreEntries.Count == 0 ? 0d : (double)total / scoreEntries.Count;
        return new Task5AggregateScores(total, average);
    }

    public static Task5RunMetadata BuildRunMetadata(Task5SliceManifest manifest, RunTask5SliceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(settings);

        var explicitEvaluationTime = EvaluationTimeParser.ParseOrNull(settings.EvaluationTime);
        EvaluationTimestampPolicy? evaluationTimestampPolicy = null;
        string evaluationTimestampPolicyKey;

        if (explicitEvaluationTime is null)
        {
            var kind = string.IsNullOrWhiteSpace(settings.EvaluationPolicyKind)
                ? EvaluationTimestampPolicy.RelativeKind
                : settings.EvaluationPolicyKind;
            var offset = string.IsNullOrWhiteSpace(settings.EvaluationPolicyOffset)
                ? "-12:00:00"
                : settings.EvaluationPolicyOffset;
            evaluationTimestampPolicy = EvaluationTimestampPolicyParser.Parse(kind, offset);
            evaluationTimestampPolicyKey = ExperimentArtifactSupport.BuildRelativeEvaluationPolicyKey(evaluationTimestampPolicy);
        }
        else
        {
            evaluationTimestampPolicyKey = "exact-time";
        }

        return new Task5RunMetadata
        {
            Runner = "task-5-first-experiment",
            TaskName = "task-5",
            CommunityContext = ResolveCommunityContext(manifest),
            Competition = manifest.Competition,
            CanonicalDatasetName = manifest.CanonicalDatasetName,
            DatasetName = settings.DatasetName ?? manifest.SliceDatasetName,
            PromptKey = settings.PromptKey,
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
                : new Task5EvaluationTimestampPolicyMetadata
                {
                    Kind = evaluationTimestampPolicy.Kind,
                    Reference = evaluationTimestampPolicy.Reference,
                    Offset = evaluationTimestampPolicy.Offset.ToTimeSpan().ToString("c", CultureInfo.InvariantCulture)
                },
            EvaluationTime = explicitEvaluationTime?.ToString("O", CultureInfo.InvariantCulture),
            StartedAtUtc = ExperimentArtifactSupport.FormatStartedAtUtc(DateTimeOffset.UtcNow),
            SampleSeed = manifest.SampleSeed,
            SampleMethod = ResolveSampleMethod(manifest),
            IncludeJustification = settings.IncludeJustification,
            PromptVersion = settings.PromptKey,
            SourceDatasetKind = DeriveSourceDatasetKind(manifest),
            DatasetItemIdMap = CreateDatasetItemIdMap(manifest),
            Model = settings.Model,
            BatchSize = settings.BatchSize
        };
    }

    public static IReadOnlyDictionary<string, string> CreateDatasetItemIdMap(Task5SliceManifest manifest)
    {
        var groupedItems = manifest.Items
            .GroupBy(item => item.CanonicalDatasetItemId, StringComparer.Ordinal)
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

    public static IReadOnlyList<string> DeriveTraceTags(Task5RunMetadata runMetadata)
    {
        var tags = new List<string>
        {
            "task-5",
            "phase-2",
            "experiment"
        };

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

    public static IReadOnlyDictionary<string, string> DerivePropagatedMetadata(Task5RunMetadata runMetadata)
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
        AddIfValid(metadata, "task", runMetadata.TaskName);
        return metadata;
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

    private static string DeriveSourceDatasetKind(Task5SliceManifest manifest)
    {
        return string.Equals(ResolveSliceKind(manifest), "single-match", StringComparison.OrdinalIgnoreCase)
            ? "single-match"
            : "slice";
    }

    private static string ResolveCommunityContext(Task5SliceManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.CommunityContext))
        {
            return manifest.CommunityContext;
        }

        if (string.IsNullOrWhiteSpace(manifest.CanonicalDatasetName))
        {
            throw new InvalidOperationException("Slice manifest must contain communityContext or canonicalDatasetName.");
        }

        return manifest.CanonicalDatasetName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Last();
    }

    private static string ResolveSampleMethod(Task5SliceManifest manifest)
    {
        return string.IsNullOrWhiteSpace(manifest.SampleMethod)
            ? "random-sample"
            : manifest.SampleMethod;
    }

    private static string ResolveSliceKind(Task5SliceManifest manifest)
    {
        return string.IsNullOrWhiteSpace(manifest.SliceKind)
            ? "random-sample"
            : manifest.SliceKind;
    }
}
