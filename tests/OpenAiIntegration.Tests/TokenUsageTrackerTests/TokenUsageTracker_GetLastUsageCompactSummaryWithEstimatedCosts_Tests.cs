using TestUtilities;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetLastUsageCompactSummaryWithEstimatedCosts method
/// </summary>
public class TokenUsageTracker_GetLastUsageCompactSummaryWithEstimatedCosts_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_with_no_usage_returns_zeros()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("o3");

        // Assert
        await Assert.That(summary).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_includes_estimated_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", It.IsAny<ChatTokenUsage>()))
            .Returns(0.05m);
        costServiceMock.Setup(x => x.CalculateCost("o3", It.IsAny<ChatTokenUsage>()))
            .Returns(6.0m);

        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("o3");

        // Assert
        await Assert.That(summary).Contains("1,000,000 / 0 / 0 / 500,000 / $0.0500");
        await Assert.That(summary).Contains("(est o3: $6.0000)");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_returns_only_last_usage_estimate()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        
        // First usage - smaller
        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 100000, outputTokens: 50000);
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage1)).Returns(0.01m);
        costServiceMock.Setup(x => x.CalculateCost("gpt-4o", usage1)).Returns(0.75m);
        
        // Second usage - larger
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000000, outputTokens: 500000);
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage2)).Returns(0.05m);
        costServiceMock.Setup(x => x.CalculateCost("gpt-4o", usage2)).Returns(7.50m);

        // Act
        tracker.AddUsage("gpt-5-nano", usage1);
        tracker.AddUsage("gpt-5-nano", usage2);
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("gpt-4o");

        // Assert - Should show only usage2
        await Assert.That(summary).Contains("1,000,000 / 0 / 0 / 500,000 / $0.0500");
        await Assert.That(summary).Contains("(est gpt-4o: $7.5000)");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_with_cached_tokens_calculates_correctly()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 500000,
            cachedInputTokens: 600000);
            
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage)).Returns(0.03m);
        costServiceMock.Setup(x => x.CalculateCost("gpt-4o", usage)).Returns(6.75m);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("gpt-4o");

        // Assert
        await Assert.That(summary).Contains("400,000 / 600,000 / 0 / 500,000 / $0.0300");
        await Assert.That(summary).Contains("(est gpt-4o: $6.7500)");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_with_reasoning_tokens_includes_in_estimate()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1000000,
            outputTokens: 1500000,
            outputReasoningTokens: 1000000);
            
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage)).Returns(0.07m);
        costServiceMock.Setup(x => x.CalculateCost("o3", usage)).Returns(14.0m);

        // Act
        tracker.AddUsage("gpt-5-nano", usage);
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("o3");

        // Assert
        await Assert.That(summary).Contains("1,000,000 / 0 / 1,000,000 / 500,000 / $0.0700");
        await Assert.That(summary).Contains("(est o3: $14.0000)");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_with_unknown_model_shows_zero_estimate()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        costServiceMock.Setup(x => x.CalculateCost("gpt-4o", usage)).Returns(0.01m);
        costServiceMock.Setup(x => x.CalculateCost("unknown-model", usage)).Returns((decimal?)null);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var summary = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("unknown-model");

        // Assert - Null cost from service is treated as 0
        await Assert.That(summary).IsEqualTo("1,000 / 0 / 0 / 500 / $0.0100 (est unknown-model: $0.0000)");
    }

    [Test]
    public async Task GetLastUsageCompactSummaryWithEstimatedCosts_updates_with_each_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        
        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 100000, outputTokens: 50000);
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage1)).Returns(0.005m);
        costServiceMock.Setup(x => x.CalculateCost("o3", usage1)).Returns(0.60m);
        
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 500000, outputTokens: 250000);
        costServiceMock.Setup(x => x.CalculateCost("gpt-5-nano", usage2)).Returns(0.025m);
        costServiceMock.Setup(x => x.CalculateCost("o3", usage2)).Returns(3.00m);

        // Act & Assert - First usage
        tracker.AddUsage("gpt-5-nano", usage1);
        var summary1 = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("o3");
        await Assert.That(summary1).Contains("100,000 / 0 / 0 / 50,000 / $0.0050");
        await Assert.That(summary1).Contains("(est o3: $0.6000)");

        // Act & Assert - Second usage
        tracker.AddUsage("gpt-5-nano", usage2);
        var summary2 = tracker.GetLastUsageCompactSummaryWithEstimatedCosts("o3");
        await Assert.That(summary2).Contains("500,000 / 0 / 0 / 250,000 / $0.0250");
        await Assert.That(summary2).Contains("(est o3: $3.0000)");
    }
}

