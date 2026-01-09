using OpenAiIntegration;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Factory for creating OpenAI prediction services that require runtime configuration.
/// </summary>
/// <remarks>
/// Reads the API key from OPENAI_API_KEY environment variable.
/// Commands use this factory to obtain prediction services configured with
/// model settings specified at call time.
/// </remarks>
public interface IOpenAiServiceFactory
{
    /// <summary>
    /// Creates a prediction service configured with the specified model.
    /// </summary>
    /// <param name="model">The model to use for predictions.</param>
    /// <returns>A configured prediction service instance.</returns>
    /// <remarks>
    /// Uses the OPENAI_API_KEY environment variable for authentication.
    /// </remarks>
    IPredictionService CreatePredictionService(string model);

    /// <summary>
    /// Creates or retrieves the shared token usage tracker.
    /// </summary>
    /// <returns>The token usage tracker instance.</returns>
    ITokenUsageTracker GetTokenUsageTracker();
}
