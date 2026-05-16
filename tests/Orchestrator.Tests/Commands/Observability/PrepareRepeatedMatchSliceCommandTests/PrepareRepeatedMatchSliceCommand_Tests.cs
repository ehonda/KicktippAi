using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability.PrepareRepeatedMatchSlice;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Observability.PrepareRepeatedMatchSliceCommandTests;

public class PrepareRepeatedMatchSliceCommand_Tests
{
    [Test]
    public async Task Running_command_writes_repeated_match_slice_artifacts_with_fixture_and_repetition_metadata()
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
                    CreateCompletedOutcome("FC Bayern München", "RB Leipzig", "001", 2, 1),
                    CreateCompletedOutcome("Borussia Dortmund", "VfB Stuttgart", "002", 1, 1),
                    CreateCompletedOutcome("Eintracht Frankfurt", "SC Freiburg", "003", 3, 2)
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory
                .Setup(factory => factory.CreateMatchOutcomeRepository())
                .Returns(matchOutcomeRepository.Object);

            var outputDirectory = Path.Combine(tempDirectory.FullName, "repeated-slice");
            var context = CreateCommandApp<PrepareRepeatedMatchSliceCommand>(
                "prepare-repeated-match-slice",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "prepare-repeated-match-slice",
                "--community-context",
                "test-community",
                "--matchdays",
                "7",
                "--match-count",
                "2",
                "--repetitions",
                "3",
                "--sample-seed",
                "42",
                "--output-directory",
                outputDirectory);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"mode\": \"repeated-match-slice\"");
            await Assert.That(output).Contains("\"matchCount\": 2");
            await Assert.That(output).Contains("\"repetitions\": 3");

            using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-manifest.json")));
            var manifestRoot = manifestDocument.RootElement;
            var manifestItems = manifestRoot.GetProperty("items").EnumerateArray().ToList();

            await Assert.That(manifestRoot.GetProperty("sliceKind").GetString()).IsEqualTo("repeated-match-slice");
            await Assert.That(manifestRoot.GetProperty("task").GetString()).IsEqualTo("repeated-match-slice");
            await Assert.That(manifestRoot.GetProperty("sampleMethod").GetString()).IsEqualTo("repeated-match-slice");
            await Assert.That(manifestRoot.GetProperty("matchCount").GetInt32()).IsEqualTo(2);
            await Assert.That(manifestRoot.GetProperty("repetitions").GetInt32()).IsEqualTo(3);
            await Assert.That(manifestRoot.GetProperty("sampleSize").GetInt32()).IsEqualTo(6);
            await Assert.That(manifestRoot.GetProperty("selectedItemIds").GetArrayLength()).IsEqualTo(2);
            await Assert.That(manifestItems.Count).IsEqualTo(6);
            await Assert.That(manifestItems.Select(item => item.GetProperty("sourceDatasetItemId").GetString()).Distinct().Count()).IsEqualTo(2);

            foreach (var fixtureGroup in manifestItems.GroupBy(item => item.GetProperty("sourceDatasetItemId").GetString()))
            {
                await Assert.That(fixtureGroup.Count()).IsEqualTo(3);
                await Assert.That(fixtureGroup.Select(item => item.GetProperty("repetitionIndex").GetInt32()).OrderBy(value => value).ToArray())
                    .IsEquivalentTo([1, 2, 3]);
                await Assert.That(fixtureGroup.All(item => item.GetProperty("sliceDatasetItemId").GetString()!.Contains("__repeated-match-slice__"))).IsTrue();
            }

            await Assert.That(manifestItems.Select(item => item.GetProperty("fixtureIndex").GetInt32()).Distinct().OrderBy(value => value).ToArray())
                .IsEquivalentTo([1, 2]);

            using var datasetDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-dataset.json")));
            var datasetRoot = datasetDocument.RootElement;
            var datasetMetadata = datasetRoot.GetProperty("datasetMetadata");
            await Assert.That(datasetRoot.GetProperty("datasetName").GetString())
                .Contains("/repeated-match-slices/matchdays-7/random-2x3-seed-42");
            await Assert.That(datasetMetadata.GetProperty("scope").GetString()).IsEqualTo("repeated-match-slice");
            await Assert.That(datasetMetadata.GetProperty("matchCount").GetInt32()).IsEqualTo(2);
            await Assert.That(datasetMetadata.GetProperty("repetitions").GetInt32()).IsEqualTo(3);
            await Assert.That(datasetMetadata.GetProperty("predictionCount").GetInt32()).IsEqualTo(6);
            await Assert.That(datasetRoot.GetProperty("items").GetArrayLength()).IsEqualTo(6);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Prepare_repeated_match_slice_settings_reject_invalid_parallel_dimensions()
    {
        var matchCountSettings = new PrepareRepeatedMatchSliceSettings
        {
            CommunityContext = "test-community",
            MatchCount = 0,
            Repetitions = 3
        };
        var repetitionsSettings = new PrepareRepeatedMatchSliceSettings
        {
            CommunityContext = "test-community",
            MatchCount = 2,
            Repetitions = 0
        };

        await Assert.That(matchCountSettings.Validate().Successful).IsFalse();
        await Assert.That(matchCountSettings.Validate().Message).Contains("--match-count must be at least 1");
        await Assert.That(repetitionsSettings.Validate().Successful).IsFalse();
        await Assert.That(repetitionsSettings.Validate().Message).Contains("--repetitions must be at least 1");
    }

    private static PersistedMatchOutcome CreateCompletedOutcome(
        string homeTeam,
        string awayTeam,
        string tippSpielId,
        int homeGoals,
        int awayGoals)
    {
        var startsAt = NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc();
        var createdAt = startsAt.ToInstant().ToDateTimeOffset();
        return new PersistedMatchOutcome(
            "test-community",
            "bundesliga-2025-26",
            homeTeam,
            awayTeam,
            startsAt,
            7,
            homeGoals,
            awayGoals,
            MatchOutcomeAvailability.Completed,
            tippSpielId,
            createdAt,
            createdAt);
    }
}
