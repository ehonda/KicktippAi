using EHonda.KicktippAi.Core;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Verify;

public class VerifyMatchdayCommand_Validation_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Invalid_stored_ko_draw_is_reported_as_invalid_prediction()
    {
        var match = CreateWm26KnockoutMatch();
        var prediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 1, awayGoals: 1)),
            databasePrediction: prediction);

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "verify-matchday",
            "gpt-4o",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("invalid prediction");
        await Assert.That(output).Contains("draws are not allowed for WM26 knockout matches");
    }

    private static Match CreateWm26KnockoutMatch()
    {
        return CreateMatch(homeTeam: "Germany", awayTeam: "Brazil") with
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };
    }
}
