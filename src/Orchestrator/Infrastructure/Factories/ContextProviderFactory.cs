using ContextProviders.Kicktipp;
using KicktippIntegration;
using Microsoft.Extensions.FileProviders;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Default implementation of <see cref="IContextProviderFactory"/>.
/// </summary>
public sealed class ContextProviderFactory : IContextProviderFactory
{
    private readonly Lazy<IFileProvider> _communityRulesFileProvider;

    public ContextProviderFactory()
    {
        _communityRulesFileProvider = new Lazy<IFileProvider>(
            ContextProviders.Kicktipp.CommunityRulesFileProvider.Create);
    }

    /// <inheritdoc />
    public IFileProvider CommunityRulesFileProvider => _communityRulesFileProvider.Value;

    /// <inheritdoc />
    public KicktippContextProvider CreateKicktippContextProvider(
        IKicktippClient kicktippClient,
        string community,
        string? communityContext = null)
    {
        return new KicktippContextProvider(
            kicktippClient,
            CommunityRulesFileProvider,
            community,
            communityContext);
    }
}
