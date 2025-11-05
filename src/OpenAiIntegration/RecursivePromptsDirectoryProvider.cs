namespace OpenAiIntegration;

/// <summary>
/// Provides the prompts directory by recursively searching upward from the current directory
/// until it finds the solution root (KicktippAi.slnx)
/// </summary>
public class RecursivePromptsDirectoryProvider : IPromptsDirectoryProvider
{
    private readonly Lazy<string> _promptsDirectory;

    public RecursivePromptsDirectoryProvider()
    {
        _promptsDirectory = new Lazy<string>(FindPromptsDirectory);
    }

    public string GetPromptsDirectory()
    {
        return _promptsDirectory.Value;
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
