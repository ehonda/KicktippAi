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
        context.HistoryCollector.Verify(collector => collector.Collect(
            CompetitionIds.Bundesliga2026_27,
            It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
            It.Is<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(entries => entries.Count == 430),
            It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
            It.Is<IReadOnlySet<string>>(names =>
                names.SetEquals(BundesligaHistoryPlayedDateMap.ExpectedDocumentNames))), Times.Once);
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
    public async Task Current_scope_sized_document_gap_fails_before_outcome_or_context_write()
    {
        var schedule = CreateFullSeasonSchedule();
        var context = CreateCollectContextCommandApp();
        ConfigureSchedule(context, schedule);
        ConfigureExactProvider(context, includeHeadToHead: false);
        var command = CreateCommand(context);

        var exitCode = await command.ExecuteWithSettingsAsync(CreateSettings());

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output)
            .Contains("raw full-season Kicktipp context set mismatch")
            .And.Contains("head-to-head-");
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
        var callCount = 0;
        context.ContextProvider.Setup(provider => provider.GetMatchContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => ++callCount == 100
                ? ThrowingDocuments(new InvalidOperationException("late provider failure"))
                : new List<DocumentContext>().ToAsyncEnumerable());
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

    private static Dictionary<int, List<MatchWithHistory>> CreateFullSeasonSchedule()
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
            schedule[round + 1] = CreateMatchday(firstLeg[round], round + 1);
            schedule[round + 18] = CreateMatchday(
                firstLeg[round].Select(fixture => (fixture.Away, fixture.Home)).ToArray(),
                round + 18);
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
        context.ContextProvider.Setup(provider => provider.GetMatchContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string homeTeam, string awayTeam, CancellationToken _) =>
            {
                var manifest = BundesligaTeamManifest.Default;
                var homeSlug = manifest.GetByKicktippName(homeTeam).TeamSlug;
                var awaySlug = manifest.GetByKicktippName(awayTeam).TeamSlug;
                var documents = new List<DocumentContext>
                {
                    new("bundesliga-standings.csv", "standings"),
                    new($"community-rules-{Community}.md", "rules"),
                    new($"recent-history-{homeSlug}.csv", $"recent-{homeSlug}"),
                    new($"recent-history-{awaySlug}.csv", $"recent-{awaySlug}"),
                    new($"home-history-{homeSlug}.csv", $"home-{homeSlug}"),
                    new($"away-history-{awaySlug}.csv", $"away-{awaySlug}")
                };
                if (includeHeadToHead)
                {
                    documents.Add(new(
                        $"head-to-head-{homeSlug}-vs-{awaySlug}.csv",
                        $"h2h-{homeSlug}-{awaySlug}"));
                }

                return documents.ToAsyncEnumerable();
            });
    }

    private static void ConfigureCompleteFrozenHistoryGate(CollectContextKicktippCommandTestContext context)
    {
        var resolutions = BundesligaHistoryPlayedDateMap.Default.Entries
            .Select(entry => new BundesligaHistoryPlayedDateResolution(
                entry.DocumentName,
                entry.RowOrdinal,
                entry.PlayedAt,
                BundesligaHistoryPlayedDateSourceClass.FixedExternalMap,
                $"{entry.SourceName}@{entry.SourceRevision}:{entry.SourceMatchId}"))
            .ToArray();
        context.HistoryCollector.Setup(collector => collector.Collect(
                CompetitionIds.Bundesliga2026_27,
                It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
                It.IsAny<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(),
                It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
                It.IsAny<IReadOnlySet<string>>()))
            .Returns((string _, IReadOnlyList<BundesligaHistoryDocument> documents,
                IReadOnlyList<BundesligaHistoryPlayedDateMapEntry> _, IReadOnlyList<PersistedMatchOutcome> _,
                IReadOnlySet<string> _) =>
                new BundesligaHistoryPlayedDateCollectionResult(true, documents, resolutions, []));
    }

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

    private static async IAsyncEnumerable<DocumentContext> ThrowingDocuments(Exception exception)
    {
        await Task.CompletedTask;
        throw exception;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
