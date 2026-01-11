using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> error handling scenarios.
/// </summary>
public class BonusCommand_ErrorHandling_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Running_command_handles_kicktipp_client_exception()
    {
        // Arrange
        var mockKicktippClient = CreateMockKicktippClient();
        mockKicktippClient.Setup(c => c.GetOpenBonusQuestionsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var context = CreateBonusCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Connection failed");
    }

    [Test]
    public async Task Running_command_handles_prediction_service_exception()
    {
        // Arrange
        var mockPredictionService = CreateMockPredictionService();
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));

        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0); // Should continue processing
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("API error");
    }

    [Test]
    public async Task Running_command_continues_processing_after_individual_question_error()
    {
        // Arrange
        var questions = new List<BonusQuestion>
        {
            CreateLeagueWinnerBonusQuestion(formFieldName: "q1"),
            CreateTrainerChangeBonusQuestion(formFieldName: "q2")
        };

        var mockPredictionService = CreateMockPredictionService();
        var callCount = 0;
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First question error");
                return CreateBonusPrediction();
            });

        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            openBonusQuestions: questions,
            existingBonusPrediction: Option.None<BonusPrediction>(),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("Placing 1 bonus predictions"); // Only second question succeeded
    }

    [Test]
    public async Task Running_command_handles_database_save_exception_and_continues()
    {
        // Arrange
        var mockPredictionRepository = CreateMockPredictionRepository();
        mockPredictionRepository.Setup(r => r.SaveBonusPredictionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database write failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to save to database");
        await Assert.That(output).Contains("Database write failed");
        await Assert.That(output).Contains("Placing"); // Should still attempt placement
    }

    [Test]
    public async Task Running_command_handles_placement_exception()
    {
        // Arrange - need to set up bonus questions so we reach the placement step
        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { CreateLeagueWinnerBonusQuestion() });
        mockKicktippClient.Setup(c => c.PlaceBonusPredictionsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, BonusPrediction>>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("Placement failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var context = CreateBonusCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Placement failed");
    }

    [Test]
    public async Task Running_command_handles_database_lookup_exception()
    {
        // Arrange
        var mockPredictionRepository = CreateMockPredictionRepository();
        mockPredictionRepository.Setup(r => r.GetBonusPredictionByTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database read failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("Database read failed");
    }
}
