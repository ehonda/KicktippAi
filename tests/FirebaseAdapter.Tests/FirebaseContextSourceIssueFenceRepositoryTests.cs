using EHonda.KicktippAi.Core;
using System.Text.Json;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, "context-source-cycle-repository" })]
public class FirebaseContextSourceIssueFenceRepositoryTests(FirestoreFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private FirebaseContextSourceCycleRepository Cycles() => new(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>());
    private FirebaseContextSourceIssueFenceRepository Fences() => new(fixture.Db);
    private static BundesligaContextSourceOuterCycle Cycle(long run = 456, BundesligaContextSourceScope scope = BundesligaContextSourceScope.ProductionLive,
        BundesligaContextSource source = BundesligaContextSource.ClubElo) => new(scope == BundesligaContextSourceScope.ProductionLive
            ? BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, run)
            : BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000000"),
        Now, Now, scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers[0] : BundesligaContextSourceContract.DevelopmentLane,
        scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers,
        [source], BundesligaContextSourceCycleStatus.Claiming);
    private static BundesligaContextSourceIssueFenceAttempt Attempt(BundesligaContextSourceHealth health) => new(BundesligaContextSourceContract.NewClaimToken(),
        health.DesiredIssueProjection!.BodySha256, health.Watermark, health.DesiredIssueProjection.DesiredState, Now);
    private async Task<BundesligaContextSourceHealth> StartAsync(long run = 456)
    {
        var cycle = Cycle(run); await Cycles().CreateOrResumeCycleAsync(cycle);
        var reference = fixture.Db.Collection("context-source-health").Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.ClubElo));
        await reference.UpdateAsync(new Dictionary<string, object> { ["consecutiveFailures.acquisition"] = 2L, ["desiredIssueProjection.desiredState"] = "Open" });
        return (await Cycles().GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.ClubElo))!;
    }
    [Before(Test)]
    public async Task ClearAsync()
    {
        foreach (var name in new[] { "context-source-cycles", "context-source-cycle-observations", "context-source-cycle-receipts", "context-source-health", "context-source-issue-fences" })
            foreach (var document in (await fixture.Db.Collection(name).GetSnapshotAsync()).Documents) await document.Reference.DeleteAsync();
    }
    [Test]
    public async Task Concurrent_genesis_and_independent_arm_transactions_grant_exactly_once()
    {
        await Task.WhenAll(Cycles().CreateOrResumeCycleAsync(Cycle()), Cycles().CreateOrResumeCycleAsync(Cycle()));
        var health = await StartAsync();
        var before = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        await Assert.That(before.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Ready);
        var attempt = Attempt(health);
        var grants = await Task.WhenAll(Fences().TryArmCreateAsync(health, before.Revision, attempt), Fences().TryArmCreateAsync(health, before.Revision, attempt));
        await Assert.That(grants.Count(grant => grant is not null)).IsEqualTo(1);
        await Assert.That(await Fences().TryArmCreateAsync(health, before.Revision, attempt)).IsNull();
        var durable = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        await Assert.That(durable.State).IsEqualTo(BundesligaContextSourceIssueFenceState.CreateUncertain);
        await Assert.That(durable.Attempt).IsEqualTo(attempt);
    }
    [Test]
    public async Task Crash_before_send_and_new_watermark_never_rearm_but_old_intent_can_bind()
    {
        var health = await StartAsync(); var fence = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        var grant = (await Fences().TryArmCreateAsync(health, fence.Revision, Attempt(health)))!;
        // Abandon the grant, simulating committed arm followed by process death before POST.
        var newer = await StartAsync(457); var persisted = (await Fences().ReadOrInitializeAsync(newer)).Fence!;
        await Assert.That(persisted).IsEqualTo(grant.Fence);
        await Assert.That(await Fences().TryArmCreateAsync(newer, persisted.Revision, Attempt(newer))).IsNull();
        var bound = await Fences().BindObservedIssueAsync(grant.Fence, 5);
        await Assert.That(bound.Disposition).IsEqualTo(BundesligaContextSourceIssueFenceBindDisposition.Bound);
        await Assert.That((await Cycles().GetHealthAsync(newer.Competition, newer.Scope, newer.Source))!.DesiredIssueProjection!.SynchronizationStatus)
            .IsEqualTo(BundesligaContextSourceIssueSynchronization.Pending);
        await Assert.That((await Fences().BindObservedIssueAsync(bound.Fence, 6)).Disposition).IsEqualTo(BundesligaContextSourceIssueFenceBindDisposition.Conflict);
    }
    [Test]
    public async Task Legacy_health_is_uncertain_and_strict_schema_remains_unchanged()
    {
        var health = await StartAsync(); var identity = BundesligaContextSourceIssueFenceIdentity.FromHealth(health);
        await fixture.Db.Collection("context-source-issue-fences").Document(identity.StorageId).DeleteAsync();
        var read = await Fences().ReadOrInitializeAsync(health);
        await Assert.That(read.Fence!.State).IsEqualTo(BundesligaContextSourceIssueFenceState.LegacyUncertain);
        await Assert.That(await Fences().TryArmCreateAsync(health, 0, Attempt(health))).IsNull();
        await Assert.That(JsonSerializer.Serialize(await Cycles().GetHealthAsync(health.Competition, health.Scope, health.Source)))
            .IsEqualTo(JsonSerializer.Serialize(health));
        var bound = await Fences().BindObservedIssueAsync(read.Fence, 8);
        await Assert.That(bound.Fence.State).IsEqualTo(BundesligaContextSourceIssueFenceState.Bound);
    }
    [Test]
    public async Task Stale_health_synchronized_projection_and_stale_bind_revision_fail_closed()
    {
        var health = await StartAsync(); var fence = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        await StartAsync(457);
        await Assert.That((await Fences().ReadOrInitializeAsync(health)).CurrentPending).IsFalse();
        await Assert.That(await Fences().TryArmCreateAsync(health, fence.Revision, Attempt(health))).IsNull();
        var newer = (await Cycles().GetHealthAsync(health.Competition, health.Scope, health.Source))!;
        var armed = (await Fences().TryArmCreateAsync(newer, fence.Revision, Attempt(newer)))!;
        await Assert.That((await Fences().BindObservedIssueAsync(fence, 8)).Disposition).IsEqualTo(BundesligaContextSourceIssueFenceBindDisposition.Conflict);
        var desired = newer.DesiredIssueProjection!;
        await Cycles().UpdateIssueProjectionAsync(newer, desired with { SynchronizationStatus = BundesligaContextSourceIssueSynchronization.Synchronized,
            AppliedBodySha256 = desired.BodySha256, LastAttemptedAtUtc = Now });
        await Assert.That((await Fences().ReadOrInitializeAsync(newer)).CurrentPending).IsFalse();
        await Assert.That((await Fences().BindObservedIssueAsync(armed.Fence, 8)).Disposition).IsEqualTo(BundesligaContextSourceIssueFenceBindDisposition.Bound);
    }
    [Test]
    [Arguments("unknown")]
    [Arguments("revision-double")]
    [Arguments("state-numeric")]
    [Arguments("partial-attempt")]
    [Arguments("bound-zero")]
    [Arguments("wrong-marker")]
    public async Task Strict_fence_maps_reject_invalid_types_fields_and_state_combinations(string mutation)
    {
        var health = await StartAsync(); var fence = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        var map = (await fixture.Db.Collection("context-source-issue-fences").Document(fence.Identity.StorageId).GetSnapshotAsync()).ToDictionary();
        switch (mutation)
        {
            case "unknown": map["extra"] = true; break;
            case "revision-double": map["revision"] = 0d; break;
            case "state-numeric": map["state"] = "0"; break;
            case "partial-attempt": map["attemptToken"] = BundesligaContextSourceContract.NewClaimToken(); break;
            case "bound-zero": map["state"] = "Bound"; map["issueNumber"] = 0L; break;
            case "wrong-marker": map["marker"] = "foreign"; break;
        }
        await fixture.Db.Collection("context-source-issue-fences").Document(fence.Identity.StorageId).SetAsync(map);
        await Assert.That(() => Fences().ReadOrInitializeAsync(health)).Throws<InvalidDataException>();
    }
    [Test]
    public async Task Development_and_roster_cycles_create_no_fence_and_existing_fence_survives_aborts()
    {
        await Cycles().CreateOrResumeCycleAsync(Cycle(scope: BundesligaContextSourceScope.Development));
        await Cycles().CreateOrResumeCycleAsync(Cycle(source: BundesligaContextSource.Rosters));
        await Assert.That((await fixture.Db.Collection("context-source-issue-fences").GetSnapshotAsync()).Documents).IsEmpty();
        var health = await StartAsync(457); var before = (await Fences().ReadOrInitializeAsync(health)).Fence!;
        await Cycles().AbortCycleAsync(Cycle(457).Identity, BundesligaContextSourceError.LocalHandoffMissing);
        var afterHealth = (await Cycles().GetHealthAsync(health.Competition, health.Scope, health.Source))!;
        await Assert.That((await Fences().ReadOrInitializeAsync(afterHealth)).Fence).IsEqualTo(before);
    }
}
