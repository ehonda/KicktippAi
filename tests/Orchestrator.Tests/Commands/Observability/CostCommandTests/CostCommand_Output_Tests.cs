using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static Orchestrator.Tests.Commands.Observability.CostCommandTests.CostCommandTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Tests for console output formatting in CostCommand.
/// </summary>
public class CostCommand_Output_Tests
{
    [Test]
    public async Task Output_displays_success_message_on_completion()
    {
        // Arrange
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
        await Assert.That(output).Contains("Cost command initialized");
        await Assert.That(output).Contains("Cost calculation completed");
    }

    [Test]
    public async Task Output_displays_summary_table_with_category_count_cost()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.5, 10) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Summary table columns
        await Assert.That(output).Contains("Category");
        await Assert.That(output).Contains("Count");
        await Assert.That(output).Contains("Cost (USD)");
        // Data
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("10");
        await Assert.That(output).Contains("$1.5000");
        // Total row
        await Assert.That(output).Contains("Total");
    }

    [Test]
    public async Task Verbose_mode_shows_filter_information()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Filters:");
        await Assert.That(output).Contains("Matchdays:");
        await Assert.That(output).Contains("Models:");
        await Assert.That(output).Contains("Community Contexts:");
        await Assert.That(output).Contains("Include Bonus:");
    }

    [Test]
    public async Task Verbose_mode_shows_per_document_costs()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.1234, 3) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing model: gpt-4o");
        await Assert.That(output).Contains("test-community");
        await Assert.That(output).Contains("Match predictions");
        await Assert.That(output).Contains("documents");
        await Assert.That(output).Contains("$0.1234");
    }

    [Test]
    public async Task Non_verbose_mode_hides_detailed_output()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (0.1234, 3) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act - No --verbose flag
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
        await Assert.That(output).DoesNotContain("Filters:");
        await Assert.That(output).DoesNotContain("Processing model:");
    }

    [Test]
    public async Task All_mode_displays_all_mode_message()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--all"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("All mode enabled - aggregating over all available data");
    }

    [Test]
    public async Task Detailed_breakdown_table_shows_all_columns()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) },
            { 1, (0.5, 3) },
            { 2, (0.25, 2) }
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
        // All required columns (may be truncated by Spectre.Console)
        await Assert.That(output).Contains("Commun"); // Community Context may be truncated
        await Assert.That(output).Contains("Model");
        await Assert.That(output).Contains("Catego"); // Category may be truncated
        await Assert.That(output).Contains("Index 0");
        await Assert.That(output).Contains("Index 1");
        await Assert.That(output).Contains("2+"); // Index 2+ may be truncated
        await Assert.That(output).Contains("Total");
        await Assert.That(output).Contains("USD");
    }

    [Test]
    public async Task Detailed_breakdown_with_bonus_shows_match_and_bonus_rows()
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
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--detailed-breakdown", "--bonus"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Both Match and Bonus categories should appear
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("Bonus");
    }

    [Test]
    public async Task Summary_table_with_bonus_shows_match_and_bonus_rows()
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
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--bonus"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Match");
        await Assert.That(output).Contains("5");
        await Assert.That(output).Contains("$1.0000");
        await Assert.That(output).Contains("Bonus");
        await Assert.That(output).Contains("3");
        await Assert.That(output).Contains("$0.5000");
        await Assert.That(output).Contains("Total");
        await Assert.That(output).Contains("8");  // 5 + 3
        await Assert.That(output).Contains("$1.5000");  // 1.0 + 0.5
    }

    [Test]
    public async Task Verbose_mode_with_bonus_shows_bonus_prediction_details()
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
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--bonus", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Include Bonus: True");
        await Assert.That(output).Contains("Bonus predictions");
        await Assert.That(output).Contains("$0.5000");
    }

    [Test]
    public async Task Output_uses_invariant_culture_for_cost_formatting()
    {
        // Arrange - Use a cost that would show differently with comma decimal separator
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1234.5678, 10) }
        };
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            matchCostsByIndex: matchCosts,
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        // Should use period as decimal separator (InvariantCulture)
        await Assert.That(output).Contains("$1234.5678");
        // Should NOT use comma as decimal separator
        await Assert.That(output).DoesNotContain("$1234,5678");
    }
}
