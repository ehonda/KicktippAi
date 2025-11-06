using Microsoft.Extensions.FileProviders;

namespace OpenAiIntegration;

/// <summary>
/// Factory for creating an IFileProvider rooted at the prompts directory
/// </summary>
public static class PromptsFileProvider
{
    /// <summary>
    /// Creates a PhysicalFileProvider rooted at the prompts directory by finding the solution root
    /// </summary>
    /// <returns>An IFileProvider rooted at the prompts directory</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found</exception>
    public static IFileProvider Create()
    {
        var promptsDirectory = FindPromptsDirectory();
        return new PhysicalFileProvider(promptsDirectory);
    }

    private static string FindPromptsDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                return Path.Combine(directory.FullName, "prompts");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find solution root (KicktippAi.slnx) to locate prompts directory");
    }
}
