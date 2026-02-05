using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository available filter discovery methods
/// (GetAvailableMatchdaysAsync, GetAvailableModelsAsync, GetAvailableCommunityContextsAsync).
/// </summary>
public class FirebasePredictionRepository_AvailableFilters_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    // --- GetAvailableMatchdaysAsync ---

    [Test]
    public async Task GetAvailableMatchdaysAsync_returns_empty_when_no_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var matchdays = await repository.GetAvailableMatchdaysAsync();

        // Assert
        await Assert.That(matchdays).IsEmpty();
    }

    [Test]
    public async Task GetAvailableMatchdaysAsync_returns_sorted_unique_matchdays()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 3),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 1),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team E", awayTeam: "Team F", matchday: 3),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var matchdays = await repository.GetAvailableMatchdaysAsync();

        // Assert
        await Assert.That(matchdays).IsEquivalentTo([1, 3]);
    }

    // --- GetAvailableModelsAsync ---

    [Test]
    public async Task GetAvailableModelsAsync_returns_empty_when_no_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var models = await repository.GetAvailableModelsAsync();

        // Assert
        await Assert.That(models).IsEmpty();
    }

    [Test]
    public async Task GetAvailableModelsAsync_returns_unique_models_from_match_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team A", awayTeam: "Team B"),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team C", awayTeam: "Team D"),
            CreatePrediction(),
            model: "o3",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var models = await repository.GetAvailableModelsAsync();

        // Assert
        await Assert.That(models).Contains("gpt-4o").And.Contains("o3");
        await Assert.That(models).HasCount().EqualTo(2);
    }

    [Test]
    public async Task GetAvailableModelsAsync_returns_unique_models_from_bonus_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 1"),
            CreateBonusPrediction(),
            model: "o3-mini",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var models = await repository.GetAvailableModelsAsync();

        // Assert
        await Assert.That(models).Contains("o3-mini");
    }

    [Test]
    public async Task GetAvailableModelsAsync_deduplicates_models_across_match_and_bonus_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team A", awayTeam: "Team B"),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 1"),
            CreateBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var models = await repository.GetAvailableModelsAsync();

        // Assert
        await Assert.That(models).HasCount().EqualTo(1);
        await Assert.That(models).Contains("gpt-4o");
    }

    // --- GetAvailableCommunityContextsAsync ---

    [Test]
    public async Task GetAvailableCommunityContextsAsync_returns_empty_when_no_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var contexts = await repository.GetAvailableCommunityContextsAsync();

        // Assert
        await Assert.That(contexts).IsEmpty();
    }

    [Test]
    public async Task GetAvailableCommunityContextsAsync_returns_unique_contexts_from_match_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team A", awayTeam: "Team B"),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-a",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team C", awayTeam: "Team D"),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-b",
            contextDocumentNames: []);

        // Act
        var contexts = await repository.GetAvailableCommunityContextsAsync();

        // Assert
        await Assert.That(contexts).Contains("community-a").And.Contains("community-b");
        await Assert.That(contexts).HasCount().EqualTo(2);
    }

    [Test]
    public async Task GetAvailableCommunityContextsAsync_returns_unique_contexts_from_bonus_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 1"),
            CreateBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "bonus-community",
            contextDocumentNames: []);

        // Act
        var contexts = await repository.GetAvailableCommunityContextsAsync();

        // Assert
        await Assert.That(contexts).Contains("bonus-community");
    }

    [Test]
    public async Task GetAvailableCommunityContextsAsync_deduplicates_contexts_across_match_and_bonus_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SavePredictionAsync(
            CreateMatch(homeTeam: "Team A", awayTeam: "Team B"),
            CreatePrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "shared-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 1"),
            CreateBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "shared-community",
            contextDocumentNames: []);

        // Act
        var contexts = await repository.GetAvailableCommunityContextsAsync();

        // Assert
        await Assert.That(contexts).HasCount().EqualTo(1);
        await Assert.That(contexts).Contains("shared-community");
    }
}
