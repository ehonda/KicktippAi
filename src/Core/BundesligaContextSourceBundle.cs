using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaContextSourcePayload(string Path, long ByteLength, string Sha256)
{
    public void Validate(BundesligaContextSource source)
    {
        var validPath = source switch
        {
            BundesligaContextSource.ClubElo => Path is "club-elo/source.csv" or "club-elo/source.html",
            BundesligaContextSource.Rosters => Path == "rosters/source.duckdb",
            _ => false
        };
        if (!validPath || ByteLength < 0) throw new InvalidDataException("Payload identity is not canonical.");
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
        if (Source == BundesligaContextSource.ClubElo)
            BundesligaContextSourceDescriptorContract.ValidateHtmlFreshness(DescriptorJson, ObservedAtUtc);
        if (Diagnostics.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Diagnostics must be nonempty.");
        if (Source == BundesligaContextSource.Rosters)
            BundesligaContextSourceDescriptorContract.ValidateRosterDiagnostics(DescriptorJson, Disposition, Diagnostics);
        else if (!Diagnostics.SequenceEqual(Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Diagnostics must be unique and ordinal sorted.");
        else
            BundesligaContextSourceDescriptorContract.ValidateClubEloDiagnostics(DescriptorJson, Disposition, Diagnostics);
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
        if (root.ValueKind != JsonValueKind.Object
            || !root.EnumerateObject().Select(x => x.Name).SequenceEqual(names, StringComparer.Ordinal)
            || !utf8.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(root)))
            throw new InvalidDataException("Manifest JSON is not canonical.");
        if (RequireManifestString(root, "contract") != Contract) throw new InvalidDataException("Manifest contract is invalid.");
        var scope = RequireManifestString(root, "scope") switch { BundesligaContextSourceContract.ProductionScope => BundesligaContextSourceScope.ProductionLive, BundesligaContextSourceContract.DevelopmentScope => BundesligaContextSourceScope.Development, _ => throw new InvalidDataException("Manifest scope is invalid.") };
        var sequence = RequireManifestNonnegativeInt64(root, "cycleSequence");
        var cycle = BundesligaContextSourceCycleIdentity.Create(RequireManifestString(root, "competition"), scope, RequireManifestString(root, "cycleId"), sequence);
        if (RequireManifestString(root, "cycleStorageId") != cycle.StorageId) throw new InvalidDataException("Manifest storage identity mismatch.");
        var consumers = RequireManifestStringArray(root, "expectedConsumers");
        var observationsElement = root.GetProperty("observations");
        if (observationsElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Manifest observations must be an array.");
        var observations = observationsElement.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Object
            ? BundesligaContextSourceDescriptorContract.ParseObservation(x.GetRawText())
            : throw new InvalidDataException("Manifest observation type is invalid.")).ToArray();
        var bundle = new BundesligaContextSourceBundle(cycle, BundesligaContextSourceContract.ParseUtc(RequireManifestString(root, "startedAtUtc")), BundesligaContextSourceContract.ParseUtc(RequireManifestString(root, "stalenessReferenceAtUtc")), RequireManifestString(root, "producerLaneId"), consumers, observations);
        bundle.Validate(); return bundle;
    }

    private static string RequireManifestString(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
            throw new InvalidDataException($"Manifest '{name}' must be a nonempty string.");
        return value.GetString()!;
    }

    private static long RequireManifestNonnegativeInt64(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var parsed) || parsed < 0)
            throw new InvalidDataException($"Manifest '{name}' must be a non-negative Int64.");
        if (value.GetRawText() != JsonSerializer.Serialize(parsed))
            throw new InvalidDataException($"Manifest '{name}' must use canonical numeric spelling.");
        return parsed;
    }

    private static string[] RequireManifestStringArray(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString())))
            throw new InvalidDataException($"Manifest '{name}' must be a nonempty-string array.");
        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static void AppendFile(IncrementalHash hash, string path, byte[] content)
    {
        BundesligaContextSourceHashing.AppendLp32(hash, Encoding.UTF8.GetBytes(path));
        BundesligaContextSourceHashing.AppendLp64(hash, content);
    }
}

public static class BundesligaContextSourceDescriptorContract
{
    public const string ClubEloHtmlNameMappingSha256 = "8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7";
    public const string RosterPolicySha256 = "56ce2f0543b91a59b63fbec7889f1bf547681e90f58da7c419028fd749285d9b";
    public const string RosterMetadataUrl = "https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb.metadata.json";
    public const string RosterArtifactUrl = "https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb";
    public const long MaximumRosterArtifactBytes = 314572800;


