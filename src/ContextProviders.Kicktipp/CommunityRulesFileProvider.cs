using EHonda.KicktippAi.Core;
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
    public static IFileProvider Create() => SolutionRelativeFileProvider.Create("community-rules");
}
