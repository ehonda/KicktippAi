using System.Text;

namespace EHonda.KicktippAi.Core;

public sealed record MatchContextDocumentSelection(
    IReadOnlyList<string> RequiredDocumentNames,
    IReadOnlyList<string> OptionalDocumentNames);

public static class MatchContextDocumentCatalog
{
    private static readonly IReadOnlyDictionary<string, string> TeamAbbreviations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1. FC Heidenheim 1846", "fch" },
            { "1. FC Köln", "fck" },
            { "1. FC Union Berlin", "fcu" },
            { "1899 Hoffenheim", "tsg" },
            { "Bayer 04 Leverkusen", "b04" },
            { "Bor. Mönchengladbach", "bmg" },
            { "Borussia Dortmund", "bvb" },
            { "Eintracht Frankfurt", "sge" },
            { "FC Augsburg", "fca" },
            { "FC Bayern München", "fcb" },
            { "FC St. Pauli", "fcs" },
            { "FSV Mainz 05", "m05" },
            { "Hamburger SV", "hsv" },
            { "RB Leipzig", "rbl" },
            { "SC Freiburg", "scf" },
            { "VfB Stuttgart", "vfb" },
            { "VfL Wolfsburg", "wob" },
            { "Werder Bremen", "svw" }
        };

    public static MatchContextDocumentSelection ForMatch(string homeTeam, string awayTeam, string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(awayTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);

        return new MatchContextDocumentSelection(
            [
                "bundesliga-standings.csv",
                $"community-rules-{communityContext}.md",
                $"recent-history-{homeAbbreviation}.csv",
                $"recent-history-{awayAbbreviation}.csv",
                $"home-history-{homeAbbreviation}.csv",
                $"away-history-{awayAbbreviation}.csv",
                $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv"
            ],
            [
                $"{homeAbbreviation}-transfers.csv",
                $"{awayAbbreviation}-transfers.csv"
            ]);
    }

    public static string GetTeamAbbreviation(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        if (TeamAbbreviations.TryGetValue(teamName, out var abbreviation))
        {
            return abbreviation;
        }

        var words = teamName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();

        foreach (var word in words.Take(3))
        {
            if (word.Length > 0 && char.IsLetter(word[0]))
            {
                builder.Append(char.ToLowerInvariant(word[0]));
            }
        }

        return builder.Length > 0 ? builder.ToString() : "unknown";
    }
}
