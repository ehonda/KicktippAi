using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Services;
using Orchestrator.Tests.Infrastructure;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextKicktippCommand_FullSeason_Tests : CollectContextKicktippCommandTests_Base
{
    private const string Community = "ehonda-dev-buli-2627";

    [Test]
    public async Task Complete_profile_schedule_publishes_the_exact_362_document_set_once_in_ordinal_order()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: true);
        ConfigureCompleteFrozenHistoryGate(context);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output)
            .Contains("Collected and validated 362 exact full-season context documents")
            .And.Contains("completed atomically");
        for (var matchday = 1; matchday <= 34; matchday++)
        {
            context.KicktippClient.Verify(client => client.GetMatchesWithHistoryAsync(
                Community,
                matchday,
                CompetitionIds.Bundesliga2026_27), Times.Once);
        }

        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.Is<IReadOnlyList<ContextDocumentWrite>>(writes =>
                writes.Count == 362
                && writes.Select(write => write.DocumentName)
                    .SequenceEqual(writes.Select(write => write.DocumentName).Order(StringComparer.Ordinal))
                && writes.Select(write => write.DocumentName).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(ExpectedFullSeasonDocumentNames())),
            Community,
            It.IsAny<CancellationToken>()), Times.Once);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextProvider.Verify(provider => provider.RecentHistory(It.IsAny<string>()), Times.Exactly(18));
        context.ContextProvider.Verify(provider => provider.HomeHistory(
            It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(18));
        context.ContextProvider.Verify(provider => provider.AwayHistory(
            It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(18));
        context.ContextProvider.Verify(provider => provider.HeadToHeadHistory(
            It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(306));
        context.ContextProvider.Verify(provider => provider.GetMatchContextAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.HistoryCollector.Verify(collector => collector.Collect(
            CompetitionIds.Bundesliga2026_27,
            It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
            It.Is<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(entries => entries.Count == 434),
            It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
            It.Is<IReadOnlySet<string>>(names =>
                names.SetEquals(BundesligaHistoryPlayedDateMap.ExpectedDocumentNames))), Times.Once);
    }

    [Test]
    public async Task Tampered_canonical_history_duplicate_fails_with_redacted_hash_diagnostics()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: true);
        context.ContextProvider.Setup(provider => provider.RecentHistory(It.IsAny<string>()))
            .ReturnsAsync((string _) => new DocumentContext(
                "recent-history-b04.csv",
                "canonical-history-bytes"));
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("returned duplicate exact")
            .And.Contains("recent-history-b04.csv")
            .And.Contains("existingBytes=")
            .And.Contains("incomingBytes=")
            .And.Contains("existingSha256=")
            .And.Contains("incomingSha256=")
            .And.DoesNotContain("canonical-history-bytes");
        context.KicktippClient.Verify(client => client.GetCurrentTippuebersichtMatchdayAsync(
            It.IsAny<string>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Rolling_history_inventory_does_not_require_accumulated_map_or_incomplete_counts()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: true);
        context.HistoryCollector.Setup(collector => collector.Collect(
                CompetitionIds.Bundesliga2026_27,
                It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
                It.IsAny<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(),
                It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
                It.IsAny<IReadOnlySet<string>>()))
            .Returns((string _, IReadOnlyList<BundesligaHistoryDocument> documents,
                IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> _, IReadOnlyList<PersistedMatchOutcome> _,
                IReadOnlySet<string> _) =>
                new BundesligaHistoryPlayedDateCollectionResult(
                    true,
                    documents,
                    CompleteFrozenResolutions().Take(390).ToArray(),
                    [],
                    ExcludedIncompleteRowCount: 42));
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output)
            .Contains("completed occurrence(s)")
            .And.Contains("excluded-incomplete=42");
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Eight_of_nine_on_a_later_page_fails_before_any_provider_outcome_or_context_write()
    {
        var schedule = CreateFullSeasonSchedule();
        schedule[18].RemoveAt(schedule[18].Count - 1);
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("matchday 18").And.Contains("found 8");
        VerifyNoProviderOutcomeOrContextWrite(context);
        context.KicktippClient.Verify(client => client.GetMatchesWithHistoryAsync(
            Community,
            19,
            CompetitionIds.Bundesliga2026_27), Times.Never);
    }

    [Test]
    public async Task Duplicate_fixture_on_a_page_fails_before_any_provider_outcome_or_context_write()
    {
        var schedule = CreateFullSeasonSchedule();
        schedule[1][8] = schedule[1][0];
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("duplicate ordered fixture identity");
        VerifyNoProviderOutcomeOrContextWrite(context);
    }

    [Test]
    public async Task Missing_full_season_h2h_identity_fails_before_outcome_or_context_write()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: false);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("returned document")
            .And.Contains("instead of exact identity")
            .And.Contains("head-to-head-b04-vs-vfb.csv");
        context.KicktippClient.Verify(client => client.GetCurrentTippuebersichtMatchdayAsync(
            It.IsAny<string>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Later_provider_failure_preserves_the_entire_previous_context_set()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: true);
        var callCount = 0;
        context.ContextProvider.Setup(provider => provider.HeadToHeadHistory(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string homeTeam, string awayTeam) =>
            {
                if (++callCount == 100)
                {
                    return Task.FromException<DocumentContext>(new InvalidOperationException("late provider failure"));
                }

                var manifest = BundesligaTeamManifest.Default;
                var homeSlug = manifest.GetByKicktippName(homeTeam).TeamSlug;
                var awaySlug = manifest.GetByKicktippName(awayTeam).TeamSlug;
                return Task.FromResult(new DocumentContext(
                    $"head-to-head-{homeSlug}-vs-{awaySlug}.csv",
                    $"h2h-{homeSlug}-{awaySlug}"));
            });
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("late provider failure");
        context.KicktippClient.Verify(client => client.GetCurrentTippuebersichtMatchdayAsync(
            It.IsAny<string>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Atomic_publication_failure_never_falls_back_to_partial_individual_saves()
    {
        var schedule = CreateFullSeasonSchedule();
        var repository = CreateMockContextRepositoryWithPreviousDocuments([]);
        repository.Setup(value => value.SaveContextDocumentsAtomicallyAsync(
                It.Is<IReadOnlyList<ContextDocumentWrite>>(writes => writes.Count == 362),
                Community,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("atomic full-season failure"));
        var context = CreateCollectContextCommandApp(
            firebaseServiceFactory: OrchestratorTestFactories.CreateMockFirebaseServiceFactoryFull(
                contextRepository: repository));
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: true);
        ConfigureCompleteFrozenHistoryGate(context);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("atomic full-season failure")
            .And.DoesNotContain("completed atomically");
        repository.Verify(value => value.SaveContextDocumentsAtomicallyAsync(
            It.Is<IReadOnlyList<ContextDocumentWrite>>(writes => writes.Count == 362),
            Community,
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(value => value.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Full_season_rejects_untyped_raw_usage_conflicting_matchdays_and_unsupported_competitions()
    {
        var rawContext = CreateCollectContextCommandApp();
        var (rawExit, rawOutput) = await RunCommandAsync(
            rawContext.App,
            rawContext.Console,
            "collect-context-kicktipp",
            "--community-context", Community,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--full-season");
        await Assert.That(rawExit).IsEqualTo(1);
        await Assert.That(rawOutput).Contains("requires the typed competition profile");

        var conflictContext = CreateCollectContextCommandApp();
        var conflictSettings = CreateSettings();
        conflictSettings.Matchdays = "1";
        var conflictExit = await CreateCommand(conflictContext).ExecuteWithSettingsAsync(conflictSettings);
        await Assert.That(conflictExit).IsEqualTo(1);
        await Assert.That(conflictContext.Console.Output).Contains("cannot be combined with --matchdays");

        var unsupportedContext = CreateCollectContextCommandApp();
        var unsupportedSettings = CreateSettings();
        unsupportedSettings.CommunityContext = "ehonda-dev-wm26";
        unsupportedSettings.Competition = CompetitionIds.FifaWorldCup2026;
        unsupportedSettings.ExpectedMatchCount = 104;
        unsupportedSettings.ExpectedMatchesPerMatchday = null;
        var unsupportedExit = await CreateCommand(unsupportedContext).ExecuteWithSettingsAsync(unsupportedSettings);
        await Assert.That(unsupportedExit).IsEqualTo(1);
        await Assert.That(unsupportedContext.Console.Output)
            .Contains("supported only for")
            .And.Contains(CompetitionIds.Bundesliga2026_27);
    }

    private static CollectContextKicktippSettings CreateSettings() => new()
    {
        CommunityContext = Community,
        Competition = CompetitionIds.Bundesliga2026_27,
        FullSeason = true,
        ExpectedMatchCount = 306,
        ExpectedMatchesPerMatchday = 9
    };

    private static CollectContextKicktippCommand CreateCommand(CollectContextKicktippCommandTestContext context)
    {
        var outcomeService = new MatchOutcomeCollectionService(
            context.FirebaseServiceFactory.Object,
            context.KicktippClientFactory.Object,
            new FakeLogger<MatchOutcomeCollectionService>());
        return new CollectContextKicktippCommand(
            context.Console,
            context.FirebaseServiceFactory.Object,
            context.KicktippClientFactory.Object,
            context.ContextProviderFactory.Object,
            outcomeService,
            context.HistoryCollector.Object,
            TimeProvider.System,
            new FakeLogger<CollectContextKicktippCommand>());
    }

    internal static Dictionary<int, List<MatchWithHistory>> CreateFullSeasonSchedule()
    {
        var rotation = BundesligaTeamManifest.Default.Entries.Select(team => team.KicktippName).ToList();
        var firstLeg = new List<(string Home, string Away)[]>();
        for (var round = 0; round < rotation.Count - 1; round++)
        {
            var fixtures = new (string Home, string Away)[rotation.Count / 2];
            for (var index = 0; index < fixtures.Length; index++)
            {
                var left = rotation[index];
                var right = rotation[rotation.Count - 1 - index];
                fixtures[index] = (round + index) % 2 == 0 ? (left, right) : (right, left);
            }

            firstLeg.Add(fixtures);
            var last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        var schedule = new Dictionary<int, List<MatchWithHistory>>();
        for (var round = 0; round < firstLeg.Count; round++)
        {
            var firstMatchday = round * 2 + 1;
            var reverseMatchday = firstMatchday + 1;
            schedule[firstMatchday] = CreateMatchday(firstLeg[round], firstMatchday);
            schedule[reverseMatchday] = CreateMatchday(
                firstLeg[round].Select(fixture => (fixture.Away, fixture.Home)).ToArray(),
                reverseMatchday);
        }

        return schedule;
    }

    private static List<MatchWithHistory> CreateMatchday(
        IReadOnlyList<(string Home, string Away)> fixtures,
        int matchday)
    {
        return fixtures.Select((fixture, index) => new MatchWithHistory(
                new Match(
                    fixture.Home,
                    fixture.Away,
                    Instant.FromUtc(2026, 8, 28, 18, 0)
                        .Plus(Duration.FromHours((matchday - 1) * 24L + index))
                        .InUtc(),
                    matchday),
                [],
                []))
            .ToList();
    }

    private static void ConfigureSchedule(
        CollectContextKicktippCommandTestContext context,
        IReadOnlyDictionary<int, List<MatchWithHistory>> schedule)
    {
        context.KicktippClient.Setup(client => client.GetMatchesWithHistoryAsync(
                Community,
                It.IsAny<int>(),
                CompetitionIds.Bundesliga2026_27))
            .ReturnsAsync((string _, int matchday, string _) => schedule[matchday]);
    }

    private static void ConfigureExactProvider(
        CollectContextKicktippCommandTestContext context,
        bool includeHeadToHead)
    {
        var manifest = BundesligaTeamManifest.Default;
        context.ContextProvider.Setup(provider => provider.CurrentStandings())
            .ReturnsAsync(new DocumentContext("bundesliga-standings.csv", "standings"));
        context.ContextProvider.Setup(provider => provider.CommunityScoringRules())
            .ReturnsAsync(new DocumentContext($"community-rules-{Community}.md", "rules"));
        context.ContextProvider.Setup(provider => provider.RecentHistory(It.IsAny<string>()))
            .ReturnsAsync((string teamName) =>
            {
                var slug = manifest.GetByKicktippName(teamName).TeamSlug;
                return new DocumentContext($"recent-history-{slug}.csv", $"recent-{slug}");
            });
        context.ContextProvider.Setup(provider => provider.HomeHistory(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string homeTeam, string _) =>
            {
                var slug = manifest.GetByKicktippName(homeTeam).TeamSlug;
                return new DocumentContext($"home-history-{slug}.csv", $"home-{slug}");
            });
        context.ContextProvider.Setup(provider => provider.AwayHistory(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string _, string awayTeam) =>
            {
                var slug = manifest.GetByKicktippName(awayTeam).TeamSlug;
                return new DocumentContext($"away-history-{slug}.csv", $"away-{slug}");
            });
        context.ContextProvider.Setup(provider => provider.HeadToHeadHistory(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((string homeTeam, string awayTeam) =>
            {
                var homeSlug = manifest.GetByKicktippName(homeTeam).TeamSlug;
                var awaySlug = manifest.GetByKicktippName(awayTeam).TeamSlug;
                var name = includeHeadToHead
                    ? $"head-to-head-{homeSlug}-vs-{awaySlug}.csv"
                    : "head-to-head-missing.csv";
                return new DocumentContext(name, $"h2h-{homeSlug}-{awaySlug}");
            });
    }

    private static void ConfigureCompleteFrozenHistoryGate(CollectContextKicktippCommandTestContext context)
    {
        var resolutions = CompleteFrozenResolutions();
        context.HistoryCollector.Setup(collector => collector.Collect(
                CompetitionIds.Bundesliga2026_27,
                It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
                It.IsAny<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(),
                It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
                It.IsAny<IReadOnlySet<string>>()))
            .Returns((string _, IReadOnlyList<BundesligaHistoryDocument> documents,
                IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> _, IReadOnlyList<PersistedMatchOutcome> _,
                IReadOnlySet<string> _) =>
                new BundesligaHistoryPlayedDateCollectionResult(
                    true,
                    documents,
                    resolutions,
                    [],
                    ExcludedIncompleteRowCount: 42));
    }

    private static BundesligaHistoryPlayedDateResolution[] CompleteFrozenResolutions() =>
        BundesligaHistoryPlayedDateMap.Default.Entries
            .Select(entry => new BundesligaHistoryPlayedDateResolution(
                entry.DocumentName,
                entry.RowOrdinal,
                entry.PlayedAt,
                BundesligaHistoryPlayedDateSourceClass.FixedExternalMap,
                $"{entry.SourceName}@{entry.SourceRevision}:{entry.SourceMatchId}"))
            .ToArray();

    private static HashSet<string> ExpectedFullSeasonDocumentNames()
    {
        var historyNames = BundesligaHistoryPlayedDateMap.ExpectedDocumentNames.ToHashSet(StringComparer.Ordinal);
        return BundesligaContextHygienePolicy.GetExpectedDocuments(Community)
            .Where(document => document.Key.Kind == DocumentPublicationKind.Context)
            .Select(document => document.Key.Name)
            .Where(name => name is "bundesliga-standings.csv"
                           || name == $"community-rules-{Community}.md"
                           || historyNames.Contains(name)
                           || name.StartsWith("head-to-head-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void VerifyNoProviderOutcomeOrContextWrite(CollectContextKicktippCommandTestContext context)
    {
        context.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Never);
        context.KicktippClient.Verify(client => client.GetCurrentTippuebersichtMatchdayAsync(
            It.IsAny<string>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.FirebaseServiceFactory.Verify(factory => factory.CreatePredictionRepository(
            It.IsAny<string>()), Times.Never);
    }

}
