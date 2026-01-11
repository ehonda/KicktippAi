using EHonda.KicktippAi.Core;
using FirebaseAdapter.Tests.Fixtures;
using NodaTime.Extensions;
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

    /// <summary>
    /// Verifies that matches with <see cref="DateTimeOffset.MinValue"/> as startsAt can be stored and retrieved.
    /// This edge case can occur when the first match on a matchday is cancelled and there's no previous time to inherit.
    /// See docs/features/cancelled-matches.md for design rationale.
    /// </summary>
    [Test]
    public async Task Storing_match_with_DateTimeOffset_MinValue_succeeds()
    {
        // Arrange
        var repository = CreateRepository();
        
        // Create a match with MinValue as startsAt (edge case for first cancelled match)
        var minValueStartsAt = DateTimeOffset.MinValue.ToZonedDateTime();
        var cancelledMatch = CreateMatch(
            homeTeam: "Cancelled Team A",
            awayTeam: "Cancelled Team B",
            startsAt: minValueStartsAt,
            matchday: 16,
            isCancelled: true);

        // Act
        await repository.StoreMatchAsync(cancelledMatch);
        var matches = await repository.GetMatchDayAsync(16);

        // Assert - match should be stored and retrievable
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Cancelled Team A");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Cancelled Team B");
        await Assert.That(matches[0].IsCancelled).IsTrue();
    }

    /// <summary>
    /// Verifies that predictions can be saved and retrieved for cancelled matches with extreme startsAt values.
    /// </summary>
    [Test]
    public async Task Saving_prediction_for_cancelled_match_with_MinValue_startsAt_succeeds()
    {
        // Arrange
        var repository = CreateRepository();
        
        var minValueStartsAt = DateTimeOffset.MinValue.ToZonedDateTime();
        var cancelledMatch = CreateMatch(
            homeTeam: "Cancelled Team A",
            awayTeam: "Cancelled Team B",
            startsAt: minValueStartsAt,
            matchday: 16,
            isCancelled: true);
        var prediction = CreatePrediction(homeGoals: 1, awayGoals: 2);

        // Act
        await repository.SavePredictionAsync(
            cancelledMatch,
            prediction,
            model: "test-model",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrievedPrediction = await repository.GetPredictionAsync(
            cancelledMatch,
            model: "test-model",
            communityContext: "test-community");

        // Assert - prediction should be stored and retrievable
        await Assert.That(retrievedPrediction).IsNotNull();
        await Assert.That(retrievedPrediction!.HomeGoals).IsEqualTo(1);
        await Assert.That(retrievedPrediction.AwayGoals).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that matches stored without the IsCancelled property (backward compatibility)
    /// default to IsCancelled = false when retrieved.
    /// This ensures existing documents in the database (created before the IsCancelled property was added)
    /// continue to work correctly.
    /// </summary>
    [Test]
    public async Task Reading_match_without_IsCancelled_property_defaults_to_false()
    {
        // Arrange - Insert a document directly without the IsCancelled field
        // This simulates a document created before the IsCancelled property was added
        var documentId = Guid.NewGuid().ToString();
        var matchData = new Dictionary<string, object>
        {
            ["homeTeam"] = "Legacy Team A",
            ["awayTeam"] = "Legacy Team B",
            ["startsAt"] = Google.Cloud.Firestore.Timestamp.FromDateTime(DateTime.UtcNow),
            ["matchday"] = 20,
            ["competition"] = "bundesliga-2025-26"
            // Note: IsCancelled is intentionally NOT included
        };

        await Fixture.Db.Collection("matches")
            .Document(documentId)
            .SetAsync(matchData);

        // Act - Read through the repository
        var repository = CreateRepository();
        var matches = await repository.GetMatchDayAsync(20);

        // Assert - IsCancelled should default to false
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Legacy Team A");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Legacy Team B");
        await Assert.That(matches[0].IsCancelled).IsFalse();
    }
}
