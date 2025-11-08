using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TestUtilities;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker Reset method
/// </summary>
public class TokenUsageTracker_Reset_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task Reset_clears_all_tracked_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(5.00m)
            .Returns(3.00m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 10000, outputTokens: 5000);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 8000, outputTokens: 4000);

        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);

        // Act
        tracker.Reset();

        // Assert
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task Reset_clears_total_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 10.50m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 5000, outputTokens: 2500);
        tracker.AddUsage("gpt-4o", usage);

        // Act
        tracker.Reset();

        // Assert
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(0m);
    }

    [Test]
    public async Task Reset_clears_last_usage_tracking()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.00m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);
        tracker.AddUsage("gpt-4o", usage);

        // Act
        tracker.Reset();

        // Assert
        var lastSummary = tracker.GetLastUsageCompactSummary();
        await Assert.That(lastSummary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task Reset_clears_last_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 7.25m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 4000, outputTokens: 2000);
        tracker.AddUsage("gpt-4o", usage);

        // Act
        tracker.Reset();

        // Assert
        await Assert.That(tracker.GetLastCost()).IsEqualTo(0m);
    }

    [Test]
    public async Task Reset_clears_last_usage_json()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        tracker.AddUsage("gpt-4o", usage);

        // Act
        tracker.Reset();

        // Assert
        await Assert.That(tracker.GetLastUsageJson()).IsNull();
    }

    [Test]
    public async Task Reset_allows_new_usage_to_be_tracked()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(5.00m)
            .Returns(3.00m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 10000, outputTokens: 5000);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);

        tracker.AddUsage("gpt-4o", usage1);
        tracker.Reset();

        // Act
        tracker.AddUsage("gpt-4o", usage2);

        // Assert - Should show only usage2
        var summary = tracker.GetCompactSummary();
        await Assert.That(summary).IsEqualTo("2,000 / 0 / 0 / 1,000 / $3.0000");
    }

    [Test]
    public async Task Reset_logs_debug_message()
    {
        // Arrange
        var tracker = CreateTrackerWithFakeLogger(out var logger, out _);

        // Act
        tracker.Reset();

        // Assert
        logger.AssertLogContains(LogLevel.Debug, "Token usage tracker reset");
    }

    [Test]
    public async Task Reset_can_be_called_multiple_times()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.00m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        tracker.Reset();
        tracker.Reset();
        tracker.Reset();

        // Assert - Should still be zero
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetCompactSummary()).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task Reset_on_tracker_with_no_usage_does_nothing()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        tracker.Reset();

        // Assert
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetCompactSummary()).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }
}
