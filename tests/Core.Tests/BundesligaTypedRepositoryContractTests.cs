using EHonda.KicktippAi.Core;
using NodaTime;

namespace Core.Tests;

public class BundesligaTypedRepositoryContractTests
{
    [Test]
    public async Task Current_request_is_the_complete_compile_time_shape_for_every_operation_family()
    {
        var methods = typeof(IBundesligaTypedPredictionAuthorityRepository).GetMethods();
        await Assert.That(methods.Length).IsEqualTo(16);

        foreach (var method in methods.Where(method => !method.Name.Contains("Copy", StringComparison.Ordinal)))
        {
            var requestType = method.Name.Contains("Match", StringComparison.Ordinal)
                ? typeof(BundesligaTypedCurrentRequest<TypedMatchSnapshot>)
                : typeof(BundesligaTypedCurrentRequest<TypedBonusSnapshot>);
            await Assert.That(method.GetParameters()[0].ParameterType).IsEqualTo(requestType);
            await Assert.That(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))).IsFalse();
        }

        foreach (var method in methods.Where(method => method.Name.Contains("Copy", StringComparison.Ordinal)))
        {
            var requestType = method.Name switch
            {
                "SaveCurrentTypedMatchCopyAsync" => typeof(TypedMatchCopySaveRequest),
                "SaveCurrentTypedBonusCopyAsync" => typeof(TypedBonusCopySaveRequest),
                _ when method.Name.Contains("Match", StringComparison.Ordinal) =>
                    typeof(BundesligaTypedCopyRequest<TypedMatchSnapshot>),
                _ => typeof(BundesligaTypedCopyRequest<TypedBonusSnapshot>)
            };
            await Assert.That(method.GetParameters()[0].ParameterType).IsEqualTo(requestType);
            await Assert.That(method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(PredictionCopyCompatibilityV2Decision))).IsFalse();
        }
    }

    [Test]
    public async Task Result_and_metadata_factories_reject_null_drift_indexes_and_timestamps()
    {
        var snapshot = BundesligaPredictionContractTestData.Match();
        var current = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            BundesligaPredictionContractTestData.DirectAuthority(), snapshot,
            BundesligaPredictionContractTestData.Model(),
            BundesligaPredictionContractTestData.CurrentIdentity(),
            BundesligaPredictionContractTestData.Routes());
        var provenance = BundesligaPredictionContractTestData.DirectProvenance(snapshot);

        await Assert.That(() => TypedMatchPredictionRecord.Create(current, null!, provenance))
            .Throws<ArgumentNullException>();
        await Assert.That(() => TypedMatchPredictionRecord.Create(current, new Prediction(1, 0), null!))
            .Throws<ArgumentNullException>();
        var changedSnapshot = BundesligaPredictionContractTestData.Match(
            scheduledInstant: "2026-09-01T19:00:00Z");
        var changedCurrent = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            current.Authority, changedSnapshot, current.ModelConfig, current.Identity,
            BundesligaPredictionContractTestData.Routes());
        await Assert.That(() => TypedMatchPredictionRecord.Create(
            changedCurrent, new Prediction(1, 0), provenance)).Throws<InvalidDataException>();
        var changedAuthority = BundesligaPredictionAuthority.CreateDirect(
            CompetitionIds.Bundesliga2026_27,
            BundesligaPredictionAuthority.AuthorityEpochValue,
            "pes-squad", "pes-squad", "other-context",
            current.Authority.PostingSeed, current.Authority.SourceSeed);
        var changedAuthorityCurrent = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            changedAuthority, snapshot, current.ModelConfig, current.Identity,
            BundesligaPredictionContractTestData.Routes());
        await Assert.That(() => TypedMatchPredictionRecord.Create(
            changedAuthorityCurrent, new Prediction(1, 0), provenance)).Throws<InvalidDataException>();
        await Assert.That(() => TypedPredictionMetadataV2.Create(
            current, provenance.PredictionIdentity, -1, provenance.GenerationTime,
            provenance.GenerationTime, provenance)).Throws<InvalidDataException>();
        await Assert.That(() => TypedPredictionMetadataV2.Create(
            current, provenance.PredictionIdentity, int.MaxValue, provenance.GenerationTime,
            provenance.GenerationTime, provenance)).Throws<InvalidDataException>();
        await Assert.That(() => TypedPredictionMetadataV2.Create(
            current, provenance.PredictionIdentity, 0, default,
            provenance.GenerationTime, provenance)).Throws<InvalidDataException>();
        await Assert.That(() => TypedPredictionMetadataV2.Create(
            current, provenance.PredictionIdentity, 0, provenance.GenerationTime,
            provenance.GenerationTime - Duration.FromSeconds(1), provenance)).Throws<InvalidDataException>();

        var metadata = TypedPredictionMetadataV2.Create(
            current, provenance.PredictionIdentity, 0, provenance.GenerationTime,
            provenance.GenerationTime, provenance);
        await Assert.That(metadata.PredictionIdentity).IsEqualTo(provenance.PredictionIdentity);
    }

    [Test]
    public async Task Bonus_result_defensively_copies_caller_owned_selection_list()
    {
        var snapshot = BundesligaPredictionContractTestData.Bonus();
        var current = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            BundesligaPredictionContractTestData.DirectAuthority(
                BundesligaPredictionContractTestData.Seed()),
            snapshot,
            BundesligaPredictionContractTestData.Model(),
            BundesligaPredictionContractTestData.CurrentIdentity(
                BundesligaPredictionContractTestData.BonusRoute, "bundesliga-bonus-primary-v1"),
            BundesligaPredictionContractTestData.Routes());
        var provenance = BundesligaPredictionContractTestData.BonusDirectProvenance(current);
        var selected = new List<string> { "a" };
        var record = TypedBonusPredictionRecord.Create(current, new BonusPrediction(selected), provenance);
        selected[0] = "b";
        selected.Add("b");

        await Assert.That(record.SelectedOptionIds).IsEquivalentTo(new[] { "a" });
        await Assert.That(record.ToBonusPrediction().SelectedOptionIds).IsEquivalentTo(new[] { "a" });

        var copyInput = BundesligaPredictionContractTestData.BonusCopyInput();
        var copyRequest = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(
            copyInput, PredictionCopyCompatibilityV2.Evaluate(copyInput));
        var sourceRow = TypedBonusPredictionRecord.Create(
            copyInput.SourceCurrent,
            new BonusPrediction(new List<string> { "a" }),
            BundesligaPredictionContractTestData.BonusDirectProvenance(copyInput.SourceCurrent));
        var candidate = TypedBonusCopyCandidate.Create(copyRequest, sourceRow);
        var targetSelections = new List<string> { "a" };
        var save = TypedBonusCopySaveRequest.Create(
            copyRequest, candidate, new BonusPrediction(targetSelections),
            BundesligaPredictionContractTestData.BonusCopyProvenance(copyInput));
        targetSelections[0] = "b";
        await Assert.That(save.SelectedOptionIds).IsEquivalentTo(new[] { "a" });

        var wrongBonusSourceKey = CreateForgedBonusCopyProvenance(
            copyInput,
            BundesligaPredictionContractTestData.BonusKey("pes-squad", "85"),
            copyInput.SourceCurrent.Snapshot.SnapshotHash);
        var wrongBonusSourceHash = CreateForgedBonusCopyProvenance(
            copyInput,
            copyInput.SourceCurrent.Snapshot.Key,
            BundesligaPredictionSnapshotHash.Create(
                TypedBonusSnapshot.SchemaVersionValue,
                BundesligaPredictionContractTestData.ShaA));
        await Assert.That(() => TypedBonusCopySaveRequest.Create(
            copyRequest, candidate, new BonusPrediction(new List<string> { "a" }), wrongBonusSourceKey))
            .Throws<InvalidDataException>();
        await Assert.That(() => TypedBonusCopySaveRequest.Create(
            copyRequest, candidate, new BonusPrediction(new List<string> { "a" }), wrongBonusSourceHash))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Current_request_validates_route_profile_generation_input_and_save_provenance()
    {
        var snapshot = BundesligaPredictionContractTestData.Match();
        var current = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            BundesligaPredictionContractTestData.DirectAuthority(), snapshot,
            BundesligaPredictionContractTestData.Model(),
            BundesligaPredictionContractTestData.CurrentIdentity(),
            BundesligaPredictionContractTestData.Routes());
        current.RequireMatchingProvenance(BundesligaPredictionContractTestData.DirectProvenance(snapshot));

        await Assert.That(current.Identity.GenerationInputContract)
            .IsEqualTo(BundesligaPredictionContractTestData.GenerationInput());
        await Assert.That(() => BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            current.Authority, snapshot, current.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(BundesligaPredictionContractTestData.BonusRoute),
            BundesligaPredictionContractTestData.Routes())).Throws<InvalidDataException>();

        var wrongProfile = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            current.Authority, snapshot, current.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(profileId: "wrong-profile"),
            BundesligaPredictionContractTestData.Routes());
        await Assert.That(() => wrongProfile.RequireMatchingProvenance(
            BundesligaPredictionContractTestData.DirectProvenance(snapshot))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Copy_request_contains_source_and_target_authority_binding_current_identity_and_bound_decision()
    {
        var input = BundesligaPredictionContractTestData.MatchCopyInput();
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);
        var request = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(input, decision);
        request.RequireMatchingTargetProvenance(BundesligaPredictionContractTestData.CopyProvenance(input));

        await Assert.That(request.TargetCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Copy);
        await Assert.That(request.SourceCurrent.Authority.Mode).IsEqualTo(BundesligaPredictionAuthorityMode.Direct);
        await Assert.That(request.TargetCurrent.Identity.RouteId).IsEqualTo(request.BindingEntry.RouteId);
        await Assert.That(request.Binding.Reference).IsEqualTo(request.Decision.Binding);
        await Assert.That(request.SourceCurrent.Identity.GenerationInputContract).IsNotNull();

        var wrongIdentity = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            request.TargetCurrent.Authority, request.TargetCurrent.Snapshot,
            request.TargetCurrent.ModelConfig,
            BundesligaPredictionContractTestData.CurrentIdentity(profileId: "wrong-copy-profile"),
            BundesligaPredictionContractTestData.Routes());
        var wrongInput = PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            wrongIdentity, input.SourceCurrent, input.PostingSeed, input.SourceSeed,
            input.Binding, input.BindingEntry,
            BundesligaPredictionContractTestData.MatchCompatibilityContract(wrongIdentity.Authority.CommunityContext),
            input.SourceContract);
        var wrongRequest = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            wrongInput, PredictionCopyCompatibilityV2.Evaluate(wrongInput));
        await Assert.That(() => wrongRequest.RequireMatchingTargetProvenance(
            BundesligaPredictionContractTestData.CopyProvenance(input))).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Copy_candidate_and_save_bind_both_source_row_and_target_provenance_to_exact_request()
    {
        var input = BundesligaPredictionContractTestData.MatchCopyInput();
        var request = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            input, PredictionCopyCompatibilityV2.Evaluate(input));
        var sourceProvenance = BundesligaPredictionContractTestData.DirectProvenance(input.SourceCurrent.Snapshot);
        var sourceRow = TypedMatchPredictionRecord.Create(
            input.SourceCurrent, new Prediction(1, 0), sourceProvenance);
        var candidate = TypedMatchCopyCandidate.Create(request, sourceRow);
        await Assert.That(() => TypedMatchCopyCandidate.Create(request, null!))
            .Throws<ArgumentNullException>();
        var targetProvenance = BundesligaPredictionContractTestData.CopyProvenance(input);
        var save = TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(1, 0), targetProvenance);

        await Assert.That(save.SourceCandidate.SourcePrediction.Provenance)
            .IsEqualTo(sourceProvenance);
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(1, 0), sourceProvenance))
            .Throws<InvalidDataException>();
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(1, 0),
            BundesligaPredictionContractTestData.CopyProvenance(input, "wrong-source-row")))
            .Throws<InvalidDataException>();
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(2, 0), targetProvenance))
            .Throws<InvalidDataException>();
        var wrongMatchSourceKey = CreateForgedMatchCopyProvenance(
            input,
            BundesligaPredictionContractTestData.MatchKey("pes-squad", "43"),
            input.SourceCurrent.Snapshot.SnapshotHash);
        var wrongMatchSourceHash = CreateForgedMatchCopyProvenance(
            input,
            input.SourceCurrent.Snapshot.Key,
            BundesligaPredictionSnapshotHash.Create(
                TypedMatchSnapshot.SchemaVersionValue,
                BundesligaPredictionContractTestData.ShaA));
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(1, 0), wrongMatchSourceKey))
            .Throws<InvalidDataException>();
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            request, candidate, new Prediction(1, 0), wrongMatchSourceHash))
            .Throws<InvalidDataException>();

        var posting = input.PostingSeed;
        var source = input.SourceSeed;
        var nextBinding = BundesligaCopyBindingGeneration.Create(
            posting.PostingCommunity, source.PostingCommunity, 2,
            BundesligaGenerationPredecessor.Create(1, input.Binding.CanonicalSha256),
            "synthetic-copy-evidence-g2", posting, source, input.Binding.Entries);
        var nextInput = BundesligaPredictionContractTestData.MatchCopyInput(posting, source, nextBinding);
        var nextRequest = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            nextInput, PredictionCopyCompatibilityV2.Evaluate(nextInput));
        await Assert.That(() => TypedMatchCopySaveRequest.Create(
            nextRequest, candidate, new Prediction(1, 0),
            BundesligaPredictionContractTestData.CopyProvenance(nextInput)))
            .Throws<InvalidDataException>();
    }

    private static PredictionGenerationProvenanceV2 CreateForgedMatchCopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedMatchSnapshot> input,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceHash) =>
        CreateForgedCopyProvenance(
            input.TargetCurrent,
            sourceKey,
            sourceHash,
            input.SourceCurrent.Snapshot.Key,
            "match-predictions-bundesliga-2026-27-typed-v1",
            "prediction-42-r0",
            "forged-match-copy");

    private static PredictionGenerationProvenanceV2 CreateForgedBonusCopyProvenance(
        PredictionCopyCompatibilityV2Input<TypedBonusSnapshot> input,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceHash) =>
        CreateForgedCopyProvenance(
            input.TargetCurrent,
            sourceKey,
            sourceHash,
            input.SourceCurrent.Snapshot.Key,
            "bonus-predictions-bundesliga-2026-27-typed-v1",
            "bonus-prediction-84",
            "forged-bonus-copy");

    private static PredictionGenerationProvenanceV2 CreateForgedCopyProvenance<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> target,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceHash,
        StableLocalItemKey expectedSourceKey,
        string physicalNamespace,
        string sourcePredictionIdentity,
        string predictionIdentity) where TSnapshot : class
    {
        var (targetKey, targetHash) = target.Snapshot switch
        {
            TypedMatchSnapshot match => (match.Key, match.SnapshotHash),
            TypedBonusSnapshot bonus => (bonus.Key, bonus.SnapshotHash),
            _ => throw new InvalidDataException("Unsupported forged target snapshot.")
        };
        if (!string.Equals(sourceKey.PostingCommunity, expectedSourceKey.PostingCommunity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Hostile source key must stay within the valid source community.");
        }
        return PredictionGenerationProvenanceV2.Create(
            target.Authority,
            physicalNamespace,
            targetKey,
            targetHash,
            sourceKey,
            sourceHash,
            target.Identity.RouteId,
            target.Identity.ProfileId,
            target.Identity.GenerationInputContract,
            sourcePredictionIdentity,
            BundesligaPredictionContractTestData.Prompt(),
            target.ModelConfig,
            PredictionServiceTierProvenanceV2.Create("standard", "standard", false),
            BundesligaPredictionContractTestData.Context(),
            Instant.FromUtc(2026, 8, 31, 12, 0),
            predictionIdentity,
            0,
            new PredictionGenerationUsageV2(0, 0, 0));
    }
}
