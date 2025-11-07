using TestUtilities;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetCompactSummaryWithEstimatedCosts method
/// </summary>
public class TokenUsageTracker_GetCompactSummaryWithEstimatedCosts_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_with_no_usage_returns_zeros()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("o3");

        // Assert
        await Assert.That(summary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000 (est o3: $0.0000)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_includes_estimated_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("o3");

        // Assert
        // Base summary + estimated cost for o3
        // o3 pricing: $2.00/1M input, $8.00/1M output
        // Expected: (1M * 2.00) + (0.5M * 8.00) = 2.00 + 4.00 = 6.00
        await Assert.That(summary).Contains("1,000,000 / 0 / 0 / 500,000 / $5.0000");
        await Assert.That(summary).Contains("(est o3: $6.0000)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_with_cached_tokens_calculates_correctly()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 3.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000,
            cachedInputTokens: 600000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("gpt-4o");

        // Assert
        // gpt-4o pricing: $2.50/1M input, $10.00/1M output, $1.25/1M cached
        // Uncached: 400,000, Cached: 600,000
        // Expected: (0.4M * 2.50) + (0.6M * 1.25) + (0.5M * 10.00) = 1.00 + 0.75 + 5.00 = 6.75
        await Assert.That(summary).Contains("(est gpt-4o: $6.7500)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_with_model_without_cached_pricing_ignores_cached_tokens()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 1.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000,
            cachedInputTokens: 600000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("o1-pro");

        // Assert
        // o1-pro pricing: $150/1M input, $600/1M output, no cached pricing
        // Uncached: 400,000 (1M - 600K), Cached ignored
        // Expected: (0.4M * 150) + (0.5M * 600) = 60 + 300 = 360
        await Assert.That(summary).Contains("(est o1-pro: $360.0000)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_with_reasoning_tokens_includes_in_output_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 2.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 1500000,
            outputReasoningTokens: 1000000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("o3");

        // Assert
        // o3 pricing: $2.00/1M input, $8.00/1M output
        // Total output: 1.5M (reasoning + regular both count as output)
        // Expected: (1M * 2.00) + (1.5M * 8.00) = 2.00 + 12.00 = 14.00
        await Assert.That(summary).Contains("(est o3: $14.0000)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_with_unknown_model_returns_zero_estimate()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.0m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("unknown-model");

        // Assert
        await Assert.That(summary).Contains("(est unknown-model: $0.0000)");
    }

    [Test]
    public async Task GetCompactSummaryWithEstimatedCosts_accumulates_multiple_usages()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.0m)
            .Returns(2.0m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 500000,
            outputTokens: 250000);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage1);
        tracker.AddUsage("gpt-5-nano", usage2);
        var summary = tracker.GetCompactSummaryWithEstimatedCosts("gpt-4o");

        // Assert
        // Total: 1.5M input, 0.75M output
        // gpt-4o: (1.5M * 2.50) + (0.75M * 10.00) = 3.75 + 7.50 = 11.25
        await Assert.That(summary).Contains("1,500,000 / 0 / 0 / 750,000 / $3.0000");
        await Assert.That(summary).Contains("(est gpt-4o: $11.2500)");
    }
}

