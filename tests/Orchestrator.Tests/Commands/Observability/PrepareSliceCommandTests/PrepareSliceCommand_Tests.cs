using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Observability;
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
                .Setup(factory => factory.CreateMatchOutcomeRepository((string?)null))
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

    [Test]
    public async Task Running_command_filters_to_matches_strictly_after_cutoff_and_persists_metadata()
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
                    CreateCompletedOutcome(
                        homeTeam: "FC Bayern München",
                        awayTeam: "RB Leipzig",
                        startsAt: NodaTime.Instant.FromUtc(2025, 12, 31, 22, 0).InUtc(),
                        matchday: 7,
                        homeGoals: 2,
                        awayGoals: 1,
                        tippSpielId: "001")
                });
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    8,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    CreateCompletedOutcome(
                        homeTeam: "Borussia Dortmund",
                        awayTeam: "VfB Stuttgart",
                        startsAt: NodaTime.Instant.FromUtc(2025, 12, 31, 23, 0).InUtc(),
                        matchday: 8,
                        homeGoals: 1,
                        awayGoals: 1,
                        tippSpielId: "002")
                });
            matchOutcomeRepository
                .Setup(repository => repository.GetMatchdayOutcomesAsync(
                    9,
                    "test-community",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    CreateCompletedOutcome(
                        homeTeam: "Eintracht Frankfurt",
                        awayTeam: "SC Freiburg",
                        startsAt: NodaTime.Instant.FromUtc(2026, 1, 1, 0, 0).InUtc(),
                        matchday: 9,
                        homeGoals: 3,
                        awayGoals: 2,
                        tippSpielId: "003"),
                    CreateCompletedOutcome(
                        homeTeam: "Bayer Leverkusen",
                        awayTeam: "1. FC Köln",
                        startsAt: NodaTime.Instant.FromUtc(2026, 1, 2, 12, 0).InUtc(),
                        matchday: 9,
                        homeGoals: 2,
                        awayGoals: 0,
                        tippSpielId: "004")
                });

            var firebaseFactory = new Mock<IFirebaseServiceFactory>();
            firebaseFactory
                .Setup(factory => factory.CreateMatchOutcomeRepository((string?)null))
                .Returns(matchOutcomeRepository.Object);

            var outputDirectory = Path.Combine(tempDirectory.FullName, "filtered");
            var commandContext = CreateCommandApp<PrepareSliceCommand>(
                "prepare-slice",
                firebaseServiceFactory: firebaseFactory);

            var startsAfter = "\"2026-01-01T00:00:00 Europe/Berlin (+01)\"";
            var normalizedStartsAfter = "2026-01-01T00:00:00 Europe/Berlin (+01)";
            var cutoff = EvaluationTimeParser.Parse(normalizedStartsAfter);

            var (exitCode, output) = await RunCommandAsync(
                commandContext.App,
                commandContext.Console,
                "prepare-slice",
                "--community-context",
                "test-community",
                "--matchdays",
                "7,8,9",
                "--sample-size",
                "2",
                "--sample-seed",
                "42",
                "--starts-after",
                startsAfter,
                "--output-directory",
                outputDirectory);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("\"startsAfter\": \"2026-01-01T00:00:00 Europe/Berlin (\\u002B01)\"");
            await Assert.That(output).Contains("\"sourcePoolKey\": \"matchdays-7-8-9-after-20251231t230000z\"");

            using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-manifest.json")));
            var manifestRoot = manifestDocument.RootElement;
            var manifestItems = manifestRoot.GetProperty("items").EnumerateArray().ToList();

            await Assert.That(manifestRoot.GetProperty("startsAfter").GetString()).IsEqualTo(normalizedStartsAfter);
            await Assert.That(manifestRoot.GetProperty("sourcePoolKey").GetString()).IsEqualTo("matchdays-7-8-9-after-20251231t230000z");
            await Assert.That(manifestItems.Count).IsEqualTo(2);
            await Assert.That(manifestItems
                    .Select(item => item.GetProperty("sourceDatasetItemId").GetString())
                    .Where(value => value is not null)
                    .Select(value => value!)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray())
                .IsEquivalentTo([
                    "bundesliga-2025-26__test-community__ts003",
                    "bundesliga-2025-26__test-community__ts004"
                ]);

            foreach (var manifestItem in manifestItems)
            {
                var startsAt = EvaluationTimeParser.Parse(manifestItem.GetProperty("startsAt").GetString()!);
                await Assert.That(startsAt > cutoff).IsTrue();
            }

            using var datasetDocument = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "slice-dataset.json")));
            var datasetMetadata = datasetDocument.RootElement.GetProperty("datasetMetadata");
            await Assert.That(datasetMetadata.GetProperty("startsAfter").GetString()).IsEqualTo(normalizedStartsAfter);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Prepare_slice_settings_reject_invalid_starts_after_format()
    {
        var settings = new PrepareSliceSettings
        {
            CommunityContext = "test-community",
            SampleSize = 2,
            SliceKind = "random-sample",
            SampleMethod = "random-sample",
            StartsAfter = "2026-01-01T00:00 Europe/Berlin"
        };

        var result = settings.Validate();

        await Assert.That(result.Successful).IsFalse();
        await Assert.That(result.Message).Contains("ZonedDateTime 'G' pattern");
        await Assert.That(result.Message).Contains("2026-03-15T12:00:00 Europe/Berlin (+01)");
    }

    private static PersistedMatchOutcome CreateCompletedOutcome(
        string homeTeam,
        string awayTeam,
        NodaTime.ZonedDateTime startsAt,
        int matchday,
        int homeGoals,
        int awayGoals,
        string tippSpielId)
    {
        var createdAt = startsAt.ToInstant().ToDateTimeOffset();
        return new PersistedMatchOutcome(
            "test-community",
            "bundesliga-2025-26",
            homeTeam,
            awayTeam,
            startsAt,
            matchday,
            homeGoals,
            awayGoals,
            MatchOutcomeAvailability.Completed,
            tippSpielId,
            createdAt,
            createdAt);
    }
}
