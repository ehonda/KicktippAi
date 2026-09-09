using System.Collections.Immutable;
using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firestore transaction boundary for complete mixed context/KPI publications.
/// </summary>
public sealed class FirebaseDocumentPublicationRepository : IDocumentPublicationRepository
{
    private const string ContextCollection = "context-documents";
    private const string KpiCollection = "kpi-documents";
    private const string HeadsCollection = "document-publication-heads";
    private const string SnapshotsCollection = "document-publication-snapshots";

    private readonly FirestoreDb _db;
    private readonly ILogger<FirebaseDocumentPublicationRepository> _logger;

    public FirebaseDocumentPublicationRepository(
        FirestoreDb db,
        ILogger<FirebaseDocumentPublicationRepository> logger,
        string competition)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        Competition = competition.Trim();
    }

    public string Competition { get; }

    public async Task<DocumentPublicationResult> PublishAsync(
        DocumentPublicationDefinition definition,
        DocumentPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Firestore can invoke this callback more than once.  Snapshot every input which has
        // caller-owned collection semantics before the first attempt; retries never observe a
        // changed guard, template, condition list, document list, or document ordering.
        request = FreezeRequest(request);
        DocumentPublicationContract.ValidateRequest(Competition, definition, request);
        var scope = new DocumentPublicationScope(Competition, request.CommunityContext, definition.PublicationSet);
        var ordered = DocumentPublicationContract.ValidateCanonicalOrder(request.Documents);
        var targetId = DocumentPublicationContract.ComputeSnapshotId(ordered);
        var guardedCommit = request.SourcePublicationCommit;

        try
        {
            return await _db.RunTransactionAsync(async transaction =>
            {
                GuardedPublicationState? guarded = null;
                if (guardedCommit is not null)
                {
                    guarded = await ReadAndValidateGuardAsync(transaction, guardedCommit);
                    if (guarded.ExistingReceipt is not null)
                    {
                        var persisted = guarded.ExistingReceipt;
                        var persistedRequest = CreateReceiptRequest(
                            guardedCommit,
                            persisted.Request.SelectedSnapshotId,
                            persisted.Request.PublicationDisposition);
                        if (FirebaseContextSourceCycleRepository.ReceiptSemanticJson(persisted.Request)
                            != FirebaseContextSourceCycleRepository.ReceiptSemanticJson(persistedRequest))
                            throw new InvalidDataException("STATE_CONFLICT");
                        if (persisted.Request.SelectedSnapshotId != targetId)
                            throw new InvalidDataException("Persisted source receipt does not match the requested publication bytes.");
                        var replay = await LoadSnapshotAsync(transaction, scope, definition, targetId)
                            ?? throw new InvalidDataException("Persisted source receipt references a missing publication snapshot.");
                        return new DocumentPublicationResult(ToDocumentDisposition(persisted.Request.PublicationDisposition), replay.Snapshot);
                    }
                }
                var currentSnapshotId = await LoadHeadSnapshotIdAsync(transaction, scope);
                // Read and validate only the head envelope first. A stale caller must fail before
                // corrupt current or target graphs are inspected.
                DocumentPublicationContract.EnsureExpectedHead(
                    scope,
                    request.ExpectedPreviousSnapshotId,
                    currentSnapshotId);

                var current = currentSnapshotId is null
                    ? null
                    : await LoadSnapshotAsync(transaction, scope, definition, currentSnapshotId)
                      ?? throw new InvalidDataException("Publication head references a missing snapshot.");
                var target = string.Equals(targetId, currentSnapshotId, StringComparison.Ordinal)
                    ? current
                    : await LoadSnapshotAsync(transaction, scope, definition, targetId);
                var disposition = DocumentPublicationContract.DecideTransition(
                    scope,
                    request.ExpectedPreviousSnapshotId,
                    currentSnapshotId,
                    targetId,
                    target is not null);

                if (disposition == DocumentPublicationDisposition.Unchanged)
                {
                    // The only missing-receipt recovery is an already-current target under the
                    // exact expected head.  Validate the whole immutable graph and metadata,
                    // then append only the receipt/final reduction; no historical predecessor
                    // or creation-time inference is permitted.
                    if (guarded is not null && current!.Snapshot.MetadataJson != request.MetadataJson)
                        throw new InvalidDataException("STATE_CONFLICT");
                    if (guarded is not null)
                        WriteGuardedReceipt(transaction, guarded, guardedCommit!, targetId, ToSourceDisposition(disposition), createdAt: null);
                    return new DocumentPublicationResult(disposition, current!.Snapshot);
                }

                if (disposition == DocumentPublicationDisposition.Reactivated)
                {
                    transaction.Set(HeadReference(scope), ToHead(scope, targetId));
                    if (guarded is not null)
                        WriteGuardedReceipt(transaction, guarded, guardedCommit!, targetId, ToSourceDisposition(disposition), createdAt: null);
                    return new DocumentPublicationResult(disposition, target!.Snapshot);
                }

                var changed = ordered
                    .Where(payload =>
                    {
                        var existing = current?.Snapshot.Documents.SingleOrDefault(entry => entry.Key == payload.Key);
                        return existing is null
                               || !string.Equals(
                                   existing.ContentSha256,
                                   DocumentPublicationContract.ComputeContentSha256(payload.Content),
                                   StringComparison.Ordinal);
                    })
                    .ToArray();
                var nextVersions = new Dictionary<DocumentPublicationKey, int>();
                foreach (var payload in changed)
                {
                    nextVersions[payload.Key] = await GetNextVersionAsync(transaction, scope, payload.Key);
                }

                // One timestamp is deliberately allocated after every read. Firestore may retry this
                // callback, but each successful attempt gives all newly-created rows one instant.
                var createdAt = Timestamp.GetCurrentTimestamp();

                var entries = ImmutableArray.CreateBuilder<DocumentPublicationEntry>(ordered.Length);
                foreach (var payload in ordered)
                {
                    var existing = current?.Snapshot.Documents.SingleOrDefault(entry => entry.Key == payload.Key);
                    var contentHash = DocumentPublicationContract.ComputeContentSha256(payload.Content);
                    if (existing is not null && string.Equals(existing.ContentSha256, contentHash, StringComparison.Ordinal))
                    {
                        entries.Add(existing);
                        continue;
                    }

                    var version = nextVersions[payload.Key];
                    entries.Add(new DocumentPublicationEntry(payload.Kind, payload.Name, version, contentHash));
                    WritePayload(transaction, scope, payload, version, createdAt);
                }

                var snapshot = new DocumentPublicationSnapshot(
                    scope.Competition,
                    scope.CommunityContext,
                    scope.PublicationSet,
                    targetId,
                    current?.Snapshot.SnapshotId,
                    createdAt.ToDateTimeOffset(),
                    request.MetadataJson,
                    entries);
                transaction.Create(SnapshotReference(scope, targetId), ToFirestoreSnapshot(snapshot));
                transaction.Set(HeadReference(scope), ToHead(scope, targetId));
                if (guarded is not null)
                    WriteGuardedReceipt(transaction, guarded, guardedCommit!, targetId, ToSourceDisposition(disposition), createdAt.ToDateTimeOffset());
                return new DocumentPublicationResult(disposition, snapshot);
            }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Aborted)
        {
            var currentSnapshotId = await LoadHeadSnapshotIdAsync(scope, cancellationToken);
            DocumentPublicationContract.EnsureExpectedHead(
                scope,
                request.ExpectedPreviousSnapshotId,
                currentSnapshotId);

            throw;
        }
    }

    private sealed record GuardedPublicationState(
        BundesligaContextSourceOuterCycle Outer,
        BundesligaContextSourceCycleClaim Source,
        BundesligaContextSourceHealth Health,
        IReadOnlyDictionary<string, BundesligaContextSourceReceipt> PriorReceipts,
        IReadOnlyDictionary<BundesligaContextSource, BundesligaContextSourceCycleClaim> SourceCycles,
        BundesligaContextSourceReceipt? ExistingReceipt,
        BundesligaContextSourceReceipt? PriorCompletedReceipt);

    private static DocumentPublicationRequest FreezeRequest(DocumentPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var documents = DocumentPublicationContract.ValidateCanonicalOrder(request.Documents)
            .Select(document => new DocumentPublicationPayload(document.Kind, document.Name, document.Content, document.Description))
            .ToImmutableArray();
        ContextSourcePublicationCommitRequest? commit = null;
        if (request.SourcePublicationCommit is { } supplied)
        {
            var guard = supplied.Guard;
            var frozenGuard = new ContextSourcePublicationGuard(
                guard.Competition, guard.Scope, guard.CycleId, guard.Source, guard.ConsumerLaneId,
                guard.CommunityContext, guard.PublicationSet, guard.BundleDigest, guard.ObservationDigest,
                guard.WatermarkSequence, guard.WatermarkCycleId);
            var template = supplied.ReceiptTemplate;
            var frozenTemplate = new ContextSourcePublicationReceiptTemplate(
                template.SelectionDisposition, template.SelectedOrigin,
                new BundesligaContextSourceDates(template.SourceDates.RatedAt, template.SourceDates.MembershipCapturedAt,
                    template.SourceDates.MembershipEffectiveAt, template.SourceDates.EnrichmentCapturedAt),
                template.RosterRevision,
                new BundesligaContextSourceCarriedFields(template.CarriedFields.AgeCount, template.CarriedFields.PositionCount,
                    template.CarriedFields.MarketValueCount, template.CarriedFields.OldestFieldEffectiveAt),
                template.ActiveConditions.ToImmutableArray());
            commit = new ContextSourcePublicationCommitRequest(frozenGuard, frozenTemplate);
        }
        return new DocumentPublicationRequest(request.CommunityContext, request.ExpectedPreviousSnapshotId,
            documents, request.MetadataJson, commit);
    }

    private async Task<GuardedPublicationState> ReadAndValidateGuardAsync(
        Transaction transaction,
        ContextSourcePublicationCommitRequest commit)
    {
        var guard = commit.Guard;
        var receiptRef = _db.Collection(FirebaseContextSourceCycleRepository.Receipts)
            .Document(BundesligaContextSourceHashing.ReceiptStorageId(guard.Identity, guard.Source, guard.ConsumerLaneId));
        var existingSnapshot = await transaction.GetSnapshotAsync(receiptRef);
        if (existingSnapshot.Exists)
        {
            var existing = FirebaseContextSourceCycleRepository.ParseReceipt(existingSnapshot, guard.Identity, guard.Source, guard.ConsumerLaneId);
            return new GuardedPublicationState(null!, null!, null!, new Dictionary<string, BundesligaContextSourceReceipt>(), new Dictionary<BundesligaContextSource, BundesligaContextSourceCycleClaim>(), existing, null);
        }

        var cycleRef = _db.Collection(FirebaseContextSourceCycleRepository.Cycles).Document(guard.Identity.StorageId);
        var outer = FirebaseContextSourceCycleRepository.ParseCycle(await transaction.GetSnapshotAsync(cycleRef), guard.Identity);
        if (outer.Status != BundesligaContextSourceCycleStatus.HandoffReady
            || outer.BundleSha256 != guard.BundleDigest
            || !outer.EnabledSources.Contains(guard.Source))
            throw new InvalidDataException("STATE_CONFLICT");
        var sourceCycles = new Dictionary<BundesligaContextSource, BundesligaContextSourceCycleClaim>();
        foreach (var source in outer.EnabledSources)
        {
            var sourceRef = _db.Collection(FirebaseContextSourceCycleRepository.Observations)
                .Document(BundesligaContextSourceHashing.SourceCycleStorageId(guard.Identity, source));
            sourceCycles[source] = FirebaseContextSourceCycleRepository.ParseSource(
                await transaction.GetSnapshotAsync(sourceRef), guard.Identity, source, outer.ExpectedConsumers);
        }
        var sourceCycle = sourceCycles[guard.Source];
        if (sourceCycle.Status != BundesligaContextSourceSourceStatus.Finalized
            || sourceCycle.ObservationDigest != guard.ObservationDigest
            || !sourceCycle.ReceivedConsumers.SequenceEqual(outer.ExpectedConsumers.Take(sourceCycle.ReceivedConsumers.Count), StringComparer.Ordinal)
            || outer.ExpectedConsumers.ElementAtOrDefault(sourceCycle.ReceivedConsumers.Count) != guard.ConsumerLaneId)
            throw new InvalidDataException("STATE_CONFLICT");
        var healthRef = _db.Collection(FirebaseContextSourceCycleRepository.Health)
            .Document(BundesligaContextSourceHashing.HealthStorageId(guard.Competition, guard.Identity.ScopeValue, guard.Source));
        var healthSnapshot = await transaction.GetSnapshotAsync(healthRef);
        if (!healthSnapshot.Exists) throw new InvalidDataException("STATE_CONFLICT");
        var health = FirebaseContextSourceCycleRepository.ParseHealth(healthSnapshot, guard.Identity, guard.Source);
        FirebaseContextSourceCycleRepository.ValidateRosterAcquisitionReasonAgainstRevisionState(
            sourceCycle.Observation!, healthSnapshot, guard.Identity, guard.Source);
        if (health.Watermark != new BundesligaContextSourceWatermark(guard.WatermarkSequence, guard.WatermarkCycleId))
            throw new InvalidDataException("STATE_CONFLICT");
        BundesligaContextSourceReceipt? priorCompletedReceipt = null;
        if (commit.ReceiptTemplate.SelectionDisposition == BundesligaContextSourceSelectionDisposition.MetadataUnchanged)
        {
            priorCompletedReceipt = await ReadAndValidateAuthoritativePriorSelectionAsync(transaction, guard, health);
        }
        var prior = new Dictionary<string, BundesligaContextSourceReceipt>(StringComparer.Ordinal);
        foreach (var lane in sourceCycle.ReceivedConsumers)
        {
            var priorRef = _db.Collection(FirebaseContextSourceCycleRepository.Receipts)
                .Document(BundesligaContextSourceHashing.ReceiptStorageId(guard.Identity, guard.Source, lane));
            var priorSnapshot = await transaction.GetSnapshotAsync(priorRef);
            if (!priorSnapshot.Exists) throw new InvalidDataException("STATE_CONFLICT");
            prior[lane] = FirebaseContextSourceCycleRepository.ParseReceipt(priorSnapshot, guard.Identity, guard.Source, lane);
        }
        return new GuardedPublicationState(outer, sourceCycle, health, prior, sourceCycles, null, priorCompletedReceipt);
    }

    /// <summary>
    /// A metadata-unchanged selection is authoritative only when health's named completed
    /// cycle, immutable observation, lane receipt, all selected fields, and that cycle's
    /// freshness reduction agree.  The current cycle's reference is intentionally not used.
    /// </summary>
    private async Task<BundesligaContextSourceReceipt> ReadAndValidateAuthoritativePriorSelectionAsync(
        Transaction transaction,
        ContextSourcePublicationGuard guard,
        BundesligaContextSourceHealth health)
    {
        var priorCycleId = health.LastCompletedCycleId ?? throw new InvalidDataException("STATE_CONFLICT");
        var identity = BundesligaContextSourceCycleIdentity.FromCycleId(guard.Competition, guard.Scope, priorCycleId);
        if (identity == guard.Identity) throw new InvalidDataException("STATE_CONFLICT");
        var outer = FirebaseContextSourceCycleRepository.ParseCycle(
            await transaction.GetSnapshotAsync(_db.Collection(FirebaseContextSourceCycleRepository.Cycles).Document(identity.StorageId)), identity);
        if (outer.Status != BundesligaContextSourceCycleStatus.Complete || !outer.EnabledSources.Contains(guard.Source))
            throw new InvalidDataException("STATE_CONFLICT");
        var source = FirebaseContextSourceCycleRepository.ParseSource(
            await transaction.GetSnapshotAsync(_db.Collection(FirebaseContextSourceCycleRepository.Observations)
                .Document(BundesligaContextSourceHashing.SourceCycleStorageId(identity, guard.Source))),
            identity, guard.Source, outer.ExpectedConsumers);
        if (source.Status != BundesligaContextSourceSourceStatus.Complete || source.Observation is null)
            throw new InvalidDataException("STATE_CONFLICT");
        var receipt = FirebaseContextSourceCycleRepository.ParseReceipt(
            await transaction.GetSnapshotAsync(_db.Collection(FirebaseContextSourceCycleRepository.Receipts)
                .Document(BundesligaContextSourceHashing.ReceiptStorageId(identity, guard.Source, guard.ConsumerLaneId))),
            identity, guard.Source, guard.ConsumerLaneId);
        var referenceDate = DateOnly.FromDateTime(outer.StalenessReferenceAtUtc.UtcDateTime);
        var selection = health.CommunitySelections.SingleOrDefault(value => value.ConsumerLaneId == guard.ConsumerLaneId);
        if (selection is null) throw new InvalidDataException("STATE_CONFLICT");
        try
        {
            BundesligaContextSourceReceiptContract.ValidateAuthoritativePriorSelection(
                health, selection, receipt, source.Observation, outer, referenceDate);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("STATE_CONFLICT");
        }
        return receipt;
    }

    private void WriteGuardedReceipt(
        Transaction transaction,
        GuardedPublicationState state,
        ContextSourcePublicationCommitRequest commit,
        string snapshotId,
        BundesligaContextSourcePublicationDisposition disposition,
        DateTimeOffset? createdAt)
    {
        var receiptRequest = CreateReceiptRequest(commit, snapshotId, disposition);
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receiptRequest, state.Source.Observation!);
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(receiptRequest, DateOnly.FromDateTime(state.Outer.StalenessReferenceAtUtc.UtcDateTime));
        var recordedAt = createdAt ?? DateTimeOffset.UtcNow;
        recordedAt = new DateTimeOffset(recordedAt.UtcDateTime.Ticks - recordedAt.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var receipt = new BundesligaContextSourceReceipt(receiptRequest, recordedAt);
        receipt.Validate();
        if (state.PriorCompletedReceipt is not null)
            BundesligaContextSourceReceiptContract.ValidateMetadataUnchangedAgainstPriorReceipt(receiptRequest, state.PriorCompletedReceipt);
        var received = state.Source.ReceivedConsumers.Append(commit.Guard.ConsumerLaneId).ToArray();
        var complete = received.Length == state.Outer.ExpectedConsumers.Count;
        var completedSource = state.Source with
        {
            ReceivedConsumers = received,
            Status = complete ? BundesligaContextSourceSourceStatus.Complete : state.Source.Status,
            CompletedAtUtc = complete ? recordedAt : null
        };
        completedSource.Validate(state.Outer.ExpectedConsumers);
        var receiptRef = _db.Collection(FirebaseContextSourceCycleRepository.Receipts).Document(receiptRequest.StorageId);
        transaction.Create(receiptRef, FirebaseContextSourceCycleRepository.ToFirestore(receipt));
        transaction.Set(_db.Collection(FirebaseContextSourceCycleRepository.Observations).Document(BundesligaContextSourceHashing.SourceCycleStorageId(receiptRequest.Identity, receiptRequest.Source)), FirebaseContextSourceCycleRepository.ToFirestore(completedSource));
        if (!complete) return;

        var allReceipts = state.Outer.ExpectedConsumers.Select(lane => lane == receiptRequest.ConsumerLaneId ? receipt : state.PriorReceipts[lane]).ToArray();
        var reduced = BundesligaContextSourceHealthReducer.ReduceCompleted(state.Health, state.Outer, completedSource, allReceipts);
        transaction.Set(_db.Collection(FirebaseContextSourceCycleRepository.Health).Document(reduced.StorageId), FirebaseContextSourceCycleRepository.ToFirestore(reduced));
        if (state.Outer.EnabledSources.All(source => source == receiptRequest.Source || state.SourceCycles[source].Status == BundesligaContextSourceSourceStatus.Complete))
        {
            var completedOuter = state.Outer with { Status = BundesligaContextSourceCycleStatus.Complete, CompletedAtUtc = recordedAt };
            transaction.Set(_db.Collection(FirebaseContextSourceCycleRepository.Cycles).Document(completedOuter.Identity.StorageId), FirebaseContextSourceCycleRepository.ToFirestore(completedOuter));
        }
    }

    private static BundesligaContextSourceReceiptRequest CreateReceiptRequest(
        ContextSourcePublicationCommitRequest commit,
        string snapshotId,
        BundesligaContextSourcePublicationDisposition disposition)
    {
        var guard = commit.Guard;
        var template = commit.ReceiptTemplate;
        return new BundesligaContextSourceReceiptRequest(guard.Identity, guard.Source, guard.ConsumerLaneId,
            guard.CommunityContext, guard.ObservationDigest, guard.BundleDigest, template.SelectionDisposition,
            snapshotId, template.SelectedOrigin, disposition, template.SourceDates, template.RosterRevision,
            template.CarriedFields, template.ActiveConditions);
    }

    private static BundesligaContextSourcePublicationDisposition ToSourceDisposition(DocumentPublicationDisposition value) => value switch
    {
        DocumentPublicationDisposition.Published => BundesligaContextSourcePublicationDisposition.Published,
        DocumentPublicationDisposition.Unchanged => BundesligaContextSourcePublicationDisposition.Unchanged,
        DocumentPublicationDisposition.Reactivated => BundesligaContextSourcePublicationDisposition.Reactivated,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static DocumentPublicationDisposition ToDocumentDisposition(BundesligaContextSourcePublicationDisposition value) => value switch
    {
        BundesligaContextSourcePublicationDisposition.Published => DocumentPublicationDisposition.Published,
        BundesligaContextSourcePublicationDisposition.Unchanged => DocumentPublicationDisposition.Unchanged,
        BundesligaContextSourcePublicationDisposition.Reactivated => DocumentPublicationDisposition.Reactivated,
        _ => throw new InvalidDataException("Source receipt is not a publication disposition.")
    };

    public async Task<LoadedDocumentPublication?> GetLastKnownGoodAsync(
        DocumentPublicationDefinition definition,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        DocumentPublicationContract.ValidateScope(new DocumentPublicationScope(Competition, communityContext, definition.PublicationSet));
        DocumentPublicationContract.ValidateRequest(
            Competition,
            definition,
            new DocumentPublicationRequest(communityContext, null, definition.RequiredDocuments.Select(key => new DocumentPublicationPayload(
                key.Kind,
                key.Name,
                string.Empty,
                key.Kind == DocumentPublicationKind.Kpi ? "validation" : null))));

        var scope = new DocumentPublicationScope(Competition, communityContext, definition.PublicationSet);
        return await _db.RunTransactionAsync(async transaction =>
        {
            var snapshotId = await LoadHeadSnapshotIdAsync(transaction, scope);
            return snapshotId is null
                ? null
                : await LoadSnapshotAsync(transaction, scope, definition, snapshotId)
                  ?? throw new InvalidDataException("Publication head references a missing snapshot.");
        }, cancellationToken: cancellationToken);
    }

    public async Task<LoadedDocumentPublication?> GetSnapshotAsync(
        DocumentPublicationDefinition definition,
        string communityContext,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        var scope = new DocumentPublicationScope(Competition, communityContext, definition.PublicationSet);
        DocumentPublicationContract.ValidateScope(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        return await _db.RunTransactionAsync(
            transaction => LoadSnapshotAsync(transaction, scope, definition, snapshotId),
            cancellationToken: cancellationToken);
    }

    private async Task<string?> LoadHeadSnapshotIdAsync(
        Transaction transaction,
        DocumentPublicationScope scope)
    {
        var reference = HeadReference(scope);
        var headSnapshot = await transaction.GetSnapshotAsync(reference);
        return LoadHeadSnapshotId(scope, reference, headSnapshot);
    }

    private async Task<string?> LoadHeadSnapshotIdAsync(
        DocumentPublicationScope scope,
        CancellationToken cancellationToken)
    {
        var reference = HeadReference(scope);
        var headSnapshot = await reference.GetSnapshotAsync(cancellationToken);
        return LoadHeadSnapshotId(scope, reference, headSnapshot);
    }

    private static string? LoadHeadSnapshotId(
        DocumentPublicationScope scope,
        DocumentReference reference,
        DocumentSnapshot headSnapshot)
    {
        if (!headSnapshot.Exists)
        {
            return null;
        }

        if (headSnapshot.Id != reference.Id || !string.Equals(headSnapshot.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication head identity is corrupt.");
        }

        var head = headSnapshot.ConvertTo<FirestoreDocumentPublicationHead>();
        if (head.Competition != scope.Competition
            || head.CommunityContext != scope.CommunityContext
            || head.PublicationSet != scope.PublicationSet)
        {
            throw new InvalidDataException("Publication head scope is corrupt.");
        }

        return head.SnapshotId;
    }

    private async Task<LoadedDocumentPublication?> LoadSnapshotAsync(
        Transaction transaction,
        DocumentPublicationScope scope,
        DocumentPublicationDefinition definition,
        string snapshotId)
    {
        var reference = SnapshotReference(scope, snapshotId);
        var metadata = await transaction.GetSnapshotAsync(reference);
        if (!metadata.Exists)
        {
            return null;
        }

        if (metadata.Id != reference.Id || !string.Equals(metadata.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication snapshot metadata identity is corrupt.");
        }

        var stored = metadata.ConvertTo<FirestoreDocumentPublicationSnapshot>();
        if (!string.Equals(stored.SnapshotId, snapshotId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication snapshot metadata does not match its requested snapshot ID.");
        }

        var snapshot = FromFirestoreSnapshot(stored);
        var documents = ImmutableArray.CreateBuilder<PublishedDocument>(snapshot.Documents.Length);
        foreach (var entry in snapshot.Documents)
        {
            var payload = await ReadPayloadAsync(transaction, scope, entry);
            if (payload is null)
            {
                throw new InvalidDataException("Publication snapshot references a missing payload.");
            }

            documents.Add(payload);
        }

        DocumentPublicationContract.ValidateLoaded(scope.Competition, scope.CommunityContext, definition, snapshot, documents);
        return new LoadedDocumentPublication(snapshot, documents);
    }

    private async Task<PublishedDocument?> ReadPayloadAsync(Transaction transaction, DocumentPublicationScope scope, DocumentPublicationEntry entry)
    {
        var reference = PayloadReference(scope, entry.Key, entry.Version);
        var snapshot = await transaction.GetSnapshotAsync(reference);
        if (!snapshot.Exists)
        {
            return null;
        }

        if (snapshot.Id != reference.Id || !string.Equals(snapshot.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication payload identity is corrupt.");
        }

        if (entry.Kind == DocumentPublicationKind.Context)
        {
            var value = snapshot.ConvertTo<FirestoreContextDocument>();
            return new PublishedDocument(value.Competition, value.CommunityContext, value.PublicationSet,
                entry.Kind, value.DocumentName, value.Version, value.Content, null, value.CreatedAt.ToDateTimeOffset());
        }

        var kpi = snapshot.ConvertTo<FirestoreKpiDocument>();
        return new PublishedDocument(kpi.Competition, kpi.CommunityContext, kpi.PublicationSet,
            entry.Kind, kpi.DocumentName, kpi.Version, kpi.Content, kpi.Description, kpi.CreatedAt.ToDateTimeOffset());
    }

    private async Task<int> GetNextVersionAsync(Transaction transaction, DocumentPublicationScope scope, DocumentPublicationKey key)
    {
        var query = _db.Collection(CollectionFor(key.Kind))
            .WhereEqualTo("competition", scope.Competition)
            .WhereEqualTo("communityContext", scope.CommunityContext)
            .WhereEqualTo("documentName", key.Name)
            .OrderByDescending("version")
            .Limit(1);
        var snapshot = await transaction.GetSnapshotAsync(query);
        if (snapshot.Count == 0)
        {
            return 0;
        }

        return snapshot.Documents[0].GetValue<int>("version") + 1;
    }

    private void WritePayload(
        Transaction transaction,
        DocumentPublicationScope scope,
        DocumentPublicationPayload payload,
        int version,
        Timestamp createdAt)
    {
        var reference = PayloadReference(scope, payload.Key, version);
        if (payload.Kind == DocumentPublicationKind.Context)
        {
            transaction.Create(reference, new FirestoreContextDocument
            {
                Id = reference.Id,
                Competition = scope.Competition,
                CommunityContext = scope.CommunityContext,
                PublicationSet = scope.PublicationSet,
                DocumentName = payload.Name,
                Content = payload.Content,
                Version = version,
                CreatedAt = createdAt
            });
            return;
        }

        transaction.Create(reference, new FirestoreKpiDocument
        {
            Id = reference.Id,
            Competition = scope.Competition,
            CommunityContext = scope.CommunityContext,
            PublicationSet = scope.PublicationSet,
            DocumentName = payload.Name,
            Content = payload.Content,
            Description = payload.Description!,
            Version = version,
            CreatedAt = createdAt
        });
    }

    private DocumentReference HeadReference(DocumentPublicationScope scope) =>
        _db.Collection(HeadsCollection).Document(DocumentPublicationContract.ComputeHeadId(scope));

    private DocumentReference SnapshotReference(DocumentPublicationScope scope, string snapshotId) =>
        _db.Collection(SnapshotsCollection).Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, snapshotId));

    private DocumentReference PayloadReference(DocumentPublicationScope scope, DocumentPublicationKey key, int version) =>
        _db.Collection(CollectionFor(key.Kind)).Document(
            BuildPublicationPayloadId(scope, key.Name, version));

    internal static string BuildPublicationPayloadId(DocumentPublicationScope scope, string documentName, int version) =>
        $"{DocumentPublicationContract.ComputeHeadId(scope)}_{documentName}_{version}";

    private static string CollectionFor(DocumentPublicationKind kind) =>
        kind == DocumentPublicationKind.Context ? ContextCollection : KpiCollection;

    private static FirestoreDocumentPublicationHead ToHead(DocumentPublicationScope scope, string snapshotId) => new()
    {
        Competition = scope.Competition,
        CommunityContext = scope.CommunityContext,
        PublicationSet = scope.PublicationSet,
        SnapshotId = snapshotId
    };

    private static FirestoreDocumentPublicationSnapshot ToFirestoreSnapshot(DocumentPublicationSnapshot snapshot) => new()
    {
        Competition = snapshot.Competition,
        CommunityContext = snapshot.CommunityContext,
        PublicationSet = snapshot.PublicationSet,
        SnapshotId = snapshot.SnapshotId,
        PreviousSnapshotId = snapshot.PreviousSnapshotId,
        CreatedAt = Timestamp.FromDateTime(snapshot.CreatedAt.UtcDateTime),
        MetadataJson = snapshot.MetadataJson,
        Documents = snapshot.Documents.Select(entry => new FirestoreDocumentPublicationEntry
        {
            Kind = entry.Kind.ToString(), Name = entry.Name, Version = entry.Version, ContentSha256 = entry.ContentSha256
        }).ToList()
    };

    private static DocumentPublicationSnapshot FromFirestoreSnapshot(FirestoreDocumentPublicationSnapshot snapshot) => new(
        snapshot.Competition, snapshot.CommunityContext, snapshot.PublicationSet, snapshot.SnapshotId,
        snapshot.PreviousSnapshotId, snapshot.CreatedAt.ToDateTimeOffset(), snapshot.MetadataJson,
        snapshot.Documents.Select(entry => new DocumentPublicationEntry(
            Enum.Parse<DocumentPublicationKind>(entry.Kind, ignoreCase: false), entry.Name, entry.Version, entry.ContentSha256)));
}
