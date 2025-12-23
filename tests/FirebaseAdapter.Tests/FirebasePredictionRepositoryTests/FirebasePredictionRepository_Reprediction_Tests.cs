using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository reprediction methods.
/// </summary>
public class FirebasePredictionRepository_Reprediction_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task GetMatchRepredictionIndexAsync_returns_negative_one_when_no_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        // Act
        var index = await repository.GetMatchRepredictionIndexAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(index).IsEqualTo(-1);
    }

    [Test]
    public async Task GetMatchRepredictionIndexAsync_returns_zero_for_first_prediction()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var index = await repository.GetMatchRepredictionIndexAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(index).IsEqualTo(0);
    }

    [Test]
    public async Task SaveRepredictionAsync_creates_new_prediction_with_specified_index()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        // Save initial prediction (index 0)
        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 1, awayGoals: 0),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - save reprediction with index 1
        await repository.SaveRepredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 2, awayGoals: 2),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: [],
            repredictionIndex: 1);

        var latestIndex = await repository.GetMatchRepredictionIndexAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        var latestPrediction = await repository.GetPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(latestIndex).IsEqualTo(1);
        await Assert.That(latestPrediction!.HomeGoals).IsEqualTo(2);
        await Assert.That(latestPrediction.AwayGoals).IsEqualTo(2);
    }

    [Test]
    public async Task GetBonusRepredictionIndexAsync_returns_negative_one_when_no_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var index = await repository.GetBonusRepredictionIndexAsync(
            "Non-existent question",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(index).IsEqualTo(-1);
    }

    [Test]
    public async Task GetBonusRepredictionIndexAsync_returns_zero_for_first_prediction()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win?");

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var index = await repository.GetBonusRepredictionIndexAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(index).IsEqualTo(0);
    }

    [Test]
    public async Task SaveBonusRepredictionAsync_creates_new_prediction_with_specified_index()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win?");

        // Save initial prediction (index 0)
        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-1" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - save reprediction with index 1
        await repository.SaveBonusRepredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-2", "opt-3" }),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: [],
            repredictionIndex: 1);

        var latestIndex = await repository.GetBonusRepredictionIndexAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        var latestPrediction = await repository.GetBonusPredictionByTextAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(latestIndex).IsEqualTo(1);
        await Assert.That(latestPrediction!.SelectedOptionIds).HasCount().EqualTo(2);
        await Assert.That(latestPrediction.SelectedOptionIds).Contains("opt-2");
    }
}
