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

    /// <summary>
    /// Calculates a detailed cost breakdown for a specific model and usage.
    /// </summary>
    /// <param name="model">The model to calculate costs for</param>
    /// <param name="usage">The token usage information</param>
    /// <returns>A <see cref="CostBreakdown"/> with per-component costs, or null if pricing is not available for the model</returns>
    CostBreakdown? CalculateCostBreakdown(string model, ChatTokenUsage usage);
}

/// <summary>
/// Detailed cost breakdown for an OpenAI API call, with per-component costs in USD.
/// </summary>
/// <param name="Input">Cost for uncached input tokens</param>
/// <param name="CachedInput">Cost for cached input tokens</param>
/// <param name="Output">Cost for output tokens</param>
/// <param name="Total">Total cost (input + cached input + output)</param>
public record CostBreakdown(decimal Input, decimal CachedInput, decimal Output, decimal Total);
