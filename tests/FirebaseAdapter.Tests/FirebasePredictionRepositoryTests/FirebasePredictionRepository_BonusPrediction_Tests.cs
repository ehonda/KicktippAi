using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository bonus prediction methods.
/// </summary>
public class FirebasePredictionRepository_BonusPrediction_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_bonus_prediction_can_be_retrieved_by_text()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win the league?");
        var prediction = CreateTestBonusPrediction(new List<string> { "opt-1", "opt-2" });

        // Act
        await repository.SaveBonusPredictionAsync(
            question,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: ["standings"]);

        var retrieved = await repository.GetBonusPredictionByTextAsync(
            "Who will win the league?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.SelectedOptionIds).Contains("opt-1");
        await Assert.That(retrieved.SelectedOptionIds).Contains("opt-2");
    }

    [Test]
    public async Task Getting_non_existent_bonus_prediction_returns_null()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetBonusPredictionByTextAsync(
            "Non-existent question",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Saving_bonus_prediction_updates_existing_prediction()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win?");

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-1" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-2", "opt-3" }),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetBonusPredictionByTextAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved!.SelectedOptionIds).HasCount().EqualTo(2);
        await Assert.That(retrieved.SelectedOptionIds).Contains("opt-2");
        await Assert.That(retrieved.SelectedOptionIds).Contains("opt-3");
    }

    [Test]
    public async Task Bonus_predictions_are_isolated_by_model()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win?");

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-1" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-2" }),
            model: "o3",
            tokenUsage: "100",
            cost: 0.05,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var gpt4Result = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "test-community");
        var o3Result = await repository.GetBonusPredictionByTextAsync("Who will win?", "o3", "test-community");

        // Assert
        await Assert.That(gpt4Result!.SelectedOptionIds).Contains("opt-1");
        await Assert.That(o3Result!.SelectedOptionIds).Contains("opt-2");
    }

    [Test]
    public async Task Bonus_predictions_are_isolated_by_community()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateTestBonusQuestion(text: "Who will win?");

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-1" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-a",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            question,
            CreateTestBonusPrediction(new List<string> { "opt-3" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-b",
            contextDocumentNames: []);

        // Act
        var communityAResult = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "community-a");
        var communityBResult = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "community-b");

        // Assert
        await Assert.That(communityAResult!.SelectedOptionIds).Contains("opt-1");
        await Assert.That(communityBResult!.SelectedOptionIds).Contains("opt-3");
    }

    [Test]
    public async Task GetBonusPredictionMetadataByTextAsync_returns_metadata_with_context_document_names()
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
            contextDocumentNames: ["team-data", "manager-data"]);

        // Act
        var metadata = await repository.GetBonusPredictionMetadataByTextAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ContextDocumentNames).Contains("team-data");
        await Assert.That(metadata.ContextDocumentNames).Contains("manager-data");
    }

    [Test]
    public async Task GetAllBonusPredictionsAsync_returns_all_predictions_for_model_and_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveBonusPredictionAsync(
            CreateTestBonusQuestion(text: "Question 1"),
            CreateTestBonusPrediction(new List<string> { "opt-1" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            CreateTestBonusQuestion(text: "Question 2"),
            CreateTestBonusPrediction(new List<string> { "opt-2" }),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var predictions = await repository.GetAllBonusPredictionsAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(predictions).HasCount().EqualTo(2);
    }
}
