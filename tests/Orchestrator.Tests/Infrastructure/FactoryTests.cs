using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.Snapshots;
using Orchestrator.Infrastructure.Factories;
using OpenAiIntegration;

namespace Orchestrator.Tests.Infrastructure;

[NotInParallel("ProcessState")]
public class FactoryTests
{
    private const string OpenAiApiKeyEnvVar = "OPENAI_API_KEY";
    private const string KicktippUsernameEnvVar = "KICKTIPP_USERNAME";
    private const string KicktippPasswordEnvVar = "KICKTIPP_PASSWORD";
    private const string FirebaseProjectIdEnvVar = "FIREBASE_PROJECT_ID";
    private const string FirebaseServiceAccountJsonEnvVar = "FIREBASE_SERVICE_ACCOUNT_JSON";

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();

    [Before(Test)]
    public void SaveEnvironmentVariables()
    {
        RememberEnvironmentVariable(OpenAiApiKeyEnvVar);
        RememberEnvironmentVariable(KicktippUsernameEnvVar);
        RememberEnvironmentVariable(KicktippPasswordEnvVar);
        RememberEnvironmentVariable(FirebaseProjectIdEnvVar);
        RememberEnvironmentVariable(FirebaseServiceAccountJsonEnvVar);
    }

    [After(Test)]
    public void RestoreEnvironmentVariables()
    {
        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Test]
    public async Task OpenAiServiceFactory_requires_api_key_and_caches_services()
    {
        Environment.SetEnvironmentVariable(OpenAiApiKeyEnvVar, null);
        var loggerFactory = CreateLoggerFactory();
        var missingKeyFactory = new OpenAiServiceFactory(loggerFactory);

        await Assert.That(() => missingKeyFactory.CreatePredictionService("gpt-5-nano"))
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(OpenAiApiKeyEnvVar, "test-openai-key");
        var sut = new OpenAiServiceFactory(loggerFactory);

        var first = sut.CreatePredictionService("gpt-5-nano");
        var second = sut.CreatePredictionService("gpt-5-nano");
        var differentModel = sut.CreatePredictionService("o4-mini");
        var tracker1 = sut.GetTokenUsageTracker();
        var tracker2 = sut.GetTokenUsageTracker();

        await Assert.That(first).IsTypeOf<PredictionService>();
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(differentModel).IsTypeOf<PredictionService>();
        await Assert.That(differentModel).IsNotSameReferenceAs(first);
        await Assert.That(tracker2).IsSameReferenceAs(tracker1);
    }

    [Test]
    public async Task KicktippClientFactory_requires_credentials_and_applies_http_client_defaults()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerFactory = CreateLoggerFactory();

        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, null);
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, null);
        var missingCredentialsFactory = new KicktippClientFactory(memoryCache, loggerFactory);

        await Assert.That(() => missingCredentialsFactory.CreateAuthenticatedHttpClient())
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(KicktippUsernameEnvVar, "user@example.com");
        Environment.SetEnvironmentVariable(KicktippPasswordEnvVar, "secret");

        var sut = new KicktippClientFactory(memoryCache, loggerFactory);
        using var httpClient = sut.CreateAuthenticatedHttpClient();

        await Assert.That(httpClient.BaseAddress).IsEqualTo(new Uri("https://www.kicktipp.de"));
        await Assert.That(httpClient.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(httpClient.DefaultRequestHeaders.UserAgent.ToString()).Contains("Mozilla/5.0");
        await Assert.That(sut.CreateClient()).IsSameReferenceAs(sut.CreateClient());
        await Assert.That(sut.CreateSnapshotClient()).IsTypeOf<SnapshotClient>();
    }

    [Test]
    public async Task ContextProviderFactory_creates_expected_provider_types_and_caches_community_rules_provider()
    {
        var kpiRepository = new Mock<IKpiRepository>();
        var firebaseFactory = new Mock<IFirebaseServiceFactory>();
        firebaseFactory.Setup(factory => factory.CreateKpiRepository()).Returns(kpiRepository.Object);

        var sut = new ContextProviderFactory(
            firebaseFactory.Object,
            new FakeLogger<FirebaseKpiContextProvider>());

        var kicktippClient = new Mock<IKicktippClient>();

        var kicktippContextProvider = sut.CreateKicktippContextProvider(kicktippClient.Object, "community-name", "community-context");
        var kpiContextProvider = sut.CreateKpiContextProvider();

        await Assert.That(kicktippContextProvider).IsTypeOf<KicktippContextProvider>();
        await Assert.That(kpiContextProvider).IsTypeOf<FirebaseKpiContextProvider>();
        await Assert.That(sut.CommunityRulesFileProvider).IsSameReferenceAs(sut.CommunityRulesFileProvider);
        firebaseFactory.Verify(factory => factory.CreateKpiRepository(), Times.Once);
    }

    [Test]
    public async Task FirebaseServiceFactory_requires_environment_variables()
    {
        var loggerFactory = CreateLoggerFactory();

        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, null);
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, "{}");
        var missingProjectFactory = new FirebaseServiceFactory(loggerFactory);

        await Assert.That(() => missingProjectFactory.FirestoreDb)
            .Throws<InvalidOperationException>();

        Environment.SetEnvironmentVariable(FirebaseProjectIdEnvVar, "firebase-project");
        Environment.SetEnvironmentVariable(FirebaseServiceAccountJsonEnvVar, null);
        var missingCredentialsFactory = new FirebaseServiceFactory(loggerFactory);

        await Assert.That(() => missingCredentialsFactory.FirestoreDb)
            .Throws<InvalidOperationException>();
    }

    private static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
    }

    private void RememberEnvironmentVariable(string name)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
        {
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }
    }
}
