using EHonda.KicktippAi.Core;
using Moq;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Services;

namespace Orchestrator.Tests.Services;

public sealed class BundesligaPredictionProvenanceAssemblerTests
{
    [Test]
    public async Task Direct_assembly_binds_prepared_current_observed_result_and_context()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([selection]),
            new Mock<IBundesligaTypedPredictionAuthorityRepository>().Object);
        var current = kernel.PrepareCurrent(items.Source, selection.SelectionId);
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var observed = ObservedMatchPredictionResult.Create(new Prediction(2, 1), Evidence());
        var context = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));

        var row = new BundesligaPredictionProvenanceAssembler().AssembleDirectMatch(
            current, observed, context, Instant.FromUtc(2026, 9, 1, 12, 0), "prediction-r0", 0);

        await Assert.That(row.Provenance.ModelConfig).IsEqualTo(current.ModelConfig);
        await Assert.That(row.Provenance.Context).IsSameReferenceAs(context.Provenance);
        await Assert.That(row.Provenance.TargetGenerationUsage.CostUsd).IsEqualTo(0.001m);
        var wrongContext = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            "wrong-profile", context.Provenance);
        await Assert.That(() => new BundesligaPredictionProvenanceAssembler().AssembleDirectMatch(
            current, observed, wrongContext, Instant.FromUtc(2026, 9, 1, 12, 0), "prediction-r0", 0))
            .Throws<InvalidDataException>();
        var wrongModel = PredictionModelConfig.Create(
            "gpt-5.6-luna", "none", 10_000,
            BundesligaPredictionAuthorityKernelTestData.PromptName, 3);
        await Assert.That(() => new BundesligaPredictionProvenanceAssembler().AssembleDirectMatch(
            current, ObservedMatchPredictionResult.Create(new Prediction(2, 1), Evidence(wrongModel)),
            context, Instant.FromUtc(2026, 9, 1, 12, 0), "prediction-r0", 0))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Accepted_copy_derives_actual_source_evidence_and_forces_zero_target_usage()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var compatibility = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules: rules);
        var selections = new[]
        {
            BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                "target-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, compatibility),
            BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, compatibility)
        };
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(selections), repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-match");
        var sourceRow = TypedMatchPredictionRecord.Create(
            sourceCurrent, new Prediction(1, 0),
            BundesligaPredictionAuthorityKernelTestData.MatchProvenance(sourceCurrent, rules));
        repository.Setup(value => value.GetCurrentTypedMatchPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedMatchSnapshot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);
        repository.Setup(value => value.GetTypedMatchCopyCandidateAsync(
                It.IsAny<BundesligaTypedCopyRequest<TypedMatchSnapshot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaTypedCopyRequest<TypedMatchSnapshot> request, CancellationToken _) =>
                TypedMatchCopyCandidate.Create(request, sourceRow));
        var plan = await kernel.PrepareMatchCopyAsync(
            items.Target, "target-match", items.Binding, items.Source, "source-match");
        var targetContext = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            plan.Request!.TargetCurrent.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));

        var save = new BundesligaPredictionProvenanceAssembler().AssembleMatchCopy(
            plan, targetContext, Instant.FromUtc(2026, 9, 1, 13, 0), "copy-r0", 0);

        await Assert.That(save.TargetProvenance.Prompt).IsEqualTo(sourceRow.Provenance.Prompt);
        await Assert.That(save.TargetProvenance.ModelConfig).IsEqualTo(sourceRow.Provenance.ModelConfig);
        await Assert.That(save.TargetProvenance.ServiceTier).IsEqualTo(sourceRow.Provenance.ServiceTier);
        await Assert.That(save.TargetProvenance.SourcePredictionIdentity)
            .IsEqualTo(sourceRow.Provenance.PredictionIdentity);
        await Assert.That(save.TargetProvenance.TargetGenerationUsage.IsZero).IsTrue();
    }

    [Test]
    public async Task Bonus_copy_preserves_source_order_and_forces_zero_target_usage()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.BonusItems();
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var compatibility = BundesligaPredictionAuthorityKernelTestData.BonusCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([
                BundesligaPredictionAuthorityKernelTestData.BonusSelection(
                    "target-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, compatibility),
                BundesligaPredictionAuthorityKernelTestData.BonusSelection(
                    "source-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, compatibility)]),
            repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-bonus");
        var sourceRow = TypedBonusPredictionRecord.Create(
            sourceCurrent, new BonusPrediction(["b", "a"]),
            BundesligaPredictionAuthorityKernelTestData.BonusProvenance(sourceCurrent, rules));
        repository.Setup(value => value.GetCurrentTypedBonusPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedBonusSnapshot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);
        repository.Setup(value => value.GetTypedBonusCopyCandidateAsync(
                It.IsAny<BundesligaTypedCopyRequest<TypedBonusSnapshot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaTypedCopyRequest<TypedBonusSnapshot> request, CancellationToken _) =>
                TypedBonusCopyCandidate.Create(request, sourceRow));
        var plan = await kernel.PrepareBonusCopyAsync(
            items.Target, "target-bonus", items.Binding, items.Source, "source-bonus");
        var context = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            plan.Request!.TargetCurrent.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));

        var save = new BundesligaPredictionProvenanceAssembler().AssembleBonusCopy(
            plan, context, Instant.FromUtc(2026, 9, 1, 13, 0), "bonus-copy-r0", 0);

        await Assert.That(save.SelectedOptionIds.SequenceEqual(["b", "a"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(save.TargetProvenance.TargetGenerationUsage.IsZero).IsTrue();
        await Assert.That(save.TargetProvenance.SourcePredictionIdentity)
            .IsEqualTo(sourceRow.Provenance.PredictionIdentity);
    }

    private static ObservedPredictionCallEvidence Evidence(PredictionModelConfig? model = null)
    {
        model ??= BundesligaPredictionAuthorityKernelTestData.Model();
        const string template = "Observed exact prompt\n";
        var requirement = PredictionPromptExecutionRequirement.Create(
            model, PromptTemplateContentHash.ComputeSha256(template), "production");
        var prompt = ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3",
            BundesligaPredictionAuthorityKernelTestData.PromptName, 3, ["production"]);
        return ObservedPredictionCallEvidence.Create(
            model, prompt, PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            new PredictionGenerationUsageV2(100, 20, 0.001m));
    }
}
