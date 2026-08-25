namespace EHonda.KicktippAi.Core;

/// <summary>
/// Exact producer-era document-name route for Bundesliga 2025/26 historical experiments.
/// This catalog is intentionally separate from the live match-context catalog.
/// </summary>
public static class Bundesliga2025_26HistoricalExperimentDocumentCatalog
{
    private static readonly IReadOnlyDictionary<string, string> TeamAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1. FC Heidenheim 1846"] = "fch",
            ["1. FC Köln"] = "fck",
            ["1. FC Union Berlin"] = "fcu",
            ["1899 Hoffenheim"] = "tsg",
            ["Bayer 04 Leverkusen"] = "b04",
            ["Bor. Mönchengladbach"] = "bmg",
            ["Borussia Dortmund"] = "bvb",
            ["Eintracht Frankfurt"] = "sge",
            ["FC Augsburg"] = "fca",
            ["FC Bayern München"] = "fcb",
            ["FC St. Pauli"] = "fcs",
            ["FSV Mainz 05"] = "m05",
            ["Hamburger SV"] = "hsv",
            ["RB Leipzig"] = "rbl",
            ["SC Freiburg"] = "scf",
            ["VfB Stuttgart"] = "vfb",
            ["VfL Wolfsburg"] = "wob",
            ["Werder Bremen"] = "svw"
        };

    public static MatchContextDocumentSelection ForMatch(Match match, string communityContext)
    {
        ArgumentNullException.ThrowIfNull(match);
        return ForMatch(match.HomeTeam, match.AwayTeam, communityContext);
    }

    public static MatchContextDocumentSelection ForMatch(
        string homeTeam,
        string awayTeam,
        string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(awayTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var homeAlias = GetTeamAlias(homeTeam);
        var awayAlias = GetTeamAlias(awayTeam);
        return new MatchContextDocumentSelection(
        [
            "bundesliga-standings.csv",
            $"community-rules-{communityContext}.md",
            $"recent-history-{homeAlias}.csv",
            $"recent-history-{awayAlias}.csv",
            $"home-history-{homeAlias}.csv",
            $"away-history-{awayAlias}.csv",
            $"head-to-head-{homeAlias}-vs-{awayAlias}.csv"
        ]);
    }

    public static string GetTeamAlias(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);
        if (!TeamAliases.TryGetValue(teamName, out var alias))
        {
            throw new InvalidDataException(
                $"Team '{teamName}' is not in the exact Bundesliga 2025/26 historical experiment alias catalog.");
        }

        return alias;
    }
}
