using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// The immutable prompt-document contract accepted in ADR-0015.
/// </summary>
public static class BundesligaClubEloPublication
{
    public const string CsvHeader = "Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At";
    public const string MetadataSchemaVersion = "club-elo-publication-v1";
    public const string MetadataSchemaVersionV2 = "club-elo-publication-v2";
    public const string RankPolicy = "elo-desc-global-rank-asc-manifest-slug-ordinal-sequential";
    public const string KpiDescription = "Bundesliga 2026/27 Club Elo rankings for all 18 manifest teams.";

    // These are deliberately private: v2 is a persisted contract, not a new public API.
    private static readonly string[] V1Properties = ["schema_version", "rated_at", "collected_at", "source_url", "selected_origin", "selection_disposition", "selection_diagnostics", "manifest_team_count", "rank_policy"];
    private static readonly string[] CsvV2Properties = [..V1Properties, "cycle_id", "attempt_id", "source_observed_at", "raw_sha256", "raw_byte_length", "provider_date_evidence", "name_mapping_sha256", "source_rows"];
    private static readonly string[] HtmlV2Properties = [..CsvV2Properties.Take(14), "displayed_date", ..CsvV2Properties.Skip(14), "sourceDescriptorSha256", "sourceDescriptor", "selectedPayload"];

    private enum MetadataFamily { V1, CsvV2, HtmlV2 }
    private enum DescriptorFamily { Csv, Html }

    public static BundesligaClubEloPublicationBuild Build(BundesligaClubEloSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ValidateSnapshotProvenance(selection.Selected);
        var diagnostics = ValidateDiagnostics(selection.Diagnostics);
        ValidateSelectionMetadata(selection.Selected.Origin, selection.Disposition, diagnostics);
        var ranked = Rank(selection.Selected);
        var documents = ranked
            .OrderBy(row => row.Entry.Team.TeamSlug, StringComparer.Ordinal)
            .Select(row => new DocumentPublicationPayload(
                DocumentPublicationKind.Context,
                $"club-elo-{row.Entry.Team.TeamSlug}.csv",
                Render([row])))
            .Append(new DocumentPublicationPayload(
                DocumentPublicationKind.Kpi,
                BundesligaDocumentPublication.ClubEloRankingsDocumentName,
                Render(ranked),
                KpiDescription))
            .ToArray();

        return new BundesligaClubEloPublicationBuild(
            selection.Selected,
            ranked,
            documents,
            JsonSerializer.Serialize(new
            {
                schema_version = MetadataSchemaVersion,
                rated_at = selection.Selected.RatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                collected_at = selection.Selected.CollectedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                source_url = selection.Selected.SourceUrl.AbsoluteUri,
                selected_origin = selection.Selected.Origin.ToString(),
                selection_disposition = selection.Disposition.ToString(),
                selection_diagnostics = diagnostics,
                manifest_team_count = BundesligaTeamManifest.ExpectedTeamCount,
                rank_policy = RankPolicy
            }));
    }

    public static DocumentPublicationRequest CreateRequest(
        string communityContext,
        string? expectedPreviousSnapshotId,
        BundesligaClubEloPublicationBuild publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return new DocumentPublicationRequest(
            communityContext,
            expectedPreviousSnapshotId,
            publication.Documents,
            publication.MetadataJson);
    }

    /// <summary>Builds the source-backed HTML-v2 form without changing the historical v1 API.</summary>
    public static BundesligaClubEloPublicationBuild BuildSourceBacked(
        BundesligaClubEloSelection selection,
        BundesligaContextSourceCycleIdentity cycle,
        BundesligaContextSourceObservation observation,
        byte[] immutablePayload)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(immutablePayload);
        ValidateCycle(cycle);
        observation.Validate();
        using var descriptorFamilyDocument = JsonDocument.Parse(observation.DescriptorJson);
        if (ClassifyDescriptor(descriptorFamilyDocument.RootElement) == DescriptorFamily.Csv)
            return BuildHistoricalCsvV2(selection, cycle, observation, immutablePayload, descriptorFamilyDocument.RootElement);
        if (observation.Source != BundesligaContextSource.ClubElo
            || observation.Disposition != BundesligaContextSourceDisposition.ArtifactCaptured
            || observation.Payload is not { Path: "club-elo/source.html" } payload
            || observation.AttemptId != BundesligaContextSourceHashing.AttemptId(cycle, observation.Source)
            || selection.Disposition != BundesligaClubEloSelectionDisposition.NetworkAccepted
            || selection.Selected.Origin != BundesligaClubEloSnapshotOrigin.NetworkCandidate
            || selection.Diagnostics.Count != 0
            || immutablePayload.LongLength != payload.ByteLength
            || BundesligaContextSourceHashing.Sha256(immutablePayload) != payload.Sha256)
            throw new InvalidDataException("HTML-v2 Club Elo publication inputs are inconsistent.");

