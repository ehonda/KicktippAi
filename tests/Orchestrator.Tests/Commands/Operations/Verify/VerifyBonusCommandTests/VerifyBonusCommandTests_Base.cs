using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.Verify;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Base class for <see cref="VerifyBonusCommand"/> tests providing shared test infrastructure.
/// </summary>
public abstract class VerifyBonusCommandTests_Base
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the verify bonus command.
    /// </summary>
    /// <remarks>
    /// The method supports two usage patterns:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Simple pattern:</b> Pass domain-level parameters (bonus questions, placed predictions, etc.).
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
    /// <param name="bonusQuestions">Bonus questions returned by GetOpenBonusQuestionsAsync. Defaults to empty.</param>
    /// <param name="placedBonusPredictions">Predictions returned by GetPlacedBonusPredictionsAsync. Defaults to empty.</param>
    /// <param name="databaseBonusPrediction">Prediction returned by GetBonusPredictionByTextAsync. Defaults to null.</param>
    /// <param name="bonusPredictionMetadata">Metadata returned by GetBonusPredictionMetadataByTextAsync. Defaults to null.</param>
    /// <param name="kpiDocumentsByName">KPI documents keyed by name for outdated checks. Defaults to empty.</param>
    /// <param name="predictionRepositoryReturnsNull">If true, CreatePredictionRepository returns null.</param>
    /// <param name="kpiRepositoryReturnsNull">If true, CreateKpiRepository returns null.</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <returns>A record with the CommandApp, TestConsole, and mocks for verification.</returns>
    protected static VerifyBonusCommandTestContext CreateVerifyBonusCommandApp(
        Option<TestConsole> console = default,
        // Domain-level parameters for simple test scenarios
        Option<List<BonusQuestion>> bonusQuestions = default,
        Option<Dictionary<string, BonusPrediction?>> placedBonusPredictions = default,
        NullableOption<BonusPrediction> databaseBonusPrediction = default,
        NullableOption<BonusPredictionMetadata> bonusPredictionMetadata = default,
        Option<Dictionary<string, KpiDocument>> kpiDocumentsByName = default,
        Option<bool> predictionRepositoryReturnsNull = default,
        Option<bool> kpiRepositoryReturnsNull = default,
        // Factory-level parameters for advanced scenarios
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());

        // Build internal mocks from domain parameters (used when factory mocks not provided)
        var questions = bonusQuestions.Or(() => []);
        var placedPredictions = placedBonusPredictions.Or(() => new Dictionary<string, BonusPrediction?>());
        var kpiDocs = kpiDocumentsByName.Or(() => new Dictionary<string, KpiDocument>());

        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: questions,
            placedBonusPredictions: placedPredictions);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: databaseBonusPrediction,
            getBonusPredictionMetadataByTextResult: bonusPredictionMetadata);

        var mockKpiRepository = CreateMockKpiRepositoryWithDocuments(kpiDocs);

        // Use provided factory mocks or build from internal mocks
        var mockFirebaseFactory = firebaseServiceFactory.Or(() =>
        {
            var factory = new Mock<IFirebaseServiceFactory>();

            if (predictionRepositoryReturnsNull.Or(false))
            {
                factory.Setup(f => f.CreatePredictionRepository()).Returns((IPredictionRepository)null!);
            }
            else
            {
                factory.Setup(f => f.CreatePredictionRepository()).Returns(mockPredictionRepository.Object);
            }

            if (kpiRepositoryReturnsNull.Or(false))
            {
                factory.Setup(f => f.CreateKpiRepository()).Returns((IKpiRepository)null!);
            }
            else
            {
                factory.Setup(f => f.CreateKpiRepository()).Returns(mockKpiRepository.Object);
            }

            return factory;
        });

        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton<ILogger<VerifyBonusCommand>>(new FakeLogger<VerifyBonusCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<VerifyBonusCommand>("verify-bonus");
        });

        return new VerifyBonusCommandTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockKicktippClient,
            mockPredictionRepository,
            mockKpiRepository);
    }

    /// <summary>
    /// Creates a mock <see cref="IKpiRepository"/> that returns documents based on document name.
    /// </summary>
    /// <param name="documentsByName">Dictionary mapping document names to their KPI documents.</param>
    protected static Mock<IKpiRepository> CreateMockKpiRepositoryWithDocuments(
        Dictionary<string, KpiDocument> documentsByName)
    {
        var mock = new Mock<IKpiRepository>();

        mock.Setup(r => r.GetKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                documentsByName.TryGetValue(docName, out var doc) ? doc : null);

        return mock;
    }

    /// <summary>
    /// Creates a standard test bonus question with configurable options.
    /// </summary>
    protected static BonusQuestion CreateTestBonusQuestion(
        Option<string> text = default,
        Option<string> formFieldName = default,
        Option<List<BonusQuestionOption>> options = default,
        Option<int> maxSelections = default)
    {
        return CreateBonusQuestion(
            text: text.Or("Who will win the league?"),
            formFieldName: formFieldName.Or("bonus_q1"),
            options: options,
            maxSelections: maxSelections);
    }

    /// <summary>
    /// Creates a list containing a single bonus question for simple test scenarios.
    /// </summary>
    protected static List<BonusQuestion> CreateSingleBonusQuestionList(
        Option<string> text = default,
        Option<string> formFieldName = default,
        Option<List<BonusQuestionOption>> options = default,
        Option<int> maxSelections = default)
    {
        return [CreateTestBonusQuestion(text, formFieldName, options, maxSelections)];
    }

    /// <summary>
    /// Creates placed bonus predictions dictionary with a single question and prediction.
    /// </summary>
    protected static Dictionary<string, BonusPrediction?> CreatePlacedBonusPredictions(
        string formFieldName,
        NullableOption<BonusPrediction> prediction = default)
    {
        return new Dictionary<string, BonusPrediction?>
        {
            [formFieldName] = prediction.Or((BonusPrediction?)null)
        };
    }

    /// <summary>
    /// Creates placed bonus predictions dictionary with multiple entries.
    /// </summary>
    protected static Dictionary<string, BonusPrediction?> CreatePlacedBonusPredictions(
        params (string FormFieldName, BonusPrediction? Prediction)[] predictions)
    {
        return predictions.ToDictionary(p => p.FormFieldName, p => p.Prediction);
    }

    /// <summary>
    /// Contains the CommandApp, TestConsole, and all mocks for a verify bonus command test.
    /// </summary>
    protected record VerifyBonusCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IPredictionRepository> PredictionRepository,
        Mock<IKpiRepository> KpiRepository);
}
