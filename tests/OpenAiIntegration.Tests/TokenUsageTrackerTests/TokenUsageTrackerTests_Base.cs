using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Base class for TokenUsageTracker tests providing shared helper functionality
/// </summary>
public abstract class TokenUsageTrackerTests_Base
{
    /// <summary>
    /// Helper method to create ChatTokenUsage instances for testing
    /// </summary>
    protected static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens = 0,
        int reasoningTokens = 0)
    {
        ChatInputTokenUsageDetails? inputDetails = cachedInputTokens > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedInputTokens)
            : null;

        ChatOutputTokenUsageDetails? outputDetails = reasoningTokens > 0
            ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: reasoningTokens)
            : null;
        
        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: inputTokens,
            outputTokenCount: outputTokens,
            inputTokenDetails: inputDetails,
            outputTokenDetails: outputDetails);
    }

    /// <summary>
    /// Helper method to create a mock cost calculation service with default behavior
    /// </summary>
    protected static Mock<ICostCalculationService> CreateMockCostCalculationService(decimal? costToReturn = 1.23m)
    {
        var mock = new Mock<ICostCalculationService>();
        mock.Setup(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(costToReturn);
        return mock;
    }

    /// <summary>
    /// Helper method to create a token usage tracker with mocked dependencies
    /// </summary>
    protected static TokenUsageTracker CreateTracker(
        out Mock<ILogger<TokenUsageTracker>> loggerMock,
        out Mock<ICostCalculationService> costServiceMock,
        decimal? costToReturn = 1.23m)
    {
        loggerMock = new Mock<ILogger<TokenUsageTracker>>();
        costServiceMock = CreateMockCostCalculationService(costToReturn);
        return new TokenUsageTracker(loggerMock.Object, costServiceMock.Object);
    }

    /// <summary>
    /// Helper method to create a token usage tracker with FakeLogger for testing log output
    /// </summary>
    protected static TokenUsageTracker CreateTrackerWithFakeLogger(
        out FakeLogger<TokenUsageTracker> logger,
        out Mock<ICostCalculationService> costServiceMock,
        decimal? costToReturn = 1.23m)
    {
        logger = new FakeLogger<TokenUsageTracker>();
        costServiceMock = CreateMockCostCalculationService(costToReturn);
        return new TokenUsageTracker(logger, costServiceMock.Object);
    }

    /// <summary>
    /// Asserts that a log message containing the specified text was logged at the specified level
    /// </summary>
    protected static void AssertLogContains(FakeLogger<TokenUsageTracker> logger, LogLevel logLevel, string messageContent)
    {
        var logs = logger.Collector.GetSnapshot();
        var matchingLog = logs.FirstOrDefault(log => log.Level == logLevel && log.Message.Contains(messageContent));
        
        if (matchingLog == null)
        {
            throw new Exception($"Expected log message containing '{messageContent}' at level {logLevel}, but none was found.");
        }
    }
}
