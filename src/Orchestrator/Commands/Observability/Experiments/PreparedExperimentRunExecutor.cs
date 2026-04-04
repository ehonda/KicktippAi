using System.Diagnostics;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Commands.Observability.Experiments;

internal sealed class PreparedExperimentRunExecutor
{
    private static readonly JsonSerializerOptions TraceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly ILangfusePublicApiClient _langfuseClient;

    public PreparedExperimentRunExecutor(
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient)
    {
        _firebaseServiceFactory = firebaseServiceFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _langfuseClient = langfuseClient;
    }

    public async Task<PreparedExperimentRunSummary> ExecuteAsync(
        string expectedTaskType,
        PreparedExperimentRunRequest request,
        CancellationToken cancellationToken)
    {
        var manifest = await PreparedExperimentCommandSupport.LoadJsonFileAsync<PreparedExperimentManifest>(
            request.ManifestPath,
            cancellationToken);
        PreparedExperimentCommandSupport.ValidateManifest(manifest);
        PreparedExperimentCommandSupport.EnsureTaskType(manifest, expectedTaskType);

        var runMetadata = string.IsNullOrWhiteSpace(request.RunMetadataFile)
            ? PreparedExperimentSupport.BuildRunMetadata(manifest, request.Options)
            : PreparedExperimentCommandSupport.NormalizeRunMetadata(
                await PreparedExperimentCommandSupport.LoadJsonFileAsync<PreparedExperimentRunMetadata>(
                    request.RunMetadataFile,
                    cancellationToken),
                manifest,
                request.Options);
        runMetadata = ApplyBatchingDefaults(runMetadata, expectedTaskType);

        var communityContext = GetCommunityContext(runMetadata, manifest);
        var datasetName = DeriveDatasetName(runMetadata, manifest);
        var explicitEvaluationTime = PreparedExperimentCommandSupport.ParseExplicitEvaluationTime(runMetadata);
        var evaluationTimestampPolicy = explicitEvaluationTime is null
            ? PreparedExperimentCommandSupport.ParseEvaluationTimestampPolicy(runMetadata)
            : null;
        var deletedExistingRun = await DeleteExistingRunIfRequestedAsync(
            datasetName,
            request.RunName,
            request.ReplaceRun,
            cancellationToken);

        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
        var contextRepository = _firebaseServiceFactory.CreateContextRepository();
        var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
        var predictionService = _openAiServiceFactory.CreatePredictionService(request.Options.Model);
        var reconstructionService = new MatchPromptReconstructionService(
            predictionRepository,
            contextRepository,
            new InstructionsTemplateProvider(PromptsFileProvider.Create()));

        var outcomesByKey = await LoadOutcomesAsync(matchOutcomeRepository, communityContext, manifest, cancellationToken);
        var traceTags = PreparedExperimentSupport.DeriveTraceTags(runMetadata);
        var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(runMetadata);
        var runMetadataPayload = JsonSerializer.SerializeToElement(runMetadata, PreparedExperimentCommandSupport.JsonOptions);
        var batches = BuildBatches(manifest.Items, runMetadata, expectedTaskType);
        var scoreEntries = new List<ExperimentItemScores>();
        var executionSummaries = new List<PreparedExperimentExecutionSummary>();
        string? datasetRunId = null;
        var completedExecutionCount = 0;

        PreparedExperimentSupport.ReportProgress(
            $"Starting {expectedTaskType} run '{request.RunName}' for model '{request.Options.Model}' with sample size {manifest.Items.Count} and {DescribeBatching(runMetadata, batches.Count)}.");

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex += 1)
        {
            var batch = batches[batchIndex];
            var batchStart = completedExecutionCount + 1;
            var batchEnd = completedExecutionCount + batch.Count;

            PreparedExperimentSupport.ReportProgress(
                $"Batch {batchIndex + 1}/{batches.Count}: executions {batchStart}-{batchEnd} of {manifest.Items.Count}.");

            var batchResults = await Task.WhenAll(batch.Select(item => ExecuteItemAsync(
                item,
                request,
                datasetName,
                runMetadata,
                explicitEvaluationTime,
                evaluationTimestampPolicy,
                predictionRepository,
                reconstructionService,
                predictionService,
                outcomesByKey,
                traceTags,
                propagatedMetadata,
                runMetadataPayload,
                cancellationToken)));

            foreach (var batchResult in batchResults)
            {
                datasetRunId ??= batchResult.DatasetRunId;
                scoreEntries.Add(batchResult.Summary.Scores);
                executionSummaries.Add(batchResult.Summary);
            }

            completedExecutionCount += batchResults.Length;
            PreparedExperimentSupport.ReportProgress(
                $"Completed batch {batchIndex + 1}/{batches.Count}: {completedExecutionCount}/{manifest.Items.Count} executions finished.");
        }

