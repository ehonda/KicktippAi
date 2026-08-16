using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

public class FirebaseContextRepository_CompetitionIsolation_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Current_bundesliga_documents_are_isolated_from_unscoped_and_world_cup_documents()
    {
        const string documentName = "shared-context.csv";
        const string communityContext = "shared-community";
        var bundesligaRepository = CreateRepository();
        var worldCupRepository = CreateRepository(competition: Option.Some(CompetitionIds.FifaWorldCup2026));

        await Fixture.Db.Collection("context-documents")
            .Document($"{documentName}_{communityContext}_0")
            .SetAsync(new Dictionary<string, object>
            {
                ["id"] = $"{documentName}_{communityContext}_0",
                ["documentName"] = documentName,
                ["content"] = "unscoped content",
                ["version"] = 0,
                ["createdAt"] = Google.Cloud.Firestore.Timestamp.GetCurrentTimestamp(),
                ["communityContext"] = communityContext
            });
        await worldCupRepository.SaveContextDocumentAsync(documentName, "world cup content", communityContext);

        var bundesligaDocument = await bundesligaRepository.GetLatestContextDocumentAsync(documentName, communityContext);
        var worldCupDocument = await worldCupRepository.GetLatestContextDocumentAsync(documentName, communityContext);

        await Assert.That(bundesligaDocument).IsNull();
        await Assert.That(worldCupDocument?.Content).IsEqualTo("world cup content");
    }

    [Test]
    public async Task Current_bundesliga_ids_include_competition_and_community()
    {
        const string documentName = "club-elo.csv";
        const string communityContext = "ehonda-dev-buli-2627";
        var repository = CreateRepository();

        await repository.SaveContextDocumentAsync(documentName, "content", communityContext);

        var snapshot = await Fixture.Db.Collection("context-documents").GetSnapshotAsync();
        await Assert.That(snapshot.Documents.Select(document => document.Id))
            .Contains($"{CompetitionIds.Bundesliga2026_27}_{documentName}_{communityContext}_0");
    }
}
