using OpenAI.Chat;

namespace TestUtilities;

/// <summary>
/// Helper methods for creating OpenAI test objects
/// </summary>
public static class OpenAITestHelpers
{
    /// <summary>
    /// Creates a ChatTokenUsage instance for testing purposes
    /// </summary>
    /// <param name="inputTokens">The number of input tokens</param>
    /// <param name="outputTokens">The number of output tokens</param>
    /// <param name="cachedInputTokens">The number of cached input tokens (optional)</param>
    /// <param name="outputReasoningTokens">The number of output reasoning tokens (optional)</param>
    /// <returns>A ChatTokenUsage instance with the specified token counts</returns>
    public static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens,
        int outputTokens,
        int cachedInputTokens = 0,
        int outputReasoningTokens = 0)
    {
        ChatInputTokenUsageDetails? inputDetails = cachedInputTokens > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedInputTokens)
            : null;

        ChatOutputTokenUsageDetails? outputDetails = outputReasoningTokens > 0
            ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: outputReasoningTokens)
            : null;

        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: inputTokens,
            outputTokenCount: outputTokens,
            inputTokenDetails: inputDetails,
            outputTokenDetails: outputDetails);
    }
}
