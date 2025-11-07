using TestUtilities;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker GetTotalCost and GetLastCost methods
/// </summary>
public class TokenUsageTracker_GetCost_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task GetTotalCost_with_no_usage_returns_zero()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var cost = tracker.GetTotalCost();

        // Assert
        await Assert.That(cost).IsEqualTo(0m);
    }

    [Test]
    public async Task GetTotalCost_with_single_usage_returns_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _, costToReturn: 5.25m);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("gpt-4o", usage);
        var cost = tracker.GetTotalCost();

        // Assert
        await Assert.That(cost).IsEqualTo(5.25m);
    }

    [Test]
    public async Task GetTotalCost_accumulates_multiple_usages()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.50m)
            .Returns(2.75m)
            .Returns(3.25m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1500, outputTokens: 750);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);
        var cost = tracker.GetTotalCost();

        // Assert
        await Assert.That(cost).IsEqualTo(7.50m); // 1.50 + 2.75 + 3.25
    }

    [Test]
    public async Task GetLastCost_with_no_usage_returns_zero()
    {
        // Arrange
        var tracker = CreateTracker(out _, out _);

        // Act
        var cost = tracker.GetLastCost();

        // Assert
        await Assert.That(cost).IsEqualTo(0m);
    }

    [Test]
    public async Task GetLastCost_returns_only_last_usage_cost()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.00m)
            .Returns(2.00m)
            .Returns(3.50m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);
        var cost = tracker.GetLastCost();

        // Assert - Should return only the cost of usage3
        await Assert.That(cost).IsEqualTo(3.50m);
    }

    [Test]
    public async Task GetLastCost_updates_with_each_new_usage()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(1.25m)
            .Returns(4.75m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 5000, outputTokens: 2500);

        // Act & Assert
        tracker.AddUsage("gpt-4o", usage1);
        await Assert.That(tracker.GetLastCost()).IsEqualTo(1.25m);

        tracker.AddUsage("gpt-4o", usage2);
        await Assert.That(tracker.GetLastCost()).IsEqualTo(4.75m);
    }

    [Test]
    public async Task GetTotalCost_and_GetLastCost_are_independent()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(2.00m)
            .Returns(3.00m)
            .Returns(5.00m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("gpt-4o", usage2);
        tracker.AddUsage("gpt-4o", usage3);

        // Assert
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(10.00m); // Sum of all
        await Assert.That(tracker.GetLastCost()).IsEqualTo(5.00m); // Only last
    }

    [Test]
    public async Task GetTotalCost_handles_null_cost_from_service()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock);
        costServiceMock.SetupSequence(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(2.00m)
            .Returns((decimal?)null)
            .Returns(3.00m);

        var usage1 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);
        var usage2 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 2000, outputTokens: 1000);
        var usage3 = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 3000, outputTokens: 1500);

        // Act
        tracker.AddUsage("gpt-4o", usage1);
        tracker.AddUsage("unknown-model", usage2);
        tracker.AddUsage("gpt-4o", usage3);
        var cost = tracker.GetTotalCost();

        // Assert - Null cost treated as 0
        await Assert.That(cost).IsEqualTo(5.00m); // 2.00 + 0 + 3.00
    }

    [Test]
    public async Task GetLastCost_handles_null_cost_from_service()
    {
        // Arrange
        var tracker = CreateTracker(out _, out var costServiceMock, costToReturn: null);
        var usage = OpenAITestHelpers.CreateChatTokenUsage(inputTokens: 1000, outputTokens: 500);

        // Act
        tracker.AddUsage("unknown-model", usage);
        var cost = tracker.GetLastCost();

        // Assert
        await Assert.That(cost).IsEqualTo(0m);
    }
}

