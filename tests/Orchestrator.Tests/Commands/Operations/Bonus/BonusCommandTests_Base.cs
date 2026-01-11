using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Bonus;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Base class for <see cref="BonusCommand"/> tests providing shared test infrastructure.
/// </summary>
public abstract class BonusCommandTests_Base
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the bonus command.
    /// This is the primary factory method for creating test scenarios.
    /// </summary>
    /// <remarks>
    /// The method supports two usage patterns:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Simple pattern:</b> Pass domain-level parameters (bonus questions, predictions).
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
    /// <param name="openBonusQuestions">Bonus questions returned by the Kicktipp client.</param>
    /// <param name="existingBonusPrediction">Prediction returned by GetBonusPredictionByTextAsync (null = no existing).</param>
    /// <param name="predictionResult">Prediction returned by PredictBonusQuestionAsync.</param>
    /// <param name="placeBonusPredictionsResult">Result of PlaceBonusPredictionsAsync. Defaults to true.</param>
    /// <param name="kpiContextDocuments">Documents returned by GetBonusQuestionContextAsync. Defaults to empty.</param>
    /// <param name="bonusRepredictionIndex">Result of GetBonusRepredictionIndexAsync. Defaults to -1.</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="openAiServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="contextProviderFactory">Pre-configured mock.</param>
    /// <returns>A context record with the CommandApp, TestConsole, and mocks for verification.</returns>
    protected static BonusCommandTestContext CreateBonusCommandApp(
        Option<TestConsole> console = default,
        // Domain-level parameters for simple test scenarios
        Option<List<BonusQuestion>> openBonusQuestions = default,
        NullableOption<BonusPrediction> existingBonusPrediction = default,
        NullableOption<BonusPrediction> predictionResult = default,
        Option<bool> placeBonusPredictionsResult = default,
        Option<List<DocumentContext>> kpiContextDocuments = default,
        Option<int> bonusRepredictionIndex = default,
        // Factory-level parameters for advanced scenarios
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default,
        Option<Mock<IContextProviderFactory>> contextProviderFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());

        // Build internal mocks from domain parameters (used when factory mocks not provided)
        var questions = openBonusQuestions.Or(() => [CreateLeagueWinnerBonusQuestion()]);
        var kpiDocs = kpiContextDocuments.Or(() => []);

        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: questions,
            placeBonusPredictionsResult: placeBonusPredictionsResult.Or(true));

        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: existingBonusPrediction,
            getBonusRepredictionIndexResult: bonusRepredictionIndex.Or(-1));

        var mockPredictionService = CreateMockPredictionService(
            predictBonusResult: predictionResult.Or(() => CreateBonusPrediction()));

        var mockTokenUsageTracker = CreateMockTokenUsageTracker();

        var mockKpiContextProvider = CreateMockKpiContextProvider(
            bonusQuestionContextDocuments: kpiDocs);

        // Use provided factory mocks or build from internal mocks
        var mockFirebaseFactory = firebaseServiceFactory.Or(() =>
            CreateMockFirebaseServiceFactoryFull(
                predictionRepository: mockPredictionRepository));

        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));

        var mockOpenAiFactory = openAiServiceFactory.Or(() =>
            CreateMockOpenAiServiceFactory(
                predictionService: mockPredictionService,
                tokenUsageTracker: mockTokenUsageTracker));

        var mockContextProviderFactory = contextProviderFactory.Or(() =>
            CreateMockContextProviderFactory(kpiContextProvider: mockKpiContextProvider));

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockOpenAiFactory.Object);
        services.AddSingleton(mockContextProviderFactory.Object);
        services.AddSingleton<ILogger<BonusCommand>>(new FakeLogger<BonusCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<BonusCommand>("bonus");
        });

        return new BonusCommandTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockOpenAiFactory,
            mockContextProviderFactory,
            mockKicktippClient,
            mockPredictionRepository,
            mockPredictionService,
            mockTokenUsageTracker,
            mockKpiContextProvider);
    }

    /// <summary>
    /// Creates a standard test bonus question for "Who will win the league?"
    /// </summary>
    protected static BonusQuestion CreateLeagueWinnerBonusQuestion(
        Option<int> maxSelections = default,
        NullableOption<string> formFieldName = default)
    {
        return CreateBonusQuestion(
            text: "Who will win the league?",
            options: new List<BonusQuestionOption>
            {
                new("bayern", "FC Bayern München"),
                new("bvb", "Borussia Dortmund"),
                new("leverkusen", "Bayer Leverkusen")
            },
            maxSelections: maxSelections.Or(1),
            formFieldName: formFieldName.Or("bonus_q1"));
    }

    /// <summary>
    /// Creates a test bonus question about trainer changes.
    /// </summary>
    protected static BonusQuestion CreateTrainerChangeBonusQuestion(
        NullableOption<string> formFieldName = default)
    {
        return CreateBonusQuestion(
            text: "Wie viele Trainerwechsel wird es geben?",
            options: new List<BonusQuestionOption>
            {
                new("0-2", "0-2 Wechsel"),
                new("3-5", "3-5 Wechsel"),
                new("6+", "6 oder mehr Wechsel")
            },
            maxSelections: 1,
            formFieldName: formFieldName.Or("bonus_q2"));
    }

    /// <summary>
    /// Creates KPI context documents for bonus question context.
    /// </summary>
    protected static List<DocumentContext> CreateBonusQuestionKpiDocuments()
    {
        return
        [
            new DocumentContext("team-data", "Bayern: 50 pts, BVB: 45 pts, Leverkusen: 43 pts"),
            new DocumentContext("manager-data", "Bayern: Kompany, BVB: Terzic")
        ];
    }

    /// <summary>
    /// Contains the CommandApp, TestConsole, and all mocks for a bonus command test.
    /// </summary>
    protected record BonusCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IOpenAiServiceFactory> OpenAiServiceFactory,
        Mock<IContextProviderFactory> ContextProviderFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IPredictionRepository> PredictionRepository,
        Mock<IPredictionService> PredictionService,
        Mock<ITokenUsageTracker> TokenUsageTracker,
        Mock<IKpiContextProvider> KpiContextProvider);
}
