using EHonda.KicktippAi.Core;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using Moq;
using OpenAI.Responses;
using TestUtilities;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

[NotInParallel("Telemetry")]
public sealed class PredictionService_Observed_Tests : PredictionServiceTests_Base
{
    private const string PromptName = "kicktippai/bundesliga-2026-27/predict-one-match";
    private const string Template = "Observed template\n";

    [Test]
    public async Task Observed_match_returns_same_invocation_prompt_tier_usage_and_exact_cost()
    {
        var model = Model();
        var requirement = Requirement(model);
        var prompt = Hosted(requirement);
        var provider = new Mock<IObservedInstructionsTemplateProvider>(MockBehavior.Strict);
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                requirement, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prompt);
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "flex"))
            .Returns(0.0123m);
        var service = CreateObservedService(model, provider.Object, cost.Object);
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        var result = await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement,
            telemetryMetadata: new PredictionTelemetryMetadata(HomeTeam: "registered-home"));

        await Assert.That(result.Prediction.HomeGoals).IsEqualTo(2);
        await Assert.That(result.Evidence.Prompt).IsSameReferenceAs(prompt);
        await Assert.That(result.Evidence.PromptRequirement).IsSameReferenceAs(requirement);
        await Assert.That(result.Evidence.ServiceTier.RequestedTier).IsEqualTo("flex");
        await Assert.That(result.Evidence.ServiceTier.FinalTier).IsEqualTo("flex");
        await Assert.That(result.Evidence.Usage.InputTokens).IsEqualTo(1000);
        await Assert.That(result.Evidence.Usage.CostUsd).IsEqualTo(0.0123m);
        var activity = activities.Single(value => value.OperationName == "predict-match");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(activity.GetTagItem("langfuse.observation.prompt.name"))
            .IsEqualTo(PromptName);
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam"))
            .IsEqualTo("registered-home");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")).IsNotNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.output"))
            .IsEqualTo("""{"home": 2, "away": 1}""");
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")?.ToString())
            .Contains("0.0123");
        provider.VerifyAll();
    }

    [Test]
    public async Task Missing_exact_cost_fails_without_a_partial_result()
    {
        var model = Model();
        var requirement = Requirement(model);
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                requirement, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(requirement));
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "flex"))
            .Returns((decimal?)null);
        var service = CreateObservedService(model, provider.Object, cost.Object);
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        async Task Act() => await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement);
        await Assert.That(Act).Throws<ObservedPredictionException>();
        var activity = activities.Single(value => value.OperationName == "predict-match");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("observed-prediction-failed");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.output")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")).IsNull();
    }

    [Test]
    public async Task Missing_final_tier_fails_without_a_partial_result()
    {
        var model = Model();
        var requirement = Requirement(model);
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                requirement, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(requirement));
        var client = new Mock<ResponsesClient>("test-api-key");
        client.Setup(value => value.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponseClientResult(
                """{"home":2,"away":1}""",
                OpenAITestHelpers.CreateChatTokenUsage(100, 20), serviceTier: null));
        var service = CreateObservedService(
            model, provider.Object, CreateMockCostCalculationService().Object, client.Object);

        async Task Act() => await service.PredictObservedMatchAsync(
            CreateTestMatch(), [], requirement);
        await Assert.That(Act).Throws<ObservedPredictionException>();
    }

    [Test]
    public async Task Flex_fallback_records_original_request_final_tier_and_stable_reason()
    {
        var model = Model();
        var requirement = Requirement(model);
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                requirement, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(requirement));
        var calls = 0;
        var client = new Mock<ResponsesClient>("test-api-key");
        client.Setup(value => value.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(), It.IsAny<CancellationToken>()))
            .Returns<CreateResponseOptions, CancellationToken>((_, _) =>
            {
                calls++;
                return calls == 1
                    ? Task.FromException<ClientResult<ResponseResult>>(
                        CreateClientResultException(429))
                    : Task.FromResult(CreateResponseClientResult(
                        """{"home":2,"away":1}""",
                        OpenAITestHelpers.CreateChatTokenUsage(100, 20), "default"));
            });
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "default"))
            .Returns(0.02m);
        var service = CreateObservedService(model, provider.Object, cost.Object, client.Object);

        var result = await service.PredictObservedMatchAsync(
            CreateTestMatch(), [], requirement);

        await Assert.That(result.Evidence.ServiceTier.RequestedTier).IsEqualTo("flex");
        await Assert.That(result.Evidence.ServiceTier.FinalTier).IsEqualTo("default");
        await Assert.That(result.Evidence.ServiceTier.FallbackOccurred).IsTrue();
        await Assert.That(result.Evidence.ServiceTier.FallbackReason)
            .IsEqualTo("flex-resource-unavailable-standard-fallback");
    }

    [Test]
    public async Task Cancellation_propagates_and_never_becomes_an_observed_exception()
    {
        var model = Model();
        var requirement = Requirement(model);
        using var source = new CancellationTokenSource();
        source.Cancel();
        var provider = new Mock<IObservedInstructionsTemplateProvider>(MockBehavior.Strict);
        var service = CreateObservedService(
            model, provider.Object, CreateMockCostCalculationService().Object);
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        async Task Act() => await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement,
            cancellationToken: source.Token);
        await Assert.That(Act).Throws<OperationCanceledException>();
        var activity = activities.Single(value => value.OperationName == "predict-match");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("observed-prediction-cancelled");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.output")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")).IsNull();
        provider.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Concurrent_calls_retain_distinct_invocation_prompt_tier_usage_cost_and_metadata()
    {
        var model = Model();
        var first = Requirement(model, "First prompt\n");
        const string secondHosted = "Second hosted prompt\n";
        const string secondFallback = "Second fallback prompt\n";
        var second = PredictionPromptExecutionRequirement.Create(
            model, PromptTemplateContentHash.ComputeSha256(secondHosted), "production",
            "prompts/second.md", PromptTemplateContentHash.ComputeSha256(secondFallback));
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                first, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(first, "First prompt\n"));
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                second, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResolvedPredictionPromptTemplate.CreateFallback(
                second, secondFallback, "prompts/second.md"));
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "flex"))
            .Returns(0.01m);
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "default"))
            .Returns(0.02m);
        var client = new Mock<ResponsesClient>("test-api-key");
        client.Setup(value => value.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(), It.IsAny<CancellationToken>()))
            .Returns<CreateResponseOptions, CancellationToken>((options, _) =>
            {
                var payload = ReadPayloadJson(options);
                var isSecond = payload.Contains("Second fallback prompt", StringComparison.Ordinal);
                var requestedTier = ExtractStringProperty(payload, "service_tier");
                if (isSecond && requestedTier == "flex")
                {
                    return Task.FromException<ClientResult<ResponseResult>>(
                        CreateClientResultException(429));
                }

                return Task.FromResult(CreateResponseClientResult(
                    """{"home":2,"away":1}""",
                    OpenAITestHelpers.CreateChatTokenUsage(
                        isSecond ? 200 : 100, isSecond ? 20 : 10),
                    isSecond ? "default" : "flex"));
            });
        var legacy = PoisonLegacyProvider();
        var service = CreateObservedService(
            model, provider.Object, cost.Object, client.Object, legacy.Object);
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        var results = await Task.WhenAll(
            service.PredictObservedMatchAsync(
                CreateTestMatch("first-home", "first-away"), [], first,
                telemetryMetadata: new PredictionTelemetryMetadata(HomeTeam: "first-meta")),
            service.PredictObservedMatchAsync(
                CreateTestMatch("second-home", "second-away"), [], second,
                telemetryMetadata: new PredictionTelemetryMetadata(HomeTeam: "second-meta")));

        await Assert.That(results[0].Evidence.Prompt.Template).IsEqualTo("First prompt\n");
        await Assert.That(results[0].Evidence.ServiceTier.FinalTier).IsEqualTo("flex");
        await Assert.That(results[0].Evidence.Usage.InputTokens).IsEqualTo(100);
        await Assert.That(results[0].Evidence.Usage.CostUsd).IsEqualTo(0.01m);
        await Assert.That(results[1].Evidence.Prompt.Template).IsEqualTo(secondFallback);
        await Assert.That(results[1].Evidence.ServiceTier.FinalTier).IsEqualTo("default");
        await Assert.That(results[1].Evidence.Usage.InputTokens).IsEqualTo(200);
        await Assert.That(results[1].Evidence.Usage.CostUsd).IsEqualTo(0.02m);
        var firstActivity = activities.Single(value =>
            Equals(value.GetTagItem("langfuse.observation.metadata.homeTeam"), "first-meta"));
        var secondActivity = activities.Single(value =>
            Equals(value.GetTagItem("langfuse.observation.metadata.homeTeam"), "second-meta"));
        await Assert.That(firstActivity.GetTagItem("gen_ai.response.service_tier"))
            .IsEqualTo("flex");
        await Assert.That(secondActivity.GetTagItem("gen_ai.response.service_tier"))
            .IsEqualTo("default");
        await Assert.That(firstActivity.GetTagItem(
                "langfuse.observation.metadata.langfusePromptFallback") is false)
            .IsTrue();
        await Assert.That(secondActivity.GetTagItem(
                "langfuse.observation.metadata.langfusePromptFallback") is true)
            .IsTrue();
        legacy.As<IPromptTemplateTelemetryMetadataProvider>().Verify(
            value => value.GetPromptTemplateTelemetryMetadata(), Times.Never);
    }

    [Test]
    public async Task Observed_bonus_is_defensive_and_uses_the_same_atomic_evidence()
    {
        var model = Model();
        var requirement = Requirement(model);
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedBonusTemplateAsync(
                requirement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(requirement));
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "flex"))
            .Returns(0.02m);
        var service = CreateObservedService(model, provider.Object, cost.Object,
            CreateMockChatClient("""{"selectedOptionIds":["opt1"]}"""));
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        var result = await service.PredictObservedBonusQuestionAsync(
            CreateTestBonusQuestion(), [], requirement);
        var copy = result.ToBonusPrediction();
        copy.SelectedOptionIds[0] = "mutated";

        await Assert.That(result.SelectedOptionIds).IsEquivalentTo(["opt1"]);
        await Assert.That(result.Evidence.Usage.CostUsd).IsEqualTo(0.02m);
        var activity = activities.Single(value => value.OperationName == "predict-bonus");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Ok);
        await Assert.That(activity.GetTagItem("langfuse.observation.output"))
            .IsEqualTo("""{"selectedOptionIds":["opt1"]}""");
    }

    [Test]
    public async Task Provider_failure_preserves_caller_exception_but_never_records_its_secret()
    {
        const string secret = "provider-secret-do-not-record";
        var model = Model();
        var requirement = Requirement(model);
        var failure = new InvalidOperationException(secret);
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                requirement, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var service = CreateObservedService(
            model, provider.Object, CreateMockCostCalculationService().Object);
        var activities = new ConcurrentQueue<Activity>();
        using var listener = Capture(activities);

        ObservedPredictionException? caught = null;
        try
        {
            await service.PredictObservedMatchAsync(
                CreateTestMatch(), [], requirement,
                telemetryMetadata: new PredictionTelemetryMetadata(HomeTeam: "public-home"));
        }
        catch (ObservedPredictionException exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.InnerException).IsSameReferenceAs(failure);
        var activity = activities.Single(value => value.OperationName == "predict-match");
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.StatusDescription).IsEqualTo("observed-prediction-failed");
        await Assert.That(ActivityText(activity)).DoesNotContain(secret);
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam"))
            .IsEqualTo("public-home");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.output")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")).IsNull();
    }

    private static PredictionService CreateObservedService(
        PredictionModelConfig model,
        IObservedInstructionsTemplateProvider observedProvider,
        ICostCalculationService cost,
        ResponsesClient? responsesClient = null,
        IInstructionsTemplateProvider? legacyProvider = null,
        bool disableFlexProcessing = false) => new(
            responsesClient ?? CreateMockChatClient(), CreateFakeLogger(), cost,
            CreateMockTokenUsageTracker().Object,
            legacyProvider ?? CreateMockTemplateProvider(model.Model).Object,
            observedProvider, model,
            new PredictionServiceOptions(DisableFlexProcessing: disableFlexProcessing,
                ReasoningEffort: model.ReasoningEffort,
                MaxOutputTokenCount: model.MaxOutputTokenCount!.Value));

    private static Mock<IInstructionsTemplateProvider> PoisonLegacyProvider()
    {
        var provider = CreateMockTemplateProvider();
        provider.As<IPromptTemplateTelemetryMetadataProvider>()
            .Setup(value => value.GetPromptTemplateTelemetryMetadata())
            .Returns(new PromptTemplateTelemetryMetadata(
                "poison-requested", "poison-actual", "poison-name", "poison-label", 999,
                true, "poison-path", "poison-hash"));
        return provider;
    }

    private static ActivityListener Capture(ConcurrentQueue<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "KicktippAi",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static string ActivityText(Activity activity) => string.Join('|',
        new[] { activity.StatusDescription ?? string.Empty }
            .Concat(activity.TagObjects.Select(value => $"{value.Key}={value.Value}"))
            .Concat(activity.Events.SelectMany(value =>
                value.Tags.Select(tag => $"{tag.Key}={tag.Value}")))
            .Concat(activity.Baggage.Select(value => $"{value.Key}={value.Value}")));

    private static PredictionModelConfig Model() =>
        PredictionModelConfig.Create("gpt-5", "high", 1000, PromptName, 3);

    private static PredictionPromptExecutionRequirement Requirement(
        PredictionModelConfig model, string template = Template) =>
        PredictionPromptExecutionRequirement.Create(
            model, PromptTemplateContentHash.ComputeSha256(template), "production");

    private static ResolvedPredictionPromptTemplate Hosted(
        PredictionPromptExecutionRequirement requirement, string template = Template) =>
        ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3", PromptName, 3, ["production"]);
}
