using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for calculating and logging OpenAI API costs
/// </summary>
public interface ICostCalculationService
{
    /// <summary>
    /// Calculates and logs the cost breakdown for an OpenAI chat completion
    /// </summary>
    /// <param name="model">The model used for the completion</param>
    /// <param name="usage">The token usage information from the response</param>
    void LogCostBreakdown(string model, ChatTokenUsage usage);
}
