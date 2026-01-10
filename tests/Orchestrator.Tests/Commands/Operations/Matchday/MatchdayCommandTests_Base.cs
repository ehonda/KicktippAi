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
    /// </summary>
    /// <param name="console">Optional TestConsole instance. Defaults to a new TestConsole.</param>
    /// <param name="firebaseServiceFactory">Optional mock for IFirebaseServiceFactory.</param>
    /// <param name="kicktippClientFactory">Optional mock for IKicktippClientFactory.</param>
    /// <param name="openAiServiceFactory">Optional mock for IOpenAiServiceFactory.</param>
    /// <param name="contextProviderFactory">Optional mock for IContextProviderFactory.</param>
    /// <returns>A tuple containing the CommandApp and the TestConsole for assertions.</returns>
    protected static (CommandApp App, TestConsole Console) CreateMatchdayCommandApp(
        Option<TestConsole> console = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default,
        Option<Mock<IContextProviderFactory>> contextProviderFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());
        var mockFirebaseFactory = firebaseServiceFactory.Or(() => CreateMockFirebaseServiceFactoryFull());
        var mockKicktippFactory = kicktippClientFactory.Or(() => CreateMockKicktippClientFactory());
        var mockOpenAiFactory = openAiServiceFactory.Or(() => CreateMockOpenAiServiceFactory());
        var mockContextProviderFactory = contextProviderFactory.Or(() => CreateMockContextProviderFactory());

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

        return (app, testConsole);
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
    /// Creates a fully configured set of mocks for a standard matchday command test scenario.
    /// </summary>
    /// <param name="matchesWithHistory">Matches to return from Kicktipp client.</param>
    /// <param name="contextDocuments">Context documents to return from context repository.</param>
    /// <param name="existingPrediction">Existing prediction to return from prediction repository.</param>
    /// <param name="predictionResult">Result to return from prediction service.</param>
    /// <param name="placeBetsResult">Result of PlaceBetsAsync.</param>
    protected static MatchdayCommandMocks CreateStandardMocks(
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        NullableOption<Prediction> existingPrediction = default,
        NullableOption<Prediction> predictionResult = default,
        Option<bool> placeBetsResult = default)
    {
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

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(
            predictionService: mockPredictionService,
            tokenUsageTracker: mockTokenUsageTracker);

        var mockContextProviderFactory = CreateMockContextProviderFactory();

        return new MatchdayCommandMocks(
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
    /// Container for all mocks used in matchday command tests.
    /// </summary>
    protected record MatchdayCommandMocks(
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
