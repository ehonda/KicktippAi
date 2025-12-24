using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Tests for FirebasePredictionRepository cost analysis methods.
/// </summary>
public class FirebasePredictionRepository_Cost_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task GetMatchPredictionCostsByRepredictionIndexAsync_returns_empty_when_no_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var costs = await repository.GetMatchPredictionCostsByRepredictionIndexAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(costs).IsEmpty();
    }

    [Test]
    public async Task GetMatchPredictionCostsByRepredictionIndexAsync_groups_costs_by_reprediction_index()
    {
        // Arrange
        var repository = CreateRepository();
        var match1 = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 1);
        var match2 = CreateTestMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 1);

        // Initial predictions (index 0)
        await repository.SavePredictionAsync(
            match1,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            match2,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Reprediction (index 1)
        await repository.SaveRepredictionAsync(
            match1,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.03,
            communityContext: "test-community",
            contextDocumentNames: [],
            repredictionIndex: 1);

        // Act
        var costs = await repository.GetMatchPredictionCostsByRepredictionIndexAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(costs).ContainsKey(0).And.ContainsKey(1);
        await Assert.That(costs[0]).Member(c => c.cost, c => c.IsGreaterThan(0.02)) // 0.01 + 0.02 = 0.03
            .And.Member(c => c.count, c => c.IsEqualTo(2));
        await Assert.That(costs[1]).Member(c => c.cost, c => c.IsEqualTo(0.03))
            .And.Member(c => c.count, c => c.IsEqualTo(1));
    }

    [Test]
    public async Task GetMatchPredictionCostsByRepredictionIndexAsync_filters_by_matchdays()
    {
        // Arrange
        var repository = CreateRepository();
        var match1 = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B", matchday: 1);
        var match2 = CreateTestMatch(homeTeam: "Team C", awayTeam: "Team D", matchday: 2);

        await repository.SavePredictionAsync(
            match1,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SavePredictionAsync(
            match2,
            CreateTestPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.05,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Act - only matchday 1
        var costs = await repository.GetMatchPredictionCostsByRepredictionIndexAsync(
            model: "gpt-4o",
            communityContext: "test-community",
            matchdays: [1]);

        // Assert
        await Assert.That(costs[0]).Member(c => c.cost, c => c.IsEqualTo(0.01))
            .And.Member(c => c.count, c => c.IsEqualTo(1));
    }

    [Test]
    public async Task GetBonusPredictionCostsByRepredictionIndexAsync_returns_empty_when_no_predictions()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var costs = await repository.GetBonusPredictionCostsByRepredictionIndexAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(costs).IsEmpty();
    }

    [Test]
    public async Task GetBonusPredictionCostsByRepredictionIndexAsync_groups_costs_by_reprediction_index()
    {
        // Arrange
        var repository = CreateRepository();
        var question1 = CreateTestBonusQuestion(text: "Question 1");
        var question2 = CreateTestBonusQuestion(text: "Question 2");

        // Initial predictions (index 0)
        await repository.SaveBonusPredictionAsync(
            question1,
            CreateTestBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.01,
            communityContext: "test-community",
            contextDocumentNames: []);

        await repository.SaveBonusPredictionAsync(
            question2,
            CreateTestBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "100",
            cost: 0.02,
            communityContext: "test-community",
            contextDocumentNames: []);

        // Reprediction (index 1)
        await repository.SaveBonusRepredictionAsync(
            question1,
            CreateTestBonusPrediction(),
            model: "gpt-4o",
            tokenUsage: "150",
            cost: 0.03,
            communityContext: "test-community",
            contextDocumentNames: [],
            repredictionIndex: 1);

        // Act
        var costs = await repository.GetBonusPredictionCostsByRepredictionIndexAsync(
            model: "gpt-4o",
            communityContext: "test-community");

        // Assert
        await Assert.That(costs).ContainsKey(0).And.ContainsKey(1);
        await Assert.That(costs[0].count).IsEqualTo(2);
        await Assert.That(costs[1].count).IsEqualTo(1);
    }
}
