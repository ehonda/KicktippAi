using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.PrepareSlice;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.PrepareSliceCommandTests;

public class PrepareSliceCommand_Tests
{
    [Test]
    public async Task Running_command_writes_deterministic_slice_artifacts_for_a_fixed_seed()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
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
                        "001",
                        new DateTimeOffset(2025, 10, 30, 16, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2025, 10, 30, 16, 0, 0, TimeSpan.Zero))
                });
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    8,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "Borussia Dortmund",
                        "VfB Stuttgart",
                        NodaTime.Instant.FromUtc(2025, 11, 2, 14, 30).InUtc(),
                        8,
                        1,
                        1,
                        MatchOutcomeAvailability.Completed,
                        "002",
                        new DateTimeOffset(2025, 11, 2, 16, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2025, 11, 2, 16, 0, 0, TimeSpan.Zero))
                });
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    9,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new PersistedMatchOutcome(
                        "test-community",
                        "bundesliga-2025-26",
                        "Eintracht Frankfurt",
                        "SC Freiburg",
                        NodaTime.Instant.FromUtc(2025, 11, 9, 16, 30).InUtc(),
                        9,
                        3,
                        2,
                        MatchOutcomeAvailability.Completed,
                        "003",
                        new DateTimeOffset(2025, 11, 9, 18, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2025, 11, 9, 18, 0, 0, TimeSpan.Zero))
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory
                .Setup(factory => factory.CreateMatchOutcomeRepository())
                .Returns(matchOutcomeRepository.Object);

            var outputDirectoryOne = Path.Combine(tempDirectory.FullName, "one");
            var outputDirectoryTwo = Path.Combine(tempDirectory.FullName, "two");

            var firstContext = CreateCommandApp<PrepareSliceCommand>("prepare-slice", firebaseServiceFactory: firebaseFactory);
            var secondContext = CreateCommandApp<PrepareSliceCommand>("prepare-slice", firebaseServiceFactory: firebaseFactory);

            var (firstExitCode, firstOutput) = await RunCommandAsync(
                firstContext.App,
                firstContext.Console,
                "prepare-slice",
                "--community-context",
                "test-community",
                "--matchdays",
                "7,8,9",
                "--sample-size",
                "2",
                "--sample-seed",
                "42",
                "--output-directory",
                outputDirectoryOne);

            var (secondExitCode, secondOutput) = await RunCommandAsync(
                secondContext.App,
                secondContext.Console,
                "prepare-slice",
                "--community-context",
                "test-community",
                "--matchdays",
                "7,8,9",
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
            await Assert.That(File.Exists(Path.Combine(outputDirectoryOne, "canonical-source.json"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDirectoryTwo, "canonical-source.json"))).IsFalse();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
