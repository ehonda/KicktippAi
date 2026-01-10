using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Base class for <see cref="MatchdayCommand"/> tests providing shared test infrastructure.
/// </summary>
public abstract class MatchdayCommandTests_Base
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the matchday command.
    /// This is the primary factory method for creating test scenarios.
    /// </summary>
    /// <remarks>
    /// The method supports two usage patterns:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Simple pattern:</b> Pass domain-level parameters (matches, documents, predictions).
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
    /// <param name="contextDocuments">Documents returned by the context repository.</param>
    /// <param name="existingPrediction">Prediction returned by GetPredictionAsync (null = no existing).</param>
    /// <param name="predictionResult">Prediction returned by PredictMatchAsync.</param>
    /// <param name="placeBetsResult">Result of PlaceBetsAsync. Defaults to true.</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="openAiServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="contextProviderFactory">Pre-configured mock.</param>
    /// <returns>A tuple with the CommandApp, TestConsole, and mocks for verification.</returns>
    protected static MatchdayCommandTestContext CreateMatchdayCommandApp(
        Option<TestConsole> console = default,
        // Domain-level parameters for simple test scenarios
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        NullableOption<Prediction> existingPrediction = default,
        NullableOption<Prediction> predictionResult = default,
        Option<bool> placeBetsResult = default,
        // Factory-level parameters for advanced scenarios
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default,
        Option<Mock<IContextProviderFactory>> contextProviderFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());

        // Build internal mocks from domain parameters (used when factory mocks not provided)
        var matches = matchesWithHistory.Or(() => [CreateBayernVsDortmundMatchWithHistory()]);
        var docs = contextDocuments.Or(() => CreateBayernVsDortmundContextDocuments());

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: matches,
            placeBetsResult: placeBetsResult.Or(true));

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(docs);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: existingPrediction);

        var mockPredictionService = CreateMockPredictionService(
            predictMatchResult: predictionResult.Or(() => CreatePrediction()));

        var mockTokenUsageTracker = CreateMockTokenUsageTracker();

        // Use provided factory mocks or build from internal mocks
        var mockFirebaseFactory = firebaseServiceFactory.Or(() =>
            CreateMockFirebaseServiceFactoryFull(
                predictionRepository: mockPredictionRepository,
                contextRepository: mockContextRepository));

        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));

        var mockOpenAiFactory = openAiServiceFactory.Or(() =>
            CreateMockOpenAiServiceFactory(
                predictionService: mockPredictionService,
                tokenUsageTracker: mockTokenUsageTracker));

        var mockContextProviderFactory = contextProviderFactory.Or(() =>
            CreateMockContextProviderFactory());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockOpenAiFactory.Object);
        services.AddSingleton(mockContextProviderFactory.Object);
        services.AddSingleton<ILogger<MatchdayCommand>>(new FakeLogger<MatchdayCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<MatchdayCommand>("matchday");
        });

        return new MatchdayCommandTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockOpenAiFactory,
            mockContextProviderFactory,
            mockKicktippClient,
            mockPredictionRepository,
            mockContextRepository,
            mockPredictionService,
            mockTokenUsageTracker);
    }

    /// <summary>
    /// Creates a test match for Bayern München vs Borussia Dortmund.
    /// These teams map to abbreviations "fcb" and "bvb" respectively.
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
    /// Creates context documents for the Bayern vs Dortmund match.
    /// </summary>
    protected static Dictionary<string, ContextDocument> CreateBayernVsDortmundContextDocuments(
        Option<string> communityContext = default,
        Option<DateTimeOffset> createdAt = default)
    {
        return CreateMatchContextDocuments(
            homeAbbreviation: "fcb",
            awayAbbreviation: "bvb",
            communityContext: communityContext.Or("test-community"),
            createdAt: createdAt);
    }

    /// <summary>
    /// Contains the CommandApp, TestConsole, and all mocks for a matchday command test.
    /// </summary>
    protected record MatchdayCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IOpenAiServiceFactory> OpenAiServiceFactory,
        Mock<IContextProviderFactory> ContextProviderFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IPredictionRepository> PredictionRepository,
        Mock<IContextRepository> ContextRepository,
        Mock<IPredictionService> PredictionService,
        Mock<ITokenUsageTracker> TokenUsageTracker);
}