    private static readonly string[] EloFields = ["contract", "sourceUrl", "rawSha256", "rawByteLength", "csvHeader", "providerRatedAt", "providerDateEvidence", "nameMappingContract", "nameMappingSha256", "sourceRows", "evaluation"];
    private static readonly string[] HtmlEloFields = ["contract", "sourceUrl", "response", "rawSha256", "rawByteLength", "parserContract", "displayedDate", "providerDateEvidence", "tableContract", "tableHeader", "nameMappingContract", "nameMappingSha256", "sourceRows", "evaluation"];
    private static readonly string[] RosterFields = ["contract", "metadataUrl", "artifactUrl", "advertisedRevision", "metadataSha256", "metadataByteLength", "remoteIdentityBefore", "acquisitionReason", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate", "policySha256", "retainedDescriptorSha256", "retainedEvaluation", "retainedDiagnostics", "evaluation"];
    private static readonly string[] DateEvidenceFields = ["kind", "recipeId", "field", "rawValue", "ratedAt"];
    private static readonly string[] SourceRowFields = ["teamSlug", "providerName", "globalRank", "elo"];
    private static readonly string[] HtmlSourceRowFields = ["teamSlug", "providerRoute", "providerDisplayName", "globalRank", "elo"];
    private static readonly string[] HtmlResponseFields = ["statusCode", "finalUrl", "redirectCount", "redirectLocation", "mediaType", "charset", "contentEncodings", "declaredContentLength"];
    private static readonly IReadOnlyDictionary<string, (string Route, string DisplayName)> HtmlClubEloMapping =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["b04"] = ("/Leverkusen", "Leverkusen"), ["bmg"] = ("/Gladbach", "Gladbach"), ["bvb"] = ("/Dortmund", "Dortmund"),
            ["fca"] = ("/Augsburg", "Augsburg"), ["fcb"] = ("/Bayern", "Bayern München"), ["fck"] = ("/Koeln", "Köln"),
            ["fcu"] = ("/UnionBerlin", "Union Berlin"), ["hsv"] = ("/Hamburg", "Hamburg"), ["m05"] = ("/Mainz", "Mainz"),
            ["rbl"] = ("/RBLeipzig", "RB Leipzig"), ["s04"] = ("/Schalke", "Schalke"), ["scf"] = ("/Freiburg", "Freiburg"),
            ["scp"] = ("/Paderborn", "Paderborn"), ["sge"] = ("/Frankfurt", "Frankfurt"), ["sve"] = ("/Elversberg", "Elversberg"),
            ["svw"] = ("/Werder", "Werder"), ["tsg"] = ("/Hoffenheim", "Hoffenheim"), ["vfb"] = ("/Stuttgart", "Stuttgart")
        };
    private static readonly string[] RemoteIdentityFields = ["etag", "byteLength"];

    public static void Validate(BundesligaContextSource source, string json, BundesligaContextSourceDisposition disposition)
    {
        if (string.IsNullOrEmpty(json) || json[0] != '{' || json[^1] != '}') throw new InvalidDataException("Descriptor JSON is not compact canonical JSON.");
        var utf8 = Encoding.UTF8.GetBytes(json);
        using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        var root = document.RootElement;
        RequireFields(root, source == BundesligaContextSource.ClubElo ? EloFieldsFor(root) : RosterFields);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(root);
        if (!utf8.AsSpan().SequenceEqual(canonical)) throw new InvalidDataException("Descriptor JSON must be compact UTF-8 with canonical property order and values.");
        if (source == BundesligaContextSource.ClubElo) ValidateElo(root, disposition); else ValidateRoster(root, disposition);
    }

    public static void ValidateRosterDiagnostics(string descriptorJson, BundesligaContextSourceDisposition disposition, IReadOnlyList<string> diagnostics)
    {
        using var document = JsonDocument.Parse(descriptorJson);
        var evaluation = document.RootElement.GetProperty("evaluation").GetString()!;
        ValidateRosterEvaluationPrecedence(evaluation, disposition, diagnostics);
    }

    /// <summary>One ADR-defined (and intentionally non-lexical) diagnostic order.</summary>
    public static void ValidateRosterEvaluationPrecedence(
        string evaluation,
        BundesligaContextSourceDisposition disposition,
        IReadOnlyList<string> diagnostics)
    {
        var primary = evaluation switch
        {
            "MetadataUnavailable" => "ROSTER_METADATA_UNAVAILABLE",
            "MetadataMalformed" => "ROSTER_METADATA_MALFORMED",
            "MetadataRevisionRejected" => "ROSTER_ADVERTISED_REVISION_REJECTED",
            "RemoteIdentityUnavailable" => "ROSTER_REMOTE_IDENTITY_UNAVAILABLE",
            "ArtifactTransportRejected" => "ROSTER_ARTIFACT_TRANSPORT_REJECTED",
            "SizeRejected" => "ROSTER_SIZE_REJECTED",
            "RemoteDriftRejected" => "ROSTER_REMOTE_DRIFT_REJECTED",
            "HashRejected" => "ROSTER_HASH_REJECTED",
            "RevisionRejected" => "ROSTER_REVISION_REJECTED",
            "SchemaRejected" => "ROSTER_DUCKDB_SCHEMA_REJECTED",
            "SourceDateRejected" => "UNKNOWN_SOURCE_DATE",
            "SeasonRejected" => "NO_ELIGIBLE_2026_MEMBERSHIP",
            "IdentityRejected" => "ROSTER_IDENTITY_REJECTED",
            _ => null
        };
        if (primary is null)
        {
            if (diagnostics.Count != 0) throw new InvalidDataException("Successful roster observations cannot contain diagnostics.");
            return;
        }
        if (disposition != BundesligaContextSourceDisposition.Rejected || diagnostics.Count == 0 || diagnostics[0] != primary)
            throw new InvalidDataException("Roster rejection diagnostics do not start with the evaluation primary code.");
        ValidateRosterDiagnosticOrder(diagnostics);
    }

