using OpenAI.Chat;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
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
