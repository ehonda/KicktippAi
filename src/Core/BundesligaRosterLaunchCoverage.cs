namespace EHonda.KicktippAi.Core;

/// <summary>
/// P0 launch floor for the pinned 2026-08-13 enrichment artifact. This is an
/// explicit activation check, not the recurring refresh policy owned by P1-05.
/// </summary>
public static class BundesligaRosterLaunchCoverage
{
    public const int RequiredKnownAgeCount = 464;
    public const int RequiredKnownPositionCount = 464;
    public const int RequiredValuedPlayerCount = 450;

    public static BundesligaRosterCoverage Validate(IReadOnlyList<BundesligaRosterClubSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var ordered = snapshots.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        if (ordered.Length != BundesligaTeamManifest.ExpectedTeamCount
            || !ordered.Select(snapshot => snapshot.Team.TeamSlug)
                .SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(team => team.TeamSlug), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Bundesliga launch roster coverage requires exact 18-club manifest coverage.");
        }

        var players = ordered
            .SelectMany(snapshot => snapshot.Members)
            .Where(member => member.Role == BundesligaRosterRole.Player)
            .ToArray();
        var coverage = new BundesligaRosterCoverage(
            players.Count(member => member.Age is not null),
            players.Count(member => member.Position is not null),
            players.Count(member => member.MarketValueEur is not null));
        if (coverage.KnownAgeCount < RequiredKnownAgeCount
            || coverage.KnownPositionCount < RequiredKnownPositionCount
            || coverage.ValuedPlayerCount < RequiredValuedPlayerCount)
        {
            throw new InvalidDataException(
                $"Bundesliga launch roster enrichment regressed: ages={coverage.KnownAgeCount}/{RequiredKnownAgeCount}, " +
                $"positions={coverage.KnownPositionCount}/{RequiredKnownPositionCount}, " +
                $"valued={coverage.ValuedPlayerCount}/{RequiredValuedPlayerCount}.");
        }

        return coverage;
    }
}

public sealed record BundesligaRosterCoverage(
    int KnownAgeCount,
    int KnownPositionCount,
    int ValuedPlayerCount);
