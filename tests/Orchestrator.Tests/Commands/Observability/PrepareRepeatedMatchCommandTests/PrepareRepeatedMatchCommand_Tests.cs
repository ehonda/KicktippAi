using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.PrepareRepeatedMatch;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.PrepareRepeatedMatchCommandTests;

public class PrepareRepeatedMatchCommand_Tests
{
    [Test]
    public async Task Running_command_materializes_repeated_match_dataset_and_manifest()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
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
                        new DateTimeOffset(2026, 3, 15, 16, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 3, 15, 16, 0, 0, TimeSpan.Zero))
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory
                .Setup(factory => factory.CreateMatchOutcomeRepository())
                .Returns(matchOutcomeRepository.Object);

            var outputDirectory = Path.Combine(tempDirectory.FullName, "repeated-match");
            var context = CreateCommandApp<PrepareRepeatedMatchCommand>(
                "prepare-repeated-match",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "prepare-repeated-match",
                "--community-context",
                "test-community",
                "--home",
                "VfB Stuttgart",
                "--away",
                "RB Leipzig",
                "--matchday",
                "26",
                "--sample-size",
                "3",
                "--output-directory",
                outputDirectory);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"mode\": \"repeated-match\"");
            await Assert.That(File.Exists(Path.Combine(outputDirectory, "canonical-source.json"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDirectory, "slice-dataset.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDirectory, "slice-manifest.json"))).IsTrue();

            using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-manifest.json")));
            var manifestRoot = manifestDocument.RootElement;
            var manifestItems = manifestRoot.GetProperty("items").EnumerateArray().ToList();
            var sourceIds = manifestItems
                .Select(item => item.GetProperty("sourceDatasetItemId").GetString())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var sliceIds = manifestItems
                .Select(item => item.GetProperty("sliceDatasetItemId").GetString())
                .ToList();

            await Assert.That(manifestRoot.GetProperty("sliceKind").GetString()).IsEqualTo("repeated-match");
            await Assert.That(manifestRoot.GetProperty("sampleMethod").GetString()).IsEqualTo("repeated-match");
            await Assert.That(manifestItems.Count).IsEqualTo(3);
            await Assert.That(sourceIds.Count).IsEqualTo(1);
            await Assert.That(sliceIds.Distinct(StringComparer.Ordinal).Count()).IsEqualTo(3);
            await Assert.That(manifestRoot.GetProperty("selectedItemIds").GetArrayLength()).IsEqualTo(1);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
