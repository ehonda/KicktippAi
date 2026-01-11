using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Default implementation of <see cref="IContextProviderFactory"/>.
/// </summary>
public sealed class ContextProviderFactory : IContextProviderFactory
{
    private readonly Lazy<IFileProvider> _communityRulesFileProvider;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<FirebaseKpiContextProvider> _kpiContextProviderLogger;

    public ContextProviderFactory(
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<FirebaseKpiContextProvider> kpiContextProviderLogger)
    {
        _communityRulesFileProvider = new Lazy<IFileProvider>(
            ContextProviders.Kicktipp.CommunityRulesFileProvider.Create);
        _firebaseServiceFactory = firebaseServiceFactory;
        _kpiContextProviderLogger = kpiContextProviderLogger;
    }

    /// <inheritdoc />
    public IFileProvider CommunityRulesFileProvider => _communityRulesFileProvider.Value;

    /// <inheritdoc />
    public IKicktippContextProvider CreateKicktippContextProvider(
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

    /// <inheritdoc />
    public IKpiContextProvider CreateKpiContextProvider()
    {
        var kpiRepository = _firebaseServiceFactory.CreateKpiRepository();
        return new FirebaseKpiContextProvider(kpiRepository, _kpiContextProviderLogger);
    }
}
