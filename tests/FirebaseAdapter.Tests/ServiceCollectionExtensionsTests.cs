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
        services.AddFirebaseDatabase(CompetitionIds.Bundesliga2026_27, options =>
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
        services.AddFirebaseDatabase(CompetitionIds.Bundesliga2026_27, options =>
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
        var services = CreateServices();

        // Act
        AddDefaultFirebaseDatabase(services);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FirestoreDb));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Singleton));
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
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_separate_typed_and_audit_capabilities_only_for_Bundesliga_2026_27()
    {
        var bundesligaServices = CreateServices();
        AddDefaultFirebaseDatabase(bundesligaServices);

        foreach (var serviceType in new[]
        {
            typeof(IBundesligaTypedPredictionAuthorityRepository),
            typeof(ILegacyFirebasePredictionAuditCostReader),
            typeof(ITypedFirebasePredictionAuditCostReader)
        })
        {
            var descriptor = bundesligaServices.SingleOrDefault(entry => entry.ServiceType == serviceType);
            await Assert.That(descriptor).IsNotNull()
                .And.Member(entry => entry!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
        }

        var wm26Services = CreateServices();
        wm26Services.AddFirebaseDatabase(CompetitionIds.FifaWorldCup2026, options =>
        {
            options.ProjectId = "test-project";
            options.ServiceAccountJson = "{}";
        });

        await Assert.That(wm26Services.Any(entry =>
            entry.ServiceType == typeof(IBundesligaTypedPredictionAuthorityRepository))).IsFalse();
        await Assert.That(wm26Services.Any(entry =>
            entry.ServiceType == typeof(ILegacyFirebasePredictionAuditCostReader))).IsFalse();
        await Assert.That(wm26Services.Any(entry =>
            entry.ServiceType == typeof(ITypedFirebasePredictionAuditCostReader))).IsFalse();
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
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
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
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IDocumentPublicationRepository_as_scoped()
    {
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDocumentPublicationRepository));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddFirebaseDatabase_registers_IResolvedTypedContextPublicationBindingRepository_as_scoped()
    {
        var services = CreateServices();
        AddDefaultFirebaseDatabase(services);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IResolvedTypedContextPublicationBindingRepository));

        await Assert.That(descriptor).IsNotNull()
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
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
            .And.Member(d => d!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
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
        services.AddFirebaseDatabase(configuration, CompetitionIds.Bundesliga2026_27);

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
        var services = CreateServices();

        // Act
        services.AddFirebaseDatabase(
            projectId: "explicit-project",
            serviceAccountJson: "explicit-json",
            competition: CompetitionIds.Bundesliga2026_27);

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
        var services = CreateServices();

        // Act
        services.AddFirebaseDatabaseWithFile(
            projectId: "file-project",
            serviceAccountPath: "/path/to/service-account.json",
            competition: CompetitionIds.Bundesliga2026_27);

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
        var services = CreateServices();

        // Act
        var result = services.AddFirebaseDatabase(CompetitionIds.Bundesliga2026_27, options =>
        {
            options.ProjectId = "test";
            options.ServiceAccountJson = "{}";
        });

        // Assert
        await Assert.That(result).IsEqualTo(services);
    }

    [Test]
    public async Task AddFirebaseDatabase_without_configureOptions_registers_services()
    {
        // Arrange
        var services = CreateServices();
        
        // Pre-configure options so validation passes when FirestoreDb is resolved
        services.Configure<FirebaseOptions>(options =>
        {
            options.ProjectId = "pre-configured-project";
            options.ServiceAccountJson = "{}";
        });

        // Act - Call without configureOptions delegate
        services.AddFirebaseDatabase(CompetitionIds.Bundesliga2026_27);

        // Assert - Services should still be registered
        var predictionDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPredictionRepository));
        var kpiDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IKpiRepository));
        var contextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IContextRepository));

        await Assert.That(predictionDescriptor).IsNotNull();
        await Assert.That(kpiDescriptor).IsNotNull();
        await Assert.That(contextDescriptor).IsNotNull();
    }

    [Test]
    public async Task AddFirebaseDatabaseWithFile_sets_ServiceAccountPath_option()
    {
        // Arrange
        var services = CreateServices();
        var expectedPath = "/path/to/credentials.json";

        // Act
        services.AddFirebaseDatabaseWithFile(
            projectId: "file-test-project",
            serviceAccountPath: expectedPath,
            competition: CompetitionIds.Bundesliga2026_27);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;

        await Assert.That(options.ProjectId).IsEqualTo("file-test-project");
        await Assert.That(options.ServiceAccountPath).IsEqualTo(expectedPath);
        // ServiceAccountJson has a default value of string.Empty, not null
        await Assert.That(options.ServiceAccountJson).IsEqualTo(string.Empty);
    }
}
