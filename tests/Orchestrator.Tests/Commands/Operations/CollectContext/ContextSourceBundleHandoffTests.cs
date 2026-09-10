using System.Text.Json;
using System.Text.Json.Nodes;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class ContextSourceBundleHandoffTests
{
    [Test]
    public async Task Development_html_handoff_round_trips_exact_bytes_and_rejects_crossed_paths()
    {
        var files = HtmlFiles(development: true); var cycle = Outer(files) with { Status = BundesligaContextSourceCycleStatus.HandoffReady };
        var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(files.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle);
        try
        {
            await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);
            var loaded = ContextSourceBundleHandoff.LoadDevelopment(cycle, files.Digest);
            await Assert.That(loaded.Payloads["club-elo/source.html"]).IsEquivalentTo(files.Payloads["club-elo/source.html"]);

            var entries = Entries(files);
            await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle,
                entries.Select(entry => entry.Path == "club-elo/source.html" ? entry with { Path = "club-elo/source.csv" } : entry).ToArray(), files.Digest)).Throws<InvalidDataException>();
            await Assert.That(() => ContextSourceBundleHandoff.VerifyEntries(files.Bundle,
                entries.Select(entry => entry.Path == "club-elo/source.html" ? entry with { Path = "club-elo/Source.html" } : entry).ToArray(), files.Digest)).Throws<InvalidDataException>();

            await File.WriteAllTextAsync(Path.Combine(root, "extra"), "x");
            await Assert.That(() => ContextSourceBundleHandoff.LoadDevelopment(cycle, files.Digest)).Throws<InvalidDataException>();
            ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);
            File.Delete(Path.Combine(root, "club-elo", "source.html"));
            await Assert.That(() => ContextSourceBundleHandoff.LoadDevelopment(cycle, files.Digest)).Throws<InvalidDataException>();
            ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);
            var payloadPath = Path.Combine(root, "club-elo", "source.html"); var temporaryPath = Path.Combine(root, "club-elo", "temporary.html");
            File.Move(payloadPath, temporaryPath); File.Move(temporaryPath, Path.Combine(root, "club-elo", "Source.html"));
            await Assert.That(() => ContextSourceBundleHandoff.LoadDevelopment(cycle, files.Digest)).Throws<InvalidDataException>();
            ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); await ContextSourceBundleHandoff.WriteDevelopmentAsync(files);
            await File.WriteAllBytesAsync(Path.Combine(root, "club-elo", "source.html"), [0x00]);
            await Assert.That(() => ContextSourceBundleHandoff.LoadDevelopment(cycle, files.Digest)).Throws<InvalidDataException>();
        }
        finally { ContextSourceBundleHandoff.CleanupDevelopment(files.Bundle.Cycle); }
    }
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
    public async Task Development_and_production_loads_normalize_malformed_manifests_as_handoff_conflicts()
    {
        var development = HtmlFiles(development: true); var developmentCycle = Outer(development) with { Status = BundesligaContextSourceCycleStatus.HandoffReady };
        var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(development.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle);
        try
        {
            await ContextSourceBundleHandoff.WriteDevelopmentAsync(development);
            await File.WriteAllTextAsync(Path.Combine(root, "manifest.json"), "{");
            InvalidDataException? developmentFailure = null;
            try { _ = ContextSourceBundleHandoff.LoadDevelopment(developmentCycle, development.Digest); }
            catch (InvalidDataException exception) { developmentFailure = exception; }
            await Assert.That(developmentFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
            await Assert.That(developmentFailure.InnerException).IsNotNull();
        }
        finally { ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle); }

        var production = Files(development: false); var productionCycle = Outer(production) with
        {
            Status = BundesligaContextSourceCycleStatus.HandoffReady,
            ArtifactName = $"bundesliga-context-source-bundle-{production.Bundle.Cycle.StorageId}"
        };
        var malformed = Entries(production).Select(entry => entry.Path == "manifest.json" ? entry with { Bytes = System.Text.Encoding.UTF8.GetBytes("{") } : entry).ToArray();
        InvalidDataException? productionFailure = null;
        try { _ = await ContextSourceBundleHandoff.LoadProductionAsync(productionCycle, new StaticArtifactStore(malformed)); }
        catch (InvalidDataException exception) { productionFailure = exception; }
        await Assert.That(productionFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
        await Assert.That(productionFailure.InnerException).IsNotNull();

        var semantic = Entries(production).Select(entry => entry.Path == "manifest.json"
            ? entry with { Bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(entry.Bytes).Replace("\"competition\":\"bundesliga-2026-27\"", "\"competition\":\"invalid\"", StringComparison.Ordinal)) }
            : entry).ToArray();
        InvalidDataException? semanticFailure = null;
        try { _ = await ContextSourceBundleHandoff.LoadProductionAsync(productionCycle, new StaticArtifactStore(semantic)); }
        catch (InvalidDataException exception) { semanticFailure = exception; }
        await Assert.That(semanticFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
        await Assert.That(semanticFailure.InnerException).IsNotNull();
    }

    [Test]
    public async Task Rehashed_csv_evidence_hostile_is_direct_data_error_and_both_loaders_classify_a_conflict()
    {
        var production = CsvFiles(development: false);
        var hostile = RehashedManifest(production, root => root["observations"]!.AsArray()[0]!["descriptor"]!.AsObject()["providerDateEvidence"] = null, true);
        await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(hostile)).Throws<InvalidDataException>();

        var productionCycle = HandoffReady(Outer(production));
        InvalidDataException? productionFailure = null;
        try { _ = await ContextSourceBundleHandoff.LoadProductionAsync(productionCycle, new StaticArtifactStore(ReplaceManifest(Entries(production), hostile))); }
        catch (InvalidDataException exception) { productionFailure = exception; }
        await Assert.That(productionFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");

        var development = CsvFiles(development: true);
        var developmentHostile = RehashedManifest(development, root => root["observations"]!.AsArray()[0]!["descriptor"]!.AsObject()["providerDateEvidence"] = null, true);
        var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(development.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle);
        try
        {
            await ContextSourceBundleHandoff.WriteDevelopmentAsync(development);
            await File.WriteAllBytesAsync(Path.Combine(root, "manifest.json"), developmentHostile);
            InvalidDataException? developmentFailure = null;
            try { _ = ContextSourceBundleHandoff.LoadDevelopment(HandoffReady(Outer(development)), development.Digest); }
            catch (InvalidDataException exception) { developmentFailure = exception; }
            await Assert.That(developmentFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
        }
        finally { ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle); }
    }

    [Test]
    public async Task Rehashed_typed_manifest_hostiles_reach_core_and_both_handoff_adapters()
    {
        var cases = new TypedManifestHostile[]
        {
            new("expected-consumers-object", HtmlFiles, root => root["expectedConsumers"] = new JsonObject()),
            new("expected-consumers-element-type", HtmlFiles, root => root["expectedConsumers"]!.AsArray()[0] = 1),
            new("expected-consumers-empty-string", HtmlFiles, root => root["expectedConsumers"]!.AsArray()[0] = ""),
            new("observations-container-type", HtmlFiles, root => root["observations"] = new JsonObject()),
            new("observation-element-type", HtmlFiles, root => root["observations"]!.AsArray()[0] = 1),
            new("descriptor-null", HtmlFiles, root => Observation(root)["descriptor"] = null),
            new("descriptor-type", HtmlFiles, root => Observation(root)["descriptor"] = new JsonArray()),
            new("payload-type", HtmlFiles, root => Observation(root)["payload"] = new JsonArray()),
            new("payload-byte-length-type", HtmlFiles, root => Observation(root)["payload"]!.AsObject()["byteLength"] = "1"),
            new("diagnostics-type", HtmlFiles, root => Observation(root)["diagnostics"] = new JsonObject()),
            new("diagnostics-element-type", HtmlFiles, root => Observation(root)["diagnostics"] = JsonNode.Parse("[\"not-a-string\",1]")!.AsArray()),
            new("observed-at-type", HtmlFiles, root => Observation(root)["observedAtUtc"] = 1),
            new("observed-at-date", HtmlFiles, root => Observation(root)["observedAtUtc"] = "not-a-date"),
            new("csv-date-evidence-type", CsvFiles, root => Descriptor(root)["providerDateEvidence"] = new JsonArray(), true),
            new("csv-date-evidence-date", CsvFiles, root => Descriptor(root)["providerDateEvidence"]!.AsObject()["ratedAt"] = "not-a-date", true),
            new("csv-source-row-kind", CsvFiles, root => Descriptor(root)["sourceRows"]!.AsArray()[0] = new JsonArray(), true),
            new("csv-raw-byte-length-overflow", CsvFiles, root => Descriptor(root)["rawByteLength"] = JsonNode.Parse("9223372036854775808"), true),
            new("csv-raw-byte-length-noncanonical", CsvFiles, root => Descriptor(root)["rawByteLength"] = JsonNode.Parse("18.0"), true),
            new("html-response-type", HtmlFiles, root => Descriptor(root)["response"] = new JsonArray(), true),
            new("html-date-evidence-date", HtmlFiles, root => Descriptor(root)["providerDateEvidence"]!.AsObject()["ratedAt"] = "not-a-date", true),
            new("html-source-row-kind", HtmlFiles, root => Descriptor(root)["sourceRows"]!.AsArray()[0] = new JsonArray(), true),
            new("html-raw-byte-length-overflow", HtmlFiles, root => Descriptor(root)["rawByteLength"] = JsonNode.Parse("9223372036854775808"), true),
            new("html-raw-byte-length-noncanonical", HtmlFiles, root => Descriptor(root)["rawByteLength"] = JsonNode.Parse("18.0"), true),
            new("roster-remote-identity-type", RosterFiles, root => Descriptor(root)["remoteIdentityBefore"] = new JsonArray(), true),
            new("roster-capture-date", RosterFiles, root => Descriptor(root)["artifactCaptureDate"] = "not-a-date", true),
            new("csv-mapping-identity", CsvFiles, root => Descriptor(root)["nameMappingSha256"] = "not-a-canonical-sha", true),
            new("html-mapping-identity", HtmlFiles, root => Descriptor(root)["nameMappingSha256"] = new string('b', 64), true),
            new("csv-mapping-order", CsvFiles, root => ReverseSourceRows(Descriptor(root)), true),
            new("html-mapping-order", HtmlFiles, root => ReverseSourceRows(Descriptor(root)), true),
            new("csv-duplicate-row", CsvFiles, root => DuplicateSourceRow(Descriptor(root)), true),
            new("html-duplicate-row", HtmlFiles, root => DuplicateSourceRow(Descriptor(root)), true),
            new("root-property-order", HtmlFiles, Rewrite: MoveRootScopeToFront),
            new("duplicate-root-property", HtmlFiles, Rewrite: json => ReplaceFirst(json, "\"contract\":", "\"contract\":\"bundesliga-context-source-bundle/v1\",\"contract\":")),
            new("duplicate-observation-property", HtmlFiles, Rewrite: json => json.Replace("\"source\":\"club-elo\",", "\"source\":\"club-elo\",\"source\":\"club-elo\",", StringComparison.Ordinal)),
            new("duplicate-descriptor-property", CsvFiles, DescriptorRewrite: json => ReplaceFirst(json, "\"contract\":", "\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"contract\":"))
        };
        await Assert.That(cases.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(cases.Length);
        await Assert.That(cases.Length).IsEqualTo(35);
        await Assert.That(cases.Count(value => value.Name.StartsWith("csv-", StringComparison.Ordinal))).IsEqualTo(8);
        await Assert.That(cases.Count(value => value.Name.StartsWith("html-", StringComparison.Ordinal))).IsEqualTo(8);
        await Assert.That(cases.Count(value => value.Name.StartsWith("roster-", StringComparison.Ordinal))).IsEqualTo(2);
        await Assert.That(cases.Any(value => value.Name == "root-property-order")).IsTrue();
        await Assert.That(cases.Any(value => value.Name == "duplicate-descriptor-property")).IsTrue();

        foreach (var hostileCase in cases)
        {
            var production = hostileCase.Fixture(false); var hostile = RehashedManifest(production, hostileCase.Change, hostileCase.RehashDescriptor, hostileCase.DescriptorRewrite, hostileCase.Rewrite);
            await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(hostile)).Throws<InvalidDataException>();
            InvalidDataException? productionFailure = null;
            try { _ = await ContextSourceBundleHandoff.LoadProductionAsync(HandoffReady(Outer(production)), new StaticArtifactStore(ReplaceManifest(Entries(production), hostile))); }
            catch (InvalidDataException exception) { productionFailure = exception; }
            await Assert.That(productionFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");

            var development = hostileCase.Fixture(true); var developmentHostile = RehashedManifest(development, hostileCase.Change, hostileCase.RehashDescriptor, hostileCase.DescriptorRewrite, hostileCase.Rewrite);
            var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(development.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle);
            try
            {
                await ContextSourceBundleHandoff.WriteDevelopmentAsync(development);
                await File.WriteAllBytesAsync(Path.Combine(root, "manifest.json"), developmentHostile);
                InvalidDataException? developmentFailure = null;
                try { _ = ContextSourceBundleHandoff.LoadDevelopment(HandoffReady(Outer(development)), development.Digest); }
                catch (InvalidDataException exception) { developmentFailure = exception; }
                await Assert.That(developmentFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
            }
            finally { ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle); }
        }
    }

    [Test]
    public async Task Rehashed_roster_remote_identity_hostiles_reach_core_and_both_handoff_adapters()
    {
        foreach (var change in new Action<JsonObject>[]
                 {
                     root => root["observations"]!.AsArray()[0]!["descriptor"]!.AsObject()["remoteIdentityBefore"] = new JsonArray(),
                     root => root["observations"]!.AsArray()[0]!["descriptor"]!.AsObject()["remoteIdentityBefore"]!.AsObject()["byteLength"] = JsonNode.Parse("9223372036854775808"),
                     root => root["observations"]!.AsArray()[0]!["descriptor"]!.AsObject()["remoteIdentityBefore"]!.AsObject()["byteLength"] = JsonNode.Parse("1.0")
                 })
        {
            var production = RosterFiles(false); var hostile = RehashedManifest(production, change, true);
            await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(hostile)).Throws<InvalidDataException>();
            InvalidDataException? productionFailure = null;
            try { _ = await ContextSourceBundleHandoff.LoadProductionAsync(HandoffReady(Outer(production)), new StaticArtifactStore(ReplaceManifest(Entries(production), hostile))); }
            catch (InvalidDataException exception) { productionFailure = exception; }
            await Assert.That(productionFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");

            var development = RosterFiles(true); var developmentHostile = RehashedManifest(development, change, true);
            var root = ContextSourceBundleHandoff.CreateDevelopmentDirectory(development.Bundle.Cycle); ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle);
            try
            {
                await ContextSourceBundleHandoff.WriteDevelopmentAsync(development); await File.WriteAllBytesAsync(Path.Combine(root, "manifest.json"), developmentHostile);
                InvalidDataException? developmentFailure = null;
                try { _ = ContextSourceBundleHandoff.LoadDevelopment(HandoffReady(Outer(development)), development.Digest); }
                catch (InvalidDataException exception) { developmentFailure = exception; }
                await Assert.That(developmentFailure!.Message).IsEqualTo("HANDOFF_ARTIFACT_CONFLICT");
            }
            finally { ContextSourceBundleHandoff.CleanupDevelopment(development.Bundle.Cycle); }
        }
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
    public async Task Store_failures_remain_visible_and_do_not_be_reclassified_as_artifact_conflicts()
    {
        var files = Files(development: false); var requested = Outer(files); var repository = new ReservationRepository(requested);
        var uploadFailure = new IOException("simulated upload failure");
        await Assert.That(() => ContextSourceBundleHandoff.ReserveUploadAndMakeReadyAsync(repository, requested, files,
            new ThrowingArtifactStore(ContextSourceArtifactProbeDisposition.Absent, uploadFailure))).Throws<IOException>();
        await Assert.That((await repository.GetCycleAsync(requested.Identity))!.Status).IsEqualTo(BundesligaContextSourceCycleStatus.UploadReserved);

        var persisted = Outer(files) with
        {
            Status = BundesligaContextSourceCycleStatus.HandoffReady,
            BundleSha256 = files.Digest,
            ArtifactName = $"bundesliga-context-source-bundle-{files.Bundle.Cycle.StorageId}"
        };
        var probeFailure = new InvalidOperationException("simulated probe programming failure");
        await Assert.That(() => ContextSourceBundleHandoff.LoadProductionAsync(persisted,
            new ThrowingArtifactStore(ContextSourceArtifactProbeDisposition.Present, probeFailure))).Throws<InvalidOperationException>();
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
    private static ContextSourceBundleFiles HtmlFiles(bool development)
    {
        var cycle = development ? BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000003") : BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 3);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); var bytes = System.Text.Encoding.UTF8.GetBytes("<html>Club Elo</html>");
        var mapping = new[]
        {
            ("b04", "/Leverkusen", "Leverkusen"), ("bmg", "/Gladbach", "Gladbach"), ("bvb", "/Dortmund", "Dortmund"), ("fca", "/Augsburg", "Augsburg"),
            ("fcb", "/Bayern", "Bayern München"), ("fck", "/Koeln", "Köln"), ("fcu", "/UnionBerlin", "Union Berlin"), ("hsv", "/Hamburg", "Hamburg"),
            ("m05", "/Mainz", "Mainz"), ("rbl", "/RBLeipzig", "RB Leipzig"), ("s04", "/Schalke", "Schalke"), ("scf", "/Freiburg", "Freiburg"),
            ("scp", "/Paderborn", "Paderborn"), ("sge", "/Frankfurt", "Frankfurt"), ("sve", "/Elversberg", "Elversberg"), ("svw", "/Werder", "Werder"),
            ("tsg", "/Hoffenheim", "Hoffenheim"), ("vfb", "/Stuttgart", "Stuttgart")
        };
        var descriptor = JsonSerializer.Serialize(new
        {
            contract = "club-elo-official-html-descriptor/v1", sourceUrl = "https://clubelo.com/GER",
            response = new { statusCode = 200, finalUrl = "https://clubelo.com/GER", redirectCount = 0, redirectLocation = (string?)null, mediaType = "text/html", charset = "utf-8", contentEncodings = Array.Empty<string>(), declaredContentLength = (long)bytes.Length },
            rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length,
            parserContract = "club-elo-official-html-parser/v1", displayedDate = "2026-09-04",
            providerDateEvidence = new { kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1", field = "h1>a[href]", rawValue = "2026-09-04", ratedAt = "2026-09-04" },
            tableContract = "club-elo-official-html-table/v1", tableHeader = new[] { "Club", "Elo", "+/-", "Golo" },
            nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1", nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256,
            sourceRows = mapping.Select((entry, index) => new { teamSlug = entry.Item1, providerRoute = entry.Item2, providerDisplayName = entry.Item3, globalRank = index + 1, elo = 1500 + index }).ToArray(), evaluation = "Eligible"
        });
        var payload = new BundesligaContextSourcePayload("club-elo/source.html", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes));
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, payload, []);
        var bundle = new BundesligaContextSourceBundle(cycle, now, now, development ? BundesligaContextSourceContract.DevelopmentLane : "pes-squad-context", development ? BundesligaContextSourceContract.DevelopmentConsumers : BundesligaContextSourceContract.ProductionConsumers, [observation]);
        return new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]> { [payload.Path] = bytes });
    }
    private static ContextSourceBundleFiles CsvFiles(bool development)
    {
        var cycle = development ? BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000005") : BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 5);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero); var bytes = System.Text.Encoding.UTF8.GetBytes("csv");
        var rows = BundesligaTeamManifest.Default.Entries.Select((entry, index) => new { teamSlug = entry.TeamSlug, providerName = $"Team {index + 1:00}", globalRank = index + 1, elo = 1500 + index }).ToArray();
        var descriptor = JsonSerializer.Serialize(new { contract = "club-elo-direct-csv-descriptor/v1", sourceUrl = "https://example.test/elo.csv", rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length, csvHeader = "Rank,Club,Country,Level,Elo,From,To", providerRatedAt = "2026-09-04", providerDateEvidence = new { kind = "ProviderCsvField", recipeId = "recipe/v1", field = "From", rawValue = "2026-09-04", ratedAt = "2026-09-04" }, nameMappingContract = "map/v1", nameMappingSha256 = new string('a', 64), sourceRows = rows, evaluation = "Eligible" });
        var payload = new BundesligaContextSourcePayload("club-elo/source.csv", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes));
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, payload, []);
        var bundle = new BundesligaContextSourceBundle(cycle, now, now, development ? BundesligaContextSourceContract.DevelopmentLane : "pes-squad-context", development ? BundesligaContextSourceContract.DevelopmentConsumers : BundesligaContextSourceContract.ProductionConsumers, [observation]);
        return new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]> { [payload.Path] = bytes });
    }
    private static ContextSourceBundleFiles RosterFiles(bool development)
    {
        var cycle = development ? BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000006") : BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 6);
        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var descriptor = $"{{\"contract\":\"transfermarkt-duckdb-observation-descriptor/v1\",\"metadataUrl\":\"{BundesligaContextSourceDescriptorContract.RosterMetadataUrl}\",\"artifactUrl\":\"{BundesligaContextSourceDescriptorContract.RosterArtifactUrl}\",\"advertisedRevision\":\"{new string('a', 40)}\",\"metadataSha256\":\"{new string('b', 64)}\",\"metadataByteLength\":1,\"remoteIdentityBefore\":{{\"etag\":\"x\",\"byteLength\":1}},\"acquisitionReason\":\"NewRevision\",\"remoteIdentityAfter\":{{\"etag\":\"x\",\"byteLength\":1}},\"embeddedRevision\":\"{new string('a', 40)}\",\"rawSha256\":\"{new string('c', 64)}\",\"expectedRawSha256\":null,\"rawByteLength\":1,\"artifactCaptureDate\":null,\"membershipEffectiveDate\":null,\"enrichmentCaptureDate\":null,\"policySha256\":\"{BundesligaContextSourceDescriptorContract.RosterPolicySha256}\",\"retainedDescriptorSha256\":null,\"retainedEvaluation\":null,\"retainedDiagnostics\":[],\"evaluation\":\"SourceDateRejected\"}}";
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.Rosters), now, BundesligaContextSourceDisposition.Rejected, descriptor, null, ["UNKNOWN_SOURCE_DATE"]);
        var bundle = new BundesligaContextSourceBundle(cycle, now, now, development ? BundesligaContextSourceContract.DevelopmentLane : "pes-squad-context", development ? BundesligaContextSourceContract.DevelopmentConsumers : BundesligaContextSourceContract.ProductionConsumers, [observation]);
        return new ContextSourceBundleFiles(bundle, new Dictionary<string, byte[]>());
    }
    private static ContextSourceArtifactEntry[] Entries(ContextSourceBundleFiles files) => [new("manifest.json", files.Bundle.CreateManifestUtf8()), new("bundle.sha256", System.Text.Encoding.ASCII.GetBytes(files.Digest + "\n"))].Concat(files.Payloads.Select(pair => new ContextSourceArtifactEntry(pair.Key, pair.Value))).ToArray();
    private static BundesligaContextSourceOuterCycle Outer(ContextSourceBundleFiles files) => new(files.Bundle.Cycle, files.Bundle.StartedAtUtc, files.Bundle.StalenessReferenceAtUtc, files.Bundle.ProducerLaneId, files.Bundle.ExpectedConsumers, files.Bundle.Observations.Select(value => value.Source).ToArray(), BundesligaContextSourceCycleStatus.BundleVerified, files.Digest);
    private static BundesligaContextSourceOuterCycle HandoffReady(BundesligaContextSourceOuterCycle cycle) => cycle with { Status = BundesligaContextSourceCycleStatus.HandoffReady, ArtifactName = $"bundesliga-context-source-bundle-{cycle.Identity.StorageId}" };
    private static ContextSourceArtifactEntry[] ReplaceManifest(IEnumerable<ContextSourceArtifactEntry> entries, byte[] manifest) => entries.Select(entry => entry.Path == "manifest.json" ? entry with { Bytes = manifest } : entry).ToArray();
    private sealed record TypedManifestHostile(
        string Name,
        Func<bool, ContextSourceBundleFiles> Fixture,
        Action<JsonObject>? Change = null,
        bool RehashDescriptor = false,
        Func<string, string>? DescriptorRewrite = null,
        Func<string, string>? Rewrite = null);

    private static JsonObject Observation(JsonObject root)
    {
        if (root["observations"] is not JsonArray { Count: > 0 } observations || observations[0] is not JsonObject observation)
            throw new InvalidOperationException("Hostile fixture must begin with an observations array containing an object.");
        return observation;
    }

    private static JsonObject Descriptor(JsonObject root)
    {
        var descriptor = Observation(root)["descriptor"] as JsonObject;
        return descriptor ?? throw new InvalidOperationException("Hostile fixture must begin with an object descriptor.");
    }

    private static void ReverseSourceRows(JsonObject descriptor)
    {
        var rows = descriptor["sourceRows"]!.AsArray();
        var reversed = rows.Reverse().Select(value => value!.DeepClone()).ToArray();
        rows.Clear(); foreach (var row in reversed) rows.Add(row);
    }

    private static void DuplicateSourceRow(JsonObject descriptor) => descriptor["sourceRows"]!.AsArray().Add(descriptor["sourceRows"]!.AsArray()[0]!.DeepClone());

    private static string MoveRootScopeToFront(string json)
    {
        using var document = JsonDocument.Parse(json);
        var scope = document.RootElement.GetProperty("scope").GetRawText();
        var property = $"\"scope\":{scope},";
        if (!json.Contains(property, StringComparison.Ordinal)) throw new InvalidOperationException("Hostile fixture must retain the root scope property.");
        return "{" + property + json[1..].Replace(property, string.Empty, StringComparison.Ordinal);
    }

    private static string ReplaceFirst(string value, string oldValue, string newValue)
    {
        var index = value.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0 ? throw new InvalidOperationException($"Hostile fixture must contain '{oldValue}'.")
            : value[..index] + newValue + value[(index + oldValue.Length)..];
    }

    private static byte[] RehashedManifest(ContextSourceBundleFiles files, Action<JsonObject>? mutate, bool rehashDescriptor = false, Func<string, string>? descriptorRewrite = null, Func<string, string>? rewrite = null)
    {
        var root = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(files.Bundle.CreateManifestUtf8()))!.AsObject();
        var observation = Observation(root); // Verify the fixture before hostile mutation can replace its container or object.
        mutate?.Invoke(root);
        var descriptor = observation["descriptor"] as JsonObject;
        var rawDescriptor = descriptor?.ToJsonString();
        if (rehashDescriptor || descriptorRewrite is not null)
        {
            if (rawDescriptor is null) throw new InvalidOperationException("A descriptor-rehashed hostile must retain an object descriptor.");
            rawDescriptor = descriptorRewrite?.Invoke(rawDescriptor) ?? rawDescriptor;
            observation["descriptorSha256"] = BundesligaContextSourceHashing.Sha256(System.Text.Encoding.UTF8.GetBytes(rawDescriptor));
        }
        var json = root.ToJsonString();
        if (descriptorRewrite is not null)
            json = ReplaceFirst(json, descriptor!.ToJsonString(), rawDescriptor!);
        return System.Text.Encoding.UTF8.GetBytes(rewrite?.Invoke(json) ?? json);
    }

    private sealed class MemoryArtifactStore : IContextSourceBundleArtifactStore
    {
        private IReadOnlyList<ContextSourceArtifactEntry>? _entries; public int Uploads { get; private set; } public bool LastOverwrite { get; private set; } public int LastCompression { get; private set; } public int LastRetention { get; private set; }
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default) => Task.FromResult(new ContextSourceArtifactProbe(_entries is null ? ContextSourceArtifactProbeDisposition.Absent : ContextSourceArtifactProbeDisposition.Present, _entries ?? []));
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default) { Uploads++; LastOverwrite = overwrite; LastCompression = compressionLevel; LastRetention = retentionDays; _entries = entries; return Task.CompletedTask; }
    }

    private sealed class StaticArtifactStore(IReadOnlyList<ContextSourceArtifactEntry> entries) : IContextSourceBundleArtifactStore
    {
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContextSourceArtifactProbe(ContextSourceArtifactProbeDisposition.Present, entries));
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> uploadEntries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Upload should not be called.");
    }

    private sealed class ThrowingArtifactStore(ContextSourceArtifactProbeDisposition disposition, Exception failure) : IContextSourceBundleArtifactStore
    {
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName, CancellationToken cancellationToken = default)
            => disposition == ContextSourceArtifactProbeDisposition.Absent
                ? Task.FromResult(new ContextSourceArtifactProbe(disposition, []))
                : Task.FromException<ContextSourceArtifactProbe>(failure);
        public Task UploadAsync(string artifactName, IReadOnlyList<ContextSourceArtifactEntry> entries, bool overwrite, int compressionLevel, int retentionDays, CancellationToken cancellationToken = default)
            => Task.FromException(failure);
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
