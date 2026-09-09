using System.Collections.ObjectModel;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed record ContextSourceCycleRequest(
    BundesligaContextSourceOuterCycle Cycle,
    IReadOnlyList<BundesligaContextSource> EnabledSources,
    bool DryRun,
    string? CurrentLaneId = null);

public sealed class ContextSourceCyclePreparation : IAsyncDisposable
{
    private static readonly AsyncLocal<ContextSourceCyclePreparation?> Ambient = new();
    private readonly bool _cleanup;
    private readonly Func<BundesligaContextSourceReceipt, CancellationToken, Task>? _reconcilePersistedReceipt;
    public ContextSourceCyclePreparation(ContextSourceBundleFiles files, bool cleanup)
        : this(files, cleanup, files.Bundle.ProducerLaneId, null, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>()) { }

    public ContextSourceCyclePreparation(
        ContextSourceBundleFiles files,
        bool cleanup,
        string currentLaneId,
        BundesligaContextSourceOuterCycle? persistedCycle,
        IReadOnlyDictionary<BundesligaContextSource, BundesligaContextSourceReceipt> persistedReceipts)
        : this(files, cleanup, currentLaneId, persistedCycle, persistedReceipts, null) { }

    public ContextSourceCyclePreparation(
        ContextSourceBundleFiles files,
        bool cleanup,
        string currentLaneId,
        BundesligaContextSourceOuterCycle? persistedCycle,
        IReadOnlyDictionary<BundesligaContextSource, BundesligaContextSourceReceipt> persistedReceipts,
        Func<BundesligaContextSourceReceipt, CancellationToken, Task>? reconcilePersistedReceipt)
    {
        Files = files ?? throw new ArgumentNullException(nameof(files));
        Files.Validate();
        if (string.IsNullOrWhiteSpace(currentLaneId) || !files.Bundle.ExpectedConsumers.Contains(currentLaneId, StringComparer.Ordinal))
            throw new InvalidDataException("Prepared context-source lane is not canonical.");
        CurrentLaneId = currentLaneId;
        PersistedCycle = persistedCycle;
        if (persistedCycle is not null)
        {
            ContextSourceBundleHandoff.ValidateBundleCycle(persistedCycle, files.Bundle);
            if (persistedCycle.Status is not (BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete))
                throw new InvalidDataException("Prepared persisted cycle is not consumable.");
        }

        var receipts = persistedReceipts?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? throw new ArgumentNullException(nameof(persistedReceipts));
        foreach (var (source, receipt) in receipts)
        {
            receipt.Validate();
            var observation = files.Bundle.Observations.SingleOrDefault(value => value.Source == source)
                ?? throw new InvalidDataException("Persisted receipt source is not in the prepared bundle.");
            if (receipt.Request.Identity != files.Bundle.Cycle
                || receipt.Request.ConsumerLaneId != currentLaneId
                || receipt.Request.Source != source
                || receipt.Request.BundleDigest != files.Digest
                || receipt.Request.ObservationDigest != observation.ObservationDigest)
                throw new InvalidDataException("Persisted receipt does not match the prepared lane and bundle.");
            BundesligaContextSourceReceiptContract.ValidateAgainstObservation(receipt.Request, observation);
        }
        if (persistedCycle?.Status == BundesligaContextSourceCycleStatus.Complete
            && !receipts.Keys.ToHashSet().SetEquals(files.Bundle.Observations.Select(value => value.Source)))
            throw new InvalidDataException("A complete prepared cycle requires every exact-lane receipt.");
        PersistedReceipts = new ReadOnlyDictionary<BundesligaContextSource, BundesligaContextSourceReceipt>(receipts);
        _reconcilePersistedReceipt = reconcilePersistedReceipt;
        _cleanup = cleanup;
    }
    public static ContextSourceCyclePreparation? Current => Ambient.Value;
    public ContextSourceBundleFiles Files { get; }
    public string CurrentLaneId { get; }
    public BundesligaContextSourceOuterCycle? PersistedCycle { get; }
    public IReadOnlyDictionary<BundesligaContextSource, BundesligaContextSourceReceipt> PersistedReceipts { get; }
    public bool TryGetPersistedReceipt(BundesligaContextSource source, out BundesligaContextSourceReceipt? receipt)
    {
        if (PersistedReceipts.TryGetValue(source, out var value))
        {
            receipt = value;
            return true;
        }
        receipt = null;
        return false;
    }
    public Task ReconcilePersistedReceiptAsync(BundesligaContextSourceReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!PersistedReceipts.TryGetValue(receipt.Request.Source, out var persisted) || persisted != receipt)
            throw new InvalidDataException("Receipt is not the prepared exact-lane persisted receipt.");
        return _reconcilePersistedReceipt?.Invoke(receipt, cancellationToken) ?? Task.CompletedTask;
    }
    public IDisposable Activate()
    {
        if (Ambient.Value is not null) throw new InvalidOperationException("A context-source cycle is already active.");
        Ambient.Value = this;
        return new AmbientActivation(this);
    }
    public ValueTask DisposeAsync() { if (_cleanup) ContextSourceBundleHandoff.CleanupDevelopment(Files.Bundle.Cycle); return ValueTask.CompletedTask; }

    private sealed class AmbientActivation(ContextSourceCyclePreparation preparation) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            if (ReferenceEquals(Ambient.Value, preparation)) Ambient.Value = null;
            _disposed = true;
        }
    }
}

