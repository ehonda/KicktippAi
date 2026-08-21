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
        var matches = Enumerable.Range(1, 8)
            .Select(_ => CreateBayernVsDortmundMatchWithHistory())
            .ToList();
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches);
        var outcomeService = new MatchOutcomeCollectionService(
            ctx.FirebaseServiceFactory.Object,
            ctx.KicktippClientFactory.Object,
            new FakeLogger<MatchOutcomeCollectionService>());
        var command = new CollectContextKicktippCommand(
            ctx.Console,
            ctx.FirebaseServiceFactory.Object,
            ctx.KicktippClientFactory.Object,
            ctx.ContextProviderFactory.Object,
            outcomeService,
            ctx.HistoryCollector.Object,
            TimeProvider.System,
            new FakeLogger<CollectContextKicktippCommand>());

        var exitCode = await command.ExecuteWithSettingsAsync(new CollectContextKicktippSettings
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
}
