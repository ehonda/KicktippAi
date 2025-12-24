using EHonda.KicktippAi.Core;
using FirebaseAdapter.Configuration;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

/// <summary>
/// Tests for ServiceCollectionExtensions.
/// These tests verify that the DI registration is configured correctly.
/// Note: These tests do not actually connect to Firebase; they verify service registration only.
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
    /// Configures the service collection with default Firebase options for testing.
    /// </summary>
    private static void AddDefaultFirebaseDatabase(IServiceCollection services)
    {
        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });
    }

    [Test]
    public async Task AddFirebaseDatabase_with_options_delegate_configures_options()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "test-json";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options).Member(o => o.ProjectId, p => p.IsEqualTo("test-project"))
            .And.Member(o => o.ServiceAccountJson, s => s.IsEqualTo("test-json"));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_FirestoreDb_as_singleton()
    {
        // Arrange
        var services = CreateServices();

        // Act
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FirestoreDb));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, l => l.IsEqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IPredictionRepository_as_scoped()
    {
        // Arrange
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPredictionRepository));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, l => l.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IKpiRepository_as_scoped()
    {
        // Arrange
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKpiRepository));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, l => l.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IContextRepository_as_scoped()
    {
        // Arrange
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IContextRepository));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, l => l.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_FirebaseKpiContextProvider_as_scoped()
    {
        // Arrange
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FirebaseKpiContextProvider));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, l => l.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_with_IConfiguration_binds_from_section()
    {
        // Arrange
        var services = CreateServices();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ProjectId"] = "config-project",
                ["Firebase:ServiceAccountJson"] = "config-json"
            })
            .Build();

        // Act
        services.AddFirebaseDatabase(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options).Member(o => o.ProjectId, p => p.IsEqualTo("config-project"))
            .And.Member(o => o.ServiceAccountJson, s => s.IsEqualTo("config-json"));
    }

    [Test]
    public async Task AddFirebaseDatabase_with_explicit_params_configures_options()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddFirebaseDatabase(
            projectId: "explicit-project",
            serviceAccountJson: "explicit-json",
            community: "test-community");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options).Member(o => o.ProjectId, p => p.IsEqualTo("explicit-project"))
            .And.Member(o => o.ServiceAccountJson, s => s.IsEqualTo("explicit-json"));
    }

    [Test]
    public async Task AddFirebaseDatabaseWithFile_configures_ServiceAccountPath()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddFirebaseDatabaseWithFile(
            projectId: "file-project",
            serviceAccountPath: "/path/to/service-account.json");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options).Member(o => o.ProjectId, p => p.IsEqualTo("file-project"))
            .And.Member(o => o.ServiceAccountPath, s => s.IsEqualTo("/path/to/service-account.json"));
    }

    [Test]
    public async Task AddFirebaseDatabase_returns_service_collection_for_chaining()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var result = services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        await Assert.That(result).IsEqualTo(services);
    }
}
