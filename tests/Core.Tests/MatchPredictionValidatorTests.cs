using EHonda.KicktippAi.Core;
using static TestUtilities.CoreTestFactories;

namespace Core.Tests;

public class MatchPredictionValidatorTests
{
    [Test]
    public async Task Wm26_knockout_draw_is_invalid()
    {
        var match = CreateMatch(homeTeam: "Germany", awayTeam: "Brazil") with
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };

        var result = MatchPredictionValidator.Validate(match, CreatePrediction(homeGoals: 1, awayGoals: 1));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.ReasonCode).IsEqualTo(MatchPredictionValidationReasons.Wm26KnockoutDrawNotAllowed);
    }

    [Test]
    public async Task Wm26_knockout_non_draw_is_valid()
    {
        var match = CreateMatch(homeTeam: "Germany", awayTeam: "Brazil") with
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };

        var result = MatchPredictionValidator.Validate(match, CreatePrediction(homeGoals: 2, awayGoals: 1));

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.ReasonCode).IsNull();
    }

    [Test]
    public async Task Wm26_group_stage_draw_is_valid()
    {
        var match = CreateMatch(homeTeam: "Germany", awayTeam: "Brazil");

        var result = MatchPredictionValidator.Validate(match, CreatePrediction(homeGoals: 1, awayGoals: 1));

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.ReasonCode).IsNull();
    }

    [Test]
    public async Task Non_wm26_draw_is_valid()
    {
        var match = CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund");

        var result = MatchPredictionValidator.Validate(match, CreatePrediction(homeGoals: 2, awayGoals: 2));

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.ReasonCode).IsNull();
    }
}
