using System.Collections.Concurrent;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;
using OpenAiIntegration;
using Orchestrator.Infrastructure;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Default implementation of <see cref="IOpenAiServiceFactory"/>.
/// </summary>
/// <remarks>
/// Reads the API key from OPENAI_API_KEY environment variable.
/// Caches services by model to avoid recreating them for repeated requests.
/// The <see cref="ITokenUsageTracker"/> is shared across all prediction services.
/// </remarks>
public sealed class OpenAiServiceFactory : IOpenAiServiceFactory
{
    private static readonly TimeSpan OpenAiNetworkTimeout = TimeSpan.FromMinutes(15);

    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly Lazy<string> _apiKey;
    private readonly ConcurrentDictionary<string, IPredictionService> _predictionServiceCache = new();
    private ITokenUsageTracker? _tokenUsageTracker;
    private ICostCalculationService? _costCalculationService;
    private IInstructionsTemplateProvider? _instructionsTemplateProvider;
    private readonly object _lock = new();

    public OpenAiServiceFactory(ILoggerFactory loggerFactory, IHttpClientFactory? httpClientFactory = null)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _apiKey = new Lazy<string>(GetApiKeyFromEnvironment);
    }

    /// <inheritdoc />
    public IPredictionService CreatePredictionService(string model)
    {
        return CreatePredictionService(model, PredictionServiceOptions.Default);
    }

    /// <inheritdoc />
    public IPredictionService CreatePredictionService(string model, PredictionServiceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(options);

        var apiKey = _apiKey.Value;
        var reasoningEffort = string.IsNullOrWhiteSpace(options.ReasoningEffort)
            ? string.Empty
            : options.ReasoningEffort.Trim().ToLowerInvariant();
        var cacheKey = $"{model}|disableFlex={options.DisableFlexProcessing}|reasoningEffort={reasoningEffort}";

        // Cache key includes model to handle different configurations
        return _predictionServiceCache.GetOrAdd(cacheKey, _ =>
        {
            return CreatePredictionServiceCore(
                model,
                options,
                GetOrCreateInstructionsTemplateProvider(),
                apiKey);
        });
    }

    /// <inheritdoc />
    public IPredictionService CreatePredictionService(
        string model,
        PredictionServiceOptions options,
        IInstructionsTemplateProvider templateProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(templateProvider);

        return CreatePredictionServiceCore(model, options, templateProvider, _apiKey.Value);
    }

    /// <inheritdoc />
    public ITokenUsageTracker GetTokenUsageTracker()
    {
        if (_tokenUsageTracker == null)
        {
            lock (_lock)
            {
                _tokenUsageTracker ??= new TokenUsageTracker(
                    _loggerFactory.CreateLogger<TokenUsageTracker>(),
                    GetOrCreateCostCalculationService());
            }
        }

        return _tokenUsageTracker;
    }

    private static string GetApiKeyFromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");
        }

        return apiKey;
    }

    private ICostCalculationService GetOrCreateCostCalculationService()
    {
        if (_costCalculationService == null)
        {
            lock (_lock)
            {
                _costCalculationService ??= new CostCalculationService(
                    _loggerFactory.CreateLogger<CostCalculationService>());
            }
        }

        return _costCalculationService;
    }

    private IInstructionsTemplateProvider GetOrCreateInstructionsTemplateProvider()
    {
        if (_instructionsTemplateProvider == null)
        {
            lock (_lock)
            {
                _instructionsTemplateProvider ??= new InstructionsTemplateProvider(
                    PromptsFileProvider.Create());
            }
        }

        return _instructionsTemplateProvider;
    }

    private IPredictionService CreatePredictionServiceCore(
        string model,
        PredictionServiceOptions options,
        IInstructionsTemplateProvider templateProvider,
        string apiKey)
    {
        var logger = _loggerFactory.CreateLogger<PredictionService>();
        var responsesClient = new ResponsesClient(new ApiKeyCredential(apiKey), CreateOpenAiClientOptions());

        return new PredictionService(
            responsesClient,
            logger,
            GetOrCreateCostCalculationService(),
            GetTokenUsageTracker(),
            templateProvider,
            model,
            options);
    }

    private OpenAIClientOptions CreateOpenAiClientOptions()
    {
        var options = new OpenAIClientOptions
        {
            // OpenAI recommends allowing long-running model requests up to 15 minutes.
            // This timeout belongs to the OpenAI client pipeline, while HttpClient.Timeout
            // stays infinite so it does not race the .NET HTTP resilience handler.
            NetworkTimeout = OpenAiNetworkTimeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };

        if (_httpClientFactory is not null)
        {
            options.Transport = new HttpClientPipelineTransport(
                _httpClientFactory.CreateClient(ServiceRegistrationExtensions.OpenAiHttpClientName));
        }

        return options;
    }
}
