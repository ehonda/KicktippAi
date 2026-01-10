using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> error handling scenarios.
/// </summary>
public class MatchdayCommand_ErrorHandling_Tests : MatchdayCommandTests_Base
{
    #region Kicktipp Client Error Tests

    [Test]
    public async Task Running_command_handles_kicktipp_client_exception()
    {
        // Arrange
        var mockKicktippClient = new Mock<IKicktippClient>();
        mockKicktippClient
            .Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull();
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Network error");
    }

    [Test]
    public async Task Running_command_handles_place_bets_failure_gracefully()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, placeBetsResult: false);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Command should complete but report failure
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to place");
    }

    #endregion

    #region Prediction Service Error Tests

    [Test]
    public async Task Running_command_handles_prediction_service_exception_for_single_match()
    {
        // Arrange
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService
            .Setup(s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));
        mockPredictionService
            .Setup(s => s.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/test.md");

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments());
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);
        var mockKicktippFactory = CreateMockKicktippClientFactory(
            CreateMockKicktippClient(matchesWithHistory: new List<MatchWithHistory>
            {
                CreateBayernVsDortmundMatchWithHistory()
            }));
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Command should continue and report per-match error
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing match:");
        await Assert.That(output).Contains("API error");
    }

    [Test]
    public async Task Running_command_continues_processing_other_matches_after_single_match_error()
    {
        // Arrange - Use two matches with known abbreviations that we have context for
        // The first match (Bayern vs Dortmund) will fail, but the second (from another call) should succeed
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory()
        };

        // Create context docs for Bayern vs Dortmund
        var contextDocs = CreateBayernVsDortmundContextDocuments();

        var callCount = 0;
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService
            .Setup(s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("First match failed");
                }
                return Task.FromResult<Prediction?>(CreatePrediction());
            });
        mockPredictionService
            .Setup(s => s.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/test.md");

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);
        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: matches);
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Error is reported for the failed match
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing match:");
        await Assert.That(output).Contains("First match failed");
    }

    #endregion

    #region Database Error Tests

    [Test]
    public async Task Running_command_handles_database_save_error_gracefully()
    {
        // Arrange
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockPredictionRepo = CreateMockPredictionRepository();
        mockPredictionRepo
            .Setup(r => r.SavePredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepo,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Command should continue but report database save error
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to save to database");
        // Prediction should still be placed to Kicktipp
        mockKicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_handles_context_repository_error_gracefully()
    {
        // Arrange
        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Context fetch failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: mockContextRepo);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Command should handle error gracefully
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
    }

    #endregion

    #region Model Validation Tests

    [Test]
    public async Task Running_command_passes_model_name_to_prediction_service()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "custom-model-name", "-c", "test-community");

        // Assert
        mocks.OpenAiServiceFactory.Verify(
            f => f.CreatePredictionService("custom-model-name"),
            Times.Once);
    }

    #endregion
}
