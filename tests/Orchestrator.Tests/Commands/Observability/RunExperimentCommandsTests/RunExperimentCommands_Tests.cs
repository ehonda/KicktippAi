using System.Diagnostics;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.Experiments;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.RunExperimentCommandsTests;

[NotInParallel("Telemetry")]
public class RunExperimentCommands_Tests
{
    [Test]
    public async Task Warmup_then_batch_chunks_with_twenty_five_items_and_three_batches_runs_warmup_then_three_equal_batches()
    {
        var items = Enumerable.Range(1, 25).ToArray();

        var chunks = PreparedExperimentSupport.CreateWarmupThenBatchChunks(items, 3);

        await Assert.That(chunks.Select(chunk => chunk.Count).ToArray()).IsEquivalentTo([1, 8, 8, 8]);
        await Assert.That(chunks[0]).IsEquivalentTo([1]);
        await Assert.That(chunks[1]).IsEquivalentTo([2, 3, 4, 5, 6, 7, 8, 9]);
        await Assert.That(chunks[2]).IsEquivalentTo([10, 11, 12, 13, 14, 15, 16, 17]);
        await Assert.That(chunks[3]).IsEquivalentTo([18, 19, 20, 21, 22, 23, 24, 25]);
    }

    [Test]
    public async Task Repeated_match_slice_batches_keep_per_fixture_warmup_batches_and_limit_parallel_workflows()
    {
        var items = Enumerable.Range(1, 3)
            .SelectMany(fixtureIndex => Enumerable.Range(1, 3).Select(repetitionIndex => new PreparedExperimentManifestItem
            {
                SourceDatasetItemId = $"source-{fixtureIndex}",
                SliceDatasetItemId = $"source-{fixtureIndex}__repeated-match-slice__random-3x3__m{fixtureIndex:00}__{repetitionIndex:00}",
                HomeTeam = $"Home {fixtureIndex}",
                AwayTeam = $"Away {fixtureIndex}",
                Matchday = fixtureIndex,
                StartsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)",
                FixtureIndex = fixtureIndex,
                RepetitionIndex = repetitionIndex
            }))
            .ToList();

        var batches = PreparedExperimentRunExecutor.CreateRepeatedMatchSliceBatches(
            items,
            batchCount: 2,
            parallelism: 2);

        await Assert.That(batches.Select(batch => batch.Count).ToArray()).IsEquivalentTo([2, 2, 2, 1, 1, 1]);
        await Assert.That(batches[0].Select(item => item.FixtureIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([1, 2]);
        await Assert.That(batches[0].All(item => item.RepetitionIndex == 1)).IsTrue();
        await Assert.That(batches[1].Select(item => item.RepetitionIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([2, 2]);
        await Assert.That(batches[2].Select(item => item.RepetitionIndex.GetValueOrDefault()).ToArray()).IsEquivalentTo([3, 3]);
        await Assert.That(batches[3].Single().FixtureIndex).IsEqualTo(3);
        await Assert.That(batches[3].Single().RepetitionIndex).IsEqualTo(1);
    }

    [Test]
    public async Task Repeated_match_slice_score_summary_averages_totals_by_repetition()
    {
        var summaries = new[]
        {
            CreateExecutionSummary("fixture-1", 1, 1, 4),
            CreateExecutionSummary("fixture-2", 2, 1, 2),
            CreateExecutionSummary("fixture-1", 1, 2, 0),
            CreateExecutionSummary("fixture-2", 2, 2, 2)
        };

        var scores = PreparedExperimentSupport.SummarizeExecutionScores(
            summaries,
            "repeated-match-slice");

        await Assert.That(scores.TotalKicktippPoints).IsEqualTo(8);
        await Assert.That(scores.AvgKicktippPoints).IsEqualTo(4);
    }

    [Test]
    public async Task Run_repeated_match_slice_settings_default_parallelism_to_five_and_validate_bounds()
    {
        var settings = new RunRepeatedMatchSliceSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name"
        };
        var invalidSettings = new RunRepeatedMatchSliceSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            Parallelism = 0
        };

        var result = settings.Validate();
        var invalidResult = invalidSettings.Validate();
        var options = settings.ToRunOptions();

        await Assert.That(result.Successful).IsTrue();
        await Assert.That(options.BatchCount).IsEqualTo(3);
        await Assert.That(options.Parallelism).IsEqualTo(5);
        await Assert.That(invalidResult.Successful).IsFalse();
        await Assert.That(invalidResult.Message).Contains("--parallelism must be at least 1");
    }

    [Test]
    public async Task Run_experiment_settings_require_langfuse_prompt_name_for_langfuse_prompt_source()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            PromptSource = "langfuse"
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--langfuse-prompt-name is required");
    }

