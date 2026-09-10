using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

[NotInParallel("context-source-cycle-coordinator")]
public class ContextSourceCycleCoordinatorTests
{
    [Test]
    public async Task Dry_run_preparation_does_not_create_a_persisted_receipt_boundary()
    {
        var cycle = Cycle(); var repository = new MemoryRepository(cycle); var coordinator = new ContextSourceCycleCoordinator(repository, [new Provider(cycle.Identity)]);
        await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, true));
        await Assert.That(preparation.PersistedCycle).IsNull();
        await Assert.That(preparation.PersistedReceipts).IsEmpty();
    }

    [Test]
    public async Task Complete_prepared_receipt_binds_the_actual_persisted_cycle_before_mutation()
    {
        var (preparation, request, repository) = await PreparedCompletionAsync();

        var receipt = await new ContextSourceCycleCoordinator(repository, []).CompletePreparedReceiptAsync(preparation, request);

        await Assert.That(receipt.Request).IsEqualTo(request);
        await Assert.That(repository.ReceiptCommits).IsEqualTo(1);
        await Assert.That(repository.GetCycleCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Complete_prepared_receipt_accepts_rejected_observation_with_headed_LKG_and_not_attempted()
    {
        var (preparation, request, repository) = PreparedRejectedHtmlCompletion();
        var headedLkg = request with { SelectedOrigin = BundesligaContextSourceSelectedOrigin.LastKnownGood };

        var receipt = await new ContextSourceCycleCoordinator(repository, []).CompletePreparedReceiptAsync(preparation, headedLkg);

        await Assert.That(receipt.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(receipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.NotAttempted);
    }

    [Test]
    [Arguments(BundesligaContextSourcePublicationDisposition.Published)]
    [Arguments(BundesligaContextSourcePublicationDisposition.Unchanged)]
    [Arguments(BundesligaContextSourcePublicationDisposition.Reactivated)]
    public async Task Complete_prepared_receipt_requires_an_existing_receipt_before_guarded_publication(
        BundesligaContextSourcePublicationDisposition publication)
    {
        var (preparation, request, repository) = PreparedEloCompletion();
        var guarded = request with { PublicationDisposition = publication };
        var before = CompletionGraph(repository);

        await AssertStateConflictAsync(() => new ContextSourceCycleCoordinator(repository, []).CompletePreparedReceiptAsync(preparation, guarded));

        await Assert.That(repository.ReceiptCommits).IsEqualTo(0);
        await Assert.That(repository.PublicationCalls).IsEqualTo(0);
        await Assert.That(repository.IssueUpdates).IsEqualTo(0);
        await Assert.That(CompletionGraph(repository)).IsEqualTo(before);
        await Assert.That(repository.ReceiptLookups).IsEquivalentTo([(guarded.Identity, guarded.Source, guarded.ConsumerLaneId)]);
    }

    [Test]
    public async Task Complete_prepared_receipt_rejects_a_semantically_different_existing_guarded_receipt_without_mutation()
    {
        var (preparation, request, repository) = PreparedEloCompletion();
        var guarded = request with { PublicationDisposition = BundesligaContextSourcePublicationDisposition.Published };
        var prior = new BundesligaContextSourceReceipt(guarded with { SelectedSnapshotId = new string('d', 64) }, new DateTimeOffset(2026, 9, 6, 12, 3, 0, TimeSpan.Zero));
        prior.Validate();
        repository.Receipts.Add((guarded.Source, guarded.ConsumerLaneId), prior);
        var before = CompletionGraph(repository);

        await AssertStateConflictAsync(() => new ContextSourceCycleCoordinator(repository, []).CompletePreparedReceiptAsync(preparation, guarded));

        await Assert.That(repository.ReceiptCommits).IsEqualTo(0);
        await Assert.That(repository.PublicationCalls).IsEqualTo(0);
        await Assert.That(repository.IssueUpdates).IsEqualTo(0);
        await Assert.That(CompletionGraph(repository)).IsEqualTo(before);
        await Assert.That(repository.ReceiptLookups).IsEquivalentTo([(guarded.Identity, guarded.Source, guarded.ConsumerLaneId)]);
    }

    [Test]
    [Arguments(BundesligaContextSourcePublicationDisposition.Published)]
    [Arguments(BundesligaContextSourcePublicationDisposition.Unchanged)]
    [Arguments(BundesligaContextSourcePublicationDisposition.Reactivated)]
    public async Task Complete_prepared_receipt_replays_an_exact_guarded_publication_without_replacing_its_receipt(
        BundesligaContextSourcePublicationDisposition publication)
    {
        var (preparation, request, repository) = PreparedEloCompletion();
        var guarded = request with { PublicationDisposition = publication };
        var persistedRequest = guarded with { ActiveConditions = new List<BundesligaContextSourceHealthCondition>(guarded.ActiveConditions) };
        var persisted = new BundesligaContextSourceReceipt(persistedRequest, new DateTimeOffset(2026, 9, 6, 12, 3, 0, TimeSpan.Zero));
        repository.Receipts.Add((guarded.Source, guarded.ConsumerLaneId), persisted);

        var replay = await new ContextSourceCycleCoordinator(repository, []).CompletePreparedReceiptAsync(preparation, guarded);

        await Assert.That(replay).IsEqualTo(persisted);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(persisted.RecordedAtUtc);
        await Assert.That(repository.ReceiptCommits).IsEqualTo(1);
        await Assert.That(repository.ReceiptLookups).IsEquivalentTo([(guarded.Identity, guarded.Source, guarded.ConsumerLaneId)]);
    }

    [Test]
    public async Task Complete_prepared_receipt_rejects_cross_identity_requests_before_a_receipt_call()
    {
        var (preparation, request, repository) = await PreparedCompletionAsync();
        var coordinator = new ContextSourceCycleCoordinator(repository, []);
        var alternatives = new[]
        {
            request with { Identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000009") },
            request with { ConsumerLaneId = "arena-context", CommunityContext = "ehonda-ai-arena" },
            request with { Source = BundesligaContextSource.ClubElo },
            request with { BundleDigest = new string('f', 64) },
            request with { ObservationDigest = new string('e', 64) },
            request with { CommunityContext = "wrong-community" }
        };

        foreach (var hostile in alternatives)
            await Assert.That(() => coordinator.CompletePreparedReceiptAsync(preparation, hostile)).Throws<InvalidDataException>();

        await Assert.That(repository.ReceiptCommits).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_prepared_receipt_rejects_missing_or_crossed_persisted_observations_before_a_receipt_call()
    {
        var (preparation, request, missingRepository) = await PreparedCompletionAsync();
        missingRepository.SourceCycle = null;
        await Assert.That(() => new ContextSourceCycleCoordinator(missingRepository, []).CompletePreparedReceiptAsync(preparation, request)).Throws<InvalidDataException>();
        await Assert.That(missingRepository.ReceiptCommits).IsEqualTo(0);

        var (crossedPreparation, crossedRequest, crossedRepository) = await PreparedCompletionAsync();
        var original = crossedRepository.SourceCycle!;
        var crossedObservation = original.Observation! with { ObservedAtUtc = original.Observation!.ObservedAtUtc.AddSeconds(1) };
        crossedRepository.SourceCycle = original with { Observation = crossedObservation, ObservationDigest = crossedObservation.ObservationDigest };
        await Assert.That(() => new ContextSourceCycleCoordinator(crossedRepository, []).CompletePreparedReceiptAsync(crossedPreparation, crossedRequest)).Throws<InvalidDataException>();
        await Assert.That(crossedRepository.ReceiptCommits).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_prepared_receipt_dry_run_writes_nothing()
    {
        var cycle = Cycle(); var repository = new MemoryRepository(cycle); var coordinator = new ContextSourceCycleCoordinator(repository, [new Provider(cycle.Identity)]);
        await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, true));
        var observation = preparation.Files.Bundle.Observations.Single();
        var request = PersistedReceipt(cycle.Identity, observation, preparation.Files.Digest, preparation.CurrentLaneId).Request;

        await Assert.That(() => coordinator.CompletePreparedReceiptAsync(preparation, request)).Throws<InvalidOperationException>();
        await Assert.That(repository.ReceiptCommits).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_prepared_not_attempted_receipt_creates_once_then_replays_and_projects_after_commit()
    {
        var (preparation, request, repository) = await PreparedCompletionAsync(production: true);
        repository.HealthState = CompletedProductionHealth();
        var projector = new ThrowingIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [], projector, new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 6, 0, TimeSpan.Zero)));

        var first = await coordinator.CompletePreparedReceiptAsync(preparation, request);
        var replay = await coordinator.CompletePreparedReceiptAsync(preparation, request);

        await Assert.That(replay).IsEqualTo(first);
        await Assert.That(replay.RecordedAtUtc).IsEqualTo(new DateTimeOffset(2026, 9, 6, 12, 4, 0, TimeSpan.Zero));
        await Assert.That(repository.ReceiptCommits).IsEqualTo(2);
        await Assert.That(repository.ReceiptLookups).IsEmpty();
        await Assert.That(repository.HealthState!.DesiredIssueProjection!.SynchronizationStatus).IsEqualTo(BundesligaContextSourceIssueSynchronization.Pending);
        await Assert.That(repository.HealthState.DesiredIssueProjection.LastErrorCode).IsEqualTo(BundesligaContextSourceIssueError.GithubIssueListFailed);
    }
    [Test]
    public async Task Both_disabled_bypasses_before_coordinator_repository_or_provider_resolution()
    {
        var resolutions = 0;
        var result = await ContextSourceCycleCoordinator.ExecuteIfEnabledAsync(false, false,
            () => { resolutions++; throw new InvalidOperationException("must not resolve"); },
            (_, _) => throw new InvalidOperationException("must not execute"));
        await Assert.That(result).IsNull(); await Assert.That(resolutions).IsEqualTo(0);
    }

    [Test]
    public async Task Development_flow_finalizes_once_writes_verified_handoff_and_cleans_in_finally()
    {
        var cycle = Cycle(); var repository = new MemoryRepository(cycle); var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false));
        var directory = ContextSourceBundleHandoff.CreateDevelopmentDirectory(cycle.Identity);
        await Assert.That(Directory.Exists(directory)).IsTrue();
        await Assert.That(provider.Calls).IsEqualTo(1);
        await Assert.That(repository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await preparation.DisposeAsync();
        await Assert.That(Directory.Exists(directory)).IsFalse();
        await Assert.That(ContextSourceCyclePreparation.Current).IsNull();
    }

    [Test]
    public async Task Development_bundle_verified_replay_loads_the_existing_local_handoff_and_advances_ready()
    {
        var requested = Cycle();
        var observation = (await new Provider(requested.Identity).ObserveAsync(requested.Identity)).Observation;
        var files = new ContextSourceBundleFiles(
            new BundesligaContextSourceBundle(requested.Identity, requested.StartedAtUtc, requested.StalenessReferenceAtUtc, requested.ProducerLaneId, requested.ExpectedConsumers, [observation]),
            new Dictionary<string, byte[]>());
        var persisted = requested with { Status = BundesligaContextSourceCycleStatus.BundleVerified, BundleSha256 = files.Digest };
        var repository = new MemoryRepository(persisted);
        var provider = new Provider(requested.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);

        try
        {
            await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false));

            await Assert.That(preparation.Files.Digest).IsEqualTo(files.Digest);
            await Assert.That(preparation.PersistedCycle!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
            await Assert.That(repository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
            await Assert.That(provider.Calls).IsEqualTo(0);
        }
        finally
        {
            ContextSourceBundleHandoff.CleanupDevelopment(requested.Identity);
        }
    }

    [Test]
    [Arguments(false, BundesligaContextSourceError.LocalHandoffMissing)]
    [Arguments(true, BundesligaContextSourceError.HandoffArtifactConflict)]
    public async Task Development_bundle_verified_replay_preserves_missing_and_conflict_abort_classification(bool corrupt, BundesligaContextSourceError expected)
    {
        var requested = Cycle();
        var observation = (await new Provider(requested.Identity).ObserveAsync(requested.Identity)).Observation;
        var files = new ContextSourceBundleFiles(
            new BundesligaContextSourceBundle(requested.Identity, requested.StartedAtUtc, requested.StalenessReferenceAtUtc, requested.ProducerLaneId, requested.ExpectedConsumers, [observation]),
            new Dictionary<string, byte[]>());
        var persisted = requested with { Status = BundesligaContextSourceCycleStatus.BundleVerified, BundleSha256 = files.Digest };
        var repository = new MemoryRepository(persisted);
        var provider = new Provider(requested.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        if (corrupt)
        {
            await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);
            await File.WriteAllTextAsync(Path.Combine(ContextSourceBundleHandoff.CreateDevelopmentDirectory(requested.Identity), "manifest.json"), "[]");
        }

        try
        {
            await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false))).Throws<InvalidDataException>();
            await Assert.That(repository.Cycle.AbortCode).IsEqualTo(expected);
            await Assert.That(provider.Calls).IsEqualTo(0);
        }
        finally
        {
            ContextSourceBundleHandoff.CleanupDevelopment(requested.Identity);
        }
    }

    [Test]
    public async Task Development_local_handoff_cancellation_is_not_recorded_as_missing()
    {
        var cycle = Cycle();
        var repository = new MemoryRepository(cycle);
        var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false), cancellationToken: cancellation.Token)).Throws<OperationCanceledException>();

        await Assert.That(repository.AbortCalls).IsEqualTo(0);
        await Assert.That(repository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.BundleVerified);
    }

    [Test]
    public async Task Dry_run_passes_the_caller_cancellation_token_to_the_provider()
    {
        var cycle = Cycle();
        var repository = new MemoryRepository(cycle);
        var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        using var cancellation = new CancellationTokenSource();

        await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, true), cancellationToken: cancellation.Token);

        await Assert.That(provider.LastCancellationToken).IsEqualTo(cancellation.Token);
        await Assert.That(repository.CreateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Dry_run_rejects_an_observation_whose_source_differs_from_its_registered_provider()
    {
        var cycle = Cycle();
        var repository = new MemoryRepository(cycle);
        var provider = new WrongSourceProvider();
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);

        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, true))).Throws<InvalidDataException>();

        await Assert.That(repository.CreateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Development_finalized_interrupted_cycle_aborts_as_local_handoff_missing_without_provider_reacquisition()
    {
        var cycle = Cycle(); var repository = new MemoryRepository(cycle) { ClaimDisposition = BundesligaContextSourceClaimDisposition.ExistingFinalized }; var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        InvalidDataException? exception = null;
        try { await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false)); }
        catch (InvalidDataException caught) { exception = caught; }
        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("LOCAL_HANDOFF_MISSING");
        await Assert.That(provider.Calls).IsEqualTo(0);
        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(BundesligaContextSourceError.LocalHandoffMissing);
    }

    [Test]
    public async Task Development_first_finalized_source_in_two_source_claiming_cycle_aborts_as_local_handoff_missing()
    {
        var cycle = Cycle() with { EnabledSources = [BundesligaContextSource.ClubElo, BundesligaContextSource.Rosters] };
        var repository = new MemoryRepository(cycle) { ClaimDisposition = BundesligaContextSourceClaimDisposition.ExistingFinalized };
        var clubEloProvider = new NeverCalledProvider(BundesligaContextSource.ClubElo);
        var rosterProvider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [clubEloProvider, rosterProvider]);

        InvalidDataException? exception = null;
        try { await coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false)); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("LOCAL_HANDOFF_MISSING");
        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(BundesligaContextSourceError.LocalHandoffMissing);
        await Assert.That(clubEloProvider.Calls).IsEqualTo(0);
        await Assert.That(rosterProvider.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Production_consumer_cannot_create_a_missing_shared_cycle_or_call_a_provider()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 456);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var cycle = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(cycle) { ReturnMissingForGet = true }; var provider = new Provider(identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[1]))).Throws<InvalidDataException>();
        await Assert.That(repository.CreateCalls).IsEqualTo(0);
        await Assert.That(provider.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Production_consumer_cannot_acquire_from_an_existing_claiming_cycle()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 456);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var cycle = new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(cycle); var provider = new Provider(identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[1]))).Throws<InvalidDataException>();
        await Assert.That(repository.CreateCalls).IsEqualTo(0);
        await Assert.That(provider.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Direct_production_call_requires_an_explicit_current_lane()
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming); var repository = new MemoryRepository(cycle); var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false))).Throws<InvalidDataException>();
        await Assert.That(repository.CreateCalls).IsEqualTo(0);
        await Assert.That(provider.Calls).IsEqualTo(0);
    }

    [Test]
    [Arguments("pes-squad-context")]
    [Arguments("schadensfresse-context")]
    public async Task Completed_lower_watermark_producer_and_consumer_are_late_before_handoff_or_provider_reentry(string currentLane)
    {
        var completed = ProductionCycle(BundesligaContextSourceCycleStatus.Complete);
        var newerIdentity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 457);
        var newer = new BundesligaContextSourceOuterCycle(newerIdentity, completed.StartedAtUtc, completed.StartedAtUtc, completed.ProducerLaneId, completed.ExpectedConsumers, completed.EnabledSources, BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(completed) { HealthState = ProductionHealth(newer, aborted: false) };
        var provider = new Provider(completed.Identity); var store = new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Present);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        var requested = completed with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null, CompletedAtUtc = null };

        InvalidDataException? exception = null;
        try { await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false, currentLane), store); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("LATE_CYCLE");
        await Assert.That(provider.Calls).IsEqualTo(0);
        await Assert.That(store.Probes).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_cycle_preparation_replays_receipts_by_exact_arena_lane_not_shared_community()
    {
        var initial = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming);
        var observation = (await new Provider(initial.Identity).ObserveAsync(initial.Identity)).Observation;
        var bundle = new BundesligaContextSourceBundle(initial.Identity, initial.StartedAtUtc, initial.StalenessReferenceAtUtc, initial.ProducerLaneId, initial.ExpectedConsumers, [observation]);
        var files = new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]>());
        var complete = initial with
        {
            Status = BundesligaContextSourceCycleStatus.Complete,
            BundleSha256 = files.Digest,
            ArtifactName = $"bundesliga-context-source-bundle-{initial.Identity.StorageId}",
            CompletedAtUtc = initial.StartedAtUtc.AddMinutes(2)
        };
        var repository = new MemoryRepository(complete);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers.Where(value => value.StartsWith("arena-", StringComparison.Ordinal)))
            repository.Receipts[(BundesligaContextSource.Rosters, lane)] = PersistedReceipt(complete.Identity, observation, files.Digest, lane);
        var store = new StaticArtifactStore([
            new ContextSourceArtifactEntry("manifest.json", bundle.CreateManifestUtf8()),
            new ContextSourceArtifactEntry("bundle.sha256", Encoding.ASCII.GetBytes(files.Digest + "\n"))
        ]);
        var requested = complete with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null, CompletedAtUtc = null };
        var provider = new Provider(initial.Identity); var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);

        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers.Where(value => value.StartsWith("arena-", StringComparison.Ordinal)))
        {
            await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false, lane), store);
            await Assert.That(preparation.CurrentLaneId).IsEqualTo(lane);
            await Assert.That(preparation.PersistedCycle!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
            await Assert.That(preparation.TryGetPersistedReceipt(BundesligaContextSource.Rosters, out var receipt)).IsTrue();
            await Assert.That(receipt!.Request.ConsumerLaneId).IsEqualTo(lane);
            await Assert.That(receipt.Request.CommunityContext).IsEqualTo("ehonda-ai-arena");
        }
        await Assert.That(provider.Calls).IsEqualTo(0);
        await Assert.That(repository.ReceiptLaneRequests).IsEquivalentTo(BundesligaContextSourceContract.ProductionConsumers.Where(value => value.StartsWith("arena-", StringComparison.Ordinal)));
    }

    [Test]
    [Arguments(ContextSourceArtifactProbeDisposition.Absent, BundesligaContextSourceError.HandoffArtifactMissing)]
    [Arguments(ContextSourceArtifactProbeDisposition.Conflict, BundesligaContextSourceError.HandoffArtifactConflict)]
    public async Task Handoff_ready_load_failure_is_durably_aborted_with_the_exact_code(ContextSourceArtifactProbeDisposition disposition, BundesligaContextSourceError expected)
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.HandoffReady); var repository = new MemoryRepository(cycle); var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[1]), new ProbeArtifactStore(disposition))).Throws<InvalidDataException>();
        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(expected);
        await Assert.That(provider.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Production_handoff_state_matrix_preserves_single_acquisition_and_follower_receipt_boundaries()
    {
        var requested = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(requested); var provider = new Provider(requested.Identity); var store = new MemoryArtifactStore();
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);

        await using var initial = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false,
            BundesligaContextSourceContract.ProductionConsumers[0]), store);
        await Assert.That(repository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That(repository.Transitions.ToArray()).IsEqualTo([
            (BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified),
            (BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved),
            (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceCycleStatus.HandoffReady)]);
        await Assert.That(provider.Calls).IsEqualTo(1); await Assert.That(store.Uploads).IsEqualTo(1); await Assert.That(store.Probes).IsEqualTo(2); await Assert.That(repository.ReceiptCommits).IsEqualTo(0);

        repository.Cycle = repository.Cycle with { Status = BundesligaContextSourceCycleStatus.UploadReserved };
        await using var replay = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false,
            BundesligaContextSourceContract.ProductionConsumers[0]), store);
        await Assert.That(replay.PersistedCycle!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That(provider.Calls).IsEqualTo(1); await Assert.That(store.Uploads).IsEqualTo(1); await Assert.That(store.Probes).IsEqualTo(3);

        await using var producerReady = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false,
            BundesligaContextSourceContract.ProductionConsumers[0]), store);
        await using var followerReady = await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false,
            BundesligaContextSourceContract.ProductionConsumers[1]), store);
        await Assert.That(producerReady.PersistedCycle!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That(followerReady.CurrentLaneId).IsEqualTo(BundesligaContextSourceContract.ProductionConsumers[1]);
        await Assert.That(provider.Calls).IsEqualTo(1); await Assert.That(store.Uploads).IsEqualTo(1); await Assert.That(store.Probes).IsEqualTo(5); await Assert.That(repository.ReceiptCommits).IsEqualTo(0);
    }

    [Test]
    public async Task Persisted_handoff_state_table_preserves_ordered_transitions_identity_and_prefixes()
    {
        foreach (var (status, lane) in new[]
                 {
                     (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[1]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[1])
                 })
        {
            var (cycle, files) = PersistedProduction(status); var request = cycle with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null };
            var repository = new MemoryRepository(cycle) { SourceCycle = FinalizedSourceCycle(cycle, files.Bundle.Observations.Single()) };
            var provider = new Provider(cycle.Identity); var store = new StaticArtifactStore(EntriesFor(files));
            var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
            var before = Fingerprint(repository, provider, store.Probes, store.Uploads);
            await using var preparation = await coordinator.PrepareAsync(new ContextSourceCycleRequest(request, request.EnabledSources, false, lane), store);
            await Assert.That(Fingerprint(repository, provider, store.Probes, store.Uploads)).IsEqualTo(AfterCycleLookup(before, lane) with
             {
                 Prefix = before.Prefix with { Status = BundesligaContextSourceCycleStatus.HandoffReady },
                 Probes = before.Probes + 1,
                TransitionCount = before.TransitionCount + (status == BundesligaContextSourceCycleStatus.UploadReserved ? 1 : 0),
                TransitionOrder = status == BundesligaContextSourceCycleStatus.UploadReserved ? "UploadReserved>HandoffReady" : ""
             });
        }

        var development = Cycle(); var developmentFiles = FilesFor(development);
        var developmentPersisted = development with { Status = BundesligaContextSourceCycleStatus.HandoffReady, BundleSha256 = developmentFiles.Digest };
        var developmentRepository = new MemoryRepository(developmentPersisted); var developmentProvider = new Provider(development.Identity);
        var developmentBefore = Fingerprint(developmentRepository, developmentProvider, 0, 0);
        await ContextSourceBundleHandoff.WriteDevelopmentAsync(developmentFiles);
        try
        {
            await using var replay = await new ContextSourceCycleCoordinator(developmentRepository, [developmentProvider]).PrepareAsync(new ContextSourceCycleRequest(development, development.EnabledSources, false));
            await Assert.That(Fingerprint(developmentRepository, developmentProvider, 0, 0)).IsEqualTo(AfterCycleLookup(developmentBefore, development.ProducerLaneId));
        }
        finally { ContextSourceBundleHandoff.CleanupDevelopment(development.Identity); }

        foreach (var lane in new[] { BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers[1] })
        {
            var (complete, completeFiles) = PersistedProduction(BundesligaContextSourceCycleStatus.Complete);
            var completeRequest = complete with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null, CompletedAtUtc = null };
            var completeRepository = new MemoryRepository(complete); var completeProvider = new Provider(complete.Identity); var completeStore = new StaticArtifactStore(EntriesFor(completeFiles));
            completeRepository.SourceCycle = CompleteSourceCycle(complete, completeFiles.Bundle.Observations.Single());
            completeRepository.Receipts[(BundesligaContextSource.Rosters, lane)] = PersistedReceipt(complete.Identity, completeFiles.Bundle.Observations[0], completeFiles.Digest, lane);
            var before = Fingerprint(completeRepository, completeProvider, completeStore.Probes, completeStore.Uploads);
            await using var preparation = await new ContextSourceCycleCoordinator(completeRepository, [completeProvider]).PrepareAsync(new ContextSourceCycleRequest(completeRequest, completeRequest.EnabledSources, false, lane), completeStore);
            await Assert.That(preparation.PersistedCycle!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
            await Assert.That(completeRepository.Cycle.Identity).IsEqualTo(complete.Identity); await Assert.That(completeRepository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
            await Assert.That(Fingerprint(completeRepository, completeProvider, completeStore.Probes, completeStore.Uploads)).IsEqualTo(AfterCycleLookup(before, lane) with { Probes = before.Probes + 1 });
        }
    }

    [Test]
    public async Task Indeterminate_persisted_handoff_retries_without_prefix_or_counter_drift()
    {
        foreach (var (status, lane) in new[]
                 {
                     (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[1]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[1]),
                      (BundesligaContextSourceCycleStatus.Complete, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.Complete, BundesligaContextSourceContract.ProductionConsumers[1])
                 })
        {
            var (cycle, files) = PersistedProduction(status); var request = cycle with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null, CompletedAtUtc = null };
            var repository = new MemoryRepository(cycle) { SourceCycle = status == BundesligaContextSourceCycleStatus.Complete ? CompleteSourceCycle(cycle, files.Bundle.Observations.Single()) : FinalizedSourceCycle(cycle, files.Bundle.Observations.Single()) }; var provider = new Provider(cycle.Identity);
            if (status == BundesligaContextSourceCycleStatus.Complete)
                repository.Receipts[(BundesligaContextSource.Rosters, lane)] = PersistedReceipt(cycle.Identity, files.Bundle.Observations.Single(), files.Digest, lane);
            var store = new ScriptedArtifactStore([ContextSourceArtifactProbeDisposition.Indeterminate, ContextSourceArtifactProbeDisposition.Present], EntriesFor(files));
            var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
            var before = Fingerprint(repository, provider, store.Probes, store.Uploads);
            await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(request, request.EnabledSources, false, lane), store)).Throws<ContextSourceArtifactRetryException>();
            await Assert.That(Fingerprint(repository, provider, store.Probes, store.Uploads)).IsEqualTo(AfterCycleLookup(before, lane) with { Probes = before.Probes + 1 });
            await using var retry = await coordinator.PrepareAsync(new ContextSourceCycleRequest(request, request.EnabledSources, false, lane), store);
            await Assert.That(Fingerprint(repository, provider, store.Probes, store.Uploads)).IsEqualTo(AfterCycleLookup(before, lane, 2) with
             {
                 Prefix = before.Prefix with { Status = status == BundesligaContextSourceCycleStatus.UploadReserved ? BundesligaContextSourceCycleStatus.HandoffReady : status },
                 Probes = before.Probes + 2,
                TransitionCount = before.TransitionCount + (status == BundesligaContextSourceCycleStatus.UploadReserved ? 1 : 0),
                TransitionOrder = status == BundesligaContextSourceCycleStatus.UploadReserved ? "UploadReserved>HandoffReady" : ""
             });
        }
    }

    [Test]
    public async Task Production_typed_conflict_aborts_once_while_indeterminate_probe_remains_retryable()
    {
        foreach (var (status, lane) in new[]
                 {
                     (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.UploadReserved, BundesligaContextSourceContract.ProductionConsumers[1]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.HandoffReady, BundesligaContextSourceContract.ProductionConsumers[1]),
                      (BundesligaContextSourceCycleStatus.Complete, BundesligaContextSourceContract.ProductionConsumers[0]),
                      (BundesligaContextSourceCycleStatus.Complete, BundesligaContextSourceContract.ProductionConsumers[1])
                 })
        {
            var (cycle, files) = PersistedProduction(status);
            var repository = new MemoryRepository(cycle) { SourceCycle = status == BundesligaContextSourceCycleStatus.Complete ? CompleteSourceCycle(cycle, files.Bundle.Observations.Single()) : FinalizedSourceCycle(cycle, files.Bundle.Observations.Single()) }; var provider = new Provider(cycle.Identity);
            if (status == BundesligaContextSourceCycleStatus.Complete)
                repository.Receipts[(BundesligaContextSource.Rosters, lane)] = PersistedReceipt(cycle.Identity, files.Bundle.Observations.Single(), files.Digest, lane);
            var malformed = new StaticArtifactStore([new ContextSourceArtifactEntry("manifest.json", Encoding.UTF8.GetBytes("[]")), new ContextSourceArtifactEntry("bundle.sha256", Encoding.ASCII.GetBytes(cycle.BundleSha256! + "\n"))]);
            var coordinator = new ContextSourceCycleCoordinator(repository, [provider]);
            var request = cycle with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null, CompletedAtUtc = null };
            var before = Fingerprint(repository, provider, malformed.Probes, malformed.Uploads);

            await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(request, request.EnabledSources, false, lane), malformed)).Throws<InvalidDataException>();
            await Assert.That(repository.Cycle.AbortCode).IsEqualTo(status == BundesligaContextSourceCycleStatus.Complete ? null : BundesligaContextSourceError.HandoffArtifactConflict);
            var aborted = Fingerprint(repository, provider, malformed.Probes, malformed.Uploads);
            var expectedStatus = status == BundesligaContextSourceCycleStatus.Complete ? BundesligaContextSourceCycleStatus.Complete : BundesligaContextSourceCycleStatus.Aborted;
            await Assert.That(aborted).IsEqualTo(AfterCycleLookup(before, lane) with
            {
                Prefix = before.Prefix with { Status = expectedStatus, AbortCode = status == BundesligaContextSourceCycleStatus.Complete ? null : BundesligaContextSourceError.HandoffArtifactConflict },
                Probes = before.Probes + 1,
                AbortCalls = before.AbortCalls + (status == BundesligaContextSourceCycleStatus.Complete ? 0 : 1)
            });

            await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(request, request.EnabledSources, false, lane), malformed)).Throws<InvalidDataException>();
            await Assert.That(Fingerprint(repository, provider, malformed.Probes, malformed.Uploads)).IsEqualTo(AfterCycleLookup(aborted, lane) with { Probes = aborted.Probes + (status == BundesligaContextSourceCycleStatus.Complete ? 1 : 0) });
        }

        var retryCycle = ProductionCycle(BundesligaContextSourceCycleStatus.UploadReserved);
        var retryRepository = new MemoryRepository(retryCycle); var retryCoordinator = new ContextSourceCycleCoordinator(retryRepository, [new Provider(retryCycle.Identity)]);
        await Assert.That(() => retryCoordinator.PrepareAsync(new ContextSourceCycleRequest(retryCycle with { Status = BundesligaContextSourceCycleStatus.Claiming, BundleSha256 = null, ArtifactName = null }, retryCycle.EnabledSources, false,
            BundesligaContextSourceContract.ProductionConsumers[0]), new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Indeterminate))).Throws<ContextSourceArtifactRetryException>();
        await Assert.That(retryRepository.Cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.UploadReserved);
        await Assert.That(retryRepository.AbortCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Complete_handoff_conflict_surfaces_without_reducing_the_terminal_cycle()
    {
        foreach (var lane in new[] { BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers[1] })
        {
            var (cycle, files) = PersistedProduction(BundesligaContextSourceCycleStatus.Complete);
            var repository = new MemoryRepository(cycle) { SourceCycle = CompleteSourceCycle(cycle, files.Bundle.Observations.Single()) }; var provider = new Provider(cycle.Identity);
            repository.Receipts[(BundesligaContextSource.Rosters, lane)] = PersistedReceipt(cycle.Identity, files.Bundle.Observations.Single(), files.Digest, lane);
            var store = new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Conflict); var before = Fingerprint(repository, provider, store.Probes, 0);
            await Assert.That(() => new ContextSourceCycleCoordinator(repository, [provider]).PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, lane), store)).Throws<InvalidDataException>();
            await Assert.That(Fingerprint(repository, provider, store.Probes, 0)).IsEqualTo(AfterCycleLookup(before, lane) with { Probes = before.Probes + 1 });
        }
    }

    [Test]
    public async Task Producer_probe_conflict_is_durably_aborted_as_artifact_conflict()
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming); var repository = new MemoryRepository(cycle); var provider = new Provider(cycle.Identity);
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider]); var store = new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Conflict);
        var before = Fingerprint(repository, provider, store.Probes, 0);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[0]), store)).Throws<InvalidDataException>();
        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(BundesligaContextSourceError.HandoffArtifactConflict);
        var aborted = Fingerprint(repository, provider, store.Probes, 0);
        await Assert.That(repository.Transitions).IsEquivalentTo([
            (BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified),
            (BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved)]);
        var expectedObservation = (await new Provider(cycle.Identity).ObserveAsync(cycle.Identity)).Observation;
        var expectedFiles = FilesFor(cycle);
        var expected = before with
        {
            Prefix = before.Prefix with
            {
                Status = BundesligaContextSourceCycleStatus.Aborted,
                BundleSha256 = expectedFiles.Digest,
                ArtifactName = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}",
                AbortCode = BundesligaContextSourceError.HandoffArtifactConflict
            },
            Source = new SourcePrefix(BundesligaContextSource.Rosters, BundesligaContextSourceSourceStatus.Finalized,
                expectedObservation.AttemptId, expectedObservation.ObservationDigest, ""),
            ProviderCalls = before.ProviderCalls + 1, Probes = before.Probes + 1, TransitionCount = before.TransitionCount + 2,
            AbortCalls = before.AbortCalls + 1, CreateCalls = before.CreateCalls + 1, ClaimCalls = before.ClaimCalls + 1,
            FinalizeCalls = before.FinalizeCalls + 1,
            TransitionOrder = "ObservationsFinalized>BundleVerified|BundleVerified>UploadReserved"
        };
        await Assert.That(aborted).IsEqualTo(expected);
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[0]), store)).Throws<InvalidDataException>();
        await Assert.That(Fingerprint(repository, provider, store.Probes, 0)).IsEqualTo(AfterCycleLookup(aborted, cycle.ProducerLaneId));
        await Assert.That(provider.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Issue_projection_failure_and_replay_update_only_post_commit_synchronization_state()
    {
        var health = CompletedProductionHealth(); var repository = new ProjectionRepository(health); var projector = new ScriptedIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [], projector, new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 5, 0, TimeSpan.Zero)));
        var request = ProductionReceipt(health);

        await coordinator.RecordReceiptAsync(request);
        await Assert.That(repository.Health.LastCompletedCycleId).IsEqualTo(health.LastCompletedCycleId);
        await Assert.That(repository.Health.DesiredIssueProjection!.SynchronizationStatus).IsEqualTo(BundesligaContextSourceIssueSynchronization.Pending);
        await Assert.That(repository.Health.DesiredIssueProjection.LastErrorCode).IsEqualTo(BundesligaContextSourceIssueError.GithubIssueUpdateFailed);

        await coordinator.RecordReceiptAsync(request);
        await Assert.That(repository.Health.LastCompletedCycleId).IsEqualTo(health.LastCompletedCycleId);
        await Assert.That(repository.Health.DesiredIssueProjection!.SynchronizationStatus).IsEqualTo(BundesligaContextSourceIssueSynchronization.Synchronized);
        await coordinator.RecordReceiptAsync(request);
        await Assert.That(projector.Calls).IsEqualTo(2);
        await Assert.That(repository.ReceiptCommits).IsEqualTo(3);
    }

    [Test]
    public async Task Exact_production_receipt_replay_with_missing_health_returns_without_issue_projection()
    {
        var health = CompletedProductionHealth();
        var request = ProductionReceipt(health);
        var persisted = new BundesligaContextSourceReceipt(request, new DateTimeOffset(2026, 9, 6, 12, 4, 0, TimeSpan.Zero));
        var repository = new MissingHealthReplayRepository(persisted);
        var projector = new ScriptedIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [], projector);

        var replay = await coordinator.RecordReceiptAsync(request);

        await Assert.That(replay).IsEqualTo(persisted);
        await Assert.That(repository.ReceiptCalls).IsEqualTo(1);
        await Assert.That(repository.HealthCalls).IsEqualTo(1);
        await Assert.That(projector.Calls).IsEqualTo(0);
    }

    [Test]
    public async Task Projector_exception_after_completed_receipt_is_nonfatal_and_CASes_pending_failure()
    {
        var health = CompletedProductionHealth(); var repository = new ProjectionRepository(health); var projector = new ThrowingIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [], projector, new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 6, 0, TimeSpan.Zero)));

        var receipt = await coordinator.RecordReceiptAsync(ProductionReceipt(health));

        await Assert.That(receipt.Request.Identity.CycleId).IsEqualTo(health.Watermark.CycleId);
        await Assert.That(repository.Health.DesiredIssueProjection!.SynchronizationStatus).IsEqualTo(BundesligaContextSourceIssueSynchronization.Pending);
        await Assert.That(repository.Health.DesiredIssueProjection.LastAttemptedAtUtc).IsEqualTo(new DateTimeOffset(2026, 9, 6, 12, 6, 0, TimeSpan.Zero));
        await Assert.That(repository.Health.DesiredIssueProjection.LastErrorCode).IsEqualTo(BundesligaContextSourceIssueError.GithubIssueListFailed);
    }

    [Test]
    public async Task Abort_path_reconciles_its_durable_projection_even_when_the_API_throws()
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.HandoffReady);
        var repository = new MemoryRepository(cycle) { HealthState = ProductionHealth(cycle, aborted: false) };
        var projector = new ThrowingIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [new Provider(cycle.Identity)], projector, new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 7, 0, TimeSpan.Zero)));

        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[1]), new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Absent))).Throws<InvalidDataException>();

        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(BundesligaContextSourceError.HandoffArtifactMissing);
        await Assert.That(repository.HealthState!.ActiveConditions).Contains(BundesligaContextSourceHealthCondition.CycleAborted);
        await Assert.That(repository.HealthState.DesiredIssueProjection!.LastErrorCode).IsEqualTo(BundesligaContextSourceIssueError.GithubIssueListFailed);
        await Assert.That(repository.IssueUpdates).IsEqualTo(1);
    }

    [Test]
    [Arguments("pes-squad-context")]
    [Arguments("schadensfresse-context")]
    public async Task Persisted_aborted_cycle_replays_its_exact_error_and_reconciles_pending_issue_for_producer_or_consumer(string currentLane)
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.Aborted) with
        {
            AbortCode = BundesligaContextSourceError.HandoffArtifactConflict,
            BundleSha256 = null,
            ArtifactName = null
        };
        var repository = new MemoryRepository(cycle) { HealthState = ProductionHealth(cycle, aborted: true) };
        var provider = new Provider(cycle.Identity);
        var store = new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Absent);
        var projector = new ScriptedIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider], projector);
        var requested = cycle with { Status = BundesligaContextSourceCycleStatus.Claiming, AbortCode = null };

        InvalidDataException? exception = null;
        try { await coordinator.PrepareAsync(new ContextSourceCycleRequest(requested, requested.EnabledSources, false, currentLane), store); }
        catch (InvalidDataException caught) { exception = caught; }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
        await Assert.That(repository.Cycle.AbortCode).IsEqualTo(BundesligaContextSourceError.HandoffArtifactConflict);
        await Assert.That(repository.AbortCalls).IsEqualTo(0);
        await Assert.That(repository.IssueUpdates).IsEqualTo(1);
        await Assert.That(provider.Calls).IsEqualTo(0);
        await Assert.That(store.Probes).IsEqualTo(0);
    }

    [Test]
    public async Task Ordinary_cycle_start_does_not_call_the_issue_projector()
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(cycle) { HealthState = ProductionHealth(cycle, aborted: false), ClaimDisposition = BundesligaContextSourceClaimDisposition.Busy };
        var provider = new Provider(cycle.Identity); var projector = new ScriptedIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [provider], projector);
        var before = Fingerprint(repository, provider, 0, 0, projector.Calls);

        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[0]), new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Absent))).Throws<InvalidDataException>();

        await Assert.That(repository.Transitions).IsEmpty();
        var firstFailure = Fingerprint(repository, provider, 0, 0, projector.Calls);
        await Assert.That(firstFailure).IsEqualTo(before with { CreateCalls = before.CreateCalls + 1, ClaimCalls = before.ClaimCalls + 1 });
        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[0]), new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Absent))).Throws<InvalidDataException>();
        await Assert.That(Fingerprint(repository, provider, 0, 0, projector.Calls)).IsEqualTo(firstFailure with { CreateCalls = firstFailure.CreateCalls + 1, ClaimCalls = firstFailure.ClaimCalls + 1 });
        await Assert.That(projector.Calls).IsEqualTo(0);
        await Assert.That(repository.IssueUpdates).IsEqualTo(0);
    }

    [Test]
    public async Task Supersession_projection_is_reconciled_immediately_after_durable_cycle_creation()
    {
        var cycle = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming);
        var repository = new MemoryRepository(cycle) { HealthState = ProductionHealth(cycle, aborted: true), ClaimDisposition = BundesligaContextSourceClaimDisposition.Busy, StartSuperseded = true };
        var projector = new ScriptedIssueProjector();
        var coordinator = new ContextSourceCycleCoordinator(repository, [new Provider(cycle.Identity)], projector, new FixedTimeProvider(new DateTimeOffset(2026, 9, 6, 12, 8, 0, TimeSpan.Zero)));

        await Assert.That(() => coordinator.PrepareAsync(new ContextSourceCycleRequest(cycle, cycle.EnabledSources, false, BundesligaContextSourceContract.ProductionConsumers[0]), new ProbeArtifactStore(ContextSourceArtifactProbeDisposition.Absent))).Throws<InvalidDataException>();

        await Assert.That(projector.Calls).IsEqualTo(1);
        await Assert.That(repository.HealthState!.DesiredIssueProjection!.LastAttemptedAtUtc).IsEqualTo(new DateTimeOffset(2026, 9, 6, 12, 8, 0, TimeSpan.Zero));
        await Assert.That(repository.HealthState.DesiredIssueProjection.LastErrorCode).IsEqualTo(BundesligaContextSourceIssueError.GithubIssueUpdateFailed);
    }

    private sealed record CompletionGraph(string State, int ReceiptCommits, int PublicationCalls, int IssueUpdates);

    private static CompletionGraph CompletionGraph(MemoryRepository repository)
        => new(
            JsonSerializer.Serialize(new
            {
                repository.Cycle,
                SourceCycle = repository.EffectiveSourceCycle,
                repository.HealthState,
                Receipts = repository.Receipts
                    .OrderBy(pair => pair.Key.Source)
                    .ThenBy(pair => pair.Key.Lane, StringComparer.Ordinal)
                    .Select(pair => new { pair.Key.Source, pair.Key.Lane, pair.Value })
                    .ToArray()
            }),
            repository.ReceiptCommits,
            repository.PublicationCalls,
            repository.IssueUpdates);

    private static async Task AssertStateConflictAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            throw new InvalidOperationException("Expected a STATE_CONFLICT.");
        }
        catch (InvalidDataException exception)
        {
            await Assert.That(exception.Message).IsEqualTo("STATE_CONFLICT");
        }
    }

    private sealed record CyclePrefix(
        BundesligaContextSourceCycleIdentity Identity,
        BundesligaContextSourceCycleStatus Status,
        string ProducerLaneId,
        string ExpectedConsumers,
        string EnabledSources,
        string? BundleSha256,
        string? ArtifactName,
        BundesligaContextSourceError? AbortCode,
        DateTimeOffset? CompletedAtUtc);

    private sealed record SourcePrefix(
        BundesligaContextSource Source,
        BundesligaContextSourceSourceStatus Status,
        string AttemptId,
        string? ObservationDigest,
        string ReceivedConsumers);

    private sealed record HandoffFingerprint(
        CyclePrefix Prefix,
        SourcePrefix? Source,
        int ProviderCalls,
        int Probes,
        int Uploads,
        int ReceiptCommits,
        int PublicationCalls,
        int TransitionCount,
        int AbortCalls,
        int CreateCalls,
        int ClaimCalls,
        int FinalizeCalls,
        int GetCycleCalls,
        int ProjectionUpdates,
        int ProjectorCalls,
        string TransitionOrder);

    private static HandoffFingerprint Fingerprint(MemoryRepository repository, Provider provider, int probes, int uploads, int projectorCalls = 0)
    {
        var cycle = repository.Cycle;
        var source = repository.EffectiveSourceCycle;
        return new HandoffFingerprint(
            new CyclePrefix(cycle.Identity, cycle.Status, cycle.ProducerLaneId,
                string.Join("|", cycle.ExpectedConsumers), string.Join("|", cycle.EnabledSources), cycle.BundleSha256,
                cycle.ArtifactName, cycle.AbortCode, cycle.CompletedAtUtc),
            source is null ? null : new SourcePrefix(source.Source, source.Status, source.AttemptId, source.ObservationDigest,
                string.Join("|", source.ReceivedConsumers)),
            provider.Calls, probes, uploads, repository.ReceiptCommits, repository.PublicationCalls, repository.Transitions.Count, repository.AbortCalls,
            repository.CreateCalls, repository.ClaimCalls, repository.FinalizeCalls, repository.GetCycleCalls, repository.IssueUpdates, projectorCalls,
            string.Join("|", repository.Transitions.Select(transition => $"{transition.Expected}>{transition.Next}")));
    }

    private static HandoffFingerprint AfterCycleLookup(HandoffFingerprint before, string lane, int attempts = 1) => before with
    {
        CreateCalls = before.CreateCalls + (lane == before.Prefix.ProducerLaneId ? attempts : 0),
        GetCycleCalls = before.GetCycleCalls + (lane == before.Prefix.ProducerLaneId ? 0 : attempts)
    };

    private static BundesligaContextSourceOuterCycle Cycle()
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000001"); var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        ContextSourceBundleHandoff.CleanupDevelopment(identity);
        return new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentConsumers, [BundesligaContextSource.Rosters], BundesligaContextSourceCycleStatus.Claiming);
    }

    private static async Task<(ContextSourceCyclePreparation Preparation, BundesligaContextSourceReceiptRequest Request, MemoryRepository Repository)> PreparedCompletionAsync(bool production = false)
    {
        var initial = production ? ProductionCycle(BundesligaContextSourceCycleStatus.Claiming) : Cycle();
        var observation = (await new Provider(initial.Identity).ObserveAsync(initial.Identity)).Observation;
        var files = new ContextSourceBundleFiles(new BundesligaContextSourceBundle(
            initial.Identity, initial.StartedAtUtc, initial.StalenessReferenceAtUtc, initial.ProducerLaneId,
            initial.ExpectedConsumers, [observation]), new Dictionary<string, byte[]>());
        var persisted = initial with { Status = BundesligaContextSourceCycleStatus.HandoffReady, BundleSha256 = files.Digest,
            ArtifactName = production ? $"bundesliga-context-source-bundle-{initial.Identity.StorageId}" : null };
        var repository = new MemoryRepository(persisted);
        repository.SourceCycle = FinalizedSourceCycle(persisted, observation);
        var preparation = new ContextSourceCyclePreparation(files, cleanup: false, persisted.ProducerLaneId, persisted,
            new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>());
        return (preparation, PersistedReceipt(persisted.Identity, observation, files.Digest, persisted.ProducerLaneId).Request, repository);
    }

    private static (ContextSourceCyclePreparation Preparation, BundesligaContextSourceReceiptRequest Request, MemoryRepository Repository) PreparedEloCompletion()
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000008");
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); var bytes = Encoding.UTF8.GetBytes("html");
        var mapping = new[]
        {
            ("b04", "/Leverkusen", "Leverkusen"), ("bmg", "/Gladbach", "Gladbach"), ("bvb", "/Dortmund", "Dortmund"), ("fca", "/Augsburg", "Augsburg"),
            ("fcb", "/Bayern", "Bayern München"), ("fck", "/Koeln", "Köln"), ("fcu", "/UnionBerlin", "Union Berlin"), ("hsv", "/Hamburg", "Hamburg"),
            ("m05", "/Mainz", "Mainz"), ("rbl", "/RBLeipzig", "RB Leipzig"), ("s04", "/Schalke", "Schalke"), ("scf", "/Freiburg", "Freiburg"),
            ("scp", "/Paderborn", "Paderborn"), ("sge", "/Frankfurt", "Frankfurt"), ("sve", "/Elversberg", "Elversberg"), ("svw", "/Werder", "Werder"),
            ("tsg", "/Hoffenheim", "Hoffenheim"), ("vfb", "/Stuttgart", "Stuttgart")
        };
        var descriptor = JsonSerializer.Serialize(new { contract = "club-elo-official-html-descriptor/v1", sourceUrl = "https://clubelo.com/GER", response = new { statusCode = 200, finalUrl = "https://clubelo.com/GER", redirectCount = 0, redirectLocation = (string?)null, mediaType = "text/html", charset = "utf-8", contentEncodings = Array.Empty<string>(), declaredContentLength = (long)bytes.Length }, rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length, parserContract = "club-elo-official-html-parser/v1", displayedDate = "2026-09-04", providerDateEvidence = new { kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1", field = "h1>a[href]", rawValue = "2026-09-04", ratedAt = "2026-09-04" }, tableContract = "club-elo-official-html-table/v1", tableHeader = new[] { "Club", "Elo", "+/-", "Golo" }, nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1", nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256, sourceRows = mapping.Select((entry, index) => new { teamSlug = entry.Item1, providerRoute = entry.Item2, providerDisplayName = entry.Item3, globalRank = index + 1, elo = 1500 + index }).ToArray(), evaluation = "Eligible" });
        var payload = new BundesligaContextSourcePayload("club-elo/source.html", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes));
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, payload, []);
        var bundle = new BundesligaContextSourceBundle(identity, now, now, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentConsumers, [observation]);
        var files = new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]> { [payload.Path] = bytes });
        var persisted = new BundesligaContextSourceOuterCycle(identity, now, now, bundle.ProducerLaneId, bundle.ExpectedConsumers, [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.HandoffReady, files.Digest);
        var request = new BundesligaContextSourceReceiptRequest(identity, BundesligaContextSource.ClubElo, bundle.ProducerLaneId, "ehonda-dev-buli-2627", observation.ObservationDigest, files.Digest, BundesligaContextSourceSelectionDisposition.NetworkAccepted, new string('c', 64), BundesligaContextSourceSelectedOrigin.NetworkCandidate, BundesligaContextSourcePublicationDisposition.Unchanged, new BundesligaContextSourceDates(new DateOnly(2026, 9, 4), null, null, null), null, new BundesligaContextSourceCarriedFields(0, 0, 0, null), []);
        var repository = new MemoryRepository(persisted) { SourceCycle = FinalizedSourceCycle(persisted, observation) };
        return (new ContextSourceCyclePreparation(files, cleanup: false, bundle.ProducerLaneId, persisted, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>()), request, repository);
    }

    private static (ContextSourceCyclePreparation Preparation, BundesligaContextSourceReceiptRequest Request, MemoryRepository Repository) PreparedRejectedHtmlCompletion()
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000007");
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        const string descriptor = "{\"contract\":\"club-elo-official-html-descriptor/v1\",\"sourceUrl\":\"https://clubelo.com/GER\",\"response\":null,\"rawSha256\":null,\"rawByteLength\":null,\"parserContract\":\"club-elo-official-html-parser/v1\",\"displayedDate\":null,\"providerDateEvidence\":null,\"tableContract\":\"club-elo-official-html-table/v1\",\"tableHeader\":[\"Club\",\"Elo\",\"+/-\",\"Golo\"],\"nameMappingContract\":\"bundesliga-2026-27-club-elo-name-map/v1\",\"nameMappingSha256\":\"8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7\",\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}";
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(identity, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.Rejected, descriptor, null, ["CLUB_ELO_TRANSPORT_REJECTED"]);
        var bundle = new BundesligaContextSourceBundle(identity, now, now, BundesligaContextSourceContract.DevelopmentLane, BundesligaContextSourceContract.DevelopmentConsumers, [observation]);
        var files = new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]>());
        var persisted = new BundesligaContextSourceOuterCycle(identity, now, now, bundle.ProducerLaneId, bundle.ExpectedConsumers, [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.HandoffReady, files.Digest);
        var request = new BundesligaContextSourceReceiptRequest(identity, BundesligaContextSource.ClubElo, bundle.ProducerLaneId, "ehonda-dev-buli-2627", observation.ObservationDigest, files.Digest, BundesligaContextSourceSelectionDisposition.NetworkCandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.LastKnownGood, BundesligaContextSourcePublicationDisposition.NotAttempted, new BundesligaContextSourceDates(new DateOnly(2026, 9, 6), null, null, null), null, new BundesligaContextSourceCarriedFields(0, 0, 0, null), [BundesligaContextSourceHealthCondition.AcquisitionFailed]);
        var repository = new MemoryRepository(persisted) { SourceCycle = FinalizedSourceCycle(persisted, observation) };
        return (new ContextSourceCyclePreparation(files, cleanup: false, bundle.ProducerLaneId, persisted, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>()), request, repository);
    }

    private static BundesligaContextSourceCycleClaim FinalizedSourceCycle(BundesligaContextSourceOuterCycle cycle, BundesligaContextSourceObservation observation)
        => new(cycle.Identity, observation.Source, observation.AttemptId, BundesligaContextSourceSourceStatus.Finalized,
            BundesligaContextSourceContract.NewClaimToken(), cycle.StartedAtUtc, cycle.StartedAtUtc.AddMinutes(10),
            cycle.StartedAtUtc.AddMinutes(1), observation.ObservationDigest, observation, null, [], null);

    private static BundesligaContextSourceCycleClaim CompleteSourceCycle(BundesligaContextSourceOuterCycle cycle, BundesligaContextSourceObservation observation)
        => FinalizedSourceCycle(cycle, observation) with
        {
            Status = BundesligaContextSourceSourceStatus.Complete,
            ReceivedConsumers = cycle.ExpectedConsumers,
            CompletedAtUtc = cycle.CompletedAtUtc
        };

    private static BundesligaContextSourceOuterCycle ProductionCycle(BundesligaContextSourceCycleStatus status)
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 456); var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var digest = status >= BundesligaContextSourceCycleStatus.UploadReserved ? new string('e', 64) : null;
        var artifact = status >= BundesligaContextSourceCycleStatus.UploadReserved ? $"bundesliga-context-source-bundle-{identity.StorageId}" : null;
        return new BundesligaContextSourceOuterCycle(identity, now, now, BundesligaContextSourceContract.ProductionConsumers[0], BundesligaContextSourceContract.ProductionConsumers, [BundesligaContextSource.Rosters], status, digest, artifact, null, status == BundesligaContextSourceCycleStatus.Complete ? now.AddMinutes(2) : null);
    }
    private static (BundesligaContextSourceOuterCycle Cycle, ContextSourceBundleFiles Files) PersistedProduction(BundesligaContextSourceCycleStatus status)
    {
        var requested = ProductionCycle(BundesligaContextSourceCycleStatus.Claiming); var files = FilesFor(requested);
        return (requested with { Status = status, BundleSha256 = files.Digest, ArtifactName = $"bundesliga-context-source-bundle-{requested.Identity.StorageId}", CompletedAtUtc = status == BundesligaContextSourceCycleStatus.Complete ? requested.StartedAtUtc.AddMinutes(2) : null }, files);
    }
    private static ContextSourceBundleFiles FilesFor(BundesligaContextSourceOuterCycle cycle)
    {
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(cycle.Identity, BundesligaContextSource.Rosters), cycle.StartedAtUtc, BundesligaContextSourceDisposition.Rejected, Descriptor(), null, ["UNKNOWN_SOURCE_DATE"]);
        return new ContextSourceBundleFiles(new BundesligaContextSourceBundle(cycle.Identity, cycle.StartedAtUtc, cycle.StalenessReferenceAtUtc, cycle.ProducerLaneId, cycle.ExpectedConsumers, [observation]), new Dictionary<string, byte[]>());
    }
    private static ContextSourceArtifactEntry[] EntriesFor(ContextSourceBundleFiles files) => [new("manifest.json", files.Bundle.CreateManifestUtf8()), new("bundle.sha256", Encoding.ASCII.GetBytes(files.Digest + "\n"))];
    private static string Descriptor() => $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"SourceDateRejected\"}}";

    private static BundesligaContextSourceHealth CompletedProductionHealth()
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 123, 456);
        var watermark = new BundesligaContextSourceWatermark(identity.Sequence, identity.CycleId);
        var conditions = new[] { BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown };
        var selections = BundesligaContextSourceContract.ProductionConsumers.Order(StringComparer.Ordinal).Select(lane => new BundesligaContextSourceCommunitySelection(lane, Community(lane), new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, null, null, new DateOnly(2026, 8, 1), null, conditions)).ToArray();
        var marker = "<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->";
        var body = BundesligaContextSourceHealth.CreateIssueBody(marker, identity.Competition, BundesligaContextSource.Rosters, watermark, conditions);
        var issue = new BundesligaContextSourceIssueProjection(marker, "[KicktippAi] Bundesliga 2026/27 rosters context-source health", BundesligaContextSourceHealth.HashIssueBody(body), BundesligaContextSourceIssueState.Open, null, BundesligaContextSourceIssueSynchronization.Pending, null, null);
        var health = new BundesligaContextSourceHealth(identity.Competition, identity.Scope, BundesligaContextSource.Rosters, watermark, identity.CycleId, new BundesligaContextSourceFailures(0, 0, 0, 0), new BundesligaContextSourceSuccessfulDates(null, new DateOnly(2026, 8, 1), null), new BundesligaContextSourceRosterRevisionState(null, null), selections, conditions, issue);
        health.Validate(); return health;
    }

    private static BundesligaContextSourceHealth ProductionHealth(BundesligaContextSourceOuterCycle cycle, bool aborted)
    {
        var watermark = new BundesligaContextSourceWatermark(cycle.Identity.Sequence, cycle.Identity.CycleId);
        var conditions = BundesligaContextSourceHealth.OrderConditions(aborted
            ? [BundesligaContextSourceHealthCondition.CycleAborted, BundesligaContextSourceHealthCondition.HandoffIncomplete]
            : [BundesligaContextSourceHealthCondition.HandoffIncomplete]);
        var marker = "<!-- kicktippai:context-source-health:bundesliga-2026-27:rosters -->";
        var body = BundesligaContextSourceHealth.CreateIssueBody(marker, cycle.Identity.Competition, BundesligaContextSource.Rosters, watermark, conditions);
        var issue = new BundesligaContextSourceIssueProjection(marker, "[KicktippAi] Bundesliga 2026/27 rosters context-source health", BundesligaContextSourceHealth.HashIssueBody(body), BundesligaContextSourceIssueState.Closed, null, BundesligaContextSourceIssueSynchronization.Pending, null, null);
        var health = new BundesligaContextSourceHealth(cycle.Identity.Competition, cycle.Identity.Scope, BundesligaContextSource.Rosters, watermark, null, new BundesligaContextSourceFailures(0, 0, 0, aborted ? 1 : 0), new BundesligaContextSourceSuccessfulDates(null, null, null), new BundesligaContextSourceRosterRevisionState(null, null), [], conditions, issue);
        health.Validate(); return health;
    }

    private static BundesligaContextSourceReceiptRequest ProductionReceipt(BundesligaContextSourceHealth health)
    {
        var identity = BundesligaContextSourceCycleIdentity.Create(health.Competition, health.Scope, health.Watermark.CycleId, health.Watermark.Sequence);
        return new BundesligaContextSourceReceiptRequest(identity, health.Source, BundesligaContextSourceContract.ProductionConsumers[^1], "ehonda-ai-arena", new string('a', 64), new string('b', 64), BundesligaContextSourceSelectionDisposition.CandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.NotAttempted, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 8, 1), null), new string('d', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]);
    }

    private static BundesligaContextSourceReceipt PersistedReceipt(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceObservation observation, string bundleDigest, string lane)
        => new(new BundesligaContextSourceReceiptRequest(identity, BundesligaContextSource.Rosters, lane, Community(lane), observation.ObservationDigest, bundleDigest, BundesligaContextSourceSelectionDisposition.CandidateRejected, new string('c', 64), BundesligaContextSourceSelectedOrigin.FallbackSeed, BundesligaContextSourcePublicationDisposition.NotAttempted, new BundesligaContextSourceDates(null, null, new DateOnly(2026, 9, 6), null), new string('a', 40), new BundesligaContextSourceCarriedFields(0, 0, 0, null), [BundesligaContextSourceHealthCondition.RosterEnrichmentDateUnknown]), new DateTimeOffset(2026, 9, 6, 12, 2, 0, TimeSpan.Zero));

    private static string Community(string lane) => lane switch
    {
        "pes-squad-context" => "pes-squad", "schadensfresse-context" => "schadensfresse", "relaxdays-tippt-context" => "relaxdays-tippt", _ => "ehonda-ai-arena"
    };

    private sealed class Provider(BundesligaContextSourceCycleIdentity identity) : IBundesligaContextSourceObservationProvider
    {
        public BundesligaContextSource Source => BundesligaContextSource.Rosters;
        public int Calls { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }
        public Task<BundesligaContextSourceObservationResult> ObserveAsync(BundesligaContextSourceCycleIdentity cycle, CancellationToken cancellationToken = default) { Calls++; LastCancellationToken = cancellationToken; var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); return Task.FromResult(new BundesligaContextSourceObservationResult(new BundesligaContextSourceObservation(Source, BundesligaContextSourceHashing.AttemptId(identity, Source), now, BundesligaContextSourceDisposition.Rejected, Descriptor(), null, ["UNKNOWN_SOURCE_DATE"]), null)); }
    }

    private sealed class WrongSourceProvider : IBundesligaContextSourceObservationProvider
    {
        public BundesligaContextSource Source => BundesligaContextSource.Rosters;
        public Task<BundesligaContextSourceObservationResult> ObserveAsync(BundesligaContextSourceCycleIdentity cycle, CancellationToken cancellationToken = default)
        {
            var descriptor = "{\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"sourceUrl\":\"https://example.test/elo.csv\",\"rawSha256\":null,\"rawByteLength\":null,\"csvHeader\":null,\"providerRatedAt\":null,\"providerDateEvidence\":null,\"nameMappingContract\":null,\"nameMappingSha256\":null,\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}";
            var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), cycle.Scope == BundesligaContextSourceScope.Development ? new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero) : throw new InvalidOperationException(), BundesligaContextSourceDisposition.Rejected, descriptor, null, ["UNKNOWN_SOURCE_DATE"]);
            return Task.FromResult(new BundesligaContextSourceObservationResult(observation, null));
        }
    }

    private sealed class NeverCalledProvider(BundesligaContextSource source) : IBundesligaContextSourceObservationProvider
    {
        public BundesligaContextSource Source { get; } = source;
        public int Calls { get; private set; }
        public Task<BundesligaContextSourceObservationResult> ObserveAsync(BundesligaContextSourceCycleIdentity cycle, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Provider should not be called.");
        }
    }

    private sealed class ProbeArtifactStore(ContextSourceArtifactProbeDisposition disposition) : IContextSourceBundleArtifactStore
    {
        public int Probes { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default) { Probes++; return Task.FromResult(new ContextSourceArtifactProbe(disposition, [])); }
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Upload should not be called.");
    }

    private sealed class StaticArtifactStore(IReadOnlyList<ContextSourceArtifactEntry> entries) : IContextSourceBundleArtifactStore
    {
        public int Probes { get; private set; }
        public int Uploads { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default) { Probes++; return Task.FromResult(new ContextSourceArtifactProbe(ContextSourceArtifactProbeDisposition.Present, entries)); }
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> uploadEntries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default) { Uploads++; throw new InvalidOperationException("Upload should not be called."); }
    }

    private sealed class ScriptedArtifactStore(IEnumerable<ContextSourceArtifactProbeDisposition> dispositions, IReadOnlyList<ContextSourceArtifactEntry> entries) : IContextSourceBundleArtifactStore
    {
        private readonly Queue<ContextSourceArtifactProbeDisposition> _dispositions = new(dispositions);
        public int Probes { get; private set; }
        public int Uploads { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
        {
            Probes++;
            var disposition = _dispositions.Dequeue();
            return Task.FromResult(new ContextSourceArtifactProbe(disposition, disposition == ContextSourceArtifactProbeDisposition.Present ? entries : []));
        }
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> uploadEntries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
        {
            Uploads++; throw new InvalidOperationException("Persisted handoff must not upload.");
        }
    }

    private sealed class MemoryArtifactStore : IContextSourceBundleArtifactStore
    {
        private IReadOnlyList<ContextSourceArtifactEntry>? _entries;
        public int Uploads { get; private set; }
        public int Probes { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
        {
            Probes++; return Task.FromResult(new ContextSourceArtifactProbe(_entries is null ? ContextSourceArtifactProbeDisposition.Absent : ContextSourceArtifactProbeDisposition.Present, _entries ?? []));
        }
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
        {
            Uploads++; _entries = entries; return Task.CompletedTask;
        }
    }

    private sealed class MemoryRepository(BundesligaContextSourceOuterCycle cycle) : IBundesligaContextSourceCycleRepository
    {
        public BundesligaContextSourceOuterCycle Cycle { get; set; } = cycle;
        public BundesligaContextSourceClaimDisposition ClaimDisposition { get; set; } = BundesligaContextSourceClaimDisposition.NewClaim;
        public bool ReturnMissingForGet { get; set; }
        public bool StartSuperseded { get; set; }
        public int CreateCalls { get; private set; }
        public int ClaimCalls { get; private set; }
        public int FinalizeCalls { get; private set; }
        public int AbortCalls { get; private set; }
        public int IssueUpdates { get; private set; }
        public int ReceiptCommits { get; private set; }
        public int PublicationCalls { get; private set; }
        public List<(BundesligaContextSourceCycleStatus Expected, BundesligaContextSourceCycleStatus Next)> Transitions { get; } = [];
        public int GetCycleCalls { get; private set; }
        public BundesligaContextSourceHealth? HealthState { get; set; }
        public BundesligaContextSourceCycleClaim? SourceCycle { get; set; }
        public Dictionary<(BundesligaContextSource Source, string Lane), BundesligaContextSourceReceipt> Receipts { get; } = [];
        public List<string> ReceiptLaneRequests { get; } = [];
        public List<(BundesligaContextSourceCycleIdentity Identity, BundesligaContextSource Source, string Lane)> ReceiptLookups { get; } = [];
        private BundesligaContextSourceCycleClaim? _claim;
        public BundesligaContextSourceCycleClaim? EffectiveSourceCycle => SourceCycle ?? _claim;
        public Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default) { CreateCalls++; return Task.FromResult(Cycle); }
        public Task<BundesligaContextSourceCycleStartResult> CreateOrResumeCycleWithResultAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default) { CreateCalls++; return Task.FromResult(new BundesligaContextSourceCycleStartResult(Cycle, StartSuperseded ? Cycle.EnabledSources : [])); }
        public Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default)
        {
            ClaimCalls++;
            _claim ??= new BundesligaContextSourceCycleClaim(identity, source, BundesligaContextSourceHashing.AttemptId(identity, source), ClaimDisposition == BundesligaContextSourceClaimDisposition.ExistingFinalized ? BundesligaContextSourceSourceStatus.Finalized : BundesligaContextSourceSourceStatus.Claimed, claimToken, claimedAtUtc, claimedAtUtc.AddMinutes(10), null, null, null, null, [], null);
            return Task.FromResult(new BundesligaContextSourceClaimResult(ClaimDisposition, _claim));
        }
        public Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default) { FinalizeCalls++; _claim = _claim! with { Status = BundesligaContextSourceSourceStatus.Finalized, FinalizedAtUtc = finalizedAtUtc, ObservationDigest = observation.ObservationDigest, Observation = observation }; Cycle = Cycle with { Status = BundesligaContextSourceCycleStatus.ObservationsFinalized }; return Task.FromResult(_claim); }
        public Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default) { if (Cycle.Status != expected) throw new InvalidDataException("STATE_CONFLICT"); Transitions.Add((expected, next)); Cycle = Cycle with { Status = next, BundleSha256 = bundleSha256 ?? Cycle.BundleSha256, ArtifactName = artifactName ?? Cycle.ArtifactName }; return Task.FromResult(Cycle); }
        public Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default) { AbortCalls++; Cycle = Cycle with { Status = BundesligaContextSourceCycleStatus.Aborted, AbortCode = error }; if (HealthState is not null) HealthState = ProductionHealth(Cycle, aborted: true); return Task.FromResult(Cycle); }
        public Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default)
        {
            ReceiptCommits++; PublicationCalls++;
            if (Receipts.TryGetValue((request.Source, request.ConsumerLaneId), out var persisted))
            {
                if (persisted.Request with { ActiveConditions = request.ActiveConditions } != request
                    || !persisted.Request.ActiveConditions.SequenceEqual(request.ActiveConditions))
                    throw new InvalidDataException("STATE_CONFLICT");
                return Task.FromResult(persisted);
            }
            var receipt = new BundesligaContextSourceReceipt(request, new DateTimeOffset(2026, 9, 6, 12, 4, 0, TimeSpan.Zero));
            Receipts.Add((request.Source, request.ConsumerLaneId), receipt);
            return Task.FromResult(receipt);
        }
        public Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default) { GetCycleCalls++; return Task.FromResult<BundesligaContextSourceOuterCycle?>(ReturnMissingForGet ? null : Cycle); }
        public Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default) => Task.FromResult(SourceCycle ?? _claim);
        public Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default) { ReceiptLaneRequests.Add(consumerLaneId); ReceiptLookups.Add((identity, source, consumerLaneId)); return Task.FromResult(Receipts.GetValueOrDefault((source, consumerLaneId))); }
        public Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default) => Task.FromResult(HealthState);
        public Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default) { if (HealthState is null || HealthState.Watermark != expectedHealth.Watermark) throw new InvalidDataException("STATE_CONFLICT"); IssueUpdates++; HealthState = HealthState with { DesiredIssueProjection = projection }; HealthState.Validate(); return Task.FromResult(HealthState); }
    }

    private sealed class ProjectionRepository(BundesligaContextSourceHealth health) : IBundesligaContextSourceCycleRepository
    {
        public BundesligaContextSourceHealth Health { get; private set; } = health;
        public int ReceiptCommits { get; private set; }
        public Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default) { ReceiptCommits++; return Task.FromResult(new BundesligaContextSourceReceipt(request, new DateTimeOffset(2026, 9, 6, 12, 4, 0, TimeSpan.Zero))); }
        public Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default) => Task.FromResult<BundesligaContextSourceHealth?>(Health);
        public Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default) { if (expectedHealth.Watermark != Health.Watermark) throw new InvalidDataException("STATE_CONFLICT"); Health = Health with { DesiredIssueProjection = projection }; Health.Validate(); return Task.FromResult(Health); }
        public Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MissingHealthReplayRepository(BundesligaContextSourceReceipt persisted) : IBundesligaContextSourceCycleRepository
    {
        public int ReceiptCalls { get; private set; }
        public int HealthCalls { get; private set; }
        public Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default)
        {
            ReceiptCalls++;
            if (request != persisted.Request) throw new InvalidDataException("STATE_CONFLICT");
            return Task.FromResult(persisted);
        }
        public Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default)
        {
            HealthCalls++;
            return Task.FromResult<BundesligaContextSourceHealth?>(null);
        }
        public Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ScriptedIssueProjector : IBundesligaContextSourceIssueProjector
    {
        public int Calls { get; private set; }
        public Task<BundesligaContextSourceIssueProjectionAttempt> ProjectAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Calls == 1
                ? BundesligaContextSourceIssueProjectionAttempt.Failed(BundesligaContextSourceIssueError.GithubIssueUpdateFailed)
                : BundesligaContextSourceIssueProjectionAttempt.Synchronized(health.DesiredIssueProjection!.BodySha256));
        }
    }

    private sealed class ThrowingIssueProjector : IBundesligaContextSourceIssueProjector
    {
        public Task<BundesligaContextSourceIssueProjectionAttempt> ProjectAsync(BundesligaContextSourceHealth health, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("simulated GitHub API failure");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider { public override DateTimeOffset GetUtcNow() => value; }
}
