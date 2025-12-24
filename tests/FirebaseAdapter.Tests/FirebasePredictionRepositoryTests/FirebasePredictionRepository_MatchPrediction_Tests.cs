using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository.SavePredictionAsync and GetPredictionAsync methods.
/// </summary>
public class FirebasePredictionRepository_MatchPrediction_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Saving_prediction_can_be_retrieved()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();
        var prediction = CreateTestPrediction(homeGoals: 3, awayGoals: 0);

        // Act
        await repository.SavePredictionAsync(
            match,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: ["standings", "form"]);

        var retrieved = await repository.GetPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved).IsNotNull()
            .And.Member(r => r!.HomeGoals, h => h.IsEqualTo(3))
            .And.Member(r => r!.AwayGoals, a => a.IsEqualTo(0));
    }

    [Test]
    public async Task Getting_non_existent_prediction_returns_null()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        // Act
        var result = await repository.GetPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HasPredictionAsync_returns_true_when_prediction_exists()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();
        var prediction = CreateTestPrediction();

        await repository.SavePredictionAsync(
            match,
            prediction,
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var result = await repository.HasPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasPredictionAsync_returns_false_when_prediction_does_not_exist()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        // Act
        var result = await repository.HasPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Saving_prediction_updates_existing_prediction()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 1, awayGoals: 1),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 3, awayGoals: 2),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: []);

        var retrieved = await repository.GetPredictionAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(retrieved!).Member(r => r.HomeGoals, h => h.IsEqualTo(3))
            .And.Member(r => r.AwayGoals, a => a.IsEqualTo(2));
    }

    [Test]
    public async Task Predictions_are_isolated_by_model()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 1, awayGoals: 0),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 2, awayGoals: 2),
            model: "o3",
            tokenUsage: "100",
            cost: 0.05,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act
        var gpt4Result = await repository.GetPredictionAsync(match, "gpt-4o", "test-community");
        var o3Result = await repository.GetPredictionAsync(match, "o3", "test-community");

        // Assert
        await Assert.That(gpt4Result!.HomeGoals).IsEqualTo(1);
        await Assert.That(o3Result!.HomeGoals).IsEqualTo(2);
    }

    [Test]
    public async Task Predictions_are_isolated_by_community()
    {
        // Arrange
        var repository = CreateRepository();
        var match = CreateTestMatch();

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 1, awayGoals: 0),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-a",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            match,
            CreateTestPrediction(homeGoals: 3, awayGoals: 1),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "community-b",
            contextDocumentNames: []);

        // Act
        var communityAResult = await repository.GetPredictionAsync(match, "gpt-4o", "community-a");
        var communityBResult = await repository.GetPredictionAsync(match, "gpt-4o", "community-b");

        // Assert
        await Assert.That(communityAResult!.HomeGoals).IsEqualTo(1);
        await Assert.That(communityBResult!.HomeGoals).IsEqualTo(3);
    }

    [Test]
    public async Task GetPredictionMetadataAsync_returns_metadata_with_context_document_names()
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
            contextDocumentNames: ["standings", "form", "injuries"]);

        // Act
        var metadata = await repository.GetPredictionMetadataAsync(
            match,
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(metadata).IsNotNull()
            .And.Member(m => m!.ContextDocumentNames, n => n.Contains("standings").And.Contains("form").And.Contains("injuries"));
    }
}