        using var descriptorDocument = JsonDocument.Parse(observation.DescriptorJson);
        var descriptor = descriptorDocument.RootElement;
        if (ClassifyDescriptor(descriptor) != DescriptorFamily.Html
            || RequiredString(descriptor, "evaluation") != "Eligible"
            || descriptor.GetProperty("rawSha256").GetString() != payload.Sha256
            || descriptor.GetProperty("rawByteLength").GetInt64() != payload.ByteLength)
            throw new InvalidDataException("HTML-v2 descriptor/payload identity is inconsistent.");
        var displayedDate = DateOnly.ParseExact(descriptor.GetProperty("displayedDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (selection.Selected.RatedAt != displayedDate || selection.Selected.CollectedAt != observation.ObservedAtUtc
            || selection.Selected.SourceUrl.AbsoluteUri != "https://clubelo.com/GER")
            throw new InvalidDataException("HTML-v2 selection provenance is inconsistent.");
        ValidateHtmlRowsAgainstSelection(descriptor.GetProperty("sourceRows"), selection.Selected);
        var historical = Build(selection);
        return historical with { MetadataJson = CreateSourceBackedMetadata(selection, cycle, observation, payload, descriptor) };
    }

    /// <summary>Serializes historical CSV-v2 evidence verbatim; publication binding is intentionally stricter.</summary>
    public static BundesligaClubEloPublicationBuild BuildHistoricalCsvV2(
        BundesligaClubEloSelection selection, BundesligaContextSourceCycleIdentity cycle,
        BundesligaContextSourceObservation observation, byte[] immutablePayload)
    {
        using var descriptor = JsonDocument.Parse(observation.DescriptorJson);
        return BuildHistoricalCsvV2(selection, cycle, observation, immutablePayload, descriptor.RootElement);
    }

    /// <summary>
    /// Retains historical CSV-v2 evidence without asserting that its numeric tokens can form a
    /// Club Elo document snapshot. In particular, a canonical fractional token stays fractional.
    /// </summary>
    public static string CreateHistoricalCsvV2Evidence(BundesligaClubEloSelection selection,
        BundesligaContextSourceCycleIdentity cycle, BundesligaContextSourceObservation observation, byte[] immutablePayload)
    {
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(observation); ArgumentNullException.ThrowIfNull(immutablePayload);
        ValidateCycle(cycle); observation.Validate();
        using var descriptorDocument = JsonDocument.Parse(observation.DescriptorJson); var descriptor = descriptorDocument.RootElement;
        ValidateCsvV2CommonTruth(selection, cycle, observation, immutablePayload, descriptor);
        var payload = observation.Payload!;
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); WriteV1Prefix(writer, selection); writer.WriteString("cycle_id", cycle.CycleId); writer.WriteString("attempt_id", observation.AttemptId);
            writer.WriteString("source_observed_at", BundesligaContextSourceContract.FormatUtc(observation.ObservedAtUtc)); writer.WriteString("raw_sha256", payload.Sha256); writer.WriteNumber("raw_byte_length", payload.ByteLength);
            WriteCsvEvidence(writer, descriptor); writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static BundesligaClubEloPublicationBuild BuildHistoricalCsvV2(BundesligaClubEloSelection selection,
        BundesligaContextSourceCycleIdentity cycle, BundesligaContextSourceObservation observation, byte[] immutablePayload, JsonElement descriptor)
    {
        ValidateCycle(cycle); observation.Validate();
        ValidateCsvV2CommonTruth(selection, cycle, observation, immutablePayload, descriptor);
        ValidateCsvSnapshotBinding(descriptor.GetProperty("sourceRows"), selection.Selected);
        return Build(selection) with { MetadataJson = CreateHistoricalCsvV2Evidence(selection, cycle, observation, immutablePayload) };
    }

