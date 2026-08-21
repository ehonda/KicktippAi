using System.Text;
using System.Text.RegularExpressions;

namespace EHonda.KicktippAi.Core;

public sealed record MatchContextDocumentSelection(IReadOnlyList<string> RequiredDocumentNames);

public static class MatchContextDocumentCatalog
{
    private sealed record MatchContextDocumentPolicy(
        bool IncludeCommunityRules,
        bool IncludeRecentHistory,
        bool IncludeFifaRankings,
        bool IncludeLineups,
        bool IncludeHomeAwayHistory,
        bool IncludeHeadToHead);

    private static readonly MatchContextDocumentPolicy BundesligaPolicy = new(
        IncludeCommunityRules: true,
        IncludeRecentHistory: true,
        IncludeFifaRankings: false,
        IncludeLineups: false,
        IncludeHomeAwayHistory: true,
        IncludeHeadToHead: true);

    private static readonly MatchContextDocumentPolicy WorldCup2026Policy = new(
        IncludeCommunityRules: true,
        IncludeRecentHistory: true,
        IncludeFifaRankings: true,
        IncludeLineups: true,
        IncludeHomeAwayHistory: false,
        IncludeHeadToHead: false);

    private static readonly IReadOnlyDictionary<string, MatchContextDocumentPolicy> CommunityPolicies =
        new Dictionary<string, MatchContextDocumentPolicy>(StringComparer.OrdinalIgnoreCase)
        {
            ["ehonda-dev-wm26"] = WorldCup2026Policy
        };

    private static readonly IReadOnlyDictionary<string, string> KnownCommunityCompetitions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ehonda-dev-wm26"] = CompetitionIds.FifaWorldCup2026,
            ["ehonda-dev-buli-2627"] = CompetitionIds.Bundesliga2026_27
        };

    public static MatchContextDocumentSelection ForMatch(
        string homeTeam,
        string awayTeam,
        string communityContext,
        string? competition = null)
    {
        return ForMatch(
            homeTeam,
            awayTeam,
            communityContext,
            competition,
            useKnockoutScoringRules: false);
    }

    public static MatchContextDocumentSelection ForMatch(
        Match match,
        string communityContext,
        string? competition = null)
    {
        ArgumentNullException.ThrowIfNull(match);

        return ForMatch(
            match.HomeTeam,
            match.AwayTeam,
            communityContext,
            competition,
            useKnockoutScoringRules: match.CompetitionSpecificData is FifaWorldCup2026MatchData);
    }

    private static MatchContextDocumentSelection ForMatch(
        string homeTeam,
        string awayTeam,
        string communityContext,
        string? competition,
        bool useKnockoutScoringRules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(awayTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var policy = ResolvePolicy(communityContext, competition);
        var homeAbbreviation = GetTeamAbbreviation(homeTeam, competition);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam, competition);
        var standingsDocumentName = GetStandingsDocumentName(competition);

        var requiredDocuments = new List<string> { standingsDocumentName };
        if (policy.IncludeCommunityRules)
        {
            requiredDocuments.Add(useKnockoutScoringRules
                ? $"community-rules-{communityContext}-knockout.md"
                : $"community-rules-{communityContext}.md");
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

        if (string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.OrdinalIgnoreCase))
        {
            // These names are derived only from the strict, case-sensitive season manifest.
            // Their payloads must be resolved through their publication heads, never generic latest.
            requiredDocuments.Add($"roster-{homeAbbreviation}");
            requiredDocuments.Add($"roster-{awayAbbreviation}");
            requiredDocuments.Add($"club-elo-{homeAbbreviation}.csv");
            requiredDocuments.Add($"club-elo-{awayAbbreviation}.csv");
        }

        return new MatchContextDocumentSelection(requiredDocuments);
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

        return new MatchContextDocumentSelection(requiredDocuments);
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

    public static string GetTeamAbbreviation(string teamName, string? competition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamName);

        if (string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.OrdinalIgnoreCase))
        {
            return BundesligaTeamManifest.Default.GetByKicktippName(teamName).TeamSlug;
        }

        return SlugifyTeamName(teamName);
    }

    private static MatchContextDocumentPolicy ResolvePolicy(string communityContext, string? competition)
    {
        if (!string.IsNullOrWhiteSpace(competition))
        {
            var canonicalCompetition = CompetitionIds.Canonicalize(competition);
            if (KnownCommunityCompetitions.TryGetValue(communityContext, out var communityCompetition)
                && !string.Equals(canonicalCompetition, communityCompetition, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Community context '{communityContext}' belongs to '{communityCompetition}' and conflicts with " +
                    $"the explicit competition '{canonicalCompetition}'.");
            }

            return string.Equals(canonicalCompetition, CompetitionIds.FifaWorldCup2026, StringComparison.Ordinal)
                ? WorldCup2026Policy
                : BundesligaPolicy;
        }

        if (CommunityPolicies.TryGetValue(communityContext, out var communityPolicy))
        {
            return communityPolicy;
        }

        return BundesligaPolicy;
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
