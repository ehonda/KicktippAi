using EHonda.KicktippAi.Core;
using System.ClientModel;
using Moq;
using OpenAI.Responses;
using TestUtilities;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

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

        var result = await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement);

        await Assert.That(result.Prediction.HomeGoals).IsEqualTo(2);
        await Assert.That(result.Evidence.Prompt).IsSameReferenceAs(prompt);
        await Assert.That(result.Evidence.ServiceTier.RequestedTier).IsEqualTo("flex");
        await Assert.That(result.Evidence.ServiceTier.FinalTier).IsEqualTo("flex");
        await Assert.That(result.Evidence.Usage.InputTokens).IsEqualTo(1000);
        await Assert.That(result.Evidence.Usage.CostUsd).IsEqualTo(0.0123m);
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

        async Task Act() => await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement);
        await Assert.That(Act).Throws<ObservedPredictionException>();
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

        async Task Act() => await service.PredictObservedMatchAsync(
            CreateTestMatch(), CreateTestContextDocuments(), requirement,
            cancellationToken: source.Token);
        await Assert.That(Act).Throws<OperationCanceledException>();
        provider.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Concurrent_calls_retain_their_own_atomic_prompt_evidence()
    {
        var model = Model();
        var first = Requirement(model, "First prompt\n");
        var second = Requirement(model, "Second prompt\n");
        var provider = new Mock<IObservedInstructionsTemplateProvider>();
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                first, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(first, "First prompt\n"));
        provider.Setup(value => value.LoadObservedMatchTemplateAsync(
                second, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Hosted(second, "Second prompt\n"));
        var cost = CreateMockCostCalculationService();
        cost.Setup(value => value.CalculateCost(model.Model, It.IsAny<OpenAI.Chat.ChatTokenUsage>(), "flex"))
            .Returns(0.01m);
        var service = CreateObservedService(model, provider.Object, cost.Object);

        var results = await Task.WhenAll(
            service.PredictObservedMatchAsync(CreateTestMatch(), [], first),
            service.PredictObservedMatchAsync(CreateTestMatch(), [], second));

        await Assert.That(results[0].Evidence.Prompt.Template).IsEqualTo("First prompt\n");
        await Assert.That(results[1].Evidence.Prompt.Template).IsEqualTo("Second prompt\n");
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

        var result = await service.PredictObservedBonusQuestionAsync(
            CreateTestBonusQuestion(), [], requirement);
        var copy = result.ToBonusPrediction();
        copy.SelectedOptionIds[0] = "mutated";

        await Assert.That(result.SelectedOptionIds).IsEquivalentTo(["opt1"]);
        await Assert.That(result.Evidence.Usage.CostUsd).IsEqualTo(0.02m);
    }

    private static PredictionService CreateObservedService(
        PredictionModelConfig model,
        IObservedInstructionsTemplateProvider observedProvider,
        ICostCalculationService cost,
        ResponsesClient? responsesClient = null) => new(
            responsesClient ?? CreateMockChatClient(), CreateFakeLogger(), cost,
            CreateMockTokenUsageTracker().Object, CreateMockTemplateProvider(model.Model).Object,
            observedProvider, model,
            new PredictionServiceOptions(ReasoningEffort: model.ReasoningEffort,
                MaxOutputTokenCount: model.MaxOutputTokenCount!.Value));

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