    /// <summary>
    /// Reconstructs a valid LKG source snapshot only from the headed, exact publication payloads
    /// and durable ADR-0015 metadata. It deliberately rejects equivalent-but-noncanonical CSV.
    /// </summary>
    public static BundesligaClubEloSnapshot ReconstructLastKnownGood(LoadedDocumentPublication loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        DocumentPublicationContract.ValidateLoaded(
            CompetitionIds.Bundesliga2026_27,
            loaded.Snapshot.CommunityContext,
            BundesligaDocumentPublication.ClubElo,
            loaded.Snapshot,
            loaded.Documents);

        var metadata = ParseMetadata(loaded.Snapshot.MetadataJson);
        var contextRows = new List<BundesligaClubEloRankedEntry>();
        foreach (var team in BundesligaTeamManifest.Default.Entries.OrderBy(team => team.TeamSlug, StringComparer.Ordinal))
        {
            var name = $"club-elo-{team.TeamSlug}.csv";
            var document = loaded.Documents.Single(document => document.Kind == DocumentPublicationKind.Context && document.Name == name);
            var rows = ReadRows(document.Content, name);
            if (rows.Count != 1 || !string.Equals(rows[0].Team, team.ClubEloName, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Club Elo LKG document '{name}' must contain exactly its manifest team row.");
            }

            if (rows[0].RatedAt != metadata.RatedAt)
            {
                throw new InvalidDataException($"Club Elo LKG document '{name}' Rated_At does not match metadata.");
            }

            contextRows.Add(new BundesligaClubEloRankedEntry(
                new BundesligaClubEloEntry(team, rows[0].GlobalRank, rows[0].Elo), rows[0].BundesligaRank, rows[0].RatedAt));
        }

        var reconstructedSelectedSnapshot = BundesligaClubEloSnapshot.Create(
            contextRows.OrderBy(row => row.Entry.Team.TeamSlug, StringComparer.Ordinal).Select(row => row.Entry).ToArray(),
            metadata.RatedAt,
            metadata.CollectedAt,
            metadata.SourceUrl,
            metadata.SelectedOrigin);
        if (reconstructedSelectedSnapshot.RatedAt != metadata.RatedAt
            || reconstructedSelectedSnapshot.CollectedAt != metadata.CollectedAt
            || reconstructedSelectedSnapshot.SourceUrl != metadata.SourceUrl
            || reconstructedSelectedSnapshot.Origin != metadata.SelectedOrigin)
        {
            throw new InvalidDataException("Club Elo LKG metadata provenance does not match the reconstructed snapshot.");
        }

        var expectedRanked = Rank(reconstructedSelectedSnapshot);
        if (!contextRows.OrderBy(row => row.Entry.Team.TeamSlug, StringComparer.Ordinal)
                .SequenceEqual(expectedRanked.OrderBy(row => row.Entry.Team.TeamSlug, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Club Elo LKG per-team Bundesliga ranks do not match the canonical rank policy.");
        }

        foreach (var expected in expectedRanked)
        {
            var name = $"club-elo-{expected.Entry.Team.TeamSlug}.csv";
            var actual = loaded.Documents.Single(document => document.Kind == DocumentPublicationKind.Context && document.Name == name);
            if (!string.Equals(actual.Content, Render([expected]), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Club Elo LKG document '{name}' is not the exact canonical single-row CSV.");
            }
        }

        var aggregate = loaded.Documents.Single(document => document.Kind == DocumentPublicationKind.Kpi
            && document.Name == BundesligaDocumentPublication.ClubEloRankingsDocumentName);
        if (!string.Equals(aggregate.Content, Render(expectedRanked), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Club Elo LKG aggregate is not the exact canonical aggregate CSV.");
        }

        var snapshot = BundesligaClubEloSnapshot.Create(
            expectedRanked.OrderBy(row => row.Entry.Team.TeamSlug, StringComparer.Ordinal).Select(row => row.Entry).ToArray(),
            reconstructedSelectedSnapshot.RatedAt,
            reconstructedSelectedSnapshot.CollectedAt,
            reconstructedSelectedSnapshot.SourceUrl,
            BundesligaClubEloSnapshotOrigin.LastKnownGood);
        ValidateV2RowsAgainstHeadedSnapshot(loaded.Snapshot.MetadataJson, snapshot);
        return snapshot;
    }

    public static IReadOnlyList<BundesligaClubEloRankedEntry> Rank(BundesligaClubEloSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Entries
            .OrderByDescending(entry => entry.Elo)
            .ThenBy(entry => entry.GlobalRank)
            .ThenBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal)
            .Select((entry, index) => new BundesligaClubEloRankedEntry(entry, index + 1, snapshot.RatedAt))
            .ToArray();
    }

    public static string Render(IEnumerable<BundesligaClubEloRankedEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = entries.ToArray();
        var builder = new System.Text.StringBuilder(CsvHeader).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(row.Entry.GlobalRank.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.BundesligaRank.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Entry.Team.ClubEloName).Append(',')
                .Append(row.Entry.Elo.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.RatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("\r\n");
        }

        return builder.ToString();
    }

    public static BundesligaClubEloPublicationMetadata ParseMetadata(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Club Elo LKG metadata must be a JSON object.");
            }

            var family = ClassifyMetadata(root);

            var ratedAtValue = RequiredString(root, "rated_at");
            var collectedAtValue = RequiredString(root, "collected_at");
            var sourceUrlValue = RequiredString(root, "source_url");
            var selectedOrigin = RequiredString(root, "selected_origin");
            var selectionDisposition = RequiredString(root, "selection_disposition");
            var rankPolicy = RequiredString(root, "rank_policy");
            var diagnostics = ParseDiagnostics(root.GetProperty("selection_diagnostics"));
            if (!string.Equals(rankPolicy, RankPolicy, StringComparison.Ordinal)
                || root.GetProperty("manifest_team_count").GetInt32() != BundesligaTeamManifest.ExpectedTeamCount
                || !Enum.GetNames<BundesligaClubEloSnapshotOrigin>().Contains(selectedOrigin, StringComparer.Ordinal)
                || !Enum.GetNames<BundesligaClubEloSelectionDisposition>().Contains(selectionDisposition, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Club Elo LKG metadata does not match the ADR-0015 contract.");
            }

            if (!DateOnly.TryParseExact(ratedAtValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ratedAt)
                || !DateTimeOffset.TryParseExact(collectedAtValue, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var collectedAt)
                || !Uri.TryCreate(sourceUrlValue, UriKind.Absolute, out var sourceUrl)
                || !string.Equals(sourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(sourceUrl.AbsoluteUri, sourceUrlValue, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Club Elo LKG metadata has invalid snapshot provenance.");
            }

            var origin = Enum.Parse<BundesligaClubEloSnapshotOrigin>(selectedOrigin, ignoreCase: false);
            var disposition = Enum.Parse<BundesligaClubEloSelectionDisposition>(selectionDisposition, ignoreCase: false);
            ValidateSelectionMetadata(origin, disposition, diagnostics);
            if (family == MetadataFamily.HtmlV2)
            {
                ValidateSourceBackedMetadata(root, ratedAt, collectedAt, sourceUrl, origin, disposition, diagnostics);
            }
            else if (family == MetadataFamily.CsvV2)
            {
                ValidateCsvV2Metadata(root, ratedAt, collectedAt, sourceUrl, origin, disposition, diagnostics);
            }
            return new BundesligaClubEloPublicationMetadata(ratedAt, collectedAt, sourceUrl, origin, disposition, diagnostics);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Club Elo LKG metadata must be valid JSON.", exception);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException("Club Elo LKG metadata is missing a required property.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("Club Elo LKG metadata has invalid property types.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Club Elo LKG metadata has invalid canonical values.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Club Elo LKG metadata has out-of-range values.", exception);
        }
    }

    /// <summary>
    /// The persisted metadata family is selected by its complete ordered shape.  In particular,
    /// the presence of a tempting v2 property is never a discriminator: mixed generations are
    /// invalid before any individual value is read.
    /// </summary>
    private static MetadataFamily ClassifyMetadata(JsonElement root)
    {
        var schema = RequiredString(root, "schema_version");
        var actual = root.EnumerateObject().Select(property => property.Name).ToArray();
        if (schema == MetadataSchemaVersion && actual.SequenceEqual(V1Properties, StringComparer.Ordinal)) return MetadataFamily.V1;
        if (schema == MetadataSchemaVersionV2 && actual.SequenceEqual(CsvV2Properties, StringComparer.Ordinal)) return MetadataFamily.CsvV2;
        if (schema == MetadataSchemaVersionV2 && actual.SequenceEqual(HtmlV2Properties, StringComparer.Ordinal)) return MetadataFamily.HtmlV2;
        throw new InvalidDataException("Club Elo metadata must be exactly one ordered v1, CSV-v2, or HTML-v2 family.");
    }

    private static DescriptorFamily ClassifyDescriptor(JsonElement descriptor)
    {
        // Observation.Validate has already checked the complete descriptor shape, canonical
        // spelling and disposition matrix.  This classifier only dispatches its exclusive
        // public family, so the publication layer cannot treat an unknown descriptor as CSV.
        return RequiredString(descriptor, "contract") switch
        {
            "club-elo-direct-csv-descriptor/v1" => DescriptorFamily.Csv,
            "club-elo-official-html-descriptor/v1" => DescriptorFamily.Html,
            _ => throw new InvalidDataException("Club Elo publication descriptor family is unknown.")
        };
    }

    private static void ValidateCycle(BundesligaContextSourceCycleIdentity cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        _ = BundesligaContextSourceCycleIdentity.Create(cycle.Competition, cycle.Scope, cycle.CycleId, cycle.Sequence);
    }

    private static BundesligaContextSourceCycleIdentity ParseCycleId(string value)
    {
        var scope = value.StartsWith("gha:", StringComparison.Ordinal)
            ? BundesligaContextSourceScope.ProductionLive
            : value.StartsWith("local:", StringComparison.Ordinal)
                ? BundesligaContextSourceScope.Development
                : throw new InvalidDataException("Club Elo v2 cycle ID has no canonical production/development prefix.");
        return BundesligaContextSourceCycleIdentity.FromCycleId(BundesligaContextSourceContract.Competition, scope, value);
    }

    private static IReadOnlyList<string> ParseDiagnostics(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Club Elo LKG metadata selection_diagnostics must be an array.");
        }

        return ValidateDiagnostics(element.EnumerateArray().Select(value =>
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())
                || !string.Equals(value.GetString(), value.GetString()!.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Club Elo LKG metadata diagnostics must be nonblank, trimmed strings.");
            }

            return value.GetString()!;
        }));
    }

    private static IReadOnlyList<string> ValidateDiagnostics(IEnumerable<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var values = diagnostics.ToArray();
        if (values.Any(value => string.IsNullOrWhiteSpace(value)
                                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Club Elo metadata diagnostics must be nonblank, trimmed strings.");
        }

        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length
            || !values.SequenceEqual(values.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Club Elo LKG metadata diagnostics must be unique and ordinal-sorted.");
        }

        return values;
    }

    private static void ValidateSnapshotProvenance(BundesligaClubEloSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.SourceUrl.IsAbsoluteUri
            || !string.Equals(snapshot.SourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || snapshot.CollectedAt.Offset != TimeSpan.Zero
            || snapshot.RatedAt > DateOnly.FromDateTime(snapshot.CollectedAt.UtcDateTime))
        {
            throw new InvalidDataException("Club Elo publication snapshot provenance is invalid.");
        }
    }

    private static void ValidateSelectionMetadata(
        BundesligaClubEloSnapshotOrigin origin,
        BundesligaClubEloSelectionDisposition disposition,
        IReadOnlyList<string> diagnostics)
    {
        if (disposition == BundesligaClubEloSelectionDisposition.NetworkAccepted)
        {
            if (origin != BundesligaClubEloSnapshotOrigin.NetworkCandidate || diagnostics.Count != 0)
            {
                throw new InvalidDataException("Accepted Club Elo network metadata requires NetworkCandidate origin and no diagnostics.");
            }

            return;
        }

        if (origin is not (BundesligaClubEloSnapshotOrigin.LaunchSeed or BundesligaClubEloSnapshotOrigin.LastKnownGood)
            || diagnostics.Count == 0)
        {
            throw new InvalidDataException("Retained Club Elo metadata requires a seed/LKG origin and diagnostics.");
        }

        var only = diagnostics.Count == 1 ? diagnostics[0] : null;
        var valid = disposition switch
        {
            BundesligaClubEloSelectionDisposition.NetworkDisabled => only == "UNATTENDED_NETWORK_USE_NOT_APPROVED",
            BundesligaClubEloSelectionDisposition.NetworkCandidateRejected => true,
            BundesligaClubEloSelectionDisposition.NetworkCandidateStale => only?.StartsWith("NETWORK_CANDIDATE_STALE:", StringComparison.Ordinal) == true,
            BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer => only?.StartsWith("NETWORK_RATED_AT_NOT_NEWER:", StringComparison.Ordinal) == true,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidDataException("Club Elo LKG metadata selection disposition contradicts its diagnostics.");
        }
    }

    private static string CreateSourceBackedMetadata(
        BundesligaClubEloSelection selection,
        BundesligaContextSourceCycleIdentity cycle,
        BundesligaContextSourceObservation observation,
        BundesligaContextSourcePayload payload,
        JsonElement descriptor)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteV1Prefix(writer, selection);
            writer.WriteString("cycle_id", cycle.CycleId); writer.WriteString("attempt_id", observation.AttemptId);
            writer.WriteString("source_observed_at", BundesligaContextSourceContract.FormatUtc(observation.ObservedAtUtc));
            writer.WriteString("raw_sha256", payload.Sha256); writer.WriteNumber("raw_byte_length", payload.ByteLength);
            writer.WriteString("displayed_date", descriptor.GetProperty("displayedDate").GetString());
            writer.WritePropertyName("provider_date_evidence"); descriptor.GetProperty("providerDateEvidence").WriteTo(writer);
            writer.WriteString("name_mapping_sha256", descriptor.GetProperty("nameMappingSha256").GetString());
            writer.WritePropertyName("source_rows"); descriptor.GetProperty("sourceRows").WriteTo(writer);
            writer.WriteString("sourceDescriptorSha256", observation.DescriptorSha256);
            writer.WritePropertyName("sourceDescriptor"); descriptor.WriteTo(writer);
            writer.WritePropertyName("selectedPayload"); writer.WriteStartObject(); writer.WriteString("path", payload.Path); writer.WriteString("rawSha256", payload.Sha256); writer.WriteNumber("rawByteLength", payload.ByteLength); writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteV1Prefix(Utf8JsonWriter writer, BundesligaClubEloSelection selection)
    {
        writer.WriteString("schema_version", MetadataSchemaVersionV2);
        writer.WriteString("rated_at", selection.Selected.RatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteString("collected_at", selection.Selected.CollectedAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        writer.WriteString("source_url", selection.Selected.SourceUrl.AbsoluteUri); writer.WriteString("selected_origin", selection.Selected.Origin.ToString()); writer.WriteString("selection_disposition", selection.Disposition.ToString());
        writer.WritePropertyName("selection_diagnostics"); writer.WriteStartArray(); foreach (var diagnostic in selection.Diagnostics) writer.WriteStringValue(diagnostic); writer.WriteEndArray(); writer.WriteNumber("manifest_team_count", BundesligaTeamManifest.ExpectedTeamCount); writer.WriteString("rank_policy", RankPolicy);
    }

    private static void WriteCsvEvidence(Utf8JsonWriter writer, JsonElement descriptor)
    {
        var evidence = descriptor.GetProperty("providerDateEvidence"); var rows = descriptor.GetProperty("sourceRows");
        writer.WritePropertyName("provider_date_evidence");
        if (evidence.ValueKind == JsonValueKind.Null) writer.WriteNullValue();
        else { writer.WriteStartObject(); writer.WriteString("kind", evidence.GetProperty("kind").GetString()); writer.WriteString("recipe_id", evidence.GetProperty("recipeId").GetString()); if (evidence.GetProperty("field").ValueKind == JsonValueKind.Null) writer.WriteNull("field"); else writer.WriteString("field", evidence.GetProperty("field").GetString()); writer.WriteString("raw_value", evidence.GetProperty("rawValue").GetString()); writer.WriteString("rated_at", evidence.GetProperty("ratedAt").GetString()); writer.WriteEndObject(); }
        writer.WriteString("name_mapping_sha256", descriptor.GetProperty("nameMappingSha256").GetString()); writer.WritePropertyName("source_rows");
        if (rows.ValueKind == JsonValueKind.Null) writer.WriteNullValue();
        else { writer.WriteStartArray(); foreach (var row in rows.EnumerateArray()) { writer.WriteStartObject(); writer.WriteString("team_slug", row.GetProperty("teamSlug").GetString()); writer.WriteString("provider_name", row.GetProperty("providerName").GetString()); writer.WriteNumber("global_rank", row.GetProperty("globalRank").GetInt32()); writer.WritePropertyName("elo"); row.GetProperty("elo").WriteTo(writer); writer.WriteEndObject(); } writer.WriteEndArray(); }
    }

    /// <summary>Shared selection, cycle and historical-row truth for every CSV-v2 route.</summary>
    private static void ValidateCsvV2CommonTruth(BundesligaClubEloSelection selection,
        BundesligaContextSourceCycleIdentity cycle, BundesligaContextSourceObservation observation,
        byte[] immutablePayload, JsonElement descriptor)
    {
        if (selection.Selected.Origin != BundesligaClubEloSnapshotOrigin.NetworkCandidate
            || selection.Disposition != BundesligaClubEloSelectionDisposition.NetworkAccepted
            || selection.Diagnostics.Count != 0
            || observation.Source != BundesligaContextSource.ClubElo
            || observation.Disposition != BundesligaContextSourceDisposition.ArtifactCaptured
            || observation.Payload is not { Path: "club-elo/source.csv" } payload
            || ClassifyDescriptor(descriptor) != DescriptorFamily.Csv
            || observation.AttemptId != BundesligaContextSourceHashing.AttemptId(cycle, observation.Source)
            || immutablePayload.LongLength != payload.ByteLength
            || BundesligaContextSourceHashing.Sha256(immutablePayload) != payload.Sha256
            || selection.Selected.CollectedAt != observation.ObservedAtUtc
            || selection.Selected.SourceUrl.AbsoluteUri != RequiredString(descriptor, "sourceUrl")
            || selection.Selected.RatedAt != DateOnly.ParseExact(RequiredString(descriptor, "providerRatedAt"), "yyyy-MM-dd", CultureInfo.InvariantCulture))
            throw new InvalidDataException("CSV-v2 publication selection/cycle/observation truth is inconsistent.");

        ValidateCsvDescriptorEvidence(descriptor);
    }

    private static void ValidateCsvDescriptorEvidence(JsonElement descriptor)
    {
        var evidence = descriptor.GetProperty("providerDateEvidence");
        ValidateCsvDateEvidence(evidence, DateOnly.ParseExact(RequiredString(descriptor, "providerRatedAt"), "yyyy-MM-dd", CultureInfo.InvariantCulture), camelCase: true);
        ValidateCsvRows(descriptor.GetProperty("sourceRows"), camelCase: true);
    }

    private static void ValidateCsvV2Metadata(JsonElement root, DateOnly ratedAt, DateTimeOffset collectedAt, Uri sourceUrl,
        BundesligaClubEloSnapshotOrigin origin, BundesligaClubEloSelectionDisposition disposition, IReadOnlyList<string> diagnostics)
    {
        var cycle = ParseCycleId(RequiredString(root, "cycle_id"));
        var observedAt = BundesligaContextSourceContract.ParseUtc(RequiredString(root, "source_observed_at"));
        BundesligaContextSourceHashing.ValidateSha(RequiredString(root, "raw_sha256"));
        if (origin != BundesligaClubEloSnapshotOrigin.NetworkCandidate || disposition != BundesligaClubEloSelectionDisposition.NetworkAccepted || diagnostics.Count != 0
            || RequiredString(root, "attempt_id") != BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo)
            || !root.GetProperty("raw_byte_length").TryGetInt64(out var length) || length < 0
            || root.GetProperty("source_rows").ValueKind != JsonValueKind.Array || root.GetProperty("provider_date_evidence").ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("CSV-v2 evidence identity or shape is invalid.");
        ValidateCsvDateEvidence(root.GetProperty("provider_date_evidence"), ratedAt, camelCase: false);
        BundesligaContextSourceHashing.ValidateSha(RequiredString(root, "name_mapping_sha256"));
        ValidateCsvRows(root.GetProperty("source_rows"), camelCase: false);
        // CSV evidence may retain fractional tokens; it is not a snapshot assertion until a
        // source-backed build invokes ValidateCsvSnapshotBinding.
        if (collectedAt != observedAt) throw new InvalidDataException("CSV-v2 collected_at must equal source_observed_at.");
        _ = length; _ = ratedAt; _ = sourceUrl; _ = origin; _ = disposition; _ = diagnostics;
    }

    private static void ValidateCsvDateEvidence(JsonElement evidence, DateOnly ratedAt, bool camelCase)
    {
        var names = camelCase ? new[] { "kind", "recipeId", "field", "rawValue", "ratedAt" } : new[] { "kind", "recipe_id", "field", "raw_value", "rated_at" };
        var recipe = camelCase ? "recipeId" : "recipe_id";
        var raw = camelCase ? "rawValue" : "raw_value";
        var date = camelCase ? "ratedAt" : "rated_at";
        if (evidence.ValueKind != JsonValueKind.Object
            || !evidence.EnumerateObject().Select(value => value.Name).SequenceEqual(names, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(RequiredString(evidence, recipe))
            || string.IsNullOrWhiteSpace(RequiredString(evidence, raw))
            || RequiredString(evidence, date) != ratedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            throw new InvalidDataException("CSV-v2 provider-date evidence is not the canonical historical CSV form.");
        var kind = RequiredString(evidence, "kind");
        if (kind == "ProviderCsvField" && evidence.GetProperty("field").ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(evidence.GetProperty("field").GetString())) return;
        if (kind == "AcceptedDailyEndpoint" && evidence.GetProperty("field").ValueKind == JsonValueKind.Null) return;
        throw new InvalidDataException("CSV-v2 provider-date evidence kind/field matrix is invalid.");
    }

    private static void ValidateCsvRows(JsonElement rows, bool camelCase)
    {
        var slug = camelCase ? "teamSlug" : "team_slug";
        var name = camelCase ? "providerName" : "provider_name";
        var rank = camelCase ? "globalRank" : "global_rank";
        var names = new[] { slug, name, rank, "elo" };
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != BundesligaTeamManifest.ExpectedTeamCount)
            throw new InvalidDataException("CSV-v2 evidence requires exactly 18 source rows.");
        var expectedSlugs = BundesligaTeamManifest.Default.Entries.Select(value => value.TeamSlug).ToArray();
        var providerNames = new HashSet<string>(StringComparer.Ordinal);
        var globalRanks = new HashSet<int>();
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (!row.EnumerateObject().Select(value => value.Name).SequenceEqual(names, StringComparer.Ordinal)
                || RequiredString(row, slug) != expectedSlugs[index]
                || string.IsNullOrWhiteSpace(RequiredString(row, name))
                || !providerNames.Add(RequiredString(row, name))
                || !row.GetProperty(rank).TryGetInt32(out var value) || value <= 0 || !globalRanks.Add(value))
                throw new InvalidDataException("CSV-v2 rows must have canonical unique slugs, provider names, and ranks.");
            ValidateCanonicalPositiveFiniteEloToken(row.GetProperty("elo"));
            index++;
        }
    }

    private static void ValidateCsvSnapshotBinding(JsonElement rows, BundesligaClubEloSnapshot snapshot)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != BundesligaTeamManifest.ExpectedTeamCount) throw new InvalidDataException("CSV-v2 binding requires all source rows.");
        var selected = snapshot.Entries.OrderBy(value => value.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        foreach (var (row, entry) in rows.EnumerateArray().Zip(selected))
        {
            if (row.GetProperty("teamSlug").GetString() != entry.Team.TeamSlug || !row.GetProperty("globalRank").TryGetInt32(out var rank) || rank != entry.GlobalRank
                || !row.GetProperty("elo").TryGetInt32(out var elo) || elo <= 0 || elo != entry.Elo) throw new InvalidDataException("CSV-v2 source evidence cannot bind the selected Int32 snapshot.");
        }
    }

    private static void ValidateSourceBackedMetadata(JsonElement root, DateOnly ratedAt, DateTimeOffset collectedAt, Uri sourceUrl,
        BundesligaClubEloSnapshotOrigin origin, BundesligaClubEloSelectionDisposition disposition, IReadOnlyList<string> diagnostics)
    {
        if (origin != BundesligaClubEloSnapshotOrigin.NetworkCandidate || disposition != BundesligaClubEloSelectionDisposition.NetworkAccepted || diagnostics.Count != 0
            || sourceUrl.AbsoluteUri != "https://clubelo.com/GER") throw new InvalidDataException("HTML-v2 metadata selection is invalid.");
        var cycle = ParseCycleId(RequiredString(root, "cycle_id"));
        var observedAt = BundesligaContextSourceContract.ParseUtc(RequiredString(root, "source_observed_at"));
        if (collectedAt != observedAt || ratedAt != DateOnly.ParseExact(RequiredString(root, "displayed_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture)) throw new InvalidDataException("HTML-v2 metadata dates are inconsistent.");
        BundesligaContextSourceHashing.ValidateSha(RequiredString(root, "raw_sha256"));
        if (RequiredString(root, "attempt_id") != BundesligaContextSourceHashing.AttemptId(cycle, BundesligaContextSource.ClubElo)) throw new InvalidDataException("HTML-v2 attempt identity is invalid.");
        if (!root.GetProperty("raw_byte_length").TryGetInt64(out var length) || length < 0) throw new InvalidDataException("HTML-v2 raw length is invalid.");
        var descriptor = root.GetProperty("sourceDescriptor");
        if (descriptor.ValueKind != JsonValueKind.Object || !BundesligaContextSourceDescriptorContract.IsHtmlClubEloDescriptor(descriptor)
            || BundesligaContextSourceHashing.Sha256(Encoding.UTF8.GetBytes(descriptor.GetRawText())) != RequiredString(root, "sourceDescriptorSha256")) throw new InvalidDataException("HTML-v2 embedded descriptor is invalid.");
        if (descriptor.GetProperty("rawSha256").GetString() != RequiredString(root, "raw_sha256") || descriptor.GetProperty("rawByteLength").GetInt64() != length
            || descriptor.GetProperty("displayedDate").GetString() != root.GetProperty("displayed_date").GetString()
            || descriptor.GetProperty("nameMappingSha256").GetString() != RequiredString(root, "name_mapping_sha256")
            || descriptor.GetProperty("providerDateEvidence").GetRawText() != root.GetProperty("provider_date_evidence").GetRawText()
            || descriptor.GetProperty("sourceRows").GetRawText() != root.GetProperty("source_rows").GetRawText()) throw new InvalidDataException("HTML-v2 flattened provenance is inconsistent.");
        var selectedPayload = root.GetProperty("selectedPayload");
        if (selectedPayload.ValueKind != JsonValueKind.Object || !selectedPayload.EnumerateObject().Select(value => value.Name).SequenceEqual(["path", "rawSha256", "rawByteLength"], StringComparer.Ordinal)
            || selectedPayload.GetProperty("path").GetString() != "club-elo/source.html" || selectedPayload.GetProperty("rawSha256").GetString() != RequiredString(root, "raw_sha256")
            || !selectedPayload.GetProperty("rawByteLength").TryGetInt64(out var selectedLength) || selectedLength != length) throw new InvalidDataException("HTML-v2 selected payload is invalid.");

        // Reuse the strict descriptor validator rather than accepting a superficially matching
        // embedded object.  No raw bytes are invented during LKG reconstruction; this only
        // proves the descriptor/payload identities that the historical publication retained.
        var retained = new BundesligaContextSourceObservation(BundesligaContextSource.ClubElo,
            RequiredString(root, "attempt_id"), observedAt, BundesligaContextSourceDisposition.ArtifactCaptured,
            descriptor.GetRawText(), new BundesligaContextSourcePayload("club-elo/source.html", length, RequiredString(root, "raw_sha256")), []);
        retained.Validate();
    }

    private static void ValidateCanonicalPositiveFiniteEloToken(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number) throw new InvalidDataException("CSV-v2 Elo must be a canonical positive finite JSON number.");
        if (value.TryGetInt64(out var integer))
        {
            if (integer <= 0 || !string.Equals(JsonSerializer.Serialize(integer), value.GetRawText(), StringComparison.Ordinal))
                throw new InvalidDataException("CSV-v2 Elo must be a canonical positive finite JSON number.");
            return;
        }
        if (!double.TryParse(value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed) || parsed <= 0
            || !string.Equals(JsonSerializer.Serialize(parsed), value.GetRawText(), StringComparison.Ordinal))
            throw new InvalidDataException("CSV-v2 Elo must be a canonical positive finite JSON number.");
    }

    private static void ValidateHtmlRowsAgainstSelection(JsonElement rows, BundesligaClubEloSnapshot selected)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != BundesligaTeamManifest.ExpectedTeamCount) throw new InvalidDataException("HTML-v2 source rows are invalid.");
        var expected = selected.Entries.OrderBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        foreach (var (row, entry) in rows.EnumerateArray().Zip(expected))
            if (row.GetProperty("teamSlug").GetString() != entry.Team.TeamSlug || row.GetProperty("globalRank").GetInt32() != entry.GlobalRank || row.GetProperty("elo").GetInt32() != entry.Elo)
                throw new InvalidDataException("HTML-v2 source rows do not equal the selected snapshot.");
    }

    private static void ValidateV2RowsAgainstHeadedSnapshot(string metadataJson, BundesligaClubEloSnapshot headed)
    {
        using var document = JsonDocument.Parse(metadataJson); var root = document.RootElement;
        var family = ClassifyMetadata(root);
        if (family == MetadataFamily.V1) return;
        if (family == MetadataFamily.CsvV2) { ValidateCsvV2RowsAgainstSelection(root.GetProperty("source_rows"), headed); return; }
        var descriptor = root.GetProperty("sourceDescriptor");
        ValidateHtmlRowsAgainstSelection(root.GetProperty("source_rows"), headed);
        if (descriptor.GetProperty("sourceRows").GetRawText() != root.GetProperty("source_rows").GetRawText()) throw new InvalidDataException("HTML-v2 headed-document row binding is inconsistent.");
    }

    private static void ValidateCsvV2RowsAgainstSelection(JsonElement rows, BundesligaClubEloSnapshot headed)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != BundesligaTeamManifest.ExpectedTeamCount) throw new InvalidDataException("CSV-v2 headed-document row count is invalid.");
        foreach (var (row, entry) in rows.EnumerateArray().Zip(headed.Entries.OrderBy(value => value.Team.TeamSlug, StringComparer.Ordinal)))
            if (row.GetProperty("team_slug").GetString() != entry.Team.TeamSlug || !row.GetProperty("global_rank").TryGetInt32(out var rank) || rank != entry.GlobalRank || !row.GetProperty("elo").TryGetInt32(out var elo) || elo <= 0 || elo != entry.Elo)
                throw new InvalidDataException("CSV-v2 evidence cannot reconstruct the headed Int32 snapshot.");
    }

    private static IReadOnlyList<CsvRow> ReadRows(string content, string documentName)
    {
        if (!content.StartsWith(CsvHeader + "\r\n", StringComparison.Ordinal)
            || !content.EndsWith("\r\n", StringComparison.Ordinal)
            || content.Contains('\n') && content.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\n')
            || content.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\r'))
        {
            throw new InvalidDataException($"Club Elo LKG document '{documentName}' violates strict CSV line endings or header.");
        }

        var lines = content.Split("\r\n", StringSplitOptions.None);
        var rows = new List<CsvRow>();
        for (var index = 1; index < lines.Length - 1; index++)
        {
            var fields = lines[index].Split(',');
            if (fields.Length != 5
                || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var globalRank) || globalRank <= 0
                || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var bundesligaRank) || bundesligaRank <= 0
                || string.IsNullOrWhiteSpace(fields[2])
                || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var elo) || elo <= 0
                || !DateOnly.TryParseExact(fields[4], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ratedAt))
            {
                throw new InvalidDataException($"Club Elo LKG document '{documentName}' has an invalid row {index + 1}.");
            }

            rows.Add(new CsvRow(globalRank, bundesligaRank, fields[2], elo, ratedAt));
        }

        return rows;
    }

    private static string RequiredString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new KeyNotFoundException(property);

    private sealed record CsvRow(int GlobalRank, int BundesligaRank, string Team, int Elo, DateOnly RatedAt);
}

public sealed record BundesligaClubEloRankedEntry(BundesligaClubEloEntry Entry, int BundesligaRank, DateOnly RatedAt);

public sealed record BundesligaClubEloPublicationBuild(
    BundesligaClubEloSnapshot Snapshot,
    IReadOnlyList<BundesligaClubEloRankedEntry> RankedEntries,
    IReadOnlyList<DocumentPublicationPayload> Documents,
    string MetadataJson);

public sealed record BundesligaClubEloPublicationMetadata(
    DateOnly RatedAt,
    DateTimeOffset CollectedAt,
    Uri SourceUrl,
    BundesligaClubEloSnapshotOrigin SelectedOrigin,
    BundesligaClubEloSelectionDisposition SelectionDisposition,
    IReadOnlyList<string> SelectionDiagnostics);
