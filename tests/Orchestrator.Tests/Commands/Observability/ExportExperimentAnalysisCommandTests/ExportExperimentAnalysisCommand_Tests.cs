using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Observability.ExportExperimentAnalysis;
using Orchestrator.Infrastructure.Langfuse;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.ExportExperimentAnalysisCommandTests;

public class ExportExperimentAnalysisCommand_Tests
{
    [Test]
    public async Task Publishing_analysis_bundle_creates_experiments_beta_alias_runs()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var inputPath = Path.Combine(tempDirectory.FullName, "analysis.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/slices/all-matchdays/random-1-seed-20260405";
            var runName = "slice__test-community__gpt-5-nano__prompt-v1__random-1-seed-20260405__startsat-12h__2026-04-05t12-00-00z";
            var targetRunName = runName + "__experiments-beta";
            var datasetItemId = "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20260405";
            var traceId = "trace-1";
            var observationId = "observation-1";

            await File.WriteAllTextAsync(
                inputPath,
                JsonSerializer.Serialize(new
                {
                    datasetName,
                    taskType = "slice",
                    primaryMetricName = "total_kicktipp_points",
                    exportedAtUtc = "2026-04-06T21:56:17Z",
                    runs = new[]
                    {
                        new
                        {
                            runName,
                            datasetRunId = "source-dataset-run-1",
                            taskType = "slice",
                            model = "gpt-5-nano",
                            promptKey = "prompt-v1",
                            sliceKind = "random-sample",
                            sliceKey = "random-1-seed-20260405",
                            sourcePoolKey = "all-matchdays",
                            selectedItemIdsHash = "hash-123",
                            selectedItemIdsCount = 1,
                            sampleSize = 1,
                            evaluationTimestampPolicyKey = "startsat-12h",
                            evaluationTime = (string?)null,
                            startedAtUtc = "2026-04-05T12:00:00Z",
                            aggregateScores = new
                            {
                                total_kicktipp_points = 4,
                                avg_kicktipp_points = 4
                            },
                            primaryMetricValue = 4,
                            rowCount = 1,
                            runSubjectKind = "model",
                            runSubjectId = "gpt-5-nano",
                            runSubjectDisplayName = "gpt-5-nano"
                        }
                    },
                    rows = new[]
                    {
                        new
                        {
                            pairingKey = datasetItemId,
                            datasetRunId = "source-dataset-run-1",
                            runName,
                            taskType = "slice",
                            model = "gpt-5-nano",
                            promptKey = "prompt-v1",
                            sliceKind = "random-sample",
                            sliceKey = "random-1-seed-20260405",
                            sourcePoolKey = "all-matchdays",
                            datasetItemId,
                            sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123",
                            traceId,
                            observationId,
                            matchday = 1,
                            homeTeam = "Home",
                            awayTeam = "Away",
                            startsAt = "2026-01-01T15:30:00 Europe/Berlin (+01)",
                            tippSpielId = "123",
                            predictedHomeGoals = 2,
                            predictedAwayGoals = 1,
                            expectedHomeGoals = 2,
                            expectedAwayGoals = 1,
                            kicktippPoints = 4,
                            predictionStatus = "placed",
                            runSubjectKind = "model",
                            runSubjectId = "gpt-5-nano",
                            runSubjectDisplayName = "gpt-5-nano"
                        }
                    }
                }));

            var createdItems = new List<LangfuseCreateDatasetRunItemRequest>();
            var createdScores = new List<LangfuseCreateScoreRequest>();
            var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            client
                .Setup(mock => mock.GetDatasetRunAsync(datasetName, targetRunName, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseDatasetRunWithItems?)null);
            client
                .Setup(mock => mock.CreateDatasetRunItemAsync(
                    It.IsAny<LangfuseCreateDatasetRunItemRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => createdItems.Add(request))
                .ReturnsAsync((LangfuseCreateDatasetRunItemRequest request, CancellationToken _) => new LangfuseDatasetRunItem(
                    "published-item-1",
                    "published-dataset-run-1",
                    request.RunName,
                    request.DatasetItemId,
                    request.TraceId,
                    request.ObservationId,
                    request.CreatedAt ?? DateTimeOffset.UtcNow,
                    request.CreatedAt ?? DateTimeOffset.UtcNow));
            client
                .Setup(mock => mock.CreateScoreAsync(It.IsAny<LangfuseCreateScoreRequest>(), It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateScoreRequest request, CancellationToken _) => createdScores.Add(request))
                .ReturnsAsync(new LangfuseCreateScoreResponse("score-1"));

            var context = CreateCommandApp<PublishExperimentAnalysisCommand>(
                "publish-experiment-analysis",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(client.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "publish-experiment-analysis",
                "--input",
                inputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"runCount\": 1");
            await Assert.That(createdItems).HasCount().EqualTo(1);
            await Assert.That(createdScores.Select(score => score.Name).OrderBy(name => name))
                .IsEquivalentTo(["avg_kicktipp_points", "total_kicktipp_points"]);

            var request = createdItems.Single();
            await Assert.That(request.RunName).IsEqualTo(targetRunName);
            await Assert.That(request.DatasetItemId).IsEqualTo(datasetItemId);
            await Assert.That(request.TraceId).IsEqualTo(traceId);
            await Assert.That(request.ObservationId).IsEqualTo(observationId);
            await Assert.That(request.CreatedAt).IsEqualTo(DateTimeOffset.Parse("2026-04-05T12:00:00Z"));

            var metadata = (JsonElement)request.Metadata!;
            await Assert.That(metadata.GetProperty("experiment_name").GetString())
                .IsEqualTo("slice__test-community__random-1-seed-20260405");
            await Assert.That(metadata.GetProperty("experiment_run_name").GetString()).IsEqualTo(targetRunName);
            await Assert.That(metadata.GetProperty("sourceRunName").GetString()).IsEqualTo(runName);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_exports_comparable_slice_runs_to_a_normalized_bundle()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "analysis.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/slices/all-matchdays/random-1-seed-20260405";
            var runOne = "slice__test-community__o3__prompt-v1__random-1-seed-20260405__startsat-12h__2026-04-05t12-00-00z";
            var runTwo = "slice__test-community__gpt-5-nano__prompt-v1__random-1-seed-20260405__startsat-12h__2026-04-05t12-00-00z";
            var datasetItemId = "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20260405";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            var langfuseClient = CreateMockLangfuseClientForComparableRuns(
                datasetName,
                runOne,
                runTwo,
                datasetItemId,
                sourceDatasetItemId,
                sliceKind: "random-sample",
                taskType: "slice");

            var context = CreateCommandApp<ExportExperimentAnalysisCommand>(
                "export-experiment-analysis",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "export-experiment-analysis",
                "--dataset-name",
                datasetName,
                "--run-names",
                $"{runOne},{runTwo}",
                "--output",
                outputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"primaryMetricName\": \"total_kicktipp_points\"");
            await Assert.That(File.Exists(outputPath)).IsTrue();

            var bundleJson = await File.ReadAllTextAsync(outputPath);
            await Assert.That(bundleJson).Contains("\"taskType\": \"slice\"");
            await Assert.That(bundleJson).Contains($"\"sourceDatasetItemId\": \"{sourceDatasetItemId}\"");
            await Assert.That(bundleJson).Contains("\"kicktippPoints\": 4");
            await Assert.That(bundleJson).Contains("\"kicktippPoints\": 3");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_exports_repeated_match_runs_with_average_primary_metric()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "analysis.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-2";
            var runOne = "repeated-match__test-community__o3__prompt-v1__repeat-2__exact-time__2026-03-15t12-00-00z";
            var runTwo = "repeated-match__test-community__gpt-5-nano__prompt-v1__repeat-2__exact-time__2026-03-15t12-00-00z";
            var datasetItemId = "bundesliga-2025-26__test-community__ts123__repeated-match__repeat-2__01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";

            var langfuseClient = CreateMockLangfuseClientForComparableRuns(
                datasetName,
                runOne,
                runTwo,
                datasetItemId,
                sourceDatasetItemId,
                sliceKind: "repeated-match",
                taskType: "repeated-match");

            var context = CreateCommandApp<ExportExperimentAnalysisCommand>(
                "export-experiment-analysis",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "export-experiment-analysis",
                "--dataset-name",
                datasetName,
                "--run-names",
                $"{runOne},{runTwo}",
                "--output",
                outputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"primaryMetricName\": \"avg_kicktipp_points\"");

            var bundleJson = await File.ReadAllTextAsync(outputPath);
            await Assert.That(bundleJson).Contains("\"taskType\": \"repeated-match\"");
            await Assert.That(bundleJson).Contains($"\"sourceDatasetItemId\": \"{sourceDatasetItemId}\"");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_exports_community_to_date_runs_with_missed_predictions()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var outputPath = Path.Combine(tempDirectory.FullName, "analysis.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/community-to-date/through-md01/community-to-date-md01";
            var runOne = "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__alice-p1";
            var runTwo = "community-to-date__test-community__community-to-date-md01__2026-04-07t12-00-00z__bob-p2";
            var datasetItemId = "bundesliga-2025-26__test-community__ts123__slice__community-to-date-md01";
            var sourceDatasetItemId = "bundesliga-2025-26__test-community__ts123";
            var datasetId = "dataset-1";
            var datasetRunIdOne = "dataset-run-1";
            var datasetRunIdTwo = "dataset-run-2";

            var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            client
                .Setup(mock => mock.GetDatasetRunAsync(datasetName, runOne, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    datasetRunIdOne,
                    runOne,
                    datasetId,
                    datasetName,
                    null,
                    ToJsonElement(new
                    {
                        runner = "community-match-experiment-runner",
                        task = "community-to-date",
                        communityContext = "test-community",
                        competition = "bundesliga-2025-26",
                        sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                        datasetName,
                        sliceKind = "community-to-date",
                        sliceKey = "community-to-date-md01",
                        sourcePoolKey = "through-md01",
                        selectedItemIdsHash = "hash-community-1",
                        selectedItemIdsCount = 1,
                        sampleSize = 1,
                        startedAtUtc = "2026-04-07T12:00:00Z",
                        sampleMethod = "community-to-date",
                        includeJustification = false,
                        sourceDatasetKind = "community-to-date",
                        datasetItemIdMap = new Dictionary<string, string>
                        {
                            [sourceDatasetItemId] = datasetItemId
                        },
                        model = "Alice",
                        observationName = "community-match-prediction",
                        runSubjectKind = "participant",
                        runSubjectId = "p1",
                        runSubjectDisplayName = "Alice",
                        batchStrategy = "simple-batched",
                        batchSize = 1
                    }),
                    []));
            client
                .Setup(mock => mock.GetDatasetRunAsync(datasetName, runTwo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseDatasetRunWithItems(
                    datasetRunIdTwo,
                    runTwo,
                    datasetId,
                    datasetName,
                    null,
                    ToJsonElement(new
                    {
                        runner = "community-match-experiment-runner",
                        task = "community-to-date",
                        communityContext = "test-community",
                        competition = "bundesliga-2025-26",
                        sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                        datasetName,
                        sliceKind = "community-to-date",
                        sliceKey = "community-to-date-md01",
                        sourcePoolKey = "through-md01",
                        selectedItemIdsHash = "hash-community-1",
                        selectedItemIdsCount = 1,
                        sampleSize = 1,
                        startedAtUtc = "2026-04-07T12:00:00Z",
                        sampleMethod = "community-to-date",
                        includeJustification = false,
                        sourceDatasetKind = "community-to-date",
                        datasetItemIdMap = new Dictionary<string, string>
                        {
                            [sourceDatasetItemId] = datasetItemId
                        },
                        model = "Bob",
                        observationName = "community-match-prediction",
                        runSubjectKind = "participant",
                        runSubjectId = "p2",
                        runSubjectDisplayName = "Bob",
                        batchStrategy = "simple-batched",
                        batchSize = 1
                    }),
                    []));
            client
                .Setup(mock => mock.ListDatasetRunItemsAsync(datasetId, runOne, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [CreateDatasetRunItem(runOne, datasetRunIdOne, datasetItemId, "trace-1")],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            client
                .Setup(mock => mock.ListDatasetRunItemsAsync(datasetId, runTwo, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                    [CreateDatasetRunItem(runTwo, datasetRunIdTwo, datasetItemId, "trace-2")],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            client
                .Setup(mock => mock.ListScoresAsync(
                    It.Is<LangfuseListScoresRequest>(request => request.DatasetRunId == datasetRunIdOne),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseScore>(
                    [
                        new LangfuseScore("score-total-1", "total_kicktipp_points", 4, null, null, datasetRunIdOne, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                        new LangfuseScore("score-avg-1", "avg_kicktipp_points", 4, null, null, datasetRunIdOne, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                    ],
                    new LangfusePaginationMeta(1, 100, 2, 1)));
            client
                .Setup(mock => mock.ListScoresAsync(
                    It.Is<LangfuseListScoresRequest>(request => request.DatasetRunId == datasetRunIdTwo),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseScore>(
                    [
                        new LangfuseScore("score-total-2", "total_kicktipp_points", 0, null, null, datasetRunIdTwo, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                        new LangfuseScore("score-avg-2", "avg_kicktipp_points", 0, null, null, datasetRunIdTwo, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                    ],
                    new LangfusePaginationMeta(1, 100, 2, 1)));
            client
                .Setup(mock => mock.ListDatasetItemsAsync(
                    It.Is<LangfuseListDatasetItemsRequest>(request => request.DatasetName == datasetName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetItem>(
                    [new LangfuseDatasetItem(
                        datasetItemId,
                        datasetId,
                        datasetName,
                        ParseJson("{\"fixture\":\"Team A vs Team B\",\"startsAt\":\"2025-08-22T20:30:00 Europe/Berlin (+02)\"}"),
                        ParseJson("{\"score\":\"2:1\"}"),
                        ParseJson("{\"competition\":\"bundesliga-2025-26\",\"season\":\"2025/2026\",\"communityContext\":\"test-community\",\"matchday\":1,\"matchdayLabel\":\"md01\",\"homeTeam\":\"Team A\",\"awayTeam\":\"Team B\",\"tippSpielId\":\"123\",\"startsAt\":\"2025-08-22T20:30:00 Europe/Berlin (+02)\"}"),
                        null)],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            client
                .Setup(mock => mock.ListTracesAsync(
                    It.Is<LangfuseListTracesRequest>(request => request.SessionId == runOne && request.Fields == "io"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseTraceWithDetails>(
                    [new LangfuseTraceWithDetails(
                        "trace-1",
                        null,
                        ParseJson($"{{\"sourceDatasetItemId\":\"{sourceDatasetItemId}\"}}"),
                        ParseJson("{\"source\":\"kicktipp-community\"}"),
                        null,
                        null,
                        ["experiment"])],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            client
                .Setup(mock => mock.ListTracesAsync(
                    It.Is<LangfuseListTracesRequest>(request => request.SessionId == runTwo && request.Fields == "io"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfusePaginatedResponse<LangfuseTraceWithDetails>(
                    [new LangfuseTraceWithDetails(
                        "trace-2",
                        null,
                        ParseJson($"{{\"sourceDatasetItemId\":\"{sourceDatasetItemId}\"}}"),
                        ParseJson("{\"source\":\"kicktipp-community\"}"),
                        null,
                        null,
                        ["experiment"])],
                    new LangfusePaginationMeta(1, 100, 1, 1)));
            client
                .Setup(mock => mock.ListObservationsAsync(
                    It.Is<LangfuseListObservationsRequest>(request => request.SessionId == runOne && request.Fields == "basic,io"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseCursorPaginatedResponse<LangfuseObservationDetail>(
                    [new LangfuseObservationDetail("observation-1", "trace-1", "GENERATION", "community-match-prediction", ParseJson("{\"status\":\"placed\",\"homeGoals\":2,\"awayGoals\":1,\"kicktippPoints\":4}"), default)],
                    new LangfuseCursorPaginationMeta(null)));
            client
                .Setup(mock => mock.ListObservationsAsync(
                    It.Is<LangfuseListObservationsRequest>(request => request.SessionId == runTwo && request.Fields == "basic,io"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LangfuseCursorPaginatedResponse<LangfuseObservationDetail>(
                    [new LangfuseObservationDetail("observation-2", "trace-2", "GENERATION", "community-match-prediction", ParseJson("{\"status\":\"missed\",\"homeGoals\":null,\"awayGoals\":null,\"kicktippPoints\":0}"), default)],
                    new LangfuseCursorPaginationMeta(null)));
            var context = CreateCommandApp<ExportExperimentAnalysisCommand>(
                "export-experiment-analysis",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(client.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "export-experiment-analysis",
                "--dataset-name",
                datasetName,
                "--run-names",
                $"{runOne},{runTwo}",
                "--output",
                outputPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"primaryMetricName\": \"total_kicktipp_points\"");

            var bundleJson = await File.ReadAllTextAsync(outputPath);
            using var bundleDocument = JsonDocument.Parse(bundleJson);
            var bobRow = bundleDocument.RootElement.GetProperty("rows").EnumerateArray()
                .Single(row => row.GetProperty("runSubjectId").GetString() == "p2");
            await Assert.That(bundleJson).Contains("\"taskType\": \"community-to-date\"");
            await Assert.That(bundleJson).Contains("\"predictionStatus\": \"missed\"");
            await Assert.That(bundleJson).Contains("\"runSubjectDisplayName\": \"Alice\"");
            await Assert.That(bobRow.TryGetProperty("predictedHomeGoals", out _)).IsFalse();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_rejects_runs_with_mismatched_prepared_dataset_items()
    {
        var datasetName = "match-predictions/bundesliga-2025-26/test-community/slices/all-matchdays/random-1-seed-20260405";
        var runOne = "slice__test-community__o3__prompt-v1__random-1-seed-20260405__startsat-12h__2026-04-05t12-00-00z";
        var runTwo = "slice__test-community__gpt-5-nano__prompt-v1__random-1-seed-20260405__startsat-12h__2026-04-05t12-00-00z";

        var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        langfuseClient
            .Setup(client => client.GetDatasetRunAsync(datasetName, runOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDatasetRun(datasetName, runOne, "dataset-1", taskType: "slice", model: "o3", sliceKey: "random-1-seed-20260405", sliceKind: "random-sample"));
        langfuseClient
            .Setup(client => client.GetDatasetRunAsync(datasetName, runTwo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDatasetRun(datasetName, runTwo, "dataset-1", taskType: "slice", model: "gpt-5-nano", sliceKey: "random-1-seed-20260405", sliceKind: "random-sample"));
        langfuseClient
            .Setup(client => client.ListDatasetRunItemsAsync("dataset-1", runOne, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                [CreateDatasetRunItem(runOne, "dataset-run-1", "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20260405", "trace-1")],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        langfuseClient
            .Setup(client => client.ListDatasetRunItemsAsync("dataset-1", runTwo, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                [CreateDatasetRunItem(runTwo, "dataset-run-2", "bundesliga-2025-26__test-community__ts999__slice__random-1-seed-20260405", "trace-2")],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        langfuseClient
            .Setup(client => client.ListScoresAsync(It.IsAny<LangfuseListScoresRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseScore>(
                [
                    new LangfuseScore("score-total", "total_kicktipp_points", 4, null, null, "dataset-run-1", "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                    new LangfuseScore("score-avg", "avg_kicktipp_points", 4, null, null, "dataset-run-1", "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                ],
                new LangfusePaginationMeta(1, 100, 2, 1)));

        var context = CreateCommandApp<ExportExperimentAnalysisCommand>(
            "export-experiment-analysis",
            configureServices: new Action<IServiceCollection>(services =>
            {
                services.AddSingleton(langfuseClient.Object);
            }));

        var (exitCode, output) = await RunCommandAsync(
            context.App,
            context.Console,
            "export-experiment-analysis",
            "--dataset-name",
            datasetName,
            "--run-names",
            $"{runOne},{runTwo}");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("does not contain the same prepared dataset item set");
    }

    private static Mock<ILangfusePublicApiClient> CreateMockLangfuseClientForComparableRuns(
        string datasetName,
        string runOne,
        string runTwo,
        string datasetItemId,
        string sourceDatasetItemId,
        string sliceKind,
        string taskType)
    {
        var datasetId = "dataset-1";
        var datasetRunIdOne = "dataset-run-1";
        var datasetRunIdTwo = "dataset-run-2";

        var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        client
            .Setup(mock => mock.GetDatasetRunAsync(datasetName, runOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDatasetRun(datasetName, runOne, datasetId, taskType, "o3", sliceKind == "repeated-match" ? "repeat-2" : "random-1-seed-20260405", sliceKind));
        client
            .Setup(mock => mock.GetDatasetRunAsync(datasetName, runTwo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDatasetRun(datasetName, runTwo, datasetId, taskType, "gpt-5-nano", sliceKind == "repeated-match" ? "repeat-2" : "random-1-seed-20260405", sliceKind));
        client
            .Setup(mock => mock.ListDatasetRunItemsAsync(datasetId, runOne, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                [CreateDatasetRunItem(runOne, datasetRunIdOne, datasetItemId, "trace-1")],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        client
            .Setup(mock => mock.ListDatasetRunItemsAsync(datasetId, runTwo, 1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetRunItem>(
                [CreateDatasetRunItem(runTwo, datasetRunIdTwo, datasetItemId, "trace-2")],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        client
            .Setup(mock => mock.ListScoresAsync(
                It.Is<LangfuseListScoresRequest>(request => request.DatasetRunId == datasetRunIdOne),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseScore>(
                [
                    new LangfuseScore("score-total-1", "total_kicktipp_points", 4, null, null, datasetRunIdOne, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                    new LangfuseScore("score-avg-1", "avg_kicktipp_points", 4, null, null, datasetRunIdOne, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                ],
                new LangfusePaginationMeta(1, 100, 2, 1)));
        client
            .Setup(mock => mock.ListScoresAsync(
                It.Is<LangfuseListScoresRequest>(request => request.DatasetRunId == datasetRunIdTwo),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseScore>(
                [
                    new LangfuseScore("score-total-2", "total_kicktipp_points", 3, null, null, datasetRunIdTwo, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                    new LangfuseScore("score-avg-2", "avg_kicktipp_points", 3, null, null, datasetRunIdTwo, "NUMERIC", "API", default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                ],
                new LangfusePaginationMeta(1, 100, 2, 1)));
        client
            .Setup(mock => mock.ListDatasetItemsAsync(
                It.Is<LangfuseListDatasetItemsRequest>(request => request.DatasetName == datasetName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseDatasetItem>(
                [new LangfuseDatasetItem(
                    datasetItemId,
                    datasetId,
                    datasetName,
                    ParseJson("{\"fixture\":\"VfB Stuttgart vs RB Leipzig\",\"startsAt\":\"2026-03-15T15:30:00 Europe/Berlin (+01)\"}"),
                    ParseJson("{\"score\":\"2:1\"}"),
                    ParseJson("{\"competition\":\"bundesliga-2025-26\",\"season\":\"2025/2026\",\"communityContext\":\"test-community\",\"matchday\":26,\"matchdayLabel\":\"md26\",\"homeTeam\":\"VfB Stuttgart\",\"awayTeam\":\"RB Leipzig\",\"tippSpielId\":\"123\",\"startsAt\":\"2026-03-15T15:30:00 Europe/Berlin (+01)\"}"),
                    null)],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        client
            .Setup(mock => mock.ListTracesAsync(
                It.Is<LangfuseListTracesRequest>(request => request.SessionId == runOne && request.Fields == "io"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseTraceWithDetails>(
                [new LangfuseTraceWithDetails(
                    "trace-1",
                    null,
                    ParseJson($"{{\"sourceDatasetItemId\":\"{sourceDatasetItemId}\"}}"),
                    ParseJson("{\"homeGoals\":2,\"awayGoals\":1}"),
                    null,
                    null,
                    ["experiment"])],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        client
            .Setup(mock => mock.ListTracesAsync(
                It.Is<LangfuseListTracesRequest>(request => request.SessionId == runTwo && request.Fields == "io"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfusePaginatedResponse<LangfuseTraceWithDetails>(
                [new LangfuseTraceWithDetails(
                    "trace-2",
                    null,
                    ParseJson($"{{\"sourceDatasetItemId\":\"{sourceDatasetItemId}\"}}"),
                    ParseJson("{\"homeGoals\":1,\"awayGoals\":0}"),
                    null,
                    null,
                    ["experiment"])],
                new LangfusePaginationMeta(1, 100, 1, 1)));
        client
            .Setup(mock => mock.ListObservationsAsync(
                It.Is<LangfuseListObservationsRequest>(request => request.SessionId == runOne && request.Fields == "basic,io"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfuseCursorPaginatedResponse<LangfuseObservationDetail>(
                [new LangfuseObservationDetail("observation-1", "trace-1", "GENERATION", "predict-match", ParseJson("{\"homeGoals\":2,\"awayGoals\":1}"), default)],
                new LangfuseCursorPaginationMeta(null)));
        client
            .Setup(mock => mock.ListObservationsAsync(
                It.Is<LangfuseListObservationsRequest>(request => request.SessionId == runTwo && request.Fields == "basic,io"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LangfuseCursorPaginatedResponse<LangfuseObservationDetail>(
                [new LangfuseObservationDetail("observation-2", "trace-2", "GENERATION", "predict-match", ParseJson("{\"homeGoals\":1,\"awayGoals\":0}"), default)],
                new LangfuseCursorPaginationMeta(null)));
        return client;
    }

    private static LangfuseDatasetRunWithItems CreateDatasetRun(
        string datasetName,
        string runName,
        string datasetId,
        string taskType,
        string model,
        string sliceKey,
        string sliceKind)
    {
        var sampleMethod = string.Equals(sliceKind, "repeated-match", StringComparison.Ordinal)
            ? "repeated-match"
            : "random-sample";
        var datasetItemIdMap = string.Equals(taskType, "slice", StringComparison.Ordinal)
            ? new Dictionary<string, string>
            {
                ["bundesliga-2025-26__test-community__ts123"] = "bundesliga-2025-26__test-community__ts123__slice__random-1-seed-20260405"
            }
            : new Dictionary<string, string>();
        var batchStrategy = string.Equals(taskType, "repeated-match", StringComparison.Ordinal)
            ? "warmup-plus-batches"
            : "simple-batched";

        return new LangfuseDatasetRunWithItems(
            runName.Contains("gpt-5-nano", StringComparison.Ordinal) ? "dataset-run-2" : "dataset-run-1",
            runName,
            datasetId,
            datasetName,
            null,
            ToJsonElement(new
            {
                runner = "match-experiment-runner",
                task = taskType,
                communityContext = "test-community",
                competition = "bundesliga-2025-26",
                sourceDatasetName = "match-predictions/bundesliga-2025-26/test-community",
                datasetName,
                promptKey = "prompt-v1",
                sliceKind,
                sliceKey,
                sourcePoolKey = "all-matchdays",
                selectedItemIdsHash = "hash-123",
                selectedItemIdsCount = 1,
                sampleSize = 1,
                evaluationTimestampPolicyKey = "startsat-12h",
                startedAtUtc = "2026-04-05T12:00:00Z",
                sampleSeed = 20260405,
                sampleMethod,
                includeJustification = false,
                promptVersion = "prompt-v1",
                sourceDatasetKind = taskType,
                datasetItemIdMap,
                model,
                batchStrategy,
                batchSize = 1
            }),
            []);
    }

    private static LangfuseDatasetRunItem CreateDatasetRunItem(
        string runName,
        string datasetRunId,
        string datasetItemId,
        string traceId)
    {
        return new LangfuseDatasetRunItem(
            $"{datasetRunId}-item-1",
            datasetRunId,
            runName,
            datasetItemId,
            traceId,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static JsonElement ParseJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static JsonElement ToJsonElement<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
