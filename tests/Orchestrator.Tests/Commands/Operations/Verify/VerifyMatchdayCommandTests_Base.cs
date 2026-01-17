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
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Base class for <see cref="VerifyMatchdayCommand"/> tests providing shared test infrastructure.
/// </summary>
public abstract class VerifyMatchdayCommandTests_Base
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing the verify matchday command.
    /// </summary>
    /// <remarks>
    /// The method supports two usage patterns:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Simple pattern:</b> Pass domain-level parameters (placed predictions, database predictions, etc.).
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
    /// <param name="placedPredictions">Predictions returned by GetPlacedPredictionsAsync. Defaults to empty.</param>
    /// <param name="databasePrediction">Prediction returned by GetPredictionAsync. Defaults to null.</param>
    /// <param name="predictionMetadata">Metadata returned by GetPredictionMetadataAsync. Defaults to null.</param>
    /// <param name="contextDocumentsByName">Context documents keyed by name. Defaults to empty.</param>
    /// <param name="cancelledMatchPrediction">Prediction returned by GetCancelledMatchPredictionAsync. Defaults to null.</param>
    /// <param name="predictionRepositoryReturnsNull">If true, CreatePredictionRepository returns null.</param>
    /// <param name="contextRepositoryReturnsNull">If true, CreateContextRepository returns null.</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <returns>A record with the CommandApp, TestConsole, and mocks for verification.</returns>
    protected static VerifyMatchdayCommandTestContext CreateVerifyMatchdayCommandApp(
        Option<TestConsole> console = default,
        // Domain-level parameters for simple test scenarios
        Option<Dictionary<Match, BetPrediction?>> placedPredictions = default,
        NullableOption<Prediction> databasePrediction = default,
        NullableOption<PredictionMetadata> predictionMetadata = default,
        Option<Dictionary<string, ContextDocument>> contextDocumentsByName = default,
        NullableOption<Prediction> cancelledMatchPrediction = default,
        Option<bool> predictionRepositoryReturnsNull = default,
        Option<bool> contextRepositoryReturnsNull = default,
        // Factory-level parameters for advanced scenarios
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default)
    {
        var testConsole = console.Or(() => new TestConsole());

        // Build internal mocks from domain parameters (used when factory mocks not provided)
        var predictions = placedPredictions.Or(() => new Dictionary<Match, BetPrediction?>());
        var docs = contextDocumentsByName.Or(() => new Dictionary<string, ContextDocument>());

        var mockKicktippClient = CreateMockKicktippClient(placedPredictions: predictions);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: databasePrediction,
            getPredictionMetadataResult: predictionMetadata,
            getCancelledMatchPredictionResult: cancelledMatchPrediction);

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(docs);

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

            if (contextRepositoryReturnsNull.Or(false))
            {
                factory.Setup(f => f.CreateContextRepository()).Returns((IContextRepository)null!);
            }
            else
            {
                factory.Setup(f => f.CreateContextRepository()).Returns(mockContextRepository.Object);
            }

            return factory;
        });

        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton<ILogger<VerifyMatchdayCommand>>(new FakeLogger<VerifyMatchdayCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<VerifyMatchdayCommand>("verify-matchday");
        });

        return new VerifyMatchdayCommandTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockKicktippClient,
            mockPredictionRepository,
            mockContextRepository);
    }

    /// <summary>
    /// Creates a standard test match for Bayern München vs Borussia Dortmund.
    /// </summary>
    protected static Match CreateTestMatch(
        Option<string> homeTeam = default,
        Option<string> awayTeam = default,
        Option<int> matchday = default)
    {
        return CreateMatch(
            homeTeam: homeTeam.Or("FC Bayern München"),
            awayTeam: awayTeam.Or("Borussia Dortmund"),
            matchday: matchday.Or(25));
    }

    /// <summary>
    /// Creates placed predictions dictionary with a single match and prediction.
    /// </summary>
    protected static Dictionary<Match, BetPrediction?> CreatePlacedPredictions(
        Match match,
        NullableOption<BetPrediction> prediction = default)
    {
        return new Dictionary<Match, BetPrediction?>
        {
            [match] = prediction.Or((BetPrediction?)null)
        };
    }

    /// <summary>
    /// Creates placed predictions dictionary with multiple matches.
    /// </summary>
    protected static Dictionary<Match, BetPrediction?> CreatePlacedPredictions(
        params (Match Match, BetPrediction? Prediction)[] predictions)
    {
        return predictions.ToDictionary(p => p.Match, p => p.Prediction);
    }

    /// <summary>
    /// Contains the CommandApp, TestConsole, and all mocks for a verify matchday command test.
    /// </summary>
    protected record VerifyMatchdayCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IPredictionRepository> PredictionRepository,
        Mock<IContextRepository> ContextRepository);
}
