using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Core;

namespace OpenAiIntegration;

/// <summary>
/// Extension methods for configuring OpenAI services in dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
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
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key cannot be null or empty", nameof(apiKey));
        }

        // Register the ChatClient as a singleton
        services.TryAddSingleton<ChatClient>(serviceProvider =>
        {
            return new ChatClient(model, apiKey);
        });

        // Register the predictor context
        services.TryAddScoped<PredictorContext>(_ => PredictorContext.CreateBasic());

        // Register the predictor implementation
        services.TryAddScoped<IPredictor<PredictorContext>, OpenAiPredictor>();

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
        var apiKey = configuration["OPENAI_API_KEY"] ?? 
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";

        return services.AddOpenAiPredictor(apiKey!, model);
    }
}
