using TestUtilities;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetLastUsageCompactSummary method
/// </summary>
public class TokenUsageTracker_GetLastUsageCompactSummary_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetLastUsageCompactSummary_with_no_usage_returns_zeros()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var summary = tracker.GetLastUsageCompactSummary();

        // Assert
        await Assert.That(summary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task GetLastUsageCompactSummary_returns_only_last_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.0m)
            .Returns(2.0m)
            .Returns(3.5m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);
        var summary = tracker.GetLastUsageCompactSummary();

        // Assert - Should show only usage3
        await Assert.That(summary).IsEqualTo("3,000 / 0 / 0 / 1,500 / $3.5000");
    }

    [Test]
    public async Task GetLastUsageCompactSummary_updates_with_each_new_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.0m)
            .Returns(2.5m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 5000, outputTokens: 2500);

        // Act & Assert
        tracker.AddUsage("gpt-4o", usage1);
        var summary1 = tracker.GetLastUsageCompactSummary();
        await Assert.That(summary1).IsEqualTo("1,000 / 0 / 0 / 500 / $1.0000");

        tracker.AddUsage("gpt-4o", usage2);
        var summary2 = tracker.GetLastUsageCompactSummary();
        await Assert.That(summary2).IsEqualTo("5,000 / 0 / 0 / 2,500 / $2.5000");
    }

    [Test]
    public async Task GetLastUsageCompactSummary_with_cached_tokens_shows_breakdown()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 4.25m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 10000,
            outputTokens: 5000,
            cachedInputTokens: 7000);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetLastUsageCompactSummary();

        // Assert
        await Assert.That(summary).IsEqualTo("3,000 / 7,000 / 0 / 5,000 / $4.2500");
    }

    [Test]
    public async Task GetLastUsageCompactSummary_with_reasoning_tokens_shows_breakdown()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 8.75m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 5000,
            outputTokens: 7000,
            outputReasoningTokens: 4000);

        // Act
        tracker.AddUsage("o3", usage);
        var summary = tracker.GetLastUsageCompactSummary();

        // Assert
        await Assert.That(summary).IsEqualTo("5,000 / 0 / 4,000 / 3,000 / $8.7500");
    }

    [Test]
    public async Task GetLastUsageCompactSummary_with_all_token_types_shows_complete_breakdown()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 12.345m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 20000,
            outputTokens: 15000,
            cachedInputTokens: 12000,
            outputReasoningTokens: 9000);

        // Act
        tracker.AddUsage("o3", usage);
        var summary = tracker.GetLastUsageCompactSummary();

        // Assert
        // Uncached: 20000 - 12000 = 8000
        // Cached: 12000
        // Reasoning: 9000
        // Regular output: 15000 - 9000 = 6000
        await Assert.That(summary).IsEqualTo("8,000 / 12,000 / 9,000 / 6,000 / $12.3450");
    }
}

