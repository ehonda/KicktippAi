using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Observability.Cost;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static Orchestrator.Tests.Commands.Observability.CostCommandTests.CostCommandTestFactories;

namespace Orchestrator.Tests.Commands.Observability.CostCommandTests;

/// <summary>
/// Tests for error handling in CostCommand.
/// </summary>
public class CostCommand_ErrorHandling_Tests
{
    [Test]
    public async Task Running_command_handles_repository_exception_gracefully()
    {
        // Arrange
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Firestore connection failed"));
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to calculate costs");
    }

    [Test]
    public async Task Running_command_logs_error_message_on_failure()
    {
        // Arrange
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        var logger = new FakeLogger<CostCommand>();
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo, logger);

        // Act
        var exitCode = await app.RunAsync(["cost"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        
        // Verify error was logged
        var errorLogs = logger.Collector.GetSnapshot()
            .Where(log => log.Level == Microsoft.Extensions.Logging.LogLevel.Error)
            .ToList();
        await Assert.That(errorLogs.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Running_command_handles_cost_query_exception_gracefully()
    {
        // Arrange
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "gpt-4o" });
        mockRepo.Setup(r => r.GetAvailableCommunityContextsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "test-community" });
        mockRepo.Setup(r => r.GetAvailableMatchdaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1 });
        mockRepo.Setup(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<int>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Query failed"));
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to calculate costs");
    }

    [Test]
    public async Task Running_command_displays_error_message_with_exception_details()
    {
        // Arrange
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Specific error message for testing"));
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Specific error message for testing");
    }

    [Test]
    public async Task Running_command_handles_empty_models_list()
    {
        // Arrange - No models available
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string>(),  // Empty!
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert - Should succeed with zero costs (no models means no queries)
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("$0.0000");
    }

    [Test]
    public async Task Running_command_handles_empty_community_contexts_list()
    {
        // Arrange - No community contexts available
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int> { 1 },
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string>());  // Empty!
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert - Should succeed with zero costs (no contexts means no queries)
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("$0.0000");
    }

    [Test]
    public async Task Running_command_handles_empty_matchdays_list()
    {
        // Arrange - No matchdays available
        var mockRepo = CreateMockPredictionRepositoryForCosts(
            availableMatchdays: new List<int>(),  // Empty!
            availableModels: new List<string> { "gpt-4o" },
            availableCommunityContexts: new List<string> { "test-community" });
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost"]);
        var output = console.Output;

        // Assert - Should succeed (matchdays are passed to query, empty list is valid filter)
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Running_command_handles_bonus_query_exception_gracefully()
    {
        // Arrange
        var matchCosts = new Dictionary<int, (double cost, int count)>
        {
            { 0, (1.0, 5) }
        };
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "gpt-4o" });
        mockRepo.Setup(r => r.GetAvailableCommunityContextsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "test-community" });
        mockRepo.Setup(r => r.GetAvailableMatchdaysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { 1 });
        mockRepo.Setup(r => r.GetMatchPredictionCostsByRepredictionIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchCosts);
        mockRepo.Setup(r => r.GetBonusPredictionCostsByRepredictionIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bonus query failed"));
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act - Request bonus costs which will fail
        var exitCode = await app.RunAsync(["cost", "--bonus"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to calculate costs");
    }

    [Test]
    public async Task Running_command_with_all_flag_handles_repository_errors()
    {
        // Arrange
        var mockRepo = new Mock<IPredictionRepository>();
        mockRepo.Setup(r => r.GetAvailableModelsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Connection timed out"));
        var (app, console, _, _, _) = CreateCostCommandApp(mockRepo);

        // Act
        var exitCode = await app.RunAsync(["cost", "--all"]);
        var output = console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to calculate costs");
        await Assert.That(output).Contains("Connection timed out");
    }
}
