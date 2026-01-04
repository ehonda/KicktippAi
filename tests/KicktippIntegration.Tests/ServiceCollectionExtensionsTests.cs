using KicktippIntegration.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TUnit.Core;

namespace KicktippIntegration.Tests;

/// <summary>
/// Tests for ServiceCollectionExtensions.
/// These tests verify that the DI registration is configured correctly.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// Creates a new ServiceCollection with logging already configured.
    /// </summary>
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// Creates a KicktippAuthenticationHandler with default dependencies for testing.
    /// </summary>
    private static KicktippAuthenticationHandler CreateAuthenticationHandler()
    {
        var options = Options.Create(new KicktippOptions());
        var logger = new LoggerFactory().CreateLogger<KicktippAuthenticationHandler>();
        return new KicktippAuthenticationHandler(options, logger);
    }

    [Test]
    public async Task AddKicktippClient_registers_IKicktippClient()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddKicktippClient();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKicktippClient));

        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task AddKicktippClient_registers_KicktippAuthenticationHandler_as_singleton()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddKicktippClient();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(KicktippAuthenticationHandler));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public async Task AddKicktippClient_registers_IMemoryCache()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddKicktippClient();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMemoryCache));

        await Assert.That(descriptor).IsNotNull();
    }

    [Test]
    public async Task AddKicktippClient_returns_service_collection_for_chaining()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var result = services.AddKicktippClient();

        // Assert
        await Assert.That(result).IsEqualTo(services);
    }

    [Test]
    public async Task AddKicktippClient_does_not_replace_existing_KicktippAuthenticationHandler()
    {
        // Arrange
        var services = CreateServices();
        var existingHandler = CreateAuthenticationHandler();
        services.AddSingleton(existingHandler);

        // Act
        services.AddKicktippClient();

        // Assert - The original handler should be preserved (TryAddSingleton behavior)
        var provider = services.BuildServiceProvider();
        var resolvedHandler = provider.GetRequiredService<KicktippAuthenticationHandler>();

        await Assert.That(resolvedHandler).IsSameReferenceAs(existingHandler);
    }

    [Test]
    public async Task AddKicktippClient_registers_IHttpClientFactory()
    {
        // Arrange
        var services = CreateServices();
        services.AddKicktippClient();

        // Act
        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IHttpClientFactory>();

        // Assert
        await Assert.That(factory).IsNotNull();
    }

    [Test]
    public async Task AddKicktippClient_registers_typed_client_configuration()
    {
        // Arrange
        var services = CreateServices();
        services.AddKicktippClient();

        // Act - Verify that a typed HttpClient configuration is registered for KicktippClient
        // The HttpClient builder registers an IConfigureOptions<HttpClientFactoryOptions> for the typed client
        var configureOptionsDescriptors = services.Where(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
            d.ServiceType.GetGenericArguments()[0] == typeof(HttpClientFactoryOptions));

        // Assert - There should be configuration registered for HttpClientFactoryOptions
        // This verifies that AddHttpClient<> was called with configuration
        await Assert.That(configureOptionsDescriptors.Any()).IsTrue();
    }
}
