using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaContextSourceIssueFenceContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static BundesligaContextSourceIssueFenceIdentity Identity() => new("ehonda/KicktippAi", BundesligaContextSourceContract.Competition,
        BundesligaContextSourceScope.ProductionLive, BundesligaContextSource.ClubElo, BundesligaContextSourceIssueFenceIdentity.CanonicalMarker);
    [Test]
    public async Task Fence_identity_is_marker_wide_and_rejects_other_authorities()
    {
        var identity = Identity(); identity.Validate();
        await Assert.That(identity.StorageId).IsEqualTo(BundesligaContextSourceHashing.HashFields("context-source-issue-fence-storage/v1",
            "ehonda/KicktippAi", "bundesliga-2026-27", "production-live", "club-elo", identity.Marker));
        foreach (var invalid in new[] { identity with { Repository = "other/repo" }, identity with { Scope = BundesligaContextSourceScope.Development },
            identity with { Source = BundesligaContextSource.Rosters }, identity with { Marker = identity.Marker + " " } })
            await Assert.That(() => invalid.Validate()).Throws<InvalidDataException>();
    }
    [Test]
    public async Task Strict_state_matrix_and_one_use_grant()
    {
        var attempt = new BundesligaContextSourceIssueFenceAttempt("11111111-1111-4111-8111-111111111111", new string('a', 64),
            new(456, "gha:123:456"), BundesligaContextSourceIssueState.Open, Now);
        var ready = new BundesligaContextSourceIssueFence(Identity(), 0, BundesligaContextSourceIssueFenceState.Ready, null, null, Now);
        foreach (var valid in new[] { ready, ready with { State = BundesligaContextSourceIssueFenceState.LegacyUncertain },
            ready with { State = BundesligaContextSourceIssueFenceState.CreateUncertain, Attempt = attempt },
            ready with { State = BundesligaContextSourceIssueFenceState.Bound, IssueNumber = 5 },
            ready with { State = BundesligaContextSourceIssueFenceState.Bound, IssueNumber = 5, Attempt = attempt } }) valid.Validate();
        foreach (var invalid in new[] { ready with { Revision = -1 }, ready with { State = (BundesligaContextSourceIssueFenceState)99 },
            ready with { Attempt = attempt }, ready with { IssueNumber = 5 }, ready with { State = BundesligaContextSourceIssueFenceState.CreateUncertain },
            ready with { State = BundesligaContextSourceIssueFenceState.Bound, IssueNumber = 0 } })
            await Assert.That(() => invalid.Validate()).Throws<InvalidDataException>();
        var grant = new BundesligaContextSourceIssueCreateGrant(ready with { State = BundesligaContextSourceIssueFenceState.CreateUncertain, Attempt = attempt });
        await Assert.That(grant.TryConsume()).IsTrue(); await Assert.That(grant.TryConsume()).IsFalse();
    }
}
