using EHonda.KicktippAi.Core;
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
    public static IFileProvider Create() => SolutionRelativeFileProvider.Create("prompts");
}
