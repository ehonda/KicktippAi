using System.Text;
using System.Text.RegularExpressions;

namespace EHonda.KicktippAi.Core;

public sealed record MatchContextDocumentSelection(
    IReadOnlyList<string> RequiredDocumentNames,
    IReadOnlyList<string> OptionalDocumentNames);

public static class MatchContextDocumentCatalog
{
    private sealed record MatchContextDocumentPolicy(
        bool IncludeCommunityRules,
        bool IncludeRecentHistory,
        bool IncludeFifaRankings,
        bool IncludeLineups,
        bool IncludeHomeAwayHistory,
        bool IncludeHeadToHead,
        bool IncludeTransfers);

    private static readonly MatchContextDocumentPolicy BundesligaPolicy = new(
        IncludeCommunityRules: true,
        IncludeRecentHistory: true,
        IncludeFifaRankings: false,
        IncludeLineups: false,
        IncludeHomeAwayHistory: true,
        IncludeHeadToHead: true,
        IncludeTransfers: true);

    private static readonly MatchContextDocumentPolicy WorldCup2026Policy = new(
        IncludeCommunityRules: true,
        IncludeRecentHistory: true,
        IncludeFifaRankings: true,
        IncludeLineups: true,
        IncludeHomeAwayHistory: false,
        IncludeHeadToHead: false,
        IncludeTransfers: false);

    private static readonly IReadOnlyDictionary<string, MatchContextDocumentPolicy> CommunityPolicies =
        new Dictionary<string, MatchContextDocumentPolicy>(StringComparer.OrdinalIgnoreCase)
        {
            ["ehonda-dev-wm26"] = WorldCup2026Policy
        };

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
        var policy = ResolvePolicy(communityContext, competition);

        var requiredDocuments = new List<string> { standingsDocumentName };
        if (policy.IncludeCommunityRules)
        {
            requiredDocuments.Add($"community-rules-{communityContext}.md");
        }

        if (policy.IncludeRecentHistory)
        {
            requiredDocuments.Add($"recent-history-{homeAbbreviation}.csv");
            requiredDocuments.Add($"recent-history-{awayAbbreviation}.csv");
        }

        if (policy.IncludeFifaRankings)
        {
            requiredDocuments.Add(GetFifaRankingDocumentName(homeTeam));
            requiredDocuments.Add(GetFifaRankingDocumentName(awayTeam));
        }

        if (policy.IncludeLineups)
        {
            requiredDocuments.Add(GetLineupDocumentName(homeTeam));
            requiredDocuments.Add(GetLineupDocumentName(awayTeam));
        }

        if (policy.IncludeHomeAwayHistory)
        {
            requiredDocuments.Add($"home-history-{homeAbbreviation}.csv");
            requiredDocuments.Add($"away-history-{awayAbbreviation}.csv");
        }

        if (policy.IncludeHeadToHead)
        {
            requiredDocuments.Add($"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv");
        }

        var optionalDocuments = policy.IncludeTransfers
            ? new List<string>
            {
                $"{homeAbbreviation}-transfers.csv",
                $"{awayAbbreviation}-transfers.csv"
            }
            : [];

        return new MatchContextDocumentSelection(requiredDocuments, optionalDocuments);
    }

    public static MatchContextDocumentSelection ForCommunity(
        string communityContext,
        string? competition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var standingsDocumentName = GetStandingsDocumentName(competition);
        var policy = ResolvePolicy(communityContext, competition);
        var requiredDocuments = new List<string> { standingsDocumentName };

        if (policy.IncludeCommunityRules)
        {
            requiredDocuments.Add($"community-rules-{communityContext}.md");
        }

        return new MatchContextDocumentSelection(requiredDocuments, []);
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

    public static string GetFifaRankingDocumentName(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        return $"fifa-ranking-{GetTeamAbbreviation(teamName)}.csv";
    }

    public static string GetLineupDocumentName(string teamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        return $"lineup-{GetTeamAbbreviation(teamName)}.csv";
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

    private static MatchContextDocumentPolicy ResolvePolicy(string communityContext, string? competition)
    {
        if (CommunityPolicies.TryGetValue(communityContext, out var communityPolicy))
        {
            return communityPolicy;
        }

        return string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase)
            ? WorldCup2026Policy
            : BundesligaPolicy;
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
