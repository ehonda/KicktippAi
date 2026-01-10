using ContextProviders.Kicktipp;
using Microsoft.Extensions.FileProviders;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Factory for creating context providers.
/// </summary>
/// <remarks>
/// Provides methods to create context providers used for gathering match and bonus
/// prediction context from various sources (Kicktipp, community rules files, etc.).
/// </remarks>
public interface IContextProviderFactory
{
    /// <summary>
    /// Gets the community rules file provider.
    /// </summary>
    IFileProvider CommunityRulesFileProvider { get; }

    /// <summary>
    /// Creates a Kicktipp context provider for the specified community.
    /// </summary>
    /// <param name="kicktippClient">The Kicktipp client to use for fetching data.</param>
    /// <param name="community">The Kicktipp community name.</param>
    /// <param name="communityContext">The community context identifier (defaults to community if not specified).</param>
    /// <returns>A configured <see cref="IKicktippContextProvider"/> instance.</returns>
    IKicktippContextProvider CreateKicktippContextProvider(
        KicktippIntegration.IKicktippClient kicktippClient,
        string community,
        string? communityContext = null);
}
