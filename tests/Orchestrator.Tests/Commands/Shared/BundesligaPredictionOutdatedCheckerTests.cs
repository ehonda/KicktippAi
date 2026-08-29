using EHonda.KicktippAi.Core;
using Moq;
using Orchestrator.Commands.Shared;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Shared;

public sealed class BundesligaPredictionOutdatedCheckerTests
{
    [Test]
    public async Task Same_version_ordinary_payload_content_mutation_is_outdated()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var changedName = manifest.Documents.First(document =>
            !IsReserved(document.Name)
            && !string.Equals(document.Name, "bundesliga-standings.csv", StringComparison.Ordinal)).Name;
        var original = ordinary[changedName];
        ordinary[changedName] = new ContextDocument(
            original.DocumentName,
            "same version but different bytes",
            original.Version,
            original.CreatedAt);

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Newer_standings_version_and_content_is_not_outdated()
    {
        var match = CreateBundesligaMatch();
        var recordedDocuments = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: recordedDocuments);
        var original = recordedDocuments["bundesliga-standings.csv"];
        var currentDocuments = new Dictionary<string, ContextDocument>(recordedDocuments, StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new ContextDocument(
            original.DocumentName,
            "Position,Team,Points\n1,FC Bayern München,3",
            original.Version + 1,
                original.CreatedAt.AddHours(1))
        };
        var contextRepository = CreateMockContextRepositoryWithDocuments(currentDocuments);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", original.Version, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            contextRepository.Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

        await Assert.That(isOutdated).IsFalse();
    }

    [Test]
    public async Task Missing_latest_standings_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                "bundesliga-standings.csv", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Latest_standings_version_rollback_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary["bundesliga-standings.csv"];
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                "bundesliga-standings.csv", "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                original.DocumentName,
                original.Content,
                original.Version - 1,
                original.CreatedAt));

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Recorded_standings_exact_read_scope_corruption_propagates_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary["bundesliga-standings.csv"];
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", original.Version, "test-community", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("Recorded standings scope is corrupt."));

        await Assert.That(() => IsOutdatedAsync(contextRepository.Object, match, manifest))
            .Throws<InvalidDataException>()
            .WithMessageContaining("scope is corrupt");
    }

    [Test]
    public async Task Missing_recorded_standings_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", 1, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Hash_tampered_recorded_standings_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary["bundesliga-standings.csv"];
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", original.Version, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(original.DocumentName, "tampered", original.Version, original.CreatedAt));

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Hash_tampered_standings_manifest_entry_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var original = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var tampered = ResolvedMatchContextManifest.Create(
            original.Competition,
            original.CommunityContext,
            original.Documents.Select(document => document.Name == "bundesliga-standings.csv"
                ? new ResolvedMatchContextDocument(
                    document.Name,
                    document.Version,
                    document.Kind,
                    new string('f', DocumentPublicationContract.Sha256HexLength))
                : document),
            original.RosterPublicationSnapshotId,
            original.ClubEloPublicationSnapshotId);

        var isOutdated = await IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            match,
            tampered);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Version_tampered_recorded_standings_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary["bundesliga-standings.csv"];
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", original.Version, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(original.DocumentName, original.Content, original.Version + 1, original.CreatedAt));

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Malformed_recorded_standings_identity_is_outdated_fail_closed()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary["bundesliga-standings.csv"];
        var contextRepository = CreateMockContextRepositoryWithDocuments(ordinary);
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv", original.Version, "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument("wrong-standings.csv", original.Content, original.Version, original.CreatedAt));

        var isOutdated = await IsOutdatedAsync(contextRepository.Object, match, manifest);

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    [Arguments("recent-history-fcb.csv")]
    [Arguments("community-rules-test-community.md")]
    public async Task Changed_non_exempt_ordinary_document_is_outdated(string documentName)
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var original = ordinary[documentName];
        ordinary[documentName] = new ContextDocument(
            original.DocumentName,
            original.Content + "\nchanged",
            original.Version + 1,
            original.CreatedAt.AddHours(1));

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Valid_shape_manifest_with_tampered_roster_hash_is_outdated()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = TamperHash(CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary), "roster-");

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    public async Task Valid_shape_manifest_with_tampered_club_elo_hash_is_outdated()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = TamperHash(CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary), "club-elo-");

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

        await Assert.That(isOutdated).IsTrue();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Changed_publication_snapshot_is_outdated(bool rosterChanged)
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var changedManifest = ResolvedMatchContextManifest.Create(
            manifest.Competition,
            manifest.CommunityContext,
            manifest.Documents,
            rosterChanged ? new string('a', DocumentPublicationContract.Sha256HexLength) : manifest.RosterPublicationSnapshotId,
            rosterChanged ? manifest.ClubEloPublicationSnapshotId : new string('b', DocumentPublicationContract.Sha256HexLength));

        var isOutdated = await BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            CreateMockContextRepositoryWithDocuments(ordinary).Object,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                changedManifest.Documents.Select(document => document.Name).ToList(), changedManifest));

        await Assert.That(isOutdated).IsTrue();
    }

    private static Dictionary<string, ContextDocument> CreateOrdinaryDocuments(Match match) =>
        MatchContextDocumentCatalog.ForMatch(match, "test-community", CompetitionIds.Bundesliga2026_27)
            .RequiredDocumentNames
            .Where(name => !IsReserved(name))
            .ToDictionary(name => name, name => CreateContextDocument(documentName: name, content: name, version: 1));

    private static ResolvedMatchContextManifest TamperHash(ResolvedMatchContextManifest manifest, string namePrefix) =>
        ResolvedMatchContextManifest.Create(
            manifest.Competition,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name.StartsWith(namePrefix, StringComparison.Ordinal)
                ? new ResolvedMatchContextDocument(
                    document.Name,
                    document.Version,
                    document.Kind,
                    new string('f', DocumentPublicationContract.Sha256HexLength))
                : document),
            manifest.RosterPublicationSnapshotId,
            manifest.ClubEloPublicationSnapshotId);

    private static Task<bool> IsOutdatedAsync(
        IContextRepository contextRepository,
        Match match,
        ResolvedMatchContextManifest manifest) =>
        BundesligaPredictionOutdatedChecker.IsOutdatedAsync(
            contextRepository,
            CreateMockBundesligaDocumentPublicationRepository().Object,
            match,
            "test-community",
            new PredictionMetadata(new Prediction(2, 1), DateTimeOffset.UtcNow,
                manifest.Documents.Select(document => document.Name).ToList(), manifest));

    private static Match CreateBundesligaMatch() =>
        CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund", matchday: 1);

    private static bool IsReserved(string name) =>
        name.StartsWith("roster-", StringComparison.Ordinal) || name.StartsWith("club-elo-", StringComparison.Ordinal);
}
