using EHonda.KicktippAi.Core;
using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository match and matchday methods.
/// </summary>
public class FirebasePredictionRepository_Match_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task StoreMatchAsync_stores_match_and_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(matchday: 10);

        // Act
        await repository.StoreMatchAsync(match);
        var matches = await repository.GetMatchDayAsync(10);

        // Assert
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0]).IsEqualTo(match);
    }

    [Test]
    public async Task GetMatchDayAsync_returns_empty_list_when_no_matches_exist()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var matches = await repository.GetMatchDayAsync(99);

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task GetMatchDayAsync_returns_matches_for_specific_matchday()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.StoreMatchAsync(CreateMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 5));
        await repository.StoreMatchAsync(CreateMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 5));
        await repository.StoreMatchAsync(CreateMatch(homeTeam: "Team E", awayTeam: "Team F", matchday: 6));

        // Act
        var matchday5 = await repository.GetMatchDayAsync(5);
        var matchday6 = await repository.GetMatchDayAsync(6);

        // Assert
        await Assert.That(matchday5).HasCount().EqualTo(2);
        await Assert.That(matchday6).HasCount().EqualTo(1);
    }

    [Test]
    public async Task GetMatchDayWithPredictionsAsync_returns_matches_with_predictions()
    {
        // Arrange
        var repository = CreateRepository();
        var match1 = CreateMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 10);
        var match2 = CreateMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 10);
        var prediction1 = CreatePrediction(homeGoals: 2, awayGoals: 1);

        await repository.StoreMatchAsync(match1);
        await repository.StoreMatchAsync(match2);

        await repository.SavePredictionAsync(
            match1,
            prediction1,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var matchPredictions = await repository.GetMatchDayWithPredictionsAsync(
            matchDay: 10,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(matchPredictions).HasCount().EqualTo(2);

        var withPrediction = matchPredictions.FirstOrDefault(mp => mp.Match.HomeTeam == "Team A");
        var withoutPrediction = matchPredictions.FirstOrDefault(mp => mp.Match.HomeTeam == "Team C");

        await Assert.That(withPrediction).IsNotNull();
        await Assert.That(withPrediction!.Match).IsEqualTo(match1);
        await Assert.That(withPrediction!.Prediction).IsEqualTo(prediction1);

        await Assert.That(withoutPrediction).IsNotNull();
        await Assert.That(withoutPrediction!.Match).IsEqualTo(match2);
        await Assert.That(withoutPrediction!.Prediction).IsNull();
    }

    [Test]
    public async Task GetAllPredictionsAsync_returns_all_predictions_for_model_and_community()
    {
        // Arrange
        var repository = CreateRepository();
        var match1 = CreateMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 1);
        var match2 = CreateMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 2);

        await repository.SavePredictionAsync(
            match1,
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            match2,
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var predictions = await repository.GetAllPredictionsAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(predictions).HasCount().EqualTo(2);
    }
}
