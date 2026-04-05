using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moq;
using OpenAI.Chat;
using TUnit.Core;

namespace OpenAiIntegration.Tests.ServiceCollectionExtensionsTests;

public class ServiceCollectionExtensions_Tests
{
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    [Test]
    public async Task AddOpenAiPredictor_with_blank_api_key_throws()
    {
        var services = CreateServices();

        await Assert.That(() => services.AddOpenAiPredictor(" "))
            .Throws<ArgumentException>()
            .WithParameterName("apiKey");
    }

    [Test]
    public async Task AddOpenAiPredictor_registers_expected_services()
    {
        var services = CreateServices();

        var result = services.AddOpenAiPredictor("test-api-key", "gpt-5-mini");

        await Assert.That(result).IsEqualTo(services);
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ChatClient)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Singleton));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(PredictorContext)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IPredictor<PredictorContext>)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ICostCalculationService)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IInstructionsTemplateProvider)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Singleton));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IMatchPromptReconstructionService)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ITokenUsageTracker)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Singleton));
        await Assert.That(services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IPredictionService)))
            .IsNotNull()
            .And.Member(descriptor => descriptor!.Lifetime, lifetime => lifetime.IsEqualTo(ServiceLifetime.Scoped));
    }

    [Test]
    public async Task AddOpenAiPredictor_resolves_predictor_context_and_file_provider()
    {
        var services = CreateServices();
        services.AddOpenAiPredictor("test-api-key", "gpt-5-mini");

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var predictorContext = scope.ServiceProvider.GetRequiredService<PredictorContext>();
        var fileProvider = provider.GetRequiredService<IFileProvider>();

        await Assert.That(predictorContext.Documents).HasCount().EqualTo(0);
        await Assert.That(fileProvider).IsNotNull();
    }

    [Test]
    public async Task AddOpenAiPredictor_with_configuration_reads_values_from_configuration()
    {
        var services = CreateServices();
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(config => config["OPENAI_API_KEY"]).Returns("config-key");
        configuration.SetupGet(config => config["OPENAI_MODEL"]).Returns("gpt-5-nano");

        services.AddOpenAiPredictor(configuration.Object);
        var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<ChatClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IPredictionService>()).IsNotNull();
    }

    [Test]
    public async Task AddOpenAiPredictor_with_configuration_uses_environment_fallback()
    {
        var services = CreateServices();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(config => config["OPENAI_API_KEY"]).Returns((string?)null);
        configuration.SetupGet(config => config["OPENAI_MODEL"]).Returns("gpt-5");

        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "env-key");

            services.AddOpenAiPredictor(configuration.Object);
            var provider = services.BuildServiceProvider();

            await Assert.That(provider.GetRequiredService<ChatClient>()).IsNotNull();
            await Assert.That(provider.GetRequiredService<IPredictionService>()).IsNotNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }
}
