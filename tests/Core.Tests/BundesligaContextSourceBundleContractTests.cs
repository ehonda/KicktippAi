using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task Manifest_and_observation_type_guards_return_artifact_data_errors()
    {
        foreach (var scalar in new[] { "null", "[]", "\"manifest\"" })
            await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(Encoding.UTF8.GetBytes(scalar))).Throws<InvalidDataException>();

        var manifest = JsonNode.Parse(Encoding.UTF8.GetString(CreateRejectedBundle().CreateManifestUtf8()))!.AsObject();
        foreach (var mutate in new Action<JsonObject>[]
                 {
                     root => root["competition"] = null,
                     root => root["producerLaneId"] = 1,
                     root => root["cycleSequence"] = "2",
                     root => root["cycleSequence"] = JsonNode.Parse("2.0"),
                     root => root["cycleSequence"] = JsonNode.Parse("9223372036854775808"),
                     root => root["startedAtUtc"] = "not-a-date",
                     root => root["expectedConsumers"] = new JsonObject(),
                     root => root["observations"] = JsonNode.Parse("[1]")!.AsArray()
                 })
        {
            var hostile = manifest.DeepClone().AsObject(); mutate(hostile);
            await Assert.That(() => BundesligaContextSourceBundle.ParseManifest(Encoding.UTF8.GetBytes(hostile.ToJsonString()))).Throws<InvalidDataException>();
        }

        var observationJson = JsonNode.Parse(Encoding.UTF8.GetString(CreateRejectedBundle().Observations[0].CreateCanonicalUtf8()))!.AsObject();
        foreach (var mutate in new Action<JsonObject>[]
                 {
                     root => root["descriptor"] = null,
                     root => root["payload"] = new JsonArray(),
                     root => root["diagnostics"] = new JsonObject()
                 })
        {
            var hostile = observationJson.DeepClone().AsObject(); mutate(hostile);
            await Assert.That(() => BundesligaContextSourceDescriptorContract.ParseObservation(hostile.ToJsonString())).Throws<InvalidDataException>();
        }

        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, "[]", BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", "{\"etag\":\"x\",\"byteLength\":\"1\"}")), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", "{\"etag\":\"x\",\"byteLength\":1.0}")), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", "[]")), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.Rosters,
            RosterDescriptor("SourceDateRejected", ("remoteIdentityBefore", "{\"etag\":\"x\",\"byteLength\":9223372036854775808}")), BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Csv_evidence_and_elo_numbers_are_typed_without_losing_exact_int64_tokens()
    {
        var slugs = BundesligaTeamManifest.Default.Entries.Select(entry => entry.TeamSlug).ToArray();
        var eligible = EligibleEloDescriptor(1500, slugs);
        var missingEvidence = JsonNode.Parse(eligible)!.AsObject();
        missingEvidence["providerDateEvidence"] = null;
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo,
            missingEvidence.ToJsonString(), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();

        var exactInt64 = MutateElo(eligible, row => row["elo"] = JsonNode.Parse("9007199254740993"));
        var fractional = MutateElo(eligible, row => row["elo"] = JsonNode.Parse("1500.5"));
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, exactInt64, BundesligaContextSourceDisposition.ArtifactCaptured);
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, fractional, BundesligaContextSourceDisposition.ArtifactCaptured);
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo,
            MutateElo(eligible, row => row["elo"] = JsonNode.Parse("9007199254740993.0")), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
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

    [Test]
    public async Task Html_descriptor_uses_its_exact_root_order_payload_identity_and_displayed_date()
    {
        var cycle = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000004");
        var bytes = Encoding.UTF8.GetBytes("abc"); var descriptor = EligibleHtmlEloDescriptor(bytes);
        var observation = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo,
            BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), Utc(),
            BundesligaContextSourceDisposition.ArtifactCaptured, descriptor,
            new BundesligaContextSourcePayload("club-elo/source.html", bytes.Length, BundesligaContextSourceHashing.Sha256(bytes)), []);

        observation.Validate();
        await Assert.That(BundesligaContextSourceDescriptorContract.ClubEloRatedAt(descriptor)).IsEqualTo(new DateOnly(2026, 9, 4));
        await Assert.That(() => (observation with { Payload = observation.Payload! with { Path = "club-elo/source.csv" } }).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo,
            descriptor.Replace("\"response\":", "\"rawSha256\":null,\"response\":"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_descriptor_rejects_non_integer_response_and_row_values_and_wrong_evaluation_diagnostic()
    {
        var descriptor = EligibleHtmlEloDescriptor(Encoding.UTF8.GetBytes("abc"));
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo,
            descriptor.Replace("\"statusCode\":200", "\"statusCode\":200.0"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo,
            descriptor.Replace("\"elo\":1500", "\"elo\":1500.0"), BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaContextSourceDescriptorContract.ValidateClubEloDiagnostics(
            descriptor.Replace("\"evaluation\":\"Eligible\"", "\"evaluation\":\"DateRejected\""),
            BundesligaContextSourceDisposition.Rejected, ["CLUB_ELO_MAPPING_REJECTED"])).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_descriptor_exercises_all_size_forms_and_response_gate_boundaries()
    {
        var body = Encoding.UTF8.GetBytes("abc");
        foreach (var declaredLength in new long?[] { 2097153, null, 0, body.Length })
        {
            var descriptor = HtmlDescriptor(body, root =>
            {
                root["evaluation"] = "SizeRejected"; root["rawSha256"] = null; root["rawByteLength"] = null;
                root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
                root["response"]!.AsObject()["declaredContentLength"] = declaredLength;
            });
            BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, BundesligaContextSourceDisposition.Rejected);
        }
        var empty = HtmlDescriptor([], root => { root["evaluation"] = "SizeRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, empty, BundesligaContextSourceDisposition.Rejected);
        var mismatch = HtmlDescriptor(body, root => { root["evaluation"] = "SizeRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["declaredContentLength"] = 0; });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, mismatch, BundesligaContextSourceDisposition.Rejected);

        foreach (var invalid in new[]
        {
            HtmlDescriptor(body, root => { root["evaluation"] = "SizeRejected"; root["rawSha256"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "SizeRejected"; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor([], root => { root["evaluation"] = "DomRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "DomRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["charset"] = "UTF-8"; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "MappingRejected"; root["providerDateEvidence"] = null; root["sourceRows"] = null; })
        })
            await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, invalid, BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_descriptor_independently_covers_response_mapping_freshness_and_diagnostics()
    {
        var body = Encoding.UTF8.GetBytes("abc");
        Action<JsonObject>[] responseFailures =
        [ response => response["statusCode"] = 404, response => response["finalUrl"] = "https://clubelo.com/DE", response => response["redirectCount"] = 1,
          response => response["redirectLocation"] = "https://clubelo.com/GER", response => response["mediaType"] = "text/plain", response => response["charset"] = "utf8", response => response["contentEncodings"] = new JsonArray("gzip") ];
        foreach (var failure in responseFailures)
        {
            var descriptor = HtmlDescriptor(body, root => { root["evaluation"] = "ResponseRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; failure(root["response"]!.AsObject()); });
            BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, BundesligaContextSourceDisposition.Rejected);
        }
        foreach (var evaluation in new[] { "DomRejected", "LexerRejected", "FragmentRejected", "DateRejected" })
        {
            var descriptor = HtmlDescriptor(body, root => { root["evaluation"] = evaluation; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; });
            BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, BundesligaContextSourceDisposition.Rejected);
        }
        var mapping = HtmlDescriptor(body, root => { root["evaluation"] = "MappingRejected"; root["sourceRows"] = null; });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, mapping, BundesligaContextSourceDisposition.Rejected);
        var coverage = HtmlDescriptor(body, root => { root["evaluation"] = "CoverageRejected"; root["sourceRows"] = new JsonArray(); });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, coverage, BundesligaContextSourceDisposition.Rejected);

        var boundary = HtmlObservation(HtmlDescriptor(body, root => root["evaluation"] = "NotNewer"), BundesligaContextSourceDisposition.Rejected, ["CLUB_ELO_NOT_NEWER"], new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
        boundary.Validate();
        await Assert.That(() => (boundary with { ObservedAtUtc = boundary.ObservedAtUtc.AddDays(1) }).Validate()).Throws<InvalidDataException>();
        var transport = HtmlObservation(HtmlDescriptor(body, root => { root["evaluation"] = "TransportRejected"; root["response"] = null; root["rawSha256"] = null; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; }), BundesligaContextSourceDisposition.Rejected, ["CLUB_ELO_CONNECTION_FAILED", "CLUB_ELO_TRANSPORT_REJECTED"], boundary.ObservedAtUtc);
        transport.Validate();
        foreach (var diagnostics in new IReadOnlyList<string>[] { ["CLUB_ELO_TRANSPORT_REJECTED", "CLUB_ELO_TIMEOUT", "CLUB_ELO_CONNECTION_FAILED"], ["CLUB_ELO_TRANSPORT_REJECTED", "CLUB_ELO_TRANSPORT_REJECTED"], ["CLUB_ELO_MAPPING_REJECTED"], ["CLUB_ELO_TRANSPORT_REJECTED", "UNKNOWN"] })
            await Assert.That(() => (transport with { Diagnostics = diagnostics }).Validate()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Html_mapping_bytes_are_reproduced_without_a_production_hash_constant()
    {
        var mapping = string.Join("\r\n", new[]
        {
            "teamSlug,providerRoute,providerDisplayName", "b04,/Leverkusen,Leverkusen", "bmg,/Gladbach,Gladbach", "bvb,/Dortmund,Dortmund", "fca,/Augsburg,Augsburg", "fcb,/Bayern,Bayern München", "fck,/Koeln,Köln", "fcu,/UnionBerlin,Union Berlin", "hsv,/Hamburg,Hamburg", "m05,/Mainz,Mainz", "rbl,/RBLeipzig,RB Leipzig", "s04,/Schalke,Schalke", "scf,/Freiburg,Freiburg", "scp,/Paderborn,Paderborn", "sge,/Frankfurt,Frankfurt", "sve,/Elversberg,Elversberg", "svw,/Werder,Werder", "tsg,/Hoffenheim,Hoffenheim", "vfb,/Stuttgart,Stuttgart"
        });
        var bytes = new UTF8Encoding(false, true).GetBytes(mapping.Normalize(NormalizationForm.FormC));
        await Assert.That(bytes.Length).IsEqualTo(487);
        await Assert.That(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()).IsEqualTo("8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7");
        await Assert.That(mapping.EndsWith("\r\n", StringComparison.Ordinal)).IsFalse();

        foreach (var hostile in new Action<JsonObject>[]
        {
            root => root["sourceRows"]!.AsArray()[0]!.AsObject()["providerRoute"] = "/Wrong", root => root["sourceRows"]!.AsArray()[0]!.AsObject()["providerDisplayName"] = "Wrong",
            root => root["sourceRows"]!.AsArray()[1]!.AsObject()["globalRank"] = 1, root => root["sourceRows"]!.AsArray()[0]!.AsObject()["elo"] = 0,
            root => root["sourceRows"]!.AsArray().RemoveAt(17)
        })
        {
            var descriptor = HtmlDescriptor(Encoding.UTF8.GetBytes("abc"), hostile);
            await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, BundesligaContextSourceDisposition.ArtifactCaptured)).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Html_evaluation_descriptor_and_diagnostic_matrix_is_complete_and_payloads_are_eligible_only()
    {
        var body = Encoding.UTF8.GetBytes("abc");
        var cases = new[]
        {
            ("TransportRejected", "CLUB_ELO_TRANSPORT_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("SizeRejected", "CLUB_ELO_SIZE_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("ResponseRejected", "CLUB_ELO_RESPONSE_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("DomRejected", "CLUB_ELO_DOM_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("LexerRejected", "CLUB_ELO_LEXER_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("FragmentRejected", "CLUB_ELO_FRAGMENT_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("DateRejected", "CLUB_ELO_DISPLAYED_DATE_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("MappingRejected", "CLUB_ELO_MAPPING_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("CoverageRejected", "CLUB_ELO_COVERAGE_REJECTED", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero)),
            ("StaleRejected", "CLUB_ELO_STALE_GT_7_DAYS", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero)),
            ("NotNewer", "CLUB_ELO_NOT_NEWER", BundesligaContextSourceDisposition.Rejected, new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero)),
            ("Eligible", "", BundesligaContextSourceDisposition.ArtifactCaptured, new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero))
        };
        foreach (var @case in cases)
        {
            var descriptor = HtmlEvaluationDescriptor(@case.Item1, body);
            BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, descriptor, @case.Item3);
            var observation = HtmlObservation(descriptor, @case.Item3, @case.Item1 == "Eligible" ? [] : [@case.Item2], @case.Item4);
            observation.Validate();
            if (@case.Item1 != "Eligible")
            {
                var wrongDiagnostic = @case.Item2 == "CLUB_ELO_DOM_REJECTED" ? "CLUB_ELO_SIZE_REJECTED" : "CLUB_ELO_DOM_REJECTED";
                await Assert.That(() => (observation with { Diagnostics = [wrongDiagnostic] }).Validate()).Throws<InvalidDataException>();
                await Assert.That(() => (observation with { Payload = new BundesligaContextSourcePayload("club-elo/source.html", body.Length, BundesligaContextSourceHashing.Sha256(body)) }).Validate()).Throws<InvalidDataException>();
            }
        }
    }

    [Test]
    public async Task Html_descriptor_boundary_and_trailing_evidence_hostiles_cover_size_coverage_dates_and_freshness()
    {
        var body = Encoding.UTF8.GetBytes("abc");
        var maximum = HtmlDescriptor(body, root =>
        {
            root["evaluation"] = "DomRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
            root["rawByteLength"] = 2097152; root["response"]!.AsObject()["declaredContentLength"] = 2097152;
        });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, maximum, BundesligaContextSourceDisposition.Rejected);
        var minimum = HtmlDescriptor(body, root =>
        {
            root["evaluation"] = "DomRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
            root["rawByteLength"] = 1; root["response"]!.AsObject()["declaredContentLength"] = 1;
        });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, minimum, BundesligaContextSourceDisposition.Rejected);
        var streamingBoundary = HtmlDescriptor(body, root =>
        {
            root["evaluation"] = "SizeRejected"; root["rawSha256"] = null; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
            root["response"]!.AsObject()["declaredContentLength"] = 2097152;
        });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, streamingBoundary, BundesligaContextSourceDisposition.Rejected);
        var sizeForms = new[]
        {
            HtmlDescriptor(body, root => { root["evaluation"] = "SizeRejected"; root["rawSha256"] = null; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["declaredContentLength"] = 2097153; }),
            streamingBoundary,
            HtmlDescriptor([], root => { root["evaluation"] = "SizeRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "SizeRejected"; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["declaredContentLength"] = 0; })
        };
        foreach (var sizeForm in sizeForms)
        {
            foreach (var trailing in new Action<JsonObject>[] { root => root["displayedDate"] = "2026-09-04", root => root["sourceRows"] = new JsonArray() })
            {
                var hostile = MutateHtmlJson(sizeForm, trailing);
                await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, hostile, BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();
            }
        }

        var nonemptyCoverage = HtmlDescriptor(body, root => { root["evaluation"] = "CoverageRejected"; root["sourceRows"]!.AsArray().RemoveAt(17); });
        BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, nonemptyCoverage, BundesligaContextSourceDisposition.Rejected);
        foreach (var hostile in new[]
        {
            HtmlDescriptor(body, root => root["evaluation"] = "CoverageRejected"),
            HtmlDescriptor(body, root => { root["evaluation"] = "MappingRejected"; root["displayedDate"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "MappingRejected"; root["providerDateEvidence"] = null; root["sourceRows"] = null; }),
            HtmlDescriptor(body, root => { root["evaluation"] = "DateRejected"; root["displayedDate"] = "2026-09-04"; root["providerDateEvidence"] = null; root["sourceRows"] = null; })
        })
            await Assert.That(() => BundesligaContextSourceDescriptorContract.Validate(BundesligaContextSource.ClubElo, hostile, BundesligaContextSourceDisposition.Rejected)).Throws<InvalidDataException>();

        var freshAtSeven = HtmlObservation(HtmlEvaluationDescriptor("Eligible", body), BundesligaContextSourceDisposition.ArtifactCaptured, [], new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));
        freshAtSeven.Validate();
        await Assert.That(() => HtmlObservation(HtmlEvaluationDescriptor("StaleRejected", body), BundesligaContextSourceDisposition.Rejected, ["CLUB_ELO_STALE_GT_7_DAYS"], freshAtSeven.ObservedAtUtc).Validate()).Throws<InvalidDataException>();
        await Assert.That(() => HtmlObservation(HtmlDescriptor(body, root => { root["evaluation"] = "Eligible"; root["displayedDate"] = "2026-09-12"; root["providerDateEvidence"]!.AsObject()["rawValue"] = "2026-09-12"; root["providerDateEvidence"]!.AsObject()["ratedAt"] = "2026-09-12"; }), BundesligaContextSourceDisposition.ArtifactCaptured, [], freshAtSeven.ObservedAtUtc).Validate()).Throws<InvalidDataException>();

        var transportDescriptor = HtmlEvaluationDescriptor("TransportRejected", body);
        foreach (var terminal in new[] { "CLUB_ELO_CONNECTION_FAILED", "CLUB_ELO_TIMEOUT", "CLUB_ELO_HTTP_REJECTED" })
            HtmlObservation(transportDescriptor, BundesligaContextSourceDisposition.Rejected, [terminal, "CLUB_ELO_TRANSPORT_REJECTED"], freshAtSeven.ObservedAtUtc).Validate();
        foreach (var hostileDiagnostics in new IReadOnlyList<string>[] { ["CLUB_ELO_TRANSPORT_REJECTED", "CLUB_ELO_TIMEOUT"], ["CLUB_ELO_DOM_REJECTED", "CLUB_ELO_TRANSPORT_REJECTED"] })
            await Assert.That(() => HtmlObservation(transportDescriptor, BundesligaContextSourceDisposition.Rejected, hostileDiagnostics, freshAtSeven.ObservedAtUtc).Validate()).Throws<InvalidDataException>();
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

    private static string MutateElo(string descriptor, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(descriptor)!.AsObject();
        mutate(root["sourceRows"]!.AsArray()[0]!.AsObject());
        return root.ToJsonString();
    }

    private static string HtmlDescriptor(byte[] bytes, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(EligibleHtmlEloDescriptor(bytes))!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string MutateHtmlJson(string descriptor, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(descriptor)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string HtmlEvaluationDescriptor(string evaluation, byte[] bytes) => HtmlDescriptor(bytes, root =>
    {
        root["evaluation"] = evaluation;
        switch (evaluation)
        {
            case "TransportRejected":
                root["response"] = null; root["rawSha256"] = null; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
                break;
            case "SizeRejected":
                root["rawSha256"] = null; root["rawByteLength"] = null; root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["declaredContentLength"] = 2097153;
                break;
            case "ResponseRejected":
                root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null; root["response"]!.AsObject()["statusCode"] = 404;
                break;
            case "DomRejected": case "LexerRejected": case "FragmentRejected": case "DateRejected":
                root["displayedDate"] = null; root["providerDateEvidence"] = null; root["sourceRows"] = null;
                break;
            case "MappingRejected": root["sourceRows"] = null; break;
            case "CoverageRejected": root["sourceRows"] = new JsonArray(); break;
        }
    });

    private static BundesligaContextSourceObservation HtmlObservation(string descriptor, BundesligaContextSourceDisposition disposition, IReadOnlyList<string> diagnostics, DateTimeOffset observedAtUtc)
    {
        var cycle = BundesligaContextSourceCycleIdentity.Development(BundesligaContextSourceContract.Competition, "0198f865-1467-7000-8000-000000000099");
        return new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo, BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo), observedAtUtc, disposition, descriptor,
            disposition == BundesligaContextSourceDisposition.ArtifactCaptured ? new BundesligaContextSourcePayload("club-elo/source.html", 3, BundesligaContextSourceHashing.Sha256(Encoding.UTF8.GetBytes("abc"))) : null, diagnostics);
    }

    private static string EligibleHtmlEloDescriptor(byte[] bytes)
    {
        var mapping = new[]
        {
            ("b04", "/Leverkusen", "Leverkusen"), ("bmg", "/Gladbach", "Gladbach"), ("bvb", "/Dortmund", "Dortmund"), ("fca", "/Augsburg", "Augsburg"),
            ("fcb", "/Bayern", "Bayern München"), ("fck", "/Koeln", "Köln"), ("fcu", "/UnionBerlin", "Union Berlin"), ("hsv", "/Hamburg", "Hamburg"),
            ("m05", "/Mainz", "Mainz"), ("rbl", "/RBLeipzig", "RB Leipzig"), ("s04", "/Schalke", "Schalke"), ("scf", "/Freiburg", "Freiburg"),
            ("scp", "/Paderborn", "Paderborn"), ("sge", "/Frankfurt", "Frankfurt"), ("sve", "/Elversberg", "Elversberg"), ("svw", "/Werder", "Werder"),
            ("tsg", "/Hoffenheim", "Hoffenheim"), ("vfb", "/Stuttgart", "Stuttgart")
        };
        var rows = mapping.Select((entry, index) => new { teamSlug = entry.Item1, providerRoute = entry.Item2, providerDisplayName = entry.Item3, globalRank = index + 1, elo = 1500 + index }).ToArray();
        return JsonSerializer.Serialize(new
        {
            contract = "club-elo-official-html-descriptor/v1", sourceUrl = "https://clubelo.com/GER",
            response = new { statusCode = 200, finalUrl = "https://clubelo.com/GER", redirectCount = 0, redirectLocation = (string?)null, mediaType = "text/html", charset = "utf-8", contentEncodings = Array.Empty<string>(), declaredContentLength = (long)bytes.Length },
            rawSha256 = BundesligaContextSourceHashing.Sha256(bytes), rawByteLength = (long)bytes.Length,
            parserContract = "club-elo-official-html-parser/v1", displayedDate = "2026-09-04",
            providerDateEvidence = new { kind = "OfficialHtmlHeadingLink", recipeId = "club-elo-official-html-displayed-date/v1", field = "h1>a[href]", rawValue = "2026-09-04", ratedAt = "2026-09-04" },
            tableContract = "club-elo-official-html-table/v1", tableHeader = new[] { "Club", "Elo", "+/-", "Golo" },
            nameMappingContract = "bundesliga-2026-27-club-elo-name-map/v1", nameMappingSha256 = BundesligaContextSourceDescriptorContract.ClubEloHtmlNameMappingSha256,
            sourceRows = rows, evaluation = "Eligible"
        });
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
