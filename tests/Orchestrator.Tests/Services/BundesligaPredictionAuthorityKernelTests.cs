using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using Orchestrator.Services;

namespace Orchestrator.Tests.Services;

public sealed class BundesligaPredictionAuthorityKernelTests
{
    [Test]
    public async Task PrepareCurrent_accepts_only_a_validated_item_and_its_exact_registered_selection()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var selection = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([selection]), repository.Object);

        var prepared = kernel.PrepareCurrent(items.Source, selection.SelectionId);
        var current = prepared.Current;

        await Assert.That(current.Authority).IsEqualTo(items.SourceAuthority);
        await Assert.That(current.Snapshot).IsSameReferenceAs(items.Source.Snapshot);
        await Assert.That(current.Identity.RouteId)
            .IsEqualTo(BundesligaPredictionAuthorityKernelTestData.MatchRoute);
        await Assert.That(prepared.PromptRequirement).IsSameReferenceAs(selection.PromptRequirement);
        await Assert.That(() => kernel.PrepareCurrent(items.Target, "unregistered-selection"))
            .Throws<InvalidDataException>();
        repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Accepted_match_copy_is_bound_to_the_exact_precompatibility_row_and_candidate()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var selections = MatchSelections(rules, rules);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(selections), repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-match").Current;
        var sourceRow = TypedMatchPredictionRecord.Create(
            sourceCurrent,
            new Prediction(2, 1),
            BundesligaPredictionAuthorityKernelTestData.MatchProvenance(sourceCurrent, rules));

        repository.Setup(value => value.GetCurrentTypedMatchPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);
        repository.Setup(value => value.GetTypedMatchCopyCandidateAsync(
                It.IsAny<BundesligaTypedCopyRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaTypedCopyRequest<TypedMatchSnapshot> request, CancellationToken _) =>
                TypedMatchCopyCandidate.Create(request, sourceRow));

        var plan = await kernel.PrepareMatchCopyAsync(
            items.Target, "target-match", items.Binding, items.Source, "source-match");

        await Assert.That(plan.IsAccepted).IsTrue();
        await Assert.That(plan.Decision.Succeeded).IsTrue();
        await Assert.That(plan.Prediction).IsNotNull();
        await Assert.That(PredictionContentEquality.Equals(plan.Prediction, sourceRow.Prediction)).IsTrue();
        await Assert.That(plan.Candidate!.CopyRequestFingerprint)
            .IsEqualTo(plan.Decision.BoundFingerprint);
        repository.VerifyAll();
    }

    [Test]
    public async Task Actual_source_policy_drift_fails_before_compatibility_candidate_activity()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var registeredRules = PredictionRulesIdentityV2.Create(
            "registered-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaA);
        var storedRules = PredictionRulesIdentityV2.Create(
            "stored-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var selections = MatchSelections(registeredRules, registeredRules);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(selections), repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-match").Current;
        var sourceRow = TypedMatchPredictionRecord.Create(
            sourceCurrent,
            new Prediction(1, 0),
            BundesligaPredictionAuthorityKernelTestData.MatchProvenance(sourceCurrent, storedRules));
        repository.Setup(value => value.GetCurrentTypedMatchPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);

        await Assert.That(() => kernel.PrepareMatchCopyAsync(
                items.Target, "target-match", items.Binding, items.Source, "source-match"))
            .Throws<InvalidDataException>()
            .WithMessageContaining("Actual typed source row");
        repository.Verify(value => value.GetTypedMatchCopyCandidateAsync(
            It.IsAny<BundesligaTypedCopyRequest<TypedMatchSnapshot>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Exact_R1_rejection_returns_no_candidate_and_candidate_drift_fails_closed()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var sourceScoring = PredictionScoringIdentityV2.Create(
            "source-scoring-v1", BundesligaPredictionAuthorityKernelTestData.ShaA);
        var targetScoring = PredictionScoringIdentityV2.Create(
            "target-scoring-v1", BundesligaPredictionAuthorityKernelTestData.ShaC);
        var sourceContract = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            sourceScoring,
            rules);
        var targetContract = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            targetScoring,
            rules);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(
            [
                BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                    "target-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, targetContract),
                BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                    "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, sourceContract)
            ]),
            repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-match").Current;
        var sourceRow = TypedMatchPredictionRecord.Create(
            sourceCurrent,
            new Prediction(1, 1),
            BundesligaPredictionAuthorityKernelTestData.MatchProvenance(sourceCurrent, rules));
        repository.Setup(value => value.GetCurrentTypedMatchPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);

        var rejected = await kernel.PrepareMatchCopyAsync(
            items.Target, "target-match", items.Binding, items.Source, "source-match");

        await Assert.That(rejected.IsAccepted).IsFalse();
        await Assert.That(rejected.Decision.Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.ScoringIdentityMismatch);
        repository.Verify(value => value.GetTypedMatchCopyCandidateAsync(
            It.IsAny<BundesligaTypedCopyRequest<TypedMatchSnapshot>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        var acceptedSelections = MatchSelections(rules, rules);
        var driftRepository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var acceptedKernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(acceptedSelections), driftRepository.Object);
        var acceptedCurrent = acceptedKernel.PrepareCurrent(items.Source, "source-match").Current;
        var acceptedRow = TypedMatchPredictionRecord.Create(
            acceptedCurrent,
            new Prediction(1, 1),
            BundesligaPredictionAuthorityKernelTestData.MatchProvenance(acceptedCurrent, rules));
        driftRepository.Setup(value => value.GetCurrentTypedMatchPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(acceptedRow);
        driftRepository.Setup(value => value.GetTypedMatchCopyCandidateAsync(
                It.IsAny<BundesligaTypedCopyRequest<TypedMatchSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaTypedCopyRequest<TypedMatchSnapshot> request, CancellationToken _) =>
                TypedMatchCopyCandidate.Create(
                    request,
                    TypedMatchPredictionRecord.Create(
                        acceptedCurrent,
                        new Prediction(9, 9),
                        acceptedRow.Provenance)));

        await Assert.That(() => acceptedKernel.PrepareMatchCopyAsync(
                items.Target, "target-match", items.Binding, items.Source, "source-match"))
            .Throws<InvalidDataException>()
            .WithMessageContaining("candidate drifted");
    }

    [Test]
    public async Task Bonus_projection_preserves_source_candidate_order()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.BonusItems();
        var rules = PredictionRulesIdentityV2.Create(
            "synthetic-rules-v1", BundesligaPredictionAuthorityKernelTestData.ShaB);
        var targetContract = BundesligaPredictionAuthorityKernelTestData.BonusCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules);
        var sourceContract = BundesligaPredictionAuthorityKernelTestData.BonusCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry(
            [
                BundesligaPredictionAuthorityKernelTestData.BonusSelection(
                    "target-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, targetContract),
                BundesligaPredictionAuthorityKernelTestData.BonusSelection(
                    "source-bonus", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, sourceContract)
            ]),
            repository.Object);
        var sourceCurrent = kernel.PrepareCurrent(items.Source, "source-bonus").Current;
        var sourceRow = TypedBonusPredictionRecord.Create(
            sourceCurrent,
            new BonusPrediction(["b", "a"]),
            BundesligaPredictionAuthorityKernelTestData.BonusProvenance(sourceCurrent, rules));
        repository.Setup(value => value.GetCurrentTypedBonusPredictionAsync(
                It.IsAny<BundesligaTypedCurrentRequest<TypedBonusSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceRow);
        repository.Setup(value => value.GetTypedBonusCopyCandidateAsync(
                It.IsAny<BundesligaTypedCopyRequest<TypedBonusSnapshot>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaTypedCopyRequest<TypedBonusSnapshot> request, CancellationToken _) =>
                TypedBonusCopyCandidate.Create(request, sourceRow));

        var plan = await kernel.PrepareBonusCopyAsync(
            items.Target, "target-bonus", items.Binding, items.Source, "source-bonus");

        await Assert.That(plan.IsAccepted).IsTrue();
        await Assert.That(plan.MappedPostingOptionIds.SequenceEqual(
            ["b", "a"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(plan.MappedPostingOptionIds is IList<string>).IsTrue();
        await Assert.That(() => ((IList<string>)plan.MappedPostingOptionIds)[0] = "changed")
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Prepared_current_proof_rejects_every_registered_selection_drift_and_has_no_public_bypass()
    {
        var items = BundesligaPredictionAuthorityKernelTestData.MatchItems();
        var exact = BundesligaPredictionAuthorityKernelTestData.MatchSelection(
            "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var kernel = new BundesligaPredictionAuthorityKernel(
            new BundesligaPredictionRouteRegistry([exact]),
            new Mock<IBundesligaTypedPredictionAuthorityRepository>().Object);
        var current = kernel.PrepareCurrent(items.Source, exact.SelectionId).Current;
        var wrongModel = PredictionModelConfig.Create(
            "gpt-5.6-luna", "none", 10_000,
            BundesligaPredictionAuthorityKernelTestData.PromptName, 3);
        var wrongRequirement = PredictionPromptExecutionRequirement.Create(
            wrongModel,
            BundesligaPredictionAuthorityKernelTestData.PromptRequirement()
                .HostedNormalizedReadbackSha256,
            "production");
        BundesligaPredictionRouteSelection Selection(
            BundesligaPredictionRouteContract? route = null,
            string? context = null,
            string? profile = null,
            BundesligaGenerationInputContractReference? generation = null,
            PredictionPromptExecutionRequirement? prompt = null) =>
            BundesligaPredictionRouteSelection.Create(
                "drifted", route ?? exact.Route,
                context ?? exact.CommunityContext,
                profile ?? exact.ProfileId,
                generation ?? exact.GenerationInputContract,
                prompt ?? exact.PromptRequirement);
        Action[] hostiles =
        [
            () => BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(
                current, Selection(prompt: wrongRequirement)),
            () => BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(
                current, Selection(route: new BundesligaPredictionRouteContract(
                    BundesligaPredictionAuthorityKernelTestData.MatchRoute,
                    BundesligaPredictionItemKind.Bonus,
                    BundesligaSeasonSubcompetition.Bundesliga))),
            () => BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(
                current, Selection(profile: "wrong-profile-v1")),
            () => BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(
                current, Selection(generation: BundesligaGenerationInputContractReference.Create(
                    "wrong-generation-input-v1", BundesligaPredictionAuthorityKernelTestData.ShaA))),
            () => BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(
                current, Selection(context: BundesligaPredictionAuthorityKernelTestData.TargetCommunity))
        ];

        foreach (var hostile in hostiles)
            await Assert.That(hostile).Throws<InvalidDataException>();

        await Assert.That(typeof(BundesligaPreparedCurrent<TypedMatchSnapshot>).GetConstructors())
            .IsEmpty();
        await Assert.That(typeof(BundesligaPreparedCurrent<TypedMatchSnapshot>)
                .GetMethods(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.DeclaredOnly))
            .IsEmpty();
        await Assert.That(typeof(BundesligaPreparedCurrent<TypedMatchSnapshot>)
            .GetMethod("Deconstruct")).IsNull();
    }

    [Test]
    public async Task Invalid_inventory_reaches_no_repository_activity_and_public_kernel_has_no_raw_snapshot_path()
    {
        var seed = BundesligaPredictionAuthorityKernelTestData.Seed(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity);
        var authority = BundesligaPredictionAuthorityKernelTestData.DirectAuthority(seed);
        var snapshot = BundesligaPredictionAuthorityKernelTestData.Match(seed.PostingCommunity);
        var repository = new Mock<IBundesligaTypedPredictionAuthorityRepository>(MockBehavior.Strict);

        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority,
                seed,
                [snapshot.Key, snapshot.Key],
                [snapshot],
                BundesligaPredictionAuthorityKernelTestData.Routes()))
            .Throws<InvalidDataException>();
        repository.VerifyNoOtherCalls();
        await Assert.That(typeof(IBundesligaPredictionAuthorityKernel).GetMethods()
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TypedMatchSnapshot)
                || parameter.ParameterType == typeof(TypedBonusSnapshot))).IsFalse();
    }

    private static BundesligaPredictionRouteSelection[] MatchSelections(
        PredictionRulesIdentityV2 targetRules,
        PredictionRulesIdentityV2 sourceRules)
    {
        var targetContract = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules: targetRules);
        var sourceContract = BundesligaPredictionAuthorityKernelTestData.MatchCompatibility(
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            BundesligaPredictionAuthorityKernelTestData.SourceCommunity,
            rules: sourceRules);
        return
        [
            BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                "target-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, targetContract),
            BundesligaPredictionAuthorityKernelTestData.MatchSelection(
                "source-match", BundesligaPredictionAuthorityKernelTestData.SourceCommunity, sourceContract)
        ];
    }
}
