using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> ValidateBonusPrediction method behavior.
/// </summary>
public class VerifyBonusCommand_Validation_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Invalid_option_id_in_prediction_returns_error()
    {
        // Arrange - database has prediction with invalid option ID
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "invalid-option" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "invalid-option" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("invalid prediction");
    }

    [Test]
    public async Task Exceeds_max_selections_returns_error()
    {
        // Arrange - question allows 1 selection but prediction has 2
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options, maxSelections: 1);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-2" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-2" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("invalid prediction");
    }

    [Test]
    public async Task Zero_selections_returns_error()
    {
        // Arrange - prediction has no selections
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string>());
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string>());

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("invalid prediction");
    }

    [Test]
    public async Task Duplicate_selections_returns_error()
    {
        // Arrange - prediction has duplicate option IDs
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options, maxSelections: 2);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("invalid prediction");
    }

    [Test]
    public async Task Valid_single_selection_returns_success()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", maxSelections: 1);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }

    [Test]
    public async Task Valid_multiple_selections_up_to_max_returns_success()
    {
        // Arrange - question allows 3 selections, prediction has 3 valid selections
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2"),
            new("opt-3", "Option 3"),
            new("opt-4", "Option 4")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options, maxSelections: 3);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-2", "opt-3" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-2", "opt-3" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }

    [Test]
    public async Task Valid_partial_selections_below_max_returns_success()
    {
        // Arrange - question allows 3 selections, prediction has only 1
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2"),
            new("opt-3", "Option 3")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options, maxSelections: 3);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-2" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-2" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }
}
