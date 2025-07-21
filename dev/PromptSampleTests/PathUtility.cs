namespace PromptSampleTests;

public static class PathUtility
{
    /// <summary>
    /// Finds the solution root directory by looking for KicktippAi.slnx in parent directories.
    /// </summary>
    /// <returns>The path to the solution root directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found.</exception>
    public static string FindSolutionRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find solution root (KicktippAi.slnx) starting from: {currentDirectory}");
    }

    /// <summary>
    /// Gets the path to the instructions template relative to the solution root.
    /// </summary>
    /// <returns>The full path to the instructions template.</returns>
    public static string GetInstructionsTemplatePath()
    {
        var solutionRoot = FindSolutionRoot();
        return Path.Combine(solutionRoot, "prompts", "reasoning-models", "predict-one-match", "v0-handcrafted", "instructions_template.md");
    }

    /// <summary>
    /// Gets the path to the .env file for a specific project relative to the solution root.
    /// </summary>
    /// <param name="projectName">The name of the project (e.g., "PromptSampleTests").</param>
    /// <returns>The full path to the .env file.</returns>
    public static string GetEnvFilePath(string projectName)
    {
        var solutionRoot = FindSolutionRoot();
        return Path.Combine(solutionRoot, "..", "KicktippAi.Secrets", "dev", projectName, ".env");
    }
}
