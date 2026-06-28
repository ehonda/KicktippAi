namespace EHonda.KicktippAi.Core;

/// <summary>
/// Base type for match data that only applies to one competition.
/// </summary>
public abstract record CompetitionSpecificMatchData(string Competition);

public enum FifaWorldCup2026KnockoutStage
{
    Unknown,
    RoundOf32,
    RoundOf16,
    Quarterfinal,
    Semifinal,
    ThirdPlacePlayoff,
    Final
}

public enum FifaWorldCup2026ResultBasis
{
    FinalScoreIncludingExtraTimeAndPenaltyShootout
}

/// <summary>
/// FIFA World Cup 2026 data needed to interpret a Kicktipp knockout-stage prediction.
/// </summary>
public sealed record FifaWorldCup2026MatchData(
    string? KicktippRoundName,
    FifaWorldCup2026KnockoutStage Stage,
    FifaWorldCup2026ResultBasis ResultBasis)
    : CompetitionSpecificMatchData(CompetitionIds.FifaWorldCup2026)
{
    public bool IsKnockoutStage => true;
}

public static class FifaWorldCup2026MatchDataValues
{
    public const string FinalScoreIncludingExtraTimeAndPenaltyShootout =
        "finalScoreIncludingExtraTimeAndPenaltyShootout";

    public static string ToValue(this FifaWorldCup2026KnockoutStage stage) => stage switch
    {
        FifaWorldCup2026KnockoutStage.RoundOf32 => "roundOf32",
        FifaWorldCup2026KnockoutStage.RoundOf16 => "roundOf16",
        FifaWorldCup2026KnockoutStage.Quarterfinal => "quarterfinal",
        FifaWorldCup2026KnockoutStage.Semifinal => "semifinal",
        FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff => "thirdPlacePlayoff",
        FifaWorldCup2026KnockoutStage.Final => "final",
        _ => "unknown"
    };

    public static bool TryParseStage(string? value, out FifaWorldCup2026KnockoutStage stage)
    {
        stage = value switch
        {
            "roundOf32" => FifaWorldCup2026KnockoutStage.RoundOf32,
            "roundOf16" => FifaWorldCup2026KnockoutStage.RoundOf16,
            "quarterfinal" => FifaWorldCup2026KnockoutStage.Quarterfinal,
            "semifinal" => FifaWorldCup2026KnockoutStage.Semifinal,
            "thirdPlacePlayoff" => FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff,
            "final" => FifaWorldCup2026KnockoutStage.Final,
            "unknown" => FifaWorldCup2026KnockoutStage.Unknown,
            _ => FifaWorldCup2026KnockoutStage.Unknown
        };

        return value is "roundOf32" or "roundOf16" or "quarterfinal" or "semifinal" or
            "thirdPlacePlayoff" or "final" or "unknown";
    }
}
