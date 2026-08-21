using System.Collections.Immutable;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// The competition-owned Bundesliga bonus context selected before document retrieval.
/// P0-16 may add categories and budgets without weakening these required documents.
/// </summary>
public sealed record BundesligaBonusContextSelection(
    ImmutableArray<DocumentPublicationKey> RequiredDocuments,
    ImmutableArray<string> TargetedTeamSlugs);

/// <summary>
/// Pure ADR-0024 selection policy for the Bundesliga aggregate baseline and exact roster targets.
/// </summary>
public static class BonusContextSelectionPolicy
{
    private const string TopScorerTeamQuestion = "Welche Mannschaft stellt den Spieler mit den meisten Toren?";

    private static readonly string[] TopScorerSignals =
    [
        "torschütz",
        "torjäger",
        "meisten tore",
        "meisten toren",
        "top scorer"
    ];

    private static readonly string[] CoachSignals =
    [
        "trainer",
        "cheftrainer",
        "trainerwechsel",
        "entlassung",
        "entlassen",
        "manager",
        "coach"
    ];

    public static BundesligaBonusContextSelection SelectBundesliga(
        BonusQuestion? question,
        BundesligaRosterLastKnownGood rosters)
    {
        ArgumentNullException.ThrowIfNull(rosters);

        var required = ImmutableArray.CreateBuilder<DocumentPublicationKey>();
        required.Add(new DocumentPublicationKey(
            DocumentPublicationKind.Kpi,
            BundesligaDocumentPublication.ClubEloRankingsDocumentName));
        required.Add(new DocumentPublicationKey(
            DocumentPublicationKind.Kpi,
            BundesligaRosterPublicationContract.SquadSummaryDocumentName));

        if (question is null)
        {
            return new BundesligaBonusContextSelection(required.ToImmutable(), []);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(question.Text);
        ArgumentNullException.ThrowIfNull(question.Options);

        var topScorer = string.Equals(question.Text, TopScorerTeamQuestion, StringComparison.Ordinal)
                        || ContainsAny(question.Text, TopScorerSignals);
        var coach = ContainsAny(question.Text, CoachSignals);
        if (!topScorer && !coach)
        {
            return new BundesligaBonusContextSelection(required.ToImmutable(), []);
        }

        var searchValues = question.Options
            .Select(option => option.Text)
            .Prepend(question.Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var targets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var snapshot in rosters.Snapshots)
        {
            if (MatchesAnyExactIdentity(searchValues,
                    snapshot.Team.KicktippName,
                    snapshot.Team.OfficialName,
                    snapshot.Team.ClubEloName))
            {
                targets.Add(snapshot.Team.TeamSlug);
                continue;
            }

            var relevantMembers = snapshot.Members.Where(member =>
                topScorer && member.Role == BundesligaRosterRole.Player
                || coach && member.Role == BundesligaRosterRole.Coach);
            if (relevantMembers.Any(member => MatchesAnyExactIdentity(searchValues, member.Name)))
            {
                targets.Add(snapshot.Team.TeamSlug);
            }
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus question '{question.Text}' requires targeted roster context, " +
                "but neither its text nor its options map exactly to a current manifest team or relevant roster member.");
        }

        var orderedTargets = targets.Order(StringComparer.Ordinal).ToImmutableArray();
        required.AddRange(orderedTargets.Select(slug =>
            new DocumentPublicationKey(DocumentPublicationKind.Context, $"roster-{slug}")));
        return new BundesligaBonusContextSelection(required.ToImmutable(), orderedTargets);
    }

    private static bool ContainsAny(string value, IEnumerable<string> signals) =>
        signals.Any(signal => value.Contains(signal, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAnyExactIdentity(IEnumerable<string> values, params string[] identities) =>
        values.Any(value => identities.Any(identity =>
            string.Equals(value.Trim(), identity, StringComparison.OrdinalIgnoreCase)
            || value.Contains(identity, StringComparison.OrdinalIgnoreCase)));
}
