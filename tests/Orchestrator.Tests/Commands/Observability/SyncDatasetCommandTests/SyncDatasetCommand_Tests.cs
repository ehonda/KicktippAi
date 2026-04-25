using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Observability.SyncDataset;
using Orchestrator.Infrastructure.Langfuse;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.SyncDatasetCommandTests;

public class SyncDatasetCommand_Tests
{
    [Test]
    public async Task Running_command_in_dry_run_validates_artifact_and_writes_summary()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var artifactPath = Path.Combine(tempDirectory.FullName, "dataset.json");
            await File.WriteAllTextAsync(
                artifactPath,
                JsonSerializer.Serialize(new
                {
                    datasetName = "match-predictions/bundesliga-2025-26/test-community",
                    items = new[]
                    {
                        new
                        {
                            id = "bundesliga-2025-26__test-community__ts123",
                            input = new
                            {
                                homeTeam = "FC Bayern München",
                                awayTeam = "RB Leipzig",
                                startsAt = "2025-10-30T15:30:00 Europe/Berlin (+01)"
                            },
                            expectedOutput = new
                            {
                                homeGoals = 2,
                                awayGoals = 1
                            },
                            metadata = new
                            {
                                competition = "bundesliga-2025-26",
                                season = "2025/2026",
                                communityContext = "test-community",
                                matchday = 7,
                                matchdayLabel = "md07",
                                homeTeam = "FC Bayern München",
                                awayTeam = "RB Leipzig",
                                tippSpielId = "123"
                            }
                        }
                    }
                }));

            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            var context = CreateCommandApp<SyncDatasetCommand>(
                "sync-dataset",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "sync-dataset",
                "--input",
                artifactPath,
                "--dry-run");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"dryRun\": true");
            await Assert.That(output).Contains("\"created\": 1");
            await Assert.That(output).Contains("match-predictions/bundesliga-2025-26/test-community");
            langfuseClient.VerifyNoOtherCalls();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_syncs_dataset_description_and_metadata()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var artifactPath = Path.Combine(tempDirectory.FullName, "dataset.json");
            var datasetName = "match-predictions/bundesliga-2025-26/test-community/repeated-match/md26-vfb-stuttgart-vs-rb-leipzig/repeat-25";
            await File.WriteAllTextAsync(
                artifactPath,
                JsonSerializer.Serialize(new
                {
                    datasetName,
                    datasetDescription = "Stuttgart's 1-0 Matchday 26 win over Leipzig was a close top-four clash where Stuttgart leapfrogged Leipzig.",
                    datasetMetadata = new
                    {
                        fixture = "VfB Stuttgart vs RB Leipzig",
                        actualResult = "1:0",
                        actualResultDisplay = "VfB Stuttgart 1 - 0 RB Leipzig",
                        matchday = 26,
                        repetitionCount = 25,
                        interestingBecause = "Close top-four clash where Stuttgart leapfrogged Leipzig."
                    },
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fixture = new { type = "string", minLength = 1 },
                            startsAt = new { type = "string", minLength = 1 }
                        },
                        required = new[] { "fixture", "startsAt" },
                        additionalProperties = false
                    },
                    expectedOutputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            score = new { type = "string", minLength = 3 }
                        },
                        required = new[] { "score" },
                        additionalProperties = false
                    },
                    items = new[]
                    {
                        new
                        {
                            id = "bundesliga-2025-26__test-community__ts123__repeated-match__repeat-25__01",
                            input = new
                            {
                                fixture = "VfB Stuttgart vs RB Leipzig",
                                startsAt = "2026-03-15T15:30:00 Europe/Berlin (+01)"
                            },
                            expectedOutput = new
                            {
                                score = "1:0"
                            },
                            metadata = new
                            {
                                competition = "bundesliga-2025-26",
                                season = "2025/2026",
                                communityContext = "test-community",
                                matchday = 26,
                                matchdayLabel = "md26",
                                homeTeam = "VfB Stuttgart",
                                awayTeam = "RB Leipzig",
                                tippSpielId = "123"
                            }
                        }
                    }
                }));

            LangfuseCreateDatasetRequest? capturedDatasetRequest = null;
            var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
            langfuseClient
                .Setup(client => client.CreateDatasetAsync(It.IsAny<LangfuseCreateDatasetRequest>(), It.IsAny<CancellationToken>()))
                .Callback((LangfuseCreateDatasetRequest request, CancellationToken _) => capturedDatasetRequest = request)
                .ReturnsAsync((LangfuseCreateDatasetRequest request, CancellationToken _) => new LangfuseDataset(
                    "dataset-1",
                    request.Name,
                    request.Description,
                    (JsonElement)request.Metadata!,
                    request.InputSchema ?? default,
                    request.ExpectedOutputSchema ?? default));
            langfuseClient
                .Setup(client => client.GetDatasetItemAsync(
                    "bundesliga-2025-26__test-community__ts123__repeated-match__repeat-25__01",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseDatasetItem?)null);
            langfuseClient
                .Setup(client => client.CreateDatasetItemAsync(It.IsAny<LangfuseCreateDatasetItemRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LangfuseCreateDatasetItemRequest request, CancellationToken _) => new LangfuseDatasetItem(
                    request.Id,
                    "dataset-1",
                    request.DatasetName,
                    (JsonElement)request.Input!,
                    (JsonElement)request.ExpectedOutput!,
                    (JsonElement)request.Metadata!,
                    null));

            var context = CreateCommandApp<SyncDatasetCommand>(
                "sync-dataset",
                configureServices: new Action<IServiceCollection>(services =>
                {
                    services.AddSingleton(langfuseClient.Object);
                }));

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "sync-dataset",
                "--input",
                artifactPath);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"created\": 1");
            await Assert.That(capturedDatasetRequest).IsNotNull();
            await Assert.That(capturedDatasetRequest!.Description)
                .IsEqualTo("Stuttgart's 1-0 Matchday 26 win over Leipzig was a close top-four clash where Stuttgart leapfrogged Leipzig.");
            var metadata = (JsonElement)capturedDatasetRequest.Metadata!;
            await Assert.That(metadata.GetProperty("fixture").GetString()).IsEqualTo("VfB Stuttgart vs RB Leipzig");
            await Assert.That(metadata.GetProperty("actualResult").GetString()).IsEqualTo("1:0");
            await Assert.That(metadata.GetProperty("repetitionCount").GetInt32()).IsEqualTo(25);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
