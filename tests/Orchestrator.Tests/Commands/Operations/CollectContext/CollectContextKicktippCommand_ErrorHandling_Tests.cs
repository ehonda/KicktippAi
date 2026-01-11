using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.CollectContext.CollectContextKicktippCommand"/> error handling scenarios.
/// </summary>
public class CollectContextKicktippCommand_ErrorHandling_Tests : CollectContextKicktippCommandTests_Base
{
    [Test]
    public async Task Running_command_handles_kicktipp_client_exception()
    {
        var mockKicktippClient = new Mock<IKicktippClient>();
        mockKicktippClient
            .Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var ctx = CreateCollectContextCommandApp(kicktippClientFactory: CreateMockKicktippClientFactory(mockKicktippClient));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Network error");
    }

    [Test]
    public async Task Running_command_handles_context_collection_exception_per_match()
    {
        var mockContextProvider = new Mock<IKicktippContextProvider>();
        mockContextProvider
            .Setup(p => p.GetMatchContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(CreateThrowingAsyncEnumerable<DocumentContext>(new InvalidOperationException("Context fetch failed")));

        var mockContextProviderFactory = CreateMockContextProviderFactory(mockContextProvider);
        var ctx = CreateCollectContextCommandApp(contextProviderFactory: mockContextProviderFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0); // Continues processing
        await Assert.That(output).Contains("Failed to collect context: Context fetch failed");
    }

    [Test]
    public async Task Running_command_continues_processing_other_matches_after_context_collection_error()
    {
        var callCount = 0;
        var mockContextProvider = new Mock<IKicktippContextProvider>();
        mockContextProvider
            .Setup(p => p.GetMatchContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    return CreateThrowingAsyncEnumerable<DocumentContext>(new InvalidOperationException("First match failed"));
                return new List<DocumentContext> { new("doc.csv", "content") }.ToAsyncEnumerable();
            });

        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory()
        };
        var mockContextProviderFactory = CreateMockContextProviderFactory(mockContextProvider);
        var ctx = CreateCollectContextCommandApp(matchesWithHistory: matches, contextProviderFactory: mockContextProviderFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to collect context: First match failed");
        await Assert.That(output).Contains("Collected 1 unique context documents");
    }

    [Test]
    public async Task Running_command_handles_save_exception_per_document()
    {
        var docs = new List<DocumentContext>
        {
            new("doc1.csv", "content1"),
            new("doc2.csv", "content2")
        };
        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo
            .Setup(r => r.SaveContextDocumentAsync("doc1.csv", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Save failed for doc1"));
        mockContextRepo
            .Setup(r => r.SaveContextDocumentAsync("doc2.csv", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        mockContextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs, firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0); // Continues processing
        await Assert.That(output).Contains("Failed to save doc1.csv: Save failed for doc1");
        await Assert.That(output).Contains("Saved: 1 documents");
    }

    [Test]
    public async Task Running_command_continues_saving_other_documents_after_single_save_error()
    {
        var docs = new List<DocumentContext>
        {
            new("doc1.csv", "content1"),
            new("doc2.csv", "content2"),
            new("doc3.csv", "content3")
        };
        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo
            .Setup(r => r.SaveContextDocumentAsync("doc2.csv", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Middle doc failed"));
        mockContextRepo
            .Setup(r => r.SaveContextDocumentAsync(It.Is<string>(s => s != "doc2.csv"), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        mockContextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var ctx = CreateCollectContextCommandApp(contextDocuments: docs, firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "collect-context-kicktipp", "--community-context", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved: 2 documents");
    }

    /// <summary>
    /// Creates an async enumerable that throws an exception when enumerated.
    /// </summary>
    private static async IAsyncEnumerable<T> CreateThrowingAsyncEnumerable<T>(Exception exception)
    {
        await Task.CompletedTask;
        throw exception;
#pragma warning disable CS0162 // Unreachable code detected - required for IAsyncEnumerable signature
        yield break;
#pragma warning restore CS0162
    }
}
