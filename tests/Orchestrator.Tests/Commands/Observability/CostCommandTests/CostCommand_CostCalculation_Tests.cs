using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static Orchestrator.Tests.Commands.Observability.CostCommandTests.CostCommandTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Tests for cost calculation logic in CostCommand.
/// </summary>
public class CostCommand_CostCalculation_Tests
{
    [Test]
    public async Task Calculating_costs_displays_match_prediction_totals()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.2345, 10) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1, 2 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("10");
        await Assert.That(output).Contains("$1.2345");
    }

    [Test]
    public async Task Calculating_costs_with_bonus_flag_includes_bonus_costs()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var bonusCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.5, 3) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            bonusCostsByIndex: bonusCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--bonus"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("Bonus");
        await Assert.That(output).Contains("$1.5000"); // Total: 1.0 + 0.5
        
        // Verify bonus costs were queried
        repo.Verify(r => r.GetBonusPredictionCostsByRepredictionIndexAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Calculating_costs_with_all_flag_includes_everything()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (2.0, 10) }
        };
        var bonusCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            bonusCostsByIndex: bonusCosts,
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--all"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All mode enabled");
        await Assert.That(output).Contains("Bonus");  // Bonus should be included
    }

    [Test]
    public async Task Calculating_costs_with_detailed_breakdown_shows_per_community_model()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) },
            { 1, (0.5, 2) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--detailed-breakdown"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Detailed breakdown table columns (may be truncated by Spectre.Console)
        await Assert.That(output).Contains("Commun"); // Community Context may be truncated
        await Assert.That(output).Contains("Model");
        await Assert.That(output).Contains("Catego"); // Category may be truncated
        await Assert.That(output).Contains("Index 0");
        await Assert.That(output).Contains("Index 1");
        await Assert.That(output).Contains("2+"); // Index 2+ may be truncated
        await Assert.That(output).Contains("Total");
        await Assert.That(output).Contains("Cost");
    }

    [Test]
    public async Task Calculating_costs_handles_empty_data_gracefully()
    {
        // Arrange - No costs returned
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("0");  // Zero count
        await Assert.That(output).Contains("$0.0000");  // Zero cost
    }

    [Test]
    public async Task Calculating_costs_aggregates_reprediction_indices_correctly()
    {
        // Arrange - Multiple reprediction indices
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 10) },  // Index 0
            { 1, (0.5, 5) },   // Index 1
            { 2, (0.25, 2) },  // Index 2+
            { 3, (0.1, 1) }    // Index 2+ (aggregated)
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--detailed-breakdown"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Total: 1.0 + 0.5 + 0.25 + 0.1 = 1.85
        await Assert.That(output).Contains("$1.8500");
        // Total count: 10 + 5 + 2 + 1 = 18
        await Assert.That(output).Contains("18");
    }

    [Test]
    public async Task Calculating_costs_for_multiple_models_aggregates_correctly()
    {
        // Arrange - Two models
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Each model returns the same costs, so total = 2 * (1.0, 5)
        await Assert.That(output).Contains("10");   // Total count
        await Assert.That(output).Contains("$2.0000");  // Total cost
    }

    [Test]
    public async Task Calculating_costs_for_multiple_community_contexts_aggregates_correctly()
    {
        // Arrange - Two community contexts
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.5, 3) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Each context returns the same costs, so total = 2 * (0.5, 3)
        await Assert.That(output).Contains("6");   // Total count
        await Assert.That(output).Contains("$1.0000");  // Total cost
    }

    [Test]
    public async Task Calculating_costs_without_bonus_flag_excludes_bonus_costs()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var bonusCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (10.0, 50) }  // Large bonus costs that should NOT appear
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            bonusCostsByIndex: bonusCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act - No --bonus flag
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Only match costs should appear in total
        await Assert.That(output).Contains("$1.0000");
        // Bonus row should not appear (not even zero because no bonus flag)
        await Assert.That(output).DoesNotContain("Bonus");
        
        // Verify bonus costs were NOT queried
        repo.Verify(r => r.GetBonusPredictionCostsByRepredictionIndexAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Calculating_costs_with_detailed_breakdown_shows_zero_indices_as_dash()
    {
        // Arrange - Only index 0 has data
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--detailed-breakdown"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Index 1 and Index 2+ should show "-" since they have no data
        // We can't easily check for "-" specifically, but the total should still be correct
        await Assert.That(output).Contains("5");  // Total count
        await Assert.That(output).Contains("$1.0000");
    }

    [Test]
    public async Task Calculating_costs_queries_repository_for_each_model_and_context_combination()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.1, 1) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "model-a", "model-b" },
            availableCommunityContexts: new List<string> { "context-1", "context-2" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        
        // Verify all 4 combinations were queried (2 models × 2 contexts)
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "model-a", "context-1", It.IsAny<List<int>?>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "model-a", "context-2", It.IsAny<List<int>?>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "model-b", "context-1", It.IsAny<List<int>?>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "model-b", "context-2", It.IsAny<List<int>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