        if (string.IsNullOrWhiteSpace(datasetRunId))
        {
            throw new InvalidOperationException($"Dataset run '{request.RunName}' did not return a datasetRunId.");
        }

        var aggregateScores = await PostRunScoresAsync(datasetRunId, runMetadata, scoreEntries, cancellationToken);
        var datasetRun = await _langfuseClient.GetDatasetRunAsync(datasetName, request.RunName, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Dataset run '{request.RunName}' could not be retrieved from dataset '{datasetName}'.");
        var datasetRunItems = await WaitForDatasetRunItemsAsync(
            datasetRun.DatasetId,
            request.RunName,
            manifest.Items.Count,
            cancellationToken);

        return new PreparedExperimentRunSummary(
            datasetName,
            request.RunName,
            request.RunName,
            runMetadata.TaskType ?? expectedTaskType,
            request.Options.Model,
            deletedExistingRun,
            manifest.Items.Count,
            runMetadata.BatchStrategy ?? expectedTaskType,
            runMetadata.BatchSize,
            runMetadata.BatchCount,
            executionSummaries.Count,
            1,
            aggregateScores,
            [new PreparedExperimentDatasetRunSummary(
                1,
                request.RunName,
                datasetRunId,
                datasetRunItems.Data.Count,
                aggregateScores,
                executionSummaries.FirstOrDefault(),
                executionSummaries.LastOrDefault())],
            executionSummaries.FirstOrDefault(),
            executionSummaries.LastOrDefault());
    }

    private IReadOnlyList<IReadOnlyList<PreparedExperimentManifestItem>> BuildBatches(
        IReadOnlyList<PreparedExperimentManifestItem> items,
        PreparedExperimentRunMetadata runMetadata,
        string expectedTaskType)
    {
        return string.Equals(expectedTaskType, "repeated-match", StringComparison.OrdinalIgnoreCase)
            ? PreparedExperimentSupport.CreateWarmupThenBatchChunks(items, runMetadata.BatchCount ?? 3)
            : PreparedExperimentSupport.CreateBatchChunks(items, runMetadata.BatchSize ?? 10);
    }

    private static PreparedExperimentRunMetadata ApplyBatchingDefaults(
        PreparedExperimentRunMetadata runMetadata,
        string expectedTaskType)
    {
        if (string.Equals(expectedTaskType, "repeated-match", StringComparison.OrdinalIgnoreCase))
        {
            return runMetadata with
            {
                BatchStrategy = string.IsNullOrWhiteSpace(runMetadata.BatchStrategy)
                    ? "warmup-plus-batches"
                    : runMetadata.BatchStrategy,
                BatchCount = runMetadata.BatchCount ?? 3,
                BatchSize = null
            };
        }

        return runMetadata with
        {
            BatchStrategy = string.IsNullOrWhiteSpace(runMetadata.BatchStrategy)
                ? "simple-batched"
                : runMetadata.BatchStrategy,
            BatchSize = runMetadata.BatchSize ?? 10,
            BatchCount = null
        };
    }

