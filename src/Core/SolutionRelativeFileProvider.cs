using Microsoft.Extensions.FileProviders;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Factory for creating file providers rooted at directories under the solution root
/// </summary>
public static class SolutionRelativeFileProvider
{
    /// <summary>
    /// Creates a PhysicalFileProvider rooted at a directory under the solution root
    /// </summary>
    /// <param name="directoryName">The name of the directory under the solution root (e.g., "prompts", "community-rules")</param>
    /// <returns>An IFileProvider rooted at the specified directory</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found</exception>
    public static IFileProvider Create(string directoryName)
    {
        var directory = SolutionPathUtility.FindDirectoryUnderSolutionRoot(directoryName);
        return new PhysicalFileProvider(directory);
    }
}
