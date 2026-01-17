using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> error handling behavior.
/// </summary>
public class VerifyBonusCommand_ErrorHandling_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Null_prediction_repository_returns_error()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp(predictionRepositoryReturnsNull: true);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error: Database not configured");
        await Assert.That(output).Contains("FIREBASE_PROJECT_ID");
    }

    [Test]
    public async Task Exception_from_kicktipp_client_returns_error()
    {
        // Arrange
        var mockKicktippClient = new Mock<IKicktippClient>();
        mockKicktippClient.Setup(c => c.GetOpenBonusQuestionsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyBonusCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:").And.Contains("Connection failed");
    }

    [Test]
    public async Task Exception_per_question_continues_processing_other_questions()
    {
        // Arrange - 2 questions, first throws exception, second succeeds
        var question1 = CreateTestBonusQuestion(text: "Question 1", formFieldName: "q1");
        var question2 = CreateTestBonusQuestion(text: "Question 2", formFieldName: "q2");

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var mockPredictionRepo = new Mock<IPredictionRepository>();
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync(
                "Question 1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync(
                "Question 2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(databasePrediction);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);

        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { question1, question2 },
            placedBonusPredictions: CreatePlacedBonusPredictions(
                ("q1", databasePrediction),
                ("q2", databasePrediction)));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert - should fail due to first question error, but still show summary
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Question 1").And.Contains("Error during verification");
        await Assert.That(output).Contains("Total bonus questions: 2");
        await Assert.That(output).Contains("Valid predictions: 1");
    }

    [Test]
    public async Task Agent_mode_shows_abbreviated_error_status()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Question 1", formFieldName: "q1");

        var mockPredictionRepo = new Mock<IPredictionRepository>();
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);
        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("q1", CreateBonusPrediction()));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Question 1");
        await Assert.That(output).Contains("(error)");
        await Assert.That(output).DoesNotContain("Error during verification");
    }

    [Test]
    public async Task Exception_in_outdated_check_shows_warning_and_continues()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var metadata = CreateBonusPredictionMetadata(
            bonusPrediction: databasePrediction,
            createdAt: new DateTimeOffset(2025, 1, 10, 10, 0, 0, TimeSpan.Zero),
            contextDocumentNames: new List<string> { "test-kpi.md" });

        var mockPredictionRepo = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: databasePrediction,
            getBonusPredictionMetadataByTextResult: metadata);

        var mockKpiRepo = new Mock<IKpiRepository>();
        mockKpiRepo.Setup(r => r.GetKpiDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("KPI lookup failed"));

        var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
        mockFirebaseFactory.Setup(f => f.CreatePredictionRepository()).Returns(mockPredictionRepo.Object);
        mockFirebaseFactory.Setup(f => f.CreateKpiRepository()).Returns(mockKpiRepo.Object);

        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated", "-v");

        // Assert - should still pass because exception is caught and treated as "not outdated"
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning: Could not check if prediction is outdated");
    }
}
