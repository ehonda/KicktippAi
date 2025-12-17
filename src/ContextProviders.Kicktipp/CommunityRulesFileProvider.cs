using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp;

/// <summary>
/// Factory for creating an IFileProvider rooted at the community-rules directory
/// </summary>
public static class CommunityRulesFileProvider
{
    /// <summary>
    /// Creates a PhysicalFileProvider rooted at the community-rules directory by finding the solution root
    /// </summary>
    /// <returns>An IFileProvider rooted at the community-rules directory</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution root cannot be found</exception>
    public static IFileProvider Create()
    {
        var communityRulesDirectory = FindCommunityRulesDirectory();
        return new PhysicalFileProvider(communityRulesDirectory);
    }

    private static string FindCommunityRulesDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null)
        {
            var solutionFile = Path.Combine(directory.FullName, "KicktippAi.slnx");
            if (File.Exists(solutionFile))
            {
                return Path.Combine(directory.FullName, "community-rules");
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find solution root (KicktippAi.slnx) to locate community-rules directory");
    }
}
