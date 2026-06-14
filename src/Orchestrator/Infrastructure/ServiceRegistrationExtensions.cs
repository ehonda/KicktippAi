using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Orchestrator.Services;

namespace Orchestrator.Infrastructure;

/// <summary>
/// Extension methods for registering Orchestrator services in dependency injection.
/// </summary>
public static class ServiceRegistrationExtensions
{
    public const string OpenAiHttpClientName = "openai";

    private const string LangfuseIngestionVersionHeaderName = "x-langfuse-ingestion-version";
    private const string LangfuseIngestionVersionHeaderValue = "4";
    private static readonly Uri FifaApiBaseAddress = new("https://api.fifa.com/api/v3/");

    /// <summary>
    /// Registers all shared infrastructure services (factories, logging).
    /// </summary>
    /// <remarks>
    /// This method is idempotent - calling it multiple times has no additional effect.
    /// </remarks>
    public static IServiceCollection AddOrchestratorInfrastructure(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        // Add logging (idempotent via TryAdd internally)
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.TimestampFormat = null;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            });
            builder.SetMinimumLevel(minimumLogLevel);
        });

        // Add memory cache for Kicktipp client
        services.AddMemoryCache();

        // Add HTTP client factory for Kicktipp
        services.AddHttpClient();
        services.AddOpenAiHttpClientIfMissing();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(ILangfusePublicApiClient)))
        {
            services.AddLangfusePublicApiClient();
        }

        // Register factories (idempotent)
        services.TryAddSingleton<IFirebaseServiceFactory, FirebaseServiceFactory>();
        services.TryAddSingleton<IKicktippClientFactory, KicktippClientFactory>();
        services.TryAddSingleton<IOpenAiServiceFactory, OpenAiServiceFactory>();
        services.TryAddSingleton<IContextProviderFactory, ContextProviderFactory>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddTransient<MatchOutcomeCollectionService>();

        // Register Langfuse/OTel tracing (no-op if credentials are absent)
        services.AddLangfuseTracing();

        return services;
    }

    private static IServiceCollection AddOpenAiHttpClientIfMissing(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(OpenAiHttpClientRegistrationMarker)))
        {
            return services;
        }

        services.TryAddSingleton<OpenAiHttpClientRegistrationMarker>();

        var clientBuilder = services.AddHttpClient(OpenAiHttpClientName, client =>
        {
            // OpenAI timeout ownership stays in ResponsesClientOptions.NetworkTimeout and
            // the .NET HTTP resilience pipeline. Keeping HttpClient.Timeout infinite
            // avoids a third timeout source racing those mechanisms.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        clientBuilder.AddStandardResilienceHandler().Configure(options =>
        {
            var defaultCircuitBreakerShouldHandle = options.CircuitBreaker.ShouldHandle;

            options.Retry.DisableForUnsafeHttpMethods();
            options.CircuitBreaker.ShouldHandle = async args =>
            {
                if (!await defaultCircuitBreakerShouldHandle(args).ConfigureAwait(false))
                {
                    return false;
                }

                return args.Outcome.Result?.StatusCode is not HttpStatusCode.RequestTimeout
                    and not HttpStatusCode.TooManyRequests;
            };
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(15);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(15);
        });

        return services;
    }

    internal static IHttpClientBuilder AddLangfusePublicApiClient(this IServiceCollection services)
    {
        services.TryAddTransient<LangfuseRetryLoggingHandler>();

        var clientBuilder = services.AddHttpClient<ILangfusePublicApiClient, LangfusePublicApiClient>((_, client) =>
        {
            var baseUrl = (Environment.GetEnvironmentVariable("LANGFUSE_BASE_URL") ?? "https://cloud.langfuse.com").TrimEnd('/');
            client.BaseAddress = new Uri($"{baseUrl}/api/public/");
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var publicKey = Environment.GetEnvironmentVariable("LANGFUSE_PUBLIC_KEY");
            var secretKey = Environment.GetEnvironmentVariable("LANGFUSE_SECRET_KEY");
            if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(secretKey))
            {
                var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authorization);
            }
        });

        clientBuilder.AddStandardResilienceHandler().Configure(options =>
        {
            var defaultShouldHandle = options.Retry.ShouldHandle;
            var defaultCircuitBreakerShouldHandle = options.CircuitBreaker.ShouldHandle;
            options.Retry.DisableForUnsafeHttpMethods();
            var safeMethodShouldHandle = options.Retry.ShouldHandle;

            options.Retry.ShouldHandle = async args =>
            {
                if (await safeMethodShouldHandle(args).ConfigureAwait(false))
                {
                    return true;
                }

                if (!await defaultShouldHandle(args).ConfigureAwait(false))
                {
                    return false;
                }

                var response = args.Outcome.Result;
                var request = response?.RequestMessage;
                return response?.StatusCode == HttpStatusCode.TooManyRequests
                    && request is not null
                    && ShouldRetryUnsafeLangfuseRateLimit(request);
            };

            options.Retry.DelayGenerator = static args =>
            {
                var response = args.Outcome.Result;
                if (response is null)
                {
                    return new ValueTask<TimeSpan?>((TimeSpan?)null);
                }

                var retryMetadata = LangfuseRetryAfterUtility.GetRetryAfterMetadata(response.Headers);
                return new ValueTask<TimeSpan?>(retryMetadata.RetryAfterDelay);
            };
            options.CircuitBreaker.ShouldHandle = async args =>
            {
                if (!await defaultCircuitBreakerShouldHandle(args).ConfigureAwait(false))
                {
                    return false;
                }

                return args.Outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests;
            };
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(45);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(4);
        });

        // Keep the retry logging handler inside the resilience pipeline so every attempted request is visible.
        clientBuilder.AddHttpMessageHandler<LangfuseRetryLoggingHandler>();

        return clientBuilder;
    }

    private static bool ShouldRetryUnsafeLangfuseRateLimit(HttpRequestMessage request)
    {
        var absolutePath = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (string.Equals(request.Method.Method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath.EndsWith("/api/public/scores", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/scores", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/api/public/dataset-items", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/dataset-items", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/api/public/dataset-run-items", StringComparison.OrdinalIgnoreCase)
                || absolutePath.EndsWith("/dataset-run-items", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(request.Method.Method, HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath.Contains("/api/public/datasets/", StringComparison.OrdinalIgnoreCase)
                && absolutePath.Contains("/runs/", StringComparison.OrdinalIgnoreCase);
        }

        return false;
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
        var headers = BuildLangfuseOtlpHeaders(publicKey, secretKey);

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
                    options.Headers = headers;
                }));

        return services;
    }

    internal static string BuildLangfuseOtlpHeaders(string publicKey, string secretKey)
    {
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));
        return string.Join(",",
            $"Authorization=Basic {authorization}",
            $"{LangfuseIngestionVersionHeaderName}={LangfuseIngestionVersionHeaderValue}");
    }

    private static bool _langfuseTracingRegistered;

    private sealed class OpenAiHttpClientRegistrationMarker
    {
    }

    /// <summary>
    /// Registers services specific to the ListKpiCommand.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and ensures infrastructure is registered.
    /// </remarks>
    public static IServiceCollection AddListKpiCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

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
    public static IServiceCollection AddUploadKpiCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

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
    public static IServiceCollection AddCostCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

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
    public static IServiceCollection AddMatchdayCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

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
    public static IServiceCollection AddRandomMatchCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

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
    public static IServiceCollection AddBonusCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // BonusCommand needs all factories:
        // - Firebase (IPredictionRepository, IKpiRepository)
        // - Kicktipp (IKicktippClient)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers services specific to the VerifyMatchdayCommand.
    /// </summary>
    public static IServiceCollection AddVerifyMatchdayCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // VerifyMatchdayCommand needs:
        // - Firebase (IPredictionRepository, IContextRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    private static IServiceCollection AddFifaRankingSourceServicesIfMissing(this IServiceCollection services)
    {
        services.TryAddTransient<IFifaRankingSource, FifaRankingSource>();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IFifaRankingApiClient)))
        {
            return services;
        }

        services.AddHttpClient<IFifaRankingApiClient, FifaRankingApiClient>(client =>
        {
            client.BaseAddress = FifaApiBaseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }

    private static IServiceCollection AddWm26LineupSourceServicesIfMissing(this IServiceCollection services)
    {
        services.TryAddTransient<IWm26LineupSource, Wm26LineupSource>();

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IWm26TransfermarktDuckDbProvider)))
        {
            services.AddHttpClient<IWm26TransfermarktDuckDbProvider, Wm26TransfermarktDuckDbProvider>();
        }

        return services;
    }

    /// <summary>
    /// Registers services specific to the VerifyBonusCommand.
    /// </summary>
    public static IServiceCollection AddVerifyBonusCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // VerifyBonusCommand needs:
        // - Firebase (IPredictionRepository, IKpiRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    /// <summary>
    /// Registers services specific to the CollectContextKicktippCommand.
    /// </summary>
    public static IServiceCollection AddCollectContextKicktippCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // CollectContextKicktippCommand needs:
        // - Firebase (IContextRepository, IMatchOutcomeRepository)
        // - Kicktipp (IKicktippClient)

        return services;
    }

    /// <summary>
    /// Registers services specific to the CollectContextFifaCommand.
    /// </summary>
    public static IServiceCollection AddCollectContextFifaCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        services.AddFifaRankingSourceServicesIfMissing();

        // CollectContextFifaCommand needs Firebase (IContextRepository, IKpiRepository)
        // and the public FIFA rankings API.

        return services;
    }

    /// <summary>
    /// Registers services specific to the CollectContextLineupsCommand.
    /// </summary>
    public static IServiceCollection AddCollectContextLineupsCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);
        services.AddWm26LineupSourceServicesIfMissing();

        // CollectContextLineupsCommand needs Firebase (IContextRepository, IKpiRepository)
        // and the Transfermarkt DuckDB snapshot.

        return services;
    }

    /// <summary>
    /// Registers services specific to the CollectContextDevCommand.
    /// </summary>
    public static IServiceCollection AddCollectContextDevCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);
        services.AddFifaRankingSourceServicesIfMissing();
        services.AddWm26LineupSourceServicesIfMissing();

        // CollectContextDevCommand composes the Kicktipp, FIFA, and lineup collection paths.

        return services;
    }

    /// <summary>
    /// Registers services specific to WM26 recent-history date-map commands.
    /// </summary>
    public static IServiceCollection AddWm26RecentHistoryCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // WM26 recent-history commands need Firebase (IContextRepository).

        return services;
    }

    /// <summary>
    /// Registers services specific to the ContextChangesCommand.
    /// </summary>
    public static IServiceCollection AddContextChangesCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // ContextChangesCommand only needs Firebase (IContextRepository)

        return services;
    }

    /// <summary>
    /// Registers services specific to the UploadTransfersCommand.
    /// </summary>
    public static IServiceCollection AddUploadTransfersCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // UploadTransfersCommand needs Firebase (IContextRepository)
        // Register keyed file provider for transfers documents directory
        services.TryAddKeyedSingleton<IFileProvider>(
            TransfersDocumentsFileProviderKey,
            (_, _) => SolutionRelativeFileProvider.Create("transfers-documents"));

        return services;
    }

    /// <summary>
    /// Registers services specific to the UploadContextCommand.
    /// </summary>
    public static IServiceCollection AddUploadContextCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // UploadContextCommand needs Firebase (IContextRepository).

        return services;
    }

    /// <summary>
    /// Registers services specific to the CopyFirestoreContextCommand.
    /// </summary>
    public static IServiceCollection AddCopyFirestoreContextCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // CopyFirestoreContextCommand needs Firebase (IContextRepository, IKpiRepository).

        return services;
    }

    /// <summary>
    /// Registers services specific to the AnalyzeMatchDetailedCommand.
    /// </summary>
    public static IServiceCollection AddAnalyzeMatchDetailedCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // AnalyzeMatchDetailedCommand needs:
        // - Firebase (IContextRepository)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers services specific to the AnalyzeMatchComparisonCommand.
    /// </summary>
    public static IServiceCollection AddAnalyzeMatchComparisonCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // AnalyzeMatchComparisonCommand needs:
        // - Firebase (IContextRepository)
        // - OpenAI (IPredictionService, ITokenUsageTracker)

        return services;
    }

    /// <summary>
    /// Registers all command services. Useful for production setup.
    /// </summary>
    public static IServiceCollection AddAllCommandServices(
        this IServiceCollection services,
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        // Infrastructure is added by each command method, but we call it first for clarity
        services.AddOrchestratorInfrastructure(minimumLogLevel);

        // Register all command-specific services
        services.AddListKpiCommandServices(minimumLogLevel);
        services.AddUploadKpiCommandServices(minimumLogLevel);
        services.AddCostCommandServices(minimumLogLevel);
        services.AddMatchdayCommandServices(minimumLogLevel);
        services.AddRandomMatchCommandServices(minimumLogLevel);
        services.AddBonusCommandServices(minimumLogLevel);
        services.AddVerifyMatchdayCommandServices(minimumLogLevel);
        services.AddVerifyBonusCommandServices(minimumLogLevel);
        services.AddCollectContextKicktippCommandServices(minimumLogLevel);
        services.AddCollectContextFifaCommandServices(minimumLogLevel);
        services.AddCollectContextLineupsCommandServices(minimumLogLevel);
        services.AddCollectContextDevCommandServices(minimumLogLevel);
        services.AddWm26RecentHistoryCommandServices(minimumLogLevel);
        services.AddContextChangesCommandServices(minimumLogLevel);
        services.AddUploadTransfersCommandServices(minimumLogLevel);
        services.AddUploadContextCommandServices(minimumLogLevel);
        services.AddCopyFirestoreContextCommandServices(minimumLogLevel);
        services.AddAnalyzeMatchDetailedCommandServices(minimumLogLevel);
        services.AddAnalyzeMatchComparisonCommandServices(minimumLogLevel);

        return services;
    }
}
