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
    public async Task Historical_compatibility_prepares_exact_five_by_four_hash_bound_cost_proxy()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var startsAt = NodaTime.Instant.FromUtc(2026, 3, 1, 18, 30).InUtc();
            var outcomes = new[]
            {
                CreateCompletedOutcome("FC Bayern München", "RB Leipzig", "101", 2, 1, CompetitionIds.Bundesliga2025_26, startsAt, "pes-squad"),
                CreateCompletedOutcome("Borussia Dortmund", "VfB Stuttgart", "102", 1, 1, CompetitionIds.Bundesliga2025_26, startsAt, "pes-squad"),
                CreateCompletedOutcome("Eintracht Frankfurt", "SC Freiburg", "103", 3, 2, CompetitionIds.Bundesliga2025_26, startsAt, "pes-squad"),
                CreateCompletedOutcome("FC St. Pauli", "1. FC Köln", "104", 0, 1, CompetitionIds.Bundesliga2025_26, startsAt, "pes-squad"),
                CreateCompletedOutcome("Hamburger SV", "1899 Hoffenheim", "105", 2, 2, CompetitionIds.Bundesliga2025_26, startsAt, "pes-squad")
            };
            var outcomesRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            outcomesRepository.Setup(repository => repository.GetMatchdayOutcomesAsync(
                    7, "pes-squad", It.IsAny<CancellationToken>()))
                .ReturnsAsync(outcomes);
            var historicalReader = new Mock<IHistoricalExperimentContextReader>(MockBehavior.Strict);
            historicalReader.Setup(repository => repository.GetContextDocumentAtOrBeforeAsync(
                    It.IsAny<string>(), "pes-squad", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, string _, DateTimeOffset evaluation, CancellationToken _) =>
                    new ContextDocument(name, $"content:{name}", 3, evaluation.AddMinutes(-1)));
            var firebaseFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            firebaseFactory.Setup(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2025_26))
                .Returns(outcomesRepository.Object);
            firebaseFactory.Setup(factory => factory.CreateBundesliga2025_26HistoricalExperimentContextReader())
                .Returns(historicalReader.Object);
            var outputDirectory = Path.Combine(tempDirectory.FullName, "historical-5x4");
            var context = CreateCommandApp<PrepareRepeatedMatchSliceCommand>(
                "prepare-repeated-match-slice",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                context.App,
                context.Console,
                "prepare-repeated-match-slice",
                "--competition", CompetitionIds.Bundesliga2025_26,
                "--historical-context-compatibility", ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1,
                "--official-knowledge-cutoff", "2026-02-16",
                "--starts-after", "2026-02-18T00:00:00 Europe/Berlin (+01)",
                "--community-context", "pes-squad",
                "--matchdays", "7",
                "--match-count", "5",
                "--repetitions", "4",
                "--sample-seed", "20260821",
                "--output-directory", outputDirectory);

            await Assert.That(output).DoesNotContain("Error:");
            await Assert.That(exitCode).IsEqualTo(0);
            using var manifestDocument = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-manifest.json")));
            var manifest = manifestDocument.RootElement;
            var compatibility = manifest.GetProperty("historicalCompatibility");
            var items = manifest.GetProperty("items").EnumerateArray().ToArray();
            await Assert.That(manifest.GetProperty("sampleSize").GetInt32()).IsEqualTo(20);
            await Assert.That(manifest.GetProperty("startsAfter").GetString())
                .IsEqualTo("2026-02-18T00:00:00 Europe/Berlin (+01)");
            await Assert.That(compatibility.GetProperty("officialKnowledgeCutoff").GetString()).IsEqualTo("2026-02-16");
            await Assert.That(compatibility.GetProperty("promptName").GetString())
                .IsEqualTo("kicktippai/bundesliga-2026-27/predict-one-match");
            await Assert.That(compatibility.GetProperty("promptVersion").GetInt32()).IsEqualTo(2);
            await Assert.That(compatibility.GetProperty("contextDocumentCount").GetInt32()).IsEqualTo(7);
            await Assert.That(DocumentPublicationContract.IsLowercaseSha256(
                manifest.GetProperty("historicalArtifactSha256").GetString())).IsTrue();
            await Assert.That(items.Length).IsEqualTo(20);
            await Assert.That(items.All(item => item.GetProperty("historicalContextManifest").GetProperty("documents").GetArrayLength() == 7)).IsTrue();
            await Assert.That(items.All(item => item.GetProperty("historicalContextManifest").GetProperty("evaluationTimestamp").GetDateTimeOffset()
                == startsAt.ToDateTimeOffset().AddHours(-12))).IsTrue();
            await Assert.That(items.Select(item => item.GetProperty("historicalContextManifest").GetProperty("manifestSha256").GetString()).Distinct().Count())
                .IsEqualTo(5);
            historicalReader.Verify(repository => repository.GetContextDocumentAtOrBeforeAsync(
                It.IsAny<string>(), "pes-squad", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Exactly(35));

            using var datasetDocument = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-dataset.json")));
            await Assert.That(datasetDocument.RootElement.GetProperty("items").EnumerateArray().All(item =>
                !item.GetProperty("metadata").TryGetProperty("historicalContextManifest", out _))).IsTrue();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Running_command_rejects_manifestless_bundesliga_source_before_writing_artifacts()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var matchOutcomeRepository = new Mock<IMatchOutcomeRepository>(MockBehavior.Strict);
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    1,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([CreateCompletedOutcome(
                    "FC Bayern München", "RB Leipzig", "manifestless", 2, 1, CompetitionIds.Bundesliga2026_27)]);
            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory
                .Setup(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2026_27))
                .Returns(matchOutcomeRepository.Object);
            var outputDirectory = Path.Combine(tempDirectory.FullName, "manifestless");
            var context = CreateCommandApp<PrepareRepeatedMatchSliceCommand>(
                "prepare-repeated-match-slice",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                context.App, context.Console, "prepare-repeated-match-slice", "--community-context", "test-community",
                "--matchdays", "1", "--match-count", "1", "--repetitions", "1", "--sample-seed", "42",
                "--output-directory", outputDirectory);

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(output).Contains("missing required immutable resolvedContextManifest");
            await Assert.That(File.Exists(Path.Combine(outputDirectory, "slice-dataset.json"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDirectory, "slice-manifest.json"))).IsFalse();
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

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
                .Setup(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2026_27))
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

        var unmarkedHistorical = new PrepareRepeatedMatchSliceSettings
        {
            Competition = CompetitionIds.Bundesliga2025_26,
            CommunityContext = "pes-squad",
            StartsAfter = "2026-02-18T00:00:00 Europe/Berlin (+01)",
            OfficialKnowledgeCutoff = "2026-02-16"
        };
        await Assert.That(unmarkedHistorical.Validate().Successful).IsFalse();
        await Assert.That(unmarkedHistorical.Validate().Message).Contains("--historical-context-compatibility");

        var wrongHistoricalMargin = new PrepareRepeatedMatchSliceSettings
        {
            Competition = CompetitionIds.Bundesliga2025_26,
            HistoricalContextCompatibility = ResolvedHistoricalExperimentContextManifest.LegacyIdHashV1,
            CommunityContext = "pes-squad",
            StartsAfter = "2026-02-18T01:00:00 Europe/Berlin (+01)",
            OfficialKnowledgeCutoff = "2026-02-16",
            MatchCount = 1,
            Repetitions = 1
        };
        await Assert.That(wrongHistoricalMargin.Validate().Successful).IsFalse();
        await Assert.That(wrongHistoricalMargin.Validate().Message).Contains(
            "2026-02-18T00:00:00 Europe/Berlin (+01)");

        wrongHistoricalMargin.StartsAfter = "2026-02-18T00:00:00 Europe/Berlin (+01)";
        await Assert.That(wrongHistoricalMargin.Validate().Successful).IsTrue();
    }

    private static PersistedMatchOutcome CreateCompletedOutcome(
        string homeTeam,
        string awayTeam,
        string tippSpielId,
        int homeGoals,
        int awayGoals,
        string competition = CompetitionIds.Bundesliga2025_26,
        NodaTime.ZonedDateTime? startsAtOverride = null,
        string communityContext = "test-community")
    {
        var startsAt = startsAtOverride ?? NodaTime.Instant.FromUtc(2025, 10, 30, 14, 30).InUtc();
        var createdAt = startsAt.ToInstant().ToDateTimeOffset();
        return new PersistedMatchOutcome(
            communityContext,
            competition,
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
