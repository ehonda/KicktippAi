using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for tracking token usage and costs across multiple API calls
/// </summary>
public interface ITokenUsageTracker
{
    /// <summary>
    /// Add token usage from a chat completion to the tracker
    /// </summary>
    /// <param name="model">The model used for the completion</param>
    /// <param name="usage">Token usage details from the completion</param>
    void AddUsage(string model, ChatTokenUsage usage);

    /// <summary>
    /// Get a compact summary of total usage and cost
    /// Format: "uncached / cached / output-reasoning / output-rest / $cost"
    /// </summary>
    /// <returns>Compact usage summary string</returns>
    string GetCompactSummary();

    /// <summary>
    /// Get a compact summary of total usage and cost with estimated costs for an alternative model
    /// Format: "uncached / cached / output-reasoning / output-rest / $cost (est: $estimatedCost)"
    /// </summary>
    /// <param name="estimatedCostsModel">The model to estimate costs for</param>
    /// <returns>Compact usage summary string with estimated costs</returns>
    string GetCompactSummaryWithEstimatedCosts(string estimatedCostsModel);

    /// <summary>
    /// Get the last usage added (for displaying individual match usage)
    /// Format: "uncached / cached / output-reasoning / output-rest / $cost"
    /// </summary>
    /// <returns>Compact usage summary string for the last operation</returns>
    string GetLastUsageCompactSummary();

    /// <summary>
    /// Get the last usage added with estimated costs for an alternative model
    /// Format: "uncached / cached / output-reasoning / output-rest / $cost (est: $estimatedCost)"
    /// </summary>
    /// <param name="estimatedCostsModel">The model to estimate costs for</param>
    /// <returns>Compact usage summary string for the last operation with estimated costs</returns>
    string GetLastUsageCompactSummaryWithEstimatedCosts(string estimatedCostsModel);

    /// <summary>
    /// Get the raw token usage data for the last operation as JSON string
    /// </summary>
    /// <returns>JSON string containing the raw token usage data, or null if no usage recorded</returns>
    string? GetLastUsageJson();

    /// <summary>
    /// Get the cost for the last operation only (not cumulative)
    /// </summary>
    /// <returns>Cost in USD for the last operation</returns>
    decimal GetLastCost();

    /// <summary>
    /// Get total cost across all tracked usage
    /// </summary>
    /// <returns>Total cost in USD</returns>
    decimal GetTotalCost();

    /// <summary>
    /// Reset all tracked usage
    /// </summary>
    void Reset();
}
