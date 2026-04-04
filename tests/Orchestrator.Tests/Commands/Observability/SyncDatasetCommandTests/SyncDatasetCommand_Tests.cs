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
}