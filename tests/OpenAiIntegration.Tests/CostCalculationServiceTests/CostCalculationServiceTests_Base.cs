using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using OpenAI.Chat;
using TestUtilities;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
    protected FakeLogger<CostCalculationService> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new FakeLogger<CostCalculationService>();
        Service = new CostCalculationService(Logger);
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
