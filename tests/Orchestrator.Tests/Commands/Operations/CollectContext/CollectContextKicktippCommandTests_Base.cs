using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

/// <summary>
/// Base class for <see cref="CollectContextKicktippCommand"/> tests providing shared test infrastructure.
/// </summary>
public abstract class CollectContextKicktippCommandTests_Base
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the collect-context kicktipp command.
    /// </summary>
    /// <remarks>
    /// The method supports two usage patterns:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Simple pattern:</b> Pass domain-level parameters (matches, context documents).
    /// The method creates properly wired mocks internally.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Advanced pattern:</b> Pass pre-configured factory mocks for scenarios requiring
    /// custom mock behavior (e.g., exceptions, specific verification).
    /// </description>
    /// </item>
    /// </list>
    /// Domain parameters are ignored when corresponding factory mocks are provided.
    /// </remarks>
    /// <param name="console">Optional TestConsole. Defaults to a new TestConsole.</param>
    /// <param name="matchesWithHistory">Matches returned by the Kicktipp client.</param>
    /// <param name="contextDocuments">Documents returned by the context provider.</param>
    /// <param name="previousContextDocuments">Previous context documents for history processing.</param>
    /// <param name="saveDocumentResult">Result of SaveContextDocumentAsync (version or null if unchanged).</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="contextProviderFactory">Pre-configured mock (overrides domain params).</param>
    /// <returns>A test context with the CommandApp, TestConsole, and mocks for verification.</returns>
    protected static CollectContextKicktippCommandTestContext CreateCollectContextCommandApp(
        Option<TestConsole> console = default,
        // Domain-level parameters for simple test scenarios
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<List<DocumentContext>> contextDocuments = default,
        Option<Dictionary<string, ContextDocument>> previousContextDocuments = default,
        NullableOption<int?> saveDocumentResult = default,
        // Factory-level parameters for advanced scenarios
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IContextProviderFactory>> contextProviderFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());

        // Build internal mocks from domain parameters (used when factory mocks not provided)
        var matches = matchesWithHistory.Or(() => new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var docs = contextDocuments.Or(CreateDefaultContextDocuments);
        var previousDocs = previousContextDocuments.Or(() => new Dictionary<string, ContextDocument>());

        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: matches);

        var mockContextProvider = CreateMockKicktippContextProvider(matchContextDocuments: docs);

        var mockContextRepository = CreateMockContextRepositoryWithPreviousDocuments(
            previousDocs,
            saveDocumentResult.Or(1));

        // Use provided factory mocks or build from internal mocks
        var mockFirebaseFactory = firebaseServiceFactory.Or(() =>
            CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository));

        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));

        var mockContextProviderFactory = contextProviderFactory.Or(() =>
            CreateMockContextProviderFactory(mockContextProvider));

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockContextProviderFactory.Object);
        services.AddSingleton<ILogger<CollectContextKicktippCommand>>(new FakeLogger<CollectContextKicktippCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<CollectContextKicktippCommand>("collect-context-kicktipp");
        });

        return new CollectContextKicktippCommandTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockContextProviderFactory,
            mockKicktippClient,
            mockContextRepository,
            mockContextProvider);
    }

    /// <summary>
    /// Creates a mock <see cref="IContextRepository"/> configured to return previous documents
    /// and track save operations.
    /// </summary>
    protected static Mock<IContextRepository> CreateMockContextRepositoryWithPreviousDocuments(
        Dictionary<string, ContextDocument> previousDocuments,
        int? saveResult = 1)
    {
        var mock = new Mock<IContextRepository>();

        mock.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                previousDocuments.TryGetValue(docName, out var doc) ? doc : null);

        mock.Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveResult);

        return mock;
    }

    /// <summary>
    /// Creates a test match for Bayern München vs Borussia Dortmund.
    /// </summary>
    protected static Match CreateBayernVsDortmundMatch(
        Option<int> matchday = default,
        Option<ZonedDateTime> startsAt = default)
    {
        return CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: matchday.Or(25),
            startsAt: startsAt.Or(() => Instant.FromUtc(2025, 3, 15, 15, 30).InUtc()));
    }

    /// <summary>
    /// Creates a <see cref="MatchWithHistory"/> for the standard Bayern vs Dortmund test match.
    /// </summary>
    protected static MatchWithHistory CreateBayernVsDortmundMatchWithHistory(
        Option<int> matchday = default)
    {
        return CreateMatchWithHistory(match: CreateBayernVsDortmundMatch(matchday: matchday));
    }

    /// <summary>
    /// Creates default context documents for testing.
    /// </summary>
    protected static List<DocumentContext> CreateDefaultContextDocuments()
    {
        return
        [
            new DocumentContext("bundesliga-standings.csv", "Position,Team,Points\n1,Bayern,50"),
            new DocumentContext("recent-history-fcb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,"),
            new DocumentContext("recent-history-bvb.csv", "Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Dortmund,Mainz,3-0,")
        ];
    }

    /// <summary>
    /// Creates history document context for testing Data_Collected_At processing.
    /// </summary>
    protected static DocumentContext CreateHistoryDocumentContext(
        Option<string> name = default,
        Option<string> content = default)
    {
        var docName = name.Or("recent-history-fcb.csv");
        var docContent = content.Or("Competition,Home_Team,Away_Team,Score,Annotation\nBundesliga,Bayern,Leipzig,2-1,");
        return new DocumentContext(docName, docContent);
    }

    /// <summary>
    /// Contains the CommandApp, TestConsole, and all mocks for a collect-context command test.
    /// </summary>
    protected record CollectContextKicktippCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IContextProviderFactory> ContextProviderFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IContextRepository> ContextRepository,
        Mock<IKicktippContextProvider> ContextProvider);
}
