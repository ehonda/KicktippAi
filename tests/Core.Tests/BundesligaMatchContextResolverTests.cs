using EHonda.KicktippAi.Core;
using Moq;
using Match = EHonda.KicktippAi.Core.Match;

namespace Core.Tests;

public sealed class BundesligaMatchContextResolverTests
{
    private const string Community = "ehonda-dev-buli-2627";
    private static readonly Match Match = new("FC Bayern München", "Borussia Dortmund", default, 1);

    [Test]
    public async Task Live_resolution_uses_one_publication_head_per_reserved_set_and_never_queries_reserved_documents_generically()
    {
        var context = CreateContextRepository();
        var rosters = CreateCanonicalRosterPublication();
        var elo = CreateCanonicalClubEloPublication();
        var publications = CreatePublicationRepository(rosters, elo);

        var resolved = await new BundesligaMatchContextResolver(context.Object, publications.Object)
            .ResolveLiveAsync(Match, Community);

        await Assert.That(resolved.Documents.Length).IsEqualTo(11);
        await Assert.That(resolved.Manifest.RosterPublicationSnapshotId).IsEqualTo(rosters.Snapshot.SnapshotId);
        await Assert.That(resolved.Manifest.ClubEloPublicationSnapshotId).IsEqualTo(elo.Snapshot.SnapshotId);
        await Assert.That(resolved.Manifest.Documents.Single(document => document.Name == "roster-fcb").Version)
            .IsEqualTo(rosters.Documents.Single(document => document.Name == "roster-fcb").Version);
        await Assert.That(resolved.Manifest.Documents.Single(document => document.Name == "club-elo-bvb.csv").Version)
            .IsEqualTo(elo.Documents.Single(document => document.Name == "club-elo-bvb.csv").Version);
        context.Verify(repository => repository.GetLatestContextDocumentAsync(
            It.Is<string>(name => name.StartsWith("roster-", StringComparison.Ordinal) || name.StartsWith("club-elo-", StringComparison.Ordinal)),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Live_resolution_materializes_an_ordinary_document_then_records_the_exact_reread_version()
    {
        var missing = "head-to-head-fcb-vs-bvb.csv";
        var context = CreateContextRepository(missing);
        context.Setup(repository => repository.SaveContextDocumentAsync(missing, "generated", Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        context.Setup(repository => repository.GetContextDocumentAsync(missing, 42, Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(missing, "generated", 42, DateTimeOffset.UnixEpoch));
        var publications = CreatePublicationRepository(CreateCanonicalRosterPublication(), CreateCanonicalClubEloPublication());

        var resolved = await new BundesligaMatchContextResolver(context.Object, publications.Object).ResolveLiveAsync(
            Match,
            Community,
            (name, _) => Task.FromResult<DocumentContext?>(new DocumentContext(name, "generated")));

        await Assert.That(resolved.Manifest.Documents.Single(document => document.Name == missing).Version).IsEqualTo(42);
        context.Verify(repository => repository.SaveContextDocumentAsync(missing, "generated", Community, It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(repository => repository.GetContextDocumentAsync(missing, 42, Community, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Recorded_resolution_uses_the_original_snapshot_versions_after_heads_advance()
    {
        var context = CreateContextRepository();
        var originalRosters = CreateCanonicalRosterPublication();
        var originalElo = CreateCanonicalClubEloPublication();
        var publications = CreatePublicationRepository(
            WithVersionAdvance(originalRosters, 10),
            WithVersionAdvance(originalElo, 10));
        publications.Setup(repository => repository.GetSnapshotAsync(BundesligaDocumentPublication.Rosters, Community, originalRosters.Snapshot.SnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalRosters);
        publications.Setup(repository => repository.GetSnapshotAsync(BundesligaDocumentPublication.ClubElo, Community, originalElo.Snapshot.SnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalElo);
        var manifest = ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            Community,
            MatchContextDocumentCatalog.ForMatch(Match, Community, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames.Select(name =>
                name switch
                {
                    "roster-fcb" => new ResolvedMatchContextDocument(name, originalRosters.Documents.Single(document => document.Name == name).Version),
                    "roster-bvb" => new ResolvedMatchContextDocument(name, originalRosters.Documents.Single(document => document.Name == name).Version),
                    "club-elo-fcb.csv" => new ResolvedMatchContextDocument(name, originalElo.Documents.Single(document => document.Name == name).Version),
                    "club-elo-bvb.csv" => new ResolvedMatchContextDocument(name, originalElo.Documents.Single(document => document.Name == name).Version),
                    _ => new ResolvedMatchContextDocument(name, 1)
                }),
            originalRosters.Snapshot.SnapshotId,
            originalElo.Snapshot.SnapshotId);

        var resolved = await new BundesligaMatchContextResolver(context.Object, publications.Object)
            .ResolveRecordedAsync(Match, manifest);

        await Assert.That(resolved.Documents.Single(document => document.Name == "roster-fcb").Content)
            .IsEqualTo(originalRosters.Documents.Single(document => document.Name == "roster-fcb").Content);
        await Assert.That(resolved.Documents.Single(document => document.Name == "club-elo-bvb.csv").Content)
            .IsEqualTo(originalElo.Documents.Single(document => document.Name == "club-elo-bvb.csv").Content);
        publications.Verify(repository => repository.GetLastKnownGoodAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Recorded_resolution_fails_clearly_when_a_selected_snapshot_payload_is_missing()
    {
        var context = CreateContextRepository();
        var publications = CreatePublicationRepository(CreateCanonicalRosterPublication(), CreateCanonicalClubEloPublication());
        var missingSnapshot = new string('a', DocumentPublicationContract.Sha256HexLength);
        publications.Setup(repository => repository.GetSnapshotAsync(BundesligaDocumentPublication.Rosters, Community, missingSnapshot, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);
        var manifest = ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            Community,
            MatchContextDocumentCatalog.ForMatch(Match, Community, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames.Select(name => new ResolvedMatchContextDocument(name, 1)),
            missingSnapshot,
            new string('b', DocumentPublicationContract.Sha256HexLength));

        await Assert.That(() => new BundesligaMatchContextResolver(context.Object, publications.Object)
                .ResolveRecordedAsync(Match, manifest))
            .Throws<InvalidDataException>()
            .WithMessageContaining(missingSnapshot);
    }

    [Test]
    public async Task Live_resolution_rejects_a_hash_valid_but_semantically_corrupt_publication_metadata()
    {
        var rosters = WithMetadata(CreateCanonicalRosterPublication(), "{}");
        var publications = CreatePublicationRepository(rosters, CreateCanonicalClubEloPublication());

        await Assert.That(() => new BundesligaMatchContextResolver(CreateContextRepository().Object, publications.Object)
                .ResolveLiveAsync(Match, Community))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Manifest_rejects_noncanonical_document_order_and_snapshot_ids()
    {
        var names = MatchContextDocumentCatalog.ForMatch(Match, Community, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames;
        await Assert.That(() => ResolvedMatchContextManifest.Create(
                CompetitionIds.Bundesliga2026_27,
                Community,
                names.Select(name => new ResolvedMatchContextDocument(name, 1)),
                new string('A', DocumentPublicationContract.Sha256HexLength),
                new string('b', DocumentPublicationContract.Sha256HexLength)))
            .Throws<ArgumentException>();

        var noncanonicalOrder = ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            Community,
            names.Reverse().Select(name => new ResolvedMatchContextDocument(name, 1)),
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));
        await Assert.That(() => ResolvedMatchContextManifest.ValidateForMatch(noncanonicalOrder, Match, Community))
            .Throws<InvalidDataException>();
    }

    private static Mock<IContextRepository> CreateContextRepository(string? missing = null)
    {
        var repository = new Mock<IContextRepository>(MockBehavior.Strict);
        var names = MatchContextDocumentCatalog.ForMatch(Match, Community, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames
            .Where(name => !name.StartsWith("roster-", StringComparison.Ordinal) && !name.StartsWith("club-elo-", StringComparison.Ordinal));
        foreach (var name in names)
        {
            if (name == missing)
            {
                repository.Setup(value => value.GetLatestContextDocumentAsync(name, Community, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ContextDocument?)null);
                continue;
            }

            repository.Setup(value => value.GetLatestContextDocumentAsync(name, Community, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContextDocument(name, name, 1, DateTimeOffset.UnixEpoch));
            repository.Setup(value => value.GetContextDocumentAsync(name, 1, Community, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContextDocument(name, name, 1, DateTimeOffset.UnixEpoch));
        }

        return repository;
    }

    private static Mock<IDocumentPublicationRepository> CreatePublicationRepository(LoadedDocumentPublication rosterHead, LoadedDocumentPublication eloHead)
    {
        var repository = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        repository.SetupGet(value => value.Competition).Returns(CompetitionIds.Bundesliga2026_27);
        repository.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rosterHead);
        repository.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eloHead);
        return repository;
    }

    private static LoadedDocumentPublication CreateCanonicalRosterPublication() => CreateCanonicalPublication(
        BundesligaDocumentPublication.Rosters,
        BundesligaRosterPublication.Build(
            BundesligaRosterSeed.Default.Entries.GroupBy(entry => entry.TeamSlug).Select(group => new BundesligaRosterClubSnapshot(
                BundesligaTeamManifest.Default.GetByTeamSlug(group.Key), group.First().MembershipAsOf,
                BundesligaRosterMembershipSource.FallbackSeed,
                group.Select(entry => new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId)).ToArray())).ToArray(),
            BundesligaRosterSeed.Default.Entries.GroupBy(entry => entry.TeamSlug).Select(group =>
            {
                var team = BundesligaTeamManifest.Default.GetByTeamSlug(group.Key);
                var players = group.Where(entry => entry.Role == BundesligaRosterRole.Player).ToArray();
                return new BundesligaRosterQualityReportRow(team, BundesligaRosterMembershipSource.FallbackSeed, group.First().MembershipAsOf,
                    [team.OfficialRosterSourceUrl], null, null, null, players.Length, 1, players.Count(player => player.TransfermarktPlayerId is not null),
                    0, 0, 0, BundesligaRosterDuckDbGateResult.NotAvailable, "DUCKDB_NOT_AVAILABLE_USE_FALLBACK_SEED", []);
            }).ToArray()).Documents,
        BundesligaRosterPublication.Build(
            BundesligaRosterSeed.Default.Entries.GroupBy(entry => entry.TeamSlug).Select(group => new BundesligaRosterClubSnapshot(
                BundesligaTeamManifest.Default.GetByTeamSlug(group.Key), group.First().MembershipAsOf,
                BundesligaRosterMembershipSource.FallbackSeed,
                group.Select(entry => new BundesligaRosterMember(entry.Role, entry.Name, entry.TransfermarktPlayerId)).ToArray())).ToArray(),
            BundesligaRosterSeed.Default.Entries.GroupBy(entry => entry.TeamSlug).Select(group =>
            {
                var team = BundesligaTeamManifest.Default.GetByTeamSlug(group.Key);
                var players = group.Where(entry => entry.Role == BundesligaRosterRole.Player).ToArray();
                return new BundesligaRosterQualityReportRow(team, BundesligaRosterMembershipSource.FallbackSeed, group.First().MembershipAsOf,
                    [team.OfficialRosterSourceUrl], null, null, null, players.Length, 1, players.Count(player => player.TransfermarktPlayerId is not null),
                    0, 0, 0, BundesligaRosterDuckDbGateResult.NotAvailable, "DUCKDB_NOT_AVAILABLE_USE_FALLBACK_SEED", []);
            }).ToArray()).MetadataJson);

    private static LoadedDocumentPublication CreateCanonicalClubEloPublication()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
            BundesligaClubEloSelectionDisposition.NetworkDisabled, ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        return CreateCanonicalPublication(BundesligaDocumentPublication.ClubElo, build.Documents, build.MetadataJson);
    }

    private static LoadedDocumentPublication CreateCanonicalPublication(
        DocumentPublicationDefinition definition,
        IReadOnlyList<DocumentPublicationPayload> payloads,
        string metadataJson)
    {
        var documents = payloads.Select((payload, index) => new PublishedDocument(
            CompetitionIds.Bundesliga2026_27,
            Community,
            definition.PublicationSet,
            payload.Kind,
            payload.Name, index + 1, payload.Content, payload.Description,
            DateTimeOffset.UnixEpoch)).ToArray();
        return new LoadedDocumentPublication(
            new DocumentPublicationSnapshot(
                CompetitionIds.Bundesliga2026_27,
                Community,
                definition.PublicationSet,
                DocumentPublicationContract.ComputeSnapshotId(payloads),
                null,
                DateTimeOffset.UnixEpoch,
                metadataJson,
                documents.Select(document => new DocumentPublicationEntry(
                    document.Kind,
                    document.Name,
                    document.Version,
                    DocumentPublicationContract.ComputeContentSha256(document.Content)))),
            documents);
    }

    private static LoadedDocumentPublication WithVersionAdvance(LoadedDocumentPublication publication, int versionDelta) => new(
        new DocumentPublicationSnapshot(
            publication.Snapshot.Competition,
            publication.Snapshot.CommunityContext,
            publication.Snapshot.PublicationSet,
            publication.Snapshot.SnapshotId,
            publication.Snapshot.PreviousSnapshotId,
            publication.Snapshot.CreatedAt,
            publication.Snapshot.MetadataJson,
            publication.Snapshot.Documents.Select(entry => entry with { Version = entry.Version + versionDelta }).ToArray()),
        publication.Documents.Select(document => document with { Version = document.Version + versionDelta }).ToArray());

    private static LoadedDocumentPublication WithMetadata(LoadedDocumentPublication publication, string metadataJson) => new(
        new DocumentPublicationSnapshot(publication.Snapshot.Competition, publication.Snapshot.CommunityContext,
            publication.Snapshot.PublicationSet, publication.Snapshot.SnapshotId, publication.Snapshot.PreviousSnapshotId,
            publication.Snapshot.CreatedAt, metadataJson, publication.Snapshot.Documents), publication.Documents);
}
