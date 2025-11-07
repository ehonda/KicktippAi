using TestUtilities;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetCompactSummary method
/// </summary>
public class TokenUsageTracker_GetCompactSummary_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetCompactSummary_with_no_usage_returns_zeros()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var summary = tracker.GetCompactSummary();

        // Assert
        await Assert.That(summary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task GetCompactSummary_with_single_usage_returns_correct_format()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.123456m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500,
            cachedInputTokens: 300);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetCompactSummary();

        // Assert
        await Assert.That(summary).IsEqualTo("700 / 300 / 0 / 500 / $5.1235");
    }

    [Test]
    public async Task GetCompactSummary_formats_numbers_with_thousand_separators()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 100.5m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1234567,
            outputTokens: 987654);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetCompactSummary();

        // Assert
        await Assert.That(summary).Contains("1,234,567");
        await Assert.That(summary).Contains("987,654");
    }

    [Test]
    public async Task GetCompactSummary_accumulates_multiple_usages()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.50m)
            .Returns(2.75m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000,
            outputTokens: 500,
            cachedInputTokens: 200);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 3000,
            outputTokens: 1500,
            cachedInputTokens: 1000,
            outputReasoningTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("o3", usage2);
        var summary = tracker.GetCompactSummary();

        // Assert
        // Uncached: (1000-200) + (3000-1000) = 800 + 2000 = 2800
        // Cached: 200 + 1000 = 1200
        // Reasoning: 0 + 500 = 500
        // Regular output: 500 + (1500-500) = 500 + 1000 = 1500
        // Cost: 1.50 + 2.75 = 4.25
        await Assert.That(summary).IsEqualTo("2,800 / 1,200 / 500 / 1,500 / $4.2500");
    }

    [Test]
    public async Task GetCompactSummary_with_reasoning_tokens_shows_in_correct_position()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 10.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 5000,
            outputTokens: 3000,
            outputReasoningTokens: 2000);

        // Act
        tracker.AddUsage("o3", usage);
        var summary = tracker.GetCompactSummary();

        // Assert - Format: uncached / cached / reasoning / output / cost
        await Assert.That(summary).IsEqualTo("5,000 / 0 / 2,000 / 1,000 / $10.0000");
    }

    [Test]
    public async Task GetCompactSummary_rounds_cost_to_four_decimal_places()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 1.23456789m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 100, outputTokens: 50);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetCompactSummary();

        // Assert
        await Assert.That(summary).Contains("$1.2346"); // Rounded
    }

    [Test]
    public async Task GetCompactSummary_uses_invariant_culture_for_formatting()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 1234.56m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000000, outputTokens: 500000);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetCompactSummary();

        // Assert
        // Should use comma for thousands, period for decimal (invariant culture)
        await Assert.That(summary).Contains("1,000,000");
        await Assert.That(summary).Contains("$1234.5600");
    }
}