    private static void ValidateRosterDiagnosticOrder(IReadOnlyList<string> diagnostics)
    {
        var order = new[] { "ROSTER_METADATA_UNAVAILABLE", "ROSTER_METADATA_MALFORMED", "ROSTER_ADVERTISED_REVISION_REJECTED", "ROSTER_REMOTE_IDENTITY_UNAVAILABLE", "ROSTER_ARTIFACT_TRANSPORT_REJECTED", "ROSTER_SIZE_REJECTED", "ROSTER_REMOTE_DRIFT_REJECTED", "ROSTER_HASH_REJECTED", "ROSTER_REVISION_REJECTED", "ROSTER_DUCKDB_SCHEMA_REJECTED", "UNKNOWN_SOURCE_DATE", "NO_ELIGIBLE_2026_MEMBERSHIP", "ROSTER_IDENTITY_REJECTED", "ROSTER_MEMBERSHIP_REJECTED", "ROSTER_ENRICHMENT_REJECTED" };
        if (diagnostics.Any(value => !order.Contains(value, StringComparer.Ordinal))
            || !diagnostics.SequenceEqual(diagnostics.Distinct(StringComparer.Ordinal))
            || !diagnostics.Select(value => Array.IndexOf(order, value)).SequenceEqual(diagnostics.Select(value => Array.IndexOf(order, value)).Order()))
            throw new InvalidDataException("Roster diagnostics are not unique and evaluation-precedence ordered.");
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
        var descriptorElement = root.GetProperty("descriptor");
        if (descriptorElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Observation descriptor must be an object.");
        var descriptor = descriptorElement.GetRawText();
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

    internal static bool IsHtmlClubEloDescriptor(JsonElement root)
        => root.TryGetProperty("contract", out var contract)
            && contract.ValueKind == JsonValueKind.String
            && contract.GetString() == "club-elo-official-html-descriptor/v1";

    private static IReadOnlyList<string> EloFieldsFor(JsonElement root)
    {
        if (root.TryGetProperty("contract", out var contract) && contract.ValueKind == JsonValueKind.String)
            return contract.GetString() switch
            {
                "club-elo-direct-csv-descriptor/v1" => EloFields,
                "club-elo-official-html-descriptor/v1" => HtmlEloFields,
                _ => throw new InvalidDataException("Club Elo descriptor contract is invalid.")
            };
        throw new InvalidDataException("Club Elo descriptor contract is invalid.");
    }

    private static void ValidateElo(JsonElement root, BundesligaContextSourceDisposition disposition)
    {
        if (IsHtmlClubEloDescriptor(root)) { ValidateHtmlElo(root, disposition); return; }
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
            if (evidence.ValueKind != JsonValueKind.Object) throw new InvalidDataException("CSV provider date evidence is required.");
            if (root.GetProperty("providerRatedAt").GetString() != evidence.GetProperty("ratedAt").GetString()) throw new InvalidDataException("Rated-at evidence mismatch.");
            ValidateSourceRows(root.GetProperty("sourceRows"), requireCompleteSet: true);
        }
    }

    private static void ValidateHtmlElo(JsonElement root, BundesligaContextSourceDisposition disposition)
    {
        RequireString(root, "contract", "club-elo-official-html-descriptor/v1");
        RequireString(root, "sourceUrl", "https://clubelo.com/GER");
        RequireString(root, "parserContract", "club-elo-official-html-parser/v1");
        RequireString(root, "tableContract", "club-elo-official-html-table/v1");
        RequireString(root, "nameMappingContract", "bundesliga-2026-27-club-elo-name-map/v1");
        RequireString(root, "nameMappingSha256", ClubEloHtmlNameMappingSha256);
        var header = root.GetProperty("tableHeader");
        if (header.ValueKind != JsonValueKind.Array || !header.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).SequenceEqual(["Club", "Elo", "+/-", "Golo"], StringComparer.Ordinal))
            throw new InvalidDataException("Club Elo HTML table header is invalid.");
        OptionalSha(root, "rawSha256"); OptionalNonnegativeInt64(root, "rawByteLength", requireFirestoreRoundTrip: true);
        ValidateHtmlResponse(root.GetProperty("response"));
        var displayedDate = root.GetProperty("displayedDate");
        if (displayedDate.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) throw new InvalidDataException("Displayed date has the wrong type.");
        if (displayedDate.ValueKind == JsonValueKind.String) RequireDate(root, "displayedDate");
        var evidence = root.GetProperty("providerDateEvidence");
        if (evidence.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object)) throw new InvalidDataException("Provider date evidence has the wrong type.");
        if (evidence.ValueKind == JsonValueKind.Object) ValidateHtmlDateEvidence(evidence, displayedDate.ValueKind == JsonValueKind.String ? displayedDate.GetString()! : null);
        var rows = root.GetProperty("sourceRows");
        if (rows.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) throw new InvalidDataException("Club Elo HTML source rows have the wrong type.");
        if (rows.ValueKind == JsonValueKind.Array) ValidateHtmlSourceRows(rows, false);
        var evaluation = RequireOneOf(root, "evaluation", "Eligible", "TransportRejected", "SizeRejected", "ResponseRejected", "DomRejected", "LexerRejected", "FragmentRejected", "DateRejected", "MappingRejected", "CoverageRejected", "StaleRejected", "NotNewer");
        if ((evaluation == "Eligible") != (disposition == BundesligaContextSourceDisposition.ArtifactCaptured) || disposition == BundesligaContextSourceDisposition.MetadataUnchanged)
            throw new InvalidDataException("Club Elo HTML disposition/evaluation conflict.");
        if (evaluation == "TransportRejected")
        {
            RequireNulls(root, "response", "rawSha256", "rawByteLength", "displayedDate", "providerDateEvidence", "sourceRows");
            return;
        }
        RequireHtmlResponse(root.GetProperty("response"));
        if (evaluation == "SizeRejected")
        {
            RequireNulls(root, "displayedDate", "providerDateEvidence", "sourceRows");
            var declared = root.GetProperty("response").GetProperty("declaredContentLength");
            if (root.GetProperty("rawSha256").ValueKind == JsonValueKind.Null && root.GetProperty("rawByteLength").ValueKind == JsonValueKind.Null)
            {
                // Advertised oversize and an E1-proved streaming M+1 rejection both lack a
                // completed raw pair. The bounded-read proof intentionally is not persisted.
                return;
            }
            var rawSha = RequireSha(root, "rawSha256"); var rawLength = RequireNonnegativeInt64(root, "rawByteLength");
            if (rawLength == 0)
            {
                if (rawSha != "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                    || declared.ValueKind != JsonValueKind.Null && declared.GetInt64() > 2097152)
                    throw new InvalidDataException("Complete empty HTML size evidence is invalid.");
            }
            else if (rawLength > 2097152 || declared.ValueKind != JsonValueKind.Number || !declared.TryGetInt64(out var declaredLength) || declaredLength is < 0 or > 2097152 || declaredLength == rawLength)
                throw new InvalidDataException("Complete HTML size-mismatch evidence is invalid.");
            return;
        }
        RequireSha(root, "rawSha256"); RequireNonnegativeInt64(root, "rawByteLength");
        RequireHtmlSizePassed(root);
        if (evaluation is "ResponseRejected" or "DomRejected" or "LexerRejected" or "FragmentRejected")
        {
            if (evaluation == "ResponseRejected" && IsAcceptingHtmlResponse(root.GetProperty("response"))) throw new InvalidDataException("Response rejection requires a failed response predicate.");
            if (evaluation != "ResponseRejected") RequireAcceptingHtmlResponse(root.GetProperty("response"));
            RequireNulls(root, "displayedDate", "providerDateEvidence", "sourceRows");
            return;
        }
        if (evaluation == "DateRejected") { RequireAcceptingHtmlResponse(root.GetProperty("response")); RequireNulls(root, "displayedDate", "providerDateEvidence", "sourceRows"); return; }
        RequireAcceptingHtmlResponse(root.GetProperty("response"));
        RequireDate(root, "displayedDate");
        if (evidence.ValueKind != JsonValueKind.Object) throw new InvalidDataException("HTML date evidence is required.");
        if (evaluation == "MappingRejected") { RequireNull(root, "sourceRows"); return; }
        ValidateHtmlSourceRows(rows, evaluation is "Eligible" or "StaleRejected" or "NotNewer");
        if (evaluation == "CoverageRejected" && rows.GetArrayLength() >= BundesligaTeamManifest.ExpectedTeamCount)
            throw new InvalidDataException("Coverage rejection must retain a canonical proper subset.");
        if (evaluation == "Eligible")
        {
            var response = root.GetProperty("response");
            if (response.GetProperty("statusCode").GetInt32() != 200
                || response.GetProperty("finalUrl").GetString() != "https://clubelo.com/GER"
                || response.GetProperty("redirectCount").GetInt32() != 0
                || response.GetProperty("redirectLocation").ValueKind != JsonValueKind.Null
                || response.GetProperty("mediaType").GetString() != "text/html"
                || response.GetProperty("charset").GetString() != "utf-8"
                || response.GetProperty("contentEncodings").GetArrayLength() != 0)
                throw new InvalidDataException("Eligible HTML response is not accepting.");
            if (response.GetProperty("declaredContentLength").ValueKind != JsonValueKind.Null
                && response.GetProperty("declaredContentLength").GetInt64() != root.GetProperty("rawByteLength").GetInt64())
                throw new InvalidDataException("Eligible HTML declared content length must equal the raw length.");
        }
    }

