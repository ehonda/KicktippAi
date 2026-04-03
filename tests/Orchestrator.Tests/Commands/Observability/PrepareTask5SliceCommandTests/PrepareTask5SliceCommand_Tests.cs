using System.Text.Json;
using Orchestrator.Commands.Observability.PrepareTask5Slice;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.PrepareTask5SliceCommandTests;

public class PrepareTask5SliceCommand_Tests
{
    [Test]
    public async Task Running_command_writes_deterministic_slice_artifacts_for_a_fixed_seed()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var canonicalArtifactPath = Path.Combine(tempDirectory.FullName, "canonical.json");
            await File.WriteAllTextAsync(
                canonicalArtifactPath,
                JsonSerializer.Serialize(new
                {
                    datasetName = "match-predictions/bundesliga-2025-26/test-community",
                    items = new[]
                    {
                        new
                        {
                            id = "bundesliga-2025-26__test-community__ts001",
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
                                tippSpielId = "001"
                            }
                        },
                        new
                        {
                            id = "bundesliga-2025-26__test-community__ts002",
                            input = new
                            {
                                homeTeam = "Borussia Dortmund",
                                awayTeam = "VfB Stuttgart",
                                startsAt = "2025-11-02T15:30:00 Europe/Berlin (+01)"
                            },
                            expectedOutput = new
                            {
                                homeGoals = 1,
                                awayGoals = 1
                            },
                            metadata = new
                            {
                                competition = "bundesliga-2025-26",
                                season = "2025/2026",
                                communityContext = "test-community",
                                matchday = 8,
                                matchdayLabel = "md08",
                                homeTeam = "Borussia Dortmund",
                                awayTeam = "VfB Stuttgart",
                                tippSpielId = "002"
                            }
                        },
                        new
                        {
                            id = "bundesliga-2025-26__test-community__ts003",
                            input = new
                            {
                                homeTeam = "Eintracht Frankfurt",
                                awayTeam = "SC Freiburg",
                                startsAt = "2025-11-09T17:30:00 Europe/Berlin (+01)"
                            },
                            expectedOutput = new
                            {
                                homeGoals = 3,
                                awayGoals = 2
                            },
                            metadata = new
                            {
                                competition = "bundesliga-2025-26",
                                season = "2025/2026",
                                communityContext = "test-community",
                                matchday = 9,
                                matchdayLabel = "md09",
                                homeTeam = "Eintracht Frankfurt",
                                awayTeam = "SC Freiburg",
                                tippSpielId = "003"
                            }
                        }
                    }
                }));

            var outputDirectoryOne = Path.Combine(tempDirectory.FullName, "one");
            var outputDirectoryTwo = Path.Combine(tempDirectory.FullName, "two");

            var firstContext = CreateCommandApp<PrepareTask5SliceCommand>("prepare-task5-slice");
            var secondContext = CreateCommandApp<PrepareTask5SliceCommand>("prepare-task5-slice");

            var (firstExitCode, firstOutput) = await RunCommandAsync(
                firstContext.App,
                firstContext.Console,
                "prepare-task5-slice",
                "--input",
                canonicalArtifactPath,
                "--sample-size",
                "2",
                "--sample-seed",
                "42",
                "--output-directory",
                outputDirectoryOne);

            var (secondExitCode, secondOutput) = await RunCommandAsync(
                secondContext.App,
                secondContext.Console,
                "prepare-task5-slice",
                "--input",
                canonicalArtifactPath,
                "--sample-size",
                "2",
                "--sample-seed",
                "42",
                "--output-directory",
                outputDirectoryTwo);

            await Assert.That(firstExitCode).IsEqualTo(0);
            await Assert.That(secondExitCode).IsEqualTo(0);
            await Assert.That(firstOutput).Contains("\"sampleSeed\": 42");
            await Assert.That(secondOutput).Contains("\"sampleSeed\": 42");

            var firstManifest = await File.ReadAllTextAsync(Path.Combine(outputDirectoryOne, "slice-manifest.json"));
            var secondManifest = await File.ReadAllTextAsync(Path.Combine(outputDirectoryTwo, "slice-manifest.json"));
            var firstDataset = await File.ReadAllTextAsync(Path.Combine(outputDirectoryOne, "slice-dataset.json"));
            var secondDataset = await File.ReadAllTextAsync(Path.Combine(outputDirectoryTwo, "slice-dataset.json"));

            await Assert.That(firstManifest).IsEqualTo(secondManifest);
            await Assert.That(firstDataset).IsEqualTo(secondDataset);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
