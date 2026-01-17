using FirebaseAdapter.Tests.Fixtures;
using NodaTime;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for cancelled match lookup methods that query by team names only (without startsAt).
/// <para>
/// These methods exist to handle an edge case where cancelled matches have inconsistent
/// startsAt values across different Kicktipp pages. See IPredictionRepository.cs for details.
/// </para>
/// </summary>
public class FirebasePredictionRepository_CancelledMatch_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task GetCancelledMatchPredictionAsync_finds_prediction_by_team_names_only()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        await repository.SavePredictionAsync(
            match,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: ["standings"]);

        // Act - lookup by team names only (ignoring startsAt)
        var retrieved = await repository.GetCancelledMatchPredictionAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved).IsEqualTo(prediction);
    }

    [Test]
    public async Task GetCancelledMatchPredictionAsync_returns_null_when_no_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetCancelledMatchPredictionAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetCancelledMatchPredictionAsync_returns_most_recent_prediction()
    {
        // Arrange
        var repository = CreateRepository();
        // Save two predictions for the same teams with different startsAt values
        // This simulates the real-world scenario where the same match was stored
        // with different timestamps from different pages
        var match1 = CreateMatch(
            homeTeam: "Team A", 
            awayTeam: "Team B",
            startsAt: Instant.FromUtc(2025, 1, 10, 15, 30).InUtc());
        var match2 = CreateMatch(
            homeTeam: "Team A", 
            awayTeam: "Team B",
            startsAt: Instant.FromUtc(1970, 1, 1, 0, 0).InUtc()); // Epoch fallback simulating MinValue
        
        var olderPrediction = CreatePrediction(homeGoals: 1, awayGoals: 0);
        var newerPrediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        // Save older prediction first
        await repository.SavePredictionAsync(
            match1,
            olderPrediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Brief delay to ensure different createdAt timestamps
        await Task.Delay(100);

        // Save newer prediction
        await repository.SavePredictionAsync(
            match2,
            newerPrediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - should return the most recent prediction regardless of startsAt
        var retrieved = await repository.GetCancelledMatchPredictionAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved).IsEqualTo(newerPrediction);
    }

    [Test]
    public async Task GetCancelledMatchPredictionAsync_respects_model_isolation()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var gpt4Prediction = CreatePrediction(homeGoals: 1, awayGoals: 0);
        var o3Prediction = CreatePrediction(homeGoals: 3, awayGoals: 2);

        await repository.SavePredictionAsync(
            match, gpt4Prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "test-community", contextDocumentNames: []);
        await repository.SavePredictionAsync(
            match, o3Prediction, model: "o3", tokenUsage: "200", cost: 0.05,
            communityContext: "test-community", contextDocumentNames: []);

        // Act
        var gpt4Result = await repository.GetCancelledMatchPredictionAsync(
            "Team A", "Team B", "gpt-4o", "test-community");
        var o3Result = await repository.GetCancelledMatchPredictionAsync(
            "Team A", "Team B", "o3", "test-community");

        // Assert
        await Assert.That(gpt4Result).IsEqualTo(gpt4Prediction);
        await Assert.That(o3Result).IsEqualTo(o3Prediction);
    }

    [Test]
    public async Task GetCancelledMatchPredictionMetadataAsync_finds_metadata_by_team_names_only()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var contextDocuments = new[] { "standings", "form-guide" };

        await repository.SavePredictionAsync(
            match,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: contextDocuments);

        // Act
        var metadata = await repository.GetCancelledMatchPredictionMetadataAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.Prediction).IsEqualTo(prediction);
        await Assert.That(metadata.ContextDocumentNames).IsEquivalentTo(contextDocuments);
    }

    [Test]
    public async Task GetCancelledMatchPredictionMetadataAsync_returns_null_when_no_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetCancelledMatchPredictionMetadataAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetCancelledMatchRepredictionIndexAsync_returns_highest_index_for_team_names()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        // Save initial prediction (index 0)
        await repository.SavePredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "test-community", contextDocumentNames: []);

        // Save two repredictions
        await repository.SaveRepredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "test-community", contextDocumentNames: [], repredictionIndex: 1);
        await repository.SaveRepredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "test-community", contextDocumentNames: [], repredictionIndex: 2);

        // Act
        var index = await repository.GetCancelledMatchRepredictionIndexAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(index).IsEqualTo(2);
    }

    [Test]
    public async Task GetCancelledMatchRepredictionIndexAsync_returns_minus_one_when_no_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetCancelledMatchRepredictionIndexAsync(
            homeTeam: "Team A",
            awayTeam: "Team B",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsEqualTo(-1);
    }

    [Test]
    public async Task GetCancelledMatchRepredictionIndexAsync_respects_community_context_isolation()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var prediction = CreatePrediction();

        // Save predictions in different community contexts
        await repository.SavePredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "community-1", contextDocumentNames: []);
        await repository.SaveRepredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "community-1", contextDocumentNames: [], repredictionIndex: 1);

        await repository.SavePredictionAsync(
            match, prediction, model: "gpt-4o", tokenUsage: "100", cost: 0.01,
            communityContext: "community-2", contextDocumentNames: []);

        // Act
        var community1Index = await repository.GetCancelledMatchRepredictionIndexAsync(
            "Team A", "Team B", "gpt-4o", "community-1");
        var community2Index = await repository.GetCancelledMatchRepredictionIndexAsync(
            "Team A", "Team B", "gpt-4o", "community-2");

        // Assert
        await Assert.That(community1Index).IsEqualTo(1);
        await Assert.That(community2Index).IsEqualTo(0);
    }
}
