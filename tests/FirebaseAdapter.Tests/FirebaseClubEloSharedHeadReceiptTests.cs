using System.Text;
using System.Text.Json.Nodes;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, FirestoreFixture.DocumentPublicationParallelKey,
    FirestoreFixture.ContextDocumentsParallelKey, FirestoreFixture.KpiParallelKey, "context-source-cycle-repository" })]
public sealed class FirebaseClubEloSharedHeadReceiptTests(FirestoreFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Bundle = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string Token = "11111111-1111-4111-8111-111111111111";

    [Before(Test)]
    public async Task ClearAsync()
    {
        foreach (var name in new[] { "context-source-cycles", "context-source-cycle-observations", "context-source-cycle-receipts",
            "context-source-health", "document-publication-heads", "document-publication-snapshots", "context-documents", "kpi-documents" })
            foreach (var document in (await fixture.Db.Collection(name).GetSnapshotAsync()).Documents)
                await document.Reference.DeleteAsync();
    }

    [Test]
    public async Task All_eight_lanes_complete_after_the_first_arena_lane_advances_the_shared_head()
    {
        var cycles = Repository();
        await CompleteBaselineAsync(cycles);
        var prepared = await PrepareAsync(cycles, 902);
        await PublishFourHeadsAsync(cycles, prepared);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers.Skip(4))
            await cycles.RecordReceiptAsync(RetainedReceipt(prepared, lane));
        await Assert.That((await cycles.GetCycleAsync(prepared.Cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        var health = await cycles.GetHealthAsync(prepared.Cycle.Identity.Competition, prepared.Cycle.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(health!.CommunitySelections.Count).IsEqualTo(8);
        await Assert.That(health.ConsecutiveFailures.Acquisition).IsEqualTo(0);
        await Assert.That(health.CommunitySelections.All(selection => selection.SelectedSnapshotId == prepared.SnapshotId)).IsTrue();
    }

    [Test]
    public async Task A_distinct_cycle_retains_heads_atomically_published_by_a_superseded_incomplete_cycle()
    {
        var cycles = Repository();
        await CompleteBaselineAsync(cycles);
        var partial = await PrepareAsync(cycles, 902);
        await PublishFourHeadsAsync(cycles, partial);
        var next = await PrepareAsync(cycles, 903, notNewer: true);
        await Assert.That((await cycles.GetCycleAsync(partial.Cycle.Identity))!.AbortCode).IsEqualTo(BundesligaContextSourceError.SupersededIncompleteCycle);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
            await cycles.RecordReceiptAsync(RetainedReceipt(next, lane));
        await Assert.That((await cycles.GetCycleAsync(next.Cycle.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        var head = await Publications().GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, "ehonda-ai-arena");
        await Assert.That(head!.Snapshot.MetadataJson).IsEqualTo(partial.Build.MetadataJson);
        var health = await cycles.GetHealthAsync(next.Cycle.Identity.Competition, next.Cycle.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(health!.ConsecutiveFailures.Acquisition).IsEqualTo(0);
        await Assert.That(health.ActiveConditions).IsEmpty();
        await Assert.That(health.DesiredIssueProjection!.DesiredState).IsEqualTo(BundesligaContextSourceIssueState.Closed);
    }

    [Test]
    public async Task Historical_NotNewer_failure_receipts_remain_valid_prior_evidence_but_new_receipts_are_clean()
    {
        var cycles = Repository();
        await CompleteBaselineAsync(cycles);
        var accepted = await PrepareAsync(cycles, 902);
        await PublishFourHeadsAsync(cycles, accepted);
        foreach (var lane in accepted.Cycle.ExpectedConsumers.Skip(4))
            await cycles.RecordReceiptAsync(RetainedReceipt(accepted, lane));
        var legacy = await PrepareAsync(cycles, 903, notNewer: true);
        foreach (var lane in legacy.Cycle.ExpectedConsumers)
            await cycles.RecordReceiptAsync(RetainedReceipt(legacy, lane));
        var oldFlags = new[] { "ACQUISITION_FAILED", "CLUB_ELO_SOURCE_REJECTED" };
        foreach (var lane in legacy.Cycle.ExpectedConsumers)
            await fixture.Db.Collection("context-source-cycle-receipts").Document(
                BundesligaContextSourceHashing.ReceiptStorageId(legacy.Cycle.Identity, BundesligaContextSource.ClubElo, lane))
                .UpdateAsync("activeConditions", oldFlags);
        var healthRef = fixture.Db.Collection("context-source-health").Document(BundesligaContextSourceHashing.HealthStorageId(
            legacy.Cycle.Identity.Competition, legacy.Cycle.Identity.ScopeValue, BundesligaContextSource.ClubElo));
        var oldHealth = (await cycles.GetHealthAsync(legacy.Cycle.Identity.Competition, legacy.Cycle.Identity.Scope, BundesligaContextSource.ClubElo))!;
        var flags = new[] { BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloSourceRejected };
        var body = BundesligaContextSourceHealth.CreateIssueBody(oldHealth.DesiredIssueProjection!.Marker,
            oldHealth.Competition, oldHealth.Source, oldHealth.Watermark, flags);
        var selections = (List<object>)(await healthRef.GetSnapshotAsync()).ToDictionary()["communitySelections"];
        foreach (var selection in selections.Cast<Dictionary<string, object>>()) selection["conditions"] = oldFlags;
        await healthRef.UpdateAsync(new Dictionary<string, object> { ["communitySelections"] = selections,
            ["activeConditions"] = oldFlags, ["consecutiveFailures.acquisition"] = 1L,
            ["desiredIssueProjection.bodySha256"] = BundesligaContextSourceHashing.Sha256(Encoding.UTF8.GetBytes(body)) });
        var current = await PrepareAsync(cycles, 904, notNewer: true);
        var dirty = RetainedReceipt(current, current.Cycle.ExpectedConsumers[0]) with
        {
            ActiveConditions = [BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloSourceRejected]
        };
        await Assert.That(() => cycles.RecordReceiptAsync(dirty)).Throws<InvalidDataException>();
        foreach (var lane in current.Cycle.ExpectedConsumers)
            await cycles.RecordReceiptAsync(RetainedReceipt(current, lane));
        var health = await cycles.GetHealthAsync(current.Cycle.Identity.Competition, current.Cycle.Identity.Scope, BundesligaContextSource.ClubElo);
        await Assert.That(health!.ActiveConditions).IsEmpty();
        await Assert.That(health.ConsecutiveFailures.Acquisition).IsEqualTo(0);
        var original = await cycles.GetReceiptAsync(legacy.Cycle.Identity, BundesligaContextSource.ClubElo, legacy.Cycle.ExpectedConsumers[0]);
        await Assert.That(original!.Request.ActiveConditions.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments("missing-receipt")]
    [Arguments("crossed-community")]
    [Arguments("wrong-date")]
    [Arguments("wrong-bundle")]
    [Arguments("wrong-observation")]
    [Arguments("missing-head")]
    [Arguments("moved-head")]
    [Arguments("missing-payload")]
    [Arguments("changed-observed-time")]
    [Arguments("crossed-source-status")]
    public async Task Invalid_committed_publication_proof_fails_without_recording_the_next_receipt(string mutation)
    {
        var cycles = Repository();
        await CompleteBaselineAsync(cycles);
        var prepared = await PrepareAsync(cycles, 902);
        await PublishFourHeadsAsync(cycles, prepared);
        var earlierLane = BundesligaContextSourceContract.ProductionConsumers[3];
        var lane = BundesligaContextSourceContract.ProductionConsumers[4];
        var receipt = fixture.Db.Collection("context-source-cycle-receipts").Document(
            BundesligaContextSourceHashing.ReceiptStorageId(prepared.Cycle.Identity, BundesligaContextSource.ClubElo, earlierLane));
        var scope = new DocumentPublicationScope(prepared.Cycle.Identity.Competition, "ehonda-ai-arena", "club-elo");
        var head = fixture.Db.Collection("document-publication-heads").Document(DocumentPublicationContract.ComputeHeadId(scope));
        switch (mutation)
        {
            case "missing-receipt": await receipt.DeleteAsync(); break;
            case "crossed-community": await receipt.UpdateAsync("communityContext", "pes-squad"); break;
            case "wrong-date": await receipt.UpdateAsync("sourceDates.ratedAt", "2026-09-03"); break;
            case "wrong-bundle": await receipt.UpdateAsync("bundleDigest", new string('a', 64)); break;
            case "wrong-observation": await receipt.UpdateAsync("observationDigest", new string('a', 64)); break;
            case "missing-head": await head.DeleteAsync(); break;
            case "moved-head": await head.UpdateAsync("snapshotId", new string('a', 64)); break;
            case "missing-payload":
                var payloads = await fixture.Db.Collection("context-documents").WhereEqualTo("communityContext", "ehonda-ai-arena").GetSnapshotAsync();
                await payloads.Documents[0].Reference.DeleteAsync(); break;
            case "changed-observed-time":
                var metadata = JsonNode.Parse(prepared.Build.MetadataJson)!;
                metadata["collected_at"] = BundesligaContextSourceContract.FormatUtc(Now.AddSeconds(30));
                metadata["source_observed_at"] = BundesligaContextSourceContract.FormatUtc(Now.AddSeconds(30));
                await fixture.Db.Collection("document-publication-snapshots")
                    .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, prepared.SnapshotId))
                    .UpdateAsync("metadataJson", metadata.ToJsonString()); break;
            case "crossed-source-status":
                await fixture.Db.Collection("context-source-cycle-observations")
                    .Document(BundesligaContextSourceHashing.SourceCycleStorageId(prepared.Cycle.Identity, BundesligaContextSource.ClubElo))
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = "Aborted", ["abortCode"] = "HANDOFF_ARTIFACT_MISSING" }); break;
        }
        await Assert.That(() => cycles.RecordReceiptAsync(RetainedReceipt(prepared, lane))).Throws<InvalidDataException>();
        await Assert.That(await cycles.GetReceiptAsync(prepared.Cycle.Identity, BundesligaContextSource.ClubElo, lane)).IsNull();
        await Assert.That((await cycles.GetSourceCycleAsync(prepared.Cycle.Identity, BundesligaContextSource.ClubElo))!.ReceivedConsumers.Count).IsEqualTo(4);
    }

    private FirebaseContextSourceCycleRepository Repository() => new(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>());
    private FirebaseDocumentPublicationRepository Publications() => new(fixture.Db,
        new FakeLogger<FirebaseDocumentPublicationRepository>(), BundesligaContextSourceContract.Competition);
    private static string Community(string lane) => lane switch
    {
        "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse",
        "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena"
    };
    private static BundesligaContextSourceOuterCycle Cycle(long sequence) => new(
        BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, sequence),
        Now, Now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers,
        [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.Claiming);

    private async Task CompleteBaselineAsync(FirebaseContextSourceCycleRepository cycles)
    {
        var cycle = Cycle(901);
        var observation = BundesligaClubEloRefresh.CreateObservation(cycle.Identity, Now, "TransportRejected").Observation;
        await ReadyAsync(cycles, cycle, observation);
        foreach (var lane in cycle.ExpectedConsumers)
            await cycles.RecordReceiptAsync(new(cycle.Identity, BundesligaContextSource.ClubElo, lane, Community(lane),
                observation.ObservationDigest, Bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected,
                new string('b', 64), BundesligaContextSourceSelectedOrigin.LaunchSeed, BundesligaContextSourcePublicationDisposition.Published,
                new(BundesligaClubEloSeed.Default.RatedAt, null, null, null), null, new(0, 0, 0, null),
                [BundesligaContextSourceHealthCondition.AcquisitionFailed, BundesligaContextSourceHealthCondition.ClubEloSourceRejected,
                    BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days]));
    }

    private async Task<Prepared> PrepareAsync(FirebaseContextSourceCycleRepository cycles, long sequence, bool notNewer = false)
    {
        var cycle = Cycle(sequence);
        var mapping = BundesligaClubEloRefresh.CanonicalMappingBytes();
        var rows = Encoding.UTF8.GetString(mapping).Split("\r\n").Skip(1).Select((line, index) =>
        {
            var values = line.Split(',');
            return new BundesligaClubEloRefresh.Row(values[1], values[2], index + 1, 1500 + index);
        }).ToArray();
        var bytes = Encoding.UTF8.GetBytes("html");
        var response = new BundesligaClubEloRefresh.Response(200, BundesligaClubEloRefresh.SourceUrl, 0, null, "text/html", "utf-8", [], bytes.Length);
        var retained = cycle.ExpectedConsumers.ToDictionary(lane => lane, _ => BundesligaClubEloSeed.Default);
        var observation = BundesligaClubEloRefresh.Evaluate(cycle.Identity, Now, response, bytes, "2026-09-04", rows, mapping, retained).Observation;
        var selection = BundesligaClubEloRefresh.Select(observation, BundesligaClubEloSeed.Default);
        var build = BundesligaClubEloPublication.BuildSourceBacked(selection, cycle.Identity, observation, bytes);
        if (notNewer)
            observation = observation with { Disposition = BundesligaContextSourceDisposition.Rejected, Payload = null,
                DescriptorJson = observation.DescriptorJson.Replace("\"evaluation\":\"Eligible\"", "\"evaluation\":\"NotNewer\""),
                Diagnostics = ["CLUB_ELO_NOT_NEWER"] };
        await ReadyAsync(cycles, cycle, observation);
        return new(cycle, observation, build);
    }

    private static async Task ReadyAsync(FirebaseContextSourceCycleRepository cycles, BundesligaContextSourceOuterCycle cycle,
        BundesligaContextSourceObservation observation)
    {
        await cycles.CreateOrResumeCycleAsync(cycle);
        await cycles.ClaimSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token, Now);
        await cycles.FinalizeSourceAsync(cycle.Identity, BundesligaContextSource.ClubElo, Token, observation, Now.AddMinutes(1));
        await cycles.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, Bundle);
        var artifact = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}";
        await cycles.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, Bundle, artifact);
        await cycles.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, Bundle, artifact);
    }

    private async Task PublishFourHeadsAsync(FirebaseContextSourceCycleRepository cycles, Prepared prepared)
    {
        foreach (var lane in prepared.Cycle.ExpectedConsumers.Take(4))
        {
            var guard = new ContextSourcePublicationGuard(prepared.Cycle.Identity.Competition, prepared.Cycle.Identity.Scope,
                prepared.Cycle.Identity.CycleId, BundesligaContextSource.ClubElo, lane, Community(lane), "club-elo", Bundle,
                prepared.Observation.ObservationDigest, prepared.Cycle.Identity.Sequence, prepared.Cycle.Identity.CycleId);
            var template = new ContextSourcePublicationReceiptTemplate(BundesligaContextSourceSelectionDisposition.NetworkAccepted,
                BundesligaContextSourceSelectedOrigin.NetworkCandidate, new(new DateOnly(2026, 9, 4), null, null, null), null, new(0, 0, 0, null), []);
            await Publications().PublishAsync(BundesligaDocumentPublication.ClubElo, new(Community(lane), null,
                prepared.Build.Documents, prepared.Build.MetadataJson, new(guard, template)));
        }
    }

    private static BundesligaContextSourceReceiptRequest RetainedReceipt(Prepared prepared, string lane) => new(
        prepared.Cycle.Identity, BundesligaContextSource.ClubElo, lane, Community(lane), prepared.Observation.ObservationDigest,
        Bundle, BundesligaContextSourceSelectionDisposition.NetworkCandidateNotNewer, prepared.SnapshotId,
        BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted,
        new(new DateOnly(2026, 9, 4), null, null, null), null, new(0, 0, 0, null), []);
    private sealed record Prepared(BundesligaContextSourceOuterCycle Cycle, BundesligaContextSourceObservation Observation,
        BundesligaClubEloPublicationBuild Build)
    {
        public string SnapshotId => DocumentPublicationContract.ComputeSnapshotId(Build.Documents);
    }
}
