using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using System.Text.Json;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, "context-source-cycle-repository" })]
public sealed class FirebaseContextSourceCycleRepositoryTests(FirestoreFixture fixture)
{
    private const string Cycles = "context-source-cycles";
    private const string Observations = "context-source-cycle-observations";
    private const string Receipts = "context-source-cycle-receipts";
    private const string Health = "context-source-health";
    private const string Heads = "document-publication-heads";
    [Before(Test)]
    public async Task ClearAsync()
    {
        foreach (var collection in new[] { Cycles, Observations, Receipts, Health, Heads })
        {
            var snapshot = await fixture.Db.Collection(collection).GetSnapshotAsync();
            foreach (var document in snapshot.Documents) await document.Reference.DeleteAsync();
        }
    }

    [Test]
    public async Task Claim_race_honors_token_and_expired_lease_aborts_without_takeover()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        var first = await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var owned = await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now().AddMinutes(1));
        var busy = await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now().AddMinutes(1));
        var expired = await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now().AddMinutes(10));
        await Assert.That(first.Disposition).IsEqualTo(BundesligaContextSourceClaimDisposition.NewClaim);
        await Assert.That(owned.Disposition).IsEqualTo(BundesligaContextSourceClaimDisposition.OwnedClaim);
        await Assert.That(busy.Disposition).IsEqualTo(BundesligaContextSourceClaimDisposition.Busy);
        await Assert.That(expired.Disposition).IsEqualTo(BundesligaContextSourceClaimDisposition.ExistingAborted);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.AcquisitionInterrupted);
        var health = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(health!.ConsecutiveFailures.Handoff).IsEqualTo(1);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.CycleAborted);
        var replay = await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now().AddMinutes(11));
        await Assert.That(replay.Disposition).IsEqualTo(BundesligaContextSourceClaimDisposition.ExistingAborted);
        await Assert.That((await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters))!.ConsecutiveFailures.Handoff).IsEqualTo(1);
    }

    [Test]
    public async Task Finalize_exact_retry_is_noop_and_conflicting_digest_is_fatal()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        var first = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var replay = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(2));
        await Assert.That(replay.FinalizedAtUtc).IsEqualTo(first.FinalizedAtUtc);
        await Assert.That(() => repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation with { Diagnostics = ["DIFFERENT"] }, Now().AddMinutes(2))).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("direct-abort")]
    [Arguments("partial-source-abort")]
    [Arguments("another-source-abort")]
    public async Task Finalize_exact_replay_precedes_later_abort_state_gates(string mutation)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1467-7000-8000-000000000060", [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));

        if (mutation == "direct-abort")
        {
            await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing);
        }
        else if (mutation == "partial-source-abort")
        {
            var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.Rosters));
            await sourceReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "LOCAL_HANDOFF_MISSING" });
        }
        else
        {
            await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('2'), Now());
            await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('3'), Now().AddMinutes(10));
        }

        var persisted = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);
        var replay = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(2));

        await Assert.That(replay.CreateCanonicalUtf8(cycle.ExpectedConsumers).SequenceEqual(persisted!.CreateCanonicalUtf8(cycle.ExpectedConsumers))).IsTrue();
        await Assert.That(() => repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation with { Diagnostics = ["DIFFERENT"] }, Now().AddMinutes(2))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Finalize_requires_a_timestamp_strictly_before_lease_expiry()
    {
        var repository = CreateRepository();
        var expired = Cycle("0198f865-1467-7000-8000-000000000030");
        await repository.CreateOrResumeCycleAsync(expired);
        await repository.ClaimSourceAsync(expired.Identity, BundesligaContextSource.Rosters, Token('1'), Now());

        await Assert.That(() => repository.FinalizeSourceAsync(expired.Identity, BundesligaContextSource.Rosters, Token('1'), Observation(expired.Identity), Now().AddMinutes(10))).Throws<InvalidDataException>();
        await Assert.That((await repository.GetSourceCycleAsync(expired.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Claimed);

        var valid = Cycle("0198f865-1468-7000-8000-000000000031");
        await repository.CreateOrResumeCycleAsync(valid);
        await repository.ClaimSourceAsync(valid.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var finalized = await repository.FinalizeSourceAsync(valid.Identity, BundesligaContextSource.Rosters, Token('2'), Observation(valid.Identity), Now().AddMinutes(10).AddSeconds(-1));

        await Assert.That(finalized.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
    }

    [Test]
    public async Task C1_correction_finalization_rejects_before_claim_and_hostile_reconstruction()
    {
        var repository = CreateRepository(); var cycle = Cycle("0198f865-1467-7000-8000-0000000000c1"); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        await Assert.That(() => repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddSeconds(-1))).Throws<InvalidDataException>();
        var finalized = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now());
        var replay = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(9));
        await Assert.That(replay.FinalizedAtUtc).IsEqualTo(finalized.FinalizedAtUtc);

        var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.Rosters));
        await sourceReference.UpdateAsync(new Dictionary<string, object> { ["finalizedAtUtc"] = "2026-09-06T11:59:59Z" });
        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task C1_correction_final_receipt_rolls_back_when_repository_clock_precedes_finalization()
    {
        var repository = CreateRepository(Now()); var cycle = Cycle("0198f865-1467-7000-8000-0000000000c2"); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);

        await Assert.That(() => repository.RecordReceiptAsync(Receipt(cycle.Identity, observation, digest))).Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
    }

    [Test]
    public async Task Canonical_Club_Elo_descriptor_and_digests_survive_Firestore_round_trip()
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1469-7000-8000-000000000032", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var observation = EligibleEloObservation(cycle.Identity);

        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), observation, Now().AddMinutes(1));
        var roundTripped = (await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo))!.Observation!;

        await Assert.That(roundTripped.DescriptorJson).IsEqualTo(observation.DescriptorJson);
        await Assert.That(roundTripped.DescriptorSha256).IsEqualTo(observation.DescriptorSha256);
        await Assert.That(roundTripped.ObservationDigest).IsEqualTo(observation.ObservationDigest);
    }

    [Test]
    public async Task Last_receipt_atomically_completes_receipt_health_source_and_outer_and_replays_time()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var request = Receipt(cycle.Identity, observation, digest);
        await Assert.That(() => repository.RecordReceiptAsync(request with { RosterRevision = new string('f', 40) })).Throws<InvalidDataException>();
        var first = await repository.RecordReceiptAsync(request);
        var replay = await repository.RecordReceiptAsync(request);
        await Assert.That(first.RecordedAtUtc).IsEqualTo(Now().AddMinutes(2));
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(first.RecordedAtUtc);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(1);
        await Assert.That((await fixture.Db.Collection(Health).GetSnapshotAsync()).Count).IsEqualTo(1);
        await Assert.That((await repository.GetReceiptAsync(cycle.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane))!.RecordedAtUtc).IsEqualTo(first.RecordedAtUtc);
        await Assert.That(() => repository.RecordReceiptAsync(request with { SelectedSnapshotId = new string('f', 64) })).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Eligible_roster_enrichment_rejections_are_reduced_once_after_the_complete_receipt_set()
    {
        var repository = CreateRepository();
        var cycle = ProductionCycle(458);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = EligibleRosterObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);

        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var enrichmentRejected = lane is "pes-squad-context" or "schadensfresse-context";
            await repository.RecordReceiptAsync(EligibleProductionRosterReceipt(cycle.Identity, observation, digest, lane, enrichmentRejected));
            if (lane != BundesligaContextSourceContract.ProductionConsumers[^1])
            {
                var incompleteHealth = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);
                await Assert.That(incompleteHealth!.ConsecutiveFailures.Enrichment).IsEqualTo(0);
                await Assert.That(incompleteHealth.ActiveConditions).DoesNotContain(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
            }
        }

        var health = (await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters))!;
        await Assert.That(health.ConsecutiveFailures.Enrichment).IsEqualTo(1);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
        await Assert.That(health.CommunitySelections.Count(selection => selection.Conditions.Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected))).IsEqualTo(2);

        var lastLane = BundesligaContextSourceContract.ProductionConsumers[^1];
        await repository.RecordReceiptAsync(EligibleProductionRosterReceipt(cycle.Identity, observation, digest, lastLane, false));
        var replayedHealth = (await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters))!;
        await Assert.That(replayedHealth.ConsecutiveFailures.Enrichment).IsEqualTo(1);
    }

    [Test]
    [Arguments("DuckDbAccepted")]
    [Arguments("MixedPerClubSelection")]
    public async Task Eligible_roster_receipt_persists_historical_reactivation_for_artifact_membership(string selectionValue)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000083");
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = EligibleRosterObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var mixed = selectionValue == nameof(BundesligaContextSourceSelectionDisposition.MixedPerClubSelection);
        var request = new BundesligaContextSourceReceiptRequest(
            cycle.Identity,
            BundesligaContextSource.Rosters,
            BundesligaContextSourceContract.DevelopmentLane,
            BundesligaContextSourceContract.DevelopmentCommunity,
            observation.ObservationDigest,
            digest,
            mixed ? BundesligaContextSourceSelectionDisposition.MixedPerClubSelection : BundesligaContextSourceSelectionDisposition.DuckDbAccepted,
            new string('c', 64),
            mixed ? BundesligaContextSourceSelectedOrigin.Mixed : BundesligaContextSourceSelectedOrigin.DuckDb,
            BundesligaContextSourcePublicationDisposition.Reactivated,
            new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), mixed ? new DateOnly(2026, 8, 30) : new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)),
            new string('a', 40),
            new BundesligaContextSourceCarriedFields(0, 0, 0, null),
            mixed ? [BundesligaContextSourceHealthCondition.RosterMembershipRejected] : []);
        if (mixed)
            await Assert.That(() => repository.RecordReceiptAsync(request with { SourceDates = request.SourceDates with { MembershipCapturedAt = new DateOnly(2026, 9, 2) } })).Throws<InvalidDataException>();

        var receipt = await repository.RecordReceiptAsync(request);
        var health = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);

        await Assert.That(receipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Reactivated);
        await Assert.That(health!.CommunitySelections.Single().SelectedOrigin).IsEqualTo(request.SelectedOrigin);
        await Assert.That(health.CommunitySelections.Single().MembershipCapturedAt).IsEqualTo(new DateOnly(2026, 9, 1));
        if (mixed)
            await Assert.That(health.CommunitySelections.Single().MembershipEffectiveAt).IsEqualTo(new DateOnly(2026, 8, 30));
    }

    [Test]
    public async Task New_receipt_requires_exact_freshness_conditions_but_exact_replay_preserves_lane_outcomes()
    {
        var referenceAt = Now().AddDays(35);
        var repository = CreateRepository(referenceAt.AddMinutes(2));
        var cycle = Cycle("0198f865-1468-7000-8000-000000000070") with { StartedAtUtc = referenceAt, StalenessReferenceAtUtc = referenceAt };
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), referenceAt);
        var observation = EligibleRosterObservation(cycle.Identity) with { ObservedAtUtc = referenceAt };
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, referenceAt.AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var conditions = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterEnrichmentRejected,
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days,
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days,
            BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days,
            BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days]);
        var request = new BundesligaContextSourceReceiptRequest(cycle.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity, observation.ObservationDigest, digest, BundesligaContextSourceSelectionDisposition.DuckDbAccepted, new string('c', 64), BundesligaContextSourceSelectedOrigin.DuckDb, BundesligaContextSourcePublicationDisposition.Published, new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), conditions);

        await Assert.That(() => repository.RecordReceiptAsync(request with { ActiveConditions = conditions.Where(condition => condition != BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days).ToArray() })).Throws<InvalidDataException>();
        await Assert.That(() => repository.RecordReceiptAsync(request with { ActiveConditions = BundesligaContextSourceHealth.OrderConditions(conditions.Append(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown)) })).Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(0);

        var recorded = await repository.RecordReceiptAsync(request);
        var replay = await repository.RecordReceiptAsync(request);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(recorded.RecordedAtUtc);
        await Assert.That(replay.Request.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("mixed-membership")]
    [Arguments("receipt-enrichment")]
    [Arguments("prior-bundle-digest-corruption")]
    public async Task Eligible_to_metadata_unchanged_repeats_every_lane_outcome_and_recomputes_freshness(string outcome)
    {
        var mixedMembership = outcome == "mixed-membership";
        var corruptPriorBundleDigest = outcome == "prior-bundle-digest-corruption";
        var firstRunId = mixedMembership ? 510L : corruptPriorBundleDigest ? 530L : 520L;
        var repository = CreateRepository();
        var eligibleCycle = ProductionCycle(firstRunId);
        await repository.CreateOrResumeCycleAsync(eligibleCycle);
        await repository.ClaimSourceAsync(eligibleCycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var eligibleObservation = EligibleRosterObservation(eligibleCycle.Identity);
        await repository.FinalizeSourceAsync(eligibleCycle.Identity, BundesligaContextSource.Rosters, Token('1'), eligibleObservation, Now().AddMinutes(1));
        var eligibleBundle = new string('e', 64);
        var eligibleArtifact = $"bundesliga-context-source-bundle-{eligibleCycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(eligibleCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, eligibleBundle);
        await repository.TransitionCycleAsync(eligibleCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, eligibleBundle, eligibleArtifact);
        await repository.TransitionCycleAsync(eligibleCycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, eligibleBundle, eligibleArtifact);

        var priorRequests = new Dictionary<string, BundesligaContextSourceReceiptRequest>(StringComparer.Ordinal);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var prior = EligibleProductionRosterReceipt(eligibleCycle.Identity, eligibleObservation, eligibleBundle, lane, !mixedMembership);
            if (mixedMembership)
            {
                prior = prior with
                {
                    SelectionDisposition = BundesligaContextSourceSelectionDisposition.MixedPerClubSelection,
                    SelectedOrigin = BundesligaContextSourceSelectedOrigin.Mixed,
                    ActiveConditions = [BundesligaContextSourceHealthCondition.RosterMembershipRejected]
                };
            }
            priorRequests.Add(lane, prior);
            await repository.RecordReceiptAsync(prior);
        }
        if (corruptPriorBundleDigest)
        {
            var priorLane = BundesligaContextSourceContract.ProductionConsumers[0];
            await fixture.Db.Collection(Receipts)
                .Document(BundesligaContextSourceHashing.ReceiptStorageId(eligibleCycle.Identity, BundesligaContextSource.Rosters, priorLane))
                .UpdateAsync("bundleDigest", new string('d', 64));
        }

        var metadataAt = Now().AddDays(16);
        var metadataRepository = CreateRepository(metadataAt.AddMinutes(2));
        var metadataCycle = ProductionCycle(firstRunId + 1) with { StartedAtUtc = metadataAt, StalenessReferenceAtUtc = metadataAt };
        await metadataRepository.CreateOrResumeCycleAsync(metadataCycle);
        await metadataRepository.ClaimSourceAsync(metadataCycle.Identity, BundesligaContextSource.Rosters, Token('2'), metadataAt);
        var metadataObservation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(metadataCycle.Identity, BundesligaContextSource.Rosters), metadataAt, BundesligaContextSourceDisposition.MetadataUnchanged, MetadataUnchangedDescriptor(eligibleObservation.DescriptorSha256, "Eligible", []), null, []);
        await metadataRepository.FinalizeSourceAsync(metadataCycle.Identity, BundesligaContextSource.Rosters, Token('2'), metadataObservation, metadataAt.AddMinutes(1));
        var metadataBundle = new string('f', 64);
        var metadataArtifact = $"bundesliga-context-source-bundle-{metadataCycle.Identity.StorageId}";
        await metadataRepository.TransitionCycleAsync(metadataCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, metadataBundle);
        await metadataRepository.TransitionCycleAsync(metadataCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, metadataBundle, metadataArtifact);
        await metadataRepository.TransitionCycleAsync(metadataCycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, metadataBundle, metadataArtifact);

        BundesligaContextSourceReceipt? finalReceipt = null;
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var prior = priorRequests[lane];
            var metadataConditions = BundesligaContextSourceHealth.OrderConditions([
                mixedMembership ? BundesligaContextSourceHealthCondition.RosterMembershipRejected : BundesligaContextSourceHealthCondition.RosterEnrichmentRejected,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days,
                BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days]);
            var current = prior with
            {
                Identity = metadataCycle.Identity,
                ObservationDigest = metadataObservation.ObservationDigest,
                BundleDigest = metadataBundle,
                SelectionDisposition = BundesligaContextSourceSelectionDisposition.MetadataUnchanged,
                PublicationDisposition = BundesligaContextSourcePublicationDisposition.Unchanged,
                ActiveConditions = metadataConditions
            };
            if (lane == BundesligaContextSourceContract.ProductionConsumers[0])
            {
                var withoutPriorOutcome = current with { ActiveConditions = metadataConditions.Where(condition => condition != (mixedMembership ? BundesligaContextSourceHealthCondition.RosterMembershipRejected : BundesligaContextSourceHealthCondition.RosterEnrichmentRejected)).ToArray() };
                await Assert.That(() => metadataRepository.RecordReceiptAsync(withoutPriorOutcome)).Throws<InvalidDataException>();
                if (corruptPriorBundleDigest)
                {
                    var before = (await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count;
                    await Assert.That(() => metadataRepository.RecordReceiptAsync(current)).Throws<InvalidDataException>();
                    await Assert.That(await metadataRepository.GetReceiptAsync(metadataCycle.Identity, BundesligaContextSource.Rosters, lane)).IsNull();
                    await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(before);
                    return;
                }
            }
            finalReceipt = await metadataRepository.RecordReceiptAsync(current);
        }

        var exactReplay = await metadataRepository.RecordReceiptAsync(finalReceipt!.Request);
        var health = (await metadataRepository.GetHealthAsync(metadataCycle.Identity.Competition, metadataCycle.Identity.Scope, BundesligaContextSource.Rosters))!;
        await Assert.That(exactReplay.RecordedAtUtc).IsEqualTo(finalReceipt.RecordedAtUtc);
        await Assert.That(health.ConsecutiveFailures.Membership).IsEqualTo(mixedMembership ? 2 : 0);
        await Assert.That(health.ConsecutiveFailures.Enrichment).IsEqualTo(mixedMembership ? 0 : 2);
        await Assert.That(health.ActiveConditions).Contains(mixedMembership ? BundesligaContextSourceHealthCondition.RosterMembershipRejected : BundesligaContextSourceHealthCondition.RosterEnrichmentRejected);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days);
        await Assert.That(health.CommunitySelections.All(selection => selection.Conditions.Contains(mixedMembership ? BundesligaContextSourceHealthCondition.RosterMembershipRejected : BundesligaContextSourceHealthCondition.RosterEnrichmentRejected))).IsTrue();
    }

    [Test]
    [Arguments("direct-abort")]
    [Arguments("partial-source-abort")]
    [Arguments("another-source-abort")]
    public async Task Receipt_exact_replay_precedes_later_abort_state_gates(string mutation)
    {
        var repository = CreateRepository();
        var cycle = ProductionCycle(490, [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var elo = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), elo, Now().AddMinutes(1));
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var roster = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), roster, Now().AddMinutes(1));
        var digest = new string('e', 64); var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        var request = ProductionReceipt(cycle.Identity, roster, digest, BundesligaContextSourceContract.ProductionConsumers[0]);
        var original = await repository.RecordReceiptAsync(request);

        if (mutation == "direct-abort")
        {
            await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactMissing);
        }
        else if (mutation == "partial-source-abort")
        {
            var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.Rosters));
            await sourceReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "HANDOFF_ARTIFACT_MISSING" });
        }
        else
        {
            var outerReference = fixture.Db.Collection(Cycles).Document(cycle.Identity.StorageId);
            var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
            await sourceReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "HANDOFF_ARTIFACT_MISSING" });
            await outerReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "HANDOFF_ARTIFACT_MISSING" });
        }

        var replay = await repository.RecordReceiptAsync(request);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(original.RecordedAtUtc);
        await Assert.That(() => repository.RecordReceiptAsync(request with { SelectedSnapshotId = new string('f', 64) })).Throws<InvalidDataException>();
    }

    [Test]
    public async Task RecordReceiptAsync_allows_initial_Elo_fallback_publication_without_a_retained_head()
    {
        var repository = CreateRepository();
        var cycle = Cycle(sources: [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var observation = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var initialHealth = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(initialHealth!.LastCompletedCycleId).IsNull();
        await Assert.That(initialHealth.CommunitySelections).IsEmpty();

        var receipt = await repository.RecordReceiptAsync(EloFallbackReceipt(cycle.Identity, observation, digest));

        await Assert.That(receipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
        await Assert.That(receipt.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LaunchSeed);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
    }

    [Test]
    public async Task Club_Elo_not_attempted_HTML_receipt_requires_the_authoritative_prior_selection_and_matching_head_but_replays_after_head_loss()
    {
        var repository = CreateRepository();
        var prior = Cycle("0198f865-1467-7000-8000-000000000070", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(prior);
        await repository.ClaimSourceAsync(prior.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var priorObservation = HtmlEloObservation(prior.Identity); var priorBundle = new string('e', 64);
        await repository.FinalizeSourceAsync(prior.Identity, BundesligaContextSource.ClubElo, Token('1'), priorObservation, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(prior.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, priorBundle);
        await repository.TransitionCycleAsync(prior.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, priorBundle);
        await repository.RecordReceiptAsync(HtmlReceipt(prior.Identity, priorObservation, priorBundle, BundesligaContextSourceSelectionDisposition.NetworkAccepted, BundesligaContextSourceSelectedOrigin.NetworkCandidate, BundesligaContextSourcePublicationDisposition.Published, new DateOnly(2026, 9, 4), []));

        var current = Cycle("0198f865-1468-7000-8000-000000000071", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(current);
        await repository.ClaimSourceAsync(current.Identity, BundesligaContextSource.ClubElo, Token('2'), Now());
        var observation = HtmlEloObservation(current.Identity); var bundle = new string('d', 64);
        await repository.FinalizeSourceAsync(current.Identity, BundesligaContextSource.ClubElo, Token('2'), observation, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(current.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(current.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var request = HtmlReceipt(current.Identity, observation, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted, new DateOnly(2026, 9, 4), []);
        var scope = new DocumentPublicationScope(current.Identity.Competition, request.CommunityContext, BundesligaDocumentPublication.ClubEloPublicationSet);
        var headReference = fixture.Db.Collection(Heads).Document(DocumentPublicationContract.ComputeHeadId(scope));

        InvalidDataException? missingHead = null;
        var beforeMissingHeadFailure = await PersistedGraphFingerprintAsync();
        try { await repository.RecordReceiptAsync(request); }
        catch (InvalidDataException exception) { missingHead = exception; }
        await Assert.That(missingHead).IsNotNull();
        await Assert.That(missingHead!.Message).IsEqualTo("Stored document identity/property set is invalid.");
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeMissingHeadFailure);
        await AssertNoCurrentReceiptMutation(current.Identity);
        await headReference.SetAsync(new Dictionary<string, object> { ["competition"] = scope.Competition, ["communityContext"] = scope.CommunityContext, ["publicationSet"] = scope.PublicationSet, ["snapshotId"] = request.SelectedSnapshotId });
        var recorded = await repository.RecordReceiptAsync(request);
        await headReference.UpdateAsync("snapshotId", new string('f', 64));
        var beforeMovedReplay = await PersistedGraphFingerprintAsync();
        var movedReplay = await repository.RecordReceiptAsync(request);
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeMovedReplay);
        await headReference.DeleteAsync();
        var beforeMissingHeadReplay = await PersistedGraphFingerprintAsync();
        var replay = await repository.RecordReceiptAsync(request);
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeMissingHeadReplay);

        await Assert.That(recorded.RecordedAtUtc).IsEqualTo(movedReplay.RecordedAtUtc);
        await Assert.That(recorded.RecordedAtUtc).IsEqualTo(replay.RecordedAtUtc);
        await Assert.That(replay.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        InvalidDataException? changedOrigin = null;
        var beforeChangedOrigin = await PersistedGraphFingerprintAsync();
        try { await repository.RecordReceiptAsync(request with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LaunchSeed }); }
        catch (InvalidDataException exception) { changedOrigin = exception; }
        await Assert.That(changedOrigin).IsNotNull();
        await Assert.That(changedOrigin!.Message).IsEqualTo("STATE_CONFLICT");
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeChangedOrigin);
        await Assert.That((await repository.GetReceiptAsync(current.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!.RecordedAtUtc).IsEqualTo(recorded.RecordedAtUtc);
    }

    [Test]
    [Arguments(BundesligaContextSourceSelectionDisposition.NetworkAccepted, BundesligaContextSourceSelectedOrigin.NetworkCandidate, BundesligaContextSourcePublicationDisposition.Published)]
    [Arguments(BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, BundesligaContextSourceSelectedOrigin.LaunchSeed, BundesligaContextSourcePublicationDisposition.Published)]
    [Arguments(BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, BundesligaContextSourceSelectedOrigin.LaunchSeed, BundesligaContextSourcePublicationDisposition.Reactivated)]
    public async Task HTML_retained_prior_origins_transition_to_current_LastKnownGood_not_attempted(
        BundesligaContextSourceSelectionDisposition priorSelection,
        BundesligaContextSourceSelectedOrigin priorOrigin,
        BundesligaContextSourcePublicationDisposition priorPublication)
    {
        var scenario = await CreateHtmlRetainedScenarioAsync(priorSelection, priorOrigin, priorPublication);
        var current = await scenario.Repository.RecordReceiptAsync(scenario.Request);
        await Assert.That(current.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(current.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.NotAttempted);

        var next = Cycle("0198f865-1469-7000-8000-0000000000b4", [BundesligaContextSource.ClubElo]);
        await scenario.Repository.CreateOrResumeCycleAsync(next);
        await scenario.Repository.ClaimSourceAsync(next.Identity, BundesligaContextSource.ClubElo, Token('3'), Now());
        var observation = HtmlEloObservation(next.Identity); var bundle = new string('b', 64);
        await scenario.Repository.FinalizeSourceAsync(next.Identity, BundesligaContextSource.ClubElo, Token('3'), observation, Now().AddMinutes(1));
        await scenario.Repository.TransitionCycleAsync(next.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await scenario.Repository.TransitionCycleAsync(next.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var lkgToLkg = HtmlReceipt(next.Identity, observation, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer,
            BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted,
            current.Request.SourceDates.RatedAt!.Value, []);
        var transitioned = await scenario.Repository.RecordReceiptAsync(lkgToLkg);
        await Assert.That(transitioned.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(transitioned.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.NotAttempted);
    }

    [Test]
    [Arguments("no-prior")]
    [Arguments("missing-prior-outer")]
    [Arguments("crossed-prior-outer")]
    [Arguments("missing-prior-source")]
    [Arguments("corrupt-prior-source")]
    [Arguments("missing-prior-receipt")]
    [Arguments("corrupt-prior-receipt")]
    [Arguments("health-selection")]
    [Arguments("missing-health")]
    [Arguments("prior-health-selected-date")]
    [Arguments("prior-health-origin")]
    [Arguments("prior-receipt-origin")]
    [Arguments("prior-observation-outcome-evaluation-mismatch")]
    [Arguments("current-launch-seed")]
    [Arguments("prior-freshness")]
    [Arguments("hostile-snapshot")]
    [Arguments("selected-date")]
    [Arguments("missing-head")]
    [Arguments("wrong-scope-head")]
    [Arguments("malformed-head")]
    [Arguments("extra-head-field")]
    [Arguments("moved-head")]
    [Arguments("wrong-current-freshness")]
    public async Task New_HTML_Club_Elo_not_attempted_hostiles_are_mutation_free(string hostile)
    {
        var scenario = await CreateHtmlRetainedScenarioAsync();
        var request = scenario.Request;
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(scenario.Current.Identity.Competition, scenario.Current.Identity.ScopeValue, BundesligaContextSource.ClubElo));
        var healthIsReadable = hostile is not ("health-selection" or "missing-health");
        switch (hostile)
        {
            case "no-prior": await healthReference.UpdateAsync("lastCompletedCycleId", null!); break;
            case "missing-prior-outer": await fixture.Db.Collection(Cycles).Document(scenario.Prior.Identity.StorageId).DeleteAsync(); break;
            case "crossed-prior-outer": await fixture.Db.Collection(Cycles).Document(scenario.Prior.Identity.StorageId).UpdateAsync("bundleSha256", new string('f', 64)); break;
            case "missing-prior-source": await fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo)).DeleteAsync(); break;
            case "corrupt-prior-source": await fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo)).UpdateAsync("status", "Finalized"); break;
            case "missing-prior-receipt": await fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane)).DeleteAsync(); break;
            case "corrupt-prior-receipt": await fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane)).UpdateAsync("selectionDisposition", "999"); break;
            case "health-selection": await healthReference.UpdateAsync("communitySelections", Array.Empty<object>()); break;
            case "missing-health": await healthReference.DeleteAsync(); break;
            case "prior-health-selected-date": await UpdatePriorHealthSelectedDateAsync(healthReference, new DateOnly(2026, 9, 5)); break;
            case "prior-health-origin": await UpdatePriorHealthSelectedOriginAsync(healthReference, BundesligaContextSourceSelectedOrigin.LastKnownGood); break;
            case "prior-receipt-origin":
                var priorObservation = (await scenario.Repository.GetSourceCycleAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo))!.Observation!;
                var independentlyValid = HtmlReceipt(scenario.Prior.Identity, priorObservation, new string('e', 64),
                    BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, BundesligaContextSourceSelectedOrigin.LaunchSeed,
                    BundesligaContextSourcePublicationDisposition.Published, new DateOnly(2026, 9, 4), []);
                independentlyValid.Validate();
                BundesligaContextSourceReceiptContract.ValidateAgainstObservation(independentlyValid, priorObservation);
                BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(independentlyValid, new DateOnly(2026, 9, 6));
                await fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane)).UpdateAsync(new Dictionary<string, object>
                {
                    ["selectionDisposition"] = independentlyValid.SelectionDisposition.ToString(),
                    ["selectedOrigin"] = independentlyValid.SelectedOrigin.ToString(),
                    ["publicationDisposition"] = independentlyValid.PublicationDisposition.ToString()
                });
                break;
            case "prior-observation-outcome-evaluation-mismatch":
                var alternateObservation = HtmlTransportEloObservation(scenario.Prior.Identity);
                alternateObservation.Validate();
                var originalPriorObservation = (await scenario.Repository.GetSourceCycleAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo))!.Observation!;
                var originalPriorReceipt = (await scenario.Repository.GetReceiptAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!;
                originalPriorObservation.Validate(); originalPriorReceipt.Validate();
                BundesligaContextSourceReceiptContract.ValidateAgainstObservation(originalPriorReceipt.Request, originalPriorObservation);
                BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(originalPriorReceipt.Request, new DateOnly(2026, 9, 6));
                var priorSourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo));
                var priorSourceMap = (await priorSourceReference.GetSnapshotAsync()).ToDictionary();
                var alternateObservationMap = new Dictionary<string, object>((IDictionary<string, object>)priorSourceMap["observation"], StringComparer.Ordinal);
                alternateObservationMap["disposition"] = alternateObservation.Disposition.ToString();
                alternateObservationMap["descriptorSha256"] = alternateObservation.DescriptorSha256;
                alternateObservationMap["descriptor"] = FirestoreMap(alternateObservation.DescriptorJson);
                alternateObservationMap["diagnostics"] = alternateObservation.Diagnostics.ToArray();
                alternateObservationMap["payload"] = null;
                await priorSourceReference.UpdateAsync(new Dictionary<string, object>
                {
                    ["observation"] = alternateObservationMap,
                    ["observationDigest"] = alternateObservation.ObservationDigest
                });
                await fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane)).UpdateAsync(new Dictionary<string, object>
                {
                    ["observationDigest"] = alternateObservation.ObservationDigest
                });
                var persistedAlternateObservation = (await scenario.Repository.GetSourceCycleAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo))!.Observation!;
                var persistedPriorReceipt = (await scenario.Repository.GetReceiptAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!;
                persistedAlternateObservation.Validate(); persistedPriorReceipt.Validate();
                BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(persistedPriorReceipt.Request, new DateOnly(2026, 9, 6));
                await Assert.That(persistedPriorReceipt.Request with { ObservationDigest = originalPriorReceipt.Request.ObservationDigest }).IsEqualTo(originalPriorReceipt.Request);
                await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAgainstObservation(persistedPriorReceipt.Request, persistedAlternateObservation)).Throws<InvalidDataException>();
                break;
            case "current-launch-seed": request = request with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LaunchSeed, PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published }; break;
            case "prior-freshness": await fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane)).UpdateAsync("activeConditions", new[] { "CLUB_ELO_STALE_GT_7_DAYS" }); break;
            case "hostile-snapshot": request = request with { SelectedSnapshotId = new string('f', 64) }; await scenario.Head.UpdateAsync("snapshotId", request.SelectedSnapshotId); break;
            case "selected-date": request = request with { SourceDates = request.SourceDates with { RatedAt = new DateOnly(2026, 9, 5) } }; break;
            case "missing-head": await scenario.Head.DeleteAsync(); break;
            case "wrong-scope-head": await scenario.Head.UpdateAsync("communityContext", "wrong-community"); break;
            case "malformed-head": await scenario.Head.UpdateAsync("snapshotId", 1L); break;
            case "extra-head-field": await scenario.Head.UpdateAsync("unexpected", true); break;
            case "moved-head": await scenario.Head.UpdateAsync("snapshotId", new string('f', 64)); break;
            case "wrong-current-freshness": request = request with { ActiveConditions = [BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days] }; break;
            default: throw new ArgumentOutOfRangeException(nameof(hostile));
        }
        var beforeFailure = await PersistedGraphFingerprintAsync();

        await Assert.That(() => scenario.Repository.RecordReceiptAsync(request)).Throws<InvalidDataException>();
        await AssertNoCurrentReceiptMutation(scenario.Current.Identity, healthIsReadable);
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeFailure);
    }

    [Test]
    public async Task HTML_retained_prior_freshness_reference_mismatch_is_state_conflict_without_current_receipt_mutation()
    {
        var scenario = await CreateHtmlRetainedScenarioAsync();
        var canonicalUtc = Now().AddDays(10);
        await fixture.Db.Collection(Cycles).Document(scenario.Prior.Identity.StorageId).UpdateAsync(new Dictionary<string, object>
        {
            ["startedAtUtc"] = BundesligaContextSourceContract.FormatUtc(canonicalUtc),
            ["stalenessReferenceAtUtc"] = BundesligaContextSourceContract.FormatUtc(canonicalUtc)
        });

        var outer = (await scenario.Repository.GetCycleAsync(scenario.Prior.Identity))!;
        var source = (await scenario.Repository.GetSourceCycleAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo))!;
        var receipt = (await scenario.Repository.GetReceiptAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!;
        var health = (await scenario.Repository.GetHealthAsync(scenario.Prior.Identity.Competition, scenario.Prior.Identity.Scope, BundesligaContextSource.ClubElo))!;
        var selection = health.CommunitySelections.Single();

        outer.Validate(); source.Validate(outer.ExpectedConsumers); receipt.Validate(); health.Validate(); source.Observation!.Validate();
        await Assert.That(outer.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That(source.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That(outer.StartedAtUtc).IsEqualTo(canonicalUtc);
        await Assert.That(outer.StalenessReferenceAtUtc).IsEqualTo(canonicalUtc);
        await Assert.That(outer.Identity).IsEqualTo(receipt.Request.Identity);
        await Assert.That(source.Identity).IsEqualTo(receipt.Request.Identity);
        await Assert.That(health.Competition).IsEqualTo(receipt.Request.Identity.Competition);
        await Assert.That(health.Scope).IsEqualTo(receipt.Request.Identity.Scope);
        await Assert.That(health.Source).IsEqualTo(receipt.Request.Source);
        await Assert.That(selection.ConsumerLaneId).IsEqualTo(receipt.Request.ConsumerLaneId);
        await Assert.That(selection.CommunityContext).IsEqualTo(receipt.Request.CommunityContext);
        await Assert.That(selection.SelectedSnapshotId).IsEqualTo(receipt.Request.SelectedSnapshotId);
        await Assert.That(selection.SelectedOrigin).IsEqualTo(receipt.Request.SelectedOrigin);
        await Assert.That(selection.RatedAt).IsEqualTo(receipt.Request.SourceDates.RatedAt);
        await Assert.That(selection.MembershipCapturedAt).IsEqualTo(receipt.Request.SourceDates.MembershipCapturedAt);
        await Assert.That(selection.MembershipEffectiveAt).IsEqualTo(receipt.Request.SourceDates.MembershipEffectiveAt);
        await Assert.That(selection.EnrichmentCapturedAt).IsEqualTo(receipt.Request.SourceDates.EnrichmentCapturedAt);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt.Request, source.Observation);

        InvalidDataException? directFailure = null;
        try
        {
            BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
                health, selection, receipt, source.Observation, outer,
                DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime));
        }
        catch (InvalidDataException exception) { directFailure = exception; }
        await Assert.That(directFailure).IsNotNull();
        await Assert.That(directFailure!.Message).IsEqualTo("Receipt freshness conditions do not match the cycle staleness reference.");

        var beforeFailure = await PersistedGraphFingerprintAsync();
        InvalidDataException? conflict = null;
        try { await scenario.Repository.RecordReceiptAsync(scenario.Request); }
        catch (InvalidDataException exception) { conflict = exception; }
        await Assert.That(conflict).IsNotNull();
        await Assert.That(conflict!.Message).IsEqualTo("STATE_CONFLICT");
        await AssertNoCurrentReceiptMutation(scenario.Current.Identity);
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeFailure);
    }

    [Test]
    public async Task Production_prior_lane_and_community_bindings_reject_valid_cross_lane_receipts_without_mutation()
    {
        var repository = CreateRepository();
        var prior = ProductionCycle(606, [BundesligaContextSource.ClubElo]);
        await CompleteRejectedEloCycle(repository, prior);

        var outer = (await repository.GetCycleAsync(prior.Identity))!;
        var source = (await repository.GetSourceCycleAsync(prior.Identity, BundesligaContextSource.ClubElo))!;
        var health = (await repository.GetHealthAsync(prior.Identity.Competition, prior.Identity.Scope, BundesligaContextSource.ClubElo))!;
        var firstLane = "pes-squad-context";
        var secondLane = "schadensfresse-context";
        var firstSelection = health.CommunitySelections.Single(selection => selection.ConsumerLaneId == firstLane);
        var secondReceipt = (await repository.GetReceiptAsync(prior.Identity, BundesligaContextSource.ClubElo, secondLane))!;

        outer.Validate(); source.Validate(outer.ExpectedConsumers); health.Validate(); secondReceipt.Validate(); source.Observation!.Validate();
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(secondReceipt.Request, source.Observation);
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(secondReceipt.Request, DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime));
        await Assert.That(firstSelection.ConsumerLaneId).IsNotEqualTo(secondReceipt.Request.ConsumerLaneId);
        await Assert.That(firstSelection.CommunityContext).IsNotEqualTo(secondReceipt.Request.CommunityContext);

        var beforeFailure = await PersistedGraphFingerprintAsync();
        await Assert.That(() => BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
            health, firstSelection, secondReceipt, source.Observation!, outer,
            DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime))).Throws<InvalidDataException>();
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeFailure);
    }

    [Test]
    public async Task Authoritative_prior_selection_rejects_a_valid_outer_with_a_different_cycle_identity_without_mutation()
    {
        var scenario = await CreateHtmlRetainedScenarioAsync();
        var outer = (await scenario.Repository.GetCycleAsync(scenario.Prior.Identity))!;
        var source = (await scenario.Repository.GetSourceCycleAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo))!;
        var receipt = (await scenario.Repository.GetReceiptAsync(scenario.Prior.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!;
        var health = (await scenario.Repository.GetHealthAsync(scenario.Prior.Identity.Competition, scenario.Prior.Identity.Scope, BundesligaContextSource.ClubElo))!;
        var selection = health.CommunitySelections.Single();
        var differentIdentityOuter = outer with { Identity = scenario.Current.Identity };

        outer.Validate(); source.Validate(outer.ExpectedConsumers); receipt.Validate(); health.Validate(); source.Observation!.Validate(); differentIdentityOuter.Validate();
        await Assert.That(outer.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That(source.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That(differentIdentityOuter.Identity).IsNotEqualTo(receipt.Request.Identity);
        await Assert.That(differentIdentityOuter.BundleSha256).IsEqualTo(receipt.Request.BundleDigest);
        await Assert.That(differentIdentityOuter with { Identity = outer.Identity }).IsEqualTo(outer);
        await Assert.That(selection.ConsumerLaneId).IsEqualTo(receipt.Request.ConsumerLaneId);
        await Assert.That(selection.CommunityContext).IsEqualTo(receipt.Request.CommunityContext);
        await Assert.That(selection.SelectedSnapshotId).IsEqualTo(receipt.Request.SelectedSnapshotId);
        await Assert.That(selection.SelectedOrigin).IsEqualTo(receipt.Request.SelectedOrigin);
        await Assert.That(selection.RatedAt).IsEqualTo(receipt.Request.SourceDates.RatedAt);
        await Assert.That(selection.MembershipCapturedAt).IsEqualTo(receipt.Request.SourceDates.MembershipCapturedAt);
        await Assert.That(selection.MembershipEffectiveAt).IsEqualTo(receipt.Request.SourceDates.MembershipEffectiveAt);
        await Assert.That(selection.EnrichmentCapturedAt).IsEqualTo(receipt.Request.SourceDates.EnrichmentCapturedAt);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt.Request, source.Observation);
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(receipt.Request, DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime));

        var beforeFailure = await PersistedGraphFingerprintAsync();
        InvalidDataException? directFailure = null;
        try
        {
            BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
                health, selection, receipt, source.Observation, differentIdentityOuter,
                DateOnly.FromDateTime(differentIdentityOuter.StalenessReferenceAtUtc.UtcDateTime));
        }
        catch (InvalidDataException exception) { directFailure = exception; }
        await Assert.That(directFailure).IsNotNull();
        await Assert.That(directFailure!.Message).IsEqualTo("Health selection does not match its authoritative prior receipt.");
        await Assert.That(await PersistedGraphFingerprintAsync()).IsEqualTo(beforeFailure);
    }

    [Test]
    public async Task RecordReceiptAsync_allows_initial_roster_fallback_publication_without_a_retained_head()
    {
        var repository = CreateRepository();
        var cycle = Cycle();
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var initialHealth = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(initialHealth!.LastCompletedCycleId).IsNull();
        await Assert.That(initialHealth.CommunitySelections).IsEmpty();

        var receipt = await repository.RecordReceiptAsync(Receipt(cycle.Identity, observation, digest) with
        {
            PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published
        });

        await Assert.That(receipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
        await Assert.That(receipt.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.FallbackSeed);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
    }

    [Test]
    public async Task Aborting_a_finalized_source_preserves_its_canonical_readable_observation()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.FinalizedPayloadUnavailable);
        var source = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);
        await Assert.That(source!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Aborted);
        await Assert.That(source.ObservationDigest).IsEqualTo(observation.ObservationDigest);
        await Assert.That(source.Observation!.Source).IsEqualTo(observation.Source);
        await Assert.That(source.Observation.AttemptId).IsEqualTo(observation.AttemptId);
        await Assert.That(source.Observation.ObservedAtUtc).IsEqualTo(observation.ObservedAtUtc);
        await Assert.That(source.Observation.Disposition).IsEqualTo(observation.Disposition);
        await Assert.That(source.Observation.DescriptorJson).IsEqualTo(observation.DescriptorJson);
        await Assert.That(source.Observation.Payload).IsEqualTo(observation.Payload);
        await Assert.That(source.Observation.Diagnostics).IsEquivalentTo(observation.Diagnostics);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Abort_and_supersession_preserve_completed_sources_and_abort_only_incomplete_sources(bool supersede)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1467-7000-8000-000000000005", [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var elo = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), elo, Now().AddMinutes(1));
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var roster = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), roster, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        await repository.RecordReceiptAsync(Receipt(cycle.Identity, roster, digest));
        var completedBeforeAbort = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);

        if (supersede)
        {
            var newer = Cycle("0198f865-1468-7000-8000-000000000006", [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
            var started = await repository.CreateOrResumeCycleWithResultAsync(newer);
            await Assert.That(started.SupersededSources).Contains(BundesligaContextSource.ClubElo);
        }
        else
        {
            await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing);
        }

        var completedAfterAbort = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);
        var incompleteAfterAbort = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo);
        await Assert.That(completedAfterAbort!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That(completedAfterAbort.CompletedAtUtc).IsEqualTo(completedBeforeAbort!.CompletedAtUtc);
        await Assert.That(completedAfterAbort.CreateCanonicalUtf8(cycle.ExpectedConsumers).SequenceEqual(completedBeforeAbort.CreateCanonicalUtf8(cycle.ExpectedConsumers))).IsTrue();
        await Assert.That(incompleteAfterAbort!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Aborted);
        await Assert.That(incompleteAfterAbort.CompletedAtUtc).IsNull();
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.AbortCode).IsEqualTo(supersede ? BundesligaContextSourceError.SupersededIncompleteCycle : BundesligaContextSourceError.LocalHandoffMissing);
    }

    [Test]
    public async Task Pre_reservation_abort_is_persisted_readable_and_exactly_replayable_without_source_documents()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        var first = await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.AcquisitionInterrupted);
        var read = await repository.GetCycleAsync(cycle.Identity);
        var replay = await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing);

        await Assert.That(first.BundleSha256).IsNull(); await Assert.That(first.ArtifactName).IsNull();
        await Assert.That(read!.CreateCanonicalUtf8().SequenceEqual(first.CreateCanonicalUtf8())).IsTrue();
        await Assert.That(replay.CreateCanonicalUtf8().SequenceEqual(first.CreateCanonicalUtf8())).IsTrue();
        await Assert.That(replay.AbortCode).IsEqualTo(BundesligaContextSourceError.AcquisitionInterrupted);
        await Assert.That((await fixture.Db.Collection(Observations).GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters))!.ConsecutiveFailures.Handoff).IsEqualTo(1);
    }

    [Test]
    public async Task Abort_recomputes_a_newly_crossed_staleness_threshold_from_retained_source_dates()
    {
        var repository = CreateRepository();
        var baselineDate = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        var baseline = ProductionCycle(470, [BundesligaContextSource.ClubElo]) with { StartedAtUtc = baselineDate, StalenessReferenceAtUtc = baselineDate };
        await CompleteRejectedEloCycle(repository, baseline);
        var beforeAbort = await repository.GetHealthAsync(baseline.Identity.Competition, baseline.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(beforeAbort!.ActiveConditions).DoesNotContain(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        await Assert.That(beforeAbort.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Closed);

        var dueDate = baselineDate.AddDays(1);
        var due = ProductionCycle(471, [BundesligaContextSource.ClubElo]) with { StartedAtUtc = dueDate, StalenessReferenceAtUtc = dueDate };
        await repository.CreateOrResumeCycleAsync(due);
        await repository.AbortCycleAsync(due.Identity, BundesligaContextSourceError.LocalHandoffMissing);

        var aborted = await repository.GetHealthAsync(due.Identity.Competition, due.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(aborted!.LastSuccessfulSourceDates.RatedAt).IsEqualTo(new DateOnly(2026, 8, 20));
        await Assert.That(aborted.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        await Assert.That(aborted.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Open);
        await Assert.That(aborted.DesiredIssueProjection.BodySha256).IsEqualTo(BundesligaContextSourceHealth.HashIssueBody(
            BundesligaContextSourceHealth.CreateIssueBody(aborted.DesiredIssueProjection.Marker, aborted.Competition, aborted.Source, aborted.Watermark, aborted.ActiveConditions)));
    }

    [Test]
    [Arguments("direct-abort")]
    [Arguments("supersession")]
    [Arguments("omitted-source")]
    public async Task Abort_and_supersession_rederive_unknown_enrichment_from_mixed_retained_selections(string transition)
    {
        var repository = CreateRepository();
        var baseline = ProductionCycle(500);
        await CompleteMixedEnrichmentRosterCycle(repository, baseline);
        var incomplete = ProductionCycle(501);
        await repository.CreateOrResumeCycleAsync(incomplete);

        if (transition == "direct-abort")
        {
            await repository.AbortCycleAsync(incomplete.Identity, BundesligaContextSourceError.LocalHandoffMissing);
        }
        else
        {
            var nextSources = transition == "omitted-source" ? new[] { BundesligaContextSource.ClubElo } : new[] { BundesligaContextSource.Rosters };
            await repository.CreateOrResumeCycleAsync(ProductionCycle(502, nextSources));
        }

        var health = await repository.GetHealthAsync(incomplete.Identity.Competition, incomplete.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(health!.LastSuccessfulSourceDates.EnrichmentCapturedAt).IsEqualTo(new DateOnly(2026, 8, 25));
        await Assert.That(health.CommunitySelections.Any(selection => selection.EnrichmentCapturedAt is null)).IsTrue();
        await Assert.That(health.CommunitySelections.Any(selection => selection.EnrichmentCapturedAt is not null)).IsTrue();
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
        await Assert.That(health.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Open);
    }

    [Test]
    public async Task Abort_after_bundle_verification_preserves_digest_without_inventing_an_artifact_reservation()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);

        var aborted = await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing);
        await Assert.That(aborted.BundleSha256).IsEqualTo(digest);
        await Assert.That(aborted.ArtifactName).IsNull();
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.CreateCanonicalUtf8().SequenceEqual(aborted.CreateCanonicalUtf8())).IsTrue();
    }

    [Test]
    public async Task Lease_expiry_aborts_every_existing_source_and_aborted_replay_reconciles_orphans_once()
    {
        var repository = CreateRepository(); var cycle = Cycle(sources: [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now()); var elo = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), elo, Now().AddMinutes(1));
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now());

        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('3'), Now().AddMinutes(10));
        foreach (var source in cycle.EnabledSources)
        {
            var state = await repository.GetSourceCycleAsync(cycle.Identity, source);
            await Assert.That(state!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Aborted);
            await Assert.That(state.AbortCode).IsEqualTo(BundesligaContextSourceError.AcquisitionInterrupted);
            await Assert.That((await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, source))!.ConsecutiveFailures.Handoff).IsEqualTo(1);
        }
        await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo))!.ObservationDigest).IsEqualTo(elo.ObservationDigest);

        var eloReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
        await eloReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Finalized", ["abortCode"] = null! });
        await repository.AbortCycleAsync(cycle.Identity, BundesligaContextSourceError.AcquisitionInterrupted);
        await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Aborted);
        await Assert.That((await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.ClubElo))!.ConsecutiveFailures.Handoff).IsEqualTo(1);
    }

    [Test]
    public async Task Outer_completion_rejects_a_status_only_complete_sibling_with_partial_receipts()
    {
        var repository = CreateRepository(); var cycle = Cycle(sources: [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now()); var elo = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), elo, Now().AddMinutes(1));
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now()); var roster = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('2'), roster, Now().AddMinutes(1)); var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var eloReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
        await eloReference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Complete", ["receivedConsumers"] = Array.Empty<string>(), ["completedAtUtc"] = BundesligaContextSourceContract.FormatUtc(Now().AddMinutes(2)) });

        await Assert.That(() => repository.RecordReceiptAsync(Receipt(cycle.Identity, roster, digest))).Throws<InvalidDataException>();
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Persisted_source_cycle_rejects_receipt_prefix_without_finalization_and_full_prefix_outside_complete()
    {
        var repository = CreateRepository();
        var missingFinalization = ProductionCycle(460);
        await repository.CreateOrResumeCycleAsync(missingFinalization);
        await repository.ClaimSourceAsync(missingFinalization.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var missingFinalizationReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(missingFinalization.Identity, BundesligaContextSource.Rosters));
        await missingFinalizationReference.UpdateAsync(new Dictionary<string, object>
        {
            ["status"] = "Aborted",
            ["abortCode"] = "ACQUISITION_INTERRUPTED",
            ["receivedConsumers"] = new[] { BundesligaContextSourceContract.ProductionConsumers[0] }
        });

        await Assert.That(() => repository.GetSourceCycleAsync(missingFinalization.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await missingFinalizationReference.UpdateAsync(new Dictionary<string, object>
        {
            ["status"] = "Claimed",
            ["abortCode"] = null!,
            ["receivedConsumers"] = Array.Empty<string>()
        });

        var fullPrefix = ProductionCycle(461);
        await repository.CreateOrResumeCycleAsync(fullPrefix);
        await repository.ClaimSourceAsync(fullPrefix.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var observation = Observation(fullPrefix.Identity);
        await repository.FinalizeSourceAsync(fullPrefix.Identity, BundesligaContextSource.Rosters, Token('2'), observation, Now().AddMinutes(1));
        var fullPrefixReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(fullPrefix.Identity, BundesligaContextSource.Rosters));
        await fullPrefixReference.UpdateAsync(new Dictionary<string, object>
        {
            ["status"] = "Aborted",
            ["abortCode"] = "ACQUISITION_INTERRUPTED",
            ["receivedConsumers"] = BundesligaContextSourceContract.ProductionConsumers.ToArray()
        });

        await Assert.That(() => repository.GetSourceCycleAsync(fullPrefix.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Metadata_unchanged_finalize_requires_the_completed_accepted_tuple_and_retained_descriptor()
    {
        var repository = CreateRepository(); var firstCycle = Cycle("0198f865-1467-7000-8000-000000000020");
        await CompleteRejectedCycle(repository, firstCycle);
        var priorObservation = Observation(firstCycle.Identity);
        var next = Cycle("0198f865-1468-7000-8000-000000000021"); await repository.CreateOrResumeCycleAsync(next);
        var restartedRepository = CreateRepository();
        var retained = await restartedRepository.GetRetainedRosterDescriptorAsync(firstCycle.Identity.Competition, firstCycle.Identity.Scope);
        await Assert.That(retained!.DescriptorSha256).IsEqualTo(priorObservation.DescriptorSha256);
        await Assert.That(retained.Evaluation).IsEqualTo("SourceDateRejected");
        await Assert.That(retained.Diagnostics).IsEquivalentTo(["UNKNOWN_SOURCE_DATE"]);
        await repository.ClaimSourceAsync(next.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var metadata = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(next.Identity, BundesligaContextSource.Rosters), Now(), BundesligaContextSourceDisposition.MetadataUnchanged, MetadataUnchangedDescriptor(priorObservation.DescriptorSha256), null, []);
        await Assert.That(() => repository.FinalizeSourceAsync(next.Identity, BundesligaContextSource.Rosters, Token('2'), metadata with { DescriptorJson = MetadataUnchangedDescriptor(new string('f', 64)) }, Now().AddMinutes(1))).Throws<InvalidDataException>();
        await Assert.That(() => repository.FinalizeSourceAsync(next.Identity, BundesligaContextSource.Rosters, Token('2'), metadata with { DescriptorJson = MetadataUnchangedDescriptor(priorObservation.DescriptorSha256, "Eligible") }, Now().AddMinutes(1))).Throws<InvalidDataException>();
        await Assert.That(() => repository.FinalizeSourceAsync(next.Identity, BundesligaContextSource.Rosters, Token('2'), metadata with { DescriptorJson = MetadataUnchangedDescriptor(priorObservation.DescriptorSha256, diagnostics: []) }, Now().AddMinutes(1))).Throws<InvalidDataException>();
        var finalized = await repository.FinalizeSourceAsync(next.Identity, BundesligaContextSource.Rosters, Token('2'), metadata, Now().AddMinutes(1));
        await Assert.That(finalized.ObservationDigest).IsEqualTo(metadata.ObservationDigest);
        var bundle = new string('f', 64);
        await repository.TransitionCycleAsync(next.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(next.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var retainedConditions = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterMembershipRejected,
            BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days]);
        var metadataReceipt = new BundesligaContextSourceReceiptRequest(next.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity, metadata.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.MetadataUnchanged, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.Unchanged, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), retainedConditions);
        await Assert.That(() => repository.RecordReceiptAsync(metadataReceipt with { SelectedSnapshotId = new string('f', 64) })).Throws<InvalidDataException>();
        await Assert.That(() => repository.RecordReceiptAsync(metadataReceipt with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood })).Throws<InvalidDataException>();
        await Assert.That(() => repository.RecordReceiptAsync(metadataReceipt with { SourceDates = metadataReceipt.SourceDates with { MembershipEffectiveAt = new DateOnly(2026, 8, 21) } })).Throws<InvalidDataException>();
        await Assert.That(() => repository.RecordReceiptAsync(metadataReceipt with { CarriedFields = metadataReceipt.CarriedFields with { AgeCount = 1 } })).Throws<InvalidDataException>();
        await repository.RecordReceiptAsync(metadataReceipt);

        var retainedAfterMetadataReplay = await CreateRepository().GetRetainedRosterDescriptorAsync(next.Identity.Competition, next.Identity.Scope);
        await Assert.That(retainedAfterMetadataReplay!.DescriptorSha256).IsEqualTo(priorObservation.DescriptorSha256);
        await Assert.That(retainedAfterMetadataReplay.Evaluation).IsEqualTo("SourceDateRejected");
        await Assert.That(retainedAfterMetadataReplay.Diagnostics).IsEquivalentTo(["UNKNOWN_SOURCE_DATE"]);
    }

    [Test]
    public async Task Metadata_unchanged_receipt_replay_after_a_later_changed_selection_returns_its_original_time()
    {
        var repository = CreateRepository();
        var first = Cycle("0198f865-1467-7000-8000-000000000050");
        await CompleteRejectedCycle(repository, first);
        var originalObservation = Observation(first.Identity);

        var metadataCycle = Cycle("0198f865-1468-7000-8000-000000000051");
        await repository.CreateOrResumeCycleAsync(metadataCycle);
        await repository.ClaimSourceAsync(metadataCycle.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var metadataObservation = new BundesligaContextSourceObservation(
            BundesligaContextSource.Rosters,
            BundesligaContextSourceHashing.AttemptId(metadataCycle.Identity, BundesligaContextSource.Rosters),
            Now(),
            BundesligaContextSourceDisposition.MetadataUnchanged,
            MetadataUnchangedDescriptor(originalObservation.DescriptorSha256),
            null,
            []);
        await repository.FinalizeSourceAsync(metadataCycle.Identity, BundesligaContextSource.Rosters, Token('2'), metadataObservation, Now().AddMinutes(1));
        var metadataBundle = new string('f', 64);
        await repository.TransitionCycleAsync(metadataCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, metadataBundle);
        await repository.TransitionCycleAsync(metadataCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, metadataBundle);
        var metadataConditions = BundesligaContextSourceHealth.OrderConditions([
            BundesligaContextSourceHealthCondition.RosterMembershipRejected,
            BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
            BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days]);
        var metadataRequest = new BundesligaContextSourceReceiptRequest(metadataCycle.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity, metadataObservation.ObservationDigest, metadataBundle, BundesligaContextSourceSelectionDisposition.MetadataUnchanged, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.Unchanged, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), metadataConditions);
        var originalReceipt = await repository.RecordReceiptAsync(metadataRequest);

        var laterRepository = CreateRepository(Now().AddMinutes(12));
        var laterCycle = Cycle("0198f865-1469-7000-8000-000000000052");
        await laterRepository.CreateOrResumeCycleAsync(laterCycle);
        await laterRepository.ClaimSourceAsync(laterCycle.Identity, BundesligaContextSource.Rosters, Token('3'), Now());
        var laterObservation = Observation(laterCycle.Identity, acquisitionReason: "RemoteIdentityChanged", etag: "y");
        await laterRepository.FinalizeSourceAsync(laterCycle.Identity, BundesligaContextSource.Rosters, Token('3'), laterObservation, Now().AddMinutes(1));
        var laterBundle = new string('d', 64);
        await laterRepository.TransitionCycleAsync(laterCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, laterBundle);
        await laterRepository.TransitionCycleAsync(laterCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, laterBundle);
        var changedSelection = Receipt(laterCycle.Identity, laterObservation, laterBundle) with
        {
            SelectedSnapshotId = new string('d', 64),
            SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood
        };
        var laterReceipt = await laterRepository.RecordReceiptAsync(changedSelection);
        var laterHealth = await laterRepository.GetHealthAsync(laterCycle.Identity.Competition, laterCycle.Identity.Scope, BundesligaContextSource.Rosters);

        var replay = await laterRepository.RecordReceiptAsync(metadataRequest);
        await Assert.That(() => laterRepository.RecordReceiptAsync(metadataRequest with { SelectedSnapshotId = new string('e', 64) })).Throws<InvalidDataException>();
        var receiptsAfterReplay = await fixture.Db.Collection(Receipts).GetSnapshotAsync();
        var laterHealthAfterReplay = await laterRepository.GetHealthAsync(laterCycle.Identity.Competition, laterCycle.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(laterReceipt.RecordedAtUtc).IsNotEqualTo(originalReceipt.RecordedAtUtc);
        await Assert.That(laterHealth!.CommunitySelections.Single().SelectedSnapshotId).IsEqualTo(changedSelection.SelectedSnapshotId);
        await Assert.That(laterHealth.CommunitySelections.Single().SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(laterHealthAfterReplay!.CommunitySelections.Single().SelectedSnapshotId).IsEqualTo(changedSelection.SelectedSnapshotId);
        await Assert.That(laterHealthAfterReplay.CommunitySelections.Single().SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(originalReceipt.RecordedAtUtc);
        await Assert.That(replay.Request.SelectedSnapshotId).IsEqualTo(metadataRequest.SelectedSnapshotId);
        await Assert.That(receiptsAfterReplay.Documents.Count).IsEqualTo(3);
        await Assert.That(() => laterRepository.RecordReceiptAsync(metadataRequest with { SelectedSnapshotId = new string('f', 64) })).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("revision")]
    [Arguments("remote-identity")]
    [Arguments("policy")]
    public async Task Retained_descriptor_rehydration_rejects_an_independently_valid_health_tuple_mismatch(string mismatch)
    {
        var repository = CreateRepository(); var cycle = Cycle("0198f865-1467-7000-8000-000000000023");
        await CompleteRejectedCycle(repository, cycle);
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        var (field, value) = mismatch switch
        {
            "revision" => ("rosterRevisionState.accepted.revision", (object)new string('f', 40)),
            "remote-identity" => ("rosterRevisionState.accepted.remoteIdentity.etag", "different"),
            "policy" => ("rosterRevisionState.accepted.policySha256", new string('f', 64)),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch))
        };
        await healthReference.UpdateAsync(field, value);

        _ = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(() => repository.GetRetainedRosterDescriptorAsync(cycle.Identity.Competition, cycle.Identity.Scope)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Firestore_persists_native_canonical_nested_shapes_and_rejects_unknown_nested_fields()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await CompleteRejectedCycle(repository, cycle);
        var sourceDocument = (await fixture.Db.Collection(Observations).GetSnapshotAsync()).Documents.Single();
        var sourceMap = sourceDocument.ToDictionary();
        await Assert.That(sourceMap["observation"] is IDictionary<string, object>).IsTrue();
        var healthDocument = (await fixture.Db.Collection(Health).GetSnapshotAsync()).Documents.Single();
        var healthMap = healthDocument.ToDictionary();
        await Assert.That(healthMap["lastSuccessfulSourceDates"] is IDictionary<string, object>).IsTrue();
        await Assert.That(healthMap["rosterRevisionState"] is IDictionary<string, object>).IsTrue();
        await Assert.That(healthMap["communitySelections"] is IEnumerable<object>).IsTrue();

        await sourceDocument.Reference.UpdateAsync("observation.unknown", true);
        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await healthDocument.Reference.UpdateAsync("rosterRevisionState.accepted.policySha256", "bad");
        await Assert.That(() => repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();

        var production = ProductionCycle(); await repository.CreateOrResumeCycleAsync(production);
        var productionHealth = (await fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(production.Identity.Competition, production.Identity.ScopeValue, BundesligaContextSource.Rosters)).GetSnapshotAsync());
        await Assert.That(productionHealth.ToDictionary()["desiredIssueProjection"] is IDictionary<string, object>).IsTrue();
        await productionHealth.Reference.UpdateAsync("desiredIssueProjection.marker", "<!-- wrong -->");
        await Assert.That(() => repository.GetHealthAsync(production.Identity.Competition, production.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await productionHealth.Reference.UpdateAsync("desiredIssueProjection.marker", "<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->");
        await productionHealth.Reference.UpdateAsync("desiredIssueProjection.unknown", true);
        await Assert.That(() => repository.GetHealthAsync(production.Identity.Competition, production.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("fallback-with-capture")]
    [Arguments("duckdb-without-capture")]
    [Arguments("mixed-without-capture")]
    public async Task Strict_health_read_rejects_hostile_roster_origin_and_membership_capture_mutations(string mutation)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000080");
        await CompleteRejectedCycle(repository, cycle);
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        var healthMap = (await healthReference.GetSnapshotAsync()).ToDictionary();
        var selections = ((IEnumerable<object>)healthMap["communitySelections"])
            .Select(value => new Dictionary<string, object>((IDictionary<string, object>)value, StringComparer.Ordinal))
            .ToList();
        switch (mutation)
        {
            case "fallback-with-capture":
                selections[0]["membershipCapturedAt"] = "2026-08-20";
                break;
            case "duckdb-without-capture":
                selections[0]["selectedOrigin"] = "DuckDb";
                break;
            case "mixed-without-capture":
                selections[0]["selectedOrigin"] = "Mixed";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        await healthReference.UpdateAsync("communitySelections", selections);

        await Assert.That(() => repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("metadataByteLength")]
    [Arguments("remoteIdentityBefore.byteLength")]
    [Arguments("remoteIdentityAfter.byteLength")]
    [Arguments("rawByteLength")]
    public async Task Strict_source_read_rejects_roster_integer_descriptor_fields_stored_as_doubles(string descriptorField)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000081");
        await CompleteRejectedCycle(repository, cycle);
        var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.Rosters));
        await sourceReference.UpdateAsync($"observation.descriptor.{descriptorField}", 1.0);

        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("raw-byte-length")]
    [Arguments("global-rank")]
    public async Task Strict_source_read_rejects_Club_Elo_integer_descriptor_fields_stored_as_doubles(string mutation)
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000082", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), EligibleEloObservation(cycle.Identity), Now().AddMinutes(1));
        var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
        if (mutation == "raw-byte-length")
        {
            await sourceReference.UpdateAsync("observation.descriptor.rawByteLength", 3.0);
        }
        else
        {
            var sourceMap = (await sourceReference.GetSnapshotAsync()).ToDictionary();
            var observationMap = (IDictionary<string, object>)sourceMap["observation"];
            var descriptorMap = (IDictionary<string, object>)observationMap["descriptor"];
            var rows = ((IEnumerable<object>)descriptorMap["sourceRows"])
                .Select(value => new Dictionary<string, object>((IDictionary<string, object>)value, StringComparer.Ordinal))
                .ToList();
            rows[0]["globalRank"] = 1.0;
            await sourceReference.UpdateAsync("observation.descriptor.sourceRows", rows);
        }

        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_Club_Elo_descriptor_round_trips_with_strict_nested_integer_reconstruction()
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000083", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var observation = HtmlEloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), observation, Now().AddMinutes(1));

        var roundTripped = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo);
        await Assert.That(roundTripped!.Observation!.DescriptorSha256).IsEqualTo(observation.DescriptorSha256);
        var sourceReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
        await sourceReference.UpdateAsync("observation.descriptor.response.redirectCount", 0.0);
        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_Club_Elo_Firestore_reconstruction_rejects_every_native_integer_coercion_and_nested_shape_hostile()
    {
        var mutations = new (string Field, double Value)[]
        {
            ("rawByteLength", 3.0), ("response.statusCode", 200.0), ("response.redirectCount", 0.0), ("response.declaredContentLength", 3.0),
            ("sourceRows.0.globalRank", 1.0), ("sourceRows.0.elo", 1500.0)
        };
        var sequence = 84;
        foreach (var mutation in mutations)
        {
            var repository = CreateRepository();
            var cycle = Cycle($"0198f865-1468-7000-8000-{sequence++:D12}", [BundesligaContextSource.ClubElo]);
            await repository.CreateOrResumeCycleAsync(cycle);
            await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
            await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), HtmlEloObservation(cycle.Identity), Now().AddMinutes(1));
            var source = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.ClubElo));
            if (mutation.Field.StartsWith("sourceRows.", StringComparison.Ordinal))
            {
                var sourceMap = (await source.GetSnapshotAsync()).ToDictionary();
                var observationMap = (IDictionary<string, object>)sourceMap["observation"];
                var descriptorMap = (IDictionary<string, object>)observationMap["descriptor"];
                var rows = ((IEnumerable<object>)descriptorMap["sourceRows"])
                    .Select(value => new Dictionary<string, object>((IDictionary<string, object>)value, StringComparer.Ordinal))
                    .ToList();
                rows[0][mutation.Field.EndsWith("globalRank", StringComparison.Ordinal) ? "globalRank" : "elo"] = mutation.Value;
                await source.UpdateAsync("observation.descriptor.sourceRows", rows);
            }
            else
                await source.UpdateAsync($"observation.descriptor.{mutation.Field}", mutation.Value);
            await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.ClubElo)).Throws<InvalidDataException>();
        }

        var shapedRepository = CreateRepository();
        var shapedCycle = Cycle("0198f865-1468-7000-8000-000000000091", [BundesligaContextSource.ClubElo]);
        await shapedRepository.CreateOrResumeCycleAsync(shapedCycle);
        await shapedRepository.ClaimSourceAsync(shapedCycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        await shapedRepository.FinalizeSourceAsync(shapedCycle.Identity, BundesligaContextSource.ClubElo, Token('1'), HtmlEloObservation(shapedCycle.Identity), Now().AddMinutes(1));
        var shapedSource = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(shapedCycle.Identity, BundesligaContextSource.ClubElo));
        await shapedSource.UpdateAsync("observation.descriptor.response.unknown", true);
        await Assert.That(() => shapedRepository.GetSourceCycleAsync(shapedCycle.Identity, BundesligaContextSource.ClubElo)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_Club_Elo_receipts_persist_exact_replay_and_reduce_health_once_for_accepted_and_retained_rejection()
    {
        var repository = CreateRepository();
        var acceptedCycle = Cycle("0198f865-1468-7000-8000-000000000092", [BundesligaContextSource.ClubElo]);
        var accepted = HtmlEloObservation(acceptedCycle.Identity); var bundle = new string('e', 64);
        await repository.CreateOrResumeCycleAsync(acceptedCycle);
        await repository.ClaimSourceAsync(acceptedCycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        await repository.FinalizeSourceAsync(acceptedCycle.Identity, BundesligaContextSource.ClubElo, Token('1'), accepted, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(acceptedCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(acceptedCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var acceptedRequest = HtmlReceipt(acceptedCycle.Identity, accepted, bundle, BundesligaContextSourceSelectionDisposition.NetworkAccepted, BundesligaContextSourceSelectedOrigin.NetworkCandidate, BundesligaContextSourcePublicationDisposition.Published, new DateOnly(2026, 9, 4), []);
        var acceptedFirst = await repository.RecordReceiptAsync(acceptedRequest);
        var acceptedReplay = await repository.RecordReceiptAsync(acceptedRequest);
        await Assert.That(acceptedReplay.RecordedAtUtc).IsEqualTo(acceptedFirst.RecordedAtUtc);
        await Assert.That((await repository.GetReceiptAsync(acceptedCycle.Identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!.RecordedAtUtc).IsEqualTo(acceptedFirst.RecordedAtUtc);
        await Assert.That((await repository.GetSourceCycleAsync(acceptedCycle.Identity, BundesligaContextSource.ClubElo))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Complete);
        await Assert.That((await repository.GetCycleAsync(acceptedCycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Count).IsEqualTo(1);
        var retainedScope = new DocumentPublicationScope(acceptedCycle.Identity.Competition, acceptedRequest.CommunityContext, BundesligaDocumentPublication.ClubEloPublicationSet);
        await fixture.Db.Collection(Heads).Document(DocumentPublicationContract.ComputeHeadId(retainedScope)).SetAsync(new Dictionary<string, object>
        {
            ["competition"] = retainedScope.Competition,
            ["communityContext"] = retainedScope.CommunityContext,
            ["publicationSet"] = retainedScope.PublicationSet,
            ["snapshotId"] = acceptedRequest.SelectedSnapshotId
        });

        var rejectedCycle = Cycle("0198f865-1468-7000-8000-000000000093", [BundesligaContextSource.ClubElo]);
        var rejected = HtmlTransportEloObservation(rejectedCycle.Identity);
        await repository.CreateOrResumeCycleAsync(rejectedCycle);
        await repository.ClaimSourceAsync(rejectedCycle.Identity, BundesligaContextSource.ClubElo, Token('2'), Now());
        await repository.FinalizeSourceAsync(rejectedCycle.Identity, BundesligaContextSource.ClubElo, Token('2'), rejected, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(rejectedCycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(rejectedCycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var rejectedRequest = HtmlReceipt(rejectedCycle.Identity, rejected, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted, new DateOnly(2026, 9, 4), [BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloSourceRejected]);
        var rejectedFirst = await repository.RecordReceiptAsync(rejectedRequest);
        var rejectedReplay = await repository.RecordReceiptAsync(rejectedRequest);
        await Assert.That(rejectedReplay.RecordedAtUtc).IsEqualTo(rejectedFirst.RecordedAtUtc);
        await Assert.That((await repository.GetCycleAsync(rejectedCycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Count).IsEqualTo(2);
        var health = (await repository.GetHealthAsync(rejectedCycle.Identity.Competition, rejectedCycle.Identity.Scope, BundesligaContextSource.ClubElo))!;
        await Assert.That(health.ConsecutiveFailures.Acquisition).IsEqualTo(1);
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.ClubEloSourceRejected);
    }

    [Test]
    public async Task Strict_read_rejects_unknown_root_field_and_scope_corruption()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        var reference = fixture.Db.Collection(Cycles).Document(cycle.Identity.StorageId);
        await reference.UpdateAsync("unknown", true);
        await Assert.That(() => repository.GetCycleAsync(cycle.Identity)).Throws<InvalidDataException>();
        await reference.DeleteAsync();
        foreach (var health in (await fixture.Db.Collection(Health).GetSnapshotAsync()).Documents) await health.Reference.DeleteAsync();
        await repository.CreateOrResumeCycleAsync(cycle); await reference.UpdateAsync("scope", "Development");
        await Assert.That(() => repository.GetCycleAsync(cycle.Identity)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Existing_cycle_resume_fails_closed_when_enabled_source_health_is_missing()
    {
        var repository = CreateRepository(); var cycle = Cycle();
        await repository.CreateOrResumeCycleAsync(cycle);
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await healthReference.DeleteAsync();

        await Assert.That(() => repository.CreateOrResumeCycleAsync(cycle)).Throws<InvalidDataException>();
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Claiming);
    }

    [Test]
    public async Task Existing_cycle_resume_fails_closed_when_enabled_source_health_watermark_is_behind()
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000051");
        await repository.CreateOrResumeCycleAsync(cycle);
        var older = Cycle("0198f865-1467-7000-8000-000000000050");
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await healthReference.UpdateAsync("watermark", new Dictionary<string, object>
        {
            ["sequence"] = older.Identity.Sequence,
            ["cycleId"] = older.Identity.CycleId
        });

        InvalidDataException? exception = null;
        try { await repository.CreateOrResumeCycleAsync(cycle); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("STATE_CONFLICT");
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Claiming);
    }

    [Test]
    public async Task Missing_outer_cycle_with_an_exact_health_watermark_fails_closed()
    {
        var repository = CreateRepository();
        var cycle = Cycle("0198f865-1468-7000-8000-000000000052");
        await repository.CreateOrResumeCycleAsync(cycle);
        await fixture.Db.Collection(Cycles).Document(cycle.Identity.StorageId).DeleteAsync();

        InvalidDataException? exception = null;
        try { await repository.CreateOrResumeCycleAsync(cycle); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("STATE_CONFLICT");
        await Assert.That(await repository.GetCycleAsync(cycle.Identity)).IsNull();
        var health = await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(health!.Watermark).IsEqualTo(new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId));
        await Assert.That(health.ConsecutiveFailures.Handoff).IsEqualTo(0);
    }

    [Test]
    public async Task Advancing_past_incomplete_health_with_a_missing_prior_outer_cycle_fails_without_losing_supersession()
    {
        var repository = CreateRepository();
        var prior = Cycle("0198f865-1467-7000-8000-000000000053");
        await repository.CreateOrResumeCycleAsync(prior);
        await fixture.Db.Collection(Cycles).Document(prior.Identity.StorageId).DeleteAsync();
        var requested = Cycle("0198f865-1468-7000-8000-000000000054");

        InvalidDataException? exception = null;
        try { await repository.CreateOrResumeCycleAsync(requested); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("STATE_CONFLICT");
        await Assert.That(await repository.GetCycleAsync(requested.Identity)).IsNull();
        var health = await repository.GetHealthAsync(prior.Identity.Competition, prior.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(health!.Watermark).IsEqualTo(new BundesligaContextSourceWatermark(prior.Identity.Sequence, prior.Identity.CycleId));
        await Assert.That(health.ConsecutiveFailures.Handoff).IsEqualTo(0);
    }

    [Test]
    public async Task New_receipt_fails_closed_when_enabled_source_health_is_missing()
    {
        var repository = CreateRepository(); var cycle = Cycle();
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await healthReference.DeleteAsync();

        await Assert.That(() => repository.RecordReceiptAsync(Receipt(cycle.Identity, observation, digest))).Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
    }

    [Test]
    public async Task Persisted_receipt_exact_replay_remains_valid_when_health_is_missing()
    {
        var repository = CreateRepository(); var cycle = Cycle();
        await CompleteRejectedCycle(repository, cycle);
        var request = Receipt(cycle.Identity, Observation(cycle.Identity), new string('e', 64));
        var original = await repository.GetReceiptAsync(cycle.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane);
        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await healthReference.DeleteAsync();

        var replay = await repository.RecordReceiptAsync(request);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(original!.RecordedAtUtc);
        await Assert.That(() => repository.RecordReceiptAsync(request with { SelectedSnapshotId = new string('f', 64) })).Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("new", "NewRevision", true)]
    [Arguments("pending", "PendingRevision", true)]
    [Arguments("policy", "PolicyChanged", true)]
    [Arguments("accepted-null", null, true)]
    [Arguments("wrong", "NewRevision", false)]
    [Arguments("impossible", "AcceptedRevisionUnchanged", false)]
    public async Task Unavailable_identity_finalization_enforces_the_deterministic_revision_reason_matrix(string state, string? reason, bool accepted)
    {
        var repository = CreateRepository();
        var cycle = Cycle(state switch
        {
            "new" => "0198f865-1469-7000-8000-000000000091",
            "pending" => "0198f865-1469-7000-8000-000000000092",
            "policy" => "0198f865-1469-7000-8000-000000000093",
            "accepted-null" => "0198f865-1469-7000-8000-000000000094",
            "wrong" => "0198f865-1469-7000-8000-000000000095",
            "impossible" => "0198f865-1469-7000-8000-000000000096",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        });
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        if (state != "new") await SetUnavailableIdentityRevisionStateAsync(cycle.Identity, state);
        var observation = UnavailableIdentityObservation(cycle.Identity, reason);
        var before = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);

        if (accepted)
        {
            var finalized = await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
            await Assert.That(finalized.ObservationDigest).IsEqualTo(observation.ObservationDigest);
        }
        else
        {
            await Assert.That(() => repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1))).Throws<InvalidDataException>();
            await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters))!
                .CreateCanonicalUtf8(cycle.ExpectedConsumers).SequenceEqual(before!.CreateCanonicalUtf8(cycle.ExpectedConsumers))).IsTrue();
        }
    }

    [Test]
    [Arguments("new", "NewRevision", true)]
    [Arguments("pending", "PendingRevision", true)]
    [Arguments("policy", "PolicyChanged", true)]
    [Arguments("accepted-null", null, true)]
    [Arguments("wrong-at-receipt", "NewRevision", false)]
    public async Task Unavailable_identity_separate_receipt_enforces_the_deterministic_revision_reason_matrix(string state, string? reason, bool accepted)
    {
        var repository = CreateRepository();
        var cycle = Cycle(state switch
        {
            "new" => "0198f865-1469-7000-8000-000000000101",
            "pending" => "0198f865-1469-7000-8000-000000000102",
            "policy" => "0198f865-1469-7000-8000-000000000103",
            "accepted-null" => "0198f865-1469-7000-8000-000000000104",
            "wrong-at-receipt" => "0198f865-1469-7000-8000-000000000105",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        });
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        if (state is "pending" or "policy" or "accepted-null") await SetUnavailableIdentityRevisionStateAsync(cycle.Identity, state);
        var observation = UnavailableIdentityObservation(cycle.Identity, reason);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var bundle = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        if (!accepted) await SetUnavailableIdentityRevisionStateAsync(cycle.Identity, "accepted-null");
        var request = Receipt(cycle.Identity, observation, bundle) with
        {
            PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published,
            ActiveConditions = BundesligaContextSourceHealth.OrderConditions([
                BundesligaContextSourceHealthCondition.AcquisitionFailed,
                BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown,
                BundesligaContextSourceHealthCondition.RosterMembershipRejected,
                BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days])
        };

        if (accepted)
        {
            var recorded = await repository.RecordReceiptAsync(request);
            await Assert.That(recorded.Request.ObservationDigest).IsEqualTo(observation.ObservationDigest);
        }
        else
        {
            await Assert.That(() => repository.RecordReceiptAsync(request)).Throws<InvalidDataException>();
            await Assert.That(await repository.GetReceiptAsync(cycle.Identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane)).IsNull();
            await Assert.That((await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
        }
    }

    [Test]
    public async Task Identical_tuple_new_revision_finalization_is_state_conflict_and_mutation_free()
    {
        var repository = CreateRepository();
        var completed = Cycle("0198f865-1469-7000-8000-000000000043");
        await CompleteRejectedCycle(repository, completed);
        var attempted = Cycle("0198f865-1469-7000-8000-000000000044");
        await repository.CreateOrResumeCycleAsync(attempted);
        await repository.ClaimSourceAsync(attempted.Identity, BundesligaContextSource.Rosters, Token('2'), Now());

        var beforeSource = await repository.GetSourceCycleAsync(attempted.Identity, BundesligaContextSource.Rosters);
        var beforeOuter = await repository.GetCycleAsync(attempted.Identity);
        var beforeHealth = await repository.GetHealthAsync(attempted.Identity.Competition, attempted.Identity.Scope, BundesligaContextSource.Rosters);
        var beforeReceipts = await fixture.Db.Collection(Receipts).GetSnapshotAsync();

        await Assert.That(() => repository.FinalizeSourceAsync(attempted.Identity, BundesligaContextSource.Rosters, Token('2'), Observation(attempted.Identity), Now().AddMinutes(1))).Throws<InvalidDataException>();

        await Assert.That((await repository.GetSourceCycleAsync(attempted.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(beforeSource!.Status);
        await Assert.That((await repository.GetCycleAsync(attempted.Identity))!.Status).IsEqualTo(beforeOuter!.Status);
        await Assert.That(await repository.GetHealthAsync(attempted.Identity.Competition, attempted.Identity.Scope, BundesligaContextSource.Rosters)).IsEquivalentTo(beforeHealth);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Count).IsEqualTo(beforeReceipts.Documents.Count);
    }

    [Test]
    public async Task New_receipt_cannot_consume_a_state_conflicting_roster_observation()
    {
        var repository = CreateRepository();
        var first = Cycle("0198f865-1469-7000-8000-000000000045");
        await CompleteRejectedCycle(repository, first);
        var candidate = Cycle("0198f865-1469-7000-8000-000000000046");
        await repository.CreateOrResumeCycleAsync(candidate);
        await repository.ClaimSourceAsync(candidate.Identity, BundesligaContextSource.Rosters, Token('2'), Now());
        var observation = Observation(candidate.Identity, acquisitionReason: "RemoteIdentityChanged", etag: "y");
        await repository.FinalizeSourceAsync(candidate.Identity, BundesligaContextSource.Rosters, Token('2'), observation, Now().AddMinutes(1));
        var bundle = new string('e', 64);
        await repository.TransitionCycleAsync(candidate.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(candidate.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        await SetAcceptedRosterStateAsync(candidate.Identity, "y");

        var beforeSource = await repository.GetSourceCycleAsync(candidate.Identity, BundesligaContextSource.Rosters);
        var beforeOuter = await repository.GetCycleAsync(candidate.Identity);
        var beforeHealth = await repository.GetHealthAsync(candidate.Identity.Competition, candidate.Identity.Scope, BundesligaContextSource.Rosters);
        var beforeReceipts = await fixture.Db.Collection(Receipts).GetSnapshotAsync();
        var request = Receipt(candidate.Identity, observation, bundle);

        await Assert.That(() => repository.RecordReceiptAsync(request)).Throws<InvalidDataException>();

        await Assert.That((await repository.GetSourceCycleAsync(candidate.Identity, BundesligaContextSource.Rosters))!.Status).IsEqualTo(beforeSource!.Status);
        await Assert.That((await repository.GetCycleAsync(candidate.Identity))!.Status).IsEqualTo(beforeOuter!.Status);
        await Assert.That(await repository.GetHealthAsync(candidate.Identity.Competition, candidate.Identity.Scope, BundesligaContextSource.Rosters)).IsEquivalentTo(beforeHealth);
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Count).IsEqualTo(beforeReceipts.Documents.Count);
    }

    [Test]
    public async Task Strict_read_rejects_undefined_numeric_enum_text()
    {
        var repository = CreateRepository(); var cycle = ProductionCycle(); await repository.CreateOrResumeCycleAsync(cycle);
        var reference = fixture.Db.Collection(Cycles).Document(cycle.Identity.StorageId);
        await reference.UpdateAsync("status", "999");
        await Assert.That(() => repository.GetCycleAsync(cycle.Identity)).Throws<InvalidDataException>();

        var completed = Cycle("0198f865-1468-7000-8000-000000000041"); await CompleteRejectedCycle(repository, completed);
        var receiptDocument = (await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Single();
        await receiptDocument.Reference.UpdateAsync("publicationDisposition", "999");
        await Assert.That(() => repository.RecordReceiptAsync(Receipt(completed.Identity, Observation(completed.Identity), new string('e', 64)))).Throws<InvalidDataException>();

        var healthReference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(cycle.Identity.Competition, cycle.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await healthReference.UpdateAsync("desiredIssueProjection.synchronizationStatus", "999");
        await Assert.That(() => repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();

        var observationReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(completed.Identity, BundesligaContextSource.Rosters));
        await observationReference.UpdateAsync("observation.disposition", "999");
        await Assert.That(() => repository.GetSourceCycleAsync(completed.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task C1_correction_health_reconstruction_enforces_causal_completed_and_pending_references()
    {
        var repository = CreateRepository(); var local = Cycle("0198f865-1468-7000-8000-0000000000d1"); await repository.CreateOrResumeCycleAsync(local);
        var localHealth = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(local.Identity.Competition, local.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await localHealth.UpdateAsync("lastCompletedCycleId", "local:0198f865-1469-7000-8000-0000000000d2");
        await Assert.That(() => repository.GetHealthAsync(local.Identity.Competition, local.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await localHealth.UpdateAsync("lastCompletedCycleId", "local:0198f865-1467-7000-8000-0000000000d0");
        await Assert.That((await repository.GetHealthAsync(local.Identity.Competition, local.Identity.Scope, BundesligaContextSource.Rosters))!.LastCompletedCycleId).IsEqualTo("local:0198f865-1467-7000-8000-0000000000d0");
        await localHealth.UpdateAsync("rosterRevisionState.pending", new Dictionary<string, object> { ["revision"] = new string('a', 40), ["remoteIdentity"] = new Dictionary<string, object> { ["etag"] = "x", ["byteLength"] = 1L }, ["policySha256"] = new string('b', 64), ["firstSeenCycleId"] = "local:0198f865-1469-7000-8000-0000000000d2", ["lastFailureCode"] = "RevisionRejected" });
        await Assert.That(() => repository.GetHealthAsync(local.Identity.Competition, local.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();

        var production = ProductionCycle(999); await CompleteMixedEnrichmentRosterCycle(repository, production);
        var productionHealth = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(production.Identity.Competition, production.Identity.ScopeValue, BundesligaContextSource.Rosters));
        await productionHealth.UpdateAsync("lastCompletedCycleId", "gha:123:1000");
        await Assert.That(() => repository.GetHealthAsync(production.Identity.Competition, production.Identity.Scope, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await productionHealth.UpdateAsync("lastCompletedCycleId", production.Identity.CycleId);
        await Assert.That((await repository.GetHealthAsync(production.Identity.Competition, production.Identity.Scope, BundesligaContextSource.Rosters))!.LastCompletedCycleId).IsEqualTo(production.Identity.CycleId);
    }

    [Test]
    public async Task C1_correction_hostile_source_chronology_and_finalized_abort_reconstruction_are_strict()
    {
        var repository = CreateRepository(); var cycle = ProductionCycle(1001); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var reference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(cycle.Identity, BundesligaContextSource.Rosters));
        foreach (var finalization in new[] { Now().AddMinutes(10), Now().AddMinutes(11) })
        {
            await reference.UpdateAsync(new Dictionary<string, object> { ["finalizedAtUtc"] = BundesligaContextSourceContract.FormatUtc(finalization), ["status"] = "Aborted", ["abortCode"] = "ACQUISITION_INTERRUPTED" });
            await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        }
        await reference.UpdateAsync(new Dictionary<string, object> { ["finalizedAtUtc"] = BundesligaContextSourceContract.FormatUtc(Now().AddMinutes(1)), ["status"] = "Complete", ["abortCode"] = null!, ["receivedConsumers"] = BundesligaContextSourceContract.ProductionConsumers.ToArray(), ["completedAtUtc"] = BundesligaContextSourceContract.FormatUtc(Now()) });
        await Assert.That(() => repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        await reference.UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "ACQUISITION_INTERRUPTED", ["receivedConsumers"] = new[] { BundesligaContextSourceContract.ProductionConsumers[0] }, ["completedAtUtc"] = null! });
        var aborted = await repository.GetSourceCycleAsync(cycle.Identity, BundesligaContextSource.Rosters);
        await Assert.That(aborted!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Aborted);
    }

    [Test]
    public async Task C1_correction_receipt_reconstruction_rejects_cross_source_and_health_only_conditions_but_replays_valid_documents()
    {
        var repository = CreateRepository(); var eloCycle = ProductionCycle(1002, [BundesligaContextSource.ClubElo]); await CompleteRejectedEloCycle(repository, eloCycle);
        var elo = EloObservation(eloCycle.Identity); var eloRequest = ProductionEloFallbackReceipt(eloCycle.Identity, elo, new string('e', 64), BundesligaContextSourceContract.ProductionConsumers[0]);
        var eloReference = fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(eloCycle.Identity, BundesligaContextSource.ClubElo, eloRequest.ConsumerLaneId));
        await eloReference.UpdateAsync("activeConditions", new[] { "ROSTER_MEMBERSHIP_REJECTED" });
        await Assert.That(() => repository.RecordReceiptAsync(eloRequest)).Throws<InvalidDataException>();
        await eloReference.UpdateAsync("activeConditions", new[] { "ACQUISITION_FAILED", "CLUB_ELO_STALE_GT_7_DAYS" });
        var eloReplay = await repository.RecordReceiptAsync(eloRequest);

        var rosterCycle = Cycle("0198f865-1468-7000-8000-0000000000d3"); await CompleteRejectedCycle(repository, rosterCycle);
        var roster = Observation(rosterCycle.Identity); var rosterRequest = Receipt(rosterCycle.Identity, roster, new string('e', 64));
        var rosterReference = fixture.Db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(rosterCycle.Identity, BundesligaContextSource.Rosters, rosterRequest.ConsumerLaneId));
        foreach (var condition in new[] { "CLUB_ELO_SOURCE_REJECTED", "HANDOFF_INCOMPLETE", "CYCLE_ABORTED", "ROSTER_MEMBERSHIP_DATE_UNKNOWN" })
        {
            await rosterReference.UpdateAsync("activeConditions", new[] { condition });
            await Assert.That(() => repository.RecordReceiptAsync(rosterRequest)).Throws<InvalidDataException>();
        }
        await rosterReference.UpdateAsync("activeConditions", new[] { "ROSTER_ENRICHMENT_DATE_UNKNOWN", "ROSTER_MEMBERSHIP_REJECTED", "ROSTER_MEMBERSHIP_STALE_GT_14_DAYS" });
        var rosterReplay = await repository.RecordReceiptAsync(rosterRequest);
        await Assert.That(eloReplay.RecordedAtUtc).IsEqualTo(Now().AddMinutes(2));
        await Assert.That(rosterReplay.RecordedAtUtc).IsEqualTo(Now().AddMinutes(2));
    }

    [Test]
    public async Task Strict_source_read_binds_nested_observation_source_and_attempt_to_the_document()
    {
        var repository = CreateRepository();
        var sourceMismatchCycle = Cycle("0198f865-1468-7000-8000-000000000043");
        await CompleteRejectedCycle(repository, sourceMismatchCycle);
        var sourceMismatchReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(sourceMismatchCycle.Identity, BundesligaContextSource.Rosters));
        await sourceMismatchReference.UpdateAsync("observation.source", "club-elo");
        await Assert.That(() => repository.GetSourceCycleAsync(sourceMismatchCycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();

        await ClearAsync();
        var attemptMismatchCycle = Cycle("0198f865-1469-7000-8000-000000000044");
        await CompleteRejectedCycle(repository, attemptMismatchCycle);
        var attemptMismatchReference = fixture.Db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(attemptMismatchCycle.Identity, BundesligaContextSource.Rosters));
        await attemptMismatchReference.UpdateAsync("observation.attemptId", new string('f', 64));
        await Assert.That(() => repository.GetSourceCycleAsync(attemptMismatchCycle.Identity, BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Receipt_is_rejected_when_it_contradicts_the_finalized_observation()
    {
        var repository = CreateRepository(); var cycle = Cycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1)); var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        var contradictory = Receipt(cycle.Identity, observation, digest) with { SelectionDisposition = BundesligaContextSourceSelectionDisposition.DuckDbAccepted, SelectedOrigin = BundesligaContextSourceSelectedOrigin.DuckDb, PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published };

        await Assert.That(() => repository.RecordReceiptAsync(contradictory)).Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Newer_watermark_aborts_incomplete_once_and_lower_unseen_cycle_is_late()
    {
        var repository = CreateRepository(); var old = Cycle("0198f865-1467-7000-8000-000000000010"); await repository.CreateOrResumeCycleAsync(old);
        await repository.ClaimSourceAsync(old.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var newer = Cycle("0198f865-1468-7000-8000-000000000011"); await repository.CreateOrResumeCycleAsync(newer);
        await Assert.That((await repository.GetCycleAsync(old.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.SupersededIncompleteCycle);
        var health = await repository.GetHealthAsync(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.Development, BundesligaContextSource.Rosters);
        await Assert.That(health!.Watermark.CycleId).IsEqualTo(newer.Identity.CycleId); await Assert.That(health.ConsecutiveFailures.Handoff).IsEqualTo(1);
        var newest = Cycle("0198f865-1469-7000-8000-000000000012"); await repository.CreateOrResumeCycleAsync(newest);
        health = await repository.GetHealthAsync(BundesligaContextSourceContract.Competition, BundesligaContextSourceScope.Development, BundesligaContextSource.Rosters);
        await Assert.That(health!.Watermark.CycleId).IsEqualTo(newest.Identity.CycleId);
        await Assert.That(health.ConsecutiveFailures.Handoff).IsEqualTo(2);
        await Assert.That((await repository.GetCycleAsync(newer.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.SupersededIncompleteCycle);
        await Assert.That(() => repository.CreateOrResumeCycleAsync(Cycle("0198f865-1466-7000-8000-000000000009"))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Newer_cycle_supersedes_and_reduces_an_omitted_incomplete_source_once()
    {
        var repository = CreateRepository();
        var old = ProductionCycle(456, [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
        await repository.CreateOrResumeCycleAsync(old);
        var newer = ProductionCycle(457, [BundesligaContextSource.ClubElo]);

        var started = await repository.CreateOrResumeCycleWithResultAsync(newer);
        var replay = await repository.CreateOrResumeCycleWithResultAsync(newer);
        var omittedHealth = await repository.GetHealthAsync(newer.Identity.Competition, newer.Identity.Scope, BundesligaContextSource.Rosters);

        started.Validate();
        await Assert.That(started.SupersededSources).IsEquivalentTo([BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters]);
        await Assert.That(replay.SupersededSources).IsEmpty();
        await Assert.That((await repository.GetCycleAsync(old.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.SupersededIncompleteCycle);
        await Assert.That(omittedHealth!.Watermark.CycleId).IsEqualTo(old.Identity.CycleId);
        await Assert.That(omittedHealth.ConsecutiveFailures.Handoff).IsEqualTo(1);
        await Assert.That(omittedHealth.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.CycleAborted);
        await Assert.That(omittedHealth.DesiredIssueProjection!.BodySha256).IsEqualTo(BundesligaContextSourceHealth.HashIssueBody(
            BundesligaContextSourceHealth.CreateIssueBody(omittedHealth.DesiredIssueProjection.Marker, omittedHealth.Competition, omittedHealth.Source, omittedHealth.Watermark, omittedHealth.ActiveConditions)));
    }

    [Test]
    public async Task Supersession_recomputes_a_newly_crossed_staleness_threshold_from_retained_source_dates()
    {
        var repository = CreateRepository();
        var baselineDate = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        var baseline = ProductionCycle(480, [BundesligaContextSource.ClubElo]) with { StartedAtUtc = baselineDate, StalenessReferenceAtUtc = baselineDate };
        await CompleteRejectedEloCycle(repository, baseline);

        var incomplete = ProductionCycle(481, [BundesligaContextSource.ClubElo]) with { StartedAtUtc = baselineDate, StalenessReferenceAtUtc = baselineDate };
        await repository.CreateOrResumeCycleAsync(incomplete);
        var dueDate = baselineDate.AddDays(1);
        var superseding = ProductionCycle(482, [BundesligaContextSource.ClubElo]) with { StartedAtUtc = dueDate, StalenessReferenceAtUtc = dueDate };
        var started = await repository.CreateOrResumeCycleWithResultAsync(superseding);

        var health = await repository.GetHealthAsync(superseding.Identity.Competition, superseding.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(started.SupersededSources).Contains(BundesligaContextSource.ClubElo);
        await Assert.That((await repository.GetCycleAsync(incomplete.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.SupersededIncompleteCycle);
        await Assert.That(health!.Watermark.CycleId).IsEqualTo(superseding.Identity.CycleId);
        await Assert.That(health.LastSuccessfulSourceDates.RatedAt).IsEqualTo(new DateOnly(2026, 8, 20));
        await Assert.That(health.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        await Assert.That(health.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Open);
        await Assert.That(health.DesiredIssueProjection.BodySha256).IsEqualTo(BundesligaContextSourceHealth.HashIssueBody(
            BundesligaContextSourceHealth.CreateIssueBody(health.DesiredIssueProjection.Marker, health.Competition, health.Source, health.Watermark, health.ActiveConditions)));
    }

    [Test]
    public async Task Existing_completed_lower_watermark_is_late_instead_of_reentering_the_cycle()
    {
        var repository = CreateRepository(); var completed = Cycle("0198f865-1467-7000-8000-000000000013");
        await CompleteRejectedCycle(repository, completed);
        var newer = Cycle("0198f865-1468-7000-8000-000000000014");
        await repository.CreateOrResumeCycleAsync(newer);

        InvalidDataException? exception = null;
        try { await repository.CreateOrResumeCycleAsync(completed); }
        catch (InvalidDataException caught) { exception = caught; }
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("LATE_CYCLE");
        await Assert.That((await repository.GetCycleAsync(completed.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        await Assert.That((await repository.GetHealthAsync(completed.Identity.Competition, completed.Identity.Scope, BundesligaContextSource.Rosters))!.Watermark.CycleId).IsEqualTo(newer.Identity.CycleId);
    }

    [Test]
    public async Task Starting_after_an_already_aborted_watermark_does_not_count_a_second_supersession()
    {
        var repository = CreateRepository(); var old = Cycle("0198f865-1467-7000-8000-000000000030"); await repository.CreateOrResumeCycleAsync(old);
        await repository.AbortCycleAsync(old.Identity, BundesligaContextSourceError.LocalHandoffMissing);
        var afterAbort = await repository.GetHealthAsync(old.Identity.Competition, old.Identity.Scope, BundesligaContextSource.Rosters);
        var newer = Cycle("0198f865-1468-7000-8000-000000000031"); await repository.CreateOrResumeCycleAsync(newer);
        var afterStart = await repository.GetHealthAsync(newer.Identity.Competition, newer.Identity.Scope, BundesligaContextSource.Rosters);

        await Assert.That(afterAbort!.ConsecutiveFailures.Handoff).IsEqualTo(1);
        await Assert.That(afterStart!.ConsecutiveFailures.Handoff).IsEqualTo(1);
        await Assert.That((await repository.GetCycleAsync(old.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.LocalHandoffMissing);

        var newest = Cycle("0198f865-1469-7000-8000-000000000032"); await repository.CreateOrResumeCycleAsync(newest);
        var afterDistinctIncomplete = await repository.GetHealthAsync(newest.Identity.Competition, newest.Identity.Scope, BundesligaContextSource.Rosters);
        await Assert.That(afterDistinctIncomplete!.ConsecutiveFailures.Handoff).IsEqualTo(2);
    }

    [Test]
    public async Task Bundle_and_artifact_reservations_are_immutable_across_transition_and_replay()
    {
        var repository = CreateRepository(); var cycle = ProductionCycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64); var conflictingDigest = new string('f', 64); var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await Assert.That(() => repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, conflictingDigest, artifact)).Throws<InvalidDataException>();

        var reserved = await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await Assert.That(() => repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact + "-other")).Throws<InvalidDataException>();
        var ready = await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        var replay = await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        await Assert.That(reserved.BundleSha256).IsEqualTo(digest);
        await Assert.That(ready.ArtifactName).IsEqualTo(artifact);
        await Assert.That(replay.CreateCanonicalUtf8().SequenceEqual(ready.CreateCanonicalUtf8())).IsTrue();
        await Assert.That(() => repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, conflictingDigest, artifact)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Issue_projection_failure_and_completion_are_CAS_updated_without_changing_durable_cycle_completion()
    {
        var repository = CreateRepository(); var cycle = ProductionCycle(); await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1)); var digest = new string('e', 64); var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers) await repository.RecordReceiptAsync(ProductionReceipt(cycle.Identity, observation, digest, lane));
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers.Where(value => value.StartsWith("arena-", StringComparison.Ordinal)))
        {
            var persistedReceipt = await repository.GetReceiptAsync(cycle.Identity, BundesligaContextSource.Rosters, lane);
            await Assert.That(persistedReceipt!.Request.ConsumerLaneId).IsEqualTo(lane);
            await Assert.That(persistedReceipt.Request.CommunityContext).IsEqualTo("ehonda-ai-arena");
        }
        var completedCycle = await repository.GetCycleAsync(cycle.Identity); var pending = (await repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters))!;

        var failedProjection = pending.DesiredIssueProjection! with { LastAttemptedAtUtc = Now().AddMinutes(3), LastErrorCode = BundesligaContextSourceIssueError.GithubIssueUpdateFailed };
        var failed = await repository.UpdateIssueProjectionAsync(pending, failedProjection);
        var synchronizedProjection = failed.DesiredIssueProjection! with { AppliedBodySha256 = failed.DesiredIssueProjection.BodySha256, SynchronizationStatus = BundesligaContextSourceIssueSynchronization.Synchronized, LastAttemptedAtUtc = Now().AddMinutes(4), LastErrorCode = null };
        var synchronized = await repository.UpdateIssueProjectionAsync(failed, synchronizedProjection);
        var replay = await repository.UpdateIssueProjectionAsync(failed, synchronizedProjection);

        await Assert.That(failed.LastCompletedCycleId).IsEqualTo(cycle.Identity.CycleId);
        await Assert.That(synchronized.LastCompletedCycleId).IsEqualTo(cycle.Identity.CycleId);
        await Assert.That(replay.DesiredIssueProjection).IsEqualTo(synchronized.DesiredIssueProjection);
        await Assert.That(replay.Watermark).IsEqualTo(synchronized.Watermark);
        await Assert.That(replay.LastCompletedCycleId).IsEqualTo(synchronized.LastCompletedCycleId);
        await Assert.That((await repository.GetCycleAsync(cycle.Identity))!.CreateCanonicalUtf8().SequenceEqual(completedCycle!.CreateCanonicalUtf8())).IsTrue();
        await Assert.That(() => repository.UpdateIssueProjectionAsync(pending, synchronizedProjection with { LastAttemptedAtUtc = Now().AddMinutes(5) })).Throws<InvalidDataException>();
    }

    private static string SnapshotFingerprint(DocumentSnapshot snapshot) => JsonSerializer.Serialize(new
    {
        snapshot.Exists,
        snapshot.Id,
        Path = snapshot.Reference.Path,
        CreateTime = snapshot.Exists ? CanonicalTimestamp(snapshot.CreateTime) : null,
        UpdateTime = snapshot.Exists ? CanonicalTimestamp(snapshot.UpdateTime) : null,
        Fields = snapshot.Exists ? CanonicalFirestoreValue(snapshot.ToDictionary()) : new SortedDictionary<string, object?>(StringComparer.Ordinal)
    });

    private static object? CanonicalFirestoreValue(object? value) => value switch
    {
        null => null,
        Timestamp timestamp => CanonicalTimestamp(timestamp),
        DocumentReference reference => new { Path = reference.Path },
        GeoPoint point => new { point.Latitude, point.Longitude },
        byte[] bytes => Convert.ToBase64String(bytes),
        Google.Protobuf.ByteString bytes => bytes.ToBase64(),
        IEnumerable<KeyValuePair<string, object>> map => map
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => CanonicalFirestoreValue(pair.Value), StringComparer.Ordinal),
        System.Collections.IEnumerable values when value is not string => values.Cast<object?>().Select(CanonicalFirestoreValue).ToArray(),
        _ => value
    };

    private static object? CanonicalTimestamp(Timestamp? timestamp)
    {
        if (timestamp is null) return null;
        var proto = timestamp.ToProto();
        return new { Seconds = proto.Seconds, Nanoseconds = proto.Nanos };
    }

    private static Dictionary<string, object?> FirestoreMap(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => FirestoreValue(property.Value),
            StringComparer.Ordinal);
    }

    private static object? FirestoreValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            property => property.Name, property => FirestoreValue(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => value.EnumerateArray().Select(FirestoreValue).ToArray(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new InvalidDataException("JSON value cannot be represented in Firestore.")
    };

    // Deliberately captures the complete persisted graph rather than only the document expected
    // to reject: head, immutable publication rows/payloads, receipts, source/outer cycles, health,
    // and the exact ordered received-consumer prefixes stored in those source-cycle documents.
    private async Task<string> PersistedGraphFingerprintAsync()
    {
        var collections = new[]
        {
            "document-publication-heads", "document-publication-snapshots", "context-documents", "kpi-documents",
            Cycles, Observations, Receipts, Health
        };
        var graph = new List<object>();
        foreach (var collection in collections.OrderBy(value => value, StringComparer.Ordinal))
        {
            var snapshot = await fixture.Db.Collection(collection).GetSnapshotAsync();
            graph.Add(new
            {
                Collection = collection,
                Count = snapshot.Documents.Count,
                Documents = snapshot.Documents.OrderBy(document => document.Reference.Path, StringComparer.Ordinal)
                    .Select(SnapshotFingerprint).ToArray()
            });
        }
        return JsonSerializer.Serialize(graph);
    }

    private async Task UpdatePriorHealthSelectedDateAsync(DocumentReference healthReference, DateOnly ratedAt)
    {
        var map = (await healthReference.GetSnapshotAsync()).ToDictionary();
        var selections = ((IEnumerable<object>)map["communitySelections"])
            .Select(value => new Dictionary<string, object>((IDictionary<string, object>)value, StringComparer.Ordinal))
            .ToArray();
        selections.Single()["ratedAt"] = ratedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await healthReference.UpdateAsync("communitySelections", selections);
    }

    private async Task UpdatePriorHealthSelectedOriginAsync(DocumentReference healthReference, BundesligaContextSourceSelectedOrigin origin)
    {
        var map = (await healthReference.GetSnapshotAsync()).ToDictionary();
        var selections = ((IEnumerable<object>)map["communitySelections"])
            .Select(value => new Dictionary<string, object>((IDictionary<string, object>)value, StringComparer.Ordinal))
            .ToArray();
        selections.Single()["selectedOrigin"] = origin.ToString();
        await healthReference.UpdateAsync("communitySelections", selections);
    }

    private async Task<(FirebaseContextSourceCycleRepository Repository, BundesligaContextSourceOuterCycle Prior, BundesligaContextSourceOuterCycle Current, BundesligaContextSourceReceiptRequest Request, DocumentReference Head)> CreateHtmlRetainedScenarioAsync(
        BundesligaContextSourceSelectionDisposition priorSelection = BundesligaContextSourceSelectionDisposition.NetworkAccepted,
        BundesligaContextSourceSelectedOrigin priorOrigin = BundesligaContextSourceSelectedOrigin.NetworkCandidate,
        BundesligaContextSourcePublicationDisposition priorPublication = BundesligaContextSourcePublicationDisposition.Published)
    {
        var repository = CreateRepository();
        var prior = Cycle("0198f865-1467-7000-8000-0000000000b2", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(prior);
        await repository.ClaimSourceAsync(prior.Identity, BundesligaContextSource.ClubElo, Token('1'), Now());
        var priorObservation = priorSelection == BundesligaContextSourceSelectionDisposition.NetworkAccepted
            ? HtmlEloObservation(prior.Identity)
            : HtmlTransportEloObservation(prior.Identity);
        var priorBundle = new string('e', 64);
        await repository.FinalizeSourceAsync(prior.Identity, BundesligaContextSource.ClubElo, Token('1'), priorObservation, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(prior.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, priorBundle);
        await repository.TransitionCycleAsync(prior.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, priorBundle);
        var priorConditions = priorSelection == BundesligaContextSourceSelectionDisposition.NetworkAccepted
            ? Array.Empty<BundesligaContextSourceHealthCondition>()
            : new[] { BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloSourceRejected };
        await repository.RecordReceiptAsync(HtmlReceipt(prior.Identity, priorObservation, priorBundle, priorSelection, priorOrigin, priorPublication, new DateOnly(2026, 9, 4), priorConditions));

        var current = Cycle("0198f865-1468-7000-8000-0000000000b3", [BundesligaContextSource.ClubElo]);
        await repository.CreateOrResumeCycleAsync(current);
        await repository.ClaimSourceAsync(current.Identity, BundesligaContextSource.ClubElo, Token('2'), Now());
        var observation = HtmlEloObservation(current.Identity); var bundle = new string('d', 64);
        await repository.FinalizeSourceAsync(current.Identity, BundesligaContextSource.ClubElo, Token('2'), observation, Now().AddMinutes(1));
        await repository.TransitionCycleAsync(current.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, bundle);
        await repository.TransitionCycleAsync(current.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, bundle);
        var request = HtmlReceipt(current.Identity, observation, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted, new DateOnly(2026, 9, 4), []);
        var scope = new DocumentPublicationScope(current.Identity.Competition, request.CommunityContext, BundesligaDocumentPublication.ClubEloPublicationSet);
        var head = fixture.Db.Collection(Heads).Document(DocumentPublicationContract.ComputeHeadId(scope));
        await head.SetAsync(new Dictionary<string, object> { ["competition"] = scope.Competition, ["communityContext"] = scope.CommunityContext, ["publicationSet"] = scope.PublicationSet, ["snapshotId"] = request.SelectedSnapshotId });
        return (repository, prior, current, request, head);
    }

    private FirebaseContextSourceCycleRepository CreateRepository(DateTimeOffset? recordedAtUtc = null) => new(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedTimeProvider(recordedAtUtc ?? Now().AddMinutes(2)));
    private async Task AssertNoCurrentReceiptMutation(BundesligaContextSourceCycleIdentity identity, bool healthIsReadable = true)
    {
        await Assert.That((await fixture.Db.Collection(Receipts).GetSnapshotAsync()).Documents.Any(document => document.Id == BundesligaContextSourceHashing.ReceiptStorageId(identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))).IsFalse();
        await Assert.That((await CreateRepository().GetSourceCycleAsync(identity, BundesligaContextSource.ClubElo))!.Status).IsEqualTo(BundesligaContextSourceSourceStatus.Finalized);
        await Assert.That((await CreateRepository().GetCycleAsync(identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        if (healthIsReadable)
        {
            var health = await CreateRepository().GetHealthAsync(identity.Competition, identity.Scope, BundesligaContextSource.ClubElo);
            await Assert.That(health!.LastCompletedCycleId).IsNotEqualTo(identity.CycleId);
            await Assert.That(health.CommunitySelections.Single().SelectedSnapshotId).IsEqualTo(new string('c', 64));
        }
    }
    private static DateTimeOffset Now() => new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static string Token(char value) => $"{new string(value, 8)}-{new string(value, 4)}-4{new string(value, 3)}-8{new string(value, 3)}-{new string(value, 12)}";
    private static BundesligaContextSourceOuterCycle Cycle(string? uuid = null, IReadOnlyList<BundesligaContextSource>? sources = null)
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, uuid ?? $"0198f865-1467-7000-8000-{Guid.NewGuid():N}"[..36]);
        return new BundesligaContextSourceOuterCycle(identity, Now(), Now(), BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentConsumers, sources ?? [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
    }
    private static BundesligaContextSourceOuterCycle ProductionCycle(long runId = 456, IReadOnlyList<BundesligaContextSource>? sources = null)
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, runId);
        return new BundesligaContextSourceOuterCycle(identity, Now(), Now(), BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, sources ?? [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
    }
    private static async Task CompleteRejectedCycle(FirebaseContextSourceCycleRepository repository, BundesligaContextSourceOuterCycle cycle)
    {
        await repository.CreateOrResumeCycleAsync(cycle); await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now()); var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1)); var digest = new string('e', 64);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, digest);
        await repository.RecordReceiptAsync(Receipt(cycle.Identity, observation, digest));
    }
    private static async Task CompleteRejectedEloCycle(FirebaseContextSourceCycleRepository repository, BundesligaContextSourceOuterCycle cycle)
    {
        await repository.CreateOrResumeCycleAsync(cycle); await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), Now()); var observation = EloObservation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token('1'), observation, Now().AddMinutes(1)); var digest = new string('e', 64); var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var receipt = ProductionEloFallbackReceipt(cycle.Identity, observation, digest, lane);
            if (DateOnly.FromDateTime(cycle.StalenessReferenceAtUtc.UtcDateTime).DayNumber - receipt.SourceDates.RatedAt!.Value.DayNumber <= 7)
                receipt = receipt with { ActiveConditions = [BundesligaContextSourceHealthCondition.AcquisitionFailed] };
            await repository.RecordReceiptAsync(receipt);
        }
    }
    private static async Task CompleteMixedEnrichmentRosterCycle(FirebaseContextSourceCycleRepository repository, BundesligaContextSourceOuterCycle cycle)
    {
        await repository.CreateOrResumeCycleAsync(cycle);
        await repository.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), Now());
        var observation = Observation(cycle.Identity);
        await repository.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.Rosters, Token('1'), observation, Now().AddMinutes(1));
        var digest = new string('e', 64); var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, digest);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, digest, artifact);
        await repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, digest, artifact);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var receipt = ProductionReceipt(cycle.Identity, observation, digest, lane);
            if (lane != BundesligaContextSourceContract.ProductionConsumers[0])
            {
                receipt = receipt with
                {
                    SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood,
                    SourceDates = receipt.SourceDates with { EnrichmentCapturedAt = new DateOnly(2026, 8, 25) },
                    ActiveConditions = BundesligaContextSourceHealth.OrderConditions([
                        BundesligaContextSourceHealthCondition.RosterMembershipRejected,
                        BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days])
                };
            }
            await repository.RecordReceiptAsync(receipt);
        }
    }
    private static BundesligaContextSourceObservation Observation(BundesligaContextSourceCycleIdentity identity, string acquisitionReason = "NewRevision", string etag = "x") => new(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), Now(), BundesligaContextSourceDisposition.Rejected, RosterDescriptor(acquisitionReason, etag), null, ["UNKNOWN_SOURCE_DATE"]);
    private static BundesligaContextSourceObservation EligibleRosterObservation(BundesligaContextSourceCycleIdentity identity) => new(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), Now(), BundesligaContextSourceDisposition.ArtifactCaptured, EligibleRosterDescriptor(), new BundesligaContextSourcePayload("rosters/source.duckdb", 1, new string('c', 64)), []);
    private static BundesligaContextSourceObservation EloObservation(BundesligaContextSourceCycleIdentity identity) => new(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), Now(), BundesligaContextSourceDisposition.Rejected, "{\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"sourceUrl\":\"https://example.test/elo.csv\",\"rawSha256\":null,\"rawByteLength\":null,\"csvHeader\":null,\"providerRatedAt\":null,\"providerDateEvidence\":null,\"nameMappingContract\":null,\"nameMappingSha256\":null,\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}", null, ["UNKNOWN_SOURCE_DATE"]);
    private static BundesligaContextSourceObservation EligibleEloObservation(BundesligaContextSourceCycleIdentity identity)
    {
        var rawSha256 = new string('c', 64);
        var rows = BundesligaTeamManifest.Default.Entries.Select((entry, index) => new { teamSlug = entry.TeamSlug, providerName = $"Team {index + 1:00}", globalRank = index + 1, elo = 1500.25 + index }).ToArray();
        var descriptor = JsonSerializer.Serialize(new
        {
            contract = "club-elo-direct-csv-descriptor/v1",
            sourceUrl = "https://example.test/elo.csv",
            rawSha256,
            rawByteLength = 3,
            csvHeader = "Rank,Club,Country,Level,Elo,From,To",
            providerRatedAt = "2026-09-04",
            providerDateEvidence = new { kind = "ProviderCsvField", recipeId = "recipe/v1", field = "From", rawValue = "2026-09-04", ratedAt = "2026-09-04" },
            nameMappingContract = "map/v1",
            nameMappingSha256 = new string('d', 64),
            sourceRows = rows,
            evaluation = "Eligible"
        });
        return new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), Now(), BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, new BundesligaContextSourcePayload("club-elo/source.csv", 3, rawSha256), []);
    }
    private static BundesligaContextSourceObservation HtmlEloObservation(BundesligaContextSourceCycleIdentity identity)
    {
        var rawSha256 = new string('c', 64);
        var mapping = new[] { ("b04", "/Leverkusen", "Leverkusen"), ("bmg", "/Gladbach", "Gladbach"), ("bvb", "/Dortmund", "Dortmund"), ("fca", "/Augsburg", "Augsburg"), ("fcb", "/Bayern", "Bayern München"), ("fck", "/Koeln", "Köln"), ("fcu", "/UnionBerlin", "Union Berlin"), ("hsv", "/Hamburg", "Hamburg"), ("m05", "/Mainz", "Mainz"), ("rbl", "/RBLeipzig", "RB Leipzig"), ("s04", "/Schalke", "Schalke"), ("scf", "/Freiburg", "Freiburg"), ("scp", "/Paderborn", "Paderborn"), ("sge", "/Frankfurt", "Frankfurt"), ("sve", "/Elversberg", "Elversberg"), ("svw", "/Werder", "Werder"), ("tsg", "/Hoffenheim", "Hoffenheim"), ("vfb", "/Stuttgart", "Stuttgart") };
        var rows = mapping.Select((entry, index) => new { teamSlug = entry.Item1, providerRoute = entry.Item2, providerDisplayName = entry.Item3, globalRank = index + 1, elo = 1500 + index }).ToArray();
        var descriptor = JsonSerializer.Serialize(new
        {
            contract = "club-elo-official-html-descriptor/v1", sourceUrl = "https://clubelo.com/GER",
            response = new { statusCode = 200, finalUrl = "https://clubelo.com/GER", redirectCount = 0, redirectLocation = (string?)null, mediaType = "text/html", charset = "utf-8", contentEncodings = Array.Empty<string>(), declaredContentLength = 3L },
            rawSha256, rawByteLength = 3L, parserContract = "club-elo-official-html-parser/v1", displayedDate = "2026-09-04",
            providerDateEvidence = new { kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1", field = "h1>a[href]", rawValue = "2026-09-04", ratedAt = "2026-09-04" },
            tableContract = "club-elo-official-html-table/v1", tableHeader = new[] { "Club", "Elo", "+/-", "Golo" },
            nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1", nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256,
            sourceRows = rows, evaluation = "Eligible"
        });
        return new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), Now(), BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, new BundesligaContextSourcePayload("club-elo/source.html", 3, rawSha256), []);
    }
    private static BundesligaContextSourceObservation HtmlTransportEloObservation(BundesligaContextSourceCycleIdentity identity) => new(
        BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), Now(), BundesligaContextSourceDisposition.Rejected,
        $"{{\"contract\":\"club-elo-official-html-descriptor/v1\",\"sourceUrl\":\"https://clubelo.com/GER\",\"response\":null,\"rawSha256\":null,\"rawByteLength\":null,\"parserContract\":\"club-elo-official-html-parser/v1\",\"displayedDate\":null,\"providerDateEvidence\":null,\"tableContract\":\"club-elo-official-html-table/v1\",\"tableHeader\":[\"Club\",\"Elo\",\"+/-\",\"Golo\"],\"nameMappingContract\":\"bundesliga-2026-27-club-elo-name-map/v1\",\"nameMappingSha256\":\"{BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256}\",\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}}",
        null, ["CLUB_ELO_TRANSPORT_REJECTED"]);
    private static BundesligaContextSourceReceiptRequest HtmlReceipt(
        BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle,
        BundesligaContextSourceSelectionDisposition selection, BundesligaContextSourceSelectedOrigin origin,
        BundesligaContextSourcePublicationDisposition publication, DateOnly ratedAt,
        IReadOnlyList<BundesligaContextSourceHealthCondition> conditions) => new(identity, BundesligaContextSource.ClubElo,
        BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity,
        observation.ObservationDigest, bundle, selection, new string('c', 64), origin, publication,
        new BundesligaContextSourceDates(ratedAt, null, null, null), null,
        new BundesligaContextSourceCarriedFields(0, 0, 0, null), conditions);
    private static BundesligaContextSourceReceiptRequest EloFallbackReceipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle) => new(identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity, observation.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.LaunchSeed, BundesligaContextSourcePublicationDisposition.Published, new BundesligaContextSourceDates(new DateOnly(2026, 8, 20), null, null, null), null, new BundesligaContextSourceCarriedFields(0, 0, 0, null), [BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days]);
    private static BundesligaContextSourceReceiptRequest ProductionEloFallbackReceipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle, string lane) => new(identity, BundesligaContextSource.ClubElo, lane, CommunityForLane(lane), observation.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.LaunchSeed, BundesligaContextSourcePublicationDisposition.Published, new BundesligaContextSourceDates(new DateOnly(2026, 8, 20), null, null, null), null, new BundesligaContextSourceCarriedFields(0, 0, 0, null), [BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days]);
    private static BundesligaContextSourceReceiptRequest Receipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle) => new(identity, BundesligaContextSource.Rosters, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentCommunity, observation.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.CandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.NotAttempted, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), BundesligaContextSourceHealth.OrderConditions([BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown, BundesligaContextSourceHealthCondition.RosterMembershipRejected, BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days]));
    private static BundesligaContextSourceReceiptRequest ProductionReceipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle, string lane) => new(identity, BundesligaContextSource.Rosters, lane, CommunityForLane(lane), observation.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.CandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.NotAttempted, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 20), null), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), BundesligaContextSourceHealth.OrderConditions([BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown, BundesligaContextSourceHealthCondition.RosterMembershipRejected, BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days]));
    private static BundesligaContextSourceReceiptRequest EligibleProductionRosterReceipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundle, string lane, bool enrichmentRejected) => new(identity, BundesligaContextSource.Rosters, lane, CommunityForLane(lane), observation.ObservationDigest, bundle, BundesligaContextSourceSelectionDisposition.DuckDbAccepted, new string('c', 64), BundesligaContextSourceSelectedOrigin.DuckDb, BundesligaContextSourcePublicationDisposition.Published, new BundesligaContextSourceDates(null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), enrichmentRejected ? [BundesligaContextSourceHealthCondition.RosterEnrichmentRejected] : []);
    private static string CommunityForLane(string lane) => lane switch { "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse", "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena" };
    private static BundesligaContextSourceObservation UnavailableIdentityObservation(BundesligaContextSourceCycleIdentity identity, string? reason) => new(
        BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.Rosters), Now(),
        BundesligaContextSourceDisposition.Rejected,
        $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":null,\"acquisitionReason\":{(reason is null ? "null" : $"\"{reason}\"")},\"remoteIdentityAfter\":null,\"embeddedRevision\":null,\"rawSha256\":null,\"expectedRawSha256\":null,\"rawByteLength\":null,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"RemoteIdentityUnavailable\"}}",
        null, ["ROSTER_REMOTE_IDENTITY_UNAVAILABLE"]);

    private Task SetUnavailableIdentityRevisionStateAsync(BundesligaContextSourceCycleIdentity identity, string state)
    {
        var reference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(identity.Competition, identity.ScopeValue, BundesligaContextSource.Rosters));
        var acceptedPolicy = state == "policy" ? new string('f', 64) : BundesligaContextSourceDescriptorContract.RosterPolicySha256;
        var map = new Dictionary<string, object?>
        {
            ["accepted"] = state == "pending" ? null : new Dictionary<string, object?>
            {
                ["revision"] = new string('a', 40),
                ["remoteIdentity"] = new Dictionary<string, object?> { ["etag"] = "x", ["byteLength"] = 1L },
                ["policySha256"] = acceptedPolicy,
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
        };
        return reference.UpdateAsync("rosterRevisionState", map);
    }
    private static string RosterDescriptor(string acquisitionReason = "NewRevision", string etag = "x") => $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"{etag}\",\"byteLength\":1}},\"acquisitionReason\":\"{acquisitionReason}\",\"remoteIdentityAfter\":{{\"etag\":\"{etag}\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"SourceDateRejected\"}}";
    private static string EligibleRosterDescriptor() => $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":\"2026-09-01\",\"membershipEffectiveDate\":\"2026-09-01\",\"enrichmentCaptureDate\":\"2026-09-01\",\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"Eligible\"}}";
    private static string MetadataUnchangedDescriptor(string retainedDescriptor, string retainedEvaluation = "SourceDateRejected", IReadOnlyList<string>? diagnostics = null)
    {
        var retainedDiagnostics = System.Text.Json.JsonSerializer.Serialize(diagnostics ?? ["UNKNOWN_SOURCE_DATE"]);
        return $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"AcceptedRevisionUnchanged\",\"remoteIdentityAfter\":null,\"embeddedRevision\":null,\"rawSha256\":null,\"expectedRawSha256\":null,\"rawByteLength\":null,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":\"{retainedDescriptor}\",\"retainedEvaluation\":\"{retainedEvaluation}\",\"retainedDiagnostics\":{retainedDiagnostics},\"evaluation\":\"MetadataUnchanged\"}}";
    }

    private Task SetAcceptedRosterStateAsync(BundesligaContextSourceCycleIdentity identity, string etag)
    {
        var reference = fixture.Db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(identity.Competition, identity.ScopeValue, BundesligaContextSource.Rosters));
        return reference.UpdateAsync("rosterRevisionState", new Dictionary<string, object?>
        {
            ["accepted"] = new Dictionary<string, object?>
            {
                ["revision"] = new string('a', 40),
                ["remoteIdentity"] = new Dictionary<string, object?> { ["etag"] = etag, ["byteLength"] = 1L },
                ["policySha256"] = BundesligaContextSourceDescriptorContract.RosterPolicySha256,
                ["descriptorSha256"] = new string('d', 64)
            },
            ["pending"] = null
        });
    }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
