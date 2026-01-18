using EHonda.KicktippAi.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static Orchestrator.Tests.Commands.Observability.CostCommandTests.CostCommandTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Tests for parsing command-line options in CostCommand.
/// </summary>
public class CostCommand_Parsing_Tests
{
    [Test]
    public async Task Parsing_matchdays_with_all_flag_uses_all_available_matchdays()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--all", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("all (3 found)");
    }

    [Test]
    public async Task Parsing_matchdays_with_csv_values_filters_to_specified_matchdays()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--matchdays", "1,2", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2");
        
        // Verify the repository was called with the filtered matchdays
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<List<int>?>(m => m != null && m.Contains(1) && m.Contains(2) && m.Count == 2),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Parsing_matchdays_with_all_string_uses_all_available_matchdays()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1, 2, 3, 4, 5 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--matchdays", "all", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("all (5 found)");
    }

    [Test]
    public async Task Parsing_matchdays_with_invalid_format_returns_error()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--matchdays", "invalid,not-a-number"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Invalid matchday format");
    }

    [Test]
    public async Task Parsing_models_with_csv_values_filters_to_specified_models()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o", "o1-mini", "o3" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--models", "gpt-4o, o1-mini", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Models: gpt-4o, o1-mini");
        
        // Verify queries were made for the specified models
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "gpt-4o",
            It.IsAny<string>(),
            It.IsAny<List<int>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            "o1-mini",
            It.IsAny<string>(),
            It.IsAny<List<int>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Parsing_models_with_all_string_uses_all_available_models()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o", "o1-mini" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--models", "all", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("all (2 found)");
    }

    [Test]
    public async Task Parsing_community_contexts_with_csv_values_filters_to_specified_contexts()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community", "other" });
        var (app, console, _, repo, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--community-contexts", "test-community,prod-community", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Community Contexts: test-community, prod-community");
        
        // Verify queries were made for the specified contexts
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            It.IsAny<string>(),
            "test-community",
            It.IsAny<List<int>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        repo.Verify(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
            It.IsAny<string>(),
            "prod-community",
            It.IsAny<List<int>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Test]
    public async Task Parsing_community_contexts_with_all_string_uses_all_available_contexts()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community", "prod-community", "other" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--community-contexts", "all", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("all (3 found)");
    }

    [Test]
    public async Task Parsing_with_whitespace_in_csv_trims_values()
    {
        // Arrange
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act - Note the extra spaces around values
        var exitCode = await app.RunAsync(["cost", "--matchdays", "  1  ,  2  ,  3  ", "--verbose"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Matchdays: 1, 2, 3");
    }
}
