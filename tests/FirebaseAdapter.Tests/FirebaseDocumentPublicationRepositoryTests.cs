using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, "context-source-cycle-repository" })]
public sealed class FirebaseDocumentPublicationRepositoryTests(FirestoreFixture fixture)
{
    private string Community { get; } = $"publication-{Guid.NewGuid():N}";
    private static readonly DocumentPublicationDefinition Definition = new(
        "fixture-publication",
        [
            new DocumentPublicationKey(DocumentPublicationKind.Context, "fixture-context"),
            new DocumentPublicationKey(DocumentPublicationKind.Kpi, "fixture-kpi")
        ]);

    [Before(Test)]
    public async Task ClearAsync()
    {
        await Task.WhenAll(
            fixture.ClearDocumentPublicationsAsync(),
            fixture.ClearContextDocumentsAsync(),
            fixture.ClearKpiDocumentsAsync());
    }

    // The class holds both publication-payload and source-cycle locks while clearing shared fixture state.
    private async Task ClearContextSourceStateForGuardedTestAsync()
    {
        foreach (var collection in new[] { "context-source-cycles", "context-source-cycle-observations", "context-source-cycle-receipts", "context-source-health" })
        {
            var snapshot = await fixture.Db.Collection(collection).GetSnapshotAsync();
            foreach (var document in snapshot.Documents) await document.Reference.DeleteAsync();
        }
    }

