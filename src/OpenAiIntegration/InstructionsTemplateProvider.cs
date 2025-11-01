namespace OpenAiIntegration;

/// <summary>
/// Default implementation that loads instructions templates from the file system
/// </summary>
public class InstructionsTemplateProvider : IInstructionsTemplateProvider
{
    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        var promptModel = GetPromptModelForModel(model);
        
        // Try to find the model-specific instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var promptsDirectory = Path.Combine(directory.FullName, "prompts", promptModel);
                var fileName = includeJustification ? "match.justification.md" : "match.md";
                var instructionsPath = Path.Combine(promptsDirectory, fileName);
                
                if (File.Exists(instructionsPath))
                {
                    return (File.ReadAllText(instructionsPath), instructionsPath);
                }

                if (includeJustification)
                {
                    var fallbackPath = Path.Combine(promptsDirectory, "match.md");
                    if (File.Exists(fallbackPath))
                    {
                        return (File.ReadAllText(fallbackPath), fallbackPath);
                    }
                }

                throw new FileNotFoundException($"Match instructions not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate match instructions");
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        var promptModel = GetPromptModelForModel(model);
        
        // Try to find the model-specific bonus instructions template relative to the current directory
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                var instructionsPath = Path.Combine(directory.FullName, "prompts", promptModel, "bonus.md");
                
                if (File.Exists(instructionsPath))
                {
                    return (File.ReadAllText(instructionsPath), instructionsPath);
                }
                
                throw new FileNotFoundException($"Bonus instructions not found at: {instructionsPath}");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root (KicktippAi.slnx) to locate bonus instructions");
    }

    /// <summary>
    /// Maps a model name to the appropriate prompt directory, handling cross-model mappings
    /// </summary>
    /// <param name="model">The model name to map</param>
    /// <returns>The prompt directory name to use</returns>
    private static string GetPromptModelForModel(string model)
    {
        return model switch
        {
            // Direct mappings
            "o3" => "o3",
            "gpt-5" => "gpt-5",
            
            // Cross-model mappings
            "o4-mini" => "o3",
            "gpt-5-mini" => "gpt-5",
            "gpt-5-nano" => "gpt-5",
            
            // Default to the model name itself for any new models
            _ => model
        };
    }
}
