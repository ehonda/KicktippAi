using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;

namespace FirebaseAdapter;

public sealed class FirebaseContextSourceIssueFenceRepository(FirestoreDb db, TimeProvider? timeProvider = null)
    : IBundesligaContextSourceIssueFenceRepository
{
    internal const string Collection = "context-source-issue-fences";
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private DateTimeOffset Now() { var value = _time.GetUtcNow(); return new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero); }
    private DocumentReference Reference(BundesligaContextSourceIssueFenceIdentity identity) => db.Collection(Collection).Document(identity.StorageId);
    private DocumentReference HealthReference(BundesligaContextSourceHealth health) => db.Collection(FirebaseContextSourceCycleRepository.Health)
        .Document(BundesligaContextSourceHashing.HealthStorageId(health.Competition, BundesligaContextSourceContract.ScopeValue(health.Scope), health.Source));

    private static bool Current(DocumentSnapshot snapshot, BundesligaContextSourceHealth expected)
    {
        if (!snapshot.Exists) return false;
        var identity = BundesligaContextSourceCycleIdentity.Create(expected.Competition, expected.Scope, expected.Watermark.CycleId, expected.Watermark.Sequence);
        var current = FirebaseContextSourceCycleRepository.ParseHealth(snapshot, identity, expected.Source);
        return current.Watermark == expected.Watermark && current.LastCompletedCycleId == expected.LastCompletedCycleId
            && current.DesiredIssueProjection == expected.DesiredIssueProjection
            && current.DesiredIssueProjection?.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Pending;
    }

    public Task<BundesligaContextSourceIssueFenceRead> ReadOrInitializeAsync(BundesligaContextSourceHealth expectedHealth, CancellationToken cancellationToken = default)
    {
        var identity = BundesligaContextSourceIssueFenceIdentity.FromHealth(expectedHealth);
        return db.RunTransactionAsync(async transaction =>
        {
            var health = await transaction.GetSnapshotAsync(HealthReference(expectedHealth));
            var reference = Reference(identity); var snapshot = await transaction.GetSnapshotAsync(reference);
            var current = Current(health, expectedHealth);
            var fence = snapshot.Exists ? ContextSourceIssueFenceFirestoreModels.Parse(snapshot, identity) : null;
            if (fence is null && current)
            {
                fence = new(identity, 0, BundesligaContextSourceIssueFenceState.LegacyUncertain, null, null, Now());
                transaction.Create(reference, ContextSourceIssueFenceFirestoreModels.ToMap(fence));
            }
            return new BundesligaContextSourceIssueFenceRead(current, fence);
        }, cancellationToken: cancellationToken);
    }

    public async Task<BundesligaContextSourceIssueCreateGrant?> TryArmCreateAsync(BundesligaContextSourceHealth expectedHealth, long expectedRevision,
        BundesligaContextSourceIssueFenceAttempt attempt, CancellationToken cancellationToken = default)
    {
        var identity = BundesligaContextSourceIssueFenceIdentity.FromHealth(expectedHealth); attempt.Validate();
        var desired = expectedHealth.DesiredIssueProjection;
        if (desired is null || desired.DesiredState != BundesligaContextSourceIssueState.Open || attempt.Watermark != expectedHealth.Watermark
            || attempt.BodySha256 != desired.BodySha256 || attempt.DesiredState != desired.DesiredState) throw new InvalidDataException("ISSUE_FENCE_ATTEMPT_MISMATCH");
        // Return the final callback result only after RunTransactionAsync acknowledges the commit.
        var armed = await db.RunTransactionAsync(async transaction =>
        {
            var health = await transaction.GetSnapshotAsync(HealthReference(expectedHealth));
            var reference = Reference(identity); var snapshot = await transaction.GetSnapshotAsync(reference);
            if (!Current(health, expectedHealth) || !snapshot.Exists) return null;
            var fence = ContextSourceIssueFenceFirestoreModels.Parse(snapshot, identity);
            if (fence.Revision != expectedRevision || fence.State != BundesligaContextSourceIssueFenceState.Ready) return null;
            var next = fence with { Revision = checked(fence.Revision + 1), State = BundesligaContextSourceIssueFenceState.CreateUncertain,
                Attempt = attempt, UpdatedAtUtc = attempt.StartedAtUtc };
            transaction.Set(reference, ContextSourceIssueFenceFirestoreModels.ToMap(next)); return next;
        }, cancellationToken: cancellationToken);
        return armed is null ? null : new BundesligaContextSourceIssueCreateGrant(armed);
    }

    public Task<BundesligaContextSourceIssueFenceBindResult> BindObservedIssueAsync(BundesligaContextSourceIssueFence expectedFence,
        long verifiedIssueNumber, CancellationToken cancellationToken = default)
    {
        expectedFence.Validate(); if (verifiedIssueNumber <= 0) throw new InvalidDataException("ISSUE_FENCE_NUMBER_INVALID");
        return db.RunTransactionAsync(async transaction =>
        {
            var reference = Reference(expectedFence.Identity);
            var fence = ContextSourceIssueFenceFirestoreModels.Parse(await transaction.GetSnapshotAsync(reference), expectedFence.Identity);
            if (fence.State == BundesligaContextSourceIssueFenceState.Bound)
                return new BundesligaContextSourceIssueFenceBindResult(fence.IssueNumber == verifiedIssueNumber
                    ? BundesligaContextSourceIssueFenceBindDisposition.Bound : BundesligaContextSourceIssueFenceBindDisposition.Conflict, fence);
            if (fence.Revision != expectedFence.Revision || fence.Attempt != expectedFence.Attempt)
                return new BundesligaContextSourceIssueFenceBindResult(BundesligaContextSourceIssueFenceBindDisposition.Conflict, fence);
            var next = fence with { Revision = checked(fence.Revision + 1), State = BundesligaContextSourceIssueFenceState.Bound,
                IssueNumber = verifiedIssueNumber, UpdatedAtUtc = Now() };
            transaction.Set(reference, ContextSourceIssueFenceFirestoreModels.ToMap(next));
            return new BundesligaContextSourceIssueFenceBindResult(BundesligaContextSourceIssueFenceBindDisposition.Bound, next);
        }, cancellationToken: cancellationToken);
    }

    // All reads belong to the caller's transaction and must occur before its first write.
    internal static async Task<DocumentSnapshot?> ReadGenesisAsync(FirestoreDb db, Transaction transaction,
        BundesligaContextSourceOuterCycle cycle, IReadOnlyDictionary<BundesligaContextSource, DocumentSnapshot> health)
    {
        if (cycle.Identity.Scope != BundesligaContextSourceScope.ProductionLive || !cycle.EnabledSources.Contains(BundesligaContextSource.ClubElo)
            || health[BundesligaContextSource.ClubElo].Exists) return null;
        var identity = new BundesligaContextSourceIssueFenceIdentity(BundesligaContextSourceIssueFenceIdentity.CanonicalRepository,
            cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.ClubElo, BundesligaContextSourceIssueFenceIdentity.CanonicalMarker);
        var snapshot = await transaction.GetSnapshotAsync(db.Collection(Collection).Document(identity.StorageId));
        if (snapshot.Exists) _ = ContextSourceIssueFenceFirestoreModels.Parse(snapshot, identity);
        return snapshot;
    }
    internal static void WriteGenesis(Transaction transaction, DocumentSnapshot? snapshot, BundesligaContextSourceOuterCycle cycle)
    {
        if (snapshot is null || snapshot.Exists) return;
        var identity = new BundesligaContextSourceIssueFenceIdentity(BundesligaContextSourceIssueFenceIdentity.CanonicalRepository,
            cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.ClubElo, BundesligaContextSourceIssueFenceIdentity.CanonicalMarker);
        transaction.Create(snapshot.Reference, ContextSourceIssueFenceFirestoreModels.ToMap(new(identity, 0,
            BundesligaContextSourceIssueFenceState.Ready, null, null, cycle.StartedAtUtc)));
    }
}
