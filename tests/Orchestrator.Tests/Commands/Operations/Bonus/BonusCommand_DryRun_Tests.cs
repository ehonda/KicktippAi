using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> dry run mode.
/// </summary>
public class BonusCommand_DryRun_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Running_command_in_dry_run_skips_database_save()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
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
    public async Task Running_command_in_dry_run_skips_kicktipp_placement()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.KicktippClient.Verify(c => c.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Running_command_in_dry_run_shows_would_have_placed_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode");
        await Assert.That(output).Contains("would have placed");
        await Assert.That(output).Contains("no actual changes made");
    }

    [Test]
    public async Task Running_command_in_dry_run_with_verbose_shows_skipped_save_message()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run - skipped database save");
    }

    [Test]
    public async Task Running_command_in_dry_run_still_generates_predictions()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        await Assert.That(output).Contains("Generated prediction:");
        context.PredictionService.Verify(s => s.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_in_dry_run_still_uses_existing_predictions_from_database()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction();
        var context = CreateBonusCommandApp(existingBonusPrediction: existingPrediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("from database");
    }

    [Test]
    public async Task Running_command_in_dry_run_displays_token_usage()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }
}
