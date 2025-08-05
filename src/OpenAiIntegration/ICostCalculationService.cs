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

    /// <summary>
    /// Calculates the cost for a specific model and usage
    /// </summary>
    /// <param name="model">The model to calculate costs for</param>
    /// <param name="usage">The token usage information</param>
    /// <returns>The calculated cost, or null if pricing is not available for the model</returns>
    decimal? CalculateCost(string model, ChatTokenUsage usage);
}
