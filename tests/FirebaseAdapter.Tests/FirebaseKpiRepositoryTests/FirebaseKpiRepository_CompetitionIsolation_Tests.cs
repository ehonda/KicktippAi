using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

public class FirebaseKpiRepository_CompetitionIsolation_Tests(FirestoreFixture fixture)
    : FirebaseKpiRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Current_bundesliga_queries_ignore_unscoped_and_world_cup_documents()
    {
        const string documentName = "squad-summary";
        const string communityContext = "shared-community";
        var bundesligaRepository = CreateRepository();
        var worldCupRepository = CreateRepository(competition: Option.Some(CompetitionIds.FifaWorldCup2026));

        await Fixture.Db.Collection("kpi-documents")
            .Document($"{documentName}_{communityContext}_0")
            .SetAsync(new Dictionary<string, object>
            {
                ["id"] = $"{documentName}_{communityContext}_0",
                ["documentName"] = documentName,
                ["content"] = "unscoped content",
                ["description"] = "unscoped",
                ["version"] = 0,
                ["createdAt"] = Timestamp.GetCurrentTimestamp(),
                ["communityContext"] = communityContext
            });
        await worldCupRepository.SaveKpiDocumentAsync(
            documentName,
            "world cup content",
            "world cup",
            communityContext);

        var bundesligaDocument = await bundesligaRepository.GetKpiDocumentAsync(documentName, communityContext);
        var worldCupDocument = await worldCupRepository.GetKpiDocumentAsync(documentName, communityContext);

        await Assert.That(bundesligaDocument).IsNull();
        await Assert.That(worldCupDocument?.Content).IsEqualTo("world cup content");
    }

    [Test]
    public async Task Current_bundesliga_ids_partition_communities()
    {
        const string documentName = "squad-summary";
        var repository = CreateRepository();

        await repository.SaveKpiDocumentAsync(documentName, "alpha", "alpha", "community-a");
        await repository.SaveKpiDocumentAsync(documentName, "bravo", "bravo", "community-b");

        var snapshot = await Fixture.Db.Collection("kpi-documents").GetSnapshotAsync();
        var ids = snapshot.Documents.Select(document => document.Id).ToArray();
        await Assert.That(ids).Contains($"{CompetitionIds.Bundesliga2026_27}_{documentName}_community-a_0");
        await Assert.That(ids).Contains($"{CompetitionIds.Bundesliga2026_27}_{documentName}_community-b_0");
    }
}
