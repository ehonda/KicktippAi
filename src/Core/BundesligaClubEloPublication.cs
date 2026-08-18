using System.Globalization;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// The immutable prompt-document contract accepted in ADR-0015.
/// </summary>
public static class BundesligaClubEloPublication
{
    public const string CsvHeader = "Global_Rank,Bundesliga_Rank,Team,ELO,Rated_At";
    public const string MetadataSchemaVersion = "club-elo-publication-v1";
    public const string RankPolicy = "elo-desc-global-rank-asc-manifest-slug-ordinal-sequential";
    public const string KpiDescription = "Bundesliga 2026/27 Club Elo rankings for all 18 manifest teams.";

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

            var requiredProperties = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema_version", "rated_at", "collected_at", "source_url", "selected_origin",
                "selection_disposition", "selection_diagnostics", "manifest_team_count", "rank_policy"
            };
            var actualProperties = root.EnumerateObject().Select(property => property.Name).ToArray();
            if (actualProperties.Length != requiredProperties.Count || actualProperties.Any(property => !requiredProperties.Contains(property)))
            {
                throw new InvalidDataException("Club Elo LKG metadata properties do not match the ADR-0015 contract.");
            }

            var schema = RequiredString(root, "schema_version");
            var ratedAtValue = RequiredString(root, "rated_at");
            var collectedAtValue = RequiredString(root, "collected_at");
            var sourceUrlValue = RequiredString(root, "source_url");
            var selectedOrigin = RequiredString(root, "selected_origin");
            var selectionDisposition = RequiredString(root, "selection_disposition");
            var rankPolicy = RequiredString(root, "rank_policy");
            var diagnostics = ParseDiagnostics(root.GetProperty("selection_diagnostics"));
            if (!string.Equals(schema, MetadataSchemaVersion, StringComparison.Ordinal)
                || !string.Equals(rankPolicy, RankPolicy, StringComparison.Ordinal)
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
