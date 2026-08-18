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
}
