using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaContextSourceBundleContractTests
{
    [Test]
    public async Task Descriptor_observation_and_bundle_digests_are_stable_and_parse_round_trip()
    {
        var bundle = CreateRejectedBundle(); var manifest = bundle.CreateManifestUtf8();
        var digest = bundle.BundleSha256(new Dictionary<string, byte[]>());
        var parsed = BundesligaContextSourceBundle.ParseManifest(manifest);
        await Assert.That(parsed.CreateManifestUtf8()).IsEquivalentTo(manifest);
        await Assert.That(bundle.Observations[0].DescriptorSha256).IsEqualTo("369db2302ffa209c306728a06efe97d830fe7fd7d01a8a94e748e42b782fe6df");
        await Assert.That(bundle.Observations[0].ObservationDigest).IsEqualTo("cb49fadbd1b368ce971f47b321ca6ca5cbe346d74af72c82a012bde5c6b913c2");
        await Assert.That(digest).IsEqualTo("3933d8deff1a1eba379efd533a5f4d2f06a3c9d471884f3139159a055a71c75e");
    }

    [Test]
    public async Task Canonical_descriptor_rejects_reorder_unknown_missing_and_type_coercion()
    {
        var descriptor = RejectedEloDescriptor();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"contract\":", "\"unknown\":null,\"contract\":"), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"rawByteLength\":null,", string.Empty), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"sourceUrl\"", "\"sourceUrl\""), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"rawByteLength\":null", "\"rawByteLength\":\"0\""), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Rejected_Club_Elo_strictly_validates_and_round_trips_every_non_null_nested_structure()
    {
        var descriptor = JsonSerializer.Serialize(new
        {
            contract = "club-elo-direct-csv-descriptor/v1", sourceUrl = "https://example.test/elo.csv", rawSha256 = (string?)null, rawByteLength = (long?)null,
            csvHeader = "Rank,Club,Country,Level,Elo,From,To", providerRatedAt = (string?)null,
            providerDateEvidence = new { kind = "AcceptedDailyEndpoint", recipeId = "daily/v1", field = (string?)null, rawValue = "2026-09-04", ratedAt = "2026-09-04" },
            nameMappingContract = "map/v1", nameMappingSha256 = Sha('b'),
            sourceRows = new[] { new { teamSlug = "team-01", providerName = "Team 01", globalRank = 1, elo = 1500d } },
            evaluation = "CoverageRejected"
        });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, BundesligaContextSourceDisposition.Rejected);

        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2);
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), Utc(), BundesligaContextSourceDisposition.Rejected, descriptor, null, ["INCOMPLETE_COVERAGE"]);
        var parsed = BundesligaContextSourceDescriptorContract.ParseObservation(Encoding.UTF8.GetString(observation.CreateCanonicalUtf8()));
        await Assert.That(parsed.DescriptorJson).IsEqualTo(descriptor);

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"ratedAt\":\"2026-09-04\"", "\"ratedAt\":1"), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"globalRank\":1", "\"globalRank\":\"1\""), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"teamSlug\":\"team-01\"", "\"unknown\":null,\"teamSlug\":\"team-01\""), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Payload_hash_and_length_are_recomputed_from_bytes()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2); var now = Utc(); var payload = Encoding.UTF8.GetBytes("abc");
        var descriptor = RejectedEloDescriptor().Replace("TransportRejected", "Eligible").Replace("\"rawSha256\":null", $"\"rawSha256\":\"{BundesligaContextSourceHashing.Sha256(payload)}\"");
        await Assert.That(() => new BundesligaContextSourcePayload("club-elo/source.csv", payload.Length, BundesligaContextSourceHashing.Sha256(payload)).Validate(BundesligaContextSource.Rosters)).Throws<InvalidDataException>();
        var rejected = CreateRejectedBundle();
        await Assert.That(() => rejected.BundleSha256(new Dictionary<string, byte[]> { ["extra"] = payload })).Throws<Exception>();
    }

    [Test]
    public async Task Manifest_rejects_fractional_timestamp_and_noncanonical_bytes()
    {
        var bytes = CreateRejectedBundle().CreateManifestUtf8(); var json = Encoding.UTF8.GetString(bytes);
        await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(Encoding.UTF8.GetBytes(json.Replace("12:00:00Z", "12:00:00.000Z")))).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(Encoding.UTF8.GetBytes(json + "\n"))).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(Encoding.UTF8.GetBytes(json.Replace("\"contract\":", "\"extra\":0,\"contract\":")))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Observation_rejects_numeric_or_undefined_disposition_before_descriptor_validation()
    {
        var observation = CreateRejectedBundle().Observations[0];
        await Assert.That(() => (observation with { Disposition = (BundesligaContextSourceDisposition)999 }).Validate()).Throws<InvalidDataException>();

        var json = Encoding.UTF8.GetString(observation.CreateCanonicalUtf8());
        await Assert.That(() => BundesligaContextSourceDescriptorContract.ParseObservation(json.Replace("\"disposition\":\"Rejected\"", "\"disposition\":\"999\""))).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.ParseObservation(json.Replace("\"disposition\":\"Rejected\"", "\"disposition\":\"2\""))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Roster_descriptor_enforces_every_evaluation_null_required_and_equality_matrix()
    {
        await Assert.That(BundesligaContextSourceDescriptorContract.RosterPolicySha256)
            .IsEqualTo("56ce2f0543b91a59b63fbec7889f1bf547681e90f58da7c419028fd749285d9b");
        foreach (var evaluation in new[] { "MetadataUnavailable", "MetadataMalformed", "MetadataRevisionRejected", "MetadataUnchanged", "RemoteIdentityUnavailable", "ArtifactTransportRejected", "SizeRejected", "RemoteDriftRejected", "HashRejected", "RevisionRejected", "SchemaRejected", "SourceDateRejected", "SeasonRejected", "IdentityRejected", "Eligible" })
        {
            var disposition = evaluation switch { "MetadataUnchanged" => BundesligaContextSourceDisposition.MetadataUnchanged, "Eligible" => BundesligaContextSourceDisposition.ArtifactCaptured, _ => BundesligaContextSourceDisposition.Rejected };
            BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters, RosterDescriptor(evaluation), disposition);
        }

        var invalid = new[]
        {
            RosterDescriptor("MetadataUnchanged", ("acquisitionReason", Json("NewRevision"))),
            RosterDescriptor("ArtifactTransportRejected", ("metadataSha256", "null")),
            RosterDescriptor("SizeRejected", ("rawSha256", Json(Sha('c')))),
            RosterDescriptor("RemoteDriftRejected", ("remoteIdentityAfter", Remote("x"))),
            RosterDescriptor("HashRejected", ("expectedRawSha256", Json(Sha('c')))),
            RosterDescriptor("RevisionRejected", ("embeddedRevision", Json(Revision('a')))),
            RosterDescriptor("SchemaRejected", ("embeddedRevision", Json(Revision('f')))),
            RosterDescriptor("SourceDateRejected", ("artifactCaptureDate", Json("2026-09-01")), ("membershipEffectiveDate", Json("2026-09-01")), ("enrichmentCaptureDate", Json("2026-09-01"))),
            RosterDescriptor("SeasonRejected", ("membershipEffectiveDate", "null")),
            RosterDescriptor("IdentityRejected", ("enrichmentCaptureDate", "null")),
            RosterDescriptor("Eligible", ("expectedRawSha256", Json(Sha('e')))),
            RosterDescriptor("SourceDateRejected", ("policySha256", Json(Sha('d'))))
        };
        foreach (var descriptor in invalid)
            await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters, descriptor, descriptor.Contains("\"evaluation\":\"Eligible\"") ? BundesligaContextSourceDisposition.ArtifactCaptured : descriptor.Contains("\"evaluation\":\"MetadataUnchanged\"") ? BundesligaContextSourceDisposition.MetadataUnchanged : BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters, RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", Remote("")), ("remoteIdentityAfter", Remote(""))), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters, RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", Remote(" ")), ("remoteIdentityAfter", Remote(" "))), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Roster_descriptor_requires_the_two_ordinal_urls_and_strict_transport_facts()
    {
        var valid = RosterDescriptor("Eligible");
        foreach (var hostile in new[]
                 {
                     "http://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb",
                     "https://PUB-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb",
                     BundesligaContextSourceDescriptorContract.RosterArtifactUrl + "/",
                     BundesligaContextSourceDescriptorContract.RosterArtifactUrl + "?x=1",
                     "https://user@pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb"
                 })
            await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
                valid.Replace(BundesligaContextSourceDescriptorContract.RosterArtifactUrl, hostile), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("SizeRejected", ("rawByteLength", BundesligaContextSourceDescriptorContract.MaximumRosterArtifactBytes.ToString())), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("RemoteDriftRejected", ("rawSha256", "null")), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("RemoteIdentityUnavailable", ("acquisitionReason", Json("RemoteIdentityChanged"))), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Roster_diagnostics_use_one_nonlexical_evaluation_precedence_for_observed_and_retained_evidence()
    {
        var valid = new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "UNKNOWN_SOURCE_DATE", "ROSTER_MEMBERSHIP_REJECTED" };
        BundesligaContextSourceDescriptorContract.ValidateRosterEvaluationPrecedence(
            "SchemaRejected", BundesligaContextSourceDisposition.Rejected, valid);
        var retained = new BundesligaContextSourceRetainedRosterDescriptor(
            Sha('a'), Revision('a'), new BundesligaContextSourceRemoteIdentity("etag", 1), Sha('b'),
            "SchemaRejected", valid);
        retained.Validate();

        foreach (var hostile in new[]
                 {
                     new[] { "UNKNOWN_SOURCE_DATE", "ROSTER_DUCKDB_SCHEMA_REJECTED" },
                     new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "ROSTER_DUCKDB_SCHEMA_REJECTED" },
                     new[] { "ROSTER_MEMBERSHIP_REJECTED" },
                     new[] { "ROSTER_DUCKDB_SCHEMA_REJECTED", "UNKNOWN_CODE" }
                 })
            await Assert.That(() => BundesligaContextSourceDescriptorContract.ValidateRosterEvaluationPrecedence(
                "SchemaRejected", BundesligaContextSourceDisposition.Rejected, hostile)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Bundle_validation_revalidates_its_directly_constructed_cycle_identity()
    {
        var valid = CreateRejectedBundle();
        var invalidIdentity = new BundesligaContextSourceCycleIdentity("not-the-competition", valid.Cycle.Scope, valid.Cycle.CycleId, valid.Cycle.Sequence);
        var observation = valid.Observations[0] with { AttemptId = BundesligaContextSourceHashing.AttemptId(invalidIdentity, BundesligaContextSource.ClubElo) };
        var invalid = valid with { Cycle = invalidIdentity, Observations = [observation] };
        await Assert.That(() => invalid.Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Eligible_descriptor_raw_hash_and_length_are_bound_to_the_observation_payload()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000003");
        var bytes = Encoding.UTF8.GetBytes("payload");
        var descriptor = RosterDescriptor("Eligible", ("rawSha256", Json(BundesligaContextSourceHashing.Sha256(bytes))), ("rawByteLength", bytes.LongLength.ToString()));
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.Rosters, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.Rosters), Utc(), BundesligaContextSourceDisposition.ArtifactCaptured, descriptor, new BundesligaContextSourcePayload("rosters/source.duckdb", bytes.LongLength, BundesligaContextSourceHashing.Sha256(bytes)), []);
        observation.Validate();
        await Assert.That(() => (observation with { Payload = observation.Payload! with { ByteLength = bytes.LongLength + 1 } }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => (observation with { Payload = observation.Payload! with { Sha256 = Sha('f') } }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Eligible_Club_Elo_requires_positive_finite_Elo_values()
    {
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(1500), BundesligaContextSourceDisposition.ArtifactCaptured);
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(1500.25), BundesligaContextSourceDisposition.ArtifactCaptured);
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(0), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(-1), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Club_Elo_descriptor_rejects_numeric_spellings_that_change_during_Firestore_round_trip()
    {
        var descriptor = EligibleEloDescriptor(1500);

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"rawByteLength\":1", "\"rawByteLength\":1.0"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"globalRank\":1,", "\"globalRank\":1e0,"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor.Replace("\"elo\":1500", "\"elo\":1500.0"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Eligible_Club_Elo_requires_the_exact_ordered_manifest_slug_set()
    {
        var slugs = BundesligaTeamManifest.Default.Entries.Select(entry => entry.TeamSlug).ToArray();
        var wrongOrder = slugs.ToArray();
        (wrongOrder[0], wrongOrder[1]) = (wrongOrder[1], wrongOrder[0]);
        var substituted = slugs.ToArray();
        substituted[0] = "aaa";

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(1500, wrongOrder), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(1500, slugs[..^1]), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, EligibleEloDescriptor(1500, substituted), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
    }

    private static BundesligaContextSourceBundle CreateRejectedBundle()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Production(BundesligaContextSourceContract.Competition, 1, 2); var now = Utc();
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), now, BundesligaContextSourceDisposition.Rejected, RejectedEloDescriptor(), null, ["UNKNOWN_SOURCE_DATE"]);
        return new BundesligaContextSourceBundle(cycle, now, now, "pes-squad-context", BundesligaContextSourceContract.ProductionConsumers, [observation]);
    }
    private static DateTimeOffset Utc() => new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    internal static string RejectedEloDescriptor() => "{\"contract\":\"club-elo-direct-csv-descriptor/v1\",\"sourceUrl\":\"https://example.test/elo.csv\",\"rawSha256\":null,\"rawByteLength\":null,\"csvHeader\":null,\"providerRatedAt\":null,\"providerDateEvidence\":null,\"nameMappingContract\":null,\"nameMappingSha256\":null,\"sourceRows\":null,\"evaluation\":\"TransportRejected\"}";

    private static string EligibleEloDescriptor(double elo, IReadOnlyList<string>? teamSlugs = null)
    {
        teamSlugs ??= BundesligaTeamManifest.Default.Entries.Select(entry => entry.TeamSlug).ToArray();
        var rows = teamSlugs.Select((teamSlug, index) => new { teamSlug, providerName = $"Team {index + 1:00}", globalRank = index + 1, elo }).ToArray();
        return JsonSerializer.Serialize(new { contract = "club-elo-direct-csv-descriptor/v1", sourceUrl = "https://example.test/elo.csv", rawSha256 = Sha('a'), rawByteLength = 1, csvHeader = "Rank,Club,Country,Level,Elo,From,To", providerRatedAt = "2026-09-04", providerDateEvidence = new { kind = "ProviderCsvField", recipeId = "recipe/v1", field = "From", rawValue = "2026-09-04", ratedAt = "2026-09-04" }, nameMappingContract = "map/v1", nameMappingSha256 = Sha('b'), sourceRows = rows, evaluation = "Eligible" });
    }

    private static string RosterDescriptor(string evaluation, params (string Name, string JsonValue)[] overrides)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contract"] = Json("transfermarkt-duckdb-observation-descriptor/v1"), ["metadataUrl"] = Json(BundesligaContextSourceDescriptorContract.RosterMetadataUrl), ["artifactUrl"] = Json(BundesligaContextSourceDescriptorContract.RosterArtifactUrl), ["advertisedRevision"] = Json(Revision('a')),
            ["metadataSha256"] = "null", ["metadataByteLength"] = "null", ["remoteIdentityBefore"] = "null", ["acquisitionReason"] = Json("NewRevision"), ["remoteIdentityAfter"] = "null", ["embeddedRevision"] = "null", ["rawSha256"] = "null", ["expectedRawSha256"] = "null", ["rawByteLength"] = "null", ["artifactCaptureDate"] = "null", ["membershipEffectiveDate"] = "null", ["enrichmentCaptureDate"] = "null", ["policySha256"] = Json(BundesligaContextSourceDescriptorContract.RosterPolicySha256), ["retainedDescriptorSha256"] = "null", ["retainedEvaluation"] = "null", ["retainedDiagnostics"] = "[]", ["evaluation"] = Json(evaluation)
        };
        void Metadata() { fields["metadataSha256"] = Json(Sha('b')); fields["metadataByteLength"] = "1"; fields["remoteIdentityBefore"] = Remote("x"); }
        void StableArtifact(bool dates) { Metadata(); fields["remoteIdentityAfter"] = Remote("x"); fields["embeddedRevision"] = Json(Revision('a')); fields["rawSha256"] = Json(Sha('c')); fields["rawByteLength"] = "7"; if (dates) { fields["artifactCaptureDate"] = Json("2026-09-01"); fields["membershipEffectiveDate"] = Json("2026-09-01"); fields["enrichmentCaptureDate"] = Json("2026-09-01"); } }
        switch (evaluation)
        {
            case "MetadataUnchanged": Metadata(); fields["acquisitionReason"] = Json("AcceptedRevisionUnchanged"); fields["retainedDescriptorSha256"] = Json(Sha('e')); fields["retainedEvaluation"] = Json("Eligible"); break;
            case "MetadataUnavailable": fields["advertisedRevision"] = "null"; fields["acquisitionReason"] = "null"; break;
            case "MetadataMalformed": case "MetadataRevisionRejected": fields["advertisedRevision"] = "null"; fields["acquisitionReason"] = "null"; fields["metadataSha256"] = Json(Sha('b')); fields["metadataByteLength"] = "1"; break;
            case "RemoteIdentityUnavailable": Metadata(); fields["remoteIdentityBefore"] = "null"; fields["acquisitionReason"] = "null"; break;
            case "ArtifactTransportRejected": Metadata(); break;
            case "SizeRejected": Metadata(); fields["rawByteLength"] = "314572801"; break;
            case "RemoteDriftRejected": Metadata(); fields["remoteIdentityAfter"] = Remote("y"); fields["rawSha256"] = Json(Sha('c')); fields["rawByteLength"] = "7"; break;
            case "HashRejected": Metadata(); fields["remoteIdentityAfter"] = Remote("x"); fields["embeddedRevision"] = Json(Revision('a')); fields["rawSha256"] = Json(Sha('c')); fields["expectedRawSha256"] = Json(Sha('e')); fields["rawByteLength"] = "7"; break;
            case "RevisionRejected": Metadata(); fields["remoteIdentityAfter"] = Remote("x"); fields["embeddedRevision"] = Json(Revision('f')); fields["rawSha256"] = Json(Sha('c')); fields["rawByteLength"] = "7"; break;
            case "SchemaRejected": StableArtifact(false); break;
            case "SourceDateRejected": StableArtifact(false); fields["artifactCaptureDate"] = Json("2026-09-01"); break;
            case "SeasonRejected": case "IdentityRejected": case "Eligible": StableArtifact(true); break;
        }
        foreach (var (name, value) in overrides) fields[name] = value;
        return "{" + string.Join(',', fields.Select(pair => Json(pair.Key) + ":" + pair.Value)) + "}";
    }
    private static string Remote(string etag) => $"{{\"etag\":{Json(etag)},\"byteLength\":1}}";
    private static string Revision(char value) => new(value, 40);
    private static string Sha(char value) => new(value, 64);
    private static string Json(string value) => JsonSerializer.Serialize(value);
}
