using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KicktippIntegration.Authentication;

namespace KicktippIntegration;

/// <summary>
/// Extension methods for configuring Kicktipp services in dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kicktipp client services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKicktippClient(this IServiceCollection services)
    {
        // Register the authentication handler as singleton to share cookies across all clients
        services.TryAddSingleton<KicktippAuthenticationHandler>();
        
        // Register the HTTP client with authentication
        services
            .AddHttpClient<IKicktippClient, KicktippClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.kicktipp.de");
                // Set a reasonable timeout for web scraping operations
                client.Timeout = TimeSpan.FromMinutes(2);
                // Add headers to mimic a real browser
                client.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            })
            .AddHttpMessageHandler<KicktippAuthenticationHandler>();
        
        return services;
    }
}
