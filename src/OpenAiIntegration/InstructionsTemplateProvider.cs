using Microsoft.Extensions.FileProviders;

namespace OpenAiIntegration;

/// <summary>
/// Default implementation that loads instructions templates from the file system
/// </summary>
public class InstructionsTemplateProvider : IInstructionsTemplateProvider
{
    private readonly IFileProvider _fileProvider;

    public InstructionsTemplateProvider(IFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
    }

    public (string template, string path) LoadMatchTemplate(string model, bool includeJustification)
    {
        var promptModel = GetPromptModelForModel(model);
        var fileName = includeJustification ? "match.justification.md" : "match.md";
        var filePath = $"{promptModel}/{fileName}";
        
        var fileInfo = _fileProvider.GetFileInfo(filePath);
        if (fileInfo.Exists)
        {
            return ReadFileContent(fileInfo);
        }

        if (includeJustification)
        {
            var fallbackPath = $"{promptModel}/match.md";
            var fallbackFileInfo = _fileProvider.GetFileInfo(fallbackPath);
            if (fallbackFileInfo.Exists)
            {
                return ReadFileContent(fallbackFileInfo);
            }
        }

        throw new FileNotFoundException($"Match instructions not found at: {filePath}");
    }

    public (string template, string path) LoadBonusTemplate(string model)
    {
        var promptModel = GetPromptModelForModel(model);
        var filePath = $"{promptModel}/bonus.md";
        
        var fileInfo = _fileProvider.GetFileInfo(filePath);
        if (fileInfo.Exists)
        {
            return ReadFileContent(fileInfo);
        }
        
        throw new FileNotFoundException($"Bonus instructions not found at: {filePath}");
    }

    /// <summary>
    /// Reads the content from a file info and returns it with the physical path
    /// </summary>
    /// <param name="fileInfo">The file info to read from</param>
    /// <returns>A tuple containing the file content and physical path</returns>
    /// <exception cref="InvalidOperationException">Thrown when the physical path is null</exception>
    private static (string content, string path) ReadFileContent(IFileInfo fileInfo)
    {
        if (fileInfo.PhysicalPath == null)
        {
            throw new InvalidOperationException(
                $"File '{fileInfo.Name}' does not have a physical path. " +
                "This may indicate the file is from a non-physical file provider.");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream);
        return (reader.ReadToEnd(), fileInfo.PhysicalPath);
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
