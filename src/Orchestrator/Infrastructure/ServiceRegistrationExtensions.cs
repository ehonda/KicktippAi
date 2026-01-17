using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Extension methods for registering Orchestrator services in dependency injection.
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Registers all shared infrastructure services (factories, logging).
    /// </summary>
    /// <remarks>
    /// This method is idempotent - calling it multiple times has no additional effect.
    /// </remarks>
    public static IServiceCollection AddOrchestratorInfrastructure(this IServiceCollection services)
    {
        // Add logging (idempotent via TryAdd internally)
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add memory cache for Kicktipp client
        services.AddMemoryCache();

        // Add HTTP client factory for Kicktipp
        services.AddHttpClient();

        // Register factories (idempotent)
        services.TryAddSingleton<IFirebaseServiceFactory, FirebaseServiceFactory>();
        services.TryAddSingleton<IKicktippClientFactory, KicktippClientFactory>();
        services.TryAddSingleton<IOpenAiServiceFactory, OpenAiServiceFactory>();
        services.TryAddSingleton<IContextProviderFactory, ContextProviderFactory>();

        return services;
    }

    /// <summary>
    /// Registers services specific to the ListKpiCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddListKpiCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // ListKpiCommand only needs Firebase factory (uses IKpiRepository)
        // No command-specific keyed services needed - factory pattern handles runtime config

        return services;
    }

    /// <summary>
    /// Service key for the KPI documents file provider.
    /// </summary>
    public const string KpiDocumentsFileProviderKey = "kpi-documents";

    /// <summary>
    /// Service key for the transfers documents file provider.
    /// </summary>
    public const string TransfersDocumentsFileProviderKey = "transfers-documents";

    /// <summary>
    /// Registers services specific to the UploadKpiCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddUploadKpiCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // UploadKpiCommand only needs Firebase factory (uses IKpiRepository)
        // Register keyed file provider for KPI documents directory
        services.TryAddKeyedSingleton<IFileProvider>(
            KpiDocumentsFileProviderKey,
            (_, _) => SolutionRelativeFileProvider.Create("kpi-documents"));

        return services;
    }

    /// <summary>
    /// Registers services specific to the CostCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddCostCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // CostCommand needs Firebase factory (uses IPredictionRepository, FirestoreDb)
        // No command-specific keyed services needed - factory pattern handles runtime config

        return services;
    }

    /// <summary>
    /// Registers services specific to the MatchdayCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddMatchdayCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // MatchdayCommand needs all factories:
        // - Firebase (IPredictionRepository, IContextRepository)
        // - Kicktipp (IKicktippClient)
        // - OpenAI (IPredictionService, ITokenUsageTracker)
        // Factory pattern handles runtime config based on settings

        return services;
    }

    /// <summary>
    /// Registers services specific to the BonusCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddBonusCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // BonusCommand needs all factories:
        // - Firebase (IPredictionRepository, IKpiRepository)
        // - Kicktipp (IKicktippClient)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers services specific to the VerifyMatchdayCommand.
    /// </summary>
    public static IServiceCollection AddVerifyMatchdayCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // VerifyMatchdayCommand needs:
        // - Firebase (IPredictionRepository, IContextRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    /// <summary>
    /// Registers services specific to the VerifyBonusCommand.
    /// </summary>
    public static IServiceCollection AddVerifyBonusCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // VerifyBonusCommand needs:
        // - Firebase (IPredictionRepository, IKpiRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    /// <summary>
    /// Registers services specific to the CollectContextKicktippCommand.
    /// </summary>
    public static IServiceCollection AddCollectContextKicktippCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // CollectContextKicktippCommand needs:
        // - Firebase (IContextRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    /// <summary>
    /// Registers services specific to the ContextChangesCommand.
    /// </summary>
    public static IServiceCollection AddContextChangesCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // ContextChangesCommand only needs Firebase (IContextRepository)

        return services;
    }

    /// <summary>
    /// Registers services specific to the UploadTransfersCommand.
    /// </summary>
    public static IServiceCollection AddUploadTransfersCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // UploadTransfersCommand needs Firebase (IContextRepository)
        // Register keyed file provider for transfers documents directory
        services.TryAddKeyedSingleton<IFileProvider>(
            TransfersDocumentsFileProviderKey,
            (_, _) => SolutionRelativeFileProvider.Create("transfers-documents"));

        return services;
    }

    /// <summary>
    /// Registers services specific to the AnalyzeMatchDetailedCommand.
    /// </summary>
    public static IServiceCollection AddAnalyzeMatchDetailedCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // AnalyzeMatchDetailedCommand needs:
        // - Firebase (IContextRepository)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers services specific to the AnalyzeMatchComparisonCommand.
    /// </summary>
    public static IServiceCollection AddAnalyzeMatchComparisonCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // AnalyzeMatchComparisonCommand needs:
        // - Firebase (IContextRepository)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers all command services. Useful for production setup.
    /// </summary>
    public static IServiceCollection AddAllCommandServices(this IServiceCollection services)
    {
        // Infrastructure is added by each command method, but we call it first for clarity
        services.AddOrchestratorInfrastructure();

        // Register all command-specific services
        services.AddListKpiCommandServices();
        services.AddUploadKpiCommandServices();
        services.AddCostCommandServices();
        services.AddMatchdayCommandServices();
        services.AddBonusCommandServices();
        services.AddVerifyMatchdayCommandServices();
        services.AddVerifyBonusCommandServices();
        services.AddCollectContextKicktippCommandServices();
        services.AddContextChangesCommandServices();
        services.AddUploadTransfersCommandServices();
        services.AddAnalyzeMatchDetailedCommandServices();
        services.AddAnalyzeMatchComparisonCommandServices();

        return services;
    }
}
