namespace EHonda.KicktippAi.Core;

/// <summary>
/// Utility class for finding paths relative to the solution root
/// </summary>
public static class SolutionPathUtility
{
    private const string SolutionFileName = "KicktippAi.slnx";
    private const string OriginalRepositoryLocatorRelativePath = ".codex-local/original-repository-path";
    
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
    /// Finds the original repository root that owns sibling local resources, such as the secrets directory.
    /// When no locator is present in the current solution root, the current solution root is returned.
    /// </summary>
    /// <returns>The absolute path to the original repository root.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a present locator is invalid.</exception>
    public static string FindOriginalRepositoryRoot()
    {
        var solutionRoot = FindSolutionRoot();
        var locatorPath = Path.Combine(solutionRoot, OriginalRepositoryLocatorRelativePath);

        if (!File.Exists(locatorPath))
        {
            return solutionRoot;
        }

        var originalRepositoryPath = File.ReadAllText(locatorPath).Trim();
        if (string.IsNullOrWhiteSpace(originalRepositoryPath))
        {
            throw new InvalidOperationException(
                $"Original repository locator is blank: {locatorPath}. Specify an absolute path to the original repository root.");
        }

        if (!Path.IsPathFullyQualified(originalRepositoryPath))
        {
            throw new InvalidOperationException(
                $"Original repository locator must contain an absolute path: {locatorPath}.");
        }

        if (!Directory.Exists(originalRepositoryPath))
        {
            throw new InvalidOperationException(
                $"Original repository locator points to a directory that does not exist: {originalRepositoryPath}.");
        }

        var originalRepositoryRoot = Path.GetFullPath(originalRepositoryPath);
        if (!File.Exists(Path.Combine(originalRepositoryRoot, SolutionFileName)))
        {
            throw new InvalidOperationException(
                $"Original repository locator must point to a directory containing {SolutionFileName}: {originalRepositoryRoot}.");
        }

        return originalRepositoryRoot;
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