    private static void ValidateHtmlResponse(JsonElement response)
    {
        if (response.ValueKind == JsonValueKind.Null) return;
        RequireFields(response, HtmlResponseFields);
        RequirePositiveInt32(response, "statusCode", requireFirestoreRoundTrip: true);
        RequireHttps(response, "finalUrl"); RequireNonnegativeInt32(response, "redirectCount", requireFirestoreRoundTrip: true);
        OptionalString(response, "redirectLocation"); OptionalString(response, "mediaType"); OptionalString(response, "charset");
        if (response.GetProperty("contentEncodings").ValueKind != JsonValueKind.Array || response.GetProperty("contentEncodings").EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) throw new InvalidDataException("Response content encodings are invalid.");
        OptionalNonnegativeInt64(response, "declaredContentLength", requireFirestoreRoundTrip: true);
    }

    private static void RequireHtmlResponse(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object) throw new InvalidDataException("HTML response evidence is required.");
    }

    private static void RequireHtmlSizePassed(JsonElement root)
    {
        var length = RequireNonnegativeInt64(root, "rawByteLength");
        if (length is < 1 or > 2097152) throw new InvalidDataException("HTML raw length did not pass the size gate.");
        var declared = root.GetProperty("response").GetProperty("declaredContentLength");
        if (declared.ValueKind != JsonValueKind.Null && (!declared.TryGetInt64(out var value) || value != length))
            throw new InvalidDataException("HTML declared length did not pass the size gate.");
    }

    private static void RequireAcceptingHtmlResponse(JsonElement response)
    {
        if (!IsAcceptingHtmlResponse(response)) throw new InvalidDataException("HTML response did not pass the accepting-response gate.");
    }

    private static bool IsAcceptingHtmlResponse(JsonElement response)
    {
        return response.GetProperty("statusCode").GetInt32() == 200
            && response.GetProperty("finalUrl").GetString() == "https://clubelo.com/GER"
            && response.GetProperty("redirectCount").GetInt32() == 0
            && response.GetProperty("redirectLocation").ValueKind == JsonValueKind.Null
            && response.GetProperty("mediaType").GetString() == "text/html"
            && response.GetProperty("charset").GetString() == "utf-8"
            && response.GetProperty("contentEncodings").GetArrayLength() == 0;
    }

    private static void ValidateHtmlDateEvidence(JsonElement evidence, string? displayedDate)
    {
        RequireFields(evidence, DateEvidenceFields);
        RequireString(evidence, "kind", "OfficialHtmlHeadingLink"); RequireString(evidence, "recipeId", "club-elo-official-html-displayed-date/v1"); RequireString(evidence, "field", "h1>a[href]");
        var raw = RequireString(evidence, "rawValue"); var ratedAt = RequireDate(evidence, "ratedAt");
        if (raw.Length != 10 || raw != ratedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) || displayedDate != raw)
            throw new InvalidDataException("HTML displayed date evidence is not canonical.");
    }

    private static void ValidateHtmlSourceRows(JsonElement rows, bool requireCompleteSet)
    {
        if (rows.ValueKind != JsonValueKind.Array || requireCompleteSet && rows.GetArrayLength() != 18) throw new InvalidDataException("HTML Club Elo source rows are invalid.");
        var slugs = new List<string>(); var routes = new HashSet<string>(StringComparer.Ordinal); var names = new HashSet<string>(StringComparer.Ordinal); var ranks = new HashSet<int>(); string? previous = null;
        foreach (var row in rows.EnumerateArray())
        {
            RequireFields(row, HtmlSourceRowFields); var slug = RequireString(row, "teamSlug");
            if (previous is not null && string.CompareOrdinal(previous, slug) >= 0) throw new InvalidDataException("Club Elo rows are not manifest-slug ordered.");
            previous = slug; slugs.Add(slug);
            var route = RequireString(row, "providerRoute"); var name = RequireString(row, "providerDisplayName");
            if (!HtmlClubEloMapping.TryGetValue(slug, out var expected) || route != expected.Route || name != expected.DisplayName)
                throw new InvalidDataException("HTML Club Elo route/name mapping is not the accepted canonical mapping.");
            if (!routes.Add(route) || !names.Add(name) || !ranks.Add(RequirePositiveInt32(row, "globalRank", requireFirestoreRoundTrip: true))) throw new InvalidDataException("HTML Club Elo identities are not unique.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(route, "^/[A-Za-z0-9][A-Za-z0-9-]{0,127}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) || route == "/GER") throw new InvalidDataException("HTML Club Elo route is unsafe.");
            RequirePositiveInt32(row, "elo", requireFirestoreRoundTrip: true);
        }
        if (requireCompleteSet && (!slugs.SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(entry => entry.TeamSlug), StringComparer.Ordinal)
            || HtmlClubEloMapping.Count != BundesligaTeamManifest.ExpectedTeamCount)) throw new InvalidDataException("Eligible Club Elo source rows must exactly match the canonical Bundesliga team manifest.");
    }

    public static DateOnly? ClubEloRatedAt(string descriptorJson)
    {
        using var document = JsonDocument.Parse(descriptorJson); var root = document.RootElement;
        var name = IsHtmlClubEloDescriptor(root) ? "displayedDate" : "providerRatedAt";
        return root.GetProperty(name).ValueKind == JsonValueKind.Null ? null : DateOnly.ParseExact(root.GetProperty(name).GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void ValidateHtmlFreshness(string descriptorJson, DateTimeOffset observedAtUtc)
    {
        using var document = JsonDocument.Parse(descriptorJson); var root = document.RootElement;
        if (!IsHtmlClubEloDescriptor(root)) return;
        var evaluation = root.GetProperty("evaluation").GetString();
        if (evaluation is not ("Eligible" or "StaleRejected" or "NotNewer")) return;
        var ratedAt = ClubEloRatedAt(descriptorJson)!.Value;
        var observed = DateOnly.FromDateTime(observedAtUtc.UtcDateTime);
        var age = observed.DayNumber - ratedAt.DayNumber;
        if (ratedAt > observed || evaluation == "Eligible" && age > 7 || evaluation == "StaleRejected" && age <= 7 || evaluation == "NotNewer" && age > 7)
            throw new InvalidDataException("HTML evaluation contradicts represented displayed-date freshness.");
    }

    public static void ValidateClubEloDiagnostics(string descriptorJson, BundesligaContextSourceDisposition disposition, IReadOnlyList<string> diagnostics)
    {
        using var document = JsonDocument.Parse(descriptorJson); var root = document.RootElement;
        if (!IsHtmlClubEloDescriptor(root)) return;
        var evaluation = root.GetProperty("evaluation").GetString()!;
        var expected = evaluation switch
        {
            "Eligible" => null, "TransportRejected" => "CLUB_ELO_TRANSPORT_REJECTED", "SizeRejected" => "CLUB_ELO_SIZE_REJECTED", "ResponseRejected" => "CLUB_ELO_RESPONSE_REJECTED", "DomRejected" => "CLUB_ELO_DOM_REJECTED", "LexerRejected" => "CLUB_ELO_LEXER_REJECTED", "FragmentRejected" => "CLUB_ELO_FRAGMENT_REJECTED", "DateRejected" => "CLUB_ELO_DISPLAYED_DATE_REJECTED", "MappingRejected" => "CLUB_ELO_MAPPING_REJECTED", "CoverageRejected" => "CLUB_ELO_COVERAGE_REJECTED", "StaleRejected" => "CLUB_ELO_STALE_GT_7_DAYS", "NotNewer" => "CLUB_ELO_NOT_NEWER", _ => throw new InvalidDataException("HTML evaluation is invalid.")
        };
        if (expected is null) { if (diagnostics.Count != 0) throw new InvalidDataException("Eligible HTML observations cannot have diagnostics."); return; }
        if (disposition != BundesligaContextSourceDisposition.Rejected || diagnostics.Count == 0 || !diagnostics.Contains(expected, StringComparer.Ordinal)) throw new InvalidDataException("HTML diagnostic does not match its evaluation.");
        var terminal = new[] { "CLUB_ELO_CONNECTION_FAILED", "CLUB_ELO_TIMEOUT", "CLUB_ELO_HTTP_REJECTED" };
        if (evaluation == "TransportRejected")
        {
            if (diagnostics.Count > 2 || diagnostics.Any(value => value != expected && !terminal.Contains(value, StringComparer.Ordinal))) throw new InvalidDataException("HTML transport diagnostics are invalid.");
        }
        else if (diagnostics.Count != 1 || diagnostics[0] != expected)
            throw new InvalidDataException("Only transport rejection may carry a terminal cause.");
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
        RequireString(root, "contract", "transfermarkt-duckdb-observation-descriptor/v1");
        RequireString(root, "metadataUrl", RosterMetadataUrl);
        RequireString(root, "artifactUrl", RosterArtifactUrl);
        var revision = OptionalLowerHex(root, "advertisedRevision", 40);
        if (RequireSha(root, "policySha256") != RosterPolicySha256)
            throw new InvalidDataException("Roster policy SHA-256 does not match the frozen ADR-0074 policy.");
        var reasonElement = root.GetProperty("acquisitionReason");
        if (reasonElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) throw new InvalidDataException("'acquisitionReason' has the wrong type.");
        var acquisitionReason = reasonElement.ValueKind == JsonValueKind.Null ? null : RequireOneOf(root, "acquisitionReason", "NewRevision", "PendingRevision", "RemoteIdentityChanged", "PolicyChanged", "AcceptedRevisionUnchanged");
        var evaluation = RequireOneOf(root, "evaluation", "MetadataUnavailable", "MetadataMalformed", "MetadataRevisionRejected", "MetadataUnchanged", "RemoteIdentityUnavailable", "ArtifactTransportRejected", "SizeRejected", "RemoteDriftRejected", "HashRejected", "RevisionRejected", "SchemaRejected", "SourceDateRejected", "SeasonRejected", "IdentityRejected", "Eligible");
        if ((evaluation == "Eligible") != (disposition == BundesligaContextSourceDisposition.ArtifactCaptured) || (evaluation == "MetadataUnchanged") != (disposition == BundesligaContextSourceDisposition.MetadataUnchanged)) throw new InvalidDataException("Roster disposition/evaluation conflict.");
        if (evaluation == "MetadataUnchanged")
        {
            if (revision is null || acquisitionReason != "AcceptedRevisionUnchanged") throw new InvalidDataException("MetadataUnchanged requires AcceptedRevisionUnchanged.");
            RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength"); ValidateRemoteIdentity(root.GetProperty("remoteIdentityBefore"));
            RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
            RequireSha(root, "retainedDescriptorSha256");
            var retainedEvaluation = RequireOneOf(root, "retainedEvaluation", "Eligible", "SchemaRejected", "SourceDateRejected", "SeasonRejected", "IdentityRejected");
            var retainedDiagnostics = RequireStringArray(root, "retainedDiagnostics");
            // Retained evidence is a prior observation outcome and has exactly the same
            // evaluation/primary/precedence contract as an observation diagnostic list.
            ValidateRosterEvaluationPrecedence(retainedEvaluation,
                retainedEvaluation == "Eligible" ? BundesligaContextSourceDisposition.ArtifactCaptured : BundesligaContextSourceDisposition.Rejected,
                retainedDiagnostics);
        }
        else
        {
            if (acquisitionReason == "AcceptedRevisionUnchanged") throw new InvalidDataException("AcceptedRevisionUnchanged may only produce MetadataUnchanged.");
            RequireNull(root, "retainedDescriptorSha256"); RequireNull(root, "retainedEvaluation"); var retained = root.GetProperty("retainedDiagnostics"); if (retained.ValueKind != JsonValueKind.Array || retained.GetArrayLength() != 0) throw new InvalidDataException("Non-metadata retained diagnostics must be empty.");
        }

        switch (evaluation)
        {
            case "MetadataUnavailable":
                RequireNulls(root, "advertisedRevision", "metadataSha256", "metadataByteLength", "remoteIdentityBefore", "acquisitionReason", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "MetadataMalformed":
            case "MetadataRevisionRejected":
                if (revision is not null || acquisitionReason is not null) throw new InvalidDataException("Rejected metadata cannot establish a revision or acquisition reason.");
                RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength");
                RequireNulls(root, "remoteIdentityBefore", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "MetadataUnchanged":
                break;
            case "RemoteIdentityUnavailable":
                if (revision is null) throw new InvalidDataException("Remote identity evaluation requires an advertised revision.");
                RequireSha(root, "metadataSha256"); RequireNonnegativeInt64(root, "metadataByteLength");
                RequireNulls(root, "remoteIdentityBefore", "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                if (acquisitionReason is not null && acquisitionReason is not ("NewRevision" or "PendingRevision" or "PolicyChanged"))
                    throw new InvalidDataException("Remote identity unavailable cannot claim an identity-dependent acquisition reason.");
                break;
            case "ArtifactTransportRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Artifact transport rejection requires a revision and acquisition reason.");
                RequireMetadataAndBefore(root);
                RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "expectedRawSha256", "rawByteLength", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "SizeRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Size rejection requires a revision and acquisition reason.");
                RequireMetadataAndBefore(root);
                if (RequireNonnegativeInt64(root, "rawByteLength") <= MaximumRosterArtifactBytes) throw new InvalidDataException("SizeRejected requires a raw byte length over 300 MiB.");
                OptionalSha(root, "expectedRawSha256");
                RequireNulls(root, "remoteIdentityAfter", "embeddedRevision", "rawSha256", "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate");
                break;
            case "RemoteDriftRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Remote drift rejection requires a revision and acquisition reason.");
                RequireMetadataAndBefore(root); ValidateRemoteIdentity(root.GetProperty("remoteIdentityAfter"));
                if (RemoteIdentitiesEqual(root)) throw new InvalidDataException("RemoteDriftRejected requires unequal identities.");
                OptionalLowerHex(root, "embeddedRevision", 40); RequireSha(root, "rawSha256"); OptionalSha(root, "expectedRawSha256"); RequireNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
                break;
            case "HashRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Hash rejection requires a revision and acquisition reason.");
                RequireMetadataAndEqualIdentities(root); OptionalLowerHex(root, "embeddedRevision", 40);
                var hashActual = RequireSha(root, "rawSha256"); var hashExpected = RequireSha(root, "expectedRawSha256");
                if (hashActual == hashExpected) throw new InvalidDataException("HashRejected requires unequal actual and expected hashes.");
                RequireNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
                break;
            case "RevisionRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Revision rejection requires a revision and acquisition reason.");
                RequireMetadataAndEqualIdentities(root); var embedded = OptionalLowerHex(root, "embeddedRevision", 40);
                if (embedded == revision) throw new InvalidDataException("RevisionRejected requires a missing or unequal embedded revision.");
                var revisionActual = RequireSha(root, "rawSha256"); RequireExpectedHashPassedOrAbsent(root, revisionActual); RequireNonnegativeInt64(root, "rawByteLength"); ValidateObservedDates(root);
                break;
            case "SchemaRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Schema rejection requires a revision and acquisition reason.");
                ValidateIdentifiedArtifact(root, revision, requireAllDates: false); break;
            case "SourceDateRejected":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Source-date rejection requires a revision and acquisition reason.");
                ValidateIdentifiedArtifact(root, revision, requireAllDates: false);
                if (new[] { "artifactCaptureDate", "membershipEffectiveDate", "enrichmentCaptureDate" }.All(name => root.GetProperty(name).ValueKind != JsonValueKind.Null)) throw new InvalidDataException("SourceDateRejected requires at least one unknown source date.");
                break;
            case "SeasonRejected":
            case "IdentityRejected":
            case "Eligible":
                if (revision is null || acquisitionReason is null) throw new InvalidDataException("Identified artifact evaluation requires a revision and acquisition reason.");
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
        if (source == BundesligaContextSource.ClubElo)
        {
            var expectedPath = IsHtmlClubEloDescriptor(root) ? "club-elo/source.html" : "club-elo/source.csv";
            if (payload.Path != expectedPath) throw new InvalidDataException("Descriptor-selected payload identity is invalid.");
        }
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
    private static DateOnly RequireDate(JsonElement root, string name) { var value = RequireString(root, name); if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var parsed) || parsed.ToString("yyyy-MM-dd") != value) throw new InvalidDataException($"'{name}' is not a canonical date."); return parsed; }
    private static long RequireNonnegativeInt64(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt64(out var value) || value < 0) throw new InvalidDataException($"'{name}' is not a non-negative Int64."); RequireCanonicalNumber(p, value); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static int RequirePositiveInt32(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out var value) || value <= 0) throw new InvalidDataException($"'{name}' is not a positive Int32."); RequireCanonicalNumber(p, value); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static int RequireNonnegativeInt32(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out var value) || value < 0) throw new InvalidDataException($"'{name}' is not a non-negative Int32."); RequireCanonicalNumber(p, value); if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p); return value; }
    private static double RequirePositiveFiniteDouble(JsonElement root, string name, bool requireFirestoreRoundTrip = false)
    {
        var p = root.GetProperty(name);
        if (p.ValueKind != JsonValueKind.Number) throw new InvalidDataException($"'{name}' is not a positive finite number.");
        if (p.TryGetInt64(out var integer))
        {
            if (integer <= 0) throw new InvalidDataException($"'{name}' is not a positive finite number.");
            RequireCanonicalNumber(p, integer);
            if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p);
            return integer;
        }
        if (!p.TryGetDouble(out var value) || !double.IsFinite(value) || value <= 0) throw new InvalidDataException($"'{name}' is not a positive finite number.");
        RequireCanonicalNumber(p, value);
        if (requireFirestoreRoundTrip) RequireFirestoreRoundtrippableNumber(p);
        return value;
    }
    private static void RequireCanonicalNumber(JsonElement value, object parsed)
    {
        if (value.GetRawText() != JsonSerializer.Serialize(parsed))
            throw new InvalidDataException("Numeric values must use canonical JSON spelling.");
    }
    private static void RequireFirestoreRoundtrippableNumber(JsonElement value)
    {
        var firestoreValue = value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble();
        if (value.GetRawText() != JsonSerializer.Serialize(firestoreValue))
            throw new InvalidDataException("Club Elo numeric values must use their canonical Firestore-roundtrippable spelling.");
    }
    private static string[] RequireStringArray(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind != JsonValueKind.Array || p.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) throw new InvalidDataException($"'{name}' must be a string array."); var values = p.EnumerateArray().Select(x => x.GetString()!).ToArray(); if (values.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException($"'{name}' must contain nonempty strings."); return values; }
    private static void OptionalString(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) throw new InvalidDataException($"'{name}' has the wrong type."); }
    private static void OptionalSha(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireSha(root, name); }
    private static void OptionalNonnegativeInt64(JsonElement root, string name, bool requireFirestoreRoundTrip = false) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireNonnegativeInt64(root, name, requireFirestoreRoundTrip); }
    private static void OptionalDate(JsonElement root, string name) { var p = root.GetProperty(name); if (p.ValueKind == JsonValueKind.Null) return; RequireDate(root, name); }
    private static string? OptionalLowerHex(JsonElement root, string name, int length) { var p = root.GetProperty(name); return p.ValueKind == JsonValueKind.Null ? null : RequireLowerHex(root, name, length); }
    private static void ValidateRemoteIdentity(JsonElement identity) { RequireFields(identity, RemoteIdentityFields); var etag = identity.GetProperty("etag"); var length = identity.GetProperty("byteLength"); if (etag.ValueKind == JsonValueKind.Null && length.ValueKind == JsonValueKind.Null) throw new InvalidDataException("Remote identity must have an ETag or byte length."); if (etag.ValueKind is not (JsonValueKind.Null or JsonValueKind.String) || etag.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(etag.GetString())) throw new InvalidDataException("Remote ETag is invalid."); if (length.ValueKind != JsonValueKind.Null) { if (length.ValueKind != JsonValueKind.Number || !length.TryGetInt64(out var n) || n < 0) throw new InvalidDataException("Remote byte length is invalid."); RequireCanonicalNumber(length, n); } }
}
