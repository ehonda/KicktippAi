using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaClubEloPolicyTests
{
    [Test]
    public async Task Disabled_network_retains_the_freshest_complete_seed_or_last_known_good()
    {
        var seed = Snapshot(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var lastKnownGood = Snapshot(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.LastKnownGood);
        var network = Snapshot(new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.NetworkCandidate);

        var selection = BundesligaClubEloPolicy.Select(
            seed,
            lastKnownGood,
            BundesligaClubEloSourceResult.Complete(network),
            unattendedNetworkUseAllowed: false);

        await Assert.That(selection.Selected).IsSameReferenceAs(lastKnownGood);
        await Assert.That(selection.Disposition).IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkDisabled);
        await Assert.That(selection.Diagnostics).IsEquivalentTo(["UNATTENDED_NETWORK_USE_NOT_APPROVED"]);
    }

    [Test]
    public async Task Missing_or_partial_network_candidate_cannot_replace_last_known_good()
    {
        var seed = Snapshot(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var lastKnownGood = Snapshot(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.LastKnownGood);
        var partial = BundesligaClubEloSourceResult.Rejected("MISSING_ALIAS:Schalke");

        var missingSelection = BundesligaClubEloPolicy.Select(seed, lastKnownGood, null, unattendedNetworkUseAllowed: true);
        var partialSelection = BundesligaClubEloPolicy.Select(seed, lastKnownGood, partial, unattendedNetworkUseAllowed: true);

        await Assert.That(missingSelection.Selected).IsSameReferenceAs(lastKnownGood);
        await Assert.That(missingSelection.Disposition)
            .IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkCandidateRejected);
        await Assert.That(missingSelection.Diagnostics).IsEquivalentTo(["NETWORK_CANDIDATE_UNAVAILABLE"]);
        await Assert.That(partialSelection.Selected).IsSameReferenceAs(lastKnownGood);
        await Assert.That(partialSelection.Diagnostics).IsEquivalentTo(["MISSING_ALIAS:Schalke"]);
    }

    [Test]
    public async Task Network_candidate_is_accepted_at_the_seven_day_freshness_boundary()
    {
        var seed = Snapshot(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 20), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var network = Snapshot(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 22), BundesligaClubEloSnapshotOrigin.NetworkCandidate);

        var selection = BundesligaClubEloPolicy.Select(
            seed,
            null,
            BundesligaClubEloSourceResult.Complete(network),
            unattendedNetworkUseAllowed: true);

        await Assert.That(selection.Selected).IsSameReferenceAs(network);
        await Assert.That(selection.Disposition).IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkAccepted);
        await Assert.That(selection.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Network_candidate_older_than_seven_days_is_stale_but_seed_age_is_exposed_not_rejected()
    {
        var oldSeed = Snapshot(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 20), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var staleNetwork = Snapshot(new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 23), BundesligaClubEloSnapshotOrigin.NetworkCandidate);

        var selection = BundesligaClubEloPolicy.Select(
            oldSeed,
            null,
            BundesligaClubEloSourceResult.Complete(staleNetwork),
            unattendedNetworkUseAllowed: true);

        await Assert.That(selection.Selected).IsSameReferenceAs(oldSeed);
        await Assert.That(selection.Disposition).IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkCandidateStale);
        await Assert.That(selection.Diagnostics)
            .IsEquivalentTo(["NETWORK_CANDIDATE_STALE:AGE_DAYS=8:MAX_DAYS=7"]);
    }

    [Test]
    public async Task Complete_fresh_network_candidate_must_also_be_newer_than_retained_snapshot()
    {
        var seed = Snapshot(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 16), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var sameRatedDate = Snapshot(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 17), BundesligaClubEloSnapshotOrigin.NetworkCandidate);

        var selection = BundesligaClubEloPolicy.Select(
            seed,
            null,
            BundesligaClubEloSourceResult.Complete(sameRatedDate),
            unattendedNetworkUseAllowed: true);

        await Assert.That(selection.Selected).IsSameReferenceAs(seed);
        await Assert.That(selection.Disposition)
            .IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer);
        await Assert.That(selection.Diagnostics)
            .IsEquivalentTo(["NETWORK_RATED_AT_NOT_NEWER:2026-08-14"]);
    }

    private static BundesligaClubEloSnapshot Snapshot(
        DateOnly ratedAt,
        DateOnly collectedAt,
        BundesligaClubEloSnapshotOrigin origin)
    {
        return BundesligaClubEloSnapshot.Create(
            BundesligaClubEloSeed.Default.Entries,
            ratedAt,
            new DateTimeOffset(collectedAt, new TimeOnly(12, 0), TimeSpan.Zero),
            new Uri("https://clubelo.com/GER"),
            origin);
    }
}
