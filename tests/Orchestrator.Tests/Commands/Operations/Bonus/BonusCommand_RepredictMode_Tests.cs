using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> reprediction mode.
/// </summary>
public class BonusCommand_RepredictMode_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Running_command_in_repredict_mode_creates_first_prediction_when_none_exists()
    {
        // Arrange - bonusRepredictionIndex = -1 means no prediction exists
        var context = CreateBonusCommandApp(bonusRepredictionIndex: -1);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No existing prediction found");
        await Assert.That(output).Contains("creating first prediction");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_creates_reprediction_when_under_limit()
    {
        // Arrange - bonusRepredictionIndex = 0 means first prediction exists
        var context = CreateBonusCommandApp(bonusRepredictionIndex: 0);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction 1");
        await Assert.That(output).Contains("current: 0");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_skips_when_at_max_repredictions()
    {
        // Arrange - bonusRepredictionIndex = 2, max = 2 means we're at limit
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: existingPrediction,
            getBonusRepredictionIndexResult: 2);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "2"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped - already at max repredictions");
        await Assert.That(output).Contains("2/2");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_shows_latest_prediction_when_skipped()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: existingPrediction,
            getBonusRepredictionIndexResult: 2);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "2"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Latest prediction:");
        await Assert.That(output).Contains("FC Bayern München");
        await Assert.That(output).Contains("reprediction 2");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_saves_with_correct_reprediction_index()
    {
        // Arrange - bonusRepredictionIndex = 1 means we should save as reprediction 2
        var context = CreateBonusCommandApp(bonusRepredictionIndex: 1);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.Verify(r => r.SaveBonusRepredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            "test-model",
            It.IsAny<string>(),
            It.IsAny<double>(),
            "test",
            It.IsAny<IEnumerable<string>>(),
            2, // nextIndex = currentIndex + 1 = 1 + 1 = 2
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_in_repredict_mode_with_verbose_shows_saved_reprediction_message()
    {
        // Arrange
        var context = CreateBonusCommandApp(bonusRepredictionIndex: 0);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved as reprediction 1 to database");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_does_not_use_normal_save()
    {
        // Arrange
        var context = CreateBonusCommandApp(bonusRepredictionIndex: 0);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Should use SaveBonusRepredictionAsync, not SaveBonusPredictionAsync
        context.PredictionRepository.Verify(r => r.SaveBonusPredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Running_command_in_repredict_mode_with_first_prediction_saves_at_index_zero()
    {
        // Arrange - bonusRepredictionIndex = -1 means no prediction, so first one is index 0
        var context = CreateBonusCommandApp(bonusRepredictionIndex: -1);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.Verify(r => r.SaveBonusRepredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            0, // First prediction in repredict mode is index 0
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_with_max_repredictions_zero_only_creates_first_prediction()
    {
        // Arrange - bonusRepredictionIndex = -1 means no prediction exists, max = 0 allows only first
        var context = CreateBonusCommandApp(bonusRepredictionIndex: -1);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "0"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("creating first prediction");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_zero_skips_after_first()
    {
        // Arrange - bonusRepredictionIndex = 0 means first exists, max = 0 means no more allowed
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: existingPrediction,
            getBonusRepredictionIndexResult: 0);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "0"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped - already at max repredictions");
        await Assert.That(output).Contains("0/0");
    }

    [Test]
    public async Task Running_command_in_repredict_mode_saves_context_document_names()
    {
        // Arrange - provide KPI context documents for reprediction
        var kpiDocs = CreateBonusQuestionKpiDocuments();
        var context = CreateBonusCommandApp(
            bonusRepredictionIndex: 0,
            kpiContextDocuments: kpiDocs);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.Verify(r => r.SaveBonusRepredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<string>>(names => names.Any()), // Verify context document names are passed
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
