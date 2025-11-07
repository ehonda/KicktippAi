using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker AddUsage method
/// </summary>
public class TokenUsageTracker_AddUsage_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task AddUsage_with_basic_tokens_tracks_correctly()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock, costToReturn: 5.00m);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).Contains("1,000"); // uncached input
        await Assert.That(summary).Contains("500"); // output
        await Assert.That(summary).Contains("$5.0000"); // cost
    }

    [Test]
    public async Task AddUsage_with_cached_tokens_separates_cached_and_uncached()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 3.50m);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500,
            cachedInputTokens: 600);

        // Act
        tracker.AddUsage("gpt-4o", usage);

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).Contains("400"); // uncached input (1000 - 600)
        await Assert.That(summary).Contains("600"); // cached input
        await Assert.That(summary).Contains("500"); // output
    }

    [Test]
    public async Task AddUsage_with_reasoning_tokens_separates_reasoning_and_regular_output()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 10.00m);
        var usage = CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 1500,
            reasoningTokens: 1000);

        // Act
        tracker.AddUsage("o3", usage);

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).Contains("1,000"); // reasoning tokens
        await Assert.That(summary).Contains("500"); // regular output (1500 - 1000)
    }

    [Test]
    public async Task AddUsage_accumulates_multiple_calls()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.00m)
            .Returns(2.00m)
            .Returns(3.00m);

        var usage1 = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = CreateChatTokenUsage(inputTokens: 500, outputTokens: 250);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).Contains("3,500"); // total uncached input (1000 + 2000 + 500)
        await Assert.That(summary).Contains("1,750"); // total output (500 + 1000 + 250)
        await Assert.That(summary).Contains("$6.0000"); // total cost (1 + 2 + 3)
    }

    [Test]
    public async Task AddUsage_updates_last_usage_tracking()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.00m)
            .Returns(2.50m);

        var usage1 = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);

        // Assert - last usage should reflect usage2
        var lastSummary = tracker.GetLastUsageCompactSummary();
        await Assert.That(lastSummary).Contains("3,000"); // last uncached input
        await Assert.That(lastSummary).Contains("1,500"); // last output
        await Assert.That(lastSummary).Contains("$2.5000"); // last cost
    }

    [Test]
    public void AddUsage_calls_cost_calculation_service()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock, costToReturn: 5.00m);
        var usage = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);

        // Assert
        costServiceMock.Verify(
            x => x.CalculateCost("gpt-4o", usage),
            Times.Once);
    }

    [Test]
    public async Task AddUsage_handles_null_cost_from_service()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock, costToReturn: null);
        var usage = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("unknown-model", usage);

        // Assert - should not throw, cost should be 0
        var totalCost = tracker.GetTotalCost();
        await Assert.That(totalCost).IsEqualTo(0m);
    }

    [Test]
    public void AddUsage_logs_debug_message()
    {
        // Arrange
        var tracker = CreateTrackerWithFakeLogger(out var logger, out _, costToReturn: 5.00m);
        var usage = CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);

        // Assert
        AssertLogContains(logger, LogLevel.Debug, "Added usage for model gpt-4o");
    }

    [Test]
    public async Task AddUsage_with_complex_token_mix_tracks_all_components()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 15.75m);
        var usage = CreateChatTokenUsage(
            inputTokens: 10000,
            outputTokens: 8000,
            cachedInputTokens: 6000,
            reasoningTokens: 5000);

        // Act
        tracker.AddUsage("o3", usage);

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).Contains("4,000"); // uncached input (10000 - 6000)
        await Assert.That(summary).Contains("6,000"); // cached input
        await Assert.That(summary).Contains("5,000"); // reasoning tokens
        await Assert.That(summary).Contains("3,000"); // regular output (8000 - 5000)
        await Assert.That(summary).Contains("$15.7500");
    }
}
