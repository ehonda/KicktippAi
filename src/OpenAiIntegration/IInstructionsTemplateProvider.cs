namespace OpenAiIntegration;

/// <summary>
/// Provides instructions templates for predictions based on model configurations
/// </summary>
public interface IInstructionsTemplateProvider
{
    /// <summary>
    /// Loads the match prediction instructions template for the specified model
    /// </summary>
    /// <param name="model">The model name to load instructions for</param>
    /// <param name="includeJustification">Whether to load the template with justification requirements</param>
    /// <returns>A tuple containing the template content and the file path it was loaded from</returns>
    (string template, string path) LoadMatchTemplate(string model, bool includeJustification);

    /// <summary>
    /// Loads the bonus question prediction instructions template for the specified model
    /// </summary>
    /// <param name="model">The model name to load instructions for</param>
    /// <returns>A tuple containing the template content and the file path it was loaded from</returns>
    (string template, string path) LoadBonusTemplate(string model);
}
