using EHonda.KicktippAi.Core;
using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

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
        var question = CreateBonusQuestion(text: "Who will win the league?");
        var prediction = new BonusPrediction(["opt-1", "opt-2"]);

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
        await Assert.That(retrieved!.SelectedOptionIds).IsEquivalentTo(prediction.SelectedOptionIds);
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
        var question = CreateBonusQuestion(text: "Who will win?");
        var updatedPrediction = new BonusPrediction(["opt-2", "opt-3"]);

        await repository.SaveBonusPredictionAsync(
            question,
            new BonusPrediction(["opt-1"]),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        await repository.SaveBonusPredictionAsync(
            question,
            updatedPrediction,
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
        await Assert.That(retrieved!.SelectedOptionIds).IsEquivalentTo(updatedPrediction.SelectedOptionIds);
    }

    [Test]
    public async Task Bonus_predictions_are_isolated_by_model()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Who will win?");
        var gpt4Prediction = new BonusPrediction(["opt-1"]);
        var o3Prediction = new BonusPrediction(["opt-2"]);

        await repository.SaveBonusPredictionAsync(
            question,
            gpt4Prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            question,
            o3Prediction,
            model: "o3",
            tokenUsage: "100",
            cost: 0.05,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var gpt4Result = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "test-community");
        var o3Result = await repository.GetBonusPredictionByTextAsync("Who will win?", "o3", "test-community");

        // Assert
        await Assert.That(gpt4Result!.SelectedOptionIds).IsEquivalentTo(gpt4Prediction.SelectedOptionIds);
        await Assert.That(o3Result!.SelectedOptionIds).IsEquivalentTo(o3Prediction.SelectedOptionIds);
    }

    [Test]
    public async Task Bonus_predictions_are_isolated_by_community()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Who will win?");
        var communityAPrediction = new BonusPrediction(["opt-1"]);
        var communityBPrediction = new BonusPrediction(["opt-3"]);

        await repository.SaveBonusPredictionAsync(
            question,
            communityAPrediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-a",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            question,
            communityBPrediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-b",
            contextDocumentNames: []);

        // Act
        var communityAResult = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "community-a");
        var communityBResult = await repository.GetBonusPredictionByTextAsync("Who will win?", "gpt-4o", "community-b");

        // Assert
        await Assert.That(communityAResult!.SelectedOptionIds).IsEquivalentTo(communityAPrediction.SelectedOptionIds);
        await Assert.That(communityBResult!.SelectedOptionIds).IsEquivalentTo(communityBPrediction.SelectedOptionIds);
    }

    [Test]
    public async Task GetBonusPredictionMetadataByTextAsync_returns_metadata_with_context_document_names()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Who will win?");
        var expectedDocumentNames = new List<string> { "team-data", "manager-data" };

        await repository.SaveBonusPredictionAsync(
            question,
            CreateBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: expectedDocumentNames);

        // Act
        var metadata = await repository.GetBonusPredictionMetadataByTextAsync(
            "Who will win?",
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ContextDocumentNames).IsEquivalentTo(expectedDocumentNames);
    }

    [Test]
    public async Task GetAllBonusPredictionsAsync_returns_all_predictions_for_model_and_community()
    {
        // Arrange
        var repository = CreateRepository();

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 1"),
            new BonusPrediction(["opt-1"]),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            CreateBonusQuestion(text: "Question 2"),
            new BonusPrediction(["opt-2"]),
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

    [Test]
    public async Task GetBonusPredictionAsync_by_questionId_returns_null_for_non_existent()
    {
        // Arrange
        var repository = CreateRepository();

        // Act - Query by questionId (this method queries by questionId field which is not populated by SaveBonusPredictionAsync)
        var result = await repository.GetBonusPredictionAsync("non-existent-id", "gpt-4o", "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetBonusPredictionAsync_by_questionId_returns_null_for_text_saved_predictions()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Who will win?");
        var prediction = new BonusPrediction(["opt-1", "opt-2"]);

        // Save using the standard method (stores by questionText, not questionId)
        await repository.SaveBonusPredictionAsync(
            question,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - Query by questionId (which is not stored by SaveBonusPredictionAsync)
        var retrieved = await repository.GetBonusPredictionAsync("some-question-id", "gpt-4o", "test-community");

        // Assert - Should return null since questionId field is not populated
        await Assert.That(retrieved).IsNull();
    }

    [Test]
    public async Task HasBonusPredictionAsync_by_questionId_returns_false_when_not_exists()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var exists = await repository.HasBonusPredictionAsync("non-existent-id", "gpt-4o", "test-community");

        // Assert
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task HasBonusPredictionAsync_by_questionId_returns_false_for_text_saved_predictions()
    {
        // Arrange
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Top scorer?");
        var prediction = new BonusPrediction(["opt-1"]);

        // Save using the standard method (stores by questionText, not questionId)
        await repository.SaveBonusPredictionAsync(
            question,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - Check by questionId (which is not stored)
        var exists = await repository.HasBonusPredictionAsync("some-question-id", "gpt-4o", "test-community");

        // Assert - Should be false since questionId field is not populated
        await Assert.That(exists).IsFalse();
    }
}