    private async Task<PreparedExperimentExecutionResult> ExecuteItemAsync(
        PreparedExperimentManifestItem item,
        PreparedExperimentRunRequest request,
        string datasetName,
        PreparedExperimentRunMetadata runMetadata,
        DateTimeOffset? explicitEvaluationTime,
        EvaluationTimestampPolicy? evaluationTimestampPolicy,
        IPredictionRepository predictionRepository,
        MatchPromptReconstructionService reconstructionService,
        IPredictionService predictionService,
        IReadOnlyDictionary<string, PersistedMatchOutcome> outcomesByKey,
        IReadOnlyList<string> traceTags,
        IReadOnlyDictionary<string, string> propagatedMetadata,
        JsonElement runMetadataPayload,
        CancellationToken cancellationToken)
    {
        var outcomeKey = BuildOutcomeKey(item.HomeTeam, item.AwayTeam, item.Matchday);
        if (!outcomesByKey.TryGetValue(outcomeKey, out var outcome))
        {
            throw new InvalidOperationException(
                $"No persisted match outcome was found for {item.HomeTeam} vs {item.AwayTeam} on matchday {item.Matchday}.");
        }

        if (!outcome.HasOutcome || outcome.HomeGoals is null || outcome.AwayGoals is null)
        {
            throw new InvalidOperationException(
                $"The selected match does not have a completed persisted outcome yet: {item.HomeTeam} vs {item.AwayTeam}.");
        }

        var storedMatch = await predictionRepository.GetStoredMatchAsync(
            item.HomeTeam,
            item.AwayTeam,
            item.Matchday,
            null,
            null,
            cancellationToken);

        var promptMatch = storedMatch is null
            ? ExperimentArtifactSupport.RehydrateForPromptOutput(new Match(item.HomeTeam, item.AwayTeam, outcome.StartsAt, item.Matchday))
            : ExperimentArtifactSupport.RehydrateForPromptOutput(storedMatch);
        var evaluationTimestamp = explicitEvaluationTime
            ?? EvaluationTimestampResolver.Resolve(
                promptMatch,
                evaluationTimestampPolicy ?? throw new InvalidOperationException(
                    "Run metadata must contain either evaluationTime or evaluationTimestampPolicy."));
        var selection = MatchContextDocumentCatalog.ForMatch(item.HomeTeam, item.AwayTeam, runMetadata.CommunityContext!);
        var reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAtTimestampAsync(
            promptMatch,
            request.Options.Model,
            runMetadata.CommunityContext!,
            evaluationTimestamp,
            selection.RequiredDocumentNames,
            selection.OptionalDocumentNames,
            runMetadata.IncludeJustification,
            cancellationToken);

        var contextDocuments = reconstructedPrompt.ResolvedContextDocuments
            .Select(document => new DocumentContext(document.DocumentName, document.Content))
            .ToList();
        var telemetryMetadata = new PredictionTelemetryMetadata(
            HomeTeam: item.HomeTeam,
            AwayTeam: item.AwayTeam,
            RepredictionIndex: 0);

        using var activity = Telemetry.Source.StartActivity("match-experiment-item");
        ConfigureTraceContext(
            activity,
            request.RunName,
            datasetName,
            runMetadata,
            item,
            outcome,
            traceTags,
            propagatedMetadata,
            evaluationTimestamp,
            predictionService.GetMatchPromptPath(runMetadata.IncludeJustification));

        var traceInput = new
        {
            datasetName,
            datasetItemId = item.SliceDatasetItemId,
            sourceDatasetItemId = item.SourceDatasetItemId,
            runName = request.RunName,
            task = runMetadata.TaskType,
            model = request.Options.Model,
            includeJustification = runMetadata.IncludeJustification,
            evaluationTimestamp,
            match = new
            {
                item.HomeTeam,
                item.AwayTeam,
                item.Matchday,
                startsAt = item.StartsAt
            },
            promptTemplatePath = reconstructedPrompt.PromptTemplatePath
        };
        activity?.SetTag("langfuse.trace.input", JsonSerializer.Serialize(traceInput, TraceJsonOptions));

        var prediction = await predictionService.PredictMatchAsync(
            promptMatch,
            contextDocuments,
            runMetadata.IncludeJustification,
            telemetryMetadata,
            cancellationToken);

        if (prediction is null)
        {
            activity?.SetTag(
                "langfuse.trace.output",
                JsonSerializer.Serialize(new { error = "Failed to generate prediction" }, TraceJsonOptions));
            throw new InvalidOperationException(
                $"Failed to generate prediction for {item.HomeTeam} vs {item.AwayTeam} on matchday {item.Matchday}.");
        }

        activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(prediction, TraceJsonOptions));

