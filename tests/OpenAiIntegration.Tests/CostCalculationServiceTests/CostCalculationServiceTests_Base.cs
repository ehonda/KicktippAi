using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
    protected Mock<ILogger<CostCalculationService>> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new Mock<ILogger<CostCalculationService>>();
        Service = new CostCalculationService(Logger.Object);
    }

    /// <summary>
    /// Verifies that a log message containing the specified text was logged at the specified level
    /// </summary>
    protected void VerifyLogContains(LogLevel logLevel, string messageContent, Func<Times> times)
    {
        Logger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(messageContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    /// <summary>
    /// Helper method to create ChatTokenUsage instances for testing
    /// </summary>
    protected static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens,
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
}
