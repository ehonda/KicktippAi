using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Shared;

public sealed class BundesligaPredictionOutdatedCheckerTests
{
    [Test]
    public async Task Same_version_ordinary_payload_content_mutation_is_outdated()
    {
        var match = CreateBundesligaMatch();
        var ordinary = CreateOrdinaryDocuments(match);
        var manifest = CreateCanonicalBundesligaResolvedContextManifest(match, ordinaryDocuments: ordinary);
        var changedName = manifest.Documents.First(document => !IsReserved(document.Name)).Name;
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

    private static Match CreateBundesligaMatch() =>
        CreateMatch(homeTeam: "FC Bayern München", awayTeam: "Borussia Dortmund", matchday: 1);

    private static bool IsReserved(string name) =>
        name.StartsWith("roster-", StringComparison.Ordinal) || name.StartsWith("club-elo-", StringComparison.Ordinal);
}
