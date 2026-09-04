using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class PredictionCopyCompatibilityV2Tests
{
    [Test]
    public async Task Bound_input_is_versioned_strict_canonical_named_json_with_fixed_fingerprint()
    {
        var decision = PredictionCopyCompatibilityV2.Evaluate(BundesligaPredictionContractTestData.MatchCopyInput());
        var bytes = decision.CanonicalInput.SerializeCanonical();
        var restored = PredictionCopyCompatibilityV2CanonicalInput.DeserializeCanonical(bytes);
        using var document = JsonDocument.Parse(bytes);

        await Assert.That(decision.CanonicalInputSchemaVersion)
            .IsEqualTo("prediction-copy-compatibility-input-v2");
        await Assert.That(document.RootElement.EnumerateObject().Select(property => property.Name).SequenceEqual(new[]
            {
                "schemaVersion", "targetCurrent", "sourceCurrent", "postingSeed", "sourceSeed",
                "binding", "bindingEntry", "targetContract", "sourceContract"
            }, StringComparer.Ordinal)).IsTrue();
        await Assert.That(restored.SerializeCanonical()).IsEquivalentTo(bytes);
        await Assert.That(decision.BoundFingerprint).IsEqualTo("5be3bee90cf35e8d9bed80ce94d5f002ac9340c73dfb6adaf8b30c8b248649b1");

        var json = Encoding.UTF8.GetString(bytes);
        foreach (var mutation in new[]
        {
            json.Replace("\"schemaVersion\":\"prediction-copy-compatibility-input-v2\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
            json.Replace("\"targetCurrent\":", "\"extra\":true,\"targetCurrent\":", StringComparison.Ordinal),
            json.Replace("\"hostedVersion\":3", "\"hostedVersion\":\"3\"", StringComparison.Ordinal),
            json.Replace(BundesligaPredictionContractTestData.ShaA, "not-a-hash", StringComparison.Ordinal),
            " " + json
        })
        {
            await Assert.That(() => PredictionCopyCompatibilityV2CanonicalInput.DeserializeCanonical(
                Encoding.UTF8.GetBytes(mutation))).Throws<Exception>();
        }
    }

    [Test]
    public async Task Named_prompt_fields_prevent_delimiter_collision_and_prompt_must_match_pinned_model()
    {
        static PredictionPromptProvenanceV2 Fallback(string label, string file) =>
            PredictionPromptProvenanceV2.Create(
                PredictionPromptSourceV2.CheckedInFallback,
                BundesligaPredictionContractTestData.MatchPrompt,
                3,
                BundesligaPredictionContractTestData.ShaA,
                label,
                true,
                file,
                BundesligaPredictionContractTestData.ShaB);

        var firstPrompt = Fallback("prod:x", "f");
        var secondPrompt = Fallback("prod", "x:f");
        var firstInput = BundesligaPredictionContractTestData.MatchCopyInput(
            targetContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(prompt: firstPrompt),
            sourceContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(prompt: firstPrompt));
        var secondInput = BundesligaPredictionContractTestData.MatchCopyInput(
            targetContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(prompt: secondPrompt),
            sourceContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(prompt: secondPrompt));

        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(firstInput).BoundFingerprint)
            .IsNotEqualTo(PredictionCopyCompatibilityV2.Evaluate(secondInput).BoundFingerprint);

        var internallySharedButWrong = PredictionPromptProvenanceV2.Create(
            PredictionPromptSourceV2.Hosted,
            "different/prompt",
            4,
            BundesligaPredictionContractTestData.ShaA,
            "production",
            true);
        await Assert.That(() => BundesligaPredictionContractTestData.MatchCompatibilityContract(
            prompt: internallySharedButWrong)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_match_and_bonus_contracts_produce_exact_bound_decisions_and_requests()
    {
        var matchInput = BundesligaPredictionContractTestData.MatchCopyInput();
        var bonusInput = BundesligaPredictionContractTestData.BonusCopyInput();
        var matchDecision = PredictionCopyCompatibilityV2.Evaluate(matchInput);
        var bonusDecision = PredictionCopyCompatibilityV2.Evaluate(bonusInput);
        var matchRequest = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(matchInput, matchDecision);
        var bonusRequest = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(bonusInput, bonusDecision);

        await Assert.That(matchDecision.Succeeded).IsTrue();
        await Assert.That(matchDecision.Binding).IsEqualTo(matchInput.Binding.Reference);
        await Assert.That(matchDecision.BoundFingerprint.Length).IsEqualTo(64);
        await Assert.That(matchRequest.SourceCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Direct);
        await Assert.That(matchRequest.TargetCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Copy);
        await Assert.That(bonusDecision.Succeeded).IsTrue();
        await Assert.That(bonusDecision.OptionProjection.Count).IsEqualTo(2);
        await Assert.That(bonusRequest.BindingEntry).IsEqualTo(bonusInput.BindingEntry);
    }

    [Test]
    public async Task Typed_identity_mismatch_returns_one_explicit_failure_without_projection()
    {
        var changedRules = PredictionRulesIdentityV2.Create(
            "different-rules-v1", BundesligaPredictionContractTestData.ShaC);
        var input = BundesligaPredictionContractTestData.BonusCopyInput(
            sourceContract: BundesligaPredictionContractTestData.BonusCompatibilityContract(rules: changedRules));
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);
        var scoringInput = BundesligaPredictionContractTestData.BonusCopyInput(
            sourceContract: BundesligaPredictionContractTestData.BonusCompatibilityContract(
                scoring: PredictionScoringIdentityV2.Create(
                    "different-scoring-v2", BundesligaPredictionContractTestData.ShaC)));

        await Assert.That(decision.Succeeded).IsFalse();
        await Assert.That(decision.Failure).IsEqualTo(PredictionCopyCompatibilityV2Failure.RulesIdentityMismatch);
        await Assert.That(decision.OptionProjection).IsEmpty();
        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(scoringInput).Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.ScoringIdentityMismatch);
        await Assert.That(() => BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(input, decision))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Prompt_model_and_copy_policy_identities_are_compared_exactly()
    {
        var changedPromptInput = BundesligaPredictionContractTestData.MatchCopyInput(
            sourceContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(
                prompt: BundesligaPredictionContractTestData.Prompt(fallback: true)));
        var changedPolicy = PredictionCopyPolicyIdentityV2.Create(
            "different-copy-policy-v2", BundesligaPredictionContractTestData.ShaB,
            BundesligaPredictionContractTestData.MatchRoute,
            BundesligaPredictionContractTestData.MatchRoute,
            "pes-squad", "pes-squad");
        var changedPolicyInput = BundesligaPredictionContractTestData.MatchCopyInput(
            sourceContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(policy: changedPolicy));
        var changedBasisInput = BundesligaPredictionContractTestData.MatchCopyInput(
            sourceContract: BundesligaPredictionContractTestData.MatchCompatibilityContract(
                resultBasis: PredictionResultBasisIdentityV2.Create(
                    ResultBasis.RegularTime90Minutes, "different-basis-contract-v2",
                    BundesligaPredictionContractTestData.ShaB)));
        var baseline = BundesligaPredictionContractTestData.MatchCopyInput();
        var changedModel = PredictionModelConfig.Create(
            "different-model", "high", 9_000,
            BundesligaPredictionContractTestData.MatchPrompt, 3);
        var changedModelCurrent = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            baseline.SourceCurrent.Authority, baseline.SourceCurrent.Snapshot, changedModel,
            baseline.SourceCurrent.Identity, BundesligaPredictionContractTestData.Routes());
        var changedModelInput = PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            baseline.TargetCurrent, changedModelCurrent, baseline.PostingSeed, baseline.SourceSeed,
            baseline.Binding, baseline.BindingEntry, baseline.TargetContract,
            BundesligaPredictionContractTestData.MatchCompatibilityContract(model: changedModel));

        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(changedPromptInput).Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.PromptModelIdentityMismatch);
        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(changedPolicyInput).Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.CopyPolicyIdentityMismatch);
        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(changedBasisInput).Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.ResultBasisMismatch);
        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(changedModelInput).Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.PromptModelIdentityMismatch);
    }

    [Test]
    public async Task Bonus_option_meaning_identity_is_separate_from_total_id_projection()
    {
        var input = BundesligaPredictionContractTestData.BonusCopyInput(
            sourceContract: BundesligaPredictionContractTestData.BonusCompatibilityContract(
                optionMeaning: PredictionOptionMeaningIdentityV2.Create(
                    "different-option-meaning-v2", BundesligaPredictionContractTestData.ShaC)));
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);

        await Assert.That(decision.Succeeded).IsFalse();
        await Assert.That(decision.Failure)
            .IsEqualTo(PredictionCopyCompatibilityV2Failure.OptionMeaningIdentityMismatch);
        await Assert.That(decision.OptionProjection).IsEmpty();
    }

    [Test]
    public async Task Accepted_decision_cannot_be_reused_across_binding_generations()
    {
        var posting = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var source = BundesligaPredictionContractTestData.Seed("pes-squad");
        var first = BundesligaPredictionContractTestData.Binding(posting, source);
        var second = BundesligaCopyBindingGeneration.Create(
            posting.PostingCommunity, source.PostingCommunity, 2,
            BundesligaGenerationPredecessor.Create(1, first.CanonicalSha256),
            "synthetic-copy-evidence-g2", posting, source, first.Entries);
        var firstInput = BundesligaPredictionContractTestData.MatchCopyInput(posting, source, first);
        var secondInput = BundesligaPredictionContractTestData.MatchCopyInput(posting, source, second);
        var firstDecision = PredictionCopyCompatibilityV2.Evaluate(firstInput);

        await Assert.That(firstDecision.Succeeded).IsTrue();
        await Assert.That(() => BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(secondInput, firstDecision))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Wrong_source_context_or_authority_cannot_form_compatibility_input()
    {
        var sourceSeed = BundesligaPredictionContractTestData.Seed("pes-squad");
        var wrongContext = BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            "pes-squad", "pes-squad", "wrong-context",
            sourceSeed.Reference, sourceSeed.Reference);

        await Assert.That(() => BundesligaPredictionContractTestData.MatchCopyInput(
            sourceSeed: sourceSeed,
            sourceAuthority: wrongContext)).Throws<InvalidDataException>();

        var copyAsSource = BundesligaPredictionContractTestData.CopyAuthority(
            BundesligaPredictionContractTestData.Seed("relaxdays-tippt"), sourceSeed);
        await Assert.That(() => BundesligaPredictionContractTestData.MatchCopyInput(
            sourceSeed: sourceSeed,
            sourceAuthority: copyAsSource)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Decision_fingerprint_changes_with_current_generation_input_identity()
    {
        var baseline = BundesligaPredictionContractTestData.MatchCopyInput();
        var changedSource = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            baseline.SourceCurrent.Authority,
            baseline.SourceCurrent.Snapshot,
            baseline.SourceCurrent.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(
                generationInput: BundesligaPredictionContractTestData.GenerationInput(
                    "changed-generation-input-v2", BundesligaPredictionContractTestData.ShaB)),
            BundesligaPredictionContractTestData.Routes());
        var changed = PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            baseline.TargetCurrent, changedSource, baseline.PostingSeed, baseline.SourceSeed,
            baseline.Binding, baseline.BindingEntry, baseline.TargetContract, baseline.SourceContract);

        await Assert.That(PredictionCopyCompatibilityV2.Evaluate(changed).BoundFingerprint)
            .IsNotEqualTo(PredictionCopyCompatibilityV2.Evaluate(baseline).BoundFingerprint);
    }
}
