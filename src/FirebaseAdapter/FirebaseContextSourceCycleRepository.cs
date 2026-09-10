using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

public sealed class FirebaseContextSourceCycleRepository : IBundesligaContextSourceCycleRepository
{
    internal const string Cycles = "context-source-cycles";
    internal const string Observations = "context-source-cycle-observations";
    internal const string Receipts = "context-source-cycle-receipts";
    internal const string Health = "context-source-health";
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter() } };
    private static readonly string[] CycleFields = ["contract", "competition", "scope", "cycleId", "cycleSequence", "startedAtUtc", "stalenessReferenceAtUtc", "producerLaneId", "expectedConsumers", "enabledSources", "status", "bundleSha256", "artifactName", "abortCode", "completedAtUtc"];
    private static readonly string[] SourceFields = ["contract", "competition", "scope", "cycleId", "source", "attemptId", "status", "claimToken", "claimedAtUtc", "leaseExpiresAtUtc", "finalizedAtUtc", "observationDigest", "observation", "abortCode", "receivedConsumers", "completedAtUtc"];
    private static readonly string[] ReceiptFields = ["contract", "competition", "scope", "cycleId", "source", "consumerLaneId", "communityContext", "recordedAtUtc", "observationDigest", "bundleDigest", "selectionDisposition", "selectedSnapshotId", "selectedOrigin", "publicationDisposition", "sourceDates", "rosterRevision", "carriedFields", "activeConditions"];
    private static readonly string[] HealthFields = ["contract", "competition", "scope", "source", "watermark", "lastCompletedCycleId", "consecutiveFailures", "lastSuccessfulSourceDates", "rosterRevisionState", "communitySelections", "activeConditions", "desiredIssueProjection"];
    private static readonly string[] ObservationFields = ["source", "attemptId", "observedAtUtc", "disposition", "descriptorSha256", "descriptor", "payload", "diagnostics"];
    private static readonly string[] PayloadFields = ["path", "byteLength", "sha256"];
    private static readonly string[] EloDescriptorFields = ["contract", "sourceUrl", "rawSha256", "rawByteLength", "csvHeader", "providerRatedAt", "providerDateEvidence", "nameMappingContract", "nameMappingSha256", "sourceRows", "evaluation"];
    private static readonly string[] HtmlEloDescriptorFields = ["contract", "sourceUrl", "response", "rawSha256", "rawByteLength", "parserContract", "displayedDate", "providerDateEvidence", "tableContract", "tableHeader", "nameMappingContract", "nameMappingSha256", "sourceRows", "evaluation"];
    private static readonly string[] RosterDescriptorFields = ["contract", "metadataUrl", "artifactUrl", "advertisedRevision", "metadataSha256", "metadataByteLength", "remoteIdentityBefore", "acquisitionReason", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate", "policySha256", "retainedDescriptorSha256", "retainedEvaluation", "retainedDiagnostics", "evaluation"];
    private static readonly string[] DateEvidenceFields = ["kind", "recipeId", "field", "rawValue", "ratedAt"];
    private static readonly string[] SourceRowFields = ["teamSlug", "providerName", "globalRank", "elo"];
    private static readonly string[] HtmlSourceRowFields = ["teamSlug", "providerRoute", "providerDisplayName", "globalRank", "elo"];
    private static readonly string[] HtmlResponseFields = ["statusCode", "finalUrl", "redirectCount", "redirectLocation", "mediaType", "charset", "contentEncodings", "declaredContentLength"];
    private static readonly string[] RemoteIdentityFields = ["etag", "byteLength"];
    private static readonly string[] WatermarkFields = ["sequence", "cycleId"];
    private static readonly string[] FailureFields = ["acquisition", "membership", "enrichment", "handoff"];
    private static readonly string[] SuccessfulDateFields = ["ratedAt", "membershipEffectiveAt", "enrichmentCapturedAt"];
    private static readonly string[] RevisionStateFields = ["accepted", "pending"];
    private static readonly string[] AcceptedRevisionFields = ["revision", "remoteIdentity", "policySha256", "descriptorSha256"];
    private static readonly string[] PendingRevisionFields = ["revision", "remoteIdentity", "policySha256", "firstSeenCycleId", "lastFailureCode"];
    private static readonly string[] CommunitySelectionFields = ["consumerLaneId", "communityContext", "selectedSnapshotId", "selectedOrigin", "ratedAt", "membershipCapturedAt", "membershipEffectiveAt", "enrichmentCapturedAt", "conditions"];
    private static readonly string[] IssueProjectionFields = ["marker", "title", "bodySha256", "desiredState", "appliedBodySha256", "synchronizationStatus", "lastAttemptedAtUtc", "lastErrorCode"];
    private static readonly string[] ReceiptDateFields = ["ratedAt", "membershipCapturedAt", "membershipEffectiveAt", "enrichmentCapturedAt"];
    private static readonly string[] CarriedFieldFields = ["ageCount", "positionCount", "marketValueCount", "oldestFieldEffectiveAt"];
    private readonly FirestoreDb _db;
    private readonly ILogger<FirebaseContextSourceCycleRepository> _logger;
    private readonly TimeProvider _timeProvider;

    public FirebaseContextSourceCycleRepository(FirestoreDb db, ILogger<FirebaseContextSourceCycleRepository> logger, TimeProvider? timeProvider = null) { _db = db ?? throw new ArgumentNullException(nameof(db)); _logger = logger ?? throw new ArgumentNullException(nameof(logger)); _timeProvider = timeProvider ?? TimeProvider.System; }

    public async Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default)
        => (await CreateOrResumeCycleWithResultAsync(requested, cancellationToken)).Cycle;

    public Task<BundesligaContextSourceCycleStartResult> CreateOrResumeCycleWithResultAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default)
    {
        requested.Validate();
        if (requested.Status != BundesligaContextSourceCycleStatus.Claiming || requested.BundleSha256 is not null || requested.ArtifactName is not null || requested.AbortCode is not null || requested.CompletedAtUtc is not null) throw new InvalidDataException("New cycle must be a pristine Claiming request.");
        return _db.RunTransactionAsync(async transaction =>
        {
            var cycleRef = CycleReference(requested.Identity);
            var cycleSnapshot = await transaction.GetSnapshotAsync(cycleRef);
            var requestedWatermark = new BundesligaContextSourceWatermark(requested.Identity.Sequence, requested.Identity.CycleId);
            var healthSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
            foreach (var source in Enum.GetValues<BundesligaContextSource>())
                healthSnapshots[source] = await transaction.GetSnapshotAsync(HealthReference(requested.Identity, source));
            if (cycleSnapshot.Exists)
            {
                var existing = ParseCycle(cycleSnapshot, requested.Identity);
                if (!CycleRequestEquals(existing, requested)) throw new InvalidDataException("STATE_CONFLICT");
                foreach (var source in existing.EnabledSources)
                {
                    var snapshot = healthSnapshots[source];
                    if (!snapshot.Exists) throw new InvalidDataException("STATE_CONFLICT");
                    var health = ParseHealth(snapshot, requested.Identity, source);
                    var comparison = requestedWatermark.CompareTo(health.Watermark);
                    if (comparison < 0) throw new InvalidDataException("LATE_CYCLE");
                    if (comparison > 0) throw new InvalidDataException("STATE_CONFLICT");
                }
                return new BundesligaContextSourceCycleStartResult(existing, []);
            }
            var priorHealth = new Dictionary<BundesligaContextSource, BundesligaContextSourceHealth>();
            var priorCycles = new Dictionary<string, (BundesligaContextSourceCycleIdentity Identity, DocumentSnapshot Outer, Dictionary<BundesligaContextSource, DocumentSnapshot> Sources)>(StringComparer.Ordinal);
            foreach (var (source, snapshot) in healthSnapshots)
            {
                if (!snapshot.Exists) continue;
                var health = ParseHealth(snapshot, requested.Identity, source);
                priorHealth[source] = health;
                var comparison = requestedWatermark.CompareTo(health.Watermark);
                if (comparison < 0) throw new InvalidDataException("LATE_CYCLE");
                if (comparison == 0) throw new InvalidDataException("STATE_CONFLICT");
                if (comparison > 0 && health.LastCompletedCycleId != health.Watermark.CycleId && !priorCycles.ContainsKey(health.Watermark.CycleId))
                {
                    var priorIdentity = BundesligaContextSourceCycleIdentity.Create(requested.Identity.Competition, requested.Identity.Scope, health.Watermark.CycleId, health.Watermark.Sequence);
                    var priorOuter = await transaction.GetSnapshotAsync(CycleReference(priorIdentity));
                    var priorSources = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
                    if (priorOuter.Exists)
                    {
                        var parsed = ParseCycle(priorOuter, priorIdentity);
                        foreach (var enabled in parsed.EnabledSources) priorSources[enabled] = await transaction.GetSnapshotAsync(SourceReference(priorIdentity, enabled));
                    }
                    priorCycles.Add(health.Watermark.CycleId, (priorIdentity, priorOuter, priorSources));
                }
            }
            var supersededBySource = new Dictionary<BundesligaContextSource, BundesligaContextSourceOuterCycle>();
            foreach (var (_, prior) in priorCycles)
            {
                if (!prior.Outer.Exists) throw new InvalidDataException("STATE_CONFLICT");
                var old = ParseCycle(prior.Outer, prior.Identity);
                var priorWatermark = new BundesligaContextSourceWatermark(prior.Identity.Sequence, prior.Identity.CycleId);
                var incompleteSources = priorHealth
                    .Where(pair => pair.Value.Watermark == priorWatermark && pair.Value.LastCompletedCycleId != prior.Identity.CycleId)
                    .Select(pair => pair.Key)
                    .ToArray();
                if (incompleteSources.Any(source => !old.EnabledSources.Contains(source))) throw new InvalidDataException("STATE_CONFLICT");
                if (old.Status == BundesligaContextSourceCycleStatus.Complete) throw new InvalidDataException("STATE_CONFLICT");
                if (old.Status == BundesligaContextSourceCycleStatus.Aborted)
                {
                    if (incompleteSources.Any(source => priorHealth[source].ConsecutiveFailures.Handoff == 0
                            || !priorHealth[source].ActiveConditions.Contains(BundesligaContextSourceHealthCondition.CycleAborted)
                            || !priorHealth[source].ActiveConditions.Contains(BundesligaContextSourceHealthCondition.HandoffIncomplete)))
                        throw new InvalidDataException("STATE_CONFLICT");
                    continue;
                }
                foreach (var source in old.EnabledSources)
                {
                    if (!priorHealth.TryGetValue(source, out var health) || health.Watermark != priorWatermark)
                        throw new InvalidDataException("STATE_CONFLICT");
                    if (!prior.Sources.TryGetValue(source, out var snapshot) || !snapshot.Exists)
                    {
                        if (health.LastCompletedCycleId == prior.Identity.CycleId) throw new InvalidDataException("STATE_CONFLICT");
                        continue;
                    }
                    var sourceCycle = ParseSource(snapshot, prior.Identity, source, old.ExpectedConsumers);
                    var sourceComplete = sourceCycle.Status == BundesligaContextSourceSourceStatus.Complete;
                    if (sourceComplete != (health.LastCompletedCycleId == prior.Identity.CycleId)
                        || sourceCycle.Status == BundesligaContextSourceSourceStatus.Aborted)
                        throw new InvalidDataException("STATE_CONFLICT");
                }
                transaction.Set(CycleReference(prior.Identity), ToFirestore(old with { Status = BundesligaContextSourceCycleStatus.Aborted, AbortCode = BundesligaContextSourceError.SupersededIncompleteCycle }));
                foreach (var (source, snapshot) in prior.Sources)
                {
                    if (!snapshot.Exists) continue; var claim = ParseSource(snapshot, prior.Identity, source, old.ExpectedConsumers);
                    if (claim.Status is not (BundesligaContextSourceSourceStatus.Aborted or BundesligaContextSourceSourceStatus.Complete)) transaction.Set(SourceReference(prior.Identity, source), ToFirestore(AbortClaim(claim, BundesligaContextSourceError.SupersededIncompleteCycle)));
                }
                foreach (var source in old.EnabledSources)
                {
                    if (priorHealth.TryGetValue(source, out var health)
                        && health.Watermark == new BundesligaContextSourceWatermark(prior.Identity.Sequence, prior.Identity.CycleId)
                        && health.LastCompletedCycleId != prior.Identity.CycleId)
                        supersededBySource[source] = old;
                }
            }
            transaction.Create(cycleRef, ToFirestore(requested));
            foreach (var source in requested.EnabledSources)
            {
                priorHealth.TryGetValue(source, out var previous);
                transaction.Set(HealthReference(requested.Identity, source), ToFirestore(AdvanceForStartedCycle(previous, requested, source, supersededBySource.ContainsKey(source))));
            }
            foreach (var (source, priorCycle) in supersededBySource.Where(pair => !requested.EnabledSources.Contains(pair.Key)))
            {
                transaction.Set(HealthReference(requested.Identity, source), ToFirestore(ReduceAbortHealth(priorHealth[source], priorCycle, source)));
            }
            return new BundesligaContextSourceCycleStartResult(requested, supersededBySource.Keys.Order().ToArray());
        }, cancellationToken: cancellationToken);
    }

    public Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default)
    {
        BundesligaContextSourceContract.ValidateClaimToken(claimToken); BundesligaContextSourceContract.FormatUtc(claimedAtUtc);
        return _db.RunTransactionAsync(async transaction =>
        {
            var outerSnapshot = await transaction.GetSnapshotAsync(CycleReference(identity));
            var sourceRef = SourceReference(identity, source); var sourceSnapshot = await transaction.GetSnapshotAsync(sourceRef);
            var outer = ParseCycle(outerSnapshot, identity);
            if (!outer.EnabledSources.Contains(source)) throw new InvalidDataException("STATE_CONFLICT");
            if (!sourceSnapshot.Exists)
            {
                if (outer.Status != BundesligaContextSourceCycleStatus.Claiming) throw new InvalidDataException("STATE_CONFLICT");
                var created = new BundesligaContextSourceCycleClaim(identity, source, BundesligaContextSourceHashing.AttemptId(identity, source), BundesligaContextSourceSourceStatus.Claimed, claimToken, claimedAtUtc, claimedAtUtc.AddMinutes(10), null, null, null, null, [], null);
                created.Validate(outer.ExpectedConsumers); transaction.Create(sourceRef, ToFirestore(created));
                return new BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition.NewClaim, created);
            }
            var existing = ParseSource(sourceSnapshot, identity, source, outer.ExpectedConsumers);
            if (outer.Status == BundesligaContextSourceCycleStatus.Aborted)
            {
                var snapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot> { [source] = sourceSnapshot };
                foreach (var enabled in outer.EnabledSources.Where(enabled => enabled != source)) snapshots[enabled] = await transaction.GetSnapshotAsync(SourceReference(identity, enabled));
                var requested = existing;
                foreach (var (enabled, snapshot) in snapshots)
                {
                    if (!snapshot.Exists) continue;
                    var claim = ParseSource(snapshot, identity, enabled, outer.ExpectedConsumers);
                    var reconciled = claim.Status == BundesligaContextSourceSourceStatus.Complete
                        || (claim.Status == BundesligaContextSourceSourceStatus.Aborted && claim.AbortCode == outer.AbortCode)
                        ? claim
                        : AbortClaim(claim, outer.AbortCode!.Value);
                    if (!ReferenceEquals(reconciled, claim)) transaction.Set(SourceReference(identity, enabled), ToFirestore(reconciled));
                    if (enabled == source) requested = reconciled;
                }
                return new BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition.ExistingAborted, requested);
            }
            if (existing.Status is BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete) return new BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition.ExistingFinalized, existing);
            if (existing.Status == BundesligaContextSourceSourceStatus.Aborted) return new BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition.ExistingAborted, existing);
            if (claimedAtUtc >= existing.LeaseExpiresAtUtc)
            {
                var sourceSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot> { [source] = sourceSnapshot };
                foreach (var enabled in outer.EnabledSources.Where(enabled => enabled != source)) sourceSnapshots[enabled] = await transaction.GetSnapshotAsync(SourceReference(identity, enabled));
                var healthSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
                foreach (var enabled in outer.EnabledSources)
                    healthSnapshots[enabled] = await transaction.GetSnapshotAsync(HealthReference(identity, enabled));
                var aborted = AbortClaim(existing, BundesligaContextSourceError.AcquisitionInterrupted);
                var outerAborted = outer with { Status = BundesligaContextSourceCycleStatus.Aborted, AbortCode = BundesligaContextSourceError.AcquisitionInterrupted };
                foreach (var (enabled, snapshot) in sourceSnapshots)
                {
                    if (!snapshot.Exists) continue;
                    var claim = ParseSource(snapshot, identity, enabled, outer.ExpectedConsumers);
                    if (claim.Status != BundesligaContextSourceSourceStatus.Complete)
                        transaction.Set(SourceReference(identity, enabled), ToFirestore(AbortClaim(claim, BundesligaContextSourceError.AcquisitionInterrupted)));
                }
                transaction.Set(CycleReference(identity), ToFirestore(outerAborted));
                foreach (var enabled in outer.EnabledSources)
                {
                    var health = ParseHealth(healthSnapshots[enabled], identity, enabled);
                    if (IsCurrentIncompleteWatermark(health, identity))
                        transaction.Set(HealthReference(identity, enabled), ToFirestore(ReduceAbortHealth(health, outer, enabled)));
                }
                return new BundesligaContextSourceClaimResult(BundesligaContextSourceClaimDisposition.ExistingAborted, aborted);
            }
            return new BundesligaContextSourceClaimResult(existing.ClaimToken == claimToken ? BundesligaContextSourceClaimDisposition.OwnedClaim : BundesligaContextSourceClaimDisposition.Busy, existing);
        }, cancellationToken: cancellationToken);
    }

    public async Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default)
    {
        BundesligaContextSourceContract.ValidateClaimToken(claimToken); BundesligaContextSourceContract.FormatUtc(finalizedAtUtc); observation.Validate();
        if (observation.Source != source || observation.AttemptId != BundesligaContextSourceHashing.AttemptId(identity, source)) throw new InvalidDataException("Observation identity mismatch.");
        var replay = await GetSourceCycleAsync(identity, source, cancellationToken);
        if (replay?.Observation is not null)
        {
            if (replay.ClaimToken == claimToken && replay.ObservationDigest == observation.ObservationDigest) return replay;
            throw new InvalidDataException("STATE_CONFLICT");
        }
        var retainedDescriptor = observation.Disposition == BundesligaContextSourceDisposition.MetadataUnchanged
            ? await GetRetainedRosterDescriptorAsync(identity.Competition, identity.Scope, cancellationToken)
            : null;
        return await _db.RunTransactionAsync(async transaction =>
        {
            var outerRef = CycleReference(identity); var outerSnapshot = await transaction.GetSnapshotAsync(outerRef); var outer = ParseCycle(outerSnapshot, identity);
            var snapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
            foreach (var enabled in outer.EnabledSources) snapshots[enabled] = await transaction.GetSnapshotAsync(SourceReference(identity, enabled));
            var healthSnapshot = await transaction.GetSnapshotAsync(HealthReference(identity, source));
            var existing = ParseSource(snapshots[source], identity, source, outer.ExpectedConsumers);
            if (existing.Status is BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete)
            {
                if (existing.ClaimToken == claimToken && existing.ObservationDigest == observation.ObservationDigest) return existing;
                throw new InvalidDataException("STATE_CONFLICT");
            }
            if (existing.Status != BundesligaContextSourceSourceStatus.Claimed || existing.ClaimToken != claimToken || finalizedAtUtc < existing.ClaimedAtUtc || finalizedAtUtc >= existing.LeaseExpiresAtUtc) throw new InvalidDataException("STATE_CONFLICT");
            ValidateRosterAcquisitionReasonAgainstRevisionState(observation, healthSnapshot, identity, source);
            ValidateMetadataUnchangedAgainstPriorState(observation, healthSnapshot, identity, source, retainedDescriptor);
            var finalized = existing with { Status = BundesligaContextSourceSourceStatus.Finalized, FinalizedAtUtc = finalizedAtUtc, ObservationDigest = observation.ObservationDigest, Observation = observation };
            transaction.Set(SourceReference(identity, source), ToFirestore(finalized));
            var allFinal = outer.EnabledSources.All(enabled => enabled == source || snapshots[enabled].Exists && ParseSource(snapshots[enabled], identity, enabled, outer.ExpectedConsumers).Status is BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete);
            if (allFinal)
            {
                if (outer.Status != BundesligaContextSourceCycleStatus.Claiming) throw new InvalidDataException("STATE_CONFLICT");
                transaction.Set(outerRef, ToFirestore(outer with { Status = BundesligaContextSourceCycleStatus.ObservationsFinalized }));
            }
            return finalized;
        }, cancellationToken: cancellationToken);
    }

    public Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default)
    {
        return _db.RunTransactionAsync(async transaction =>
        {
            var reference = CycleReference(identity); var existing = ParseCycle(await transaction.GetSnapshotAsync(reference), identity);
            if (bundleSha256 is not null)
            {
                if (existing.BundleSha256 is not null && existing.BundleSha256 != bundleSha256) throw new InvalidDataException("STATE_CONFLICT");
                BundesligaContextSourceHashing.ValidateSha(bundleSha256);
            }
            if (artifactName is not null && existing.ArtifactName is not null && existing.ArtifactName != artifactName) throw new InvalidDataException("STATE_CONFLICT");
            if (existing.Status == next)
            {
                if (bundleSha256 is not null && existing.BundleSha256 != bundleSha256
                    || artifactName is not null && existing.ArtifactName != artifactName)
                    throw new InvalidDataException("STATE_CONFLICT");
                return existing;
            }
            if (existing.Status != expected || !AllowedTransition(identity.Scope, expected, next)) throw new InvalidDataException("STATE_CONFLICT");
            var updated = existing with { Status = next, BundleSha256 = bundleSha256 ?? existing.BundleSha256, ArtifactName = artifactName ?? existing.ArtifactName };
            try { updated.Validate(); }
            catch (InvalidDataException) { throw new InvalidDataException("STATE_CONFLICT"); }
            transaction.Set(reference, ToFirestore(updated)); return updated;
        }, cancellationToken: cancellationToken);
    }

    public Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default)
    {
        return _db.RunTransactionAsync(async transaction =>
        {
            var outerRef = CycleReference(identity); var outer = ParseCycle(await transaction.GetSnapshotAsync(outerRef), identity);
            var sourceSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
            foreach (var source in outer.EnabledSources) sourceSnapshots[source] = await transaction.GetSnapshotAsync(SourceReference(identity, source));
            var healthSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
            foreach (var source in outer.EnabledSources) healthSnapshots[source] = await transaction.GetSnapshotAsync(HealthReference(identity, source));
            if (outer.Status == BundesligaContextSourceCycleStatus.Complete) throw new InvalidDataException("STATE_CONFLICT");
            if (outer.Status == BundesligaContextSourceCycleStatus.Aborted)
            {
                var persistedError = outer.AbortCode ?? throw new InvalidDataException("Stored aborted cycle has no abort code.");
                foreach (var (source, snapshot) in sourceSnapshots)
                {
                    if (!snapshot.Exists) continue;
                    var claim = ParseSource(snapshot, identity, source, outer.ExpectedConsumers);
                    if (claim.Status == BundesligaContextSourceSourceStatus.Complete) continue;
                    if (claim.Status != BundesligaContextSourceSourceStatus.Aborted || claim.AbortCode != persistedError)
                        transaction.Set(SourceReference(identity, source), ToFirestore(AbortClaim(claim, persistedError)));
                }
                return outer;
            }
            var aborted = outer with { Status = BundesligaContextSourceCycleStatus.Aborted, AbortCode = error };
            transaction.Set(outerRef, ToFirestore(aborted));
            foreach (var (source, snapshot) in sourceSnapshots)
            {
                if (!snapshot.Exists) continue;
                var claim = ParseSource(snapshot, identity, source, outer.ExpectedConsumers);
                if (claim.Status == BundesligaContextSourceSourceStatus.Complete) continue;
                if (claim.Status == BundesligaContextSourceSourceStatus.Aborted && claim.AbortCode == error) continue;
                transaction.Set(SourceReference(identity, source), ToFirestore(AbortClaim(claim, error)));
            }
            foreach (var source in outer.EnabledSources)
            {
                var health = ParseHealth(healthSnapshots[source], identity, source);
                if (!IsCurrentIncompleteWatermark(health, identity)) continue;
                transaction.Set(HealthReference(identity, source), ToFirestore(ReduceAbortHealth(health, outer, source)));
            }
            return aborted;
        }, cancellationToken: cancellationToken);
    }

    public Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        return _db.RunTransactionAsync(async transaction =>
        {
            var targetRef = ReceiptReference(request); var targetSnapshot = await transaction.GetSnapshotAsync(targetRef);
            if (targetSnapshot.Exists)
            {
                var persisted = ParseReceipt(targetSnapshot, request.Identity, request.Source, request.ConsumerLaneId);
                if (ReceiptSemanticJson(persisted.Request) != ReceiptSemanticJson(request)) throw new InvalidDataException("STATE_CONFLICT");
                return persisted;
            }
            var outerRef = CycleReference(request.Identity); var outerSnapshot = await transaction.GetSnapshotAsync(outerRef); var outer = ParseCycle(outerSnapshot, request.Identity);
            if (outer.Status is not (BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete) || outer.BundleSha256 != request.BundleDigest) throw new InvalidDataException("STATE_CONFLICT");
            var sourceSnapshots = new Dictionary<BundesligaContextSource, DocumentSnapshot>();
            foreach (var source in outer.EnabledSources) sourceSnapshots[source] = await transaction.GetSnapshotAsync(SourceReference(request.Identity, source));
            var sourceCycle = ParseSource(sourceSnapshots[request.Source], request.Identity, request.Source, outer.ExpectedConsumers);
            if (sourceCycle.ObservationDigest != request.ObservationDigest || sourceCycle.Status is not (BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete)) throw new InvalidDataException("STATE_CONFLICT");
            BundesligaContextSourceReceiptContract.ValidateAgainstObservation(request, sourceCycle.Observation!);
            if (request.Source == BundesligaContextSource.Rosters && request.RosterRevision != AdvertisedRevision(sourceCycle.Observation!)) throw new InvalidDataException("STATE_CONFLICT");
            var healthRef = HealthReference(request.Identity, request.Source); var healthSnapshot = await transaction.GetSnapshotAsync(healthRef);
            if (!healthSnapshot.Exists) throw new InvalidDataException("STATE_CONFLICT");
            var health = ParseHealth(healthSnapshot, request.Identity, request.Source);
            ValidateRosterAcquisitionReasonAgainstRevisionState(sourceCycle.Observation!, healthSnapshot, request.Identity, request.Source);
            var receiptSnapshots = new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);
            foreach (var lane in outer.ExpectedConsumers) receiptSnapshots[lane] = await transaction.GetSnapshotAsync(ReceiptReference(request.Identity, request.Source, lane));
            if (request.SelectionDisposition == BundesligaContextSourceSelectionDisposition.MetadataUnchanged)
            {
                var priorCycleId = health.LastCompletedCycleId ?? throw new InvalidDataException("STATE_CONFLICT");
                var priorIdentity = BundesligaContextSourceCycleIdentity.FromCycleId(request.Identity.Competition, request.Identity.Scope, priorCycleId);
                if (priorIdentity == request.Identity) throw new InvalidDataException("STATE_CONFLICT");
                var priorOuter = ParseCycle(await transaction.GetSnapshotAsync(CycleReference(priorIdentity)), priorIdentity);
                if (priorOuter.Status != BundesligaContextSourceCycleStatus.Complete || !priorOuter.EnabledSources.Contains(request.Source))
                    throw new InvalidDataException("STATE_CONFLICT");
                var priorSource = ParseSource(await transaction.GetSnapshotAsync(SourceReference(priorIdentity, request.Source)), priorIdentity, request.Source, priorOuter.ExpectedConsumers);
                if (priorSource.Status != BundesligaContextSourceSourceStatus.Complete || priorSource.Observation is null)
                    throw new InvalidDataException("STATE_CONFLICT");
                var priorSnapshot = await transaction.GetSnapshotAsync(ReceiptReference(priorIdentity, request.Source, request.ConsumerLaneId));
                var priorReceipt = ParseReceipt(priorSnapshot, priorIdentity, request.Source, request.ConsumerLaneId);
                var priorSelection = health.CommunitySelections.SingleOrDefault(selection => selection.ConsumerLaneId == request.ConsumerLaneId);
                if (priorSelection is null) throw new InvalidDataException("STATE_CONFLICT");
                try
                {
                    BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
                        health, priorSelection, priorReceipt, priorSource.Observation,
                        priorOuter,
                        DateOnly.FromDateTime(priorOuter.StalenessReferenceAtUtc.UtcDateTime));
                }
                catch (InvalidDataException)
                {
                    throw new InvalidDataException("STATE_CONFLICT");
                }
                BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(request, priorReceipt);
            }
            if (request.Source == BundesligaContextSource.ClubElo
                && request.PublicationDisposition == BundesligaContextSourcePublicationDisposition.NotAttempted
                && IsHtmlClubEloObservation(sourceCycle.Observation!))
            {
                var priorCycleId = health.LastCompletedCycleId ?? throw new InvalidDataException("STATE_CONFLICT");
                var priorIdentity = BundesligaContextSourceCycleIdentity.FromCycleId(request.Identity.Competition, request.Identity.Scope, priorCycleId);
                if (priorIdentity == request.Identity) throw new InvalidDataException("STATE_CONFLICT");
                var priorOuter = ParseCycle(await transaction.GetSnapshotAsync(CycleReference(priorIdentity)), priorIdentity);
                if (priorOuter.Status != BundesligaContextSourceCycleStatus.Complete || !priorOuter.EnabledSources.Contains(request.Source))
                    throw new InvalidDataException("STATE_CONFLICT");
                var priorSource = ParseSource(await transaction.GetSnapshotAsync(SourceReference(priorIdentity, request.Source)), priorIdentity, request.Source, priorOuter.ExpectedConsumers);
                if (priorSource.Status != BundesligaContextSourceSourceStatus.Complete || priorSource.Observation is null)
                    throw new InvalidDataException("STATE_CONFLICT");
                var priorReceipt = ParseReceipt(await transaction.GetSnapshotAsync(ReceiptReference(priorIdentity, request.Source, request.ConsumerLaneId)), priorIdentity, request.Source, request.ConsumerLaneId);
                var selections = health.CommunitySelections.Where(selection => selection.ConsumerLaneId == request.ConsumerLaneId).ToArray();
                if (selections.Length != 1) throw new InvalidDataException("STATE_CONFLICT");
                try
                {
                    BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
                        health, selections[0], priorReceipt, priorSource.Observation, priorOuter,
                        DateOnly.FromDateTime(priorOuter.StalenessReferenceAtUtc.UtcDateTime));
                }
                catch (InvalidDataException)
                {
                    throw new InvalidDataException("STATE_CONFLICT");
                }
                if (request.SelectedOrigin != BundesligaContextSourceSelectedOrigin.LastKnownGood
                    || request.ConsumerLaneId != priorReceipt.Request.ConsumerLaneId
                    || request.CommunityContext != priorReceipt.Request.CommunityContext
                    || request.SelectedSnapshotId != priorReceipt.Request.SelectedSnapshotId
                    || request.SourceDates.RatedAt != priorReceipt.Request.SourceDates.RatedAt)
                    throw new InvalidDataException("STATE_CONFLICT");
                var publicationScope = new DocumentPublicationScope(request.Identity.Competition, request.CommunityContext, BundesligaDocumentPublication.ClubEloPublicationSet);
                var publicationHead = await transaction.GetSnapshotAsync(PublicationHeadReference(publicationScope));
                ValidateClubEloPublicationHead(publicationHead, publicationScope, request.SelectedSnapshotId);
            }
            BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(request, DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime));
            var expectedLane = outer.ExpectedConsumers.ElementAtOrDefault(sourceCycle.ReceivedConsumers.Count);
            if (expectedLane != request.ConsumerLaneId) throw new InvalidDataException("STATE_CONFLICT");
            var recordedAtUtc = TruncateUtc(_timeProvider.GetUtcNow());
            var receipt = new BundesligaContextSourceReceipt(request, recordedAtUtc); receipt.Validate();
            var received = sourceCycle.ReceivedConsumers.Append(request.ConsumerLaneId).ToArray();
            var sourceComplete = received.Length == outer.ExpectedConsumers.Count;
            var completedSource = sourceCycle with { ReceivedConsumers = received, Status = sourceComplete ? BundesligaContextSourceSourceStatus.Complete : sourceCycle.Status, CompletedAtUtc = sourceComplete ? recordedAtUtc : null };
            completedSource.Validate(outer.ExpectedConsumers);
            transaction.Create(targetRef, ToFirestore(receipt));
            transaction.Set(SourceReference(request.Identity, request.Source), ToFirestore(completedSource));
            if (sourceComplete)
            {
                var allReceipts = outer.ExpectedConsumers.Select(lane => lane == request.ConsumerLaneId ? receipt : ParseReceipt(receiptSnapshots[lane], request.Identity, request.Source, lane)).ToArray();
                var reduced = BundesligaContextSourceHealthReducer.ReduceCompleted(health, outer, completedSource, allReceipts);
                transaction.Set(healthRef, ToFirestore(reduced));
                var allSourcesComplete = outer.EnabledSources.All(source => source == request.Source || ParseSource(sourceSnapshots[source], request.Identity, source, outer.ExpectedConsumers) is { Status: BundesligaContextSourceSourceStatus.Complete } other && other.ReceivedConsumers.SequenceEqual(outer.ExpectedConsumers, StringComparer.Ordinal));
                if (allSourcesComplete)
                {
                    var completedOuter = outer with { Status = BundesligaContextSourceCycleStatus.Complete, CompletedAtUtc = recordedAtUtc };
                    transaction.Set(outerRef, ToFirestore(completedOuter));
                }
            }
            return receipt;
        }, cancellationToken: cancellationToken);
    }

    public async Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default) { var snapshot = await CycleReference(identity).GetSnapshotAsync(cancellationToken); return snapshot.Exists ? ParseCycle(snapshot, identity) : null; }
    public async Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default) { var outer = await GetCycleAsync(identity, cancellationToken); if (outer is null) return null; var snapshot = await SourceReference(identity, source).GetSnapshotAsync(cancellationToken); return snapshot.Exists ? ParseSource(snapshot, identity, source, outer.ExpectedConsumers) : null; }
    public async Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default)
    {
        var outer = await GetCycleAsync(identity, cancellationToken);
        if (outer is null) return null;
        if (!outer.EnabledSources.Contains(source) || !outer.ExpectedConsumers.Contains(consumerLaneId, StringComparer.Ordinal)) throw new InvalidDataException("STATE_CONFLICT");
        var snapshot = await ReceiptReference(identity, source, consumerLaneId).GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? ParseReceipt(snapshot, identity, source, consumerLaneId) : null;
    }
    public async Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default) { if (!Enum.IsDefined(scope)) throw new InvalidDataException("Context-source scope is invalid."); var probe = scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceCycleIdentity.Production(competition, 1, 1) : BundesligaContextSourceCycleIdentity.Development(competition, "0198f865-1467-7000-8000-000000000000"); var snapshot = await HealthReference(probe, source).GetSnapshotAsync(cancellationToken); if (!snapshot.Exists) return null; var map = snapshot.ToDictionary(); var watermark = map.TryGetValue("watermark", out var value) && value is IDictionary<string, object> nested && nested.TryGetValue("cycleId", out var cycleValue) ? cycleValue as string : null; if (watermark is null) throw new InvalidDataException("Stored health watermark is invalid."); var sequence = ((IDictionary<string, object>)map["watermark"])["sequence"] is long number ? number : throw new InvalidDataException("Stored health sequence is invalid."); var identity = BundesligaContextSourceCycleIdentity.Create(competition, scope, watermark, sequence); return ParseHealth(snapshot, identity, source); }

    public async Task<BundesligaContextSourceRetainedRosterDescriptor?> GetRetainedRosterDescriptorAsync(string competition, BundesligaContextSourceScope scope, CancellationToken cancellationToken = default)
    {
        var health = await GetHealthAsync(competition, scope, BundesligaContextSource.Rosters, cancellationToken);
        var accepted = health?.RosterRevisionState?.Accepted;
        if (accepted is null) return null;
        if (health!.LastCompletedCycleId is null) throw new InvalidDataException("Accepted roster state has no completed source cycle.");
        var candidates = new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);
        foreach (var query in new[]
                 {
                     _db.Collection(Observations).WhereEqualTo("observation.descriptorSha256", accepted.DescriptorSha256),
                     _db.Collection(Observations).WhereEqualTo("observation.descriptor.retainedDescriptorSha256", accepted.DescriptorSha256)
                 })
        {
            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            foreach (var document in snapshot.Documents) candidates[document.Id] = document;
        }

        (BundesligaContextSourceWatermark Watermark, string Evaluation, IReadOnlyList<string> Diagnostics)? retained = null;
        foreach (var candidate in candidates.Values)
        {
            var map = candidate.ToDictionary();
            RequireMapFields(map, SourceFields);
            if (map["contract"] as string != BundesligaContextSourceCycleClaim.Contract
                || map["competition"] as string != competition
                || map["scope"] as string != BundesligaContextSourceContract.ScopeValue(scope)
                || map["source"] as string != BundesligaContextSourceContract.SourceValue(BundesligaContextSource.Rosters)
                || map["cycleId"] is not string cycleId)
                continue;
            var identity = BundesligaContextSourceCycleIdentity.FromCycleId(competition, scope, cycleId);
            var outer = await GetCycleAsync(identity, cancellationToken) ?? throw new InvalidDataException("Accepted roster descriptor outer cycle is missing.");
            var completed = ParseSource(candidate, identity, BundesligaContextSource.Rosters, outer.ExpectedConsumers);
            var watermark = new BundesligaContextSourceWatermark(identity.Sequence, identity.CycleId);
            if (completed.Status != BundesligaContextSourceSourceStatus.Complete || completed.Observation is null || watermark.CompareTo(health.Watermark) > 0)
                continue;

            using var descriptorDocument = JsonDocument.Parse(completed.Observation.DescriptorJson);
            var descriptor = descriptorDocument.RootElement;
            if (descriptor.GetProperty("advertisedRevision").GetString() != accepted.Revision
                || DescriptorRemoteIdentity(descriptor) != accepted.RemoteIdentity
                || descriptor.GetProperty("policySha256").GetString() != accepted.PolicySha256)
                throw new InvalidDataException("Accepted roster descriptor immutable tuple does not match health.");
            string descriptorSha256;
            string evaluation;
            IReadOnlyList<string> diagnostics;
            if (completed.Observation.Disposition == BundesligaContextSourceDisposition.MetadataUnchanged)
            {
                descriptorSha256 = descriptor.GetProperty("retainedDescriptorSha256").GetString()!;
                evaluation = descriptor.GetProperty("retainedEvaluation").GetString()!;
                diagnostics = descriptor.GetProperty("retainedDiagnostics").EnumerateArray().Select(value => value.GetString()!).ToArray();
            }
            else
            {
                descriptorSha256 = completed.Observation.DescriptorSha256;
                evaluation = descriptor.GetProperty("evaluation").GetString()!;
                diagnostics = completed.Observation.Diagnostics.ToArray();
            }
            if (descriptorSha256 != accepted.DescriptorSha256) continue;
            if (retained is null || watermark.CompareTo(retained.Value.Watermark) > 0)
                retained = (watermark, evaluation, diagnostics);
        }
        if (retained is null) throw new InvalidDataException("Accepted roster descriptor source cycle is missing.");
        var result = new BundesligaContextSourceRetainedRosterDescriptor(
            accepted.DescriptorSha256,
            accepted.Revision,
            accepted.RemoteIdentity,
            accepted.PolicySha256,
            retained.Value.Evaluation,
            retained.Value.Diagnostics);
        result.Validate();
        return result;
    }

    public Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default)
    {
        expectedHealth.Validate(); projection.Validate();
        if (expectedHealth.Scope != BundesligaContextSourceScope.ProductionLive || expectedHealth.DesiredIssueProjection is not { } expectedProjection)
            throw new InvalidDataException("Only production health can update an issue projection.");
        if (!SameDesiredProjection(expectedProjection, projection)) throw new InvalidDataException("Issue projection update changes desired state.");
        var identity = BundesligaContextSourceCycleIdentity.Create(expectedHealth.Competition, expectedHealth.Scope, expectedHealth.Watermark.CycleId, expectedHealth.Watermark.Sequence);
        return _db.RunTransactionAsync(async transaction =>
        {
            var reference = HealthReference(identity, expectedHealth.Source);
            var current = ParseHealth(await transaction.GetSnapshotAsync(reference), identity, expectedHealth.Source);
            if (current.Watermark != expectedHealth.Watermark || current.LastCompletedCycleId != expectedHealth.LastCompletedCycleId
                || current.DesiredIssueProjection is not { } currentProjection || !SameDesiredProjection(expectedProjection, currentProjection))
                throw new InvalidDataException("STATE_CONFLICT");
            if (currentProjection == projection) return current;
            if (currentProjection != expectedProjection) throw new InvalidDataException("STATE_CONFLICT");
            var updated = current with { DesiredIssueProjection = projection };
            updated.Validate(); transaction.Set(reference, ToFirestore(updated)); return updated;
        }, cancellationToken: cancellationToken);
    }

    private DocumentReference CycleReference(BundesligaContextSourceCycleIdentity identity) => _db.Collection(Cycles).Document(identity.StorageId);
    private DocumentReference SourceReference(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source) => _db.Collection(Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(identity, source));
    private DocumentReference ReceiptReference(BundesligaContextSourceReceiptRequest request) => _db.Collection(Receipts).Document(request.StorageId);
    private DocumentReference ReceiptReference(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string lane) => _db.Collection(Receipts).Document(BundesligaContextSourceHashing.ReceiptStorageId(identity, source, lane));
    private DocumentReference HealthReference(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source) => _db.Collection(Health).Document(BundesligaContextSourceHashing.HealthStorageId(identity.Competition, identity.ScopeValue, source));
    private DocumentReference PublicationHeadReference(DocumentPublicationScope scope) => _db.Collection("document-publication-heads").Document(DocumentPublicationContract.ComputeHeadId(scope));
    private static bool AllowedTransition(BundesligaContextSourceScope scope, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next) => (expected, next) switch { (BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified) => true, (BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved) => scope == BundesligaContextSourceScope.ProductionLive, (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady) => scope == BundesligaContextSourceScope.ProductionLive, (BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady) => scope == BundesligaContextSourceScope.Development, _ => false };
    private static bool CycleRequestEquals(BundesligaContextSourceOuterCycle left, BundesligaContextSourceOuterCycle right) => left.Identity == right.Identity && left.ProducerLaneId == right.ProducerLaneId && left.ExpectedConsumers.SequenceEqual(right.ExpectedConsumers, StringComparer.Ordinal) && left.EnabledSources.SequenceEqual(right.EnabledSources);
    private static bool SameDesiredProjection(BundesligaContextSourceIssueProjection left, BundesligaContextSourceIssueProjection right)
        => left.Marker == right.Marker && left.Title == right.Title && left.BodySha256 == right.BodySha256 && left.DesiredState == right.DesiredState;

    private static BundesligaContextSourceCycleClaim AbortClaim(BundesligaContextSourceCycleClaim claim, BundesligaContextSourceError error)
    {
        if (claim.Status == BundesligaContextSourceSourceStatus.Complete) return claim;
        var aborted = claim with { Status = BundesligaContextSourceSourceStatus.Aborted, AbortCode = error, CompletedAtUtc = null };
        aborted.Validate(claim.Identity.Scope == BundesligaContextSourceScope.ProductionLive ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers);
        return aborted;
    }

    private static string AdvertisedRevision(BundesligaContextSourceObservation observation)
    {
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        return document.RootElement.GetProperty("advertisedRevision").GetString()!;
    }

    private static BundesligaContextSourceHealth AdvanceForStartedCycle(BundesligaContextSourceHealth? previous, BundesligaContextSourceOuterCycle cycle, BundesligaContextSource source, bool superseded)
    {
        var failures = previous?.ConsecutiveFailures ?? new BundesligaContextSourceFailures(0, 0, 0, 0);
        var dates = previous?.LastSuccessfulSourceDates ?? new BundesligaContextSourceSuccessfulDates(null, null, null);
        if (superseded) failures = failures with { Handoff = failures.Handoff + 1 };
        var conditions = BundesligaContextSourceHealth.OrderConditions((previous?.ActiveConditions ?? []).Append(BundesligaContextSourceHealthCondition.HandoffIncomplete).Concat(superseded ? [BundesligaContextSourceHealthCondition.CycleAborted] : []));
        if (superseded)
            conditions = RecomputeRetainedDateConditions(source, dates, previous?.CommunitySelections ?? [], cycle.StalenessReferenceAtUtc, conditions);
        var health = new BundesligaContextSourceHealth(cycle.Identity.Competition, cycle.Identity.Scope, source, new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId), previous?.LastCompletedCycleId, failures, dates, source == BundesligaContextSource.Rosters ? previous?.RosterRevisionState ?? new BundesligaContextSourceRosterRevisionState(null, null) : null, previous?.CommunitySelections ?? [], conditions, ProjectIssue(cycle.Identity.Scope, source, new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId), failures, conditions));
        health.Validate(); return health;
    }

    private static BundesligaContextSourceIssueProjection? ProjectIssue(BundesligaContextSourceScope scope, BundesligaContextSource source, BundesligaContextSourceWatermark watermark, BundesligaContextSourceFailures failures, IReadOnlyList<BundesligaContextSourceHealthCondition> conditions)
    {
        if (scope == BundesligaContextSourceScope.Development) return null;
        var sourceValue = BundesligaContextSourceContract.SourceValue(source); var marker = $"<!-- kicktippai:context-source-health:bundesliga-2026-27:{sourceValue} -->"; var body = BundesligaContextSourceHealth.CreateIssueBody(marker, BundesligaContextSourceContract.Competition, source, watermark, conditions);
        var open = failures.Acquisition >= 2 || failures.Membership >= 2 || failures.Enrichment >= 2 || failures.Handoff >= 2 || conditions.Any(x => x is BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days);
        return new BundesligaContextSourceIssueProjection(marker, $"[KicktippAi] Bundesliga 2026/27 {sourceValue} context-source health", BundesligaContextSourceHealth.HashIssueBody(body), open ? BundesligaContextSourceIssueState.Open : BundesligaContextSourceIssueState.Closed, null, BundesligaContextSourceIssueSynchronization.Pending, null, null);
    }

    private static bool IsCurrentIncompleteWatermark(BundesligaContextSourceHealth health, BundesligaContextSourceCycleIdentity identity)
        => health.Watermark.Sequence == identity.Sequence
           && health.Watermark.CycleId == identity.CycleId
           && health.LastCompletedCycleId != identity.CycleId;

    private static BundesligaContextSourceHealth ReduceAbortHealth(BundesligaContextSourceHealth health, BundesligaContextSourceOuterCycle cycle, BundesligaContextSource source)
    {
        var failures = health.ConsecutiveFailures with { Handoff = health.ConsecutiveFailures.Handoff + 1 };
        var conditions = RecomputeRetainedDateConditions(source, health.LastSuccessfulSourceDates, health.CommunitySelections, cycle.StalenessReferenceAtUtc, health.ActiveConditions
            .Append(BundesligaContextSourceHealthCondition.CycleAborted)
            .Append(BundesligaContextSourceHealthCondition.HandoffIncomplete));
        var updated = health with
        {
            ConsecutiveFailures = failures,
            ActiveConditions = conditions,
            DesiredIssueProjection = ProjectIssue(cycle.Identity.Scope, source, health.Watermark, failures, conditions)
        };
        updated.Validate();
        return updated;
    }

    private static BundesligaContextSourceHealthCondition[] RecomputeRetainedDateConditions(
        BundesligaContextSource source,
        BundesligaContextSourceSuccessfulDates dates,
        IReadOnlyList<BundesligaContextSourceCommunitySelection> communitySelections,
        DateTimeOffset stalenessReferenceAtUtc,
        IEnumerable<BundesligaContextSourceHealthCondition> retainedConditions)
    {
        var conditions = retainedConditions.Where(condition => !IsStaleOrUnknown(condition)).ToHashSet();
        var reference = DateOnly.FromDateTime(stalenessReferenceAtUtc.UtcDateTime);
        if (source == BundesligaContextSource.ClubElo)
        {
            if (dates.RatedAt is { } ratedAt && reference.DayNumber - ratedAt.DayNumber > 7)
                conditions.Add(BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days);
        }
        else
        {
            ApplyRosterRetainedDateCondition(dates.MembershipEffectiveAt, reference, true, conditions);
            ApplyRosterRetainedDateCondition(dates.EnrichmentCapturedAt, reference, false, conditions);
            if (communitySelections.Any(selection => selection.EnrichmentCapturedAt is null))
                conditions.Add(BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
        }
        return BundesligaContextSourceHealth.OrderConditions(conditions);
    }

    private static void ApplyRosterRetainedDateCondition(
        DateOnly? sourceDate,
        DateOnly reference,
        bool membership,
        ISet<BundesligaContextSourceHealthCondition> conditions)
    {
        if (sourceDate is null)
        {
            conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown : BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown);
            return;
        }
        var age = reference.DayNumber - sourceDate.Value.DayNumber;
        if (age > 14)
            conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days : BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days);
        if (age > 30)
            conditions.Add(membership ? BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days : BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days);
    }

    private static bool IsStaleOrUnknown(BundesligaContextSourceHealthCondition condition)
        => condition is BundesligaContextSourceHealthCondition.ClubEloStaleGt7Days
            or BundesligaContextSourceHealthCondition.RosterMembershipDateUnknown
            or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt14Days
            or BundesligaContextSourceHealthCondition.RosterMembershipStaleGt30Days
            or BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown
            or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt14Days
            or BundesligaContextSourceHealthCondition.RosterEnrichmentStaleGt30Days;

    private static void ValidateMetadataUnchangedAgainstPriorState(
        BundesligaContextSourceObservation observation,
        DocumentSnapshot healthSnapshot,
        BundesligaContextSourceCycleIdentity identity,
        BundesligaContextSource source,
        BundesligaContextSourceRetainedRosterDescriptor? retainedDescriptor)
    {
        if (observation.Disposition != BundesligaContextSourceDisposition.MetadataUnchanged) return;
        if (source != BundesligaContextSource.Rosters || !healthSnapshot.Exists)
            throw new InvalidDataException("STATE_CONFLICT");
        var health = ParseHealth(healthSnapshot, identity, source);
        var accepted = health.RosterRevisionState?.Accepted;
        if (accepted is null || health.RosterRevisionState!.Pending is not null || retainedDescriptor is null)
            throw new InvalidDataException("STATE_CONFLICT");
        retainedDescriptor.Validate();
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        var root = document.RootElement;
        var remoteIdentity = DescriptorRemoteIdentity(root);
        if (root.GetProperty("advertisedRevision").GetString() != accepted.Revision
            || remoteIdentity != accepted.RemoteIdentity
            || root.GetProperty("policySha256").GetString() != accepted.PolicySha256
            || root.GetProperty("retainedDescriptorSha256").GetString() != accepted.DescriptorSha256
            || retainedDescriptor.DescriptorSha256 != accepted.DescriptorSha256
            || retainedDescriptor.Revision != accepted.Revision
            || retainedDescriptor.RemoteIdentity != accepted.RemoteIdentity
            || retainedDescriptor.PolicySha256 != accepted.PolicySha256
            || root.GetProperty("retainedEvaluation").GetString() != retainedDescriptor.Evaluation
            || !root.GetProperty("retainedDiagnostics").EnumerateArray().Select(value => value.GetString()!).SequenceEqual(retainedDescriptor.Diagnostics, StringComparer.Ordinal))
            throw new InvalidDataException("STATE_CONFLICT");
    }

    internal static void ValidateRosterAcquisitionReasonAgainstRevisionState(
        BundesligaContextSourceObservation observation,
        DocumentSnapshot healthSnapshot,
        BundesligaContextSourceCycleIdentity identity,
        BundesligaContextSource source)
    {
        if (source != BundesligaContextSource.Rosters) return;
        if (!healthSnapshot.Exists) throw new InvalidDataException("STATE_CONFLICT");
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        var descriptor = document.RootElement;
        var evaluation = descriptor.GetProperty("evaluation").GetString();
        var reason = descriptor.GetProperty("acquisitionReason").ValueKind == JsonValueKind.Null
            ? null
            : descriptor.GetProperty("acquisitionReason").GetString();
        if (evaluation is "MetadataUnavailable" or "MetadataMalformed" or "MetadataRevisionRejected")
        {
            if (reason is not null) throw new InvalidDataException("STATE_CONFLICT");
            return;
        }

        var health = ParseHealth(healthSnapshot, identity, source);
        var revision = descriptor.GetProperty("advertisedRevision").GetString()
            ?? throw new InvalidDataException("STATE_CONFLICT");
        var policy = descriptor.GetProperty("policySha256").GetString()
            ?? throw new InvalidDataException("STATE_CONFLICT");
        var before = descriptor.GetProperty("remoteIdentityBefore");
        var expected = ExpectedAcquisitionReason(health.RosterRevisionState, revision, policy,
            before.ValueKind == JsonValueKind.Null ? null : DescriptorRemoteIdentity(descriptor));
        // Identity unavailability does not erase revision-state evidence.  A null reason is
        // valid only when the deterministic state calculation itself produces null.
        if (reason != expected)
            throw new InvalidDataException("STATE_CONFLICT");
    }

    private static string? ExpectedAcquisitionReason(
        BundesligaContextSourceRosterRevisionState? state,
        string revision,
        string policy,
        BundesligaContextSourceRemoteIdentity? identity)
    {
        var pending = state?.Pending;
        if (pending is not null)
            return pending.Revision != revision ? "NewRevision"
                : pending.PolicySha256 != policy ? "PolicyChanged"
                : "PendingRevision";

        var accepted = state?.Accepted;
        if (accepted is null || accepted.Revision != revision) return "NewRevision";
        if (accepted.PolicySha256 != policy) return "PolicyChanged";
        if (identity is null) return null;
        return accepted.RemoteIdentity == identity ? "AcceptedRevisionUnchanged" : "RemoteIdentityChanged";
    }

    private static BundesligaContextSourceRemoteIdentity DescriptorRemoteIdentity(JsonElement descriptor)
    {
        var before = descriptor.GetProperty("remoteIdentityBefore");
        return new BundesligaContextSourceRemoteIdentity(
            before.GetProperty("etag").ValueKind == JsonValueKind.Null ? null : before.GetProperty("etag").GetString(),
            before.GetProperty("byteLength").ValueKind == JsonValueKind.Null ? null : before.GetProperty("byteLength").GetInt64());
    }

    internal static FirestoreContextSourceCycle ToFirestore(BundesligaContextSourceOuterCycle value) => new() { Contract = BundesligaContextSourceOuterCycle.Contract, Competition = value.Identity.Competition, Scope = value.Identity.ScopeValue, CycleId = value.Identity.CycleId, CycleSequence = value.Identity.Sequence, StartedAtUtc = BundesligaContextSourceContract.FormatUtc(value.StartedAtUtc), StalenessReferenceAtUtc = BundesligaContextSourceContract.FormatUtc(value.StalenessReferenceAtUtc), ProducerLaneId = value.ProducerLaneId, ExpectedConsumers = value.ExpectedConsumers.ToList(), EnabledSources = value.EnabledSources.Select(BundesligaContextSourceContract.SourceValue).ToList(), Status = value.Status.ToString(), BundleSha256 = value.BundleSha256, ArtifactName = value.ArtifactName, AbortCode = value.AbortCode is null ? null : BundesligaContextSourceContract.ErrorValue(value.AbortCode.Value), CompletedAtUtc = value.CompletedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(value.CompletedAtUtc.Value) };
    internal static FirestoreContextSourceCycleObservation ToFirestore(BundesligaContextSourceCycleClaim value) => new() { Contract = BundesligaContextSourceCycleClaim.Contract, Competition = value.Identity.Competition, Scope = value.Identity.ScopeValue, CycleId = value.Identity.CycleId, Source = BundesligaContextSourceContract.SourceValue(value.Source), AttemptId = value.AttemptId, Status = value.Status.ToString(), ClaimToken = value.ClaimToken, ClaimedAtUtc = BundesligaContextSourceContract.FormatUtc(value.ClaimedAtUtc), LeaseExpiresAtUtc = BundesligaContextSourceContract.FormatUtc(value.LeaseExpiresAtUtc), FinalizedAtUtc = value.FinalizedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(value.FinalizedAtUtc.Value), ObservationDigest = value.ObservationDigest, Observation = value.Observation is null ? null : JsonObjectToMap(value.Observation.CreateCanonicalUtf8()), AbortCode = value.AbortCode is null ? null : BundesligaContextSourceContract.ErrorValue(value.AbortCode.Value), ReceivedConsumers = value.ReceivedConsumers.ToList(), CompletedAtUtc = value.CompletedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(value.CompletedAtUtc.Value) };
    internal static FirestoreContextSourceReceipt ToFirestore(BundesligaContextSourceReceipt value) => new() { Contract = BundesligaContextSourceReceipt.Contract, Competition = value.Request.Identity.Competition, Scope = value.Request.Identity.ScopeValue, CycleId = value.Request.Identity.CycleId, Source = BundesligaContextSourceContract.SourceValue(value.Request.Source), ConsumerLaneId = value.Request.ConsumerLaneId, CommunityContext = value.Request.CommunityContext, RecordedAtUtc = BundesligaContextSourceContract.FormatUtc(value.RecordedAtUtc), ObservationDigest = value.Request.ObservationDigest, BundleDigest = value.Request.BundleDigest, SelectionDisposition = value.Request.SelectionDisposition.ToString(), SelectedSnapshotId = value.Request.SelectedSnapshotId, SelectedOrigin = value.Request.SelectedOrigin.ToString(), PublicationDisposition = value.Request.PublicationDisposition.ToString(), SourceDates = new FirestoreContextSourceDates { RatedAt = Date(value.Request.SourceDates.RatedAt), MembershipCapturedAt = Date(value.Request.SourceDates.MembershipCapturedAt), MembershipEffectiveAt = Date(value.Request.SourceDates.MembershipEffectiveAt), EnrichmentCapturedAt = Date(value.Request.SourceDates.EnrichmentCapturedAt) }, RosterRevision = value.Request.RosterRevision, CarriedFields = new FirestoreContextSourceCarriedFields { AgeCount = value.Request.CarriedFields.AgeCount, PositionCount = value.Request.CarriedFields.PositionCount, MarketValueCount = value.Request.CarriedFields.MarketValueCount, OldestFieldEffectiveAt = Date(value.Request.CarriedFields.OldestFieldEffectiveAt) }, ActiveConditions = value.Request.ActiveConditions.Select(BundesligaContextSourceHealth.ToContractValue).ToList() };
    internal static FirestoreContextSourceHealth ToFirestore(BundesligaContextSourceHealth value) => new()
    {
        Contract = BundesligaContextSourceHealth.Contract, Competition = value.Competition, Scope = BundesligaContextSourceContract.ScopeValue(value.Scope), Source = BundesligaContextSourceContract.SourceValue(value.Source),
        Watermark = new FirestoreContextSourceWatermark { Sequence = value.Watermark.Sequence, CycleId = value.Watermark.CycleId }, LastCompletedCycleId = value.LastCompletedCycleId,
        ConsecutiveFailures = new FirestoreContextSourceFailures { Acquisition = value.ConsecutiveFailures.Acquisition, Membership = value.ConsecutiveFailures.Membership, Enrichment = value.ConsecutiveFailures.Enrichment, Handoff = value.ConsecutiveFailures.Handoff },
        LastSuccessfulSourceDates = new FirestoreContextSourceSuccessfulDates { RatedAt = Date(value.LastSuccessfulSourceDates.RatedAt), MembershipEffectiveAt = Date(value.LastSuccessfulSourceDates.MembershipEffectiveAt), EnrichmentCapturedAt = Date(value.LastSuccessfulSourceDates.EnrichmentCapturedAt) },
        RosterRevisionState = value.RosterRevisionState is null ? null : ToFirestore(value.RosterRevisionState),
        CommunitySelections = value.CommunitySelections.Select(ToFirestore).ToList(),
        ActiveConditions = value.ActiveConditions.Select(BundesligaContextSourceHealth.ToContractValue).ToList(),
        DesiredIssueProjection = value.DesiredIssueProjection is null ? null : ToFirestore(value.DesiredIssueProjection)
    };

    internal static BundesligaContextSourceOuterCycle ParseCycle(DocumentSnapshot snapshot, BundesligaContextSourceCycleIdentity identity) { RequireSnapshot(snapshot, snapshot.Reference.Parent.Id, identity.StorageId, CycleFields); var v = snapshot.ConvertTo<FirestoreContextSourceCycle>(); if (v.Contract != BundesligaContextSourceOuterCycle.Contract || v.Competition != identity.Competition || v.Scope != identity.ScopeValue || v.CycleId != identity.CycleId || v.CycleSequence != identity.Sequence || !TryParseDefined(v.Status, out BundesligaContextSourceCycleStatus status)) throw new InvalidDataException("Stored cycle identity/enums are invalid."); var result = new BundesligaContextSourceOuterCycle(identity, BundesligaContextSourceContract.ParseUtc(v.StartedAtUtc), BundesligaContextSourceContract.ParseUtc(v.StalenessReferenceAtUtc), v.ProducerLaneId, v.ExpectedConsumers, v.EnabledSources.Select(BundesligaContextSourceContract.ParseSource).ToArray(), status, v.BundleSha256, v.ArtifactName, v.AbortCode is null ? null : BundesligaContextSourceContract.ParseError(v.AbortCode), v.CompletedAtUtc is null ? null : BundesligaContextSourceContract.ParseUtc(v.CompletedAtUtc)); result.Validate(); return result; }
    internal static BundesligaContextSourceCycleClaim ParseSource(DocumentSnapshot snapshot, BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, IReadOnlyList<string> consumers) { RequireSnapshot(snapshot, snapshot.Reference.Parent.Id, BundesligaContextSourceHashing.SourceCycleStorageId(identity, source), SourceFields); var v = snapshot.ConvertTo<FirestoreContextSourceCycleObservation>(); if (v.Contract != BundesligaContextSourceCycleClaim.Contract || v.Competition != identity.Competition || v.Scope != identity.ScopeValue || v.CycleId != identity.CycleId || v.Source != BundesligaContextSourceContract.SourceValue(source) || !TryParseDefined(v.Status, out BundesligaContextSourceSourceStatus status)) throw new InvalidDataException("Stored source-cycle identity/enums are invalid."); var observation = v.Observation is null ? null : ParseObservationMap(v.Observation, source); var result = new BundesligaContextSourceCycleClaim(identity, source, v.AttemptId, status, v.ClaimToken, BundesligaContextSourceContract.ParseUtc(v.ClaimedAtUtc), BundesligaContextSourceContract.ParseUtc(v.LeaseExpiresAtUtc), v.FinalizedAtUtc is null ? null : BundesligaContextSourceContract.ParseUtc(v.FinalizedAtUtc), v.ObservationDigest, observation, v.AbortCode is null ? null : BundesligaContextSourceContract.ParseError(v.AbortCode), v.ReceivedConsumers, v.CompletedAtUtc is null ? null : BundesligaContextSourceContract.ParseUtc(v.CompletedAtUtc)); result.Validate(consumers); return result; }
    internal static BundesligaContextSourceReceipt ParseReceipt(DocumentSnapshot snapshot, BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string lane) { RequireSnapshot(snapshot, snapshot.Reference.Parent.Id, BundesligaContextSourceHashing.ReceiptStorageId(identity, source, lane), ReceiptFields); var map = snapshot.ToDictionary(); RequireNestedMap(map, "sourceDates", ReceiptDateFields); RequireNestedMap(map, "carriedFields", CarriedFieldFields); var v = snapshot.ConvertTo<FirestoreContextSourceReceipt>(); if (v.Contract != BundesligaContextSourceReceipt.Contract || v.Competition != identity.Competition || v.Scope != identity.ScopeValue || v.CycleId != identity.CycleId || v.Source != BundesligaContextSourceContract.SourceValue(source) || v.ConsumerLaneId != lane || !TryParseDefined(v.SelectionDisposition, out BundesligaContextSourceSelectionDisposition selection) || !TryParseDefined(v.SelectedOrigin, out BundesligaContextSourceSelectedOrigin origin) || !TryParseDefined(v.PublicationDisposition, out BundesligaContextSourcePublicationDisposition publication)) throw new InvalidDataException("Stored receipt identity/enums are invalid."); var request = new BundesligaContextSourceReceiptRequest(identity, source, lane, v.CommunityContext, v.ObservationDigest, v.BundleDigest, selection, v.SelectedSnapshotId, origin, publication, new BundesligaContextSourceDates(ParseDate(v.SourceDates.RatedAt), ParseDate(v.SourceDates.MembershipCapturedAt), ParseDate(v.SourceDates.MembershipEffectiveAt), ParseDate(v.SourceDates.EnrichmentCapturedAt)), v.RosterRevision, new BundesligaContextSourceCarriedFields(v.CarriedFields.AgeCount, v.CarriedFields.PositionCount, v.CarriedFields.MarketValueCount, ParseDate(v.CarriedFields.OldestFieldEffectiveAt)), v.ActiveConditions.Select(ParseCondition).ToArray()); var result = new BundesligaContextSourceReceipt(request, BundesligaContextSourceContract.ParseUtc(v.RecordedAtUtc)); result.Validate(); return result; }
    internal static BundesligaContextSourceHealth ParseHealth(DocumentSnapshot snapshot, BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source) { RequireSnapshot(snapshot, snapshot.Reference.Parent.Id, BundesligaContextSourceHashing.HealthStorageId(identity.Competition, identity.ScopeValue, source), HealthFields); var map = snapshot.ToDictionary(); ValidateHealthNestedShape(map); var v = snapshot.ConvertTo<FirestoreContextSourceHealth>(); if (v.Contract != BundesligaContextSourceHealth.Contract || v.Competition != identity.Competition || v.Scope != identity.ScopeValue || v.Source != BundesligaContextSourceContract.SourceValue(source)) throw new InvalidDataException("Stored health identity is invalid."); var dates = new BundesligaContextSourceSuccessfulDates(ParseDate(v.LastSuccessfulSourceDates.RatedAt), ParseDate(v.LastSuccessfulSourceDates.MembershipEffectiveAt), ParseDate(v.LastSuccessfulSourceDates.EnrichmentCapturedAt)); var result = new BundesligaContextSourceHealth(v.Competition, identity.Scope, source, new BundesligaContextSourceWatermark(v.Watermark.Sequence, v.Watermark.CycleId), v.LastCompletedCycleId, new BundesligaContextSourceFailures(v.ConsecutiveFailures.Acquisition, v.ConsecutiveFailures.Membership, v.ConsecutiveFailures.Enrichment, v.ConsecutiveFailures.Handoff), dates, v.RosterRevisionState is null ? null : FromFirestore(v.RosterRevisionState), v.CommunitySelections.Select(FromFirestore).ToArray(), v.ActiveConditions.Select(ParseCondition).ToArray(), v.DesiredIssueProjection is null ? null : FromFirestore(v.DesiredIssueProjection)); result.Validate(); return result; }
    private static void RequireSnapshot(DocumentSnapshot snapshot, string collection, string expectedId, IReadOnlyList<string> fields) { if (!snapshot.Exists || snapshot.Id != expectedId || snapshot.Reference.Parent.Id != collection || !snapshot.ToDictionary().Keys.Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Stored document identity/property set is invalid."); }
    private static void ValidateClubEloPublicationHead(DocumentSnapshot snapshot, DocumentPublicationScope scope, string expectedSnapshotId)
    {
        var headId = DocumentPublicationContract.ComputeHeadId(scope);
        RequireSnapshot(snapshot, "document-publication-heads", headId, ["competition", "communityContext", "publicationSet", "snapshotId"]);
        FirestoreDocumentPublicationHead head;
        try { head = snapshot.ConvertTo<FirestoreDocumentPublicationHead>(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("STATE_CONFLICT");
        }
        if (head.Competition != scope.Competition
            || head.CommunityContext != scope.CommunityContext
            || head.PublicationSet != scope.PublicationSet)
            throw new InvalidDataException("STATE_CONFLICT");
        try
        {
            BundesligaContextSourceHashing.ValidateSha(head.SnapshotId);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("STATE_CONFLICT");
        }
        if (head.SnapshotId != expectedSnapshotId) throw new InvalidDataException("STATE_CONFLICT");
    }
    private static bool IsHtmlClubEloObservation(BundesligaContextSourceObservation observation)
    {
        using var document = JsonDocument.Parse(observation.DescriptorJson);
        return document.RootElement.TryGetProperty("contract", out var contract)
            && contract.ValueKind == JsonValueKind.String
            && contract.GetString() == "club-elo-official-html-descriptor/v1";
    }
    internal static string ReceiptSemanticJson(BundesligaContextSourceReceiptRequest value) => JsonSerializer.Serialize(value, JsonOptions);
    private static FirestoreContextSourceRosterRevisionState ToFirestore(BundesligaContextSourceRosterRevisionState value) => new()
    {
        Accepted = value.Accepted is null ? null : new FirestoreContextSourceAcceptedRevision { Revision = value.Accepted.Revision, RemoteIdentity = ToFirestore(value.Accepted.RemoteIdentity), PolicySha256 = value.Accepted.PolicySha256, DescriptorSha256 = value.Accepted.DescriptorSha256 },
        Pending = value.Pending is null ? null : new FirestoreContextSourcePendingRevision { Revision = value.Pending.Revision, RemoteIdentity = ToFirestore(value.Pending.RemoteIdentity), PolicySha256 = value.Pending.PolicySha256, FirstSeenCycleId = value.Pending.FirstSeenCycleId, LastFailureCode = value.Pending.LastFailureCode }
    };
    private static FirestoreContextSourceRemoteIdentity ToFirestore(BundesligaContextSourceRemoteIdentity value) => new() { Etag = value.Etag, ByteLength = value.ByteLength };
    private static FirestoreContextSourceCommunitySelection ToFirestore(BundesligaContextSourceCommunitySelection value) => new() { ConsumerLaneId = value.ConsumerLaneId, CommunityContext = value.CommunityContext, SelectedSnapshotId = value.SelectedSnapshotId, SelectedOrigin = value.SelectedOrigin.ToString(), RatedAt = Date(value.RatedAt), MembershipCapturedAt = Date(value.MembershipCapturedAt), MembershipEffectiveAt = Date(value.MembershipEffectiveAt), EnrichmentCapturedAt = Date(value.EnrichmentCapturedAt), Conditions = value.Conditions.Select(BundesligaContextSourceHealth.ToContractValue).ToList() };
    private static FirestoreContextSourceIssueProjection ToFirestore(BundesligaContextSourceIssueProjection value) => new() { Marker = value.Marker, Title = value.Title, BodySha256 = value.BodySha256, DesiredState = value.DesiredState.ToString(), AppliedBodySha256 = value.AppliedBodySha256, SynchronizationStatus = value.SynchronizationStatus.ToString(), LastAttemptedAtUtc = value.LastAttemptedAtUtc is null ? null : BundesligaContextSourceContract.FormatUtc(value.LastAttemptedAtUtc.Value), LastErrorCode = value.LastErrorCode is null ? null : IssueErrorValue(value.LastErrorCode.Value) };

    private static BundesligaContextSourceRosterRevisionState FromFirestore(FirestoreContextSourceRosterRevisionState value) => new(
        value.Accepted is null ? null : new BundesligaContextSourceAcceptedRevision(value.Accepted.Revision, FromFirestore(value.Accepted.RemoteIdentity), value.Accepted.PolicySha256, value.Accepted.DescriptorSha256),
        value.Pending is null ? null : new BundesligaContextSourcePendingRevision(value.Pending.Revision, FromFirestore(value.Pending.RemoteIdentity), value.Pending.PolicySha256, value.Pending.FirstSeenCycleId, value.Pending.LastFailureCode));
    private static BundesligaContextSourceRemoteIdentity FromFirestore(FirestoreContextSourceRemoteIdentity value) => new(value.Etag, value.ByteLength);
    private static BundesligaContextSourceCommunitySelection FromFirestore(FirestoreContextSourceCommunitySelection value)
    {
        if (!TryParseDefined(value.SelectedOrigin, out BundesligaContextSourceSelectedOrigin origin)) throw new InvalidDataException("Stored selection origin is invalid.");
        return new BundesligaContextSourceCommunitySelection(value.ConsumerLaneId, value.CommunityContext, value.SelectedSnapshotId, origin, ParseDate(value.RatedAt), ParseDate(value.MembershipCapturedAt), ParseDate(value.MembershipEffectiveAt), ParseDate(value.EnrichmentCapturedAt), value.Conditions.Select(ParseCondition).ToArray());
    }
    private static BundesligaContextSourceIssueProjection FromFirestore(FirestoreContextSourceIssueProjection value)
    {
        if (!TryParseDefined(value.DesiredState, out BundesligaContextSourceIssueState desired) || !TryParseDefined(value.SynchronizationStatus, out BundesligaContextSourceIssueSynchronization synchronization)) throw new InvalidDataException("Stored issue projection enums are invalid.");
        return new BundesligaContextSourceIssueProjection(value.Marker, value.Title, value.BodySha256, desired, value.AppliedBodySha256, synchronization, value.LastAttemptedAtUtc is null ? null : BundesligaContextSourceContract.ParseUtc(value.LastAttemptedAtUtc), value.LastErrorCode is null ? null : ParseIssueError(value.LastErrorCode));
    }

    private static Dictionary<string, object?> JsonObjectToMap(byte[] canonicalJson)
    {
        using var document = JsonDocument.Parse(canonicalJson);
        return (Dictionary<string, object?>)JsonValueToFirestore(document.RootElement)!;
    }

    private static object? JsonValueToFirestore(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => JsonValueToFirestore(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => value.EnumerateArray().Select(JsonValueToFirestore).ToList(),
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new InvalidDataException("Unsupported canonical JSON value.")
    };

    private static BundesligaContextSourceObservation ParseObservationMap(IReadOnlyDictionary<string, object?> observation, BundesligaContextSource source)
    {
        RequireMapFields(observation, ObservationFields);
        var descriptor = RequireMap(observation["descriptor"]);
        ValidateStoredDescriptorNumberTypes(descriptor, source);
        IReadOnlyDictionary<string, object?>? payload = null;
        if (observation["payload"] is not null)
        {
            payload = RequireMap(observation["payload"]);
            RequireMapFields(payload, PayloadFields);
            RequireStoredInt64(payload, "byteLength");
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteProperty(writer, "source", observation["source"]); WriteProperty(writer, "attemptId", observation["attemptId"]); WriteProperty(writer, "observedAtUtc", observation["observedAtUtc"]); WriteProperty(writer, "disposition", observation["disposition"]); WriteProperty(writer, "descriptorSha256", observation["descriptorSha256"]);
            writer.WritePropertyName("descriptor"); WriteDescriptor(writer, descriptor, source);
            writer.WritePropertyName("payload"); if (payload is null) writer.WriteNullValue(); else WriteOrderedMap(writer, payload, PayloadFields);
            writer.WritePropertyName("diagnostics"); WriteValue(writer, observation["diagnostics"]); writer.WriteEndObject();
        }
        return BundesligaContextSourceDescriptorContract.ParseObservation(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static void ValidateStoredDescriptorNumberTypes(
        IReadOnlyDictionary<string, object?> descriptor,
        BundesligaContextSource source)
    {
        RequireMapFields(descriptor, source == BundesligaContextSource.ClubElo ? EloFieldsFor(descriptor) : RosterDescriptorFields);
        if (source == BundesligaContextSource.ClubElo)
        {
            RequireStoredInt64(descriptor, "rawByteLength", allowNull: true);
            if (IsHtmlEloDescriptor(descriptor) && descriptor["response"] is not null)
            {
                var response = RequireMap(descriptor["response"]); RequireMapFields(response, HtmlResponseFields);
                RequireStoredInt64(response, "statusCode"); RequireStoredInt64(response, "redirectCount"); RequireStoredInt64(response, "declaredContentLength", allowNull: true);
            }
            if (descriptor["sourceRows"] is null) return;
            if (descriptor["sourceRows"] is not System.Collections.IEnumerable rows || descriptor["sourceRows"] is string)
                throw new InvalidDataException("Stored canonical array is invalid.");
            foreach (var row in rows)
            {
                var value = RequireMap(row); RequireStoredInt64(value, "globalRank");
                if (IsHtmlEloDescriptor(descriptor)) RequireStoredInt64(value, "elo");
            }
            return;
        }

        RequireStoredInt64(descriptor, "metadataByteLength", allowNull: true);
        RequireStoredInt64(descriptor, "rawByteLength", allowNull: true);
        foreach (var field in new[] { "remoteIdentityBefore", "remoteIdentityAfter" })
        {
            if (descriptor[field] is not null)
                RequireStoredInt64(RequireMap(descriptor[field]), "byteLength", allowNull: true);
        }
    }

    private static void RequireStoredInt64(
        IReadOnlyDictionary<string, object?> map,
        string field,
        bool allowNull = false)
    {
        if (!map.TryGetValue(field, out var value)
            || value is null && !allowNull
            || value is not null && value is not long)
            throw new InvalidDataException($"Stored canonical integer '{field}' must use Firestore's integer type.");
    }

    private static void WriteDescriptor(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> descriptor, BundesligaContextSource source)
    {
        var fields = source == BundesligaContextSource.ClubElo ? EloFieldsFor(descriptor) : RosterDescriptorFields;
        RequireMapFields(descriptor, fields); writer.WriteStartObject();
        foreach (var field in fields)
        {
            writer.WritePropertyName(field); var value = descriptor[field];
            if (field == "providerDateEvidence" && value is not null) WriteOrderedMap(writer, RequireMap(value), DateEvidenceFields);
            else if (field == "response" && value is not null) WriteOrderedMap(writer, RequireMap(value), HtmlResponseFields);
            else if (field == "sourceRows" && value is not null) WriteOrderedMapArray(writer, value, source == BundesligaContextSource.ClubElo && IsHtmlEloDescriptor(descriptor) ? HtmlSourceRowFields : SourceRowFields);
            else if (field is "remoteIdentityBefore" or "remoteIdentityAfter" && value is not null) WriteOrderedMap(writer, RequireMap(value), RemoteIdentityFields);
            else WriteValue(writer, value);
        }
        writer.WriteEndObject();
    }

    private static void WriteOrderedMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?> map, IReadOnlyList<string> fields)
    {
        RequireMapFields(map, fields); writer.WriteStartObject(); foreach (var field in fields) WriteProperty(writer, field, map[field]); writer.WriteEndObject();
    }
    private static void WriteOrderedMapArray(Utf8JsonWriter writer, object value, IReadOnlyList<string> fields)
    {
        if (value is not System.Collections.IEnumerable values || value is string) throw new InvalidDataException("Stored canonical array is invalid.");
        writer.WriteStartArray(); foreach (var item in values) WriteOrderedMap(writer, RequireMap(item), fields); writer.WriteEndArray();
    }
    private static void WriteProperty(Utf8JsonWriter writer, string name, object? value) { writer.WritePropertyName(name); WriteValue(writer, value); }
    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string text: writer.WriteStringValue(text); break;
            case long number: writer.WriteNumberValue(number); break;
            case int number: writer.WriteNumberValue(number); break;
            case double number: writer.WriteNumberValue(number); break;
            case bool flag: writer.WriteBooleanValue(flag); break;
            case System.Collections.IEnumerable values when value is not string:
                writer.WriteStartArray(); foreach (var item in values) WriteValue(writer, item); writer.WriteEndArray(); break;
            default: throw new InvalidDataException("Stored canonical value type is invalid.");
        }
    }

    private static IReadOnlyDictionary<string, object?> RequireMap(object? value)
        => value as IReadOnlyDictionary<string, object?> ?? throw new InvalidDataException("Stored canonical map is invalid.");
    private static bool IsHtmlEloDescriptor(IReadOnlyDictionary<string, object?> descriptor)
        => descriptor.TryGetValue("contract", out var contract) && contract is string value && value == "club-elo-official-html-descriptor/v1";
    private static IReadOnlyList<string> EloFieldsFor(IReadOnlyDictionary<string, object?> descriptor)
    {
        if (!descriptor.TryGetValue("contract", out var contract) || contract is not string value) throw new InvalidDataException("Stored Club Elo descriptor contract is invalid.");
        return value switch
        {
            "club-elo-direct-csv-descriptor/v1" => EloDescriptorFields,
            "club-elo-official-html-descriptor/v1" => HtmlEloDescriptorFields,
            _ => throw new InvalidDataException("Stored Club Elo descriptor contract is invalid.")
        };
    }
    private static void RequireMapFields(IReadOnlyDictionary<string, object?> map, IReadOnlyList<string> fields)
    {
        if (!map.Keys.Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException("Stored nested property set is invalid.");
    }
    private static IReadOnlyDictionary<string, object?> RequireNestedMap(IReadOnlyDictionary<string, object> root, string name, IReadOnlyList<string> fields)
    {
        if (!root.TryGetValue(name, out var value)) throw new InvalidDataException("Stored nested property is missing.");
        var map = RequireMap(value); RequireMapFields(map, fields); return map;
    }

    private static void ValidateHealthNestedShape(IReadOnlyDictionary<string, object> root)
    {
        RequireNestedMap(root, "watermark", WatermarkFields); RequireNestedMap(root, "consecutiveFailures", FailureFields); RequireNestedMap(root, "lastSuccessfulSourceDates", SuccessfulDateFields);
        if (root["rosterRevisionState"] is not null)
        {
            var state = RequireNestedMap(root, "rosterRevisionState", RevisionStateFields);
            foreach (var (name, fields) in new[] { ("accepted", (IReadOnlyList<string>)AcceptedRevisionFields), ("pending", (IReadOnlyList<string>)PendingRevisionFields) })
            {
                if (state[name] is null) continue; var revision = RequireMap(state[name]); RequireMapFields(revision, fields); var remote = RequireMap(revision["remoteIdentity"]); RequireMapFields(remote, RemoteIdentityFields);
            }
        }
        if (root["communitySelections"] is not System.Collections.IEnumerable selections || root["communitySelections"] is string) throw new InvalidDataException("Stored community selections are invalid.");
        foreach (var selectionValue in selections) { var selection = RequireMap(selectionValue); RequireMapFields(selection, CommunitySelectionFields); }
        if (root["desiredIssueProjection"] is not null) { var issue = RequireMap(root["desiredIssueProjection"]); RequireMapFields(issue, IssueProjectionFields); }
    }

    private static string IssueErrorValue(BundesligaContextSourceIssueError value) => value switch
    {
        BundesligaContextSourceIssueError.GithubIssueListFailed => "GITHUB_ISSUE_LIST_FAILED", BundesligaContextSourceIssueError.GithubIssueCreateFailed => "GITHUB_ISSUE_CREATE_FAILED", BundesligaContextSourceIssueError.GithubIssueUpdateFailed => "GITHUB_ISSUE_UPDATE_FAILED", BundesligaContextSourceIssueError.GithubIssueCloseFailed => "GITHUB_ISSUE_CLOSE_FAILED", _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static BundesligaContextSourceIssueError ParseIssueError(string value) => value switch
    {
        "GITHUB_ISSUE_LIST_FAILED" => BundesligaContextSourceIssueError.GithubIssueListFailed, "GITHUB_ISSUE_CREATE_FAILED" => BundesligaContextSourceIssueError.GithubIssueCreateFailed, "GITHUB_ISSUE_UPDATE_FAILED" => BundesligaContextSourceIssueError.GithubIssueUpdateFailed, "GITHUB_ISSUE_CLOSE_FAILED" => BundesligaContextSourceIssueError.GithubIssueCloseFailed, _ => throw new InvalidDataException("Stored issue error is invalid.")
    };
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static DateTimeOffset TruncateUtc(DateTimeOffset value) => new(value.UtcDateTime.Ticks - value.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    private static DateOnly? ParseDate(string? value) => value is null ? null : DateOnly.TryParseExact(value, "yyyy-MM-dd", out var result) && result.ToString("yyyy-MM-dd") == value ? result : throw new InvalidDataException("Stored date is invalid.");
    private static BundesligaContextSourceHealthCondition ParseCondition(string value) => Enum.GetValues<BundesligaContextSourceHealthCondition>().SingleOrDefault(x => BundesligaContextSourceHealth.ToContractValue(x) == value) is var parsed && BundesligaContextSourceHealth.ToContractValue(parsed) == value ? parsed : throw new InvalidDataException("Stored health condition is invalid.");
    private static bool TryParseDefined<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
        => Enum.TryParse(value, false, out parsed) && Enum.IsDefined(parsed) && value == parsed.ToString();
}
