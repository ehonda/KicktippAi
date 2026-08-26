using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace EHonda.KicktippAi.Core;

/// <summary>Builds and strictly reconstructs the atomic Bundesliga roster publication.</summary>
public static class BundesligaRosterPublication
{
    public const string LegacyMetadataContract = "bundesliga-roster-publication/v1";
    public const string MetadataContract = "bundesliga-roster-publication/v2";
    public const string SquadSummaryDescription = "Bundesliga 2026/27 roster membership and squad summary KPI.";

    public static BundesligaRosterBuiltPublication Build(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaRosterQualityReportRow> qualityRows)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(qualityRows);
        var orderedSnapshots = snapshots.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        var orderedQualityRows = ValidateSnapshotQuality(orderedSnapshots, qualityRows);
        var qualityReport = BundesligaRosterCsv.RenderQualityReport(orderedQualityRows);
        var documents = orderedSnapshots
            .Select(snapshot => new DocumentPublicationPayload(
                DocumentPublicationKind.Context,
                $"roster-{snapshot.Team.TeamSlug}",
                BundesligaRosterCsv.RenderTeamRoster(snapshot)))
            .Append(new DocumentPublicationPayload(
                DocumentPublicationKind.Context,
                BundesligaRosterPublicationContract.AggregateRosterDocumentName,
                BundesligaRosterCsv.RenderAggregate(orderedSnapshots)))
            .Append(new DocumentPublicationPayload(
                DocumentPublicationKind.Kpi,
                BundesligaRosterPublicationContract.SquadSummaryDocumentName,
                BundesligaRosterCsv.RenderSummary(orderedSnapshots),
                SquadSummaryDescription))
            .ToArray();
        var metadata = new PublicationMetadata(
            MetadataContract,
            qualityReport,
            orderedSnapshots.Select(snapshot => CreateClubMetadata(snapshot, orderedQualityRows.Single(row => row.Team.TeamSlug == snapshot.Team.TeamSlug))).ToArray());
        var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        _ = ParseMetadata(metadataJson); // Builders and readers share one strict metadata validator.
        var request = new DocumentPublicationRequest("metadata-only", null, documents, metadataJson);
        DocumentPublicationContract.ValidateRequest(CompetitionIds.Bundesliga2026_27, BundesligaDocumentPublication.Rosters, request);
        return new BundesligaRosterBuiltPublication(documents, qualityReport, metadataJson);
    }

    public static DocumentPublicationRequest CreateRequest(
        string communityContext,
        string? expectedPreviousSnapshotId,
        BundesligaRosterBuiltPublication publication) =>
        new(communityContext, expectedPreviousSnapshotId, publication.Documents, publication.MetadataJson);

    /// <summary>
    /// Reconstructs the exact bytes emitted by <see cref="Build"/> through the same headed
    /// publication contract used by live reads. Launch gates use this result so serialized
    /// roster rows, aggregate reuse, metadata, and derived rows are all validated before write.
    /// </summary>
    public static BundesligaRosterLastKnownGood ReconstructBuilt(BundesligaRosterBuiltPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        const string validationCommunityContext = "roster-publication-validation";
        var createdAt = DateTimeOffset.UnixEpoch;
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27,
            validationCommunityContext,
            BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind,
            payload.Name,
            index + 1,
            payload.Content,
            payload.Description,
            createdAt)).ToArray();
        var snapshotId = DocumentPublicationContract.ComputeSnapshotId(publication.Documents);
        var snapshot = new DocumentPublicationSnapshot(
            CompetitionIds.Bundesliga2026_27,
            validationCommunityContext,
            BundesligaDocumentPublication.RosterPublicationSet,
            snapshotId,
            null,
            createdAt,
            publication.MetadataJson,
            documents.Select(document => new DocumentPublicationEntry(
                document.Kind,
                document.Name,
                document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));
        return ReconstructLastKnownGood(new LoadedDocumentPublication(snapshot, documents));
    }

    public static BundesligaRosterLastKnownGood ReconstructLastKnownGood(LoadedDocumentPublication loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        DocumentPublicationContract.ValidateLoaded(
            CompetitionIds.Bundesliga2026_27,
            loaded.Snapshot.CommunityContext,
            BundesligaDocumentPublication.Rosters,
            loaded.Snapshot,
            loaded.Documents);
        var metadata = ParseMetadata(loaded.Snapshot.MetadataJson);
        var isLegacy = string.Equals(metadata.Contract, LegacyMetadataContract, StringComparison.Ordinal);
        var byKey = loaded.Documents.ToDictionary(document => document.Key);
        var snapshots = new List<BundesligaRosterClubSnapshot>();
        var qualityRows = new List<BundesligaRosterQualityReportRow>();
        foreach (var club in metadata.Clubs)
        {
            var team = BundesligaTeamManifest.Default.GetByTeamSlug(club.TeamSlug);
            var content = byKey[new DocumentPublicationKey(DocumentPublicationKind.Context, $"roster-{club.TeamSlug}")].Content;
            var snapshot = ParseAndValidateTeamRoster(content, team, club, isLegacy);
            snapshots.Add(snapshot);
            qualityRows.Add(CreateQualityRow(snapshot, club));
        }

        var ordered = snapshots.OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        _ = ValidateSnapshotQuality(ordered, qualityRows);
        var expectedAggregate = isLegacy
            ? BundesligaRosterCsv.RenderLegacyAggregate(ordered)
            : BundesligaRosterCsv.RenderAggregate(ordered);
        var expectedSummary = BundesligaRosterCsv.RenderSummary(ordered);
        if (!string.Equals(byKey[new DocumentPublicationKey(DocumentPublicationKind.Context, BundesligaRosterPublicationContract.AggregateRosterDocumentName)].Content, expectedAggregate, StringComparison.Ordinal)
            || !string.Equals(byKey[new DocumentPublicationKey(DocumentPublicationKind.Kpi, BundesligaRosterPublicationContract.SquadSummaryDocumentName)].Content, expectedSummary, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Headed Bundesliga roster aggregate or squad summary does not match its canonical per-team documents.");
        }

        var expectedQuality = BundesligaRosterCsv.RenderQualityReport(qualityRows);
        if (!string.Equals(metadata.QualityReportCsv, expectedQuality, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Headed Bundesliga roster metadata quality report does not match canonical roster metadata.");
        }

        return new BundesligaRosterLastKnownGood(loaded.Snapshot.SnapshotId, ordered, qualityRows, expectedQuality);
    }

    private static ClubMetadata CreateClubMetadata(BundesligaRosterClubSnapshot snapshot, BundesligaRosterQualityReportRow quality) => new(
        snapshot.Team.TeamSlug,
        SourceName(snapshot.MembershipSource),
        snapshot.MembershipAsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        quality.SourceReferences.Select(uri => uri.AbsoluteUri).Order(StringComparer.Ordinal).ToArray(),
        quality.SourceRevision,
        quality.LastKnownGoodSnapshotId,
        quality.DuckDbSnapshotAsOf?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        GateName(quality.DuckDbGateResult),
        quality.SelectionReason,
        quality.Diagnostics.Order(StringComparer.Ordinal).ToArray(),
        snapshot.Members.OrderBy(member => member.Role == BundesligaRosterRole.Coach ? 0 : 1)
            .ThenBy(member => BundesligaRosterSeed.NormalizeName(member.Name), StringComparer.Ordinal)
            .ThenBy(member => member.TransfermarktPlayerId ?? 0)
            .Select(member => new MemberMetadata(member.Role.ToString(), BundesligaRosterSeed.NormalizeName(member.Name), member.TransfermarktPlayerId))
            .ToArray());

    private static PublicationMetadata ParseMetadata(string json)
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<PublicationMetadata>(json, JsonOptions)
                ?? throw new InvalidDataException("Roster publication metadata is empty.");
            if (!string.Equals(JsonSerializer.Serialize(metadata, JsonOptions), json, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Roster publication metadata is not canonical JSON.");
            }
            if (!(string.Equals(metadata.Contract, MetadataContract, StringComparison.Ordinal)
                  || string.Equals(metadata.Contract, LegacyMetadataContract, StringComparison.Ordinal))
                || string.IsNullOrEmpty(metadata.QualityReportCsv)
                || metadata.Clubs is null
                || metadata.Clubs.Length != BundesligaTeamManifest.ExpectedTeamCount
                || !metadata.Clubs.Select(club => club.TeamSlug)
                    .SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(team => team.TeamSlug), StringComparer.Ordinal))
            {
                throw new InvalidDataException("Roster publication metadata does not have the required contract or exact club coverage.");
            }

            foreach (var club in metadata.Clubs)
            {
                ValidateMetadataClub(club);
            }
            return metadata;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Roster publication metadata is malformed.", exception);
        }
    }

    private static BundesligaRosterClubSnapshot ParseAndValidateTeamRoster(
        string content,
        BundesligaTeamManifestEntry team,
        ClubMetadata club,
        bool isLegacy)
    {
        if (!DateOnly.TryParseExact(club.MembershipAsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOf)
            || !Enum.TryParse<BundesligaRosterMembershipSource>(club.SelectedSource, ignoreCase: false, out var source)
            || club.Members is null
            || club.SourceReferences is null
            || club.SourceReferences.Length == 0
            || club.SourceReferences.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException($"Roster metadata for '{team.TeamSlug}' is malformed.");
        }

        var expectedMembers = club.Members.Select(member => ParseMetadataMember(member, team.TeamSlug)).ToArray();
        var document = ReadRosterRows(content, team, asOf, isLegacy);
        if (document.MemberRows.Count != expectedMembers.Length
            || !document.MemberRows.Select(row => (row.Role, row.Name))
                .SequenceEqual(expectedMembers.Select(member => (Role: Enum.Parse<BundesligaRosterRole>(member.Role), member.Name))))
        {
            throw new InvalidDataException($"Roster document for '{team.TeamSlug}' does not match its headed membership metadata.");
        }

        var memberByIdentity = expectedMembers.ToDictionary(member => (Enum.Parse<BundesligaRosterRole>(member.Role), member.Name));
        var members = document.MemberRows.Select(row => new BundesligaRosterMember(
            row.Role,
            row.Name,
            memberByIdentity[(row.Role, row.Name)].TransfermarktPlayerId,
            row.Age,
            row.Position,
            row.MarketValueEur)).ToArray();
        var snapshot = new BundesligaRosterClubSnapshot(team, asOf, source, members);
        var expectedTotal = BundesligaRosterCsv.KnownMarketValueTotal(snapshot.Members);
        if (!isLegacy && document.TeamAccumulatedMarketValueEur != expectedTotal)
        {
            throw new InvalidDataException($"Roster document for '{team.TeamSlug}' has an incorrect known-value subtotal.");
        }

        var expectedContent = isLegacy
            ? BundesligaRosterCsv.RenderLegacyTeamRoster(snapshot)
            : BundesligaRosterCsv.RenderTeamRoster(snapshot);
        if (!string.Equals(expectedContent, content, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Roster document for '{team.TeamSlug}' is not canonical.");
        }

        return snapshot;
    }

    private static ParsedRosterDocument ReadRosterRows(
        string content,
        BundesligaTeamManifestEntry team,
        DateOnly asOf,
        bool isLegacy)
    {
        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { BadDataFound = null, HeaderValidated = null, MissingFieldFound = null });
        if (!csv.Read() || !csv.ReadHeader() || !(csv.HeaderRecord ?? []).SequenceEqual(BundesligaRosterCsv.RosterHeaders, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Roster document for '{team.TeamSlug}' has an invalid header.");
        }

        var rows = new List<ParsedRow>();
        long? accumulatedMarketValue = null;
        var accumulatedRowCount = 0;
        while (csv.Read())
        {
            var rowTeam = csv.GetField("Team");
            var date = csv.GetField("Data_Collected_At");
            var roleText = csv.GetField("Role");
            var name = csv.GetField("Name");
            if (!string.Equals(rowTeam, team.KicktippName, StringComparison.Ordinal)
                || !string.Equals(date, asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Roster document for '{team.TeamSlug}' has an invalid row.");
            }

            if (string.Equals(roleText, BundesligaRosterCsv.TeamAccumulatedRole, StringComparison.Ordinal))
            {
                accumulatedRowCount++;
                if (isLegacy
                    || accumulatedRowCount != 1
                    || !string.Equals(name, BundesligaRosterCsv.MissingValue, StringComparison.Ordinal)
                    || !string.Equals(csv.GetField("Age"), BundesligaRosterCsv.MissingValue, StringComparison.Ordinal)
                    || !string.Equals(csv.GetField("Position"), BundesligaRosterCsv.MissingValue, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Roster document for '{team.TeamSlug}' has an invalid team-accumulated row.");
                }

                accumulatedMarketValue = ParseTeamAccumulatedMoney(csv.GetField("Market_Value_EUR"));
                continue;
            }

            if (accumulatedRowCount != 0
                || !Enum.TryParse<BundesligaRosterRole>(roleText, false, out var role)
                || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException($"Roster document for '{team.TeamSlug}' has an invalid or misplaced member row.");
            }

            var normalizedName = BundesligaRosterSeed.NormalizeName(name);
            rows.Add(new ParsedRow(role, normalizedName,
                ParseOptionalPositive(csv.GetField("Age"), "Age"),
                ParsePosition(csv.GetField("Position"), role),
                ParseOptionalMoney(csv.GetField("Market_Value_EUR"), role)));
        }

        if (isLegacy ? accumulatedRowCount != 0 : accumulatedRowCount != 1)
        {
            throw new InvalidDataException($"Roster document for '{team.TeamSlug}' does not have the required team-accumulated row count.");
        }

        return new ParsedRosterDocument(rows, accumulatedMarketValue);
    }

    private static MemberMetadata ParseMetadataMember(MemberMetadata member, string teamSlug)
    {
        if (!Enum.TryParse<BundesligaRosterRole>(member.Role, false, out var role)
            || string.IsNullOrWhiteSpace(member.Name)
            || member.TransfermarktPlayerId is <= 0 && member.TransfermarktPlayerId is not null
            || role == BundesligaRosterRole.Coach && member.TransfermarktPlayerId is not null)
        {
            throw new InvalidDataException($"Roster membership metadata for '{teamSlug}' is malformed.");
        }

        if (!string.Equals(member.Name, BundesligaRosterSeed.NormalizeName(member.Name), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Roster membership metadata for '{teamSlug}' is not canonical.");
        }
        return member;
    }

    private static BundesligaRosterQualityReportRow CreateQualityRow(BundesligaRosterClubSnapshot snapshot, ClubMetadata club)
    {
        if (!Enum.TryParse<BundesligaRosterDuckDbGateResult>(club.DuckDbGateResult, false, out var gate)
            || !DateOnly.TryParseExact(club.DuckDbSnapshotAsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var duckDate)
                && club.DuckDbSnapshotAsOf is not null)
        {
            throw new InvalidDataException($"Roster quality metadata for '{snapshot.Team.TeamSlug}' is malformed.");
        }
        var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
        return new BundesligaRosterQualityReportRow(snapshot.Team, snapshot.MembershipSource, snapshot.MembershipAsOf,
            club.SourceReferences.Select(uri => new Uri(uri)).ToArray(), club.SourceRevision, club.LastKnownGoodSnapshotId,
            club.DuckDbSnapshotAsOf is null ? null : duckDate, players.Length, 1,
            players.Count(player => player.TransfermarktPlayerId is not null), players.Count(player => player.Age is not null),
            players.Count(player => player.Position is not null), players.Count(player => player.MarketValueEur is not null),
            gate, club.SelectionReason, club.Diagnostics ?? []);
    }

    private static int? ParseOptionalPositive(string? value, string field) => value == BundesligaRosterCsv.MissingValue ? null
        : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed
        : throw new InvalidDataException($"Roster {field} must be positive or N/A.");
    private static long? ParseOptionalMoney(string? value, BundesligaRosterRole role) => role == BundesligaRosterRole.Coach && value == BundesligaRosterCsv.MissingValue ? null
        : value == BundesligaRosterCsv.MissingValue ? null
        : long.TryParse(value?.Replace(".", string.Empty, StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed
        : throw new InvalidDataException("Roster Market_Value_EUR must be positive or N/A.");
    private static long? ParseTeamAccumulatedMoney(string? value) => value == BundesligaRosterCsv.MissingValue ? null
        : long.TryParse(value?.Replace(".", string.Empty, StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed
        : throw new InvalidDataException("Roster team-accumulated Market_Value_EUR must be positive or N/A.");
    private static BundesligaRosterPosition? ParsePosition(string? value, BundesligaRosterRole role) => role == BundesligaRosterRole.Coach && value == "Coach" ? null
        : value == BundesligaRosterCsv.MissingValue ? null
        : Enum.TryParse<BundesligaRosterPosition>(value, false, out var position) ? position
        : throw new InvalidDataException("Roster Position is invalid.");
    private static string SourceName(BundesligaRosterMembershipSource value) => value.ToString();
    private static string GateName(BundesligaRosterDuckDbGateResult value) => value.ToString();

    private sealed record ParsedRow(BundesligaRosterRole Role, string Name, int? Age, BundesligaRosterPosition? Position, long? MarketValueEur);
    private sealed record ParsedRosterDocument(IReadOnlyList<ParsedRow> MemberRows, long? TeamAccumulatedMarketValueEur);
    private sealed record PublicationMetadata(string Contract, string QualityReportCsv, ClubMetadata[] Clubs);
    private sealed record ClubMetadata(string TeamSlug, string SelectedSource, string MembershipAsOf, string[] SourceReferences,
        string? SourceRevision, string? LastKnownGoodSnapshotId, string? DuckDbSnapshotAsOf, string DuckDbGateResult,
        string SelectionReason, string[]? Diagnostics, MemberMetadata[] Members);
    private sealed record MemberMetadata(string Role, string Name, int? TransfermarktPlayerId);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static void ValidateMetadataClub(ClubMetadata club)
    {
        if (!Enum.TryParse<BundesligaRosterMembershipSource>(club.SelectedSource, false, out var source)
            || !Enum.TryParse<BundesligaRosterDuckDbGateResult>(club.DuckDbGateResult, false, out var gate)
            || !DateOnly.TryParseExact(club.MembershipAsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || club.SourceReferences is null || club.SourceReferences.Length == 0
            || club.SourceReferences.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            || !club.SourceReferences.SequenceEqual(club.SourceReferences.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || club.Diagnostics is null || !club.Diagnostics.SequenceEqual(club.Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || club.Members is null || club.Members.Length == 0)
        {
            throw new InvalidDataException($"Roster metadata for '{club.TeamSlug}' is malformed.");
        }
        var isDuck = source == BundesligaRosterMembershipSource.DuckDb;
        if (isDuck != (gate == BundesligaRosterDuckDbGateResult.Pass)
            || (isDuck && (string.IsNullOrWhiteSpace(club.SourceRevision) || club.DuckDbSnapshotAsOf is null || club.DuckDbSnapshotAsOf != club.MembershipAsOf))
            || (!isDuck && (string.IsNullOrWhiteSpace(club.SourceRevision) != (club.DuckDbSnapshotAsOf is null)))
            || (source == BundesligaRosterMembershipSource.LastKnownGood
                ? !IsLowerSha256(club.LastKnownGoodSnapshotId)
                : club.LastKnownGoodSnapshotId is not null)
            || !string.Equals(club.SelectionReason, ExpectedSelectionReason(source, gate, club.Diagnostics), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Roster metadata source/gate/provenance matrix is invalid for '{club.TeamSlug}'.");
        }
        if (club.DuckDbSnapshotAsOf is not null && !DateOnly.TryParseExact(club.DuckDbSnapshotAsOf, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new InvalidDataException($"Roster metadata DuckDB snapshot date is invalid for '{club.TeamSlug}'.");
        }
    }

    /// <summary>Shared truth boundary for creation and reconstruction metadata.</summary>
    private static IReadOnlyList<BundesligaRosterQualityReportRow> ValidateSnapshotQuality(
        IReadOnlyList<BundesligaRosterClubSnapshot> snapshots,
        IReadOnlyList<BundesligaRosterQualityReportRow> qualityRows)
    {
        if (snapshots.Count != BundesligaTeamManifest.ExpectedTeamCount || qualityRows.Count != snapshots.Count)
        {
            throw new InvalidDataException("Roster snapshot and quality metadata must cover exactly the manifest clubs.");
        }
        var orderedRows = qualityRows.OrderBy(row => row.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        if (!snapshots.Select(snapshot => snapshot.Team.TeamSlug).SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(team => team.TeamSlug), StringComparer.Ordinal)
            || !orderedRows.Select(row => row.Team.TeamSlug).SequenceEqual(BundesligaTeamManifest.Default.Entries.Select(team => team.TeamSlug), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Roster snapshot and quality metadata do not have canonical manifest coverage.");
        }
        foreach (var snapshot in snapshots)
        {
            var row = orderedRows.Single(row => row.Team.TeamSlug == snapshot.Team.TeamSlug);
            if (row.Team != snapshot.Team || row.SelectedSource != snapshot.MembershipSource || row.MembershipAsOf != snapshot.MembershipAsOf)
            {
                throw new InvalidDataException($"Roster quality metadata does not match selected provenance for '{snapshot.Team.TeamSlug}'.");
            }
            var members = snapshot.Members.ToArray();
            var orderedMembers = members.OrderBy(member => member.Role == BundesligaRosterRole.Coach ? 0 : 1)
                .ThenBy(member => BundesligaRosterSeed.NormalizeName(member.Name), StringComparer.Ordinal)
                .ThenBy(member => member.TransfermarktPlayerId ?? 0).ToArray();
            if (!members.SequenceEqual(orderedMembers) || members.Any(member => !string.Equals(member.Name, BundesligaRosterSeed.NormalizeName(member.Name), StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Roster members for '{snapshot.Team.TeamSlug}' are not canonical.");
            }
            var players = members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
            if (row.PlayerCount != players.Length || row.CoachCount != members.Count(member => member.Role == BundesligaRosterRole.Coach)
                || row.StablePlayerIdCount != players.Count(member => member.TransfermarktPlayerId is not null)
                || row.KnownAgeCount != players.Count(member => member.Age is not null)
                || row.KnownPositionCount != players.Count(member => member.Position is not null)
                || row.ValuedPlayerCount != players.Count(member => member.MarketValueEur is not null)
                || !row.SourceReferences.Select(uri => uri.AbsoluteUri).SequenceEqual(row.SourceReferences.Select(uri => uri.AbsoluteUri).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || !row.Diagnostics.SequenceEqual(row.Diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || !string.Equals(row.SelectionReason, ExpectedSelectionReason(row.SelectedSource, row.DuckDbGateResult, row.Diagnostics), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Roster quality metadata has false or non-canonical facts for '{snapshot.Team.TeamSlug}'.");
            }
            var duck = row.SelectedSource == BundesligaRosterMembershipSource.DuckDb;
            if (duck != (row.DuckDbGateResult == BundesligaRosterDuckDbGateResult.Pass)
                || (duck && (string.IsNullOrWhiteSpace(row.SourceRevision) || row.DuckDbSnapshotAsOf != row.MembershipAsOf || row.LastKnownGoodSnapshotId is not null))
                || (!duck && (string.IsNullOrWhiteSpace(row.SourceRevision) != (row.DuckDbSnapshotAsOf is null)))
                || (row.SelectedSource == BundesligaRosterMembershipSource.LastKnownGood
                    ? !IsLowerSha256(row.LastKnownGoodSnapshotId)
                    : row.LastKnownGoodSnapshotId is not null))
            {
                throw new InvalidDataException($"Roster quality provenance is invalid for '{snapshot.Team.TeamSlug}'.");
            }
        }
        return orderedRows;
    }

    private static string ExpectedSelectionReason(
        BundesligaRosterMembershipSource source,
        BundesligaRosterDuckDbGateResult gate,
        IReadOnlyCollection<string> diagnostics)
    {
        if (diagnostics.Contains("LAUNCH_ENRICHMENT_OVERLAY", StringComparer.Ordinal))
        {
            if (gate != BundesligaRosterDuckDbGateResult.NotEvaluated
                || source == BundesligaRosterMembershipSource.DuckDb)
            {
                throw new InvalidDataException("Launch enrichment overlay provenance is invalid.");
            }

            return source == BundesligaRosterMembershipSource.LastKnownGood
                ? "LAUNCH_ENRICHMENT_OVERLAY_USE_LAST_KNOWN_GOOD"
                : "LAUNCH_ENRICHMENT_OVERLAY_USE_FALLBACK_SEED";
        }

        return source switch
        {
            BundesligaRosterMembershipSource.DuckDb when gate == BundesligaRosterDuckDbGateResult.Pass => "DUCKDB_GATES_PASSED",
            BundesligaRosterMembershipSource.FallbackSeed => $"{(gate is BundesligaRosterDuckDbGateResult.NotAvailable or BundesligaRosterDuckDbGateResult.NotEvaluated ? "DUCKDB_NOT_AVAILABLE" : "DUCKDB_REJECTED")}_USE_FALLBACK_SEED",
            BundesligaRosterMembershipSource.LastKnownGood => $"{(gate is BundesligaRosterDuckDbGateResult.NotAvailable or BundesligaRosterDuckDbGateResult.NotEvaluated ? "DUCKDB_NOT_AVAILABLE" : "DUCKDB_REJECTED")}_USE_LAST_KNOWN_GOOD",
            _ => throw new InvalidDataException("Roster source and DuckDB gate matrix is invalid.")
        };
    }

    private static bool IsLowerSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record BundesligaRosterBuiltPublication(
    IReadOnlyList<DocumentPublicationPayload> Documents,
    string QualityReport,
    string MetadataJson);

public sealed record BundesligaRosterLastKnownGood(
    string SnapshotId,
    IReadOnlyList<BundesligaRosterClubSnapshot> Snapshots,
    IReadOnlyList<BundesligaRosterQualityReportRow> QualityRows,
    string QualityReport);
