using System.Text;
using System.Text.RegularExpressions;

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

    public static MatchContextDocumentSelection ForMatch(
        string homeTeam,
        string awayTeam,
        string communityContext,
        string? competition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(awayTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);
        var standingsDocumentName = GetStandingsDocumentName(competition);

        return new MatchContextDocumentSelection(
            [
                standingsDocumentName,
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

    public static string GetStandingsDocumentName(string? competition = null)
    {
        return string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase)
            ? "fifa-world-cup-2026-standings.csv"
            : "bundesliga-standings.csv";
    }

    public static string GetStandingsDocumentBaseName(string? competition = null)
    {
        var documentName = GetStandingsDocumentName(competition);
        return documentName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? documentName[..^4]
            : documentName;
    }

    public static string GetTeamAbbreviation(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        if (TeamAbbreviations.TryGetValue(teamName, out var abbreviation))
        {
            return abbreviation;
        }

        return SlugifyTeamName(teamName);
    }

    private static string SlugifyTeamName(string teamName)
    {
        var normalized = teamName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = Regex.Replace(builder.ToString().Trim('-'), "-{2,}", "-");
        return string.IsNullOrWhiteSpace(slug) ? "unknown" : slug;
    }
}
