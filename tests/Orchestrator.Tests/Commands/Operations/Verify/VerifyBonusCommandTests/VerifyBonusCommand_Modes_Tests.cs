using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> verbose, agent, and init-bonus mode behaviors.
/// </summary>
public class VerifyBonusCommand_Modes_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Verbose_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Verbose_mode_displays_question_lookup_details()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Who will win the league?", formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", databasePrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Looking up: Who will win the league?");
    }

    [Test]
    public async Task Verbose_mode_displays_valid_prediction_with_option_text()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("✓").And.Contains("Option 1").And.Contains("valid");
    }

    [Test]
    public async Task Verbose_mode_displays_no_prediction_message()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Question without prediction", formFieldName: "bonus_q1");

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", (BonusPrediction?)null));
        // Note: databaseBonusPrediction defaults to null

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("○ Question without prediction").And.Contains("no prediction");
    }

    [Test]
    public async Task Verbose_agent_mode_displays_no_prediction_message_without_colon()
    {
        // Arrange - both database and kicktipp have no prediction, in verbose + agent mode
        var question = CreateTestBonusQuestion(text: "Question without prediction", formFieldName: "bonus_q1");

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", (BonusPrediction?)null));
        // Note: databaseBonusPrediction defaults to null

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v", "--agent");

        // Assert - in agent mode, the colon after the question text should not be shown
        await Assert.That(output).Contains("○ Question without prediction");
        await Assert.That(output).Contains("(no prediction)");
    }

    [Test]
    public async Task Agent_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("Agent mode enabled - prediction details will be hidden");
    }

    [Test]
    public async Task Agent_mode_hides_prediction_option_texts_in_verbose()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Secret question", formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v", "--agent");

        // Assert
        await Assert.That(output).Contains("Secret question");
        await Assert.That(output).Contains("(valid)");
        await Assert.That(output).DoesNotContain("Option 1");
    }

    [Test]
    public async Task Agent_mode_shows_abbreviated_mismatch_status()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Test question", formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-2" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Test question");
        await Assert.That(output).Contains("(mismatch with Kicktipp)");
        await Assert.That(output).DoesNotContain("Database:");
        await Assert.That(output).DoesNotContain("Kicktipp:");
    }

    [Test]
    public async Task Non_agent_mode_shows_detailed_mismatch_info()
    {
        // Arrange
        var question = CreateTestBonusQuestion(text: "Test question", formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-2" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(output).Contains("✗ Test question");
        await Assert.That(output).Contains("Database:").And.Contains("Option 1");
        await Assert.That(output).Contains("Kicktipp:").And.Contains("Option 2");
    }

    [Test]
    public async Task Init_bonus_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(output).Contains("Init bonus mode enabled");
    }

    [Test]
    public async Task Init_bonus_mode_returns_error_when_no_predictions_exist()
    {
        // Arrange - has questions but no database predictions
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", (BonusPrediction?)null));
        // Note: databaseBonusPrediction defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Init bonus detected - no database predictions exist");
        await Assert.That(output).Contains("Returning error to trigger initial prediction workflow");
    }

    [Test]
    public async Task Init_bonus_mode_succeeds_when_predictions_exist()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Init bonus detected");
    }

    [Test]
    public async Task Check_outdated_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(output).Contains("Outdated check enabled");
    }
}