        var traceId = activity?.TraceId.ToString();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new InvalidOperationException(
                $"Trace creation failed for {item.HomeTeam} vs {item.AwayTeam}; no trace id was available.");
        }

        var datasetRunItem = await _langfuseClient.CreateDatasetRunItemAsync(
            new LangfuseCreateDatasetRunItemRequest(
                request.RunName,
                item.SliceDatasetItemId,
                traceId,
                request.RunDescription,
                runMetadataPayload),
            cancellationToken);

        var itemScores = PreparedExperimentSupport.CalculateScores(prediction, outcome.HomeGoals.Value, outcome.AwayGoals.Value);
        await PostItemScoresAsync(
            datasetRunItem.DatasetRunId,
            item.SliceDatasetItemId,
            item.SourceDatasetItemId,
            itemScores,
            prediction,
            outcome,
            traceId,
            cancellationToken);

        return new PreparedExperimentExecutionResult(
            datasetRunItem.DatasetRunId,
            new PreparedExperimentExecutionSummary(
                item.SliceDatasetItemId,
                item.SourceDatasetItemId,
                request.RunName,
                traceId,
                prediction,
                itemScores,
                traceTags));
    }

    private async Task PostItemScoresAsync(
        string datasetRunId,
        string datasetItemId,
        string sourceDatasetItemId,
        ExperimentItemScores scores,
        Prediction prediction,
        PersistedMatchOutcome outcome,
        string traceId,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.SerializeToElement(new
        {
            datasetRunId,
            datasetItemId,
            sourceDatasetItemId,
            prediction,
            expectedOutput = new
            {
                homeGoals = outcome.HomeGoals,
                awayGoals = outcome.AwayGoals
            }
        }, PreparedExperimentCommandSupport.JsonOptions);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "kicktipp_points",
                scores.KicktippPoints,
                TraceId: traceId,
                Comment: $"Experiment score for {outcome.HomeTeam} vs {outcome.AwayTeam}",
                Metadata: metadata),
            cancellationToken);
    }

    private async Task<ExperimentAggregateScores> PostRunScoresAsync(
        string datasetRunId,
        PreparedExperimentRunMetadata runMetadata,
        IReadOnlyList<ExperimentItemScores> scoreEntries,
        CancellationToken cancellationToken)
    {
        var aggregateScores = PreparedExperimentSupport.SummarizeScores(scoreEntries);
        var runMetadataPayload = JsonSerializer.SerializeToElement(runMetadata, PreparedExperimentCommandSupport.JsonOptions);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "total_kicktipp_points",
                aggregateScores.TotalKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Aggregate score for {runMetadata.SampleSize} item(s)",
                Metadata: runMetadataPayload),
            cancellationToken);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "avg_kicktipp_points",
                aggregateScores.AvgKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Aggregate score for {runMetadata.SampleSize} item(s)",
                Metadata: runMetadataPayload),
            cancellationToken);

        return aggregateScores;
    }

    private async Task<LangfusePaginatedResponse<LangfuseDatasetRunItem>> WaitForDatasetRunItemsAsync(
        string datasetId,
        string runName,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        var limit = Math.Max(100, expectedCount);

        for (var attempt = 0; attempt < 6; attempt += 1)
        {
            var datasetRunItems = await _langfuseClient.ListDatasetRunItemsAsync(
                datasetId,
                runName,
                1,
                limit,
                cancellationToken);

            if (datasetRunItems.Data.Count >= expectedCount)
            {
                return datasetRunItems;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return await _langfuseClient.ListDatasetRunItemsAsync(datasetId, runName, 1, limit, cancellationToken);
    }

    private async Task<bool> DeleteExistingRunIfRequestedAsync(
        string datasetName,
        string runName,
        bool replaceRun,
        CancellationToken cancellationToken)
    {
        if (!replaceRun)
        {
            return false;
        }

        return await _langfuseClient.DeleteDatasetRunAsync(datasetName, runName, cancellationToken);
    }

    private static IReadOnlyDictionary<string, PersistedMatchOutcome> LoadOutcomeDictionary(
        IEnumerable<PersistedMatchOutcome> outcomes)
    {
        return outcomes.ToDictionary(
            outcome => BuildOutcomeKey(outcome.HomeTeam, outcome.AwayTeam, outcome.Matchday),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyDictionary<string, PersistedMatchOutcome>> LoadOutcomesAsync(
        IMatchOutcomeRepository matchOutcomeRepository,
        string communityContext,
        PreparedExperimentManifest manifest,
        CancellationToken cancellationToken)
    {
        var dictionary = new Dictionary<string, PersistedMatchOutcome>(StringComparer.OrdinalIgnoreCase);

        foreach (var matchday in manifest.Items.Select(item => item.Matchday).Distinct().OrderBy(matchday => matchday))
        {
            var outcomes = await matchOutcomeRepository.GetMatchdayOutcomesAsync(matchday, communityContext, cancellationToken);
            foreach (var pair in LoadOutcomeDictionary(outcomes))
            {
                dictionary[pair.Key] = pair.Value;
            }
        }

        return dictionary;
    }

    private static void ConfigureTraceContext(
        Activity? activity,
        string runName,
        string datasetName,
        PreparedExperimentRunMetadata runMetadata,
        PreparedExperimentManifestItem item,
        PersistedMatchOutcome outcome,
        IReadOnlyList<string> traceTags,
        IReadOnlyDictionary<string, string> propagatedMetadata,
        DateTimeOffset evaluationTimestamp,
        string promptTemplatePath)
    {
        activity?.SetTag("langfuse.trace.name", runName);
        LangfuseActivityPropagation.SetEnvironment(activity, "development");
        LangfuseActivityPropagation.SetSessionId(activity, runName);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);

        foreach (var metadata in propagatedMetadata)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, metadata.Key, metadata.Value);
        }

        LangfuseActivityPropagation.SetTraceMetadata(activity, "datasetName", datasetName, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "datasetItemId", item.SliceDatasetItemId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "sourceDatasetItemId", item.SourceDatasetItemId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", runMetadata.CommunityContext, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "matchday", item.Matchday.ToString(), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "selectedMatch", $"{item.HomeTeam} vs {item.AwayTeam}", propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(
            activity,
            "homeTeams",
            PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.HomeTeam]),
            propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(
            activity,
            "awayTeams",
            PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.AwayTeam]),
            propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(
            activity,
            "teams",
            PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.HomeTeam, item.AwayTeam]),
            propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(
            activity,
            "evaluationTimestamp",
            evaluationTimestamp.ToString("O"),
            propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "tippSpielId", outcome.TippSpielId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "promptTemplatePath", promptTemplatePath, propagateToObservations: false);
    }

    private static string DeriveDatasetName(PreparedExperimentRunMetadata runMetadata, PreparedExperimentManifest manifest)
    {
        return runMetadata.DatasetName
               ?? manifest.SliceDatasetName
               ?? throw new InvalidOperationException("No dataset name was provided for the experiment run.");
    }

    private static string GetCommunityContext(PreparedExperimentRunMetadata runMetadata, PreparedExperimentManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
            ? runMetadata.CommunityContext
            : !string.IsNullOrWhiteSpace(manifest.CommunityContext)
                ? manifest.CommunityContext
                : throw new InvalidOperationException("Run metadata or manifest must contain communityContext.");
    }

    private static string BuildOutcomeKey(string homeTeam, string awayTeam, int matchday)
    {
        return string.Join("|", matchday, homeTeam.Trim(), awayTeam.Trim());
    }

    private static string DescribeBatching(PreparedExperimentRunMetadata runMetadata, int batchTotal)
    {
        return string.Equals(runMetadata.TaskType, "repeated-match", StringComparison.OrdinalIgnoreCase)
            ? $"warmup plus {Math.Max(0, batchTotal - 1)} additional batch(es)"
            : $"batch size {runMetadata.BatchSize}";
    }

    private sealed record PreparedExperimentExecutionResult(
        string DatasetRunId,
        PreparedExperimentExecutionSummary Summary);
}

internal sealed record PreparedExperimentRunRequest(
    string ManifestPath,
    string RunName,
    string? RunDescription,
    string? RunMetadataFile,
    bool ReplaceRun,
    PreparedExperimentRunOptions Options);