    private static PreparedExperimentExecutionSummary CreateExecutionSummary(
        string sourceDatasetItemId,
        int fixtureIndex,
        int repetitionIndex,
        int kicktippPoints)
    {
        return new PreparedExperimentExecutionSummary(
            $"{sourceDatasetItemId}__repeated-match-slice__slice__m{fixtureIndex:00}__{repetitionIndex:00}",
            sourceDatasetItemId,
            "run-name",
            $"trace-{fixtureIndex}-{repetitionIndex}",
            null,
            new ExperimentItemScores(kicktippPoints),
            [],
            null,
            "placed",
            fixtureIndex,
            repetitionIndex);
    }

    [Test]
    public async Task Run_experiment_settings_reject_langfuse_prompt_source_with_justification()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            PromptSource = "langfuse",
            LangfusePromptName = "kicktippai/predict-one-match-o3-poc",
            IncludeJustification = true
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("does not support --include-justification");
    }

    [Test]
    public async Task Run_experiment_settings_accept_and_normalize_reasoning_effort_values()
    {
        var noneSettings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = "None"
        };
        var xhighSettings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = " XHIGH "
        };

        var noneResult = noneSettings.Validate();
        var xhighResult = xhighSettings.Validate();

        await Assert.That(noneResult.Successful).IsTrue();
        await Assert.That(xhighResult.Successful).IsTrue();
        await Assert.That(noneSettings.ReasoningEffort).IsEqualTo("none");
        await Assert.That(xhighSettings.ReasoningEffort).IsEqualTo("xhigh");
    }

    [Test]
    public async Task Run_experiment_settings_reject_invalid_reasoning_effort_values()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.5",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            ReasoningEffort = "maximum"
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--reasoning-effort must be one of");
    }

    [Test]
    public async Task Run_experiment_settings_reject_invalid_max_output_token_count()
    {
        var settings = new RunRepeatedMatchSettings
        {
            Model = "gpt-5.4-nano",
            ManifestPath = "slice-manifest.json",
            RunName = "run-name",
            MaxOutputTokenCount = 0
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("--max-output-tokens must be at least 1");
    }

    [Test]
    public async Task Langfuse_prompt_run_options_flow_into_experiment_metadata_tags_and_propagated_metadata()
    {
        var manifest = new PreparedExperimentManifest
        {
            SliceKey = "repeat-25",
            SliceKind = "repeated-match",
            SampleMethod = "repeated-match",
            CommunityContext = "pes-squad",
            SourcePoolKey = "md26-vfb-stuttgart-vs-rb-leipzig",
            SourceDatasetName = "match-predictions/bundesliga-2025-26/pes-squad",
            SliceDatasetName = "match-predictions/bundesliga-2025-26/pes-squad/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25",
            Competition = "bundesliga-2025-26",
            Season = "2025/2026",
            SampleSize = 25,
            Items =
            [
                new PreparedExperimentManifestItem
                {
                    SourceDatasetItemId = "source-item",
                    SliceDatasetItemId = "slice-item",
                    HomeTeam = "VfB Stuttgart",
                    AwayTeam = "RB Leipzig",
                    Matchday = 26,
                    StartsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)"
                }
            ]
        };
        var options = new PreparedExperimentRunOptions(
            "gpt-5.5",
            "langfuse-o3-poc",
            false,
            "2026-03-15T12:00:00 Europe/Berlin (+01)",
            null,
            null,
            null,
            "langfuse",
            "kicktippai/predict-one-match-o3-poc",
            "poc",
            7,
            "warmup-plus-batches",
            null,
            3,
            "xhigh",
            20_000);

        var metadata = PreparedExperimentSupport.BuildRunMetadata(manifest, options);
        var tags = PreparedExperimentSupport.DeriveTraceTags(metadata);
        var propagatedMetadata = PreparedExperimentSupport.DerivePropagatedMetadata(metadata);

        await Assert.That(metadata.PromptSource).IsEqualTo("langfuse");
        await Assert.That(metadata.LangfusePromptName).IsEqualTo("kicktippai/predict-one-match-o3-poc");
        await Assert.That(metadata.LangfusePromptLabel).IsEqualTo("poc");
        await Assert.That(metadata.LangfusePromptVersion).IsEqualTo(7);
        await Assert.That(metadata.ReasoningEffort).IsEqualTo("xhigh");
        await Assert.That(metadata.MaxOutputTokenCount).IsEqualTo(20_000);
        await Assert.That(metadata.RunSubjectId).IsEqualTo("gpt-5.5:reasoning-effort:xhigh");
        await Assert.That(metadata.RunSubjectDisplayName).IsEqualTo("gpt-5.5 (xhigh)");
        await Assert.That(tags).Contains("prompt-source:langfuse");
        await Assert.That(tags).Contains("langfuse-prompt:kicktippai/predict-one-match-o3-poc");
        await Assert.That(tags).Contains("langfuse-prompt-label:poc");
        await Assert.That(tags).Contains("langfuse-prompt-version:7");
        await Assert.That(tags).Contains("reasoning-effort:xhigh");
        await Assert.That(tags).Contains("max-output-tokens:20000");
        await Assert.That(propagatedMetadata["promptSource"]).IsEqualTo("langfuse");
        await Assert.That(propagatedMetadata["langfusePromptVersion"]).IsEqualTo("7");
        await Assert.That(propagatedMetadata["reasoningEffort"]).IsEqualTo("xhigh");
        await Assert.That(propagatedMetadata["maxOutputTokens"]).IsEqualTo("20000");
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_slice_reconstructs_predicts_and_posts_scores()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var runMetadataPath = Path.Combine(tempDirectory.FullName, "run-metadata.json");
            var runName = "slice__test-community__gpt-5-nano__prompt-v1__random-1-seed-20251011__startsat-12h__2026-01-10t12-00-00z";
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/slices/all-matchdays/random-1-seed-20251011";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20251011";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    sliceKey = "random-1-seed-20251011",
                    sliceKind = "random-sample",
                    sampleMethod = "random-sample",
                    communityContext = "test-community",
                    sourcePoolKey = "all-matchdays",
                    canonicalDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSeed = 20251011,
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-123",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "FC Bayern München",
                            awayTeam = "RB Leipzig",
                            matchday = 7,
                            startsAt = "2025-10-30T15:30:00 Europe/Berlin (+01)"
                        }
                    }
                }));

            await File.WriteAllTextAsync(
                runMetadataPath,
                JsonSerializer.Serialize(new
                {
                    runner = "match-experiment-runner",
                    task = "slice",
                    communityContext = "test-community",
                    competition = "bundesliga-2025-26",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    datasetName,
                    promptKey = "prompt-v1",
                    sliceKind = "random-sample",
                    sliceKey = "random-1-seed-20251011",
                    sourcePoolKey = "all-matchdays",
                    selectedItemIdsHash = "hash-123",
                    selectedItemIdsCount = 1,
                    sampleSize = 1,
                    evaluationTimestampPolicyKey = "startsat-12h",
                    evaluationTimestampPolicy = new
                    {
                        kind = "relative",
                        reference = "startsAt",
                        offset = "-12:00:00"
                    },
                    startedAtUtc = "2026-01-10T12:00:00Z",
                    sampleSeed = 20251011,
                    sampleMethod = "random-sample",
                    includeJustification = false,
                    promptVersion = "prompt-v1",
                    sourceDatasetKind = "slice",
                    datasetItemIdMap = new Dictionary<string, string>
                    {
                        [sourceDatasetItemId] = sliceDatasetItemId
                    },
                    model = "gpt-5-nano",
                    batchStrategy = "simple-batched",
                    batchSize = 1
                }));

            var match = new Match(
                "FC Bayern München",
                "RB Leipzig",
                NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc(),
                7);
            var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
            predictionRepository
                .Setup(repository => repository.GetStoredMatchAsync(
                    "FC Bayern München",
                    "RB Leipzig",
                    7,
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(match);

            var evaluationTimestamp = new DateTimeOffset(2025, 10, 30, 3, 30, 0, TimeSpan.FromHours(1));
            var selection = MatchContextDocumentCatalog.ForMatch("FC Bayern München", "RB Leipzig", "test-community");
            var contextRepository = new Mock<IContextRepository>();
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        evaluationTimestamp,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(documentName, $"content:{documentName}", 1, evaluationTimestamp.AddMinutes(-5)));
            }

            foreach (var documentName in selection.OptionalDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        evaluationTimestamp,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ContextDocument?)null);
            }

            var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    7,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "FC Bayern München",
                        "RB Leipzig",
                        NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc(),
                        7,
                        2,
                        1,
                        MatchOutcomeAvailability.Completed,
                        "123",
                        evaluationTimestamp,
                        evaluationTimestamp)
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory.Setup(factory => factory.CreatePredictionRepository((string?)null)).Returns(predictionRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateContextRepository((string?)null)).Returns(contextRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateMatchOutcomeRepository((string?)null)).Returns(matchOutcomeRepository.Object);
            firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((FirestoreDb)null!);

            var predictionService = CreateMockPredictionService(
                predictMatchResult: new Prediction(2, 1),
                matchPromptPath: "prompts/gpt-5/match.md");
            predictionService
                .Setup(service => service.PredictMatchAsync(
                    It.IsAny<Match>(),
                    It.IsAny<IEnumerable<DocumentContext>>(),
                    It.IsAny<bool>(),
                    It.IsAny<PredictionTelemetryMetadata?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    using var childActivity = Telemetry.Source.StartActivity("predict-match");
                    childActivity?.SetTag("langfuse.observation.type", "generation");
                    return new Prediction(2, 1);
                });
            var openAiServiceFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var createdDatasetRunItems = new List<LangfuseCreateDatasetRunItemRequest>();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => createdDatasetRunItems.Add(request))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    "dataset-run-item-1",
                    "dataset-run-1",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    runName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    runName,
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        runName,
                        sliceDatasetItemId,
                        "trace-id",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunSliceCommand>(
                "run-slice",
                firebaseServiceFactory: firebaseFactory,
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-slice",
                "gpt-5-nano",
                "--manifest",
                manifestPath,
                "--run-name",
                runName,
                "--run-metadata-file",
                runMetadataPath,
                "--batch-size",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"executionCount\": 1");
            await Assert.That(output).Contains("\"taskType\": \"slice\"");
            await Assert.That(output).Contains("\"total_kicktipp_points\": 4");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo(["avg_kicktipp_points", "kicktipp_points", "total_kicktipp_points"]);
            await Assert.That(postedScores.All(score => !string.IsNullOrWhiteSpace(score.Id))).IsTrue();
            await Assert.That(postedScores.Select(score => score.Id).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(3);
            var experimentItemRun = capturedActivities.Single(activity => activity.OperationName == "experiment-item-run");
            var experimentItemInput = experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString();
            await Assert.That(experimentItemInput).Contains("RB Leipzig");
            await Assert.That(experimentItemInput).Contains("2025-10-30T15:30:00 Europe/Berlin");
            await Assert.That(experimentItemInput).DoesNotContain("datasetName");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.input")?.ToString()).IsEqualTo(experimentItemInput);
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.expected_output")?.ToString())
                .IsEqualTo("{\"score\":\"2:1\"}");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.metadata")?.ToString())
                .Contains(sliceDatasetItemId);
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.output")?.ToString()).Contains("\"homeGoals\":2");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.tags")?.ToString()).DoesNotContain("phase-2");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.trace.tags")?.ToString()).DoesNotContain("experiment");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.id")).IsEqualTo("dataset-run-1");
            await Assert.That(createdDatasetRunItems.Single().ObservationId).IsEqualTo(experimentItemRun.SpanId.ToString());
            var predictMatchActivity = capturedActivities.Single(activity => activity.OperationName == "predict-match");
            await Assert.That(predictMatchActivity.GetBaggageItem("langfuse.experiment.id")).IsEqualTo("dataset-run-1");
            await Assert.That(predictMatchActivity.GetBaggageItem("langfuse.experiment.item.id")).IsEqualTo(sliceDatasetItemId);

            langfuseClient.Verify(client => client.CreateDatasetRunItemAsync(It.IsAny<LangfuseCreateDatasetRunItemRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            langfuseClient.Verify(client => client.CreateScoreAsync(It.IsAny<LangfuseCreateScoreRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_repeated_match_without_metadata_file_uses_direct_settings_and_exact_evaluation_time()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var runName = "repeated-match__test-community__gpt-5-nano__prompt-v1__repeat-1__exact-time__2026-03-15t12-00-00z";
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-1";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__repeated-match__repeat-1__01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    sliceKey = "repeat-1",
                    sliceKind = "repeated-match",
                    sampleMethod = "repeated-match",
                    communityContext = "test-community",
                    sourcePoolKey = "md26-vfb-stuttgart-vs-rb-leipzig",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-456",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "VfB Stuttgart",
                            awayTeam = "RB Leipzig",
                            matchday = 26,
                            startsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)"
                        }
                    }
                }));

            var exactEvaluationTime = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.FromHours(1));
            var match = new Match(
                "VfB Stuttgart",
                "RB Leipzig",
                NodaTime.Instant.FromUtc(2026, 3, 15, 14, 30).InUtc(),
                26);

            var predictionRepository = new Mock<IPredictionRepository>(MockBehavior.Strict);
            predictionRepository
                .Setup(repository => repository.GetStoredMatchAsync(
                    "VfB Stuttgart",
                    "RB Leipzig",
                    26,
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(match);

            var selection = MatchContextDocumentCatalog.ForMatch("VfB Stuttgart", "RB Leipzig", "test-community");
            var contextRepository = new Mock<IContextRepository>();
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        exactEvaluationTime,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ContextDocument(documentName, $"content:{documentName}", 1, exactEvaluationTime.AddMinutes(-5)));
            }

            foreach (var documentName in selection.OptionalDocumentNames)
            {
                contextRepository
                    .Setup(repository => repository.GetContextDocumentByTimestampAsync(
                        documentName,
                        exactEvaluationTime,
                        "test-community",
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ContextDocument?)null);
            }

            var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    26,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "VfB Stuttgart",
                        "RB Leipzig",
                        NodaTime.Instant.FromUtc(2026, 3, 15, 14, 30).InUtc(),
                        26,
                        2,
                        1,
                        MatchOutcomeAvailability.Completed,
                        "123",
                        exactEvaluationTime,
                        exactEvaluationTime)
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory.Setup(factory => factory.CreatePredictionRepository((string?)null)).Returns(predictionRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateContextRepository((string?)null)).Returns(contextRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateMatchOutcomeRepository((string?)null)).Returns(matchOutcomeRepository.Object);
            firebaseFactory.SetupGet(factory => factory.FirestoreDb).Returns((Google.Cloud.Firestore.FirestoreDb)null!);

            var predictionService = CreateMockPredictionService(
                predictMatchResult: new Prediction(2, 1),
                matchPromptPath: "prompts/gpt-5/match.md");
            var openAiServiceFactory = CreateMockOpenAiServiceFactory(predictionService: predictionService);

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    "dataset-run-item-1",
                    "dataset-run-1",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    runName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    runName,
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        runName,
                        sliceDatasetItemId,
                        "trace-id",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunRepeatedMatchCommand>(
                "run-repeated-match",
                firebaseServiceFactory: firebaseFactory,
                configureServices: new Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-repeated-match",
                "gpt-5-nano",
                "--manifest",
                manifestPath,
                "--run-name",
                runName,
                "--prompt-key",
                "prompt-v1",
                "--evaluation-time",
                "2026-03-15T12:00:00 Europe/Berlin (+01)",
                "--batch-count",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"executionCount\": 1");
            await Assert.That(output).Contains("\"taskType\": \"repeated-match\"");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo(["avg_kicktipp_points", "kicktipp_points", "total_kicktipp_points"]);
            var experimentItemRun = capturedActivities.Single(activity => activity.OperationName == "experiment-item-run");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString())
                .Contains("VfB Stuttgart vs RB Leipzig");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.observation.input")?.ToString())
                .DoesNotContain("datasetName");
            await Assert.That(experimentItemRun.GetTagItem("langfuse.experiment.item.expected_output")?.ToString())
                .IsEqualTo("{\"score\":\"2:1\"}");

            contextRepository.Verify(repository => repository.GetContextDocumentByTimestampAsync(
                It.IsAny<string>(),
                exactEvaluationTime,
                "test-community",
                It.IsAny<CancellationToken>()), Times.AtLeast(selection.RequiredDocumentNames.Count));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    [NotInParallel("ProcessState")]
    public async Task Running_run_community_to_date_creates_one_dataset_run_per_participant()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var capturedActivities = new List<Activity>();

        try
        {
            var manifestPath = Path.Combine(tempDirectory.FullName, "slice-manifest.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/community-to-date/through-md01/community-to-date-md01";
            var sliceDatasetItemId = "bundesliga-2025-26__test-community__ts123__slice__community-to-date-md01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    task = "community-to-date",
                    sliceKey = "community-to-date-md01",
                    sliceKind = "community-to-date",
                    sampleMethod = "community-to-date",
                    communityContext = "test-community",
                    sourcePoolKey = "through-md01",
                    sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                    sliceDatasetName = datasetName,
                    competition = "bundesliga-2025-26",
                    season = "2025/2026",
                    sampleSize = 1,
                    selectedItemIds = new[] { sourceDatasetItemId },
                    selectedItemIdsHash = "hash-community-1",
                    items = new[]
                    {
                        new
                        {
                            sourceDatasetItemId,
                            sliceDatasetItemId,
                            homeTeam = "Team A",
                            awayTeam = "Team B",
                            matchday = 1,
                            startsAt = "2025-08-22T20:30:00 Europe/Berlin (+02)",
                            tippSpielId = "123"
                        }
                    },
                    participants = new object[]
                    {
                        new
                        {
                            participantId = "p1",
                            displayName = "Alice",
                            predictions = new[]
                            {
                                new
                                {
                                    sourceDatasetItemId,
                                    status = "placed",
                                    homeGoals = 2,
                                    awayGoals = 1,
                                    kicktippPoints = 4
                                }
                            }
                        },
                        new
                        {
                            participantId = "p2",
                            displayName = "Bob",
                            predictions = new[]
                            {
                                new
                                {
                                    sourceDatasetItemId,
                                    status = "missed",
                                    homeGoals = (int?)null,
                                    awayGoals = (int?)null,
                                    kicktippPoints = 0
                                }
                            }
                        }
                    }
                }));

            var postedScores = new List<LangfuseCreateScoreRequest>();
            var openAiServiceFactory = CreateMockOpenAiServiceFactory();
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    $"{request.RunName}-item-1",
                    request.RunName.Contains("alice", StringComparison.Ordinal) ? "dataset-run-1" : "dataset-run-2",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            langfuseClient
                .Setup(client => client.CreateScoreAsync(
                    It.IsAny<LangfuseCreateScoreRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => postedScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    It.Is<string>(runName => runName.Contains("alice", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string runName, CancellationToken _) => new LangfuseDatasetRunWithItems(
                    "dataset-run-1",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.GetDatasetRunAsync(
                    datasetName,
                    It.Is<string>(runName => runName.Contains("bob", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string runName, CancellationToken _) => new LangfuseDatasetRunWithItems(
                    "dataset-run-2",
                    runName,
                    "dataset-1",
                    datasetName,
                    null,
                    default,
                    []));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    It.Is<string>(runName => runName.Contains("alice", StringComparison.Ordinal)),
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-1",
                        "dataset-run-1",
                        "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__alice-p1",
                        sliceDatasetItemId,
                        "trace-alice",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            langfuseClient
                .Setup(client => client.ListDatasetRunItemsAsync(
                    "dataset-1",
                    It.Is<string>(runName => runName.Contains("bob", StringComparison.Ordinal)),
                    1,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [new LangfuseDatasetRunItem(
                        "dataset-run-item-2",
                        "dataset-run-2",
                        "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__bob-p2",
                        sliceDatasetItemId,
                        "trace-bob",
                        null,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));

            using var listener = CreateActivityListener(capturedActivities);
            var context = CreateCommandApp<RunCommunityToDateCommand>(
                "run-community-to-date",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(openAiServiceFactory.Object);
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "run-community-to-date",
                "--manifest",
                manifestPath,
                "--run-family-name",
                "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z",
                "--participant-limit",
                "2",
                "--batch-size",
                "1");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"taskType\": \"community-to-date\"");
            await Assert.That(output).Contains("\"runCount\": 2");
            await Assert.That(output).Contains("\"executionCount\": 2");
            await Assert.That(postedScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo([
                    "avg_kicktipp_points",
                    "avg_kicktipp_points",
                    "kicktipp_points",
                    "kicktipp_points",
                    "total_kicktipp_points",
                    "total_kicktipp_points"
                ]);
            await Assert.That(postedScores.All(score => !string.IsNullOrWhiteSpace(score.Id))).IsTrue();
            await Assert.That(postedScores.Select(score => score.Id).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(6);
            await Assert.That(capturedActivities.Any(activity => activity.OperationName == "community-match-prediction")).IsTrue();
            await Assert.That(postedScores.Where(score => score.Name == "kicktipp_points").All(score => !string.IsNullOrWhiteSpace(score.ObservationId))).IsTrue();

            var experimentItemRuns = capturedActivities
                .Where(activity => activity.OperationName == "experiment-item-run")
                .ToList();
            await Assert.That(experimentItemRuns.Count).IsEqualTo(2);
            await Assert.That(experimentItemRuns.All(activity =>
            {
                var input = activity.GetTagItem("langfuse.observation.input")?.ToString();
                return input is not null && input.Contains("Team A vs Team B", StringComparison.Ordinal);
            })).IsTrue();
            await Assert.That(experimentItemRuns.All(activity =>
            {
                var input = activity.GetTagItem("langfuse.observation.input")?.ToString();
                return input is not null && !input.Contains("datasetName", StringComparison.Ordinal);
            })).IsTrue();

            var predictionObservations = capturedActivities
                .Where(activity => activity.OperationName == "community-match-prediction")
                .ToList();
            await Assert.That(predictionObservations.Count).IsEqualTo(2);
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.homeTeam")?.ToString(), "Team A", StringComparison.Ordinal))).IsTrue();
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.awayTeam")?.ToString(), "Team B", StringComparison.Ordinal))).IsTrue();
            await Assert.That(predictionObservations.All(activity =>
                string.Equals(activity.GetTagItem("langfuse.observation.metadata.match")?.ToString(), "Team A vs Team B", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
