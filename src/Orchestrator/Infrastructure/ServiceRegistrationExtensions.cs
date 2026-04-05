using System.Text;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Services;

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
        services.TryAddTransient<MatchOutcomeCollectionService>();

        // Register Langfuse/OTel tracing (no-op if credentials are absent)
        services.AddLangfuseTracing();

        return services;
    }

    /// <summary>
    /// Registers the OpenTelemetry tracing pipeline with the Langfuse OTLP endpoint.
    /// If <c>LANGFUSE_PUBLIC_KEY</c> or <c>LANGFUSE_SECRET_KEY</c> are not set,
    /// no pipeline is registered and all <see cref="System.Diagnostics.ActivitySource.StartActivity(string)"/>
    /// calls return <c>null</c> (graceful degradation).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the standard <c>AddOpenTelemetry()</c> API from <c>OpenTelemetry.Extensions.Hosting</c>,
    /// which registers the <see cref="TracerProvider"/> as a DI-managed singleton and an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> that triggers provider construction.
    /// Since this app uses Spectre.Console.Cli (no <c>IHost</c>), the <see cref="TypeRegistrar"/>
    /// manually starts hosted services after building the <c>ServiceProvider</c>.
    /// </para>
    /// <para>
    /// This method is idempotent — multiple calls have no additional effect.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddLangfuseTracing(this IServiceCollection services)
    {
        // Idempotency: skip if tracing has already been registered.
        if (_langfuseTracingRegistered)
            return services;

        var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
        var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");

        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(secretKey))
        {
            // No credentials — skip OTel registration entirely
            return services;
        }

        _langfuseTracingRegistered = true;

        var baseUrl = Environment.GetEnvironmentVariable("LANGFUSE_BASE_URL") ?? "https://cloud.langfuse.com";
        var authHeader = $"Authorization=Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"))}";

        // NOTE: Setting options.Endpoint programmatically sets AppendSignalPathToEndpoint = false,
        // so the full URL including /v1/traces must be provided.
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("KicktippAi"))
            .WithTracing(tracing => tracing
                .AddSource(Telemetry.Source.Name)
                .AddProcessor(new LangfuseBaggageSpanProcessor())
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri($"{baseUrl}/api/public/otel/v1/traces");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = authHeader;
                }));

        return services;
    }

    private static bool _langfuseTracingRegistered;

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
    /// Registers services specific to the RandomMatchCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddRandomMatchCommandServices(this IServiceCollection services)
    {
        services.AddOrchestratorInfrastructure();

        // RandomMatchCommand needs the same factories as MatchdayCommand:
        // - Firebase (IPredictionRepository, IContextRepository)
        // - Kicktipp (IKicktippClient)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

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
        // - Firebase (IContextRepository, IMatchOutcomeRepository)
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
        services.AddRandomMatchCommandServices();
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