    [Test]
    public async Task Initial_mixed_publish_creates_one_headed_snapshot_and_exact_read()
    {
        var repository = CreateRepository();
        var result = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));
        var loaded = await repository.GetLastKnownGoodAsync(Definition, Community);

        await Assert.That(result.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(result.Snapshot.Documents.Length).IsEqualTo(2);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Documents.Select(document => document.Content)).Contains("context-v1");
        await Assert.That(loaded.Documents.Select(document => document.Content)).Contains("kpi-v1");
    }

    [Test]
    public async Task Unchanged_and_stale_cas_are_decided_before_writes()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-v1", "kpi-v1")))
            .Throws<DocumentPublicationConcurrencyException>();
        var unchanged = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1", first.Snapshot.SnapshotId));

        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(unchanged.Snapshot.SnapshotId).IsEqualTo(first.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Retriable_publication_boundary_rejects_noncanonical_caller_order_before_any_write()
    {
        var repository = CreateRepository();
        var ordered = Request("context", "kpi");
        var hostile = new DocumentPublicationRequest(Community, null, ordered.Documents.Reverse(), "{\"fixture\":true}");

        await Assert.That(() => repository.PublishAsync(Definition, hostile)).Throws<ArgumentException>();
        await Assert.That(await repository.GetLastKnownGoodAsync(Definition, Community)).IsNull();
    }

    [Test]
    public async Task Guarded_publication_is_atomic_replays_the_original_receipt_after_head_movement_and_rejects_semantic_or_byte_drift()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var request = RosterRequest(prepared.Commit, expected: null, contentTag: "first");

        var published = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, request);
        var receipt = await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, BundesligaContextSource.Rosters, prepared.Guard.ConsumerLaneId);
        var health = await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source);
        await Assert.That(published.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(receipt!.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
        await Assert.That(health!.LastCompletedCycleId).IsEqualTo(prepared.Guard.CycleId);

        var moved = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(commit: null, expected: published.Snapshot.SnapshotId, contentTag: "moved"));
        var replay = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, request);
        await Assert.That(replay.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(replay.Snapshot.SnapshotId).IsEqualTo(published.Snapshot.SnapshotId);
        await Assert.That((await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId))!.RecordedAtUtc)
            .IsEqualTo(receipt.RecordedAtUtc);

        var semanticMismatch = new DocumentPublicationRequest(request.CommunityContext, request.ExpectedPreviousSnapshotId,
            request.Documents, request.MetadataJson, new ContextSourcePublicationCommitRequest(prepared.Guard,
                prepared.Commit.ReceiptTemplate with { ActiveConditions = [] }));
        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters, semanticMismatch)).Throws<InvalidDataException>();
        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(prepared.Commit, null, "different-bytes"))).Throws<InvalidDataException>();
        await Assert.That((await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, BundesligaContextSourceContract.DevelopmentCommunity))!.Snapshot.SnapshotId)
            .IsEqualTo(moved.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Guarded_absent_receipt_allows_only_exact_head_unchanged_and_rejects_misleading_current_target_shapes()
    {
        var publication = CreateRepository();
        var unchangedPrepared = await PrepareGuardedRosterCycleAsync(eligibleRoster: true);
        var legacy = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(null, null, "unchanged"));
        var persistedLegacy = await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters,
            BundesligaContextSourceContract.DevelopmentCommunity);
        await Assert.That(persistedLegacy).IsNotNull();
        var unchanged = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(unchangedPrepared.Commit, legacy.Snapshot.SnapshotId, "unchanged"));
        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(unchanged.Snapshot.CreatedAt).IsEqualTo(persistedLegacy!.Snapshot.CreatedAt);
        await Assert.That(unchanged.Snapshot.Documents).IsEquivalentTo(legacy.Snapshot.Documents);
        await Assert.That((await unchangedPrepared.Cycles.GetReceiptAsync(unchangedPrepared.Guard.Identity, unchangedPrepared.Guard.Source, unchangedPrepared.Guard.ConsumerLaneId))!.Request.PublicationDisposition)
            .IsEqualTo(BundesligaContextSourcePublicationDisposition.Unchanged);

        var mismatchPrepared = await PrepareGuardedRosterCycleAsync();
        var a = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, unchanged.Snapshot.SnapshotId, "a"));
        var target = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, a.Snapshot.SnapshotId, "target"));
        var b = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, target.Snapshot.SnapshotId, "b"));
        var reactivatedTarget = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, b.Snapshot.SnapshotId, "target"));
        await Assert.That(reactivatedTarget.Disposition).IsEqualTo(DocumentPublicationDisposition.Reactivated);
        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(mismatchPrepared.Commit, a.Snapshot.SnapshotId, "target"))).Throws<DocumentPublicationConcurrencyException>();
        await Assert.That(await mismatchPrepared.Cycles.GetReceiptAsync(mismatchPrepared.Guard.Identity, mismatchPrepared.Guard.Source, mismatchPrepared.Guard.ConsumerLaneId)).IsNull();
        await Assert.That((await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, BundesligaContextSourceContract.DevelopmentCommunity))!.Snapshot.SnapshotId)
            .IsEqualTo(target.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Guarded_historical_reactivation_commits_the_real_source_cycle_once()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var first = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, null, "first"));
        var historical = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, first.Snapshot.SnapshotId, "historical"));
        var current = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(null, historical.Snapshot.SnapshotId, "current"));

        var reactivated = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(prepared.Commit, current.Snapshot.SnapshotId, "historical"));
        var receipt = await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId);
        var source = await prepared.Cycles.GetSourceCycleAsync(prepared.Guard.Identity, prepared.Guard.Source);
        var health = await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source);

        await Assert.That(reactivated.Disposition).IsEqualTo(DocumentPublicationDisposition.Reactivated);
        await Assert.That(reactivated.Snapshot.SnapshotId).IsEqualTo(historical.Snapshot.SnapshotId);
        await Assert.That(receipt!.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Reactivated);
        await Assert.That(source!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That(health!.LastCompletedCycleId).IsEqualTo(prepared.Guard.CycleId);
        var replay = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(prepared.Commit, current.Snapshot.SnapshotId, "historical"));
        await Assert.That(replay.Snapshot.SnapshotId).IsEqualTo(historical.Snapshot.SnapshotId);
        await Assert.That((await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId))!.RecordedAtUtc)
            .IsEqualTo(receipt.RecordedAtUtc);
    }

    [Test]
    public async Task Guarded_publication_freezes_mutable_template_conditions_before_the_transaction_can_observe_them()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var mutableConditions = prepared.Commit.ReceiptTemplate.ActiveConditions.ToList();
        var commit = new ContextSourcePublicationCommitRequest(prepared.Guard,
            prepared.Commit.ReceiptTemplate with { ActiveConditions = mutableConditions });
        var publish = publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(commit, null, "frozen"));
        mutableConditions.Clear();

        await publish;
        var receipt = await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId);
        await Assert.That(receipt!.Request.ActiveConditions).IsEquivalentTo(prepared.Commit.ReceiptTemplate.ActiveConditions);
    }

    [Test]
    public async Task Guard_and_prefix_validation_precede_head_comparison_and_are_mutation_free()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var badGuard = prepared.Guard with { BundleDigest = new string('f', 64) };
        var badCommit = new ContextSourcePublicationCommitRequest(badGuard, prepared.Commit.ReceiptTemplate);

        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(badCommit, new string('a', 64), "guard-failure"))).Throws<InvalidDataException>();
        await Assert.That(await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId)).IsNull();
        await Assert.That(await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, BundesligaContextSourceContract.DevelopmentCommunity)).IsNull();
    }

    [Test]
    public async Task Guarded_boundary_accepts_a_deterministic_unavailable_identity_reason()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync(unavailableIdentity: true);

        var published = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(prepared.Commit, null, "identity-unavailable"));
        await Assert.That(published.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That((await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId))!.Request.ActiveConditions)
            .Contains(BundesligaContextSourceHealthCondition.AcquisitionFailed);
    }

    [Test]
    [Arguments("new")]
    [Arguments("pending")]
    [Arguments("policy")]
    [Arguments("accepted-null")]
    public async Task Guarded_boundary_accepts_each_valid_unavailable_identity_reason(string state)
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync(unavailableIdentity: true, unavailableIdentityState: state);
        var result = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(prepared.Commit, null, $"identity-{state}"));
        await Assert.That(result.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That((await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId))!
            .Request.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.AcquisitionFailed);
    }

    [Test]
    public async Task Guarded_production_lanes_write_each_receipt_but_reduce_only_with_the_final_lane_and_exact_replay()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedProductionRosterCycleAsync();
        var commits = BundesligaContextSourceContract.ProductionConsumers
            .Select(lane => ProductionGuardedCommit(prepared, lane)).ToArray();
        var initialHealth = (await prepared.Cycles.GetHealthAsync(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope, BundesligaContextSource.Rosters))!;
        var latestSnapshotIdByCommunity = new Dictionary<string, string?>();

        async Task<(DocumentPublicationRequest Request, DocumentPublicationResult Result)> PublishLaneAsync(ContextSourcePublicationCommitRequest commit, string contentTag)
        {
            latestSnapshotIdByCommunity.TryGetValue(commit.Guard.CommunityContext, out var expectedHead);
            var request = ProductionRosterRequest(commit, expectedHead, contentTag);
            var result = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, request);
            latestSnapshotIdByCommunity[commit.Guard.CommunityContext] = result.Snapshot.SnapshotId;
            return (request, result);
        }

        var (firstRequest, first) = await PublishLaneAsync(commits[0], "first");
        var afterFirstSource = await prepared.Cycles.GetSourceCycleAsync(prepared.Outer.Identity, BundesligaContextSource.Rosters);
        var afterFirstHealth = await prepared.Cycles.GetHealthAsync(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(afterFirstSource!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
        await Assert.That(afterFirstSource.ReceivedConsumers).IsEquivalentTo([commits[0].Guard.ConsumerLaneId]);
        await Assert.That(afterFirstHealth).IsEquivalentTo(initialHealth);
        await Assert.That((await prepared.Cycles.GetCycleAsync(prepared.Outer.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);

        foreach (var commit in commits.Skip(1).Take(commits.Length - 2))
            await PublishLaneAsync(commit, commit.Guard.ConsumerLaneId);
        var beforeFinalHealth = await prepared.Cycles.GetHealthAsync(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope, BundesligaContextSource.Rosters);
        var final = commits[^1];
        var (finalRequest, completed) = await PublishLaneAsync(final, "final");

        await Assert.That((await prepared.Cycles.GetSourceCycleAsync(prepared.Outer.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That((await prepared.Cycles.GetCycleAsync(prepared.Outer.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        var reduced = (await prepared.Cycles.GetHealthAsync(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope, BundesligaContextSource.Rosters))!;
        await Assert.That(reduced.LastCompletedCycleId).IsEqualTo(prepared.Outer.Identity.CycleId);
        await Assert.That(reduced).IsNotEquivalentTo(beforeFinalHealth);
        await Assert.That((await publication.PublishAsync(BundesligaDocumentPublication.Rosters, firstRequest)).Snapshot.SnapshotId)
            .IsEqualTo(first.Snapshot.SnapshotId);
        await Assert.That((await publication.PublishAsync(BundesligaDocumentPublication.Rosters, finalRequest)).Snapshot.SnapshotId)
            .IsEqualTo(completed.Snapshot.SnapshotId);
        await Assert.That((await prepared.Cycles.GetHealthAsync(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope, BundesligaContextSource.Rosters))!)
            .IsEquivalentTo(reduced);
    }

    [Test]
    [Arguments("superseded")]
    [Arguments("strict-prefix")]
    [Arguments("observation-digest")]
    [Arguments("observation-identity")]
    [Arguments("watermark")]
    [Arguments("bundle-digest")]
    public async Task Guarded_validation_failures_precede_head_comparison_and_leave_the_publication_and_cycle_unwritten(string mutation)
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var commit = prepared.Commit;
        if (mutation == "superseded")
            await fixture.Db.Collection("context-source-cycles").Document(prepared.Guard.Identity.StorageId)
                .UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "SUPERSEDED_INCOMPLETE_CYCLE" });
        else if (mutation == "strict-prefix")
            await fixture.Db.Collection("context-source-cycle-observations").Document(BundesligaContextSourceHashing.SourceCycleStorageId(prepared.Guard.Identity, prepared.Guard.Source))
                .UpdateAsync("receivedConsumers", new[] { prepared.Guard.ConsumerLaneId });
        else if (mutation == "observation-digest")
            commit = new ContextSourcePublicationCommitRequest(prepared.Guard with { ObservationDigest = new string('d', 64) }, prepared.Commit.ReceiptTemplate);
        else if (mutation == "observation-identity")
            await fixture.Db.Collection("context-source-cycle-observations").Document(BundesligaContextSourceHashing.SourceCycleStorageId(prepared.Guard.Identity, prepared.Guard.Source))
                .UpdateAsync("observation.attemptId", "0000000000000000000000000000000000000000000000000000000000000000");
        else if (mutation == "watermark")
            commit = new ContextSourcePublicationCommitRequest(prepared.Guard with { WatermarkSequence = prepared.Guard.WatermarkSequence + 1 }, prepared.Commit.ReceiptTemplate);
        else if (mutation == "bundle-digest")
            commit = new ContextSourcePublicationCommitRequest(prepared.Guard with { BundleDigest = new string('d', 64) }, prepared.Commit.ReceiptTemplate);
        else throw new ArgumentOutOfRangeException(nameof(mutation));

        var beforeDocuments = (await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Count;
        var beforeSnapshots = (await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Count;
        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(commit, new string('a', 64), mutation))).Throws<InvalidDataException>();
        await Assert.That(await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, prepared.Guard.CommunityContext)).IsNull();
        await Assert.That((await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Count).IsEqualTo(beforeDocuments);
        await Assert.That((await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Count).IsEqualTo(beforeSnapshots);
        await Assert.That(await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId)).IsNull();
    }

    [Test]
    [Arguments("valid")]
    [Arguments("reversed")]
    [Arguments("duplicate")]
    [Arguments("invalid-primary")]
    [Arguments("unknown")]
    public async Task Guarded_diagnostic_precedence_validates_before_a_stale_head_and_leaves_all_persisted_state_unchanged(string diagnosticCase)
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync(schemaRejectedRoster: true);
        var sourceReference = fixture.Db.Collection("context-source-cycle-observations")
            .Document(BundesligaContextSourceHashing.SourceCycleStorageId(prepared.Guard.Identity, prepared.Guard.Source));
        if (diagnosticCase != "valid")
        {
            var diagnostics = diagnosticCase switch
            {
                "reversed" => new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "ROSTER_MEMBERSHIP_REJECTED", "UNKNOWN_SOURCE_DATE" },
                "duplicate" => new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "ROSTER_DUCKDB_SCHEMA_REJECTED" },
                "invalid-primary" => new[] { "ROSTER_MEMBERSHIP_REJECTED" },
                "unknown" => new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "UNKNOWN_CODE" },
                _ => throw new ArgumentOutOfRangeException(nameof(diagnosticCase))
            };
            await sourceReference.UpdateAsync("observation.diagnostics", diagnostics);
        }

        var baseline = await publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(null, null, $"diagnostic-{diagnosticCase}-baseline"));
        var beforeHead = await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters,
            prepared.Guard.CommunityContext);
        var beforeDocuments = (await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal);
        var beforeKpis = (await fixture.Db.Collection("kpi-documents").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal);
        var beforeSnapshots = (await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal);
        var beforeSource = (await sourceReference.GetSnapshotAsync()).ToDictionary();
        var beforeHealth = await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source);
        const string staleExpectedHead = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        await Assert.That(beforeHead).IsNotNull();
        await Assert.That(baseline.Snapshot.SnapshotId).IsNotEqualTo(staleExpectedHead);
        if (diagnosticCase == "valid")
            await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
                RosterRequest(prepared.Commit, staleExpectedHead, $"diagnostic-{diagnosticCase}"))).Throws<DocumentPublicationConcurrencyException>();
        else
            await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
                RosterRequest(prepared.Commit, staleExpectedHead, $"diagnostic-{diagnosticCase}"))).Throws<InvalidDataException>();

        await Assert.That((await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters,
            prepared.Guard.CommunityContext))!.Snapshot).IsEquivalentTo(beforeHead!.Snapshot);
        await Assert.That((await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal)).IsEquivalentTo(beforeDocuments);
        await Assert.That((await fixture.Db.Collection("kpi-documents").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal)).IsEquivalentTo(beforeKpis);
        await Assert.That((await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Documents
            .ToDictionary(document => document.Id, document => document.ToDictionary(), StringComparer.Ordinal)).IsEquivalentTo(beforeSnapshots);
        await Assert.That((await sourceReference.GetSnapshotAsync()).ToDictionary()).IsEquivalentTo(beforeSource);
        await Assert.That((await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source))!)
            .IsEquivalentTo(beforeHealth);
        await Assert.That(await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId)).IsNull();
    }

    [Test]
    public async Task Guarded_payload_collision_is_atomic_and_a_later_exact_commit_replays()
    {
        var publication = CreateRepository();
        var prepared = await PrepareGuardedRosterCycleAsync();
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, prepared.Guard.CommunityContext, BundesligaDocumentPublication.RosterPublicationSet);
        var key = BundesligaDocumentPublication.Rosters.RequiredDocuments.Single(value => value.Kind == DocumentPublicationKind.Context && value.Name == "roster-b04");
        var collision = fixture.Db.Collection("context-documents").Document(PublicationPayloadId(scope, key.Name, 0));
        await collision.SetAsync(new Dictionary<string, object> { ["collision"] = true });
        try
        {
            var beforeSource = await prepared.Cycles.GetSourceCycleAsync(prepared.Guard.Identity, prepared.Guard.Source);
            var beforeHealth = await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source);

            await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(prepared.Commit, null, "collision"))).Throws<Exception>();
            await Assert.That(await publication.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, prepared.Guard.CommunityContext)).IsNull();
            await Assert.That(await prepared.Cycles.GetReceiptAsync(prepared.Guard.Identity, prepared.Guard.Source, prepared.Guard.ConsumerLaneId)).IsNull();
            await Assert.That((await prepared.Cycles.GetSourceCycleAsync(prepared.Guard.Identity, prepared.Guard.Source))!
                .CreateCanonicalUtf8(BundesligaContextSourceContract.DevelopmentConsumers)
                .SequenceEqual(beforeSource!.CreateCanonicalUtf8(BundesligaContextSourceContract.DevelopmentConsumers))).IsTrue();
            await Assert.That((await prepared.Cycles.GetHealthAsync(prepared.Guard.Competition, prepared.Guard.Scope, prepared.Guard.Source))!)
                .IsEquivalentTo(beforeHealth);
        }
        finally
        {
            await collision.DeleteAsync();
        }

        var committed = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(prepared.Commit, null, "collision"));
        var replay = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, RosterRequest(prepared.Commit, null, "collision"));
        await Assert.That(replay.Snapshot.SnapshotId).IsEqualTo(committed.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Guarded_metadata_unchanged_rejects_a_prior_receipt_with_a_different_valid_bundle_digest_before_any_write()
    {
        var publication = CreateRepository();
        var prior = await PrepareGuardedRosterCycleAsync(cycleId: "0198f865-1467-7000-8000-000000000001");
        var priorRequest = RosterRequest(prior.Commit, null, "authoritative-prior");
        var published = await publication.PublishAsync(BundesligaDocumentPublication.Rosters, priorRequest);
        var priorReceiptReference = fixture.Db.Collection("context-source-cycle-receipts")
            .Document(BundesligaContextSourceHashing.ReceiptStorageId(prior.Guard.Identity, prior.Guard.Source, prior.Guard.ConsumerLaneId));
        await priorReceiptReference.UpdateAsync("bundleDigest", new string('d', 64));

        var current = await PrepareGuardedRosterCycleAsync(metadataUnchanged: true, clearContextSourceState: false,
            cycleId: "0198f865-1468-7000-8000-000000000002");
        var beforeDocuments = (await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Count;
        var beforeSnapshots = (await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Count;
        var beforeHealth = await current.Cycles.GetHealthAsync(current.Guard.Competition, current.Guard.Scope, current.Guard.Source);

        await Assert.That(() => publication.PublishAsync(BundesligaDocumentPublication.Rosters,
            RosterRequest(current.Commit, published.Snapshot.SnapshotId, "authoritative-prior"))).Throws<InvalidDataException>();

        await Assert.That(await current.Cycles.GetReceiptAsync(current.Guard.Identity, current.Guard.Source, current.Guard.ConsumerLaneId)).IsNull();
        await Assert.That((await fixture.Db.Collection("context-documents").GetSnapshotAsync()).Count).IsEqualTo(beforeDocuments);
        await Assert.That((await fixture.Db.Collection("document-publication-snapshots").GetSnapshotAsync()).Count).IsEqualTo(beforeSnapshots);
        await Assert.That((await current.Cycles.GetHealthAsync(current.Guard.Competition, current.Guard.Scope, current.Guard.Source))!)
            .IsEquivalentTo(beforeHealth);
    }

    [Test]
    public async Task Changed_document_increments_while_unchanged_headed_document_reuses_its_version()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));
        var second = await repository.PublishAsync(Definition, Request("context-v2", "kpi-v1", first.Snapshot.SnapshotId));

        await Assert.That(second.Snapshot.Documents.Single(entry => entry.Name == "fixture-context").Version).IsEqualTo(1);
        await Assert.That(second.Snapshot.Documents.Single(entry => entry.Name == "fixture-kpi").Version).IsEqualTo(0);
    }

    [Test]
    public async Task Changed_document_allocates_above_an_unheaded_existing_maximum_version()
    {
        var repository = CreateRepository();
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var prefix = DocumentPublicationContract.ComputeHeadId(scope);
        for (var version = 0; version < 2; version++)
        {
            await fixture.Db.Collection("context-documents").Document($"{prefix}_fixture-context_{version}").SetAsync(
                new FirestoreContextDocument
                {
                    Competition = scope.Competition,
                    CommunityContext = scope.CommunityContext,
                    PublicationSet = scope.PublicationSet,
                    DocumentName = "fixture-context",
                    Content = $"unheaded-{version}",
                    Version = version,
                    CreatedAt = Timestamp.GetCurrentTimestamp()
                });
        }

        var published = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));

        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Name == "fixture-context").Version).IsEqualTo(2);
    }

    [Test]
    public async Task A_to_b_to_a_reactivation_preserves_immutable_ancestry_entries_and_timestamp()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var second = await repository.PublishAsync(Definition, Request("context-b", "kpi-b", first.Snapshot.SnapshotId));
        var reactivated = await repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId));

        await Assert.That(reactivated.Disposition).IsEqualTo(DocumentPublicationDisposition.Reactivated);
        await Assert.That(reactivated.Snapshot.SnapshotId).IsEqualTo(first.Snapshot.SnapshotId);
        await Assert.That(reactivated.Snapshot.PreviousSnapshotId).IsNull();
        await Assert.That(reactivated.Snapshot.CreatedAt.ToUnixTimeMilliseconds())
            .IsEqualTo(first.Snapshot.CreatedAt.ToUnixTimeMilliseconds());
        await Assert.That(reactivated.Snapshot.Documents).IsEquivalentTo(first.Snapshot.Documents);
    }

    [Test]
    public async Task Current_and_reactivation_misfiled_metadata_fail_closed_without_moving_the_head()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var second = await repository.PublishAsync(Definition, Request("context-b", "kpi-b", first.Snapshot.SnapshotId));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);

        await CorruptStoredSnapshotIdAsync(scope, second.Snapshot.SnapshotId, first.Snapshot.SnapshotId);
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId)))
            .Throws<InvalidDataException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(second.Snapshot.SnapshotId);

        await CorruptStoredSnapshotIdAsync(scope, second.Snapshot.SnapshotId, second.Snapshot.SnapshotId);
        await CorruptStoredSnapshotIdAsync(scope, first.Snapshot.SnapshotId, second.Snapshot.SnapshotId);
        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId)))
            .Throws<InvalidDataException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(second.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Stale_cas_wins_over_a_corrupt_current_snapshot_graph()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        await CorruptStoredSnapshotIdAsync(scope, first.Snapshot.SnapshotId, new string('a', DocumentPublicationContract.Sha256HexLength));

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-b", "kpi-b")))
            .Throws<DocumentPublicationConcurrencyException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(first.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Reserved_generic_exact_reads_prefer_the_canonical_publication_scoped_payload_ids()
    {
        var rosterContext = new DocumentPublicationKey(
            DocumentPublicationKind.Context,
            BundesligaRosterPublicationContract.AggregateRosterDocumentName);
        var eloKpi = new DocumentPublicationKey(DocumentPublicationKind.Kpi, BundesligaDocumentPublication.ClubEloRankingsDocumentName);
        var rosterScope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, BundesligaDocumentPublication.Rosters.PublicationSet);
        var eloScope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, BundesligaDocumentPublication.ClubElo.PublicationSet);
        await fixture.Db.Collection("context-documents")
            .Document(PublicationPayloadId(rosterScope, rosterContext.Name, 7))
            .SetAsync(new FirestoreContextDocument
            {
                Competition = rosterScope.Competition, CommunityContext = Community, PublicationSet = rosterScope.PublicationSet,
                DocumentName = rosterContext.Name, Content = "published roster", Version = 7, CreatedAt = Timestamp.GetCurrentTimestamp()
            });
        await fixture.Db.Collection("kpi-documents")
            .Document(PublicationPayloadId(eloScope, eloKpi.Name, 3))
            .SetAsync(new FirestoreKpiDocument
            {
                Competition = eloScope.Competition, CommunityContext = Community, PublicationSet = eloScope.PublicationSet,
                DocumentName = eloKpi.Name, Content = "published elo", Description = "Elo", Version = 3, CreatedAt = Timestamp.GetCurrentTimestamp()
            });

        var context = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), CompetitionIds.Bundesliga2026_27);
        var kpi = new FirebaseKpiRepository(fixture.Db, new FakeLogger<FirebaseKpiRepository>(), CompetitionIds.Bundesliga2026_27);
        var loadedContext = await context.GetContextDocumentAsync(rosterContext.Name, 7, Community);
        var loadedKpi = await kpi.GetKpiDocumentAsync(eloKpi.Name, Community, 3);

        await Assert.That(loadedContext!.Content).IsEqualTo("published roster");
        await Assert.That(loadedKpi!.Content).IsEqualTo("published elo");
    }

    [Test]
    public async Task Canonical_roster_and_elo_definitions_publish_and_read_all_required_documents()
    {
        var repository = CreateRepository();
        var roster = await repository.PublishAsync(BundesligaDocumentPublication.Rosters, CanonicalRequest(BundesligaDocumentPublication.Rosters));
        var elo = await repository.PublishAsync(BundesligaDocumentPublication.ClubElo, CanonicalRequest(BundesligaDocumentPublication.ClubElo));
        var loadedRoster = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, Community);
        var loadedElo = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, Community);

        await Assert.That(roster.Snapshot.Documents.Length).IsEqualTo(20);
        await Assert.That(elo.Snapshot.Documents.Length).IsEqualTo(19);
        await Assert.That(loadedRoster!.Documents.Length).IsEqualTo(20);
        await Assert.That(loadedElo!.Documents.Length).IsEqualTo(19);
    }

    [Test]
    public async Task Same_expected_concurrent_publishers_have_one_winner_and_no_loser_graph()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var leftRequest = Request("context-left", "kpi-left", first.Snapshot.SnapshotId);
        var rightRequest = Request("context-right", "kpi-right", first.Snapshot.SnapshotId);
        var leftTask = repository.PublishAsync(Definition, leftRequest);
        var rightTask = repository.PublishAsync(Definition, rightRequest);
        var left = await CaptureAsync(leftTask);
        var right = await CaptureAsync(rightTask);

        await Assert.That(new[] { left.Result, right.Result }.Count(result => result is not null)).IsEqualTo(1);
        await Assert.That(new[] { left.Exception, right.Exception }.Single(exception => exception is not null))
            .IsTypeOf<DocumentPublicationConcurrencyException>();
        var winner = left.Result ?? right.Result!;
        var loserRequest = left.Result is null ? leftRequest : rightRequest;
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var loserSnapshot = DocumentPublicationContract.ComputeSnapshotId(loserRequest.Documents);
        var loaded = await repository.GetLastKnownGoodAsync(Definition, Community);

        await Assert.That(loaded!.Snapshot.SnapshotId).IsEqualTo(winner.Snapshot.SnapshotId);
        var loserMetadata = await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, loserSnapshot)).GetSnapshotAsync();
        await Assert.That(loserMetadata.Exists).IsFalse();
    }

    [Test]
    public async Task Payload_create_collision_rolls_back_without_a_head_or_partial_peer_payload()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var collidingId = PublicationPayloadId(scope, "fixture-context", 0);
        await fixture.Db.Collection("context-documents").Document(collidingId).SetAsync(new Dictionary<string, object>
        {
            ["collision"] = true
        });
        var repository = CreateRepository();

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a")))
            .Throws<Exception>();
        var head = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync();
        var kpi = await fixture.Db.Collection("kpi-documents")
            .Document(PublicationPayloadId(scope, "fixture-kpi", 0)).GetSnapshotAsync();
        await Assert.That(head.Exists).IsFalse();
        await Assert.That(kpi.Exists).IsFalse();
    }

    [Test]
    public async Task Snapshot_metadata_collision_fails_closed_without_a_head_or_payloads()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var request = Request("context-a", "kpi-a");
        var target = DocumentPublicationContract.ComputeSnapshotId(request.Documents);
        await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, target))
            .SetAsync(new FirestoreDocumentPublicationSnapshot
            {
                Competition = scope.Competition,
                CommunityContext = scope.CommunityContext,
                PublicationSet = scope.PublicationSet,
                SnapshotId = target,
                CreatedAt = Timestamp.GetCurrentTimestamp(),
                MetadataJson = "{}",
                Documents = []
            });

        await Assert.That(() => CreateRepository().PublishAsync(Definition, request)).Throws<InvalidDataException>();
        var head = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync();
        var context = await fixture.Db.Collection("context-documents")
            .Document(PublicationPayloadId(scope, "fixture-context", 0)).GetSnapshotAsync();
        await Assert.That(head.Exists).IsFalse();
        await Assert.That(context.Exists).IsFalse();
    }

    [Test]
    public async Task Headed_payload_scope_version_and_hash_corruption_fail_closed_for_context_and_kpi()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var context = first.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Context);
        var kpi = first.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Kpi);

        await AssertPayloadFailureAsync("context-documents", scope, context, "competition", "wrong-competition", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "communityContext", "wrong-community", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "publicationSet", "wrong-set", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "version", 42, repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "content", "wrong-content", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "competition", "wrong-competition", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "communityContext", "wrong-community", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "publicationSet", "wrong-set", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "version", 42, repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "content", "wrong-content", repository);
    }

    [Test]
    public async Task Missing_and_wrong_scope_head_or_snapshot_envelopes_fail_closed()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var head = fixture.Db.Collection("document-publication-heads").Document(DocumentPublicationContract.ComputeHeadId(scope));
        var metadata = fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, first.Snapshot.SnapshotId));

        await head.UpdateAsync("communityContext", "wrong-community");
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await head.UpdateAsync("communityContext", Community);
        await metadata.UpdateAsync("publicationSet", "wrong-set");
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await metadata.UpdateAsync("publicationSet", Definition.PublicationSet);
        await metadata.DeleteAsync();
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Version_allocation_includes_gaps_legacy_unheaded_and_other_set_rows_for_both_kinds()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        await SeedVersionAsync("context-documents", "legacy-context", scope, "fixture-context", 2, string.Empty);
        await SeedVersionAsync("context-documents", "unheaded-context", scope, "fixture-context", 7, "other-set");
        await SeedVersionAsync("kpi-documents", "legacy-kpi", scope, "fixture-kpi", 3, string.Empty);
        await SeedVersionAsync("kpi-documents", "unheaded-kpi", scope, "fixture-kpi", 9, "other-set");

        var published = await CreateRepository().PublishAsync(Definition, Request("context-a", "kpi-a"));

        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Context).Version).IsEqualTo(8);
        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Kpi).Version).IsEqualTo(10);
    }

    [Test]
    public async Task Noop_metadata_and_kpi_description_do_not_rewrite_rows_and_new_rows_share_one_timestamp()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var before = await repository.GetLastKnownGoodAsync(Definition, Community);
        var unchanged = await repository.PublishAsync(Definition, new DocumentPublicationRequest(
            Community, first.Snapshot.SnapshotId,
            [new(DocumentPublicationKind.Context, "fixture-context", "context-a"), new(DocumentPublicationKind.Kpi, "fixture-kpi", "kpi-a", "Changed description")],
            "{\"changedMetadata\":true}"));
        var after = await repository.GetLastKnownGoodAsync(Definition, Community);
        var snapshotTimestamp = (await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, first.Snapshot.SnapshotId)).GetSnapshotAsync())
            .GetValue<Timestamp>("createdAt").ToDateTimeOffset().ToUnixTimeMilliseconds();

        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(after!.Snapshot.MetadataJson).IsEqualTo(before!.Snapshot.MetadataJson);
        await Assert.That(after.Documents.Select(document => document.CreatedAt.ToUnixTimeMilliseconds()).Distinct()).IsEquivalentTo(new[] { snapshotTimestamp });
    }

    [Test]
    public async Task Scope_isolation_uses_distinct_deterministic_head_and_snapshot_paths()
    {
        var first = await CreateRepository().PublishAsync(Definition, Request("context-a", "kpi-a"));
        var secondCommunity = $"{Community}-two";
        var otherDefinition = new DocumentPublicationDefinition("fixture-other", Definition.RequiredDocuments);
        var otherCommunityResult = await CreateRepository().PublishAsync(
            Definition,
            new DocumentPublicationRequest(secondCommunity, null, Request("context-b", "kpi-b").Documents, "{\"fixture\":true}"));
        var otherSetResult = await CreateRepository().PublishAsync(
            otherDefinition,
            new DocumentPublicationRequest(Community, null, Request("context-c", "kpi-c").Documents, "{\"fixture\":true}"));
        var scopes = new[]
        {
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet),
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, secondCommunity, Definition.PublicationSet),
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, otherDefinition.PublicationSet)
        };

        await Assert.That(scopes.Select(DocumentPublicationContract.ComputeHeadId).Distinct().Count()).IsEqualTo(3);
        await Assert.That(new[]
        {
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[0], first.Snapshot.SnapshotId),
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[1], otherCommunityResult.Snapshot.SnapshotId),
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[2], otherSetResult.Snapshot.SnapshotId)
        }.Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Generic_repositories_reject_reserved_bundesliga_writes_but_allow_other_competitions()
    {
        var context = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), CompetitionIds.Bundesliga2026_27);
        var kpi = new FirebaseKpiRepository(fixture.Db, new FakeLogger<FirebaseKpiRepository>(), CompetitionIds.Bundesliga2026_27);

        await Assert.That(() => context.SaveContextDocumentAsync("roster-b04", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.SaveContextDocumentAsync("team-rosters", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.SaveContextDocumentAsync("club-elo-b04.csv", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => kpi.SaveKpiDocumentAsync("team-squad-summary", "content", "description", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => kpi.SaveKpiDocumentAsync("club-elo-rankings", "content", "description", "community"))
            .Throws<InvalidOperationException>();

        var historical = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), "bundesliga-2025-26");
        await historical.SaveContextDocumentAsync("roster-b04", "historical", "community");
        await context.SaveContextDocumentAsync("nonreserved", "allowed", "community");
        var wm26 = new FirebaseKpiRepository(
            fixture.Db,
            new FakeLogger<FirebaseKpiRepository>(),
            CompetitionIds.FifaWorldCup2026);
        await wm26.SaveKpiDocumentAsync("club-elo-rankings", "allowed", "description", "community");
    }

    [Test]
    public async Task Club_elo_initial_unchanged_changed_and_stale_cas_use_one_exact_headed_lkg()
    {
        var repository = CreateRepository();
        var initialBuild = ClubEloBuild(BundesligaClubEloSeed.Default);
        var initialRequest = BundesligaClubEloPublication.CreateRequest(Community, null, initialBuild);

        var initial = await repository.PublishAsync(BundesligaDocumentPublication.ClubElo, initialRequest);
        var loaded = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, Community);
        var unchanged = await repository.PublishAsync(
            BundesligaDocumentPublication.ClubElo,
            BundesligaClubEloPublication.CreateRequest(Community, initial.Snapshot.SnapshotId, initialBuild));
        var changedSnapshot = BundesligaClubEloSnapshot.Create(
            BundesligaClubEloSeed.Default.Entries.Select(entry => entry.Team.TeamSlug == "b04" ? entry with { Elo = entry.Elo + 1 } : entry).ToArray(),
            BundesligaClubEloSeed.Default.RatedAt,
            BundesligaClubEloSeed.Default.CollectedAt,
            BundesligaClubEloSeed.Default.SourceUrl,
            BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var changed = await repository.PublishAsync(
            BundesligaDocumentPublication.ClubElo,
            BundesligaClubEloPublication.CreateRequest(Community, initial.Snapshot.SnapshotId, ClubEloBuild(changedSnapshot)));

        await Assert.That(initial.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(initial.Snapshot.Documents.Length).IsEqualTo(19);
        var reconstructed = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded!);
        await Assert.That(reconstructed.Entries).IsEquivalentTo(BundesligaClubEloSeed.Default.Entries);
        await Assert.That(reconstructed.RatedAt).IsEqualTo(BundesligaClubEloSeed.Default.RatedAt);
        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(changed.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(changed.Snapshot.Documents.Single(entry => entry.Name == "club-elo-b04.csv").Version).IsEqualTo(1);
        await Assert.That(changed.Snapshot.Documents.Single(entry => entry.Name == "club-elo-rankings").Version).IsEqualTo(1);
        await Assert.That(() => repository.PublishAsync(BundesligaDocumentPublication.ClubElo, initialRequest))
            .Throws<DocumentPublicationConcurrencyException>();
    }

    [Test]
    public async Task Club_elo_corrupt_headed_payload_fails_before_lkg_reconstruction_or_head_movement()
    {
        var repository = CreateRepository();
        var published = await repository.PublishAsync(
            BundesligaDocumentPublication.ClubElo,
            BundesligaClubEloPublication.CreateRequest(Community, null, ClubEloBuild(BundesligaClubEloSeed.Default)));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, BundesligaDocumentPublication.ClubEloPublicationSet);
        var entry = published.Snapshot.Documents.Single(value => value.Name == "club-elo-b04.csv");
        var reference = fixture.Db.Collection("context-documents").Document(PublicationPayloadId(scope, entry.Name, entry.Version));
        await reference.UpdateAsync("content", "corrupt");

        await Assert.That(() => repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, Community))
            .Throws<InvalidDataException>();
        await Assert.That(() => repository.PublishAsync(
                BundesligaDocumentPublication.ClubElo,
                BundesligaClubEloPublication.CreateRequest(Community, published.Snapshot.SnapshotId, ClubEloBuild(BundesligaClubEloSeed.Default))))
            .Throws<InvalidDataException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(published.Snapshot.SnapshotId);
    }

    private async Task<(FirebaseContextSourceCycleRepository Cycles, ContextSourcePublicationGuard Guard, ContextSourcePublicationCommitRequest Commit)> PrepareGuardedRosterCycleAsync(
        bool unavailableIdentity = false,
        string? unavailableIdentityState = null,
        bool metadataUnchanged = false,
        bool eligibleRoster = false,
        bool schemaRejectedRoster = false,
        bool clearContextSourceState = true,
        string? cycleId = null)
    {
        if (clearContextSourceState) await ClearContextSourceStateForGuardedTestAsync();
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db,
            new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedTimeProvider(GuardNow.AddMinutes(2)));
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition,
            cycleId ?? NextGuardedCycleId());
        var outer = new BundesligaContextSourceOuterCycle(identity, GuardNow, GuardNow,
            BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentConsumers,
            [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
        await cycles.CreateOrResumeCycleAsync(outer);
        await cycles.ClaimSourceAsync(identity, BundesligaContextSource.Rosters, GuardToken, GuardNow);
        if (unavailableIdentityState is not null and not "new") await SetGuardedUnavailableIdentityRevisionStateAsync(identity, unavailableIdentityState);
        var observation = metadataUnchanged ? GuardedMetadataUnchangedObservation(identity) : eligibleRoster ? EligibleGuardedRosterObservation(identity) : schemaRejectedRoster ? GuardedSchemaRejectedRosterObservation(identity) : unavailableIdentity ? GuardedUnavailableIdentityObservation(identity, UnavailableIdentityReason(unavailableIdentityState)) : GuardedRosterObservation(identity);
        await cycles.FinalizeSourceAsync(identity, BundesligaContextSource.Rosters, GuardToken, observation, GuardNow.AddMinutes(1));
        var bundle = new string('e', 64);
        await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.ObservationsFinalized,
            BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.BundleVerified,
            BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var guard = new ContextSourcePublicationGuard(identity.Competition, identity.Scope, identity.CycleId,
            BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane,
            BundesligaContextSourceContract.DevelopmentCommunity, BundesligaDocumentPublication.RosterPublicationSet,
            bundle, observation.ObservationDigest, identity.Sequence, identity.CycleId);
        var template = new ContextSourcePublicationReceiptTemplate(
            metadataUnchanged ? BundesligaContextSourceSelectionDisposition.MetadataUnchanged : eligibleRoster ? BundesligaContextSourceSelectionDisposition.DuckDbAccepted : BundesligaContextSourceSelectionDisposition.CandidateRejected,
            eligibleRoster ? BundesligaContextSourceSelectedOrigin.DuckDb : BundesligaContextSourceSelectedOrigin.FallbackSeed,
            eligibleRoster
                ? new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1))
                : new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40),
            new BundesligaContextSourceCarriedFields(0, 0, 0, null),
            BundesligaContextSourceHealth.OrderConditions(eligibleRoster ? [] : unavailableIdentity ? new[]
            {
                BundesligaContextSourceHealthCondition.AcquisitionFailed,
                BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
                BundesligaContextSourceHealthCondition.RosterMembershipRejected,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days
            } : new[]
            {
                BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
                BundesligaContextSourceHealthCondition.RosterMembershipRejected,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days
        }));
        return (cycles, guard, new ContextSourcePublicationCommitRequest(guard, template));
    }

    private sealed record PreparedProductionGuardedRosterCycle(
        FirebaseContextSourceCycleRepository Cycles,
        BundesligaContextSourceOuterCycle Outer,
        BundesligaContextSourceObservation Observation,
        string BundleDigest);

    private async Task<PreparedProductionGuardedRosterCycle> PrepareGuardedProductionRosterCycleAsync()
    {
        await ClearContextSourceStateForGuardedTestAsync();
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db,
            new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedTimeProvider(GuardNow.AddMinutes(2)));
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 321, 9701);
        var outer = new BundesligaContextSourceOuterCycle(identity, GuardNow, GuardNow,
            BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers,
            [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
        await cycles.CreateOrResumeCycleAsync(outer);
        await cycles.ClaimSourceAsync(identity, BundesligaContextSource.Rosters, GuardToken, GuardNow);
        var observation = GuardedRosterObservation(identity);
        await cycles.FinalizeSourceAsync(identity, BundesligaContextSource.Rosters, GuardToken, observation, GuardNow.AddMinutes(1));
        var bundle = new string('e', 64);
        var artifact = $"bundesliga-context-source-bundle-{identity.StorageId}";
        await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, bundle, artifact);
        var handoff = await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, bundle, artifact);
        return new PreparedProductionGuardedRosterCycle(cycles, handoff, observation, bundle);
    }

    private static ContextSourcePublicationCommitRequest ProductionGuardedCommit(PreparedProductionGuardedRosterCycle prepared, string lane)
    {
        var community = lane switch
        {
            "pes-squad-context" => "pes-squad",
            "schadensfresse-context" => "schadensfresse",
            "relaxdays-tippt-context" => "relaxdays-tippt",
            _ => "ehonda-ai-arena"
        };
        var guard = new ContextSourcePublicationGuard(prepared.Outer.Identity.Competition, prepared.Outer.Identity.Scope,
            prepared.Outer.Identity.CycleId, BundesligaContextSource.Rosters, lane, community,
            BundesligaDocumentPublication.RosterPublicationSet, prepared.BundleDigest, prepared.Observation.ObservationDigest,
            prepared.Outer.Identity.Sequence, prepared.Outer.Identity.CycleId);
        var template = new ContextSourcePublicationReceiptTemplate(
            BundesligaContextSourceSelectionDisposition.CandidateRejected,
            BundesligaContextSourceSelectedOrigin.FallbackSeed,
            new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40),
            new BundesligaContextSourceCarriedFields(0, 0, 0, null),
            BundesligaContextSourceHealth.OrderConditions([
                BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
                BundesligaContextSourceHealthCondition.RosterMembershipRejected,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days]));
        return new ContextSourcePublicationCommitRequest(guard, template);
    }

    private static DocumentPublicationRequest ProductionRosterRequest(ContextSourcePublicationCommitRequest commit, string? expectedHead, string contentTag) => new(
        commit.Guard.CommunityContext,
        expectedHead,
        BundesligaDocumentPublication.Rosters.RequiredDocuments.Select(key => new DocumentPublicationPayload(
            key.Kind, key.Name, $"{contentTag}:{key.Name}", key.Kind == DocumentPublicationKind.Kpi ? $"{contentTag} summary" : null)),
        "{\"guarded\":true}", commit);

    private static DocumentPublicationRequest RosterRequest(
        ContextSourcePublicationCommitRequest? commit,
        string? expected,
        string contentTag) => new(
        BundesligaContextSourceContract.DevelopmentCommunity,
        expected,
        BundesligaDocumentPublication.Rosters.RequiredDocuments.Select(key => new DocumentPublicationPayload(
            key.Kind, key.Name, $"{contentTag}:{key.Name}", key.Kind == DocumentPublicationKind.Kpi ? $"{contentTag} summary" : null)),
        "{\"guarded\":true}", commit);

    private static BundesligaContextSourceObservation GuardedRosterObservation(BundesligaContextSourceCycleIdentity identity) => new(
        BundesligaContextSource.Rosters,
        BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), GuardNow,
        BundesligaContextSourceDisposition.Rejected,
        $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"SourceDateRejected\"}}",
        null, ["UNKNOWN_SOURCE_DATE"]);

    private static BundesligaContextSourceObservation EligibleGuardedRosterObservation(BundesligaContextSourceCycleIdentity identity) => new(
        BundesligaContextSource.Rosters,
        BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), GuardNow,
        BundesligaContextSourceDisposition.ArtifactCaptured,
        $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":\"2026-09-01\",\"membershipEffectiveDate\":\"2026-09-01\",\"enrichmentCaptureDate\":\"2026-09-01\",\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"Eligible\"}}",
        new BundesligaContextSourcePayload("rosters/source.duckdb", 1, new string('c', 64)), []);

    private static BundesligaContextSourceObservation GuardedSchemaRejectedRosterObservation(BundesligaContextSourceCycleIdentity identity)
    {
        var observation = GuardedRosterObservation(identity);
        return observation with
        {
            DescriptorJson = observation.DescriptorJson.Replace("\"evaluation\":\"SourceDateRejected\"", "\"evaluation\":\"SchemaRejected\""),
            Diagnostics = ["ROSTER_DUCKDB_SCHEMA_REJECTED", "UNKNOWN_SOURCE_DATE", "ROSTER_MEMBERSHIP_REJECTED"]
        };
    }

    private static BundesligaContextSourceObservation GuardedUnavailableIdentityObservation(BundesligaContextSourceCycleIdentity identity, string? reason = "NewRevision") => new(
        BundesligaContextSource.Rosters,
        BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), GuardNow,
        BundesligaContextSourceDisposition.Rejected,
        $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":null,\"acquisitionReason\":{(reason is null ? "null" : $"\"{reason}\"")},\"remoteIdentityAfter\":null,\"embeddedRevision\":null,\"rawSha256\":null,\"expectedRawSha256\":null,\"rawByteLength\":null,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"RemoteIdentityUnavailable\"}}",
        null, ["ROSTER_REMOTE_IDENTITY_UNAVAILABLE"]);

    private static string? UnavailableIdentityReason(string? state) => state switch
    {
        null or "new" => "NewRevision",
        "pending" => "PendingRevision",
        "policy" => "PolicyChanged",
        "accepted-null" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private Task SetGuardedUnavailableIdentityRevisionStateAsync(BundesligaContextSourceCycleIdentity identity, string state)
    {
        var reference = fixture.Db.Collection("context-source-health").Document(BundesligaContextSourceHashing.HealthStorageId(identity.Competition, identity.ScopeValue, BundesligaContextSource.Rosters));
        var policy = state == "policy" ? new string('f', 64) : BundesligaContextSourceDescriptorContract.RosterPolicySha256;
        return reference.UpdateAsync("rosterRevisionState", new Dictionary<string, object?>
        {
            ["accepted"] = state == "pending" ? null : new Dictionary<string, object?>
            {
                ["revision"] = new string('a', 40),
                ["remoteIdentity"] = new Dictionary<string, object?> { ["etag"] = "x", ["byteLength"] = 1L },
                ["policySha256"] = policy,
                ["descriptorSha256"] = new string('d', 64)
            },
            ["pending"] = state == "pending" ? new Dictionary<string, object?>
            {
                ["revision"] = new string('a', 40),
                ["remoteIdentity"] = new Dictionary<string, object?> { ["etag"] = "x", ["byteLength"] = 1L },
                ["policySha256"] = BundesligaContextSourceDescriptorContract.RosterPolicySha256,
                ["firstSeenCycleId"] = identity.CycleId,
                ["lastFailureCode"] = "RevisionRejected"
            } : null
        });
    }

    private static BundesligaContextSourceObservation GuardedMetadataUnchangedObservation(BundesligaContextSourceCycleIdentity identity) => new(
        BundesligaContextSource.Rosters,
        BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), GuardNow,
        BundesligaContextSourceDisposition.MetadataUnchanged,
        $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"AcceptedRevisionUnchanged\",\"remoteIdentityAfter\":null,\"embeddedRevision\":null,\"rawSha256\":null,\"expectedRawSha256\":null,\"rawByteLength\":null,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":\"{GuardedRosterDescriptorSha256}\",\"retainedEvaluation\":\"SourceDateRejected\",\"retainedDiagnostics\":[\"UNKNOWN_SOURCE_DATE\"],\"evaluation\":\"MetadataUnchanged\"}}",
        null, []);

    private static readonly DateTimeOffset GuardNow = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private const string GuardToken = "11111111-1111-4111-8111-111111111111";
    private static int guardedCycleSequence;
    private static string NextGuardedCycleId() => $"0198f865-1467-7000-8000-{Interlocked.Increment(ref guardedCycleSequence):D12}";
    private static readonly string GuardedRosterDescriptorSha256 = GuardedRosterObservation(
        BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000001")).DescriptorSha256;
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }

    private FirebaseDocumentPublicationRepository CreateRepository() => new(
        fixture.Db,
        new FakeLogger<FirebaseDocumentPublicationRepository>(),
        CompetitionIds.Bundesliga2026_27);

    private DocumentPublicationRequest Request(string context, string kpi, string? expected = null) => new(
        Community,
        expected,
        [
            new DocumentPublicationPayload(DocumentPublicationKind.Context, "fixture-context", context),
            new DocumentPublicationPayload(DocumentPublicationKind.Kpi, "fixture-kpi", kpi, "Fixture KPI")
        ],
        "{\"fixture\":true}");

    private DocumentPublicationRequest CanonicalRequest(DocumentPublicationDefinition definition) => new(
        Community,
        null,
        definition.RequiredDocuments.Select(key => new DocumentPublicationPayload(
            key.Kind,
            key.Name,
            $"payload:{key.Kind}:{key.Name}",
            key.Kind == DocumentPublicationKind.Kpi ? $"Description for {key.Name}" : null)),
        "{\"fixture\":true}");

    private static BundesligaClubEloPublicationBuild ClubEloBuild(BundesligaClubEloSnapshot snapshot) =>
        BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            snapshot,
            BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));

    private async Task CorruptStoredSnapshotIdAsync(DocumentPublicationScope scope, string metadataId, string storedSnapshotId)
    {
        var reference = fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, metadataId));
        await reference.UpdateAsync("snapshotId", storedSnapshotId);
    }

    private async Task<string> HeadSnapshotIdAsync(DocumentPublicationScope scope)
    {
        var snapshot = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope))
            .GetSnapshotAsync();
        return snapshot.GetValue<string>("snapshotId");
    }

    private static string PublicationPayloadId(DocumentPublicationScope scope, string name, int version) =>
        $"{DocumentPublicationContract.ComputeHeadId(scope)}_{name}_{version}";

    private async Task AssertPayloadFailureAsync(
        string collection,
        DocumentPublicationScope scope,
        DocumentPublicationEntry entry,
        string field,
        object invalidValue,
        FirebaseDocumentPublicationRepository repository)
    {
        var reference = fixture.Db.Collection(collection).Document(PublicationPayloadId(scope, entry.Name, entry.Version));
        var original = (await reference.GetSnapshotAsync()).GetValue<object>(field);
        await reference.UpdateAsync(field, invalidValue);
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await reference.UpdateAsync(field, original);
    }

    private async Task SeedVersionAsync(
        string collection,
        string id,
        DocumentPublicationScope scope,
        string name,
        int version,
        string publicationSet)
    {
        var values = new Dictionary<string, object>
        {
            ["competition"] = scope.Competition,
            ["communityContext"] = scope.CommunityContext,
            ["documentName"] = name,
            ["version"] = version,
            ["content"] = $"seed-{version}",
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["publicationSet"] = publicationSet
        };
        if (collection == "kpi-documents")
        {
            values["description"] = "seed";
        }

        await fixture.Db.Collection(collection).Document(id).SetAsync(values);
    }

    private static async Task<(DocumentPublicationResult? Result, Exception? Exception)> CaptureAsync(Task<DocumentPublicationResult> task)
    {
        try
        {
            return (await task, null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }
}
