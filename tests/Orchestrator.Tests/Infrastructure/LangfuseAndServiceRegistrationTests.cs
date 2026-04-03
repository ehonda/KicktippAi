using System.Diagnostics;
using System.Reflection;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Orchestrator.Services;
using OpenAiIntegration;

namespace Orchestrator.Tests.Infrastructure;

[NotInParallel("ProcessState")]
public class LangfuseAndServiceRegistrationTests
{
    private const string LangfusePublicKeyEnvVar = "LANGFUSE_PUBLIC_KEY";
    private const string LangfuseSecretKeyEnvVar = "LANGFUSE_SECRET_KEY";
    private const string LangfuseBaseUrlEnvVar = "LANGFUSE_BASE_URL";

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();

    [Before(Test)]
    public void SaveState()
    {
        RememberEnvironmentVariable(LangfusePublicKeyEnvVar);
        RememberEnvironmentVariable(LangfuseSecretKeyEnvVar);
        RememberEnvironmentVariable(LangfuseBaseUrlEnvVar);
        ResetLangfuseRegistration();
    }

    [After(Test)]
    public void RestoreState()
    {
        foreach (var (name, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        ResetLangfuseRegistration();
    }

    [Test]
    public async Task Processor_copies_langfuse_baggage_and_observation_metadata_without_overwriting_existing_tags()
    {
        using var root = new Activity("root").Start();
        LangfuseActivityPropagation.SetTraceMetadata(root, "match_id", "123");

        using var child = new Activity("child")
            .SetParentId(root.Id!)
            .AddBaggage("langfuse.environment", "production")
            .AddBaggage("langfuse.trace.tags", "")
            .AddBaggage("other.key", "ignored")
            .Start();
        child.SetTag("langfuse.environment", "already-set");

        var processor = new LangfuseBaggageSpanProcessor();

        processor.OnStart(child);

        await Assert.That(child.GetTagItem("langfuse.environment")?.ToString()).IsEqualTo("already-set");
        await Assert.That(child.GetTagItem("langfuse.observation.metadata.match_id")?.ToString()).IsEqualTo("123");
        await Assert.That(child.GetTagItem("other.key")).IsNull();
        await Assert.That(child.GetTagItem("langfuse.trace.tags")).IsNull();
    }

    [Test]
    public async Task Processor_sets_langfuse_tag_when_no_existing_tag_is_present()
    {
        using var activity = new Activity("child")
            .AddBaggage("langfuse.environment", "development")
            .Start();

        var processor = new LangfuseBaggageSpanProcessor();

        processor.OnStart(activity);

        await Assert.That(activity.GetTagItem("langfuse.environment")?.ToString()).IsEqualTo("development");
    }

    [Test]
    public async Task Processor_clears_trace_metadata_only_for_root_activities()
    {
        using var root = new Activity("root").Start();
        LangfuseActivityPropagation.SetTraceMetadata(root, "match_id", "123");

        using var child = new Activity("child").SetParentId(root.Id!).Start();
        var processor = new LangfuseBaggageSpanProcessor();

        processor.OnEnd(child);
        await Assert.That(LangfuseActivityPropagation.GetObservationMetadata(root).Any()).IsTrue();

        processor.OnEnd(root);
        await Assert.That(LangfuseActivityPropagation.GetObservationMetadata(root).Any()).IsFalse();
    }

    [Test]
    public async Task AddOrchestratorInfrastructure_registers_core_services()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorInfrastructure();

        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFirebaseServiceFactory) &&
            descriptor.ImplementationType == typeof(FirebaseServiceFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IKicktippClientFactory) &&
            descriptor.ImplementationType == typeof(KicktippClientFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IOpenAiServiceFactory) &&
            descriptor.ImplementationType == typeof(OpenAiServiceFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IContextProviderFactory) &&
            descriptor.ImplementationType == typeof(ContextProviderFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(MatchOutcomeCollectionService))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ILangfusePublicApiClient))).IsTrue();
    }

    [Test]
    public async Task AddLangfuseTracing_without_credentials_is_noop()
    {
        Environment.SetEnvironmentVariable(LangfusePublicKeyEnvVar, null);
        Environment.SetEnvironmentVariable(LangfuseSecretKeyEnvVar, null);
        var services = new ServiceCollection();

        services.AddLangfuseTracing();

        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IHostedService))).IsFalse();
    }

    [Test]
    public async Task AddLangfuseTracing_with_credentials_is_idempotent()
    {
        Environment.SetEnvironmentVariable(LangfusePublicKeyEnvVar, "public-key");
        Environment.SetEnvironmentVariable(LangfuseSecretKeyEnvVar, "secret-key");
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, "https://example.test");

        var services = new ServiceCollection();

        services.AddLangfuseTracing();
        var countAfterFirstRegistration = services.Count;

        services.AddLangfuseTracing();

        await Assert.That(services.Count).IsEqualTo(countAfterFirstRegistration);
        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IHostedService))).IsTrue();
    }

    [Test]
    public async Task BuildLangfuseOtlpHeaders_includes_auth_and_v4_ingestion_header()
    {
        var headers = ServiceRegistrationExtensions.BuildLangfuseOtlpHeaders("public-key", "secret-key");

        await Assert.That(headers).Contains("Authorization=Basic ");
        await Assert.That(headers).Contains("x-langfuse-ingestion-version=4");
    }

    [Test]
    public async Task AddCommandServices_register_keyed_file_providers_and_reuse_infrastructure()
    {
        var services = new ServiceCollection();

        services.AddUploadKpiCommandServices();
        services.AddUploadTransfersCommandServices();
        services.AddAllCommandServices();

        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFileProvider) &&
            Equals(descriptor.ServiceKey, ServiceRegistrationExtensions.KpiDocumentsFileProviderKey))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFileProvider) &&
            Equals(descriptor.ServiceKey, ServiceRegistrationExtensions.TransfersDocumentsFileProviderKey))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFirebaseServiceFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IContextProviderFactory))).IsTrue();
    }

    private void RememberEnvironmentVariable(string name)
    {
        if (!_originalEnvironmentVariables.ContainsKey(name))
        {
            _originalEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }
    }

    private static void ResetLangfuseRegistration()
    {
        typeof(ServiceRegistrationExtensions)
            .GetField("_langfuseTracingRegistered", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, false);
    }
}
