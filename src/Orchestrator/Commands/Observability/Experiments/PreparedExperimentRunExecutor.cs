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
        var promptRoute = await ResolvePromptRouteAsync(runMetadata, cancellationToken);
        if (promptRoute.TraceMetadata is { } promptTraceMetadata)
        {
            runMetadata = runMetadata with
            {
                LangfusePromptVersion = promptTraceMetadata.Version
            };
        }

        var predictionServiceOptions = PredictionServiceOptions.FlexProcessingWithStandardFallback with
        {
            LangfusePromptTraceMetadata = promptRoute.TraceMetadata,
            ReasoningEffort = runMetadata.ReasoningEffort
        };
        var predictionService = promptRoute.TemplateProvider is null
            ? _openAiServiceFactory.CreatePredictionService(
                request.Options.Model,
                predictionServiceOptions)
            : _openAiServiceFactory.CreatePredictionService(
                request.Options.Model,
                predictionServiceOptions,
                promptRoute.TemplateProvider);
        var reconstructionService = new MatchPromptReconstructionService(
            predictionRepository,
            contextRepository,
            promptRoute.TemplateProvider ?? new InstructionsTemplateProvider(PromptsFileProvider.Create()));

        var outcomesByKey = await LoadOutcomesAsync(matchOutcomeRepository, communityContext, manifest, cancellationToken);
        var experimentName = PreparedExperimentSupport.DeriveExperimentName(runMetadata, request.RunName);
        var traceTags = PreparedExperimentSupport.DeriveTraceTags(runMetadata);
        var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(runMetadata);
        var runMetadataPayload = PreparedExperimentSupport.BuildLangfuseExperimentMetadata(
            runMetadata,
            experimentName,
            request.RunName,
            new Dictionary<string, string?>
            {
                ["openaiServiceTierStrategy"] = "flex-first-standard-fallback",
                ["openaiReasoningEffort"] = runMetadata.ReasoningEffort
            });
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
                experimentName,
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
                datasetRunItems.Meta.TotalItems,
                aggregateScores,
                executionSummaries.FirstOrDefault(),
                executionSummaries.LastOrDefault())],
            executionSummaries.FirstOrDefault(),
            executionSummaries.LastOrDefault());
    }

    public async Task<PreparedExperimentRunSummary> ExecuteCommunityToDateAsync(
        PreparedExperimentCommunityRunRequest request,
        CancellationToken cancellationToken)
    {
        var manifest = await PreparedExperimentCommandSupport.LoadJsonFileAsync<PreparedExperimentManifest>(
            request.ManifestPath,
            cancellationToken);
        PreparedExperimentCommandSupport.ValidateManifest(manifest);
        PreparedExperimentCommandSupport.EnsureTaskType(manifest, "community-to-date");

        if (manifest.Participants.Count == 0)
        {
            throw new InvalidOperationException("Community-to-date manifests must contain at least one participant.");
        }

        var datasetName = string.IsNullOrWhiteSpace(request.DatasetName)
            ? manifest.SliceDatasetName
            : request.DatasetName.Trim();
        if (string.IsNullOrWhiteSpace(datasetName))
        {
            throw new InvalidOperationException("No dataset name was provided for the community-to-date run.");
        }

        var startedAtUtc = ExperimentArtifactSupport.FormatStartedAtUtc(DateTimeOffset.UtcNow);
        var batchSize = request.BatchSize;
        var participants = SelectParticipants(manifest, request);
        var runFamilyName = string.IsNullOrWhiteSpace(request.RunFamilyName)
            ? BuildCommunityRunFamilyName(manifest, startedAtUtc)
            : request.RunFamilyName.Trim();

        var datasetRunSummaries = new List<PreparedExperimentDatasetRunSummary>();
        var executionSummaries = new List<PreparedExperimentExecutionSummary>();
        var scoreEntries = new List<ExperimentItemScores>();
        var deletedAnyExistingRun = false;

        PreparedExperimentSupport.ReportProgress(
            $"Starting community-to-date run family '{runFamilyName}' with {participants.Count} participant run(s) and sample size {manifest.Items.Count}.");

        for (var participantIndex = 0; participantIndex < participants.Count; participantIndex += 1)
        {
            var participant = participants[participantIndex];
            var runName = BuildCommunityParticipantRunName(runFamilyName, participant);
            var runMetadata = BuildCommunityRunMetadata(manifest, participant, datasetName, startedAtUtc, batchSize);
            var deletedExistingRun = await DeleteExistingRunIfRequestedAsync(
                datasetName,
                runName,
                request.ReplaceRuns,
                cancellationToken);
            deletedAnyExistingRun |= deletedExistingRun;

            var traceTags = PreparedExperimentSupport.DeriveTraceTags(runMetadata);
            var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(runMetadata);
            var experimentName = runFamilyName;
            var runMetadataPayload = PreparedExperimentSupport.BuildLangfuseExperimentMetadata(
                runMetadata,
                experimentName,
                runName);
            var predictionsBySourceDatasetItemId = participant.Predictions
                .GroupBy(prediction => prediction.SourceDatasetItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var batches = PreparedExperimentSupport.CreateBatchChunks(manifest.Items, batchSize);
            var participantScoreEntries = new List<ExperimentItemScores>();
            var participantExecutionSummaries = new List<PreparedExperimentExecutionSummary>();
            string? datasetRunId = null;
            var completedExecutionCount = 0;

            PreparedExperimentSupport.ReportProgress(
                $"Participant {participantIndex + 1}/{participants.Count}: starting run '{runName}' for '{participant.DisplayName}' with batch size {batchSize}.");

            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex += 1)
            {
                var batch = batches[batchIndex];
                var batchStart = completedExecutionCount + 1;
                var batchEnd = completedExecutionCount + batch.Count;

                PreparedExperimentSupport.ReportProgress(
                    $"Participant {participant.DisplayName}: batch {batchIndex + 1}/{batches.Count}, executions {batchStart}-{batchEnd} of {manifest.Items.Count}.");

                var batchResults = await Task.WhenAll(batch.Select(item => ExecuteCommunityItemAsync(
                    item,
                    participant,
                    predictionsBySourceDatasetItemId,
                    runName,
                    experimentName,
                    request.RunDescription,
                    datasetName,
                    runMetadata,
                    traceTags,
                    propagatedMetadata,
                    runMetadataPayload,
                    cancellationToken)));

                foreach (var batchResult in batchResults)
                {
                    datasetRunId ??= batchResult.DatasetRunId;
                    participantScoreEntries.Add(batchResult.Summary.Scores);
                    participantExecutionSummaries.Add(batchResult.Summary);
                }

                completedExecutionCount += batchResults.Length;
            }

            if (string.IsNullOrWhiteSpace(datasetRunId))
            {
                throw new InvalidOperationException($"Dataset run '{runName}' did not return a datasetRunId.");
            }

            var aggregateScores = await PostRunScoresAsync(datasetRunId, runMetadata, participantScoreEntries, cancellationToken);
            var datasetRun = await _langfuseClient.GetDatasetRunAsync(datasetName, runName, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Dataset run '{runName}' could not be retrieved from dataset '{datasetName}'.");
            var datasetRunItems = await WaitForDatasetRunItemsAsync(
                datasetRun.DatasetId,
                runName,
                manifest.Items.Count,
                cancellationToken);

            datasetRunSummaries.Add(new PreparedExperimentDatasetRunSummary(
                participantIndex + 1,
                runName,
                datasetRunId,
                datasetRunItems.Meta.TotalItems,
                aggregateScores,
                participantExecutionSummaries.FirstOrDefault(),
                participantExecutionSummaries.LastOrDefault()));
            executionSummaries.AddRange(participantExecutionSummaries);
            scoreEntries.AddRange(participantScoreEntries);
        }

        var overallAggregateScores = PreparedExperimentSupport.SummarizeScores(scoreEntries);
        return new PreparedExperimentRunSummary(
            datasetName,
            runFamilyName,
            runFamilyName,
            "community-to-date",
            "community-predictions",
            deletedAnyExistingRun,
            manifest.Items.Count,
            "simple-batched",
            batchSize,
            null,
            executionSummaries.Count,
            datasetRunSummaries.Count,
            overallAggregateScores,
            datasetRunSummaries,
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

    private async Task<ExperimentPromptRoute> ResolvePromptRouteAsync(
        PreparedExperimentRunMetadata runMetadata,
        CancellationToken cancellationToken)
    {
        var promptSource = string.IsNullOrWhiteSpace(runMetadata.PromptSource)
            ? "local"
            : runMetadata.PromptSource.Trim().ToLowerInvariant();

        if (promptSource == "local")
        {
            return new ExperimentPromptRoute(null, null);
        }

        if (promptSource != "langfuse")
        {
            throw new InvalidOperationException($"Unsupported prompt source '{runMetadata.PromptSource}'.");
        }

        if (runMetadata.IncludeJustification)
        {
            throw new InvalidOperationException(
                "The Langfuse prompt source POC only supports match prompts without justification.");
        }

        if (string.IsNullOrWhiteSpace(runMetadata.LangfusePromptName))
        {
            throw new InvalidOperationException("Run metadata must contain langfusePromptName when promptSource is langfuse.");
        }

        var prompt = await _langfuseClient.GetPromptAsync(
                         runMetadata.LangfusePromptName,
                         runMetadata.LangfusePromptLabel,
                         runMetadata.LangfusePromptVersion,
                         cancellationToken)
                     ?? throw new FileNotFoundException(
                         $"Langfuse prompt '{runMetadata.LangfusePromptName}' was not found.");

        _ = prompt.GetTextPrompt();
        var templateProvider = new LangfuseTextPromptTemplateProvider(
            _langfuseClient,
            runMetadata.LangfusePromptName,
            runMetadata.LangfusePromptLabel,
            runMetadata.LangfusePromptVersion,
            prompt);

        return new ExperimentPromptRoute(
            templateProvider,
            new LangfusePromptTraceMetadata(prompt.Name, prompt.Version));
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
        string experimentName,
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

        using var activity = Telemetry.Source.StartActivity("experiment-item-run");
        ConfigureTraceContext(
            activity,
            request.RunName,
            experimentName,
            request.RunDescription,
            datasetName,
            runMetadata,
            item,
            outcome.TippSpielId,
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
            reasoningEffort = runMetadata.ReasoningEffort,
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
        var traceInputJson = JsonSerializer.Serialize(traceInput, TraceJsonOptions);
        SetTraceAndRootObservationInput(activity, traceInputJson);

        var prediction = await predictionService.PredictMatchAsync(
            promptMatch,
            contextDocuments,
            runMetadata.IncludeJustification,
            telemetryMetadata,
            cancellationToken);

        if (prediction is null)
        {
            SetTraceAndRootObservationOutput(
                activity,
                JsonSerializer.Serialize(new { error = "Failed to generate prediction" }, TraceJsonOptions));
            throw new InvalidOperationException(
                $"Failed to generate prediction for {item.HomeTeam} vs {item.AwayTeam} on matchday {item.Matchday}.");
        }

        SetTraceAndRootObservationOutput(activity, JsonSerializer.Serialize(prediction, TraceJsonOptions));

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
                runMetadataPayload,
                activity?.SpanId.ToString()),
            cancellationToken);
        SetExperimentRunId(activity, datasetRunItem.DatasetRunId);

        var itemScores = PreparedExperimentSupport.CalculateScores(prediction, outcome.HomeGoals.Value, outcome.AwayGoals.Value);
        await PostItemScoreAsync(
            datasetRunItem.DatasetRunId,
            datasetName,
            request.RunName,
            experimentName,
            runMetadata,
            item,
            traceId,
            activity?.SpanId.ToString(),
            itemScores,
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

    private async Task<PreparedExperimentExecutionResult> ExecuteCommunityItemAsync(
        PreparedExperimentManifestItem item,
        PreparedExperimentParticipantManifest participant,
        IReadOnlyDictionary<string, PreparedExperimentParticipantPrediction> predictionsBySourceDatasetItemId,
        string runName,
        string experimentName,
        string? runDescription,
        string datasetName,
        PreparedExperimentRunMetadata runMetadata,
        IReadOnlyList<string> traceTags,
        IReadOnlyDictionary<string, string> propagatedMetadata,
        JsonElement runMetadataPayload,
        CancellationToken cancellationToken)
    {
        var participantPrediction = predictionsBySourceDatasetItemId.TryGetValue(item.SourceDatasetItemId, out var prediction)
            ? prediction
            : new PreparedExperimentParticipantPrediction
            {
                SourceDatasetItemId = item.SourceDatasetItemId,
                Status = "missed",
                KicktippPoints = 0
            };

        var traceInput = JsonSerializer.Serialize(new
        {
            datasetName,
            datasetItemId = item.SliceDatasetItemId,
            sourceDatasetItemId = item.SourceDatasetItemId,
            runName,
            task = runMetadata.TaskType,
            source = "kicktipp-community",
            participant = new
            {
                participant.ParticipantId,
                participant.DisplayName
            },
            match = new
            {
                item.HomeTeam,
                item.AwayTeam,
                item.Matchday,
                item.StartsAt,
                item.TippSpielId
            }
        }, TraceJsonOptions);
        var predictionPayload = CreateCommunityPredictionPayload(participantPrediction);

        using var activity = Telemetry.Source.StartActivity("experiment-item-run");
        ConfigureTraceContext(
            activity,
            runName,
            experimentName,
            runDescription,
            datasetName,
            runMetadata,
            item,
            item.TippSpielId,
            traceTags,
            propagatedMetadata);

        SetTraceAndRootObservationInput(activity, traceInput);
        SetTraceAndRootObservationOutput(activity, predictionPayload.GetRawText());

        string? predictionObservationId = null;
        using (var observation = Telemetry.Source.StartActivity(runMetadata.ObservationName ?? "community-match-prediction"))
        {
            predictionObservationId = observation?.SpanId.ToString();
            ConfigureCommunityPredictionObservation(observation, participant, participantPrediction, item, predictionPayload);
        }

        var traceId = activity?.TraceId.ToString();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new InvalidOperationException(
                $"Trace creation failed for {item.HomeTeam} vs {item.AwayTeam}; no trace id was available.");
        }

        var datasetRunItem = await _langfuseClient.CreateDatasetRunItemAsync(
            new LangfuseCreateDatasetRunItemRequest(
                runName,
                item.SliceDatasetItemId,
                traceId,
                runDescription,
                runMetadataPayload,
                activity?.SpanId.ToString()),
            cancellationToken);
        SetExperimentRunId(activity, datasetRunItem.DatasetRunId);

        var itemScores = new ExperimentItemScores(participantPrediction.KicktippPoints);
        await PostItemScoreAsync(
            datasetRunItem.DatasetRunId,
            datasetName,
            runName,
            experimentName,
            runMetadata,
            item,
            traceId,
            predictionObservationId ?? activity?.SpanId.ToString(),
            itemScores,
            cancellationToken);

        return new PreparedExperimentExecutionResult(
            datasetRunItem.DatasetRunId,
            new PreparedExperimentExecutionSummary(
                item.SliceDatasetItemId,
                item.SourceDatasetItemId,
                runName,
                traceId,
                CreatePredictionOrNull(participantPrediction),
                itemScores,
                traceTags,
                null,
                participantPrediction.Status));
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
                Id: PreparedExperimentSupport.CreateScoreId("total_kicktipp_points", datasetRunId),
                Metadata: runMetadataPayload,
                Environment: "sdk-experiment"),
            cancellationToken);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "avg_kicktipp_points",
                aggregateScores.AvgKicktippPoints,
                DatasetRunId: datasetRunId,
                Comment: $"Aggregate score for {runMetadata.SampleSize} item(s)",
                Id: PreparedExperimentSupport.CreateScoreId("avg_kicktipp_points", datasetRunId),
                Metadata: runMetadataPayload,
                Environment: "sdk-experiment"),
            cancellationToken);

        return aggregateScores;
    }

    private async Task PostItemScoreAsync(
        string datasetRunId,
        string datasetName,
        string runName,
        string experimentName,
        PreparedExperimentRunMetadata runMetadata,
        PreparedExperimentManifestItem item,
        string traceId,
        string? observationId,
        ExperimentItemScores itemScores,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.SerializeToElement(new
        {
            datasetRunId,
            datasetRunName = runName,
            datasetName,
            datasetItemId = item.SliceDatasetItemId,
            sourceDatasetItemId = item.SourceDatasetItemId,
            experiment_name = experimentName,
            experiment_run_name = runName,
            task = runMetadata.TaskType,
            runSubjectKind = runMetadata.RunSubjectKind,
            runSubjectId = runMetadata.RunSubjectId,
            runSubjectDisplayName = runMetadata.RunSubjectDisplayName,
            reasoningEffort = runMetadata.ReasoningEffort,
            item.HomeTeam,
            item.AwayTeam,
            item.Matchday,
            item.TippSpielId
        }, PreparedExperimentCommandSupport.JsonOptions);

        await _langfuseClient.CreateScoreAsync(
            new LangfuseCreateScoreRequest(
                "kicktipp_points",
                itemScores.KicktippPoints,
                TraceId: traceId,
                ObservationId: string.IsNullOrWhiteSpace(observationId) ? null : observationId,
                DataType: "NUMERIC",
                Comment: $"Item score for {item.HomeTeam} vs {item.AwayTeam}",
                Id: PreparedExperimentSupport.CreateScoreId(
                    "kicktipp_points",
                    datasetRunId,
                    traceId,
                    observationId,
                    item.SliceDatasetItemId),
                Metadata: metadata,
                Environment: "sdk-experiment"),
            cancellationToken);
    }

    private async Task<LangfusePaginatedResponse<LangfuseDatasetRunItem>> WaitForDatasetRunItemsAsync(
        string datasetId,
        string runName,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        var limit = Math.Min(100, Math.Max(1, expectedCount));

        for (var attempt = 0; attempt < 6; attempt += 1)
        {
            var datasetRunItems = await _langfuseClient.ListDatasetRunItemsAsync(
                datasetId,
                runName,
                1,
                limit,
                cancellationToken);

            if (datasetRunItems.Meta.TotalItems >= expectedCount)
            {
                return datasetRunItems;
            }

            PreparedExperimentSupport.ReportProgress(
                $"Waiting for Langfuse dataset run items for '{runName}': {datasetRunItems.Meta.TotalItems}/{expectedCount} visible after poll {attempt + 1}/6; retrying in 00:00:02.");
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
        string experimentName,
        string? runDescription,
        string datasetName,
        PreparedExperimentRunMetadata runMetadata,
        PreparedExperimentManifestItem item,
        string? tippSpielId,
        IReadOnlyList<string> traceTags,
        IReadOnlyDictionary<string, string> propagatedMetadata,
        DateTimeOffset? evaluationTimestamp = null,
        string? promptTemplatePath = null)
    {
        activity?.SetTag("langfuse.trace.name", "experiment-item-run");
        LangfuseActivityPropagation.SetEnvironment(activity, "sdk-experiment");
        LangfuseActivityPropagation.SetSessionId(activity, runName);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);

        activity?.SetTag("langfuse.experiment.name", runName);
        activity?.SetTag("langfuse.experiment.description", runDescription);
        activity?.SetTag("langfuse.experiment.item.id", item.SliceDatasetItemId);
        activity?.SetTag("langfuse.experiment.item.root_observation_id", activity.SpanId.ToString());

        foreach (var metadata in propagatedMetadata)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, metadata.Key, metadata.Value);
        }

        LangfuseActivityPropagation.SetTraceMetadata(activity, "experiment_name", experimentName);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "experiment_run_name", runName);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "datasetName", datasetName, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "datasetItemId", item.SliceDatasetItemId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "dataset_item_id", item.SliceDatasetItemId, propagateToObservations: false);
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
        if (evaluationTimestamp is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(
                activity,
                "evaluationTimestamp",
                evaluationTimestamp.Value.ToString("O"),
                propagateToObservations: false);
        }

        LangfuseActivityPropagation.SetTraceMetadata(activity, "tippSpielId", tippSpielId, propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "promptTemplatePath", promptTemplatePath, propagateToObservations: false);
    }

    private static void SetTraceAndRootObservationInput(Activity? activity, string inputJson)
    {
        if (activity is null || string.IsNullOrWhiteSpace(inputJson))
        {
            return;
        }

        activity.SetTag("langfuse.trace.input", inputJson);
        activity.SetTag("langfuse.observation.input", inputJson);
    }

    private static void SetTraceAndRootObservationOutput(Activity? activity, string outputJson)
    {
        if (activity is null || string.IsNullOrWhiteSpace(outputJson))
        {
            return;
        }

        activity.SetTag("langfuse.trace.output", outputJson);
        activity.SetTag("langfuse.observation.output", outputJson);
    }

    private static void SetExperimentRunId(Activity? activity, string datasetRunId)
    {
        if (activity is null || string.IsNullOrWhiteSpace(datasetRunId))
        {
            return;
        }

        activity.SetTag("langfuse.experiment.id", datasetRunId);
    }

    private static IReadOnlyList<PreparedExperimentParticipantManifest> SelectParticipants(
        PreparedExperimentManifest manifest,
        PreparedExperimentCommunityRunRequest request)
    {
        var participants = manifest.Participants
            .OrderBy(participant => participant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(participant => participant.ParticipantId, StringComparer.Ordinal)
            .ToList();

        if (request.ParticipantIds.Count > 0)
        {
            var filteredParticipants = participants
                .Where(participant => request.ParticipantIds.Contains(participant.ParticipantId))
                .ToList();
            var missingParticipantIds = request.ParticipantIds
                .Except(filteredParticipants.Select(participant => participant.ParticipantId), StringComparer.Ordinal)
                .OrderBy(participantId => participantId, StringComparer.Ordinal)
                .ToList();
            if (missingParticipantIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The community-to-date manifest does not contain participant id(s): {string.Join(", ", missingParticipantIds)}.");
            }

            participants = filteredParticipants;
        }

        if (request.ParticipantLimit is not null)
        {
            participants = participants.Take(request.ParticipantLimit.Value).ToList();
        }

        if (participants.Count == 0)
        {
            throw new InvalidOperationException("No participants remain after applying the requested community-to-date filters.");
        }

        return participants;
    }

    private static PreparedExperimentRunMetadata BuildCommunityRunMetadata(
        PreparedExperimentManifest manifest,
        PreparedExperimentParticipantManifest participant,
        string datasetName,
        string startedAtUtc,
        int batchSize)
    {
        return new PreparedExperimentRunMetadata
        {
            Runner = "community-match-experiment-runner",
            TaskType = "community-to-date",
            CommunityContext = manifest.CommunityContext,
            Competition = manifest.Competition,
            SourceDatasetName = manifest.SourceDatasetName,
            DatasetName = datasetName,
            SliceKind = string.IsNullOrWhiteSpace(manifest.SliceKind)
                ? "community-to-date"
                : manifest.SliceKind,
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
            StartedAtUtc = startedAtUtc,
            SampleSeed = manifest.SampleSeed,
            SampleMethod = string.IsNullOrWhiteSpace(manifest.SampleMethod)
                ? "community-to-date"
                : manifest.SampleMethod,
            IncludeJustification = false,
            SourceDatasetKind = "community-to-date",
            DatasetItemIdMap = PreparedExperimentSupport.CreateDatasetItemIdMap(manifest),
            Model = participant.DisplayName,
            ObservationName = "community-match-prediction",
            RunSubjectKind = "participant",
            RunSubjectId = participant.ParticipantId,
            RunSubjectDisplayName = participant.DisplayName,
            BatchStrategy = "simple-batched",
            BatchSize = batchSize,
            BatchCount = null
        };
    }

    private static string BuildCommunityRunFamilyName(PreparedExperimentManifest manifest, string startedAtUtc)
    {
        var communityToken = ExperimentArtifactSupport.Slugify(manifest.CommunityContext);
        var sliceToken = ExperimentArtifactSupport.Slugify(string.IsNullOrWhiteSpace(manifest.SliceKey) ? "community-to-date" : manifest.SliceKey);
        return $"community-to-date__{communityToken}__{sliceToken}__{BuildRunTimestampToken(startedAtUtc)}";
    }

    private static string BuildCommunityParticipantRunName(
        string runFamilyName,
        PreparedExperimentParticipantManifest participant)
    {
        var participantToken = ExperimentArtifactSupport.Slugify($"{participant.DisplayName}-{participant.ParticipantId}");
        return $"{runFamilyName}__{participantToken}";
    }

    private static string BuildRunTimestampToken(string startedAtUtc)
    {
        return startedAtUtc.ToLowerInvariant().Replace(':', '-');
    }

    private static JsonElement CreateCommunityPredictionPayload(PreparedExperimentParticipantPrediction prediction)
    {
        return JsonSerializer.SerializeToElement(new
        {
            status = prediction.Status,
            homeGoals = prediction.HomeGoals,
            awayGoals = prediction.AwayGoals,
            kicktippPoints = prediction.KicktippPoints
        }, PreparedExperimentCommandSupport.JsonOptions);
    }

    private static Prediction? CreatePredictionOrNull(PreparedExperimentParticipantPrediction prediction)
    {
        return prediction.HomeGoals is int homeGoals && prediction.AwayGoals is int awayGoals
            ? new Prediction(homeGoals, awayGoals)
            : null;
    }

    private static void ConfigureCommunityPredictionObservation(
        Activity? activity,
        PreparedExperimentParticipantManifest participant,
        PreparedExperimentParticipantPrediction prediction,
        PreparedExperimentManifestItem item,
        JsonElement predictionPayload)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("langfuse.observation.type", "generation");
        activity.SetTag("gen_ai.request.model", "kicktipp-community");
        activity.SetTag("langfuse.observation.input", JsonSerializer.Serialize(new
        {
            source = "kicktipp-community",
            participantId = participant.ParticipantId,
            participantDisplayName = participant.DisplayName,
            item.SourceDatasetItemId,
            item.TippSpielId
        }, TraceJsonOptions));
        activity.SetTag("langfuse.observation.output", predictionPayload.GetRawText());
        new PredictionTelemetryMetadata(item.HomeTeam, item.AwayTeam).ApplyToObservation(activity);
        activity.SetTag("langfuse.observation.metadata.participantId", participant.ParticipantId);
        activity.SetTag("langfuse.observation.metadata.participantDisplayName", participant.DisplayName);
        activity.SetTag("langfuse.observation.metadata.predictionStatus", prediction.Status);
        activity.SetTag("langfuse.observation.metadata.sourceDatasetItemId", item.SourceDatasetItemId);

        if (!string.IsNullOrWhiteSpace(item.TippSpielId))
        {
            activity.SetTag("langfuse.observation.metadata.tippSpielId", item.TippSpielId);
        }
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

    private sealed record ExperimentPromptRoute(
        IInstructionsTemplateProvider? TemplateProvider,
        LangfusePromptTraceMetadata? TraceMetadata);
}

internal sealed record PreparedExperimentRunRequest(
    string ManifestPath,
    string RunName,
    string? RunDescription,
    string? RunMetadataFile,
    bool ReplaceRun,
    PreparedExperimentRunOptions Options);

internal sealed record PreparedExperimentCommunityRunRequest(
    string ManifestPath,
    string? RunFamilyName,
    string? RunDescription,
    string? DatasetName,
    bool ReplaceRuns,
    int BatchSize,
    int? ParticipantLimit,
    IReadOnlySet<string> ParticipantIds);
