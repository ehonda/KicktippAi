using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

internal enum BundesligaCanonicalHistoryKind
{
    Recent,
    Home,
    Away
}

internal sealed record BundesligaCanonicalHistoryRequest(
    BundesligaCanonicalHistoryKind Kind,
    string DocumentName,
    string TeamName,
    int Matchday,
    Match Match);

/// <summary>
/// Selects the one fixture-scoped source that owns each globally named Bundesliga history document.
/// The contract reproduces the accepted ADR-0032/ADR-0041 inventory without resolving collisions by
/// incidental enumeration order.
/// </summary>
internal static class BundesligaFullSeasonHistorySelector
{
    internal const int AcceptedLatestSeedMatchday = 2;

    public static IReadOnlyList<BundesligaCanonicalHistoryRequest> Select(
        IReadOnlyDictionary<int, IReadOnlyList<MatchWithHistory>> matchdays)
    {
        ArgumentNullException.ThrowIfNull(matchdays);
        if (!matchdays.TryGetValue(1, out var firstMatchday))
        {
            throw new InvalidDataException("Canonical Bundesliga history selection requires matchday 1.");
        }

        var requests = new List<BundesligaCanonicalHistoryRequest>(54);
        foreach (var team in BundesligaTeamManifest.Default.Entries.OrderBy(entry => entry.TeamSlug, StringComparer.Ordinal))
        {
            var recentFixture = firstMatchday.SingleOrDefault(match => Participates(match.Match, team.KicktippName))
                ?? throw new InvalidDataException(
                    $"Canonical recent-history selection found no matchday-1 fixture for '{team.TeamSlug}'.");
            requests.Add(new(
                BundesligaCanonicalHistoryKind.Recent,
                $"recent-history-{team.TeamSlug}.csv",
                team.KicktippName,
                1,
                recentFixture.Match));

            var homeFixture = FindEarliest(matchdays, team.KicktippName, home: true);
            requests.Add(new(
                BundesligaCanonicalHistoryKind.Home,
                $"home-history-{team.TeamSlug}.csv",
                team.KicktippName,
                homeFixture.Matchday,
                homeFixture.Match));

            var awayFixture = FindEarliest(matchdays, team.KicktippName, home: false);
            requests.Add(new(
                BundesligaCanonicalHistoryKind.Away,
                $"away-history-{team.TeamSlug}.csv",
                team.KicktippName,
                awayFixture.Matchday,
                awayFixture.Match));
        }

        var ordered = requests.OrderBy(request => request.DocumentName, StringComparer.Ordinal).ToArray();
        var expected = BundesligaHistoryPlayedDateMap.ExpectedDocumentNames.ToHashSet(StringComparer.Ordinal);
        var actual = ordered.Select(request => request.DocumentName).ToHashSet(StringComparer.Ordinal);
        if (ordered.Length != expected.Count || !actual.SetEquals(expected))
        {
            throw new InvalidDataException(
                "Canonical Bundesliga history selector did not produce the exact accepted 54-document set.");
        }

        var late = ordered.FirstOrDefault(request => request.Matchday > AcceptedLatestSeedMatchday);
        if (late is not null)
        {
            throw new InvalidDataException(
                $"Canonical history selector for '{late.DocumentName}' resolved matchday {late.Matchday}; " +
                $"the accepted ADR-0032 inventory requires every selector fixture within matchdays 1-{AcceptedLatestSeedMatchday}.");
        }

        return ordered;
    }

    private static (int Matchday, Match Match) FindEarliest(
        IReadOnlyDictionary<int, IReadOnlyList<MatchWithHistory>> matchdays,
        string teamName,
        bool home)
    {
        foreach (var (matchday, matches) in matchdays.OrderBy(pair => pair.Key))
        {
            var fixture = matches.SingleOrDefault(match => string.Equals(
                home ? match.Match.HomeTeam : match.Match.AwayTeam,
                teamName,
                StringComparison.Ordinal));
            if (fixture is not null)
            {
                return (matchday, fixture.Match);
            }
        }

        throw new InvalidDataException(
            $"Canonical {(home ? "home" : "away")}-history selection found no fixture for " +
            $"'{BundesligaTeamManifest.Default.GetByKicktippName(teamName).TeamSlug}'.");
    }

    private static bool Participates(Match match, string teamName) =>
        string.Equals(match.HomeTeam, teamName, StringComparison.Ordinal)
        || string.Equals(match.AwayTeam, teamName, StringComparison.Ordinal);
}
