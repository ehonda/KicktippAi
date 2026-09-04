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
        var observed = ObservedMatchPredictionResult.Create(
            new Prediction(2, 1), Evidence(current.PromptRequirement));
        var context = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            current.Current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));

        var row = new BundesligaPredictionProvenanceAssembler().AssembleDirectMatch(
            current, observed, context, Instant.FromUtc(2026, 9, 1, 12, 0), "prediction-r0", 0);

        await Assert.That(row.Provenance.ModelConfig).IsEqualTo(current.Current.ModelConfig);
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
            current, ObservedMatchPredictionResult.Create(new Prediction(2, 1),
                Evidence(PredictionPromptExecutionRequirement.Create(
                    wrongModel,
                    current.PromptRequirement.HostedNormalizedReadbackSha256,
                    current.PromptRequirement.RequiredLabel))),
            context, Instant.FromUtc(2026, 9, 1, 12, 0), "prediction-r0", 0))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Direct_match_and_bonus_reject_hash_label_and_configured_fallback_policy_drift()
    {
        var matchItems = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var bonusItems = BundesligaPredictionAuthorityKernelTestData.BonusItems();
        var matchSelection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var bonusSelection = BundesligaPredictionAuthorityKernelTestData.BonusSelection(
            "source-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([matchSelection, bonusSelection]),
            new Mock<IBundesligaTypedPredictionAuthorityRepository>().Object);
        var match = kernel.PrepareCurrent(matchItems.Source, matchSelection.SelectionId);
        var bonus = kernel.PrepareCurrent(bonusItems.Source, bonusSelection.SelectionId);
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var matchContext = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            match.Current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));
        var bonusContext = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            bonus.Current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));
        var wrongHashTemplate = "Wrong hosted prompt\n";
        var wrongHash = PredictionPromptExecutionRequirement.Create(
            BundesligaPredictionAuthorityKernelTestData.Model(),
            PromptTemplateContentHash.ComputeSha256(wrongHashTemplate), "production");
        var wrongLabel = PredictionPromptExecutionRequirement.Create(
            BundesligaPredictionAuthorityKernelTestData.Model(),
            match.PromptRequirement.HostedNormalizedReadbackSha256, "staging");
        var wrongFallback = PredictionPromptExecutionRequirement.Create(
            BundesligaPredictionAuthorityKernelTestData.Model(),
            match.PromptRequirement.HostedNormalizedReadbackSha256, "production",
            "prompts/unregistered.md", BundesligaPredictionAuthorityKernelTestData.ShaA);
        var hostileEvidence = new[]
        {
            Evidence(wrongHash, Hosted(wrongHash, wrongHashTemplate, "production")),
            Evidence(wrongLabel, Hosted(wrongLabel,
                BundesligaPredictionAuthorityKernelTestData.PromptTemplate, "staging")),
            Evidence(wrongFallback, Hosted(wrongFallback,
                BundesligaPredictionAuthorityKernelTestData.PromptTemplate, "production"))
        };
        var assembler = new BundesligaPredictionProvenanceAssembler();

        foreach (var evidence in hostileEvidence)
        {
            await Assert.That(() => assembler.AssembleDirectMatch(
                    match, ObservedMatchPredictionResult.Create(new Prediction(2, 1), evidence),
                    matchContext, Instant.FromUtc(2026, 9, 1, 12, 0), "match-r0", 0))
                .Throws<InvalidDataException>();
            await Assert.That(() => assembler.AssembleDirectBonus(
                    bonus, ObservedBonusPredictionResult.Create(new BonusPrediction(["a"]), evidence),
                    bonusContext, Instant.FromUtc(2026, 9, 1, 12, 0), "bonus-r0", 0))
                .Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Exact_hosted_and_fallback_policies_are_accepted_without_a_raw_current_overload()
    {
        const string fallbackTemplate = "Pinned fallback prompt\n";
        var fallbackRequirement = PredictionPromptExecutionRequirement.Create(
            BundesligaPredictionAuthorityKernelTestData.Model(),
            PromptTemplateContentHash.ComputeSha256(
                BundesligaPredictionAuthorityKernelTestData.PromptTemplate),
            "production", "prompts/pinned.md",
            PromptTemplateContentHash.ComputeSha256(fallbackTemplate));
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            promptRequirement: fallbackRequirement);
        var bonusItems = BundesligaPredictionAuthorityKernelTestData.BonusItems();
        var bonusSelection = BundesligaPredictionAuthorityKernelTestData.BonusSelection(
            "source-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            promptRequirement: fallbackRequirement);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([selection, bonusSelection]),
            new Mock<IBundesligaTypedPredictionAuthorityRepository>().Object);
        var prepared = kernel.PrepareCurrent(items.Source, selection.SelectionId);
        var preparedBonus = kernel.PrepareCurrent(bonusItems.Source, bonusSelection.SelectionId);
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var context = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            prepared.Current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));
        var bonusContext = BundesligaPredictionContextObservationV2.Create(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            preparedBonus.Current.Identity.ProfileId,
            BundesligaPredictionAuthorityKernelTestData.Context(rules));
        var assembler = new BundesligaPredictionProvenanceAssembler();
        var hosted = Evidence(fallbackRequirement, Hosted(
            fallbackRequirement, BundesligaPredictionAuthorityKernelTestData.PromptTemplate,
            "production"));
        var fallback = Evidence(fallbackRequirement,
            ResolvedPredictionPromptTemplate.CreateFallback(
                fallbackRequirement, fallbackTemplate, "prompts/pinned.md"));

        var hostedRow = assembler.AssembleDirectMatch(
            prepared, ObservedMatchPredictionResult.Create(new Prediction(2, 1), hosted),
            context, Instant.FromUtc(2026, 9, 1, 12, 0), "hosted-r0", 0);
        var fallbackRow = assembler.AssembleDirectMatch(
            prepared, ObservedMatchPredictionResult.Create(new Prediction(1, 0), fallback),
            context, Instant.FromUtc(2026, 9, 1, 12, 1), "fallback-r0", 0);
        var hostedBonusRow = assembler.AssembleDirectBonus(
            preparedBonus,
            ObservedBonusPredictionResult.Create(new BonusPrediction(["a"]), hosted),
            bonusContext, Instant.FromUtc(2026, 9, 1, 12, 2), "hosted-bonus-r0", 0);
        var fallbackBonusRow = assembler.AssembleDirectBonus(
            preparedBonus,
            ObservedBonusPredictionResult.Create(new BonusPrediction(["b"]), fallback),
            bonusContext, Instant.FromUtc(2026, 9, 1, 12, 3), "fallback-bonus-r0", 0);

        await Assert.That(hostedRow.Provenance.Prompt.ActualSource)
            .IsEqualTo(PredictionPromptSourceV2.Hosted);
        await Assert.That(fallbackRow.Provenance.Prompt.ActualSource)
            .IsEqualTo(PredictionPromptSourceV2.CheckedInFallback);
        await Assert.That(hostedBonusRow.Provenance.Prompt.ActualSource)
            .IsEqualTo(PredictionPromptSourceV2.Hosted);
        await Assert.That(fallbackBonusRow.Provenance.Prompt.ActualSource)
            .IsEqualTo(PredictionPromptSourceV2.CheckedInFallback);
        var directParameters = new[]
        {
            typeof(IBundesligaPredictionProvenanceAssembler),
            typeof(BundesligaPredictionProvenanceAssembler)
        }.SelectMany(type => type.GetMethods())
            .Where(method => method.Name is nameof(
                IBundesligaPredictionProvenanceAssembler.AssembleDirectMatch)
                or nameof(IBundesligaPredictionProvenanceAssembler.AssembleDirectBonus))
            .SelectMany(method => method.GetParameters())
            .ToArray();
        await Assert.That(directParameters
            .Any(parameter => parameter.ParameterType.IsGenericType
                && parameter.ParameterType.GetGenericTypeDefinition()
                    == typeof(BundesligaTypedCurrentRequest<>))).IsFalse();
        await Assert.That(directParameters
            .Any(parameter => parameter.ParameterType
                == typeof(PredictionPromptExecutionRequirement))).IsFalse();
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
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-match").Current;
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
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-bonus").Current;
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

    private static ObservedPredictionCallEvidence Evidence(
        PredictionPromptExecutionRequirement requirement,
        ResolvedPredictionPromptTemplate? resolved = null)
    {
        var prompt = resolved ?? ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, BundesligaPredictionAuthorityKernelTestData.PromptTemplate,
            "langfuse://prompt/3",
            BundesligaPredictionAuthorityKernelTestData.PromptName, 3, ["production"]);
        return ObservedPredictionCallEvidence.Create(
            requirement, prompt, PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            new PredictionGenerationUsageV2(100, 20, 0.001m));
    }

    private static ResolvedPredictionPromptTemplate Hosted(
        PredictionPromptExecutionRequirement requirement,
        string template,
        string label) =>
        ResolvedPredictionPromptTemplate.CreateHosted(
            requirement, template, "langfuse://prompt/3",
            BundesligaPredictionAuthorityKernelTestData.PromptName, 3, [label]);
}
