namespace EHonda.KicktippAi.Core;

public static class MatchPredictionValidator
{
    public static MatchPredictionValidationResult Validate(Match match, Prediction prediction)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(prediction);

        return ValidateScores(match, prediction.HomeGoals, prediction.AwayGoals);
    }

    public static MatchPredictionValidationResult ValidateScores(Match match, int homeGoals, int awayGoals)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.CompetitionSpecificData is FifaWorldCup2026MatchData && homeGoals == awayGoals)
        {
            return MatchPredictionValidationResult.Invalid(
                MatchPredictionValidationReasons.Wm26KnockoutDrawNotAllowed);
        }

        return MatchPredictionValidationResult.Valid;
    }

    public static string DescribeFailure(string? reasonCode) => reasonCode switch
    {
        MatchPredictionValidationReasons.Wm26KnockoutDrawNotAllowed =>
            "draws are not allowed for WM26 knockout matches",
        _ => "prediction is invalid"
    };
}

public readonly record struct MatchPredictionValidationResult(bool IsValid, string? ReasonCode)
{
    public static MatchPredictionValidationResult Valid => new(true, null);

    public static MatchPredictionValidationResult Invalid(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new MatchPredictionValidationResult(false, reasonCode);
    }
}

public static class MatchPredictionValidationReasons
{
    public const string Wm26KnockoutDrawNotAllowed = "wm26_knockout_draw_not_allowed";
}
