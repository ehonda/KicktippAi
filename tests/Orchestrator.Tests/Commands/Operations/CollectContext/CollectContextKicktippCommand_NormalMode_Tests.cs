using EHonda.KicktippAi.Core;
using Moq;
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

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

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

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
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

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 matches for current matchday");
    }

    [Test]
    public async Task Running_command_displays_context_collection_per_match()
    {
        var ctx = CreateCollectContextCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

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

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

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
}
