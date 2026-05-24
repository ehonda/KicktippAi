using EHonda.KicktippAi.Core;
using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp;

/// <summary>
/// Factory for creating an <see cref="IFileProvider"/> rooted at the WM26 onboarding context directory.
/// </summary>
public static class WorldCup2026ContextDocumentsFileProvider
{
    /// <summary>
    /// Creates a file provider rooted at docs/onboarding-wm26.
    /// </summary>
    public static IFileProvider Create() => SolutionRelativeFileProvider.Create("docs/onboarding-wm26");
}
