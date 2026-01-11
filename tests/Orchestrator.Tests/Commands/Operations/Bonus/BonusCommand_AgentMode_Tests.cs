using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> agent mode.
/// </summary>
public class BonusCommand_AgentMode_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Running_command_in_agent_mode_hides_existing_prediction_details()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var context = CreateBonusCommandApp(existingBonusPrediction: existingPrediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("from database");
        // Should NOT show the actual prediction option
        await Assert.That(output).DoesNotContain("FC Bayern München");
    }

    [Test]
    public async Task Running_command_in_agent_mode_hides_generated_prediction_details()
    {
        // Arrange
        var prediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bvb" });
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            predictionResult: prediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction");
        // Should NOT show the actual prediction option
        await Assert.That(output).DoesNotContain("Borussia Dortmund");
    }

    [Test]
    public async Task Running_command_in_agent_mode_shows_checkmark_for_existing_prediction()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction();
        var context = CreateBonusCommandApp(existingBonusPrediction: existingPrediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("✓");
    }

    [Test]
    public async Task Running_command_in_agent_mode_shows_checkmark_for_generated_prediction()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("✓");
    }

    [Test]
    public async Task Running_command_in_agent_mode_still_shows_question_text()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Who will win the league?");
    }

    [Test]
    public async Task Running_command_in_agent_mode_still_shows_placement_summary()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Successfully placed");
    }

    [Test]
    public async Task Running_command_in_agent_mode_hides_prediction_in_repredict_skipped_case()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var mockPredictionRepository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: existingPrediction,
            getBonusRepredictionIndexResult: 2);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "2", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped - already at max repredictions");
        // In agent mode, the latest prediction details should be hidden
        await Assert.That(output).DoesNotContain("Latest prediction:");
        await Assert.That(output).DoesNotContain("FC Bayern München");
    }
}
