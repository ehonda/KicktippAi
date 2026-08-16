using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using TestUtilities;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public class FirebasePredictionRepository_CompetitionIsolation_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Current_bundesliga_query_ignores_unscoped_and_world_cup_predictions()
    {
        const string communityContext = "shared-community";
        var match = CreateMatch();
        var bundesligaRepository = CreateRepository();
        var worldCupRepository = CreateRepository(
            competition: NullableOption.Some(CompetitionIds.FifaWorldCup2026));

        await Fixture.Db.Collection("match-predictions")
            .Document("legacy-unscoped-prediction")
            .SetAsync(new Dictionary<string, object>
            {
                ["id"] = "legacy-unscoped-prediction",
                ["homeTeam"] = match.HomeTeam,
                ["awayTeam"] = match.AwayTeam,
                ["startsAt"] = Timestamp.FromDateTimeOffset(match.StartsAt.ToInstant().ToDateTimeOffset()),
                ["model"] = "gpt-5",
                ["communityContext"] = communityContext
            });
        await worldCupRepository.SavePredictionAsync(
            match,
            CreatePrediction(homeGoals: 2, awayGoals: 1),
            model: "gpt-5",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: communityContext,
            contextDocumentNames: []);

        var bundesligaPrediction = await bundesligaRepository.GetPredictionAsync(match, "gpt-5", communityContext);
        var worldCupPrediction = await worldCupRepository.GetPredictionAsync(match, "gpt-5", communityContext);

        await Assert.That(bundesligaPrediction).IsNull();
        await Assert.That(worldCupPrediction).IsEqualTo(CreatePrediction(homeGoals: 2, awayGoals: 1));
    }

    [Test]
    public async Task Current_bundesliga_predictions_store_explicit_competition_and_community_fields()
    {
        var repository = CreateRepository();
        var match = CreateMatch();
        var question = CreateBonusQuestion(text: "Who will win?");

        await repository.SavePredictionAsync(
            match,
            CreatePrediction(),
            "gpt-5",
            "100",
            0.01,
            "community-a",
            []);
        await repository.SaveBonusPredictionAsync(
            question,
            new BonusPrediction([question.Options[0].Id]),
            "gpt-5",
            "100",
            0.01,
            "community-b",
            []);

        var matchSnapshot = await Fixture.Db.Collection("match-predictions").GetSnapshotAsync();
        var bonusSnapshot = await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync();

        await Assert.That(matchSnapshot.Count).IsEqualTo(1);
        await Assert.That(Guid.TryParse(matchSnapshot.Documents[0].Id, out _)).IsTrue();
        await Assert.That(matchSnapshot.Documents[0].GetValue<string>("competition"))
            .IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(matchSnapshot.Documents[0].GetValue<string>("communityContext"))
            .IsEqualTo("community-a");

        await Assert.That(bonusSnapshot.Count).IsEqualTo(1);
        await Assert.That(Guid.TryParse(bonusSnapshot.Documents[0].Id, out _)).IsTrue();
        await Assert.That(bonusSnapshot.Documents[0].GetValue<string>("competition"))
            .IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(bonusSnapshot.Documents[0].GetValue<string>("communityContext"))
            .IsEqualTo("community-b");
    }
}
