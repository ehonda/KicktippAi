using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Tests for <see cref="VerifyMatchdayCommand"/> error handling behavior.
/// </summary>
public class VerifyMatchdayCommand_ErrorHandling_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Null_prediction_repository_returns_error()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp(predictionRepositoryReturnsNull: true);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Database not configured");
        await Assert.That(output).Contains("FIREBASE_PROJECT_ID");
    }

    [Test]
    public async Task Null_context_repository_with_check_outdated_returns_error()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp(contextRepositoryReturnsNull: true);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Database not configured");
    }

    [Test]
    public async Task Null_context_repository_without_check_outdated_succeeds()
    {
        // Arrange - no matches, so verification passes
        var ctx = CreateVerifyMatchdayCommandApp(contextRepositoryReturnsNull: true);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert - passes because check-outdated is not enabled
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Exception_from_kicktipp_client_returns_error()
    {
        // Arrange
        var mockKicktippClient = new Mock<IKicktippClient>();
        mockKicktippClient.Setup(c => c.GetPlacedPredictionsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyMatchdayCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:").And.Contains("Connection failed");
    }

    [Test]
    public async Task Exception_per_match_continues_processing_other_matches()
    {
        // Arrange - 2 matches, first throws exception, second succeeds
        var match1 = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var match2 = CreateTestMatch(homeTeam: "Team C", awayTeam: "Team D");

        var mockPredictionRepo = new Mock<IPredictionRepository>();
        mockPredictionRepo.Setup(r => r.GetPredictionAsync(
                match1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        mockPredictionRepo.Setup(r => r.GetPredictionAsync(
                match2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePrediction(homeGoals: 1, awayGoals: 0));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);

        var predictions = CreatePlacedPredictions(
            (match1, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            (match2, CreateBetPrediction(homeGoals: 1, awayGoals: 0)));

        var mockKicktippClient = CreateMockKicktippClient(placedPredictions: predictions);
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert - should fail due to first match error, but still show summary
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Team A vs Team B").And.Contains("Error during verification");
        await Assert.That(output).Contains("Total matches: 2");
        await Assert.That(output).Contains("Matching predictions: 1");
    }

    [Test]
    public async Task Agent_mode_shows_abbreviated_error_status()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");

        var mockPredictionRepo = new Mock<IPredictionRepository>();
        mockPredictionRepo.Setup(r => r.GetPredictionAsync(
                It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);
        var mockKicktippClient = CreateMockKicktippClient(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Team A vs Team B");
        await Assert.That(output).Contains("(error)");
        await Assert.That(output).DoesNotContain("Error during verification");
    }

    [Test]
    public async Task Exception_in_outdated_check_shows_warning_and_continues()
    {
        // Arrange
        var match = CreateTestMatch();
        var metadata = CreatePredictionMetadata(
            createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "some-doc.csv" });

        var mockPredictionRepo = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(homeGoals: 2, awayGoals: 1),
            getPredictionMetadataResult: metadata);

        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Context retrieval failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepo,
            contextRepository: mockContextRepo);

        var mockKicktippClient = CreateMockKicktippClient(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert - should still pass because outdated check failure is gracefully handled
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning: Failed to check outdated status");
    }

    [Test]
    public async Task Command_displays_initialized_message()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(output).Contains("Verify matchday command initialized");
    }

    [Test]
    public async Task Command_displays_match_count_after_fetching()
    {
        // Arrange
        var match1 = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var match2 = CreateTestMatch(homeTeam: "Team C", awayTeam: "Team D");

        var predictions = CreatePlacedPredictions(
            (match1, CreateBetPrediction()),
            (match2, CreateBetPrediction()));

        var ctx = CreateVerifyMatchdayCommandApp(placedPredictions: predictions);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(output).Contains("Found 2 matches on Kicktipp");
    }
}
