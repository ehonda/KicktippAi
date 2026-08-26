using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterPublicationTests
{
    [Test]
    public async Task Build_and_strict_headed_reconstruction_preserve_the_complete_canonical_snapshot()
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var snapshotId = DocumentPublicationContract.ComputeSnapshotId(publication.Documents);
        var snapshot = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test",
            BundesligaDocumentPublication.RosterPublicationSet, snapshotId, null, DateTimeOffset.UtcNow, publication.MetadataJson,
            documents.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));

        var reconstructed = BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(snapshot, documents));

        await Assert.That(reconstructed.SnapshotId).IsEqualTo(snapshotId);
        await Assert.That(reconstructed.Snapshots).Count().IsEqualTo(18);
        await Assert.That(reconstructed.QualityReport).IsEqualTo(publication.QualityReport);
        await Assert.That(publication.MetadataJson).StartsWith(
            $"{{\"contract\":\"{BundesligaRosterPublication.MetadataContract}\"");
        await Assert.That(publication.Documents
            .Where(document => document.Name.StartsWith("roster-", StringComparison.Ordinal))
            .All(document => document.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[^1]
                .Contains(",Team Accumulated,N/A,N/A,N/A,", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Built_payload_reconstruction_validates_the_exact_serialized_v2_graph()
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);

        var reconstructed = BundesligaRosterPublication.ReconstructBuilt(publication);

        await Assert.That(reconstructed.SnapshotId)
            .IsEqualTo(DocumentPublicationContract.ComputeSnapshotId(publication.Documents));
        await Assert.That(reconstructed.Snapshots).Count().IsEqualTo(18);

        var corrupt = publication with
        {
            Documents = publication.Documents.Select(document =>
                document.Name == "roster-b04"
                    ? document with { Content = RemoveTeamAccumulatedRows(document.Content) }
                    : document).ToArray()
        };
        await Assert.That(() => BundesligaRosterPublication.ReconstructBuilt(corrupt))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Strict_reconstruction_preserves_historical_v1_without_team_accumulated_rows()
    {
        var (snapshots, rows) = SeedSnapshots();
        var current = BundesligaRosterPublication.Build(snapshots, rows);
        var legacyDocuments = current.Documents.Select(document => document with
        {
            Content = document.Kind == DocumentPublicationKind.Context
                && (document.Name == BundesligaRosterPublicationContract.AggregateRosterDocumentName
                    || document.Name.StartsWith("roster-", StringComparison.Ordinal))
                    ? RemoveTeamAccumulatedRows(document.Content)
                    : document.Content
        }).ToArray();
        var legacy = current with
        {
            Documents = legacyDocuments,
            MetadataJson = current.MetadataJson.Replace(
                BundesligaRosterPublication.MetadataContract,
                BundesligaRosterPublication.LegacyMetadataContract,
                StringComparison.Ordinal)
        };

        var reconstructed = BundesligaRosterPublication.ReconstructLastKnownGood(Load(legacy));

        await Assert.That(reconstructed.Snapshots).Count().IsEqualTo(18);
        await Assert.That(reconstructed.Snapshots.Sum(snapshot => snapshot.Members.Count)).IsEqualTo(
            snapshots.Sum(snapshot => snapshot.Members.Count));
    }

    [Test]
    [Arguments("missing")]
    [Arguments("duplicate")]
    [Arguments("misplaced")]
    [Arguments("malformed-irrelevant")]
    [Arguments("malformed-total")]
    [Arguments("incorrect-total")]
    public async Task V2_reconstruction_rejects_corrupt_team_accumulated_rows(string scenario)
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);
        var target = publication.Documents.Single(document => document.Name == "roster-b04");
        var lines = target.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
        var accumulated = lines[^1];
        switch (scenario)
        {
            case "missing": lines.RemoveAt(lines.Count - 1); break;
            case "duplicate": lines.Add(accumulated); break;
            case "misplaced": lines.RemoveAt(lines.Count - 1); lines.Insert(2, accumulated); break;
            case "malformed-irrelevant": lines[^1] = accumulated.Replace(",N/A,N/A,N/A,", ",Not A Team,N/A,N/A,", StringComparison.Ordinal); break;
            case "malformed-total": lines[^1] = accumulated[..accumulated.LastIndexOf(',')] + ",0"; break;
            case "incorrect-total": lines[^1] = accumulated[..accumulated.LastIndexOf(',')] + ",1"; break;
        }
        var corrupted = string.Join("\r\n", lines) + "\r\n";
        var changed = publication with
        {
            Documents = publication.Documents.Select(document => document.Name == target.Name
                ? document with { Content = corrupted }
                : document).ToArray()
        };

        await Assert.That(() => BundesligaRosterPublication.ReconstructLastKnownGood(Load(changed)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reconstruction_rejects_aggregate_corruption_even_when_prompt_rows_exist()
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1,
            payload.Name == BundesligaRosterPublicationContract.AggregateRosterDocumentName ? payload.Content + "corrupt" : payload.Content,
            payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var snapshot = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(publication.Documents), null, DateTimeOffset.UtcNow, publication.MetadataJson,
            documents.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));

        await Assert.That(() => BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(snapshot, documents)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reconstruction_rejects_a_self_consistent_but_semantically_invalid_metadata_mutation()
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var snapshot = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(publication.Documents), null, DateTimeOffset.UtcNow,
            publication.MetadataJson.Replace("\"duckDbGateResult\":\"NotAvailable\"", "\"duckDbGateResult\":\"Pass\"", StringComparison.Ordinal),
            documents.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));

        await Assert.That(() => BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(snapshot, documents)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Build_rejects_false_quality_counts_and_noncanonical_provenance_before_emitting_metadata()
    {
        var (snapshots, rows) = SeedSnapshots();
        var falseCount = rows.Select((row, index) => index == 0 ? row with { PlayerCount = row.PlayerCount + 1 } : row).ToArray();
        var falseReason = rows.Select((row, index) => index == 0 ? row with { SelectionReason = "DUCKDB_GATES_PASSED" } : row).ToArray();

        await Assert.That(() => BundesligaRosterPublication.Build(snapshots, falseCount)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterPublication.Build(snapshots, falseReason)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Launch_overlay_provenance_requires_not_evaluated_gate_diagnostic_and_explicit_reason()
    {
        var (snapshots, rows) = SeedSnapshots();
        var overlayRows = rows.Select(row => row with
        {
            SourceRevision = "fixture@launch-overlay",
            DuckDbSnapshotAsOf = new DateOnly(2026, 8, 13),
            DuckDbGateResult = BundesligaRosterDuckDbGateResult.NotEvaluated,
            SelectionReason = "LAUNCH_ENRICHMENT_OVERLAY_USE_FALLBACK_SEED",
            Diagnostics = row.Diagnostics.Append("LAUNCH_ENRICHMENT_OVERLAY")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        }).ToArray();

        var publication = BundesligaRosterPublication.Build(snapshots, overlayRows);
        await Assert.That(BundesligaRosterPublication.ReconstructBuilt(publication).QualityRows
            .All(row => row.SelectionReason == "LAUNCH_ENRICHMENT_OVERLAY_USE_FALLBACK_SEED"))
            .IsTrue();

        var wrongGate = overlayRows.Select((row, index) => index == 0
            ? row with { DuckDbGateResult = BundesligaRosterDuckDbGateResult.Rejected }
            : row).ToArray();
        var missingDiagnostic = overlayRows.Select((row, index) => index == 0
            ? row with { Diagnostics = row.Diagnostics.Where(value => value != "LAUNCH_ENRICHMENT_OVERLAY").ToArray() }
            : row).ToArray();
        await Assert.That(() => BundesligaRosterPublication.Build(snapshots, wrongGate))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterPublication.Build(snapshots, missingDiagnostic))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reconstruction_rejects_noncanonical_member_array_and_lkg_provenance_mutations()
    {
        var (snapshots, rows) = SeedSnapshots();
        var publication = BundesligaRosterPublication.Build(snapshots, rows);
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var snapshot = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(publication.Documents), null, DateTimeOffset.UtcNow,
            publication.MetadataJson.Replace("\"members\":[{\"role\":\"Coach\"", "\"members\":[{\"role\":\"Player\"", StringComparison.Ordinal),
            documents.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));

        await Assert.That(() => BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(snapshot, documents)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reconstruction_rejects_direct_lkg_id_date_and_revision_provenance_mutations()
    {
        var (snapshots, rows) = SeedSnapshots();
        var lkgSnapshots = snapshots.Select(snapshot => snapshot with { MembershipSource = BundesligaRosterMembershipSource.LastKnownGood }).ToArray();
        var lkgRows = rows.Select(row => row with
        {
            SelectedSource = BundesligaRosterMembershipSource.LastKnownGood,
            LastKnownGoodSnapshotId = new string('a', 64),
            SelectionReason = "DUCKDB_NOT_AVAILABLE_USE_LAST_KNOWN_GOOD"
        }).ToArray();
        var publication = BundesligaRosterPublication.Build(lkgSnapshots, lkgRows);
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var entries = documents.Select(document => new DocumentPublicationEntry(document.Kind, document.Name, document.Version,
            DocumentPublicationContract.ComputeContentSha256(document.Content)));
        var snapshotId = DocumentPublicationContract.ComputeSnapshotId(publication.Documents);
        foreach (var metadata in new[]
                 {
                     publication.MetadataJson.Replace(new string('a', 64), "not-a-sha", StringComparison.Ordinal),
                     publication.MetadataJson.Replace("\"membershipAsOf\":\"2026-08-16\"", "\"membershipAsOf\":\"2026-08-17\"", StringComparison.Ordinal),
                     publication.MetadataJson.Replace("\"sourceRevision\":null", "\"sourceRevision\":\"unexpected\"", StringComparison.Ordinal)
                 })
        {
            var mutated = new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
                snapshotId, null, DateTimeOffset.UtcNow, metadata, entries);
            await Assert.That(() => BundesligaRosterPublication.ReconstructLastKnownGood(new LoadedDocumentPublication(mutated, documents)))
                .Throws<InvalidDataException>();
        }
    }

    private static (IReadOnlyList<BundesligaRosterClubSnapshot>, IReadOnlyList<BundesligaRosterQualityReportRow>) SeedSnapshots()
    {
        var seed = BundesligaRosterSeed.Default;
        var snapshots = seed.Entries.GroupBy(entry => entry.TeamSlug).Select(group =>
        {
            var team = BundesligaTeamManifest.Default.GetByTeamSlug(group.Key);
            return new BundesligaRosterClubSnapshot(team, group.First().MembershipAsOf, BundesligaRosterMembershipSource.FallbackSeed,
                group.Select(entry => new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId)).ToArray());
        }).OrderBy(snapshot => snapshot.Team.TeamSlug, StringComparer.Ordinal).ToArray();
        var rows = snapshots.Select(snapshot =>
        {
            var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
            var diagnostics = players.Any(player => player.TransfermarktPlayerId is null)
                ? new[] { $"MISSING_STABLE_PLAYER_IDS:{players.Count(player => player.TransfermarktPlayerId is null)}" }
                : Array.Empty<string>();
            return new BundesligaRosterQualityReportRow(snapshot.Team, snapshot.MembershipSource, snapshot.MembershipAsOf,
                [snapshot.Team.OfficialRosterSourceUrl], null, null, null, players.Length, 1,
                players.Count(player => player.TransfermarktPlayerId is not null), 0, 0, 0,
                BundesligaRosterDuckDbGateResult.NotAvailable, "DUCKDB_NOT_AVAILABLE_USE_FALLBACK_SEED", diagnostics);
        }).ToArray();
        return (snapshots, rows);
    }

    private static LoadedDocumentPublication Load(BundesligaRosterBuiltPublication publication)
    {
        var documents = publication.Documents.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27, "roster-test", BundesligaDocumentPublication.RosterPublicationSet,
            payload.Kind, payload.Name, index + 1, payload.Content, payload.Description, DateTimeOffset.UtcNow)).ToArray();
        var snapshot = new DocumentPublicationSnapshot(
            CompetitionIds.Bundesliga2026_27,
            "roster-test",
            BundesligaDocumentPublication.RosterPublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(publication.Documents),
            null,
            DateTimeOffset.UtcNow,
            publication.MetadataJson,
            documents.Select(document => new DocumentPublicationEntry(
                document.Kind,
                document.Name,
                document.Version,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));
        return new LoadedDocumentPublication(snapshot, documents);
    }

    private static string RemoveTeamAccumulatedRows(string content) => string.Join(
        "\r\n",
        content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.Contains(",Team Accumulated,", StringComparison.Ordinal))) + "\r\n";
}
