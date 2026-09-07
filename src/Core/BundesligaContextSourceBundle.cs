using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaContextSourcePayload(string Path, long ByteLength, string Sha256)
{
    public void Validate(BundesligaContextSource source)
    {
        var expected = source == BundesligaContextSource.ClubElo ? "club-elo/source.csv" : "rosters/source.duckdb";
        if (Path != expected || ByteLength < 0) throw new InvalidDataException("Payload identity is not canonical.");
        BundesligaContextSourceHashing.ValidateSha(Sha256);
    }
}

public sealed record BundesligaContextSourceObservation(
    BundesligaContextSource Source,
    string AttemptId,
    DateTimeOffset ObservedAtUtc,
    BundesligaContextSourceDisposition Disposition,
    string DescriptorJson,
    BundesligaContextSourcePayload? Payload,
    IReadOnlyList<string> Diagnostics)
{
    public string SourceValue => BundesligaContextSourceContract.SourceValue(Source);
    public string DescriptorSha256 => BundesligaContextSourceHashing.Sha256(Encoding.UTF8.GetBytes(DescriptorJson));
    public string ObservationDigest => BundesligaContextSourceHashing.Sha256(CreateCanonicalUtf8());

    public byte[] CreateCanonicalUtf8()
    {
        Validate();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("source", SourceValue);
            writer.WriteString("attemptId", AttemptId);
            writer.WriteString("observedAtUtc", BundesligaContextSourceContract.FormatUtc(ObservedAtUtc));
            writer.WriteString("disposition", Disposition.ToString());
            writer.WriteString("descriptorSha256", DescriptorSha256);
            writer.WritePropertyName("descriptor");
            using (var descriptor = JsonDocument.Parse(DescriptorJson)) descriptor.RootElement.WriteTo(writer);
            WritePayload(writer, Payload);
            writer.WritePropertyName("diagnostics"); JsonSerializer.Serialize(writer, Diagnostics);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Source) || !Enum.IsDefined(Disposition))
            throw new InvalidDataException("Observation source/disposition is invalid.");
        BundesligaContextSourceHashing.ValidateSha(AttemptId);
        BundesligaContextSourceContract.FormatUtc(ObservedAtUtc);
        BundesligaContextSourceDescriptorContract.Validate(Source, DescriptorJson, Disposition);
        if (Diagnostics.Any(string.IsNullOrWhiteSpace) || !Diagnostics.SequenceEqual(Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Diagnostics must be unique and ordinal sorted.");
        if (Disposition == BundesligaContextSourceDisposition.ArtifactCaptured)
        {
            if (Payload is null) throw new InvalidDataException("ArtifactCaptured requires a payload.");
            Payload.Validate(Source);
            BundesligaContextSourceDescriptorContract.ValidatePayloadIdentity(Source, DescriptorJson, Payload);
        }
        else if (Payload is not null) throw new InvalidDataException("Only ArtifactCaptured may carry a payload.");
        if (Disposition == BundesligaContextSourceDisposition.MetadataUnchanged && Source != BundesligaContextSource.Rosters) throw new InvalidDataException("MetadataUnchanged is roster-only.");
        if (Disposition == BundesligaContextSourceDisposition.Rejected && Diagnostics.Count == 0) throw new InvalidDataException("Rejected observations require diagnostics.");
    }

    internal static void WritePayload(Utf8JsonWriter writer, BundesligaContextSourcePayload? payload)
    {
        writer.WritePropertyName("payload");
        if (payload is null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject(); writer.WriteString("path", payload.Path); writer.WriteNumber("byteLength", payload.ByteLength); writer.WriteString("sha256", payload.Sha256); writer.WriteEndObject();
    }
}

public sealed record BundesligaContextSourceBundle(
    BundesligaContextSourceCycleIdentity Cycle,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset StalenessReferenceAtUtc,
    string ProducerLaneId,
    IReadOnlyList<string> ExpectedConsumers,
    IReadOnlyList<BundesligaContextSourceObservation> Observations)
{
    public const string Contract = "bundesliga-context-source-bundle/v1";
    private static readonly byte[] DigestDomain = Encoding.UTF8.GetBytes("bundesliga-context-source-bundle-digest/v1");

    public byte[] CreateManifestUtf8()
    {
        Validate();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("contract", Contract);
            writer.WriteString("competition", Cycle.Competition);
            writer.WriteString("scope", Cycle.ScopeValue);
            writer.WriteString("cycleId", Cycle.CycleId);
            writer.WriteString("cycleStorageId", Cycle.StorageId);
            writer.WriteNumber("cycleSequence", Cycle.Sequence);
            writer.WriteString("startedAtUtc", BundesligaContextSourceContract.FormatUtc(StartedAtUtc));
            writer.WriteString("stalenessReferenceAtUtc", BundesligaContextSourceContract.FormatUtc(StalenessReferenceAtUtc));
            writer.WriteString("producerLaneId", ProducerLaneId);
            writer.WritePropertyName("expectedConsumers"); JsonSerializer.Serialize(writer, ExpectedConsumers);
            writer.WritePropertyName("observations"); writer.WriteStartArray();
            foreach (var observation in Observations)
            {
                using var document = JsonDocument.Parse(observation.CreateCanonicalUtf8());
                document.RootElement.WriteTo(writer);
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public string BundleSha256(Func<BundesligaContextSourceObservation, byte[]> payloadBytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        BundesligaContextSourceHashing.AppendLp32(hash, DigestDomain);
        AppendFile(hash, "manifest.json", CreateManifestUtf8());
        foreach (var observation in Observations.Where(x => x.Payload is not null))
        {
            var bytes = payloadBytes(observation);
            if (bytes.LongLength != observation.Payload!.ByteLength || BundesligaContextSourceHashing.Sha256(bytes) != observation.Payload.Sha256)
                throw new InvalidDataException("Payload bytes do not match their manifest identity.");
            AppendFile(hash, observation.Payload.Path, bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public string BundleSha256(IReadOnlyDictionary<string, byte[]> payloads)
    {
        var expected = Observations.Where(x => x.Payload is not null).Select(x => x.Payload!.Path).ToHashSet(StringComparer.Ordinal);
        if (!payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)) throw new InvalidDataException("Payload set does not match the manifest.");
        return BundleSha256(observation => payloads[observation.Payload!.Path]);
    }

    public void Validate()
    {
        _ = BundesligaContextSourceCycleIdentity.Create(Cycle.Competition, Cycle.Scope, Cycle.CycleId, Cycle.Sequence);
        BundesligaContextSourceContract.FormatUtc(StartedAtUtc); BundesligaContextSourceContract.FormatUtc(StalenessReferenceAtUtc);
        if (StartedAtUtc != StalenessReferenceAtUtc) throw new InvalidDataException("Bundle timestamps must be identical.");
        BundesligaContextSourceContract.ValidateConsumers(Cycle.Scope, ProducerLaneId, ExpectedConsumers);
        if (Observations.Count == 0 || Observations.Select(x => x.Source).Distinct().Count() != Observations.Count || !Observations.Select(x => x.Source).SequenceEqual(Observations.Select(x => x.Source).Order()))
            throw new InvalidDataException("Observations must be nonempty, unique, and ordered club-elo then rosters.");
        foreach (var observation in Observations)
        {
            observation.Validate();
            if (observation.AttemptId != BundesligaContextSourceHashing.AttemptId(Cycle, observation.Source)) throw new InvalidDataException("Observation attempt identity mismatch.");
        }
    }

    public static BundesligaContextSourceBundle ParseManifest(byte[] utf8)
    {
        using var document = JsonDocument.Parse(utf8);
        var root = document.RootElement;
        var names = new[] { "contract", "competition", "scope", "cycleId", "cycleStorageId", "cycleSequence", "startedAtUtc", "stalenessReferenceAtUtc", "producerLaneId", "expectedConsumers", "observations" };
        if (!root.EnumerateObject().Select(x => x.Name).SequenceEqual(names, StringComparer.Ordinal) || !utf8.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(root))) throw new InvalidDataException("Manifest JSON is not canonical.");
        if (root.GetProperty("contract").GetString() != Contract) throw new InvalidDataException("Manifest contract is invalid.");
        var scope = root.GetProperty("scope").GetString() switch { BundesligaContextSourceContract.ProductionScope => BundesligaContextSourceScope.ProductionLive, BundesligaContextSourceContract.DevelopmentScope => BundesligaContextSourceScope.Development, _ => throw new InvalidDataException("Manifest scope is invalid.") };
        if (!root.GetProperty("cycleSequence").TryGetInt64(out var sequence)) throw new InvalidDataException("Manifest sequence is invalid.");
        var cycle = BundesligaContextSourceCycleIdentity.Create(root.GetProperty("competition").GetString()!, scope, root.GetProperty("cycleId").GetString()!, sequence);
        if (root.GetProperty("cycleStorageId").GetString() != cycle.StorageId) throw new InvalidDataException("Manifest storage identity mismatch.");
        var consumers = root.GetProperty("expectedConsumers").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString()! : throw new InvalidDataException("Manifest consumer type is invalid.")).ToArray();
        var observations = root.GetProperty("observations").EnumerateArray().Select(x => BundesligaContextSourceDescriptorContract.ParseObservation(x.GetRawText())).ToArray();
        var bundle = new BundesligaContextSourceBundle(cycle, BundesligaContextSourceContract.ParseUtc(root.GetProperty("startedAtUtc").GetString()!), BundesligaContextSourceContract.ParseUtc(root.GetProperty("stalenessReferenceAtUtc").GetString()!), root.GetProperty("producerLaneId").GetString()!, consumers, observations);
        bundle.Validate(); return bundle;
    }

    private static void AppendFile(IncrementalHash hash, string path, byte[] content)
    {
        BundesligaContextSourceHashing.AppendLp32(hash, Encoding.UTF8.GetBytes(path));
        BundesligaContextSourceHashing.AppendLp64(hash, content);
    }
}

public static class BundesligaContextSourceDescriptorContract
{
    public const string RosterPolicySha256 = "56ce2f0543b91a59b63fbec7889f1bf547681e90f58da7c419028fd749285d9b";

    private static readonly string[] EloFields = ["contract", "sourceUrl", "rawSha256", "rawByteLength", "csvHeader", "providerRatedAt", "providerDateEvidence", "nameMappingContract", "nameMappingSha256", "sourceRows", "evaluation"];
    private static readonly string[] RosterFields = ["contract", "metadataUrl", "artifactUrl", "advertisedRevision", "metadataSha256", "metadataByteLength", "remoteIdentityBefore", "acquisitionReason", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate", "policySha256", "retainedDescriptorSha256", "retainedEvaluation", "retainedDiagnostics", "evaluation"];
    private static readonly string[] DateEvidenceFields = ["kind", "recipeId", "field", "rawValue", "ratedAt"];
    private static readonly string[] SourceRowFields = ["teamSlug", "providerName", "globalRank", "elo"];
    private static readonly string[] RemoteIdentityFields = ["etag", "byteLength"];

    public static void Validate(BundesligaContextSource source, string json, BundesligaContextSourceDisposition disposition)
    {
        if (string.IsNullOrEmpty(json) || json[0] != '{' || json[^1] != '}') throw new InvalidDataException("Descriptor JSON is not compact canonical JSON.");
        var utf8 = Encoding.UTF8.GetBytes(json);
        using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        var root = document.RootElement;
        RequireFields(root, source == BundesligaContextSource.ClubElo ? EloFields : RosterFields);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(root);
        if (!utf8.AsSpan().SequenceEqual(canonical)) throw new InvalidDataException("Descriptor JSON must be compact UTF-8 with canonical property order and values.");
        if (source == BundesligaContextSource.ClubElo) ValidateElo(root, disposition); else ValidateRoster(root, disposition);
    }

    public static BundesligaContextSourceObservation ParseObservation(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        RequireFields(root, ["source", "attemptId", "observedAtUtc", "disposition", "descriptorSha256", "descriptor", "payload", "diagnostics"]);
        if (!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(root))) throw new InvalidDataException("Observation JSON is not canonical.");
        var source = BundesligaContextSourceContract.ParseSource(RequireString(root, "source"));
        var dispositionValue = RequireString(root, "disposition");
        if (!Enum.TryParse<BundesligaContextSourceDisposition>(dispositionValue, false, out var disposition)
            || !Enum.IsDefined(disposition)
            || dispositionValue != disposition.ToString())
            throw new InvalidDataException("Observation disposition is unknown.");
        var descriptor = root.GetProperty("descriptor").GetRawText();
        if (RequireSha(root, "descriptorSha256") != BundesligaContextSourceHashing.Sha256(Encoding.UTF8.GetBytes(descriptor))) throw new InvalidDataException("Descriptor digest mismatch.");
        BundesligaContextSourcePayload? payload = null;
        if (root.GetProperty("payload") is { ValueKind: not JsonValueKind.Null } payloadElement)
        {
            RequireFields(payloadElement, ["path", "byteLength", "sha256"]);
            payload = new BundesligaContextSourcePayload(RequireString(payloadElement, "path"), RequireNonnegativeInt64(payloadElement, "byteLength"), RequireSha(payloadElement, "sha256"));
        }
        var diagnosticsElement = root.GetProperty("diagnostics");
        if (diagnosticsElement.ValueKind != JsonValueKind.Array || diagnosticsElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) throw new InvalidDataException("Observation diagnostics are invalid.");
        var observation = new BundesligaContextSourceObservation(source, RequireSha(root, "attemptId"), BundesligaContextSourceContract.ParseUtc(RequireString(root, "observedAtUtc")), disposition, descriptor, payload, diagnosticsElement.EnumerateArray().Select(x => x.GetString()!).ToArray());
        observation.Validate();
        return observation;
    }

    private static void ValidateElo(JsonElement root, BundesligaContextSourceDisposition disposition)
    {
        RequireString(root, "contract", "club-elo-direct-csv-descriptor/v1");
        RequireHttps(root, "sourceUrl");
        OptionalSha(root, "rawSha256"); OptionalNonnegativeInt64(root, "rawByteLength", requireFirestoreRoundTrip: true); OptionalString(root, "csvHeader"); OptionalDate(root, "providerRatedAt");
        var evidence = root.GetProperty("providerDateEvidence");
        if (evidence.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object)) throw new InvalidDataException("'providerDateEvidence' has the wrong type.");
        if (evidence.ValueKind == JsonValueKind.Object) ValidateDateEvidence(evidence);
        OptionalString(root, "nameMappingContract"); OptionalSha(root, "nameMappingSha256");
        var sourceRows = root.GetProperty("sourceRows");
        if (sourceRows.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new InvalidDataException("'sourceRows' has the wrong type.");
        if (sourceRows.ValueKind == JsonValueKind.Array) ValidateSourceRows(sourceRows, requireCompleteSet: false);
        var evaluation = RequireOneOf(root, "evaluation", "Eligible", "TransportRejected", "PayloadRejected", "HeaderRejected", "DateRejected", "MappingRejected", "CoverageRejected");
        if ((evaluation == "Eligible") != (disposition == BundesligaContextSourceDisposition.ArtifactCaptured)) throw new InvalidDataException("Club Elo disposition/evaluation conflict.");
        if (disposition == BundesligaContextSourceDisposition.MetadataUnchanged) throw new InvalidDataException("Club Elo does not support MetadataUnchanged.");
        if (evaluation == "Eligible")
        {
            RequireSha(root, "rawSha256"); RequireNonnegativeInt64(root, "rawByteLength"); RequireString(root, "csvHeader");
            RequireDate(root, "providerRatedAt"); RequireSha(root, "nameMappingSha256"); RequireString(root, "nameMappingContract");
            evidence = root.GetProperty("providerDateEvidence");
            if (root.GetProperty("providerRatedAt").GetString() != evidence.GetProperty("ratedAt").GetString()) throw new InvalidDataException("Rated-at evidence mismatch.");
            ValidateSourceRows(root.GetProperty("sourceRows"), requireCompleteSet: true);
        }
    }

    private static void ValidateDateEvidence(JsonElement evidence)
    {
        RequireFields(evidence, DateEvidenceFields);
        var kind = RequireOneOf(evidence, "kind", "ProviderCsvField", "AcceptedDailyEndpoint");
        if (kind == "ProviderCsvField") RequireString(evidence, "field"); else RequireNull(evidence, "field");
        RequireString(evidence, "recipeId"); RequireString(evidence, "rawValue"); RequireDate(evidence, "ratedAt");
    }

    private static void ValidateSourceRows(JsonElement rows, bool requireCompleteSet)
    {
        if (rows.ValueKind != JsonValueKind.Array || requireCompleteSet && rows.GetArrayLength() != 18)
            throw new InvalidDataException(requireCompleteSet ? "Eligible Club Elo requires 18 source rows." : "Club Elo source rows must be an array.");
        var names = new HashSet<string>(StringComparer.Ordinal); var ranks = new HashSet<int>(); var slugs = new List<string>(); string? previous = null;
        foreach (var row in rows.EnumerateArray())
        {
            RequireFields(row, SourceRowFields);
            var slug = RequireString(row, "teamSlug");
            if (previous is not null && string.CompareOrdinal(previous, slug) >= 0) throw new InvalidDataException("Club Elo rows are not manifest-slug ordered.");
            previous = slug; slugs.Add(slug);
            if (!names.Add(RequireString(row, "providerName")) || !ranks.Add(RequirePositiveInt32(row, "globalRank", requireFirestoreRoundTrip: true))) throw new InvalidDataException("Club Elo names/ranks must be unique.");
            RequirePositiveFiniteDouble(row, "elo", requireFirestoreRoundTrip: true);
        }
        if (requireCompleteSet && !slugs.SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(entry => entry.TeamSlug), StringComparer.Ordinal))
            throw new InvalidDataException("Eligible Club Elo source rows must exactly match the canonical Bundesliga team manifest.");
    }

    private static void ValidateRoster(JsonElement root, BundesligaContextSourceDisposition disposition)
    {
        RequireString(root, "contract", "transfermarkt-duckdb-observation-descriptor/v1"); RequireHttps(root, "metadataUrl"); RequireHttps(root, "artifactUrl");
        var revision = RequireLowerHex(root, "advertisedRevision", 40);
        if (RequireSha(root, "policySha256") != RosterPolicySha256)
            throw new InvalidDataException("Roster policy SHA-256 does not match the frozen ADR-0074 policy.");
        var acquisitionReason = RequireOneOf(root, "acquisitionReason", "NewRevision", "PendingRevision", "RemoteIdentityChanged", "PolicyChanged", "AcceptedRevisionUnchanged");
        var evaluation = RequireOneOf(root, "evaluation", "Eligible", "MetadataUnchanged", "TransportRejected", "SizeRejected", "RemoteDriftRejected", "HashRejected", "RevisionRejected", "SchemaRejected", "SourceDateRejected", "SeasonRejected", "IdentityRejected");
        if ((evaluation == "Eligible") != (disposition == BundesligaContextSourceDisposition.ArtifactCaptured) || (evaluation == "MetadataUnchanged") != (disposition == BundesligaContextSourceDisposition.MetadataUnchanged)) throw new InvalidDataException("Roster disposition/evaluation conflict.");
        if (evaluation == "MetadataUnchanged")
        {
            if (acquisitionReason != "AcceptedRevisionUnchanged") throw new InvalidDataException("MetadataUnchanged requires AcceptedRevisionUnchanged.");
            RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength"); ValidateRemoteIdentity(root.GetProperty("remoteIdentityBefore"));
            RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
            RequireSha(root, "retainedDescriptorSha256");
            RequireOneOf(root, "retainedEvaluation", "Eligible", "SchemaRejected", "SourceDateRejected", "SeasonRejected", "IdentityRejected");
            RequireCanonicalStringArray(root, "retainedDiagnostics");
        }
        else
        {
            if (acquisitionReason == "AcceptedRevisionUnchanged") throw new InvalidDataException("AcceptedRevisionUnchanged may only produce MetadataUnchanged.");
            RequireNull(root, "retainedDescriptorSha256"); RequireNull(root, "retainedEvaluation"); var retained = root.GetProperty("retainedDiagnostics"); if (retained.ValueKind != JsonValueKind.Array || retained.GetArrayLength() != 0) throw new InvalidDataException("Non-metadata retained diagnostics must be empty.");
        }

        switch (evaluation)
        {
            case "MetadataUnchanged":
                break;
            case "TransportRejected":
                ValidateAllNullOrMetadataObserved(root);
                RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "SizeRejected":
                RequireMetadataAndBefore(root); RequireNonnegativeInt64(root, "rawByteLength"); OptionalSha(root, "expectedRawSha256");
                RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "RemoteDriftRejected":
                RequireMetadataAndBefore(root); ValidateRemoteIdentity(root.GetProperty("remoteIdentityAfter"));
                if (RemoteIdentitiesEqual(root)) throw new InvalidDataException("RemoteDriftRejected requires unequal identities.");
                ValidateObservedArtifactFacts(root);
                break;
            case "HashRejected":
                RequireMetadataAndEqualIdentities(root); OptionalLowerHex(root, "embeddedRevision", 40);
                var hashActual = RequireSha(root, "rawSha256"); var hashExpected = RequireSha(root, "expectedRawSha256");
                if (hashActual == hashExpected) throw new InvalidDataException("HashRejected requires unequal actual and expected hashes.");
                RequireNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
                break;
            case "RevisionRejected":
                RequireMetadataAndEqualIdentities(root); var embedded = OptionalLowerHex(root, "embeddedRevision", 40);
                if (embedded == revision) throw new InvalidDataException("RevisionRejected requires a missing or unequal embedded revision.");
                var revisionActual = RequireSha(root, "rawSha256"); RequireExpectedHashPassedOrAbsent(root, revisionActual); RequireNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
                break;
            case "SchemaRejected":
                ValidateIdentifiedArtifact(root, revision, requireAllDates: false); break;
            case "SourceDateRejected":
                ValidateIdentifiedArtifact(root, revision, requireAllDates: false);
                if (new[] { "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate" }.All(name => root.GetProperty(name).ValueKind != JsonValueKind.Null)) throw new InvalidDataException("SourceDateRejected requires at least one unknown source date.");
                break;
            case "SeasonRejected":
            case "IdentityRejected":
            case "Eligible":
                ValidateIdentifiedArtifact(root, revision, requireAllDates: true); break;
        }
    }

    public static void ValidatePayloadIdentity(BundesligaContextSource source, string descriptorJson, BundesligaContextSourcePayload payload)
    {
        using var document = JsonDocument.Parse(descriptorJson);
        var root = document.RootElement;
        var rawSha = RequireSha(root, "rawSha256");
        var rawLength = RequireNonnegativeInt64(root, "rawByteLength");
        if (rawSha != payload.Sha256 || rawLength != payload.ByteLength)
            throw new InvalidDataException("Descriptor raw identity does not match its payload.");
        payload.Validate(source);
    }

    private static void ValidateAllNullOrMetadataObserved(JsonElement root)
    {
        var metadataSha = root.GetProperty("metadataSha256"); var metadataLength = root.GetProperty("metadataByteLength"); var before = root.GetProperty("remoteIdentityBefore");
        var allNull = metadataSha.ValueKind == JsonValueKind.Null && metadataLength.ValueKind == JsonValueKind.Null && before.ValueKind == JsonValueKind.Null;
        if (allNull) return;
        RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength"); ValidateRemoteIdentity(before);
    }

    private static void RequireMetadataAndBefore(JsonElement root)
    {
        RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength"); ValidateRemoteIdentity(root.GetProperty("remoteIdentityBefore"));
    }

    private static void RequireMetadataAndEqualIdentities(JsonElement root)
    {
        RequireMetadataAndBefore(root); ValidateRemoteIdentity(root.GetProperty("remoteIdentityAfter"));
        if (!RemoteIdentitiesEqual(root)) throw new InvalidDataException("Remote identities must agree.");
    }

    private static bool RemoteIdentitiesEqual(JsonElement root)
        => root.GetProperty("remoteIdentityBefore").GetRawText() == root.GetProperty("remoteIdentityAfter").GetRawText();

    private static void ValidateObservedArtifactFacts(JsonElement root)
    {
        OptionalLowerHex(root, "embeddedRevision", 40); OptionalSha(root, "rawSha256"); OptionalSha(root, "expectedRawSha256"); OptionalNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
    }

    private static void ValidateObservedDates(JsonElement root)
    {
        OptionalDate(root, "artifactCaptureDate"); OptionalDate(root, "membershipEffectiveDate"); OptionalDate(root, "enrichmentCaptureDate");
    }

    private static void ValidateIdentifiedArtifact(JsonElement root, string advertisedRevision, bool requireAllDates)
    {
        RequireMetadataAndEqualIdentities(root);
        if (RequireLowerHex(root, "embeddedRevision", 40) != advertisedRevision) throw new InvalidDataException("Embedded revision mismatch.");
        var actual = RequireSha(root, "rawSha256"); RequireExpectedHashPassedOrAbsent(root, actual); RequireNonnegativeInt64(root, "rawByteLength");
        if (requireAllDates) { RequireDate(root, "artifactCaptureDate"); RequireDate(root, "membershipEffectiveDate"); RequireDate(root, "enrichmentCaptureDate"); }
        else ValidateObservedDates(root);
    }

    private static void RequireExpectedHashPassedOrAbsent(JsonElement root, string actual)
    {
        if (root.GetProperty("expectedRawSha256").ValueKind != JsonValueKind.Null && RequireSha(root, "expectedRawSha256") != actual)
            throw new InvalidDataException("Expected and actual raw hashes conflict.");
    }

    private static void RequireFields(JsonElement value, IReadOnlyList<string> names)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(x => x.Name).SequenceEqual(names, StringComparer.Ordinal)) throw new InvalidDataException("Canonical JSON property set/order is invalid.");
    }
    private static string RequireString(JsonElement root, string name, string? exact = null) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(p.GetString()) || (exact is not null && p.GetString() != exact)) throw new InvalidDataException($"'{name}' is invalid."); return p.GetString()!; }
    private static void RequireNull(JsonElement root, string name) { if (root.GetProperty(name).ValueKind != JsonValueKind.Null) throw new InvalidDataException($"'{name}' must be null."); }
    private static void RequireNulls(JsonElement root, params string[] names) { foreach (var name in names) RequireNull(root, name); }
    private static string RequireOneOf(JsonElement root, string name, params string[] allowed) { var value = RequireString(root, name); if (!allowed.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException($"'{name}' has an unknown enum value."); return value; }
    private static string RequireSha(JsonElement root, string name) { var value = RequireString(root, name); BundesligaContextSourceHashing.ValidateSha(value); return value; }
    private static string RequireLowerHex(JsonElement root, string name, int length) { var value = RequireString(root, name); if (value.Length != length || value.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) throw new InvalidDataException($"'{name}' must be lowercase hex."); return value; }
    private static void RequireHttps(JsonElement root, string name) { var value = RequireString(root, name); if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException($"'{name}' must be HTTPS."); }
    private static void RequireDate(JsonElement root, string name) { var value = RequireString(root, name); if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var parsed) || parsed.ToString("yyyy-MM-dd") != value) throw new InvalidDataException($"'{name}' is not a canonical date."); }
    private static long RequireNonnegativeInt64(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt64(out var value) || value < 0) throw new InvalidDataException($"'{name}' is not a non-negative Int64."); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static int RequirePositiveInt32(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out var value) || value <= 0) throw new InvalidDataException($"'{name}' is not a positive Int32."); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static double RequirePositiveFiniteDouble(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetDouble(out var value) || !double.IsFinite(value) || value <= 0) throw new InvalidDataException($"'{name}' is not a positive finite number."); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static void RequireFirestoreRoundtrippableNumber(JsonElement value)
    {
        var firestoreValue = value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble();
        if (value.GetRawText() != JsonSerializer.Serialize(firestoreValue))
            throw new InvalidDataException("Club Elo numeric values must use their canonical Firestore-roundtrippable spelling.");
    }
    private static void RequireCanonicalStringArray(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Array || p.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) throw new InvalidDataException($"'{name}' must be a string array."); var values = p.EnumerateArray().Select(x => x.GetString()!).ToArray(); if (values.Any(string.IsNullOrWhiteSpace) || !values.SequenceEqual(values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw new InvalidDataException($"'{name}' must be unique and ordinal sorted."); }
    private static void OptionalString(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) throw new InvalidDataException($"'{name}' has the wrong type."); }
    private static void OptionalSha(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireSha(root, name); }
    private static void OptionalNonnegativeInt64(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireNonnegativeInt64(root, name, requireFirestoreRoundTrip); }
    private static void OptionalDate(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireDate(root, name); }
    private static string? OptionalLowerHex(JsonElement root, string name, int length) { var p = root.GetProperty(name); return p.ValueKind == JsonValueKind.Null ? null : RequireLowerHex(root, name, length); }
    private static void ValidateRemoteIdentity(JsonElement identity) { RequireFields(identity, RemoteIdentityFields); var etag = identity.GetProperty("etag"); var length = identity.GetProperty("byteLength"); if (etag.ValueKind == JsonValueKind.Null && length.ValueKind == JsonValueKind.Null) throw new InvalidDataException("Remote identity must have an ETag or byte length."); if (etag.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) || etag.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(etag.GetString())) throw new InvalidDataException("Remote ETag is invalid."); if (length.ValueKind != JsonValueKind.Null && (!length.TryGetInt64(out var n) || n < 0)) throw new InvalidDataException("Remote byte length is invalid."); }
}
