using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterLaunchCoverageTests
{
    [Test]
    public async Task Pinned_launch_floor_accepts_the_audited_enrichment_counts()
    {
        var snapshots = EnrichedSnapshots();

        var coverage = BundesligaRosterLaunchCoverage.Validate(snapshots);

        await Assert.That(coverage.KnownAgeCount).IsEqualTo(464);
        await Assert.That(coverage.KnownPositionCount).IsEqualTo(464);
        await Assert.That(coverage.ValuedPlayerCount).IsEqualTo(450);
    }

    [Test]
    [Arguments("age")]
    [Arguments("position")]
    [Arguments("value")]
    public async Task Pinned_launch_floor_rejects_any_audited_coverage_regression(string field)
    {
        var snapshots = EnrichedSnapshots();
        var changed = false;
        var regressed = snapshots.Select(snapshot => snapshot with
        {
            Members = snapshot.Members.Select(member =>
            {
                if (changed || member.Role != BundesligaRosterRole.Player)
                {
                    return member;
                }

                var candidate = field switch
                {
                    "age" when member.Age is not null => member with { Age = null },
                    "position" when member.Position is not null => member with { Position = null },
                    "value" when member.MarketValueEur is not null => member with { MarketValueEur = null },
                    _ => member
                };
                changed = candidate != member;
                return candidate;
            }).ToArray()
        }).ToArray();

        await Assert.That(() => BundesligaRosterLaunchCoverage.Validate(regressed))
            .Throws<InvalidDataException>();
    }

    private static IReadOnlyList<BundesligaRosterClubSnapshot> EnrichedSnapshots()
    {
        var valuedRemaining = BundesligaRosterLaunchCoverage.RequiredValuedPlayerCount;
        return BundesligaRosterSeed.Default.Entries
            .GroupBy(entry => entry.TeamSlug)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var team = BundesligaTeamManifest.Default.GetByTeamSlug(group.Key);
                var members = group.Select(entry =>
                {
                    if (entry.Role != BundesligaRosterRole.Player || entry.TransfermarktPlayerId is null)
                    {
                        return new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId);
                    }

                    var value = valuedRemaining > 0 ? 1_000_000L : (long?)null;
                    if (value is not null)
                    {
                        valuedRemaining--;
                    }
                    return new BundesligaRosterMember(
                        entry.Role,
                        entry.Name,
                        entry.TransfermarktPlayerId,
                        25,
                        BundesligaRosterPosition.Midfield,
                        value);
                }).ToArray();
                return new BundesligaRosterClubSnapshot(
                    team,
                    group.First().MembershipAsOf,
                    BundesligaRosterMembershipSource.DuckDb,
                    members);
            }).ToArray();
    }
}
