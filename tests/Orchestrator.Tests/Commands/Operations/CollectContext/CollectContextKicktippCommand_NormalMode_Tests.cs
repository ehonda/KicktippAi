using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Services;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> normal execution mode.
/// </summary>
public class CollectContextKicktippCommand_NormalMode_Tests : CollectContextKicktippCommandTests_Base
{
    private static readonly IReadOnlyList<(string HomeTeam, string AwayTeam)> MatchdayOneFixtures =
    [
        ("FC Bayern München", "VfB Stuttgart"),
        ("RB Leipzig", "Bor. Mönchengladbach"),
        ("FSV Mainz 05", "SC Paderborn 07"),
        ("1. FC Union Berlin", "Eintracht Frankfurt"),
        ("1. FC Köln", "1899 Hoffenheim"),
        ("SV Elversberg", "Bayer 04 Leverkusen"),
        ("Borussia Dortmund", "Hamburger SV"),
        ("SC Freiburg", "Werder Bremen"),
        ("FC Augsburg", "FC Schalke 04")
    ];

    [Test]
    public async Task Running_command_displays_initialization_message()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "test-community",
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collect-context kicktipp command initialized");
    }

    [Test]
    public async Task Running_command_displays_community_context()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "my-test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("my-test-community");
    }

    [Test]
    public async Task Running_command_with_no_matches_displays_message_and_returns_success()
    {
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "test-community",
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
    }

    [Test]
    public async Task Bundesliga_running_command_with_no_matches_fails_closed()
    {
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "ehonda-dev-buli-2627",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("derived no").And.Contains("expected selected-history documents");
        ctx.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Profile_owned_expected_match_count_fails_before_context_collection_or_publication()
    {
        var matches = CreateCurrentOpenMatches();
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 0));

        var exitCode = await CreateCommand(ctx).ExecuteWithSettingsAsync(new CollectContextKicktippSettings
        {
            CommunityContext = "ehonda-dev-buli-2627",
            Competition = CompetitionIds.Bundesliga2026_27,
            ExpectedMatchesPerMatchday = 9,
            DryRun = true
        });

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(ctx.Console.Output)
            .Contains("expected exactly 9 matches")
            .And.Contains("matchday, but found 8");
        ctx.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Never);
        ctx.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Explained_reduced_current_open_match_count_succeeds()
    {
        var matches = CreateCurrentOpenMatches();
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 1));

        var exitCode = await CreateCommand(ctx).ExecuteWithSettingsAsync(new CollectContextKicktippSettings
        {
            CommunityContext = "ehonda-dev-buli-2627",
            Competition = CompetitionIds.Bundesliga2026_27,
            ExpectedMatchesPerMatchday = 9,
            DryRun = true
        });

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(ctx.Console.Output).Contains("Found 8 matches for current matchday");
        ctx.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            "ehonda-dev-buli-2627",
            CompetitionIds.Bundesliga2026_27,
            "ehonda-dev-buli-2627",
            null), Times.Once);
        foreach (var fixture in MatchdayOneFixtures.Skip(1))
        {
            ctx.ContextProvider.Verify(provider => provider.GetMatchContextAsync(
                fixture.HomeTeam,
                fixture.AwayTeam,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Test]
    public async Task Explained_reduced_count_fails_when_an_open_fixture_has_a_different_matchday()
    {
        var matches = CreateCurrentOpenMatches(matchday: 2);
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 1));

        var exitCode = await CreateCommand(ctx).ExecuteWithSettingsAsync(new CollectContextKicktippSettings
        {
            CommunityContext = "ehonda-dev-buli-2627",
            Competition = CompetitionIds.Bundesliga2026_27,
            ExpectedMatchesPerMatchday = 9,
            DryRun = true
        });

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(ctx.Console.Output).Contains("expected exactly 9 matches").And.Contains("found 8");
        ctx.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public async Task Explained_reduced_count_does_not_relax_an_explicit_target_matchday()
    {
        var matches = CreateCurrentOpenMatches();
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 1));

        var exitCode = await CreateCommand(ctx).ExecuteWithSettingsAsync(new CollectContextKicktippSettings
        {
            CommunityContext = "ehonda-dev-buli-2627",
            Competition = CompetitionIds.Bundesliga2026_27,
            Matchdays = "1",
            ExpectedMatchesPerMatchday = 9,
            DryRun = true
        });

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(ctx.Console.Output)
            .Contains("expected exactly 9 matches")
            .And.Contains("Getting matchday 1 matches")
            .And.Contains("but found 8");
        ctx.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Reduced_count_fails_for_blank_or_duplicate_outcome_tippSpielId(bool duplicateId)
    {
        var matches = CreateCurrentOpenMatches();
        var outcomes = CreateMatchdayOutcomes(9, completedCount: 1).ToArray();
        outcomes[1] = outcomes[1] with
        {
            TippSpielId = duplicateId ? outcomes[0].TippSpielId : " "
        };
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, outcomes);

        var exitCode = await ExecuteProfileCountCommand(ctx);

        await Assert.That(exitCode).IsEqualTo(1);
        VerifyNoContextCollectionOrPublication(ctx);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Reduced_count_fails_for_duplicate_or_wrong_matchday_outcome_identity(bool wrongMatchday)
    {
        var matches = CreateCurrentOpenMatches();
        var outcomes = CreateMatchdayOutcomes(9, completedCount: 1).ToArray();
        outcomes[1] = wrongMatchday
            ? outcomes[1] with { Matchday = 2 }
            : outcomes[1] with
            {
                HomeTeam = outcomes[0].HomeTeam,
                AwayTeam = outcomes[0].AwayTeam
            };
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, outcomes);

        var exitCode = await ExecuteProfileCountCommand(ctx);

        await Assert.That(exitCode).IsEqualTo(1);
        VerifyNoContextCollectionOrPublication(ctx);
    }

    [Test]
    public async Task Reduced_count_fails_for_a_duplicate_open_fixture_identity()
    {
        var matches = CreateCurrentOpenMatches();
        matches[^1] = matches[0];
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 1));

        var exitCode = await ExecuteProfileCountCommand(ctx);

        await Assert.That(exitCode).IsEqualTo(1);
        VerifyNoContextCollectionOrPublication(ctx);
    }

    [Test]
    public async Task Reduced_count_fails_when_the_open_set_omits_a_pending_fixture()
    {
        var matches = CreateCurrentOpenMatches();
        matches[^1] = CreateFixture(MatchdayOneFixtures[0], matchday: 1);
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        ConfigureOutcomeCollection(ctx, CreateMatchdayOutcomes(9, completedCount: 1));

        var exitCode = await ExecuteProfileCountCommand(ctx);

        await Assert.That(exitCode).IsEqualTo(1);
        VerifyNoContextCollectionOrPublication(ctx);
    }

    [Test]
    public async Task Bundesliga_derives_the_exact_selected_history_set_from_fetched_fixtures()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "ehonda-dev-buli-2627",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(0);
        ctx.HistoryCollector.Verify(collector => collector.Collect(
            CompetitionIds.Bundesliga2026_27,
            It.IsAny<IReadOnlyList<BundesligaHistoryDocument>>(),
            It.IsAny<IReadOnlyList<BundesligaHistoryPlayedDateMapEntry>>(),
            It.IsAny<IReadOnlyList<PersistedMatchOutcome>>(),
            It.Is<IReadOnlySet<string>>(names => names.SetEquals(new[]
            {
                "away-history-bvb.csv",
                "home-history-fcb.csv",
                "recent-history-bvb.csv",
                "recent-history-fcb.csv"
            }))), Times.Once);
    }

    [Test]
    public async Task Running_command_displays_match_count()
    {
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory()
        };
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "test-community",
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 matches for current matchday");
    }

    [Test]
    public async Task Running_command_displays_context_collection_per_match()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "test-community",
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collecting context for:");
        await Assert.That(output).Contains("FC Bayern München vs Borussia Dortmund");
    }

    [Test]
    public async Task Running_command_displays_unique_document_count()
    {
        var docs = new List<DocumentContext>
        {
            new("doc1.csv", "content1"),
            new("doc2.csv", "content2"),
            new("doc3.csv", "content3")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collected 3 unique context documents");
    }

    [Test]
    public async Task Running_command_deduplicates_documents_across_matches()
    {
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory()
        };
        // Same document returned for both matches should be deduplicated
        var docs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50")
        };
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches, contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console,
            "collect-context-kicktipp", "--community-context", "test-community",
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collected 1 unique context documents");
    }

    [Test]
    public async Task Running_command_saves_documents_and_shows_completion()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Context collection completed!");
        await Assert.That(output).Contains("Saved:");
    }

    [Test]
    public async Task Running_command_calls_save_for_each_document()
    {
        var docs = new List<DocumentContext>
        {
            new("doc1.csv", "content1"),
            new("doc2.csv", "content2")
        };
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs);

        await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync("doc1.csv", It.IsAny<string>(), "test-community", It.IsAny<CancellationToken>()),
            Times.Once);
        ctx.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync("doc2.csv", It.IsAny<string>(), "test-community", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_shows_skipped_count_when_documents_unchanged()
    {
        var mockContextRepo = CreateMockContextRepositoryWithPreviousDocuments(
            new Dictionary<string, ContextDocument>(),
            saveResult: null); // null indicates document unchanged
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var ctx = CreateCollectContextCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped:");
        await Assert.That(output).Contains("(unchanged)");
    }

    [Test]
    public async Task Running_command_with_matchdays_collects_each_requested_matchday()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "collect-context-kicktipp",
            "--community-context",
            "test-community",
            "--matchdays",
            "2,3");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Getting matchday 2 matches");
        await Assert.That(output).Contains("Getting matchday 3 matches");

        ctx.KicktippClient.Verify(
            c => c.GetMatchesWithHistoryAsync("test-community", 2, CompetitionIds.Bundesliga2026_27),
            Times.Once);
        ctx.KicktippClient.Verify(
            c => c.GetMatchesWithHistoryAsync("test-community", 3, CompetitionIds.Bundesliga2026_27),
            Times.Once);
        ctx.KicktippClient.Verify(
            c => c.GetMatchesWithHistoryAsync("test-community", CompetitionIds.Bundesliga2026_27),
            Times.Never);

        ctx.ContextProviderFactory.Verify(
            f => f.CreateKicktippContextProvider(
                ctx.KicktippClient.Object,
                "test-community",
                CompetitionIds.Bundesliga2026_27,
                "test-community",
                2),
            Times.Once);
        ctx.ContextProviderFactory.Verify(
            f => f.CreateKicktippContextProvider(
                ctx.KicktippClient.Object,
                "test-community",
                CompetitionIds.Bundesliga2026_27,
                "test-community",
                3),
            Times.Once);
    }

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

    private static void ConfigureOutcomeCollection(
        CollectContextKicktippCommandTestContext context,
        IReadOnlyList<CollectedMatchOutcome> outcomes)
    {
        var outcomeRepository = CreateMockMatchOutcomeRepository(incompleteMatchdays: new[] { 1 });
        context.FirebaseServiceFactory
            .Setup(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(outcomeRepository.Object);
        context.KicktippClient
            .Setup(client => client.GetCurrentTippuebersichtMatchdayAsync("ehonda-dev-buli-2627"))
            .ReturnsAsync(1);
        context.KicktippClient
            .Setup(client => client.GetMatchdayOutcomesAsync("ehonda-dev-buli-2627", 1))
            .ReturnsAsync(outcomes);
    }

    private static List<MatchWithHistory> CreateCurrentOpenMatches(int matchday = 1)
    {
        return MatchdayOneFixtures
            .Skip(1)
            .Select(fixture => CreateFixture(fixture, matchday))
            .ToList();
    }

    private static MatchWithHistory CreateFixture(
        (string HomeTeam, string AwayTeam) fixture,
        int matchday)
    {
        return CreateMatchWithHistory(match: CreateMatch(
            homeTeam: fixture.HomeTeam,
            awayTeam: fixture.AwayTeam,
            matchday: matchday));
    }

    private static IReadOnlyList<CollectedMatchOutcome> CreateMatchdayOutcomes(int count, int completedCount)
    {
        return MatchdayOneFixtures
            .Take(count)
            .Select((fixture, index) => new CollectedMatchOutcome(
                fixture.HomeTeam,
                fixture.AwayTeam,
                CreateBayernVsDortmundMatch().StartsAt,
                1,
                index < completedCount ? 1 : null,
                index < completedCount ? 0 : null,
                index < completedCount ? MatchOutcomeAvailability.Completed : MatchOutcomeAvailability.Pending,
                $"fixture-{index + 1}"))
            .ToArray();
    }

    private static async Task<int> ExecuteProfileCountCommand(CollectContextKicktippCommandTestContext context)
    {
        return await CreateCommand(context).ExecuteWithSettingsAsync(new CollectContextKicktippSettings
        {
            CommunityContext = "ehonda-dev-buli-2627",
            Competition = CompetitionIds.Bundesliga2026_27,
            ExpectedMatchesPerMatchday = 9,
            DryRun = true
        });
    }

    private static void VerifyNoContextCollectionOrPublication(CollectContextKicktippCommandTestContext context)
    {
        context.ContextProviderFactory.Verify(factory => factory.CreateKicktippContextProvider(
            It.IsAny<IKicktippClient>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.ContextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
