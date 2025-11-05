namespace OpenAiIntegration;

/// <summary>
/// Provides the root directory path where prompt templates are stored
/// </summary>
public interface IPromptsDirectoryProvider
{
    /// <summary>
    /// Gets the absolute path to the prompts directory
    /// </summary>
    /// <returns>The absolute path to the directory containing prompt templates</returns>
    string GetPromptsDirectory();
}
