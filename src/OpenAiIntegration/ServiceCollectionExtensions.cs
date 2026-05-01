using System.ClientModel;
using System.ClientModel.Primitives;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;

namespace OpenAiIntegration;

/// <summary>
/// Extension methods for configuring OpenAI services in dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan OpenAiNetworkTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Adds OpenAI predictor services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="apiKey">The OpenAI API key</param>
    /// <param name="model">The OpenAI model to use (defaults to gpt-4o-mini)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAiPredictor(
        this IServiceCollection services,
        string apiKey,
        string model = "gpt-4o-mini")
    {
        return services.AddOpenAiPredictor(apiKey, model, options: null);
    }

    /// <summary>
    /// Adds OpenAI predictor services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="apiKey">The OpenAI API key</param>
    /// <param name="options">The prediction service options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAiPredictor(
        this IServiceCollection services,
        string apiKey,
        PredictionServiceOptions? options)
    {
        return services.AddOpenAiPredictor(apiKey, "gpt-4o-mini", options);
    }

    /// <summary>
    /// Adds OpenAI predictor services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="apiKey">The OpenAI API key</param>
    /// <param name="model">The OpenAI model to use</param>
    /// <param name="options">The prediction service options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAiPredictor(
        this IServiceCollection services,
        string apiKey,
        string model,
        PredictionServiceOptions? options)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key cannot be null or empty", nameof(apiKey));
        }

        // Register the ResponsesClient as a singleton
        services.TryAddSingleton<ResponsesClient>(serviceProvider =>
        {
            return new ResponsesClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions
                {
                    NetworkTimeout = OpenAiNetworkTimeout,
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
                });
        });

        // Register the predictor context
        services.TryAddScoped<PredictorContext>(_ => PredictorContext.CreateBasic());

        // Register the predictor implementation
        services.TryAddScoped<IPredictor<PredictorContext>>(serviceProvider =>
            new OpenAiPredictor(
                serviceProvider.GetRequiredService<ResponsesClient>(),
                serviceProvider.GetRequiredService<ILogger<OpenAiPredictor>>(),
                model));

        // Register the cost calculation service
        services.TryAddScoped<ICostCalculationService, CostCalculationService>();

        // Register the file provider for prompts
        services.TryAddSingleton(PromptsFileProvider.Create());

        // Register the instructions template provider
        services.TryAddSingleton<IInstructionsTemplateProvider, InstructionsTemplateProvider>();

        services.TryAddScoped<IMatchPromptReconstructionService, MatchPromptReconstructionService>();

        // Register the token usage tracker as singleton (to accumulate across requests)
        services.TryAddSingleton<ITokenUsageTracker>(serviceProvider =>
            new TokenUsageTracker(
                serviceProvider.GetRequiredService<ILogger<TokenUsageTracker>>(),
                serviceProvider.GetRequiredService<ICostCalculationService>()));

        // Register the prediction service with model parameter
        services.TryAddScoped<IPredictionService>(serviceProvider =>
            new PredictionService(
                serviceProvider.GetRequiredService<ResponsesClient>(),
                serviceProvider.GetRequiredService<ILogger<PredictionService>>(),
                serviceProvider.GetRequiredService<ICostCalculationService>(),
                serviceProvider.GetRequiredService<ITokenUsageTracker>(),
                serviceProvider.GetRequiredService<IInstructionsTemplateProvider>(),
                model,
                options));

        return services;
    }

    /// <summary>
    /// Adds OpenAI predictor services to the service collection using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration containing OpenAI settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAiPredictor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddOpenAiPredictor(configuration, options: null);
    }

    /// <summary>
    /// Adds OpenAI predictor services to the service collection using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration containing OpenAI settings</param>
    /// <param name="options">The prediction service options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddOpenAiPredictor(
        this IServiceCollection services,
        IConfiguration configuration,
        PredictionServiceOptions? options)
    {
        var apiKey = configuration["OPENAI_API_KEY"] ??
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";

        return services.AddOpenAiPredictor(apiKey!, model, options);
    }
}