public sealed class ContextSourceCycleCoordinator
{
    private readonly IBundesligaContextSourceCycleRepository _repository;
    private readonly IReadOnlyDictionary<BundesligaContextSource, IBundesligaContextSourceObservationProvider> _providers;
    private readonly IBundesligaContextSourceIssueProjector? _issueProjector;
    private readonly TimeProvider _timeProvider;

    public ContextSourceCycleCoordinator(
        IBundesligaContextSourceCycleRepository repository,
        IEnumerable<IBundesligaContextSourceObservationProvider> providers,
        IBundesligaContextSourceIssueProjector? issueProjector = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _providers = providers?.GroupBy(x => x.Source).ToDictionary(x => x.Key, x => x.Single()) ?? throw new ArgumentNullException(nameof(providers));
        _issueProjector = issueProjector;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static Task<ContextSourceCyclePreparation?> ExecuteIfEnabledAsync(
        bool clubEloEnabled, bool rostersEnabled, Func<ContextSourceCycleCoordinator> resolveCoordinator,
        Func<ContextSourceCycleCoordinator, IReadOnlyList<BundesligaContextSource>, Task<ContextSourceCyclePreparation>> execute)
    {
        var enabled = new List<BundesligaContextSource>(); if (clubEloEnabled) enabled.Add(BundesligaContextSource.ClubElo); if (rostersEnabled) enabled.Add(BundesligaContextSource.Rosters);
        if (enabled.Count == 0) return Task.FromResult<ContextSourceCyclePreparation?>(null);
        return ExecuteAsync(resolveCoordinator(), enabled, execute);
        static async Task<ContextSourceCyclePreparation?> ExecuteAsync(ContextSourceCycleCoordinator coordinator, IReadOnlyList<BundesligaContextSource> enabled, Func<ContextSourceCycleCoordinator, IReadOnlyList<BundesligaContextSource>, Task<ContextSourceCyclePreparation>> execute) => await execute(coordinator, enabled);
    }

    public async Task<ContextSourceCyclePreparation> PrepareAsync(ContextSourceCycleRequest request, IContextSourceBundleArtifactStore? artifactStore = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); request.Cycle.Validate();
        if (request.EnabledSources.Count == 0 || !request.EnabledSources.SequenceEqual(request.Cycle.EnabledSources)) throw new InvalidDataException("Enabled source list does not match the cycle.");
        if (request.Cycle.Identity.Scope == BundesligaContextSourceScope.ProductionLive && string.IsNullOrWhiteSpace(request.CurrentLaneId)) throw new InvalidDataException("Production source-cycle calls require an explicit current lane.");
        var currentLane = request.CurrentLaneId ?? request.Cycle.ProducerLaneId;
        if (!request.Cycle.ExpectedConsumers.Contains(currentLane, StringComparer.Ordinal)) throw new InvalidDataException("Current lane is not in the cycle consumer list.");
        foreach (var source in request.EnabledSources) if (!_providers.ContainsKey(source)) throw new InvalidOperationException($"No observation provider is registered for '{BundesligaContextSourceContract.SourceValue(source)}'.");
        if (request.DryRun) return new ContextSourceCyclePreparation(await ObserveInMemoryAsync(request.Cycle.Identity, request.EnabledSources, request.Cycle, cancellationToken), cleanup: false, currentLane, null, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>());

        BundesligaContextSourceOuterCycle cycle;
        if (currentLane == request.Cycle.ProducerLaneId)
        {
            var start = await _repository.CreateOrResumeCycleWithResultAsync(request.Cycle, cancellationToken);
            start.Validate();
            cycle = start.Cycle;
            if (start.SupersededSources.Count > 0)
                await ReconcileCycleIssueProjectionsAsync(cycle, start.SupersededSources, cancellationToken);
        }
        else
        {
            cycle = await _repository.GetCycleAsync(request.Cycle.Identity, cancellationToken)
                ?? throw new InvalidDataException("HANDOFF_ARTIFACT_MISSING");
        }
        await RejectLateCycleAsync(cycle, cancellationToken);
        ContextSourceBundleHandoff.ValidateBundleCycle(request.Cycle, cycle);
        if (cycle.Status == BundesligaContextSourceCycleStatus.Aborted)
        {
            await ReconcileCycleIssueProjectionsAsync(cycle, cancellationToken);
            throw new InvalidDataException(BundesligaContextSourceContract.ErrorValue(cycle.AbortCode!.Value));
        }
        if (cycle.Status is BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete)
        {
            try
            {
                var loaded = cycle.Identity.Scope == BundesligaContextSourceScope.Development
                    ? ContextSourceBundleHandoff.LoadDevelopment(cycle, cycle.BundleSha256!)
                    : await ContextSourceBundleHandoff.LoadProductionAsync(cycle, artifactStore ?? throw new InvalidOperationException("Production handoff store is required."), cancellationToken);
                return await CreatePreparationAsync(loaded, cycle.Identity.Scope == BundesligaContextSourceScope.Development, currentLane, cycle, cancellationToken);
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_MISSING")
            {
                if (cycle.Status == BundesligaContextSourceCycleStatus.HandoffReady) await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactMissing, cancellationToken); throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
            {
                if (cycle.Status == BundesligaContextSourceCycleStatus.HandoffReady) await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken); throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "LOCAL_HANDOFF_MISSING")
            {
                if (cycle.Status == BundesligaContextSourceCycleStatus.HandoffReady) await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing, cancellationToken); throw;
            }
        }
        if (cycle.Status == BundesligaContextSourceCycleStatus.BundleVerified
            && cycle.Identity.Scope == BundesligaContextSourceScope.Development)
        {
            ContextSourceBundleFiles loaded;
            try
            {
                loaded = ContextSourceBundleHandoff.LoadDevelopment(cycle, cycle.BundleSha256!);
            }
            catch (InvalidDataException exception) when (exception.Message == "LOCAL_HANDOFF_MISSING")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing, cancellationToken);
                throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken);
                throw;
            }

            var ready = await _repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, cycle.BundleSha256, null, cancellationToken);
            return await CreatePreparationAsync(loaded, cleanup: true, currentLane, ready, cancellationToken);
        }
        if (cycle.Status == BundesligaContextSourceCycleStatus.UploadReserved && cycle.Identity.Scope == BundesligaContextSourceScope.ProductionLive)
        {
            try
            {
                var loaded = await ContextSourceBundleHandoff.LoadProductionAsync(cycle, artifactStore ?? throw new InvalidOperationException("Production handoff store is required."), cancellationToken);
                var ready = await _repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady, cycle.BundleSha256, cycle.ArtifactName, cancellationToken);
                return await CreatePreparationAsync(loaded, cleanup: false, currentLane, ready, cancellationToken);
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_MISSING")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.FinalizedPayloadUnavailable, cancellationToken); throw new InvalidDataException("FINALIZED_PAYLOAD_UNAVAILABLE", exception);
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_UPLOAD_FAILED")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffUploadFailed, cancellationToken); throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken); throw;
            }
        }
        if (currentLane != cycle.ProducerLaneId)
            throw new InvalidDataException("HANDOFF_ARTIFACT_MISSING");
        if (cycle.Status != BundesligaContextSourceCycleStatus.Claiming)
        {
            var error = cycle.Identity.Scope == BundesligaContextSourceScope.Development ? BundesligaContextSourceError.LocalHandoffMissing : BundesligaContextSourceError.FinalizedPayloadUnavailable;
            await AbortCycleAndReconcileIssuesAsync(cycle.Identity, error, cancellationToken); throw new InvalidDataException(BundesligaContextSourceContract.ErrorValue(error));
        }

        var observations = new List<BundesligaContextSourceObservation>(); var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var source in request.EnabledSources)
        {
            var token = BundesligaContextSourceContract.NewClaimToken(); var now = TruncateUtc(DateTimeOffset.UtcNow);
            var claim = await _repository.ClaimSourceAsync(cycle.Identity, source, token, now, cancellationToken);
            if (claim.Disposition == BundesligaContextSourceClaimDisposition.Busy) throw new InvalidDataException("STATE_CONFLICT");
            if (claim.Disposition == BundesligaContextSourceClaimDisposition.ExistingAborted)
            {
                await ReconcileCycleIssueProjectionsAsync(await _repository.GetCycleAsync(cycle.Identity, cancellationToken) ?? cycle, cancellationToken);
                throw new InvalidDataException(BundesligaContextSourceContract.ErrorValue(claim.Claim.AbortCode ?? BundesligaContextSourceError.StateConflict));
            }
            if (claim.Disposition == BundesligaContextSourceClaimDisposition.ExistingFinalized)
            {
                var error = cycle.Identity.Scope == BundesligaContextSourceScope.Development
                    ? BundesligaContextSourceError.LocalHandoffMissing
                    : BundesligaContextSourceError.FinalizedPayloadUnavailable;
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, error, cancellationToken);
                throw new InvalidDataException(BundesligaContextSourceContract.ErrorValue(error));
            }
            var result = await _providers[source].ObserveAsync(cycle.Identity, cancellationToken); result.Validate();
            if (result.Observation.Source != source) throw new InvalidDataException("Provider returned the wrong source.");
            var finalized = await _repository.FinalizeSourceAsync(cycle.Identity, source, token, result.Observation, TruncateUtc(DateTimeOffset.UtcNow), cancellationToken);
            observations.Add(finalized.Observation!); if (result.PayloadBytes is not null) payloads.Add(result.Observation.Payload!.Path, result.PayloadBytes);
        }
        ContextSourceBundleFiles files;
        try
        {
            files = new ContextSourceBundleFiles(new BundesligaContextSourceBundle(cycle.Identity, cycle.StartedAtUtc, cycle.StalenessReferenceAtUtc, cycle.ProducerLaneId, cycle.ExpectedConsumers, observations), payloads); files.Validate();
            await _repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, files.Digest, null, cancellationToken);
        }
        catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
        {
            await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken); throw;
        }
        if (cycle.Identity.Scope == BundesligaContextSourceScope.Development)
        {
            try
            {
                await ContextSourceBundleHandoff.WriteDevelopmentAsync(files, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken);
                throw;
            }
            catch (InvalidDataException exception) when (exception.Message == "LOCAL_HANDOFF_MISSING")
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing, cancellationToken);
                throw;
            }
            catch (IOException exception)
            {
                await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.LocalHandoffMissing, cancellationToken);
                throw new InvalidDataException("LOCAL_HANDOFF_MISSING", exception);
            }

            var ready = await _repository.TransitionCycleAsync(cycle.Identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.HandoffReady, files.Digest, null, cancellationToken);
            return await CreatePreparationAsync(files, cleanup: true, currentLane, ready, cancellationToken);
        }
        try
        {
            var ready = await ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(_repository, cycle, files, artifactStore ?? throw new InvalidOperationException("Production handoff store is required."), cancellationToken);
            return await CreatePreparationAsync(files, cleanup: false, currentLane, ready, cancellationToken);
        }
        catch (InvalidDataException exception) when (exception.Message == "HANDOFF_UPLOAD_FAILED")
        {
            await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffUploadFailed, cancellationToken);
            throw;
        }
        catch (InvalidDataException exception) when (exception.Message == "HANDOFF_ARTIFACT_CONFLICT")
        {
            await AbortCycleAndReconcileIssuesAsync(cycle.Identity, BundesligaContextSourceError.HandoffArtifactConflict, cancellationToken);
            throw;
        }
    }

    public async Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var receipt = await _repository.RecordReceiptAsync(request, cancellationToken);
        if (request.Identity.Scope == BundesligaContextSourceScope.Development) return receipt;

        var health = await _repository.GetHealthAsync(request.Identity.Competition, request.Identity.Scope, request.Source, cancellationToken);
        // The repository fails closed before creating a receipt when health is absent. If it
        // returned a receipt and health has since disappeared, this call was an exact semantic
        // replay and must remain independent of mutable issue-projection state.
        if (health is null) return receipt;
        if (health.LastCompletedCycleId != request.Identity.CycleId) return receipt;
        if (_issueProjector is null) throw new InvalidOperationException("Production context-source receipts require an issue projector.");
        await ReconcileIssueProjectionAsync(health, cancellationToken);
        return receipt;
    }

    /// <summary>
    /// Completes the receipt that belongs to a prepared lane.  Preparation is the authority
    /// boundary: a caller cannot use this operation to attach a selection to another cycle,
    /// source, bundle, observation, or consumer lane.  The repository retains its exact
    /// semantic-replay transaction, so a guarded Published/Unchanged/Reactivated receipt is
    /// returned unchanged on replay.
    /// </summary>
    public async Task<BundesligaContextSourceReceipt> CompletePreparedReceiptAsync(
        ContextSourceCyclePreparation preparation,
        BundesligaContextSourceReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        preparation.Files.Validate();
        // A dry run produces an in-memory bundle only; it must never manufacture a receipt.
        if (preparation.PersistedCycle is null) throw new InvalidOperationException("Dry-run preparation cannot complete a receipt.");
        var observation = preparation.Files.Bundle.Observations.SingleOrDefault(value => value.Source == request.Source)
            ?? throw new InvalidDataException("Receipt source is not in the prepared bundle.");
        if (request.Identity != preparation.Files.Bundle.Cycle
            || request.ConsumerLaneId != preparation.CurrentLaneId
            || request.BundleDigest != preparation.Files.Digest
            || request.ObservationDigest != observation.ObservationDigest)
            throw new InvalidDataException("Receipt does not match the prepared cycle identity.");
        ContextSourceBundleHandoff.ValidateBundleCycle(preparation.PersistedCycle, preparation.Files.Bundle);
        if (preparation.PersistedCycle.BundleSha256 != preparation.Files.Digest)
            throw new InvalidDataException("Prepared persisted cycle does not bind the exact bundle.");
        var persisted = await _repository.GetCycleAsync(request.Identity, cancellationToken)
            ?? throw new InvalidDataException("Prepared persisted cycle is missing.");
        ContextSourceBundleHandoff.ValidateBundleCycle(persisted, preparation.Files.Bundle);
        if (persisted.Status is not (BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete)
            || persisted.BundleSha256 != preparation.Files.Digest
            || !persisted.EnabledSources.Contains(request.Source))
            throw new InvalidDataException("Prepared persisted cycle is no longer receipt-completable.");
        var sourceCycle = await _repository.GetSourceCycleAsync(request.Identity, request.Source, cancellationToken)
            ?? throw new InvalidDataException("Prepared source observation is missing.");
        sourceCycle.Validate(persisted.ExpectedConsumers);
        if (sourceCycle.Status is not (BundesligaContextSourceSourceStatus.Finalized or BundesligaContextSourceSourceStatus.Complete)
            || sourceCycle.Identity != request.Identity
            || sourceCycle.Source != request.Source
            || sourceCycle.AttemptId != BundesligaContextSourceHashing.AttemptId(request.Identity, request.Source)
            || sourceCycle.ObservationDigest != observation.ObservationDigest
            || sourceCycle.Observation is null
            || !sourceCycle.Observation.CreateCanonicalUtf8().AsSpan().SequenceEqual(observation.CreateCanonicalUtf8()))
            throw new InvalidDataException("Prepared source observation does not match the immutable bundle.");
        BundesligaContextSourceReceiptContract.ValidateAgainstObservation(request, observation);
        BundesligaContextSourceReceiptContract.ValidateFreshnessConditions(
            request, DateOnly.FromDateTime(persisted.StalenessReferenceAtUtc.UtcDateTime));
        if (request.PublicationDisposition is BundesligaContextSourcePublicationDisposition.Published
            or BundesligaContextSourcePublicationDisposition.Unchanged
            or BundesligaContextSourcePublicationDisposition.Reactivated)
        {
            var priorReceipt = await _repository.GetReceiptAsync(request.Identity, request.Source, request.ConsumerLaneId, cancellationToken);
            if (priorReceipt is null || !ReceiptRequestsAreSemanticallyEqual(priorReceipt.Request, request))
                throw new InvalidDataException("STATE_CONFLICT");
        }
        return await RecordReceiptAsync(request, cancellationToken);
    }

    private static bool ReceiptRequestsAreSemanticallyEqual(
        BundesligaContextSourceReceiptRequest left,
        BundesligaContextSourceReceiptRequest right)
        => left.Identity.Competition == right.Identity.Competition
           && left.Identity.Scope == right.Identity.Scope
           && left.Identity.CycleId == right.Identity.CycleId
           && left.Identity.Sequence == right.Identity.Sequence
           && left.Source == right.Source
           && left.ConsumerLaneId == right.ConsumerLaneId
           && left.CommunityContext == right.CommunityContext
           && left.ObservationDigest == right.ObservationDigest
           && left.BundleDigest == right.BundleDigest
           && left.SelectionDisposition == right.SelectionDisposition
           && left.SelectedSnapshotId == right.SelectedSnapshotId
           && left.SelectedOrigin == right.SelectedOrigin
           && left.PublicationDisposition == right.PublicationDisposition
           && left.SourceDates.RatedAt == right.SourceDates.RatedAt
           && left.SourceDates.MembershipCapturedAt == right.SourceDates.MembershipCapturedAt
           && left.SourceDates.MembershipEffectiveAt == right.SourceDates.MembershipEffectiveAt
           && left.SourceDates.EnrichmentCapturedAt == right.SourceDates.EnrichmentCapturedAt
           && left.RosterRevision == right.RosterRevision
           && left.CarriedFields.AgeCount == right.CarriedFields.AgeCount
           && left.CarriedFields.PositionCount == right.CarriedFields.PositionCount
           && left.CarriedFields.MarketValueCount == right.CarriedFields.MarketValueCount
           && left.CarriedFields.OldestFieldEffectiveAt == right.CarriedFields.OldestFieldEffectiveAt
           && left.ActiveConditions.SequenceEqual(right.ActiveConditions);

    private async Task AbortCycleAndReconcileIssuesAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken)
    {
        var aborted = await _repository.AbortCycleAsync(identity, error, cancellationToken);
        await ReconcileCycleIssueProjectionsAsync(aborted, cancellationToken);
    }

    private Task ReconcileCycleIssueProjectionsAsync(BundesligaContextSourceOuterCycle cycle, CancellationToken cancellationToken)
        => ReconcileCycleIssueProjectionsAsync(cycle, cycle.EnabledSources, cancellationToken);

    private async Task ReconcileCycleIssueProjectionsAsync(BundesligaContextSourceOuterCycle cycle, IReadOnlyList<BundesligaContextSource> sources, CancellationToken cancellationToken)
    {
        if (cycle.Identity.Scope != BundesligaContextSourceScope.ProductionLive || _issueProjector is null) return;
        foreach (var source in sources)
        {
            var health = await _repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, source, cancellationToken);
            if (health is not null) await ReconcileIssueProjectionAsync(health, cancellationToken);
        }
    }

    private async Task ReconcileIssueProjectionAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken)
    {
        if (health.Scope != BundesligaContextSourceScope.ProductionLive || _issueProjector is null) return;
        var desired = health.DesiredIssueProjection ?? throw new InvalidDataException("Production health has no issue projection.");
        if (desired.SynchronizationStatus == BundesligaContextSourceIssueSynchronization.Synchronized) return;

        BundesligaContextSourceIssueProjection projection;
        var attemptedAtUtc = TruncateUtc(_timeProvider.GetUtcNow());
        try
        {
            var attempt = await _issueProjector.ProjectAsync(health, cancellationToken);
            if (attempt.ErrorCode is { } error && !Enum.IsDefined(error)) throw new InvalidDataException("Issue projection returned an undefined error code.");
            BundesligaContextSourceHashing.ValidateOptionalSha(attempt.AppliedBodySha256);
            if (attempt.ErrorCode is null && attempt.AppliedBodySha256 != desired.BodySha256) throw new InvalidDataException("Successful issue projection did not apply the desired body.");
            projection = desired with
            {
                AppliedBodySha256 = attempt.AppliedBodySha256 ?? desired.AppliedBodySha256,
                SynchronizationStatus = attempt.ErrorCode is null ? BundesligaContextSourceIssueSynchronization.Synchronized : BundesligaContextSourceIssueSynchronization.Pending,
                LastAttemptedAtUtc = attemptedAtUtc,
                LastErrorCode = attempt.ErrorCode
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            projection = desired with
            {
                SynchronizationStatus = BundesligaContextSourceIssueSynchronization.Pending,
                LastAttemptedAtUtc = attemptedAtUtc,
                LastErrorCode = BundesligaContextSourceIssueError.GithubIssueListFailed
            };
        }

        try
        {
            await _repository.UpdateIssueProjectionAsync(health, projection, cancellationToken);
        }
        catch (InvalidDataException exception) when (exception.Message == "STATE_CONFLICT")
        {
            // A competing receipt/abort may already have advanced or synchronized this projection.
        }
    }

    private async Task<ContextSourceBundleFiles> ObserveInMemoryAsync(BundesligaContextSourceCycleIdentity identity, IReadOnlyList<BundesligaContextSource> sources, BundesligaContextSourceOuterCycle cycle, CancellationToken cancellationToken)
    {
        var observations = new List<BundesligaContextSourceObservation>(); var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var result = await _providers[source].ObserveAsync(identity, cancellationToken);
            result.Validate();
            if (result.Observation.Source != source) throw new InvalidDataException("Provider returned the wrong source.");
            observations.Add(result.Observation);
            if (result.PayloadBytes is not null) payloads.Add(result.Observation.Payload!.Path, result.PayloadBytes);
        }
        var files = new ContextSourceBundleFiles(new BundesligaContextSourceBundle(identity, cycle.StartedAtUtc, cycle.StalenessReferenceAtUtc, cycle.ProducerLaneId, cycle.ExpectedConsumers, observations), payloads); files.Validate(); return files;
    }

    private async Task RejectLateCycleAsync(BundesligaContextSourceOuterCycle cycle, CancellationToken cancellationToken)
    {
        var requested = new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId);
        foreach (var source in cycle.EnabledSources)
        {
            var health = await _repository.GetHealthAsync(cycle.Identity.Competition, cycle.Identity.Scope, source, cancellationToken);
            if (health is not null && requested.CompareTo(health.Watermark) < 0) throw new InvalidDataException("LATE_CYCLE");
        }
    }

    private async Task<ContextSourceCyclePreparation> CreatePreparationAsync(
        ContextSourceBundleFiles files,
        bool cleanup,
        string currentLane,
        BundesligaContextSourceOuterCycle persistedCycle,
        CancellationToken cancellationToken)
    {
        var receipts = new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>();
        foreach (var source in persistedCycle.EnabledSources)
        {
            var receipt = await _repository.GetReceiptAsync(persistedCycle.Identity, source, currentLane, cancellationToken);
            if (receipt is not null) receipts.Add(source, receipt);
        }
        return new ContextSourceCyclePreparation(
            files,
            cleanup,
            currentLane,
            persistedCycle,
            receipts,
            (receipt, token) => RecordReceiptAsync(receipt.Request, token));
    }

    private static DateTimeOffset TruncateUtc(DateTimeOffset value) => new(value.UtcDateTime.Ticks - value.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
}
