using System.Text.Json;
using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Langfuse;

namespace Orchestrator.Tests.Infrastructure;

public sealed class ObservedLangfusePromptProviderTests
{
    private const string Name = "kicktippai/bundesliga-2026-27/predict-one-match";

    [Test]
    public async Task Observed_resolution_returns_atomic_hosted_evidence_without_last_state()
    {
        const string text = "Hosted exact prompt\n";
        var prompt = CreatePrompt(text, ["production"]);
        var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        var provider = new LangfuseTextPromptTemplateProvider(
            client.Object, Name, "production", 3, prompt);
        var requirement = PredictionPromptExecutionRequirement.Create(
            Model(), PromptTemplateContentHash.ComputeSha256(text), "production");

        var resolved = await provider.LoadObservedMatchTemplateAsync(requirement, false);

        await Assert.That(resolved.Template).IsEqualTo(text);
        await Assert.That(resolved.Provenance.HostedVersion).IsEqualTo(3);
        await Assert.That(provider.GetPromptTemplateTelemetryMetadata()).IsNull();
        client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Shared_but_wrong_requirement_identity_fails_before_resolution()
    {
        const string text = "Hosted exact prompt\n";
        var client = new Mock<ILangfusePublicApiClient>(MockBehavior.Strict);
        var provider = new LangfuseTextPromptTemplateProvider(
            client.Object, Name, "production", 3, CreatePrompt(text, ["production"]));
        var wrong = PredictionPromptExecutionRequirement.Create(
            PredictionModelConfig.Create("gpt-5", "high", 1000, Name, 4),
            PromptTemplateContentHash.ComputeSha256(text), "production");

        async Task Act() => await provider.LoadObservedMatchTemplateAsync(wrong, false);
        await Assert.That(Act).Throws<InvalidDataException>();
    }

    private static PredictionModelConfig Model() =>
        PredictionModelConfig.Create("gpt-5", "high", 1000, Name, 3);

    private static LangfusePrompt CreatePrompt(string text, IReadOnlyList<string> labels)
    {
        using var prompt = JsonDocument.Parse(JsonSerializer.Serialize(text));
        using var config = JsonDocument.Parse("{}");
        return new LangfusePrompt(Name, 3, "text", prompt.RootElement.Clone(), labels, [],
            config.RootElement.Clone());
    }
}
