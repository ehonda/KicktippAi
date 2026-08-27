using EHonda.KicktippAi.Core;
using NodaTime;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

public sealed class VerifyBonusCommand_DeadlineScope_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Explicit_deadline_ceiling_verifies_only_selected_questions()
    {
        var due = CreateTestBonusQuestion(formFieldName: "due-field") with
        {
            Deadline = Instant.FromUtc(2026, 8, 28, 18, 30).InUtc()
        };
        var later = CreateTestBonusQuestion(text: "Later", formFieldName: "later-field") with
        {
            Deadline = Instant.FromUtc(2026, 9, 9, 10, 0).InUtc()
        };
        var prediction = new BonusPrediction(["opt-1"]);
        var metadata = CreateCanonicalBundesligaBonusPredictionMetadata(
            due,
            prediction,
            communityContext: "test");
        var context = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { due, later },
            placedBonusPredictions: CreatePlacedBonusPredictions("due-field", prediction),
            databaseBonusPrediction: prediction,
            bonusPredictionMetadata: metadata);

        var exitCode = await context.App.RunAsync(
        [
            "verify-bonus", "test-model", "--community", "test",
            "--bonus-deadline-at-or-before", "2026-08-28T18:30:00Z"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains("Selected 1 of 2 open bonus questions");
        await Assert.That(context.Console.Output).Contains("Total bonus questions: 1");
    }

    [Test]
    public async Task Explicit_deadline_ceiling_selecting_zero_questions_fails_visibly()
    {
        var later = CreateTestBonusQuestion() with
        {
            Deadline = Instant.FromUtc(2026, 9, 9, 10, 0).InUtc()
        };
        var context = CreateVerifyBonusCommandApp(
            bonusQuestions: new List<BonusQuestion> { later });

        var exitCode = await context.App.RunAsync(
        [
            "verify-bonus", "test-model", "--community", "test",
            "--bonus-deadline-at-or-before", "2026-08-28T18:30:00Z"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("selected zero");
    }
}
