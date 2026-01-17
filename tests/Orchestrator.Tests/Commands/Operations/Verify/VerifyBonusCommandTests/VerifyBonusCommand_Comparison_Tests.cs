using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> bonus prediction comparison logic.
/// </summary>
public class VerifyBonusCommand_Comparison_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Matching_predictions_returns_success()
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
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All predictions are valid - verification successful");
    }

    [Test]
    public async Task Both_null_predictions_counts_as_mismatch()
    {
        // Arrange - both database and Kicktipp have no prediction (database returns null, Kicktipp dict has null)
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", (BonusPrediction?)null));
        // Note: databaseBonusPrediction defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test-community");

        // Assert - no database prediction means discrepancy
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Kicktipp_has_prediction_but_database_does_not_returns_error()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction));
        // Note: databaseBonusPrediction defaults to null

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Database_has_prediction_but_kicktipp_does_not_returns_error()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", (BonusPrediction?)null),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
        await Assert.That(output).Contains("no prediction"); // Kicktipp shows "no prediction" when null
    }

    [Test]
    public async Task Different_selected_options_returns_error()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-2" });

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Verification failed");
    }

    [Test]
    public async Task Multiple_questions_with_mixed_results_shows_correct_summary()
    {
        // Arrange - 3 questions: 2 valid, 1 mismatch
        var question1 = CreateTestBonusQuestion(text: "Question 1", formFieldName: "q1");
        var question2 = CreateTestBonusQuestion(text: "Question 2", formFieldName: "q2");
        var question3 = CreateTestBonusQuestion(text: "Question 3", formFieldName: "q3");

        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        // Setup prediction repository to return different results per question
        var mockPredictionRepo = CreateMockPredictionRepository();
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync("Question 1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(databasePrediction);
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync("Question 2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(databasePrediction);
        mockPredictionRepo.Setup(r => r.GetBonusPredictionByTextAsync("Question 3", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPrediction?)null); // No database prediction

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepo);

        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { question1, question2, question3 },
            placedBonusPredictions: CreatePlacedBonusPredictions(
                ("q1", databasePrediction),
                ("q2", databasePrediction),
                ("q3", CreateBonusPrediction()))); // Kicktipp has prediction, database doesn't

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var ctx = CreateVerifyBonusCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Total bonus questions: 3");
        await Assert.That(output).Contains("Questions with database predictions: 2");
        await Assert.That(output).Contains("Valid predictions: 2");
        await Assert.That(output).Contains("Missing or invalid predictions: 1");
    }

    [Test]
    public async Task No_bonus_questions_returns_success()
    {
        // Arrange - no questions from Kicktipp
        var ctx = CreateVerifyBonusCommandApp(bonusQuestions: new List<BonusQuestion>());

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No bonus questions found on Kicktipp");
    }

    [Test]
    public async Task Multiple_selections_match_correctly()
    {
        // Arrange - question allows 2 selections, both match
        var options = new List<BonusQuestionOption>
        {
            new("opt-1", "Option 1"),
            new("opt-2", "Option 2"),
            new("opt-3", "Option 3")
        };
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1", options: options, maxSelections: 2);
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1", "opt-3" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-3", "opt-1" }); // Same IDs, different order

        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("bonus_q1", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert - should match regardless of order
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("verification successful");
    }

    [Test]
    public async Task Form_field_name_used_as_dictionary_key()
    {
        // Arrange - question has formFieldName, so that's used as dictionary key
        var question = CreateTestBonusQuestion(text: "Question Text", formFieldName: "form_field_key");
        var databasePrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });
        var kicktippPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "opt-1" });

        // Kicktipp returns prediction keyed by form field name
        var ctx = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { question },
            placedBonusPredictions: CreatePlacedBonusPredictions("form_field_key", kicktippPrediction),
            databaseBonusPrediction: databasePrediction);

        // Act
        var (exitCode, _) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
    }
}
