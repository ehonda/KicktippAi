using EHonda.KicktippAi.Core;

namespace OpenAiIntegration.Tests;

public sealed class ObservedPredictionContractsTests
{
    private const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Name = "kicktippai/bundesliga-2026-27/predict-one-match";

    [Test]
    public async Task Hosted_resolution_is_atomic_and_rejects_every_identity_drift()
    {
        const string template = "Observed prompt\r\n";
        var requirement = Requirement(PromptTemplateContentHash.ComputeSha256(template));
        var resolved = ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3", Name, 3, ["production"]);

        await Assert.That(resolved.Template).IsEqualTo(template);
        await Assert.That(resolved.Provenance.ActualSource).IsEqualTo(PredictionPromptSourceV2.Hosted);
        await Assert.That(() => ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template + "drift", "langfuse://prompt/3", Name, 3, ["production"]))
            .Throws<InvalidDataException>();
        await Assert.That(() => ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3", Name, 4, ["production"]))
            .Throws<InvalidDataException>();
        await Assert.That(() => ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3", Name, 3, ["staging"]))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Fallback_requires_the_exact_pinned_file_and_hash()
    {
        const string fallback = "Pinned fallback\n";
        var requirement = PredictionPromptExecutionRequirement.Create(
            Model(), ShaA, "production", "prompts/pinned.md",
            PromptTemplateContentHash.ComputeSha256(fallback));
        var resolved = ResolvedPredictionPromptTemplate.CreateFallback(
            requirement, fallback, "prompts/pinned.md");

        await Assert.That(resolved.Provenance.ActualSource)
            .IsEqualTo(PredictionPromptSourceV2.CheckedInFallback);
        await Assert.That(() => ResolvedPredictionPromptTemplate.CreateFallback(
            requirement, fallback, "prompts/other.md")).Throws<InvalidDataException>();
        await Assert.That(() => ResolvedPredictionPromptTemplate.CreateFallback(
            requirement, fallback + "drift", "prompts/pinned.md")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Evidence_binds_model_prompt_tier_usage_and_results_copy_mutable_inputs()
    {
        const string template = "Observed prompt\n";
        var model = Model();
        var requirement = Requirement(PromptTemplateContentHash.ComputeSha256(template));
        var prompt = ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3", Name, 3, ["production"]);
        var evidence = ObservedPredictionCallEvidence.Create(
            model, prompt, PredictionServiceTierProvenanceV2.Create("flex", "default", true, "fallback"),
            new PredictionGenerationUsageV2(100, 20, 0.01m));
        var selected = new List<string> { "a" };
        var bonus = ObservedBonusPredictionResult.Create(new BonusPrediction(selected), evidence);
        selected[0] = "mutated";

        await Assert.That(bonus.SelectedOptionIds).IsEquivalentTo(["a"]);
        await Assert.That(bonus.ToBonusPrediction().SelectedOptionIds).IsEquivalentTo(["a"]);
        await Assert.That(() => ObservedPredictionCallEvidence.Create(
            PredictionModelConfig.Create("gpt-5.6-sol", "high", 1000, Name, 4), prompt,
            evidence.ServiceTier, evidence.Usage)).Throws<InvalidDataException>();

        var uncertainties = new List<string> { "weather" };
        var match = ObservedMatchPredictionResult.Create(
            new Prediction(2, 1, new PredictionJustification(
                "reason", new PredictionJustificationContextSources([], []), uncertainties)), evidence);
        uncertainties[0] = "mutated";
        await Assert.That(match.Prediction.Justification!.Uncertainties)
            .IsEquivalentTo(["weather"]);
    }

    [Test]
    public async Task Requirement_rejects_unpinned_or_partial_fallback_identity()
    {
        await Assert.That(() => PredictionPromptExecutionRequirement.Create(
            PredictionModelConfig.Create("gpt-5.6-sol"), ShaA, "production"))
            .Throws<InvalidDataException>();
        await Assert.That(() => PredictionPromptExecutionRequirement.Create(
            Model(), ShaA, "production", "fallback.md", null)).Throws<InvalidDataException>();
        await Assert.That(() => PredictionPromptExecutionRequirement.Create(
            Model(), ShaB.ToUpperInvariant(), "production")).Throws<InvalidDataException>();
    }

    private static PredictionModelConfig Model() =>
        PredictionModelConfig.Create("gpt-5.6-sol", "high", 1000, Name, 3);

    private static PredictionPromptExecutionRequirement Requirement(string hash) =>
        PredictionPromptExecutionRequirement.Create(Model(), hash, "production");
}
