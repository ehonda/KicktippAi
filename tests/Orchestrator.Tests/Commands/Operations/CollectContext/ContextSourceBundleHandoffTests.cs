using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class ContextSourceBundleHandoffTests
{
    [Test]
    public async Task Development_handoff_rehashes_disk_and_rejects_extra_missing_and_corrupt_files()
    {
        var files = Files(development: true); var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(files.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle);
        try
        {
            await ContextSourceBundleHandoff.WriteDevelopmentAsync(files); ContextSourceBundleHandoff.ValidateDevelopmentDirectory(files);
            await File.AppendAllTextAsync(Path.Combine(root, "manifest.json"), " ");
            await Assert.That(() => ContextSourceBundleHandoff.ValidateDevelopmentDirectory(files)).Throws<InvalidDataException>();
            ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); await ContextSourceBundleHandoff.WriteDevelopmentAsync(files); await File.WriteAllTextAsync(Path.Combine(root, "extra"), "x");
            await Assert.That(() => ContextSourceBundleHandoff.ValidateDevelopmentDirectory(files)).Throws<InvalidDataException>();
            File.Delete(Path.Combine(root, "extra")); File.Delete(Path.Combine(root, "bundle.sha256"));
            await Assert.That(() => ContextSourceBundleHandoff.ValidateDevelopmentDirectory(files)).Throws<InvalidDataException>();
        }
        finally { ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); }
    }

    [Test]
    public async Task Artifact_verifier_rejects_links_traversal_duplicates_and_alternate_case()
    {
        var files = Files(development: false); var entries = Entries(files);
        await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle, entries.Append(new ContextSourceArtifactEntry("../escape", [], true)).ToArray(), files.Digest)).Throws<InvalidDataException>();
        await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle, entries.Select(x => x.Path == "manifest.json" ? x with { LinkTarget = "target" } : x).ToArray(), files.Digest)).Throws<InvalidDataException>();
        await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle, entries.Append(entries[0]).ToArray(), files.Digest)).Throws<InvalidDataException>();
        await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle, entries.Select(x => x.Path == "manifest.json" ? x with { Path = "Manifest.json" } : x).ToArray(), files.Digest)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Upload_success_then_ready_CAS_failure_recovers_by_probe_without_reupload()
    {
        var files = Files(development: false); var requested = Outer(files); var repository = new ReservationRepository(requested); var store = new MemoryArtifactStore(); repository.FailFirstReady = true;
        await Assert.That(() => ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(repository, requested, files, store)).Throws<InvalidDataException>();
        var ready = await ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(repository, requested, files, store);
        await Assert.That(ready.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That(store.Uploads).IsEqualTo(1);
        await Assert.That(store.LastOverwrite).IsFalse(); await Assert.That(store.LastCompression).IsEqualTo(0); await Assert.That(store.LastRetention).IsEqualTo(7);
    }

    [Test]
    public async Task Handoff_binds_manifest_to_requested_cycle_and_classifies_reserved_absence_as_upload_failure()
    {
        var files = Files(development: false); var requested = Outer(files);
        var otherIdentity = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 3);
        var mismatched = requested with { Identity = otherIdentity };
        await Assert.That(() => ContextSourceBundleHandoff.ValidateBundleCycle(mismatched, files.Bundle)).Throws<InvalidDataException>();

        var reserved = requested with { Status = BundesligaContextSourceCycleStatus.UploadReserved, BundleSha256 = files.Digest, ArtifactName = $"bundesliga-context-source-bundle-{requested.Identity.StorageId}" };
        await Assert.That(() => ContextSourceBundleHandoff.LoadProductionAsync(reserved, new MemoryArtifactStore())).Throws<InvalidDataException>();
        try { await ContextSourceBundleHandoff.LoadProductionAsync(reserved, new MemoryArtifactStore()); }
        catch (InvalidDataException exception) { await Assert.That(exception.Message).IsEqualTo("HANDOFF_UPLOAD_FAILED"); }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Indeterminate_probe_before_or_after_upload_keeps_reservation_replayable(bool beforeUpload)
    {
        var files = Files(development: false); var requested = Outer(files); var repository = new ReservationRepository(requested);
        var store = new ScriptedArtifactStore(beforeUpload
            ? [ContextSourceArtifactProbeDisposition.Indeterminate, ContextSourceArtifactProbeDisposition.Present]
            : [ContextSourceArtifactProbeDisposition.Absent, ContextSourceArtifactProbeDisposition.Indeterminate, ContextSourceArtifactProbeDisposition.Present], Entries(files));

        await Assert.That(() => ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(repository, requested, files, store)).Throws<ContextSourceArtifactRetryException>();
        await Assert.That((await repository.GetCycleAsync(requested.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.UploadReserved);
        var ready = await ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(repository, requested, files, store);
        await Assert.That(ready.Status).IsEqualTo(BundesligaContextSourceCycleStatus.HandoffReady);
        await Assert.That(store.Uploads).IsEqualTo(beforeUpload ? 0 : 1);
    }

    private static ContextSourceBundleFiles Files(bool development)
    {
        var cycle = development ? BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000002") : BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); var descriptor = "{\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"sourceUrl\":\"https://example.test/elo.csv\",\"rawSha256\":null,\"rawByteLength\":null,\"csvHeader\":null,\"providerRatedAt\":null,\"providerDateEvidence\":null,\"nameMappingContract\":null,\"nameMappingSha256\":null,\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}";
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.Rejected, descriptor, null, ["UNKNOWN_SOURCE_DATE"]);
        var bundle = new BundesligaContextSourceBundle(cycle, now, now, development ? BundesligaContextSourceContract.DevelopmentLane : "pes-squad-context", development ? BundesligaContextSourceContract.DevelopmentConsumers : BundesligaContextSourceContract.ProductionConsumers, [observation]);
        return new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]>());
    }
    private static ContextSourceArtifactEntry[] Entries(ContextSourceBundleFiles files) => [new("manifest.json", files.Bundle.CreateManifestUtf8()), new("bundle.sha256", System.Text.Encoding.ASCII.GetBytes(files.Digest + "\n"))];
    private static BundesligaContextSourceOuterCycle Outer(ContextSourceBundleFiles files) => new(files.Bundle.Cycle, files.Bundle.StartedAtUtc, files.Bundle.StalenessReferenceAtUtc, files.Bundle.ProducerLaneId, files.Bundle.ExpectedConsumers, files.Bundle.Observations.Select(value => value.Source).ToArray(), BundesligaContextSourceCycleStatus.BundleVerified, files.Digest);

    private sealed class MemoryArtifactStore : IContextSourceBundleArtifactStore
    {
        private IReadOnlyList<ContextSourceArtifactEntry>? _entries; public int Uploads { get; private set; } public bool LastOverwrite { get; private set; } public int LastCompression { get; private set; } public int LastRetention { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default) => Task.FromResult(new ContextSourceArtifactProbe(_entries is null ? ContextSourceArtifactProbeDisposition.Absent : ContextSourceArtifactProbeDisposition.Present, _entries ?? []));
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default) { Uploads++; LastOverwrite = overwrite; LastCompression = compressionLevel; LastRetention = retentionDays; _entries = entries; return Task.CompletedTask; }
    }

    private sealed class ScriptedArtifactStore(IEnumerable<ContextSourceArtifactProbeDisposition> dispositions, IReadOnlyList<ContextSourceArtifactEntry> entries) : IContextSourceBundleArtifactStore
    {
        private readonly Queue<ContextSourceArtifactProbeDisposition> _dispositions = new(dispositions);
        private IReadOnlyList<ContextSourceArtifactEntry> _entries = entries;
        public int Uploads { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
        {
            var disposition = _dispositions.Dequeue();
            return Task.FromResult(new ContextSourceArtifactProbe(disposition, disposition == ContextSourceArtifactProbeDisposition.Present ? _entries : []));
        }
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
        {
            Uploads++; _entries = entries; return Task.CompletedTask;
        }
    }

    private sealed class ReservationRepository(BundesligaContextSourceOuterCycle requested) : IBundesligaContextSourceCycleRepository
    {
        private BundesligaContextSourceOuterCycle _cycle = requested;
        public bool FailFirstReady { get; set; }
        public Task<BundesligaContextSourceOuterCycle> TransitionCycleAsync(BundesligaContextSourceCycleIdentity _, BundesligaContextSourceCycleStatus expected, BundesligaContextSourceCycleStatus next, string? bundleSha256 = null, string? artifactName = null, CancellationToken cancellationToken = default) { if (_cycle.Status == next && _cycle.BundleSha256 == bundleSha256 && _cycle.ArtifactName == artifactName) return Task.FromResult(_cycle); if (_cycle.Status != expected) throw new InvalidDataException("STATE_CONFLICT"); if (next == BundesligaContextSourceCycleStatus.HandoffReady && FailFirstReady) { FailFirstReady = false; throw new InvalidDataException("simulated CAS"); } _cycle = _cycle with { Status = next, BundleSha256 = bundleSha256 ?? _cycle.BundleSha256, ArtifactName = artifactName ?? _cycle.ArtifactName }; return Task.FromResult(_cycle); }
        public Task<BundesligaContextSourceOuterCycle> CreateOrResumeCycleAsync(BundesligaContextSourceOuterCycle requested, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceClaimResult> ClaimSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceCycleClaim> FinalizeSourceAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string claimToken, BundesligaContextSourceObservation observation, DateTimeOffset finalizedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle> AbortCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSourceError error, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceReceipt> RecordReceiptAsync(BundesligaContextSourceReceiptRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BundesligaContextSourceOuterCycle?> GetCycleAsync(BundesligaContextSourceCycleIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult<BundesligaContextSourceOuterCycle?>(_cycle);
        public Task<BundesligaContextSourceCycleClaim?> GetSourceCycleAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, CancellationToken cancellationToken = default) => Task.FromResult<BundesligaContextSourceCycleClaim?>(null);
        public Task<BundesligaContextSourceReceipt?> GetReceiptAsync(BundesligaContextSourceCycleIdentity identity, BundesligaContextSource source, string consumerLaneId, CancellationToken cancellationToken = default) => Task.FromResult<BundesligaContextSourceReceipt?>(null);
        public Task<BundesligaContextSourceHealth?> GetHealthAsync(string competition, BundesligaContextSourceScope scope, BundesligaContextSource source, CancellationToken cancellationToken = default) => Task.FromResult<BundesligaContextSourceHealth?>(null);
        public Task<BundesligaContextSourceHealth> UpdateIssueProjectionAsync(BundesligaContextSourceHealth expectedHealth, BundesligaContextSourceIssueProjection projection, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
