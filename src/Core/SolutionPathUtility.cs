namespace EHonda.KicktippAi.Core;

/// <summary>
/// Utility class for finding paths relative to the solution root
/// </summary>
public static class SolutionPathUtility
{
    private const string SolutionFileName = "KicktippAi.slnx";
    
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
            var solutionFile = Path.Combine(directory.FullName, SolutionFileName);
            if (File.Exists(solutionFile))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find solution root ({SolutionFileName}) starting from: {currentDirectory}");
    }
    
    /// <summary>
    /// Finds a directory under the solution root.
    /// </summary>
    /// <param name="relativePath">The relative path from the solution root (e.g., "prompts", "community-rules").</param>
    /// <returns>The absolute path to the directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found.</exception>
    public static string FindDirectoryUnderSolutionRoot(string relativePath)
    {
        var solutionRoot = FindSolutionRoot();
        return Path.Combine(solutionRoot, relativePath);
    }
}
