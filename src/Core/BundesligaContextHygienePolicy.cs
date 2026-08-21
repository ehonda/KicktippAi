namespace EHonda.KicktippAi.Core;

[Flags]
public enum BundesligaContextDocumentUse
{
    None = 0,
    Match = 1,
    Bonus = 2,
    PublicationSupport = 4
}

public enum BundesligaContextHygieneClassification
{
    Expected,
    DeprecatedTeamOrManager,
    Transfer,
    WorldCup,
    HistoricalSeason,
    InvalidProfileOwnedName,
    Unexpected
}

public sealed record BundesligaContextHygieneAssessment(
    DocumentPublicationKey Key,
    BundesligaContextHygieneClassification Classification,
    BundesligaContextDocumentUse Use,
    bool BlocksGenericMutation,
    string Reason);

/// <summary>
/// Defines the complete storage-name hygiene boundary for Bundesliga 2026/27 context.
/// Prompt consumers still choose a bounded subset of these documents for a concrete match or
/// bonus question; this policy accounts for the names that may legitimately exist in the live
/// partition and protects profile-owned names from generic mutation paths.
/// </summary>
public static class BundesligaContextHygienePolicy
{
    private static readonly string[] ProfileOwnedPrefixes =
    [
        "community-rules-",
        "recent-history-",
        "home-history-",
        "away-history-",
        "head-to-head-",
        "roster-",
        "club-elo-"
    ];

    public static IReadOnlyList<(DocumentPublicationKey Key, BundesligaContextDocumentUse Use)> GetExpectedDocuments(
        string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var documents = new Dictionary<DocumentPublicationKey, BundesligaContextDocumentUse>();
        Add(DocumentPublicationKind.Context, "bundesliga-standings.csv", BundesligaContextDocumentUse.Match);
        Add(DocumentPublicationKind.Context, $"community-rules-{communityContext}.md", BundesligaContextDocumentUse.Match);

        var teams = BundesligaTeamManifest.Default.Entries
            .OrderBy(team => team.TeamSlug, StringComparer.Ordinal)
            .ToArray();
        foreach (var team in teams)
        {
            Add(DocumentPublicationKind.Context, $"recent-history-{team.TeamSlug}.csv", BundesligaContextDocumentUse.Match);
            Add(DocumentPublicationKind.Context, $"home-history-{team.TeamSlug}.csv", BundesligaContextDocumentUse.Match);
            Add(DocumentPublicationKind.Context, $"away-history-{team.TeamSlug}.csv", BundesligaContextDocumentUse.Match);
            Add(
                DocumentPublicationKind.Context,
                $"roster-{team.TeamSlug}",
                BundesligaContextDocumentUse.Match | BundesligaContextDocumentUse.Bonus);
            Add(DocumentPublicationKind.Context, $"club-elo-{team.TeamSlug}.csv", BundesligaContextDocumentUse.Match);
        }

        foreach (var home in teams)
        {
            foreach (var away in teams.Where(away => away.TeamSlug != home.TeamSlug))
            {
                Add(
                    DocumentPublicationKind.Context,
                    $"head-to-head-{home.TeamSlug}-vs-{away.TeamSlug}.csv",
                    BundesligaContextDocumentUse.Match);
            }
        }

        Add(
            DocumentPublicationKind.Context,
            BundesligaRosterPublicationContract.AggregateRosterDocumentName,
            BundesligaContextDocumentUse.PublicationSupport);
        Add(
            DocumentPublicationKind.Kpi,
            BundesligaRosterPublicationContract.SquadSummaryDocumentName,
            BundesligaContextDocumentUse.Bonus);
        Add(
            DocumentPublicationKind.Kpi,
            BundesligaDocumentPublication.ClubEloRankingsDocumentName,
            BundesligaContextDocumentUse.Bonus);

        return documents
            .OrderBy(entry => entry.Key.Kind)
            .ThenBy(entry => entry.Key.Name, StringComparer.Ordinal)
            .Select(entry => (entry.Key, entry.Value))
            .ToArray();

        void Add(DocumentPublicationKind kind, string name, BundesligaContextDocumentUse use)
        {
            var key = new DocumentPublicationKey(kind, name);
            documents[key] = documents.GetValueOrDefault(key) | use;
        }
    }

