using EHonda.KicktippAi.Core;
using Moq;
using NodaTime;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

public sealed class BonusCommand_DeadlineScope_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Explicit_deadline_ceiling_processes_and_posts_only_selected_questions()
    {
        var due = CreateLeagueWinnerBonusQuestion(formFieldName: "due-field") with
        {
            Deadline = Instant.FromUtc(2026, 8, 28, 18, 30).InUtc()
        };
        var later = CreateTrainerChangeBonusQuestion(formFieldName: "later-field") with
        {
            Deadline = Instant.FromUtc(2026, 9, 9, 10, 0).InUtc()
        };
        var storedPrediction = new BonusPrediction(["bayern"]);
        var storedMetadata = CreateCanonicalBundesligaBonusPredictionMetadata(
            due,
            storedPrediction,
            communityContext: "test");
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { due, later },
            existingBonusPrediction: storedPrediction,
            existingBonusPredictionMetadata: storedMetadata);

        var exitCode = await context.App.RunAsync(
        [
            "bonus", "test-model", "--community", "test",
            "--bonus-deadline-at-or-before", "2026-08-28T18:30:00Z"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(context.Console.Output).Contains("Selected 1 of 2 open bonus questions");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            "test",
            It.Is<Dictionary<string, BonusPrediction>>(predictions =>
                predictions.Count == 1
                && predictions.ContainsKey("due-field")
                && !predictions.ContainsKey("later-field")),
            false), Times.Once);
    }

    [Test]
    public async Task Explicit_deadline_ceiling_selecting_zero_questions_fails_visibly()
    {
        var later = CreateLeagueWinnerBonusQuestion() with
        {
            Deadline = Instant.FromUtc(2026, 9, 9, 10, 0).InUtc()
        };
        var context = CreateBonusCommandApp(openBonusQuestions: new List<BonusQuestion> { later });

        var exitCode = await context.App.RunAsync(
        [
            "bonus", "test-model", "--community", "test",
            "--bonus-deadline-at-or-before", "2026-08-28T18:30:00Z"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("selected zero");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }
}
