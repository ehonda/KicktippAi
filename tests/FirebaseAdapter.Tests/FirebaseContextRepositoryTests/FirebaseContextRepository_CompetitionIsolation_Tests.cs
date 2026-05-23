using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

public class FirebaseContextRepository_CompetitionIsolation_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    [Test]
    public async Task World_cup_context_documents_are_isolated_from_legacy_bundesliga_documents()
    {
        var bundesligaRepository = CreateRepository();
        var worldCupRepository = CreateRepository(competition: Option.Some(CompetitionIds.FifaWorldCup2026));

        await bundesligaRepository.SaveContextDocumentAsync(
            "bundesliga-standings.csv",
            "legacy content",
            "ehonda-dev-wm26");
        await worldCupRepository.SaveContextDocumentAsync(
            "fifa-world-cup-2026-standings.csv",
            "world cup content",
            "ehonda-dev-wm26");

        var bundesligaDocument = await bundesligaRepository.GetLatestContextDocumentAsync(
            "bundesliga-standings.csv",
            "ehonda-dev-wm26");
        var worldCupDocument = await worldCupRepository.GetLatestContextDocumentAsync(
            "fifa-world-cup-2026-standings.csv",
            "ehonda-dev-wm26");
        var missingAcrossCompetition = await worldCupRepository.GetLatestContextDocumentAsync(
            "bundesliga-standings.csv",
            "ehonda-dev-wm26");

        await Assert.That(bundesligaDocument?.Content).IsEqualTo("legacy content");
        await Assert.That(worldCupDocument?.Content).IsEqualTo("world cup content");
        await Assert.That(missingAcrossCompetition).IsNull();
    }
}