    public static BundesligaContextHygieneAssessment Assess(
        DocumentPublicationKind kind,
        string documentName,
        string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        var key = new DocumentPublicationKey(kind, documentName);
        var expected = GetExpectedDocuments(communityContext);
        var exact = expected.FirstOrDefault(entry => entry.Key == key);
        if (exact.Key is not null)
        {
            return new(key, BundesligaContextHygieneClassification.Expected, exact.Use, true,
                "Bundesliga 2026/27 profile-owned document");
        }

        if (expected.Any(entry => entry.Key.Kind == kind
                                  && string.Equals(entry.Key.Name, documentName, StringComparison.OrdinalIgnoreCase)))
        {
            return new(key, BundesligaContextHygieneClassification.InvalidProfileOwnedName,
                BundesligaContextDocumentUse.None, true,
                "Case-variant of a Bundesliga 2026/27 profile-owned document");
        }

        if (string.Equals(documentName, "team-data", StringComparison.OrdinalIgnoreCase)
            || string.Equals(documentName, "manager-data", StringComparison.OrdinalIgnoreCase))
        {
            return new(key, BundesligaContextHygieneClassification.DeprecatedTeamOrManager,
                BundesligaContextDocumentUse.None, true,
                "Superseded by Club Elo, roster coach rows, and team-squad-summary");
        }

        if (documentName.Contains("transfer", StringComparison.OrdinalIgnoreCase))
        {
            return new(key, BundesligaContextHygieneClassification.Transfer,
                BundesligaContextDocumentUse.None, true,
                "Transfer documents are retired from the live context contract");
        }

        if (IsWorldCupName(documentName))
        {
            return new(key, BundesligaContextHygieneClassification.WorldCup,
                BundesligaContextDocumentUse.None, true,
                "World Cup context cannot enter the Bundesliga 2026/27 partition");
        }

        if (IsHistoricalSeasonName(documentName))
        {
            return new(key, BundesligaContextHygieneClassification.HistoricalSeason,
                BundesligaContextDocumentUse.None, true,
                "Historical-season context is preserved in its own competition partition");
        }

        if (IsProfileOwnedLookingName(kind, documentName))
        {
            return new(key, BundesligaContextHygieneClassification.InvalidProfileOwnedName,
                BundesligaContextDocumentUse.None, true,
                "Name looks profile-owned but is outside the exact Bundesliga 2026/27 contract");
        }

        return new(key, BundesligaContextHygieneClassification.Unexpected,
            BundesligaContextDocumentUse.None, false,
            "Not selected by the Bundesliga 2026/27 match or bonus contracts");
    }

    public static void ThrowIfBlockedGenericMutation(
        string competition,
        DocumentPublicationKind kind,
        string documentName,
        string communityContext)
    {
        var canonicalCompetition = CompetitionIds.Canonicalize(competition);
        if (!string.Equals(canonicalCompetition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            return;
        }

        var assessment = Assess(kind, documentName, communityContext);
        if (assessment.BlocksGenericMutation)
        {
            throw new InvalidOperationException(
                $"Generic mutation of '{kind}:{documentName}' is blocked for {CompetitionIds.Bundesliga2026_27}: " +
                assessment.Reason + ".");
        }
    }

    private static bool IsProfileOwnedLookingName(DocumentPublicationKind kind, string documentName)
    {
        if (kind == DocumentPublicationKind.Context
            && (string.Equals(documentName, "bundesliga-standings.csv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(documentName, BundesligaRosterPublicationContract.AggregateRosterDocumentName, StringComparison.OrdinalIgnoreCase)
                || ProfileOwnedPrefixes.Any(prefix => documentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return kind == DocumentPublicationKind.Kpi
               && (string.Equals(documentName, BundesligaRosterPublicationContract.SquadSummaryDocumentName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(documentName, BundesligaDocumentPublication.ClubEloRankingsDocumentName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWorldCupName(string documentName) =>
        documentName.StartsWith("fifa-", StringComparison.OrdinalIgnoreCase)
        || documentName.StartsWith("lineup-", StringComparison.OrdinalIgnoreCase)
        || string.Equals(documentName, "lineups", StringComparison.OrdinalIgnoreCase)
        || documentName.Contains("wm26", StringComparison.OrdinalIgnoreCase)
        || documentName.Contains("world-cup-2026", StringComparison.OrdinalIgnoreCase);

    private static bool IsHistoricalSeasonName(string documentName) =>
        documentName.Contains("2025-26", StringComparison.OrdinalIgnoreCase)
        || documentName.Contains("2025_26", StringComparison.OrdinalIgnoreCase)
        || documentName.Contains("2025/26", StringComparison.OrdinalIgnoreCase);
}
