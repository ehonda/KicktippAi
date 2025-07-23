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
    /// Get the last usage added (for displaying individual match usage)
    /// Format: "uncached / cached / output-reasoning / output-rest / $cost"
    /// </summary>
    /// <returns>Compact usage summary string for the last operation</returns>
    string GetLastUsageCompactSummary();

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
