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
[NotInParallel("ServiceCollectionTests")]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddFirebaseDatabase_with_options_delegate_configures_options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "test-json";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options.ProjectId).IsEqualTo("test-project");
        await Assert.That(options.ServiceAccountJson).IsEqualTo("test-json");
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_FirestoreDb_as_singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}"; // Minimal JSON
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FirestoreDb));

        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IPredictionRepository_as_scoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPredictionRepository));

        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IKpiRepository_as_scoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKpiRepository));

        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IContextRepository_as_scoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IContextRepository));

        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_FirebaseKpiContextProvider_as_scoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFirebaseDatabase(options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FirebaseKpiContextProvider));

        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.Lifetime).IsEqualTo(ServiceLifetime.Scoped);
    }

    [Test]
    public async Task AddFirebaseDatabase_with_IConfiguration_binds_from_section()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

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

        await Assert.That(options.ProjectId).IsEqualTo("config-project");
        await Assert.That(options.ServiceAccountJson).IsEqualTo("config-json");
    }

    [Test]
    public async Task AddFirebaseDatabase_with_explicit_params_configures_options()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFirebaseDatabase(
            projectId: "explicit-project",
            serviceAccountJson: "explicit-json",
            community: "test-community");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options.ProjectId).IsEqualTo("explicit-project");
        await Assert.That(options.ServiceAccountJson).IsEqualTo("explicit-json");
    }

    [Test]
    public async Task AddFirebaseDatabaseWithFile_configures_ServiceAccountPath()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddFirebaseDatabaseWithFile(
            projectId: "file-project",
            serviceAccountPath: "/path/to/service-account.json");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options.ProjectId).IsEqualTo("file-project");
        await Assert.That(options.ServiceAccountPath).IsEqualTo("/path/to/service-account.json");
    }

    [Test]
    public async Task AddFirebaseDatabase_returns_service_collection_for_chaining()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

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
