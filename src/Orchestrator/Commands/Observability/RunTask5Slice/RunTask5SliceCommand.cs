using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Commands.Observability.RunTask5Slice;

public sealed class RunTask5SliceCommand : AsyncCommand<RunTask5SliceSettings>
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions TraceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly ILangfusePublicApiClient _langfuseClient;
    private readonly ILogger<RunTask5SliceCommand> _logger;

    public RunTask5SliceCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        ILangfusePublicApiClient langfuseClient,
        ILogger<RunTask5SliceCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _langfuseClient = langfuseClient;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RunTask5SliceSettings settings)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var manifest = await LoadJsonFileAsync<Task5SliceManifest>(settings.ManifestPath, cancellationToken);
            ValidateManifest(manifest);

            var runMetadata = string.IsNullOrWhiteSpace(settings.RunMetadataFile)
                ? Task5SliceSupport.BuildRunMetadata(manifest, settings)
                : NormalizeRunMetadata(
                    await LoadJsonFileAsync<Task5RunMetadata>(settings.RunMetadataFile, cancellationToken),
                    manifest,
                    settings);

            var communityContext = GetCommunityContext(runMetadata, manifest);
            var datasetName = DeriveDatasetName(settings.DatasetName, runMetadata, manifest);
            var batchSize = settings.BatchSize ?? runMetadata.BatchSize ?? 10;
            var explicitEvaluationTime = ParseExplicitEvaluationTime(runMetadata);
            var evaluationTimestampPolicy = explicitEvaluationTime is null
                ? ParseEvaluationTimestampPolicy(runMetadata)
                : null;
            var deletedExistingRun = await DeleteExistingRunIfRequestedAsync(
                datasetName,
                settings.RunName,
                settings.ReplaceRun,
                cancellationToken);

            var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
            var contextRepository = _firebaseServiceFactory.CreateContextRepository();
            var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();
            var predictionService = _openAiServiceFactory.CreatePredictionService(settings.Model);
            var reconstructionService = new MatchPromptReconstructionService(
                predictionRepository,
                contextRepository,
                new InstructionsTemplateProvider(PromptsFileProvider.Create()));

            var outcomesByKey = await LoadOutcomesAsync(matchOutcomeRepository, communityContext, manifest, cancellationToken);
            var traceTags = Task5SliceSupport.DeriveTraceTags(runMetadata);
            var propagatedMetadata = Task5SliceSupport.DerivePropagatedMetadata(runMetadata);
            var runMetadataPayload = JsonSerializer.SerializeToElement(runMetadata, OutputJsonOptions);

            var batches = Task5SliceSupport.CreateBatchChunks(manifest.Items, batchSize);
            var scoreEntries = new List<Task5ItemScores>();
            var executionSummaries = new List<Task5SliceExecutionSummary>();
            string? datasetRunId = null;
            var completedExecutionCount = 0;

            Task5SliceSupport.ReportProgress(
                $"Starting Task 5 slice run '{settings.RunName}' for model '{settings.Model}' with sample size {manifest.Items.Count} and batch size {batchSize}.");

            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex += 1)
            {
                var batch = batches[batchIndex];
                var batchStart = completedExecutionCount + 1;
                var batchEnd = completedExecutionCount + batch.Count;

                Task5SliceSupport.ReportProgress(
                    $"Batch {batchIndex + 1}/{batches.Count}: executions {batchStart}-{batchEnd} of {manifest.Items.Count}.");

                var batchResults = await Task.WhenAll(batch.Select(item => ExecuteSliceItemAsync(
                    item,
                    settings,
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
                Task5SliceSupport.ReportProgress(
                    $"Completed batch {batchIndex + 1}/{batches.Count}: {completedExecutionCount}/{manifest.Items.Count} executions finished.");
            }

            if (string.IsNullOrWhiteSpace(datasetRunId))
            {
                throw new InvalidOperationException($"Dataset run '{settings.RunName}' did not return a datasetRunId.");
            }

            var aggregateScores = await PostRunScoresAsync(datasetRunId, runMetadata, scoreEntries, cancellationToken);
            var datasetRun = await _langfuseClient.GetDatasetRunAsync(datasetName, settings.RunName, cancellationToken)
                ?? throw new InvalidOperationException($"Dataset run '{settings.RunName}' could not be retrieved from dataset '{datasetName}'.");
            var datasetRunItems = await WaitForDatasetRunItemsAsync(
                datasetRun.DatasetId,
                settings.RunName,
                manifest.Items.Count,
                cancellationToken);

            var summary = new Task5SliceRunSummary(
                datasetName,
                settings.RunName,
                settings.RunName,
                settings.Model,
                deletedExistingRun,
                manifest.Items.Count,
                batchSize,
                executionSummaries.Count,
                1,
                aggregateScores,
                [new Task5SliceDatasetRunSummary(
                    1,
                    settings.RunName,
                    datasetRunId,
                    datasetRunItems.Data.Count,
                    aggregateScores,
                    executionSummaries.FirstOrDefault(),
                    executionSummaries.LastOrDefault())],
                executionSummaries.FirstOrDefault(),
                executionSummaries.LastOrDefault());

            _console.WriteLine(JsonSerializer.Serialize(summary, OutputJsonOptions));
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing run-task5-slice command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task<Task5SliceExecutionResult> ExecuteSliceItemAsync(
        Task5SliceManifestItem item,
        RunTask5SliceSettings settings,
        string datasetName,
        Task5RunMetadata runMetadata,
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
                evaluationTimestampPolicy ?? throw new InvalidOperationException("Run metadata must contain either evaluationTime or evaluationTimestampPolicy."));
        var selection = MatchContextDocumentCatalog.ForMatch(item.HomeTeam, item.AwayTeam, runMetadata.CommunityContext!);
        var reconstructedPrompt = await reconstructionService.ReconstructMatchPredictionPromptAtTimestampAsync(
            promptMatch,
            settings.Model,
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

        using var activity = Telemetry.Source.StartActivity("task-5-slice-item");
        ConfigureTraceContext(
            activity,
            settings.RunName,
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
            sourceDatasetItemId = item.CanonicalDatasetItemId,
            runName = settings.RunName,
            model = settings.Model,
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
            activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(new { error = "Failed to generate prediction" }, TraceJsonOptions));
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
                settings.RunName,
                item.SliceDatasetItemId,
                traceId,
                settings.RunDescription,
                runMetadataPayload),
            cancellationToken);

        var itemScores = Task5SliceSupport.CalculateScores(prediction, outcome.HomeGoals.Value, outcome.AwayGoals.Value);
        await PostItemScoresAsync(
            datasetRunItem.DatasetRunId,
            item.SliceDatasetItemId,
            item.CanonicalDatasetItemId,
            itemScores,
            prediction,
            outcome,
            traceId,
            cancellationToken);

        return new Task5SliceExecutionResult(
            datasetRunItem.DatasetRunId,
            new Task5SliceExecutionSummary(
                item.SliceDatasetItemId,
                item.CanonicalDatasetItemId,
                settings.RunName,
                traceId,
                prediction,
                itemScores,
                traceTags));
    }

    private async Task PostItemScoresAsync(
        string datasetRunId,
        string datasetItemId,
        string sourceDatasetItemId,
        Task5ItemScores scores,
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
        }, OutputJsonOptions);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "kicktipp_points",
                scores.KicktippPoints,
                TraceId: traceId,
                Comment: $"Task 5 slice score for {outcome.HomeTeam} vs {outcome.AwayTeam}",
                Metadata: metadata),
            cancellationToken);
    }

    private async Task<Task5AggregateScores> PostRunScoresAsync(
        string datasetRunId,
        Task5RunMetadata runMetadata,
        IReadOnlyList<Task5ItemScores> scoreEntries,
        CancellationToken cancellationToken)
    {
        var aggregateScores = Task5SliceSupport.SummarizeScores(scoreEntries);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "total_kicktipp_points",
                aggregateScores.TotalKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Aggregate score for {runMetadata.SampleSize} item(s)",
                Metadata: JsonSerializer.SerializeToElement(runMetadata, OutputJsonOptions)),
            cancellationToken);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "avg_kicktipp_points",
                aggregateScores.AvgKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Aggregate score for {runMetadata.SampleSize} item(s)",
                Metadata: JsonSerializer.SerializeToElement(runMetadata, OutputJsonOptions)),
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
        Task5SliceManifest manifest,
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
        Task5RunMetadata runMetadata,
        Task5SliceManifestItem item,
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
        LangfuseActivityPropagation.SetTraceMetadata(activity, "sourceDatasetItemId", item.CanonicalDatasetItemId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", runMetadata.CommunityContext, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "matchday", item.Matchday.ToString(), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "selectedMatch", $"{item.HomeTeam} vs {item.AwayTeam}", propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "homeTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.HomeTeam]), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "awayTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.AwayTeam]), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "teams", PredictionTelemetryMetadata.BuildDelimitedFilterValue([item.HomeTeam, item.AwayTeam]), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "evaluationTimestamp", evaluationTimestamp.ToString("O"), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "tippSpielId", outcome.TippSpielId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "promptTemplatePath", promptTemplatePath, propagateToObservations: false);
    }

    private static string DeriveDatasetName(string? datasetNameOverride, Task5RunMetadata runMetadata, Task5SliceManifest manifest)
    {
        return datasetNameOverride
            ?? runMetadata.DatasetName
            ?? manifest.SliceDatasetName
            ?? throw new InvalidOperationException("No dataset name was provided for the slice run.");
    }

    private static string GetCommunityContext(Task5RunMetadata runMetadata, Task5SliceManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
            ? runMetadata.CommunityContext
            : !string.IsNullOrWhiteSpace(manifest.CommunityContext)
                ? manifest.CommunityContext
                : throw new InvalidOperationException("Run metadata or manifest must contain communityContext.");
    }

    private static Task5RunMetadata NormalizeRunMetadata(
        Task5RunMetadata runMetadata,
        Task5SliceManifest manifest,
        RunTask5SliceSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(runMetadata.Model)
            && !string.Equals(runMetadata.Model, settings.Model, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Run metadata model '{runMetadata.Model}' does not match requested model '{settings.Model}'.");
        }

        return runMetadata with
        {
            CommunityContext = string.IsNullOrWhiteSpace(runMetadata.CommunityContext)
                ? manifest.CommunityContext
                : runMetadata.CommunityContext,
            Model = settings.Model,
            Competition = string.IsNullOrWhiteSpace(runMetadata.Competition) ? manifest.Competition : runMetadata.Competition,
            CanonicalDatasetName = string.IsNullOrWhiteSpace(runMetadata.CanonicalDatasetName)
                ? manifest.CanonicalDatasetName
                : runMetadata.CanonicalDatasetName,
            DatasetName = string.IsNullOrWhiteSpace(runMetadata.DatasetName) ? manifest.SliceDatasetName : runMetadata.DatasetName,
            PromptKey = string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? settings.PromptKey : runMetadata.PromptKey,
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
                ? string.IsNullOrWhiteSpace(runMetadata.PromptKey) ? settings.PromptKey : runMetadata.PromptKey
                : runMetadata.PromptVersion,
            SourceDatasetKind = string.IsNullOrWhiteSpace(runMetadata.SourceDatasetKind)
                ? string.Equals(manifest.SliceKind, "single-match", StringComparison.OrdinalIgnoreCase) ? "single-match" : "slice"
                : runMetadata.SourceDatasetKind,
            DatasetItemIdMap = runMetadata.DatasetItemIdMap.Count > 0
                ? runMetadata.DatasetItemIdMap
                : Task5SliceSupport.CreateDatasetItemIdMap(manifest),
            BatchSize = settings.BatchSize ?? runMetadata.BatchSize
        };
    }

    private static DateTimeOffset? ParseExplicitEvaluationTime(Task5RunMetadata runMetadata)
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

        return EvaluationTimeParser.Parse(runMetadata.EvaluationTime);
    }

    private static void ValidateManifest(Task5SliceManifest manifest)
    {
        if (manifest.Items.Count == 0)
        {
            throw new InvalidOperationException("Slice manifest must contain at least one item.");
        }

        var seenHostedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.CanonicalDatasetItemId))
            {
                throw new InvalidOperationException("Each slice manifest item must contain canonicalDatasetItemId.");
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

    private static EvaluationTimestampPolicy ParseEvaluationTimestampPolicy(Task5RunMetadata runMetadata)
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

    private static string BuildOutcomeKey(string homeTeam, string awayTeam, int matchday)
    {
        return string.Join("|", matchday, homeTeam.Trim(), awayTeam.Trim());
    }

    private static async Task<T> LoadJsonFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        var absolutePath = Path.GetFullPath(path);
        var raw = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var value = JsonSerializer.Deserialize<T>(raw, OutputJsonOptions);
        return value ?? throw new InvalidOperationException($"JSON file '{absolutePath}' could not be deserialized.");
    }

    private sealed record Task5SliceExecutionResult(
        string DatasetRunId,
        Task5SliceExecutionSummary Summary);
}
