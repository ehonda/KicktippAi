namespace EHonda.KicktippAi.Core;

public static class CompetitionIds
{
    public const string Bundesliga2025_26 = "bundesliga-2025-26";
    public const string Bundesliga2026_27 = "bundesliga-2026-27";
    public const string FifaWorldCup2026 = "fifa-world-cup-2026";

    public static string Canonicalize(string competition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        var value = competition.Trim();
        if (string.Equals(value, Bundesliga2025_26, StringComparison.OrdinalIgnoreCase)) return Bundesliga2025_26;
        if (string.Equals(value, Bundesliga2026_27, StringComparison.OrdinalIgnoreCase)) return Bundesliga2026_27;
        if (string.Equals(value, FifaWorldCup2026, StringComparison.OrdinalIgnoreCase)) return FifaWorldCup2026;
        throw new ArgumentOutOfRangeException(nameof(competition), competition, "Unsupported competition ID.");
    }
}
