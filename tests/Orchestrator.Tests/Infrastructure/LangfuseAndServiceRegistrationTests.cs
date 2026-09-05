using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Orchestrator.Services;
using OpenAiIntegration;
using Polly.CircuitBreaker;
using Spectre.Console;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

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
        var originalCurrent = Activity.Current;
        Activity.Current = null;

        try
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
        finally
        {
            Activity.Current = originalCurrent;
        }
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
            descriptor.ServiceType == typeof(ICommunityKicktippCredentialLoader) &&
            descriptor.ImplementationType == typeof(CommunityKicktippCredentialLoader) &&
            descriptor.Lifetime == ServiceLifetime.Singleton)).IsTrue();
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
    public async Task Langfuse_client_registration_retries_safe_get_requests_when_rate_limited()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-1")
                .UsingGet())
            .InScenario("langfuse-trace-retry")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-1")
                .UsingGet())
            .InScenario("langfuse-trace-retry")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "id": "trace-1",
                      "name": "trace-name",
                      "metadata": {},
                      "output": { "homeGoals": 2, "awayGoals": 1 },
                      "scores": [],
                      "observations": [],
                      "tags": []
                    }
                    """));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();

        var trace = await client.GetTraceAsync("trace-1");

        await Assert.That(trace).IsNotNull();
        await Assert.That(trace!.Id).IsEqualTo("trace-1");
        await Assert.That(server.LogEntries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_uses_hosted_prompt_and_records_prompt_metadata()
    {
        var prompt = CreateTextPrompt(
            "kicktippai/wm26/predict-one-match",
            7,
            "Hosted WM prompt\n\n{{context_documents}}");
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                "kicktippai/wm26/predict-one-match",
                "latest",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompt);

        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            "kicktippai/wm26/predict-one-match",
            "latest",
            version: null);

        var (template, path) = provider.LoadMatchTemplate("gpt-5-nano", includeJustification: false);
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(template).Contains("Hosted WM prompt");
        await Assert.That(path).IsEqualTo("langfuse://prompts/kicktippai%2Fwm26%2Fpredict-one-match/versions/7?label=latest");
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.RequestedSource).IsEqualTo("langfuse");
        await Assert.That(metadata.ActualSource).IsEqualTo("langfuse");
        await Assert.That(metadata!.LangfusePromptName).IsEqualTo("kicktippai/wm26/predict-one-match");
        await Assert.That(metadata.LangfusePromptLabel).IsEqualTo("latest");
        await Assert.That(metadata.LangfusePromptVersion).IsEqualTo(7);
        await Assert.That(metadata.IsFallback).IsFalse();
        await Assert.That(metadata.ContentSha256).IsEqualTo(PromptTemplateContentHash.ComputeSha256(template));
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_falls_back_to_local_wm_match_prompt_when_fetch_returns_missing()
    {
        var warnings = new List<string>();
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                "kicktippai/wm26/predict-one-match",
                "latest",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LangfusePrompt?)null);

        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            "kicktippai/wm26/predict-one-match",
            "latest",
            version: null,
            promptKind: LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: "wm26",
            fallbackWarning: warnings.Add);

        var (template, path) = provider.LoadMatchTemplate("gpt-5-nano", includeJustification: false);
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(path.Replace('\\', '/')).Contains("prompts/wm26/match.md");
        await Assert.That(template).Contains("FIFA World Cup 2026");
        await Assert.That(template).Contains("{{context_documents}}");
        await Assert.That(warnings).HasCount().EqualTo(1);
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.RequestedSource).IsEqualTo("langfuse");
        await Assert.That(metadata.ActualSource).IsEqualTo("local");
        await Assert.That(metadata!.LangfusePromptName).IsEqualTo("kicktippai/wm26/predict-one-match");
        await Assert.That(metadata.LangfusePromptLabel).IsEqualTo("latest");
        await Assert.That(metadata.LangfusePromptVersion).IsNull();
        await Assert.That(metadata.IsFallback).IsTrue();
        await Assert.That(metadata.PromptPath.Replace('\\', '/')).Contains("prompts/wm26/match.md");
        await Assert.That(metadata.ContentSha256).IsEqualTo(PromptTemplateContentHash.ComputeSha256(template));
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_falls_back_to_local_wm_bonus_prompt_when_fetch_fails()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                "kicktippai/wm26/predict-bonus",
                "latest",
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network unavailable"));

        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            "kicktippai/wm26/predict-bonus",
            "latest",
            version: null,
            promptKind: LangfusePromptKind.Bonus,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: "wm26");

        var (template, path) = provider.LoadBonusTemplate("gpt-5-nano");
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(path.Replace('\\', '/')).Contains("prompts/wm26/bonus.md");
        await Assert.That(template).Contains("FIFA World Cup 2026 bonus question");
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.IsFallback).IsTrue();
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_rejects_world_cup_hosted_match_justification()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            "kicktippai/wm26/predict-one-match",
            "latest",
            version: null,
            preloadedPrompt: CreateTextPrompt("kicktippai/wm26/predict-one-match", 1, "prompt"));

        await Assert.That(() => provider.LoadMatchTemplate("gpt-5-nano", includeJustification: true))
            .Throws<NotSupportedException>()
            .WithMessageContaining("WM 2026");
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_uses_one_bundesliga_hosted_match_prompt_with_or_without_justification()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "staging",
            version: null,
            preloadedPrompt: CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                1,
                "prompt",
                ["staging"]));

        var withoutJustification = provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false);
        var withJustification = provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: true);

        await Assert.That(withJustification).IsEqualTo(withoutJustification);
    }

    [Test]
    public async Task Bundesliga_prediction_service_pins_default_production_prompt_version()
    {
        await AssertBundesligaPredictionServiceLookupAsync(
            promptLabel: null,
            explicitVersion: null,
            expectedLabel: CompetitionResolver.DefaultBundesligaPromptLabel,
            expectedVersion: CompetitionResolver.BundesligaMatchPromptVersion);
    }

    [Test]
    public async Task Bundesliga_prediction_service_preserves_staging_label_without_implicit_version()
    {
        await AssertBundesligaPredictionServiceLookupAsync(
            promptLabel: "staging",
            explicitVersion: null,
            expectedLabel: "staging",
            expectedVersion: null);
    }

    [Test]
    public async Task Bundesliga_prediction_service_explicit_version_takes_precedence_over_staging_label()
    {
        await AssertBundesligaPredictionServiceLookupAsync(
            promptLabel: "staging",
            explicitVersion: 7,
            expectedLabel: "staging",
            expectedVersion: 7);
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_rejects_a_version_without_the_required_promotion_label()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                CompetitionResolver.BundesligaMatchPromptVersion,
                "prompt",
                ["staging"]));
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            CompetitionResolver.BundesligaMatchPromptVersion,
            promptKind: LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: CompetitionResolver.BundesligaFallbackPromptModel);

        await Assert.That(() => provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false))
            .Throws<InvalidDataException>()
            .WithMessageContaining("required label 'production'");
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_rejects_a_different_immutable_version()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                CompetitionResolver.BundesligaMatchPromptVersion + 1,
                "prompt",
                ["production"]));
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            CompetitionResolver.BundesligaMatchPromptVersion);

        await Assert.That(() => provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false))
            .Throws<InvalidDataException>()
            .WithMessageContaining($"does not match required version {CompetitionResolver.BundesligaMatchPromptVersion}");
    }

    [Test]
    public async Task Langfuse_text_prompt_provider_rejects_a_different_prompt_name()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaBonusPromptName,
                CompetitionResolver.BundesligaMatchPromptVersion,
                "prompt",
                ["production"]));
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            CompetitionResolver.BundesligaMatchPromptVersion);

        await Assert.That(() => provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false))
            .Throws<InvalidDataException>()
            .WithMessageContaining("does not match required name");
    }

    [Test]
    public async Task Required_hosted_prompt_failure_prevents_prediction_service_construction()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("hosted prompt unavailable"));
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);

        await Assert.That(() => PredictionServiceCommandSupport.CreatePredictionService(
                openAiFactory.Object,
                langfuseClient.Object,
                new Mock<IAnsiConsole>().Object,
                "gpt-5.6-luna",
                CompetitionIds.Bundesliga2026_27,
                "ehonda-dev-buli-2627",
                "ehonda-dev-buli-2627",
                CompetitionResolver.LangfusePromptSource,
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                "none",
                10_000,
                bonusPrompt: false,
                requireHostedPrompt: true))
            .Throws<FileNotFoundException>()
            .WithMessageContaining("hosted prompt unavailable");
        openAiFactory.Verify(
            factory => factory.CreatePredictionService(
                It.IsAny<string>(),
                It.IsAny<PredictionServiceOptions>(),
                It.IsAny<IInstructionsTemplateProvider>()),
            Times.Never);
    }

    [Test]
    public async Task Required_hosted_prompt_binding_drift_prevents_prediction_service_construction()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                CompetitionResolver.BundesligaMatchPromptVersion,
                "prompt",
                ["staging"]));
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);

        await Assert.That(() => PredictionServiceCommandSupport.CreatePredictionService(
                openAiFactory.Object,
                langfuseClient.Object,
                new Mock<IAnsiConsole>().Object,
                "gpt-5.6-luna",
                CompetitionIds.Bundesliga2026_27,
                "ehonda-dev-buli-2627",
                "ehonda-dev-buli-2627",
                CompetitionResolver.LangfusePromptSource,
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                "none",
                10_000,
                bonusPrompt: false,
                requireHostedPrompt: true))
            .Throws<InvalidDataException>()
            .WithMessageContaining("required label 'production'");
        openAiFactory.Verify(
            factory => factory.CreatePredictionService(
                It.IsAny<string>(),
                It.IsAny<PredictionServiceOptions>(),
                It.IsAny<IInstructionsTemplateProvider>()),
            Times.Never);
    }

    [Test]
    public async Task Required_hosted_prompt_is_verified_before_prediction_service_construction()
    {
        var promptFetched = false;
        var factoryObservedFetch = false;
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .Callback(() => promptFetched = true)
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                CompetitionResolver.BundesligaMatchPromptVersion,
                "prompt",
                ["production"]));
        var predictionService = new Mock<IPredictionService>();
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
        openAiFactory
            .Setup(factory => factory.CreatePredictionService(
                "gpt-5.6-luna",
                It.IsAny<PredictionServiceOptions>(),
                It.IsAny<IInstructionsTemplateProvider>()))
            .Callback(() => factoryObservedFetch = promptFetched)
            .Returns(predictionService.Object);

        var result = PredictionServiceCommandSupport.CreatePredictionService(
            openAiFactory.Object,
            langfuseClient.Object,
            new Mock<IAnsiConsole>().Object,
            "gpt-5.6-luna",
            CompetitionIds.Bundesliga2026_27,
            "ehonda-dev-buli-2627",
            "ehonda-dev-buli-2627",
            CompetitionResolver.LangfusePromptSource,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            CompetitionResolver.BundesligaMatchPromptVersion,
            "none",
            10_000,
            bonusPrompt: false,
            requireHostedPrompt: true);

        await Assert.That(result).IsSameReferenceAs(predictionService.Object);
        await Assert.That(factoryObservedFetch).IsTrue();
    }

    [Test]
    public async Task Near_match_cl_prompt_cannot_fall_through_to_an_ordinary_prediction_service()
    {
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);

        await Assert.That(() => PredictionServiceCommandSupport.CreatePredictionService(
                openAiFactory.Object,
                new Mock<ILangfusePublicApiClient>().Object,
                new Mock<IAnsiConsole>().Object,
                SchadensfresseChampionsLeagueBonusProfile.Model,
                SchadensfresseChampionsLeagueBonusProfile.Competition,
                SchadensfresseChampionsLeagueBonusProfile.Community,
                SchadensfresseChampionsLeagueBonusProfile.Community,
                "langfuse",
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                SchadensfresseChampionsLeagueBonusProfile.PromptLabel,
                SchadensfresseChampionsLeagueBonusProfile.PromptVersion,
                SchadensfresseChampionsLeagueBonusProfile.ReasoningEffort,
                9_999,
                bonusPrompt: true,
                bonusProfile: SchadensfresseChampionsLeagueBonusProfile.ProfileId,
                bonusContextDocumentBudget: 0,
                bonusContextTokenBudget: 0,
                bonusDeadlineAtOrBefore: SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("complete exact frozen invocation tuple");
        openAiFactory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Cl_profile_with_local_prompt_source_fails_before_local_service_construction()
    {
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);

        await Assert.That(() => PredictionServiceCommandSupport.CreatePredictionService(
                openAiFactory.Object,
                langfuseClient: null,
                new Mock<IAnsiConsole>().Object,
                SchadensfresseChampionsLeagueBonusProfile.Model,
                SchadensfresseChampionsLeagueBonusProfile.Competition,
                SchadensfresseChampionsLeagueBonusProfile.Community,
                SchadensfresseChampionsLeagueBonusProfile.Community,
                "local",
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                SchadensfresseChampionsLeagueBonusProfile.PromptLabel,
                SchadensfresseChampionsLeagueBonusProfile.PromptVersion,
                SchadensfresseChampionsLeagueBonusProfile.ReasoningEffort,
                SchadensfresseChampionsLeagueBonusProfile.MaxOutputTokens,
                bonusPrompt: true,
                bonusProfile: SchadensfresseChampionsLeagueBonusProfile.ProfileId,
                bonusContextDocumentBudget: 0,
                bonusContextTokenBudget: 0,
                bonusDeadlineAtOrBefore: SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("complete exact frozen invocation tuple");
        openAiFactory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Cl_deadline_alone_without_profile_or_target_cannot_construct_an_ordinary_service()
    {
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);

        await Assert.That(() => PredictionServiceCommandSupport.CreatePredictionService(
                openAiFactory.Object,
                new Mock<ILangfusePublicApiClient>().Object,
                new Mock<IAnsiConsole>().Object,
                "gpt-5.6-luna",
                SchadensfresseChampionsLeagueBonusProfile.Competition,
                "pes-squad",
                "pes-squad",
                "langfuse",
                CompetitionResolver.BundesligaBonusPromptName,
                "production",
                CompetitionResolver.BundesligaBonusPromptVersion,
                "none",
                10_000,
                bonusPrompt: true,
                bonusProfile: null,
                bonusContextDocumentBudget: 20,
                bonusContextTokenBudget: 32_000,
                bonusDeadlineAtOrBefore: SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("complete exact frozen invocation tuple");
        openAiFactory.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Langfuse_match_fallback_uses_justification_mirror_when_requested()
    {
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((LangfusePrompt?)null);

        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            version: null,
            promptKind: LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: CompetitionResolver.BundesligaFallbackPromptModel);

        var (template, path) = provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: true);

        await Assert.That(path.Replace('\\', '/')).Contains("prompts/bundesliga-2026-27/match.justification.md");
        await Assert.That(template).Contains("Bundesliga 2026/27");
        await Assert.That(template).Contains("{{context_documents}}");
    }

    [Test]
    public async Task Ordinary_versioned_hosted_route_retains_the_visible_outage_fallback()
    {
        var warnings = new List<string>();
        var langfuseClient = new Mock<ILangfusePublicApiClient>();
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                "production",
                CompetitionResolver.BundesligaMatchPromptVersion,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network unavailable"));
        var provider = new LangfuseTextPromptTemplateProvider(
            langfuseClient.Object,
            CompetitionResolver.BundesligaMatchPromptName,
            "production",
            CompetitionResolver.BundesligaMatchPromptVersion,
            promptKind: LangfusePromptKind.Match,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: CompetitionResolver.BundesligaFallbackPromptModel,
            fallbackWarning: warnings.Add);

        var (_, path) = provider.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false);
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(path.Replace('\\', '/')).Contains("prompts/bundesliga-2026-27/match.md");
        await Assert.That(warnings).Count().IsEqualTo(1);
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ActualSource).IsEqualTo(CompetitionResolver.LocalPromptSource);
        await Assert.That(metadata.IsFallback).IsTrue();
    }

    [Test]
    [Arguments(500)]
    [Arguments(502)]
    [Arguments(599)]
    public async Task Dedicated_cl_mirror_accepts_only_public_api_server_outages(int statusCode)
    {
        var client = new Mock<ILangfusePublicApiClient>();
        client.Setup(value => value.GetPromptAsync(
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                "production",
                1,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LangfusePublicApiException((HttpStatusCode)statusCode, "v2/prompts/test", "outage"));
        var provider = CreateDedicatedClProvider(client.Object);

        provider.LoadBonusTemplate("gpt-5.6-sol");
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ActualSource).IsEqualTo("dedicated-cl-mirror");
        await Assert.That(metadata.LangfusePromptVersion).IsEqualTo(1);
        await Assert.That(metadata.IsFallback).IsTrue();
    }

    [Test]
    [Arguments(401)]
    [Arguments(403)]
    [Arguments(404)]
    [Arguments(429)]
    public async Task Dedicated_cl_mirror_rejects_fatal_public_api_statuses(int statusCode)
    {
        var client = new Mock<ILangfusePublicApiClient>();
        client.Setup(value => value.GetPromptAsync(
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                "production",
                1,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LangfusePublicApiException((HttpStatusCode)statusCode, "v2/prompts/test", "fatal"));
        var provider = CreateDedicatedClProvider(client.Object);

        await Assert.That(() => provider.LoadBonusTemplate("gpt-5.6-sol"))
            .Throws<LangfusePublicApiException>();
    }

    [Test]
    public async Task Dedicated_cl_mirror_accepts_http_client_timeout_cancellation()
    {
        var client = new Mock<ILangfusePublicApiClient>();
        client.Setup(value => value.GetPromptAsync(
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                "production",
                1,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("HttpClient timeout", new TimeoutException("request timeout")));
        var provider = CreateDedicatedClProvider(client.Object);

        var (_, path) = provider.LoadBonusTemplate("gpt-5.6-sol");
        await Assert.That(path.Replace('\\', '/')).Contains("prompts/bundesliga-2026-27/champions-league/bonus.md");
    }

    [Test]
    public async Task Dedicated_cl_mirror_does_not_convert_requested_cancellation_into_fallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new Mock<ILangfusePublicApiClient>();
        client.Setup(value => value.GetPromptAsync(
                SchadensfresseChampionsLeagueBonusProfile.PromptName,
                "production",
                1,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var provider = CreateDedicatedClProvider(client.Object);

        await Assert.That(() => provider.LoadBonusTemplate("gpt-5.6-sol"))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Local_prompt_provider_pins_the_competition_mirror_and_records_its_hash()
    {
        var provider = new LocalPromptTemplateProvider(
            new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            CompetitionResolver.BundesligaFallbackPromptModel);

        var (template, path) = provider.LoadMatchTemplate("some-runtime-model", includeJustification: false);
        var metadata = provider.GetPromptTemplateTelemetryMetadata();

        await Assert.That(path.Replace('\\', '/')).Contains("prompts/bundesliga-2026-27/match.md");
        await Assert.That(template).Contains("Bundesliga 2026/27");
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.RequestedSource).IsEqualTo("local");
        await Assert.That(metadata.ActualSource).IsEqualTo("local");
        await Assert.That(metadata.LangfusePromptName).IsNull();
        await Assert.That(metadata.LangfusePromptVersion).IsNull();
        await Assert.That(metadata.IsFallback).IsFalse();
        await Assert.That(metadata.ContentSha256).IsEqualTo(PromptTemplateContentHash.ComputeSha256(template));
    }

    [Test]
    public async Task OpenAi_http_client_does_not_retry_completion_posts_in_the_resilience_pipeline()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/v1/responses")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":{"code":"resource_unavailable","message":"capacity unavailable"}}"""));

        var services = new ServiceCollection();
        services.AddOrchestratorInfrastructure();

        using var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(ServiceRegistrationExtensions.OpenAiHttpClientName);
        httpClient.BaseAddress = new Uri(server.Urls[0]);

        using var response = await httpClient.PostAsync("v1/responses", new StringContent("{}"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(server.LogEntries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task OpenAi_http_client_circuit_breaker_does_not_open_for_repeated_rate_limits()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/v1/responses")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"error":{"code":"resource_unavailable","message":"capacity unavailable"}}"""));

        var services = new ServiceCollection();
        services.AddOrchestratorInfrastructure();

        using var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(ServiceRegistrationExtensions.OpenAiHttpClientName);
        httpClient.BaseAddress = new Uri(server.Urls[0]);
        BrokenCircuitException? brokenCircuit = null;

        for (var attempt = 0; attempt < 120; attempt += 1)
        {
            try
            {
                using var response = await httpClient.PostAsync("v1/responses", new StringContent("{}"));
                await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
            }
            catch (BrokenCircuitException ex)
            {
                brokenCircuit = ex;
                break;
            }
        }

        await Assert.That(brokenCircuit).IsNull();
    }

    [Test]
    public async Task Langfuse_client_logs_retryable_responses_inside_the_resilience_pipeline()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-1")
                .UsingGet())
            .InScenario("langfuse-trace-retry-logging")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-1")
                .UsingGet())
            .InScenario("langfuse-trace-retry-logging")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "id": "trace-1",
                      "name": "trace-name",
                      "metadata": {},
                      "output": { "homeGoals": 2, "awayGoals": 1 },
                      "scores": [],
                      "observations": [],
                      "tags": []
                    }
                    """));

        var retryLogger = new FakeLogger<LangfuseRetryLoggingHandler>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<LangfuseRetryLoggingHandler>>(_ => retryLogger);
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();

        var trace = await client.GetTraceAsync("trace-1");

        await Assert.That(trace).IsNotNull();
        await Assert.That(retryLogger.Collector.GetSnapshot().Any(record =>
            record.Message.Contains("Langfuse request GET /api/public/traces/trace-1 returned 429"))).IsTrue();
    }

    [Test]
    public async Task Langfuse_client_retries_rate_limited_score_posts_in_the_resilience_pipeline()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/scores")
                .UsingPost())
            .InScenario("langfuse-score-retry")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath("/api/public/scores")
                .UsingPost())
            .InScenario("langfuse-score-retry")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"id\":\"score-1\"}"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();

        var score = await client.CreateScoreAsync(new LangfuseCreateScoreRequest("kicktipp_points", 4, TraceId: "trace-1"));

        await Assert.That(score.Id).IsEqualTo("score-1");
        await Assert.That(server.LogEntries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Langfuse_client_circuit_breaker_does_not_open_for_repeated_rate_limits()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-rate-limited")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();
        BrokenCircuitException? brokenCircuit = null;

        for (var attempt = 0; attempt < 120; attempt += 1)
        {
            try
            {
                await client.GetTraceAsync("trace-rate-limited");
            }
            catch (BrokenCircuitException ex)
            {
                brokenCircuit = ex;
                break;
            }
            catch (LangfusePublicApiException)
            {
                // Expected while the endpoint keeps returning 429s.
            }
        }

        await Assert.That(brokenCircuit).IsNull();
    }

    [Test]
    public async Task Langfuse_client_retries_rate_limited_dataset_item_posts_in_the_resilience_pipeline()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/dataset-items")
                .UsingPost())
            .InScenario("langfuse-dataset-item-retry")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath("/api/public/dataset-items")
                .UsingPost())
            .InScenario("langfuse-dataset-item-retry")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "id": "dataset-item-1",
                      "datasetId": "dataset-1",
                      "datasetName": "dataset-name",
                      "input": {},
                      "expectedOutput": {},
                      "metadata": {},
                      "status": null
                    }
                    """));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();

        var datasetItem = await client.CreateDatasetItemAsync(new LangfuseCreateDatasetItemRequest("dataset-item-1", "dataset-name"));

        await Assert.That(datasetItem.Id).IsEqualTo("dataset-item-1");
        await Assert.That(server.LogEntries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Langfuse_client_retries_rate_limited_dataset_run_item_posts_in_the_resilience_pipeline()
    {
        using var server = WireMockServer.Start();
        Environment.SetEnvironmentVariable(LangfuseBaseUrlEnvVar, server.Urls[0]);

        server
            .Given(Request.Create()
                .WithPath("/api/public/dataset-run-items")
                .UsingPost())
            .InScenario("langfuse-dataset-run-item-retry")
            .WillSetStateTo("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "0")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath("/api/public/dataset-run-items")
                .UsingPost())
            .InScenario("langfuse-dataset-run-item-retry")
            .WhenStateIs("retried")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "id": "dataset-run-item-1",
                      "datasetRunId": "dataset-run-1",
                      "datasetRunName": "run-name",
                      "datasetItemId": "dataset-item-1",
                      "traceId": "trace-1",
                      "observationId": null,
                      "createdAt": "2026-04-07T00:00:00Z",
                      "updatedAt": "2026-04-07T00:00:00Z"
                    }
                    """));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLangfusePublicApiClient();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILangfusePublicApiClient>();

        var datasetRunItem = await client.CreateDatasetRunItemAsync(new LangfuseCreateDatasetRunItemRequest("run-name", "dataset-item-1", "trace-1"));

        await Assert.That(datasetRunItem.Id).IsEqualTo("dataset-run-item-1");
        await Assert.That(server.LogEntries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Langfuse_client_errors_include_retry_after_metadata()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/api/public/traces/trace-1")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Retry-After", "17")
                .WithBody("{\"message\":\"rate limit exceeded\"}"));

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{server.Urls[0]}/api/public/")
        };

        var client = new LangfusePublicApiClient(httpClient, new FakeLogger<LangfusePublicApiClient>());

        LangfusePublicApiException? exception = null;

        try
        {
            await client.GetTraceAsync("trace-1");
        }
        catch (LangfusePublicApiException ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.RetryAfterHeaderValue).IsEqualTo("17");
        await Assert.That(exception.RetryAfterDelay).IsEqualTo(TimeSpan.FromSeconds(17));
        await Assert.That(exception.Message).Contains("Retry-After: 17.");
    }

    [Test]
    public async Task Langfuse_client_fetches_an_immutable_prompt_version_without_the_label_query_parameter()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/api/public/v2/prompts/kicktippai/predict-one-match-o3-poc")
                .WithParam("version", "7")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "name": "kicktippai/predict-one-match-o3-poc",
                      "version": 7,
                      "type": "text",
                      "prompt": "Hello {{context_documents}}",
                      "labels": ["poc"],
                      "tags": ["kicktippai"],
                      "config": {}
                    }
                    """));

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{server.Urls[0]}/api/public/")
        };

        var client = new LangfusePublicApiClient(httpClient, new FakeLogger<LangfusePublicApiClient>());

        var prompt = await client.GetPromptAsync("kicktippai/predict-one-match-o3-poc", "poc", 7);

        await Assert.That(prompt).IsNotNull();
        await Assert.That(prompt!.Name).IsEqualTo("kicktippai/predict-one-match-o3-poc");
        await Assert.That(prompt.Version).IsEqualTo(7);
        await Assert.That(prompt.GetTextPrompt()).IsEqualTo("Hello {{context_documents}}");
        var requestUrl = server.LogEntries.Single().RequestMessage?.Url ?? string.Empty;
        await Assert.That(requestUrl).Contains("version=7");
        await Assert.That(requestUrl).DoesNotContain("label=");
    }

    [Test]
    public async Task Langfuse_client_keeps_a_label_only_prompt_lookup_label_resolved()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/api/public/v2/prompts/kicktippai/predict-one-match-o3-poc")
                .WithParam("label", "staging")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(
                    """
                    {
                      "name": "kicktippai/predict-one-match-o3-poc",
                      "version": 8,
                      "type": "text",
                      "prompt": "Hello {{context_documents}}",
                      "labels": ["staging"],
                      "tags": ["kicktippai"],
                      "config": {}
                    }
                    """));

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{server.Urls[0]}/api/public/")
        };

        var client = new LangfusePublicApiClient(httpClient, new FakeLogger<LangfusePublicApiClient>());

        var prompt = await client.GetPromptAsync("kicktippai/predict-one-match-o3-poc", "staging");

        await Assert.That(prompt).IsNotNull();
        var requestUrl = server.LogEntries.Single().RequestMessage?.Url ?? string.Empty;
        await Assert.That(requestUrl).Contains("label=staging");
        await Assert.That(requestUrl).DoesNotContain("version=");
    }

    [Test]
    public async Task Langfuse_text_prompt_template_provider_returns_match_template_and_prompt_path()
    {
        var prompt = new LangfusePrompt(
            "kicktippai/predict-one-match-o3-poc",
            7,
            "text",
            System.Text.Json.JsonSerializer.SerializeToElement("Hello {{context_documents}}"),
            ["poc"],
            ["kicktippai"],
            System.Text.Json.JsonSerializer.SerializeToElement(new { }));
        var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        client
            .Setup(langfuseClient => langfuseClient.GetPromptAsync(
                "kicktippai/predict-one-match-o3-poc",
                "poc",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompt);
        var provider = new LangfuseTextPromptTemplateProvider(
            client.Object,
            "kicktippai/predict-one-match-o3-poc",
            "poc",
            null);

        var (template, path) = provider.LoadMatchTemplate("gpt-5.5", includeJustification: false);

        await Assert.That(template).IsEqualTo("Hello {{context_documents}}");
        await Assert.That(path).Contains("langfuse://prompts/");
        await Assert.That(path).Contains("/versions/7");
        client.VerifyAll();
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
    public async Task AddAllCommandServices_registers_shared_infrastructure()
    {
        var services = new ServiceCollection();

        services.AddContextHygieneInventoryCommandServices();
        services.AddAllCommandServices();

        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFirebaseServiceFactory))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IContextProviderFactory))).IsTrue();
    }

    [Test]
    public async Task AddAllCommandServices_uses_supplied_minimum_log_level()
    {
        var services = new ServiceCollection();

        services.AddAllCommandServices(LogLevel.Warning);

        using var provider = services.BuildServiceProvider();
        var loggerFilterOptions = provider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

        await Assert.That(loggerFilterOptions.MinLevel).IsEqualTo(LogLevel.Warning);
    }

    private static LangfuseTextPromptTemplateProvider CreateDedicatedClProvider(ILangfusePublicApiClient client) =>
        new(
            client,
            SchadensfresseChampionsLeagueBonusProfile.PromptName,
            "production",
            1,
            promptKind: LangfusePromptKind.Bonus,
            fallbackTemplateProvider: new InstructionsTemplateProvider(PromptsFileProvider.Create()),
            fallbackModel: "bundesliga-2026-27/champions-league",
            expectedContentSha256: SchadensfresseChampionsLeagueBonusProfile.PromptNormalizedSha256,
            availabilityOnlyFallback: true,
            fallbackSource: "dedicated-cl-mirror");

    private static LangfusePrompt CreateTextPrompt(
        string name,
        int version,
        string text,
        IReadOnlyList<string>? labels = null)
    {
        using var promptDocument = JsonDocument.Parse(JsonSerializer.Serialize(text));
        using var configDocument = JsonDocument.Parse("{}");

        return new LangfusePrompt(
            name,
            version,
            "text",
            promptDocument.RootElement.Clone(),
            Labels: labels ?? ["latest"],
            Tags: [],
            configDocument.RootElement.Clone());
    }

    private static async Task AssertBundesligaPredictionServiceLookupAsync(
        string? promptLabel,
        int? explicitVersion,
        string expectedLabel,
        int? expectedVersion)
    {
        var mirrorProvider = new InstructionsTemplateProvider(PromptsFileProvider.Create());
        var (mirror, _) = mirrorProvider.LoadMatchTemplate(
            CompetitionResolver.BundesligaFallbackPromptModel,
            includeJustification: false);
        var langfuseClient = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        langfuseClient
            .Setup(client => client.GetPromptAsync(
                CompetitionResolver.BundesligaMatchPromptName,
                expectedLabel,
                expectedVersion,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTextPrompt(
                CompetitionResolver.BundesligaMatchPromptName,
                expectedVersion ?? 99,
                mirror,
                [expectedLabel]));

        IInstructionsTemplateProvider? capturedProvider = null;
        var predictionService = new Mock<IPredictionService>();
        var openAiFactory = new Mock<IOpenAiServiceFactory>(MockBehavior.Strict);
        openAiFactory
            .Setup(factory => factory.CreatePredictionService(
                "gpt-5.6-luna",
                It.IsAny<PredictionServiceOptions>(),
                It.IsAny<IInstructionsTemplateProvider>()))
            .Callback<string, PredictionServiceOptions, IInstructionsTemplateProvider>(
                (_, _, provider) => capturedProvider = provider)
            .Returns(predictionService.Object);

        PredictionServiceCommandSupport.CreatePredictionService(
            openAiFactory.Object,
            langfuseClient.Object,
            new Mock<IAnsiConsole>().Object,
            "gpt-5.6-luna",
            CompetitionIds.Bundesliga2026_27,
            "ehonda-dev-buli-2627",
            "ehonda-dev-buli-2627",
            promptSource: null,
            langfusePromptName: null,
            langfusePromptLabel: promptLabel,
            langfusePromptVersion: explicitVersion,
            reasoningEffort: "none",
            maxOutputTokenCount: 10_000,
            bonusPrompt: false);

        await Assert.That(capturedProvider).IsNotNull();
        capturedProvider!.LoadMatchTemplate("gpt-5.6-luna", includeJustification: false);
        langfuseClient.VerifyAll();
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
