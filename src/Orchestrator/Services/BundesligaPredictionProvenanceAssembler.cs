using EHonda.KicktippAi.Core;
using NodaTime;
using OpenAiIntegration;

namespace Orchestrator.Services;

public interface IBundesligaPredictionProvenanceAssembler
{
    TypedMatchPredictionRecord AssembleDirectMatch(
        BundesligaPreparedCurrent<TypedMatchSnapshot> prepared,
        ObservedMatchPredictionResult observed,
        BundesligaPredictionContextObservationV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex);

    TypedBonusPredictionRecord AssembleDirectBonus(
        BundesligaPreparedCurrent<TypedBonusSnapshot> prepared,
        ObservedBonusPredictionResult observed,
        BundesligaPredictionContextObservationV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex);

    TypedMatchCopySaveRequest AssembleMatchCopy(
        BundesligaMatchCopyPlan plan,
        BundesligaPredictionContextObservationV2 targetContext,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex);

    TypedBonusCopySaveRequest AssembleBonusCopy(
        BundesligaBonusCopyPlan plan,
        BundesligaPredictionContextObservationV2 targetContext,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex);
}

public sealed class BundesligaPredictionProvenanceAssembler : IBundesligaPredictionProvenanceAssembler
{
    private const string MatchNamespace = "match-predictions-bundesliga-2026-27-typed-v1";
    private const string BonusNamespace = "bonus-predictions-bundesliga-2026-27-typed-v1";

    public TypedMatchPredictionRecord AssembleDirectMatch(
        BundesligaPreparedCurrent<TypedMatchSnapshot> prepared,
        ObservedMatchPredictionResult observed,
        BundesligaPredictionContextObservationV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(observed);
        var current = prepared.Current;
        var provenance = AssembleDirect(
            prepared, current.Snapshot.Key, current.Snapshot.SnapshotHash, observed.Evidence,
            context, MatchNamespace, generationTime, predictionIdentity, repredictionIndex);
        return TypedMatchPredictionRecord.Create(current, observed.Prediction, provenance);
    }

    public TypedBonusPredictionRecord AssembleDirectBonus(
        BundesligaPreparedCurrent<TypedBonusSnapshot> prepared,
        ObservedBonusPredictionResult observed,
        BundesligaPredictionContextObservationV2 context,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(observed);
        var current = prepared.Current;
        var provenance = AssembleDirect(
            prepared, current.Snapshot.Key, current.Snapshot.SnapshotHash, observed.Evidence,
            context, BonusNamespace, generationTime, predictionIdentity, repredictionIndex);
        return TypedBonusPredictionRecord.Create(current, observed.ToBonusPrediction(), provenance);
    }

    public TypedMatchCopySaveRequest AssembleMatchCopy(
        BundesligaMatchCopyPlan plan,
        BundesligaPredictionContextObservationV2 targetContext,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var (request, candidate) = RequireAccepted(plan.Request, plan.Candidate, plan.Decision);
        var provenance = AssembleCopy(request, candidate.SourcePrediction.Provenance,
            targetContext, MatchNamespace, generationTime, predictionIdentity, repredictionIndex);
        return TypedMatchCopySaveRequest.Create(
            request, candidate, candidate.SourcePrediction.Prediction, provenance);
    }

    public TypedBonusCopySaveRequest AssembleBonusCopy(
        BundesligaBonusCopyPlan plan,
        BundesligaPredictionContextObservationV2 targetContext,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var (request, candidate) = RequireAccepted(plan.Request, plan.Candidate, plan.Decision);
        var provenance = AssembleCopy(request, candidate.SourcePrediction.Provenance,
            targetContext, BonusNamespace, generationTime, predictionIdentity, repredictionIndex);
        return TypedBonusCopySaveRequest.Create(
            request, candidate, new BonusPrediction(plan.MappedPostingOptionIds.ToList()), provenance);
    }

    private static PredictionGenerationProvenanceV2 AssembleDirect<TSnapshot>(
        BundesligaPreparedCurrent<TSnapshot> prepared,
        StableLocalItemKey key,
        BundesligaPredictionSnapshotHash hash,
        ObservedPredictionCallEvidence observed,
        BundesligaPredictionContextObservationV2 context,
        string storageNamespace,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex) where TSnapshot : class
    {
        var current = prepared.Current;
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(context);
        prepared.PromptRequirement.RequireExactPolicy(observed.PromptRequirement);
        prepared.PromptRequirement.RequireResolved(observed.Prompt);
        if (current.Authority.Mode != BundesligaPredictionAuthorityMode.Direct
            || observed.ModelConfig != current.ModelConfig)
            throw new InvalidDataException("Direct assembly requires the exact prepared direct model identity.");
        context.Require(current.Authority, current.Identity);
        RequireGenerationTime(generationTime);
        var provenance = PredictionGenerationProvenanceV2.Create(
            current.Authority, storageNamespace, key, hash, key, hash,
            current.Identity.RouteId, current.Identity.ProfileId, current.Identity.GenerationInputContract,
            null, observed.Prompt.Provenance, observed.ModelConfig, observed.ServiceTier,
            context.Provenance, generationTime, predictionIdentity, repredictionIndex, observed.Usage);
        current.RequireMatchingProvenance(provenance);
        return provenance;
    }

    private static PredictionGenerationProvenanceV2 AssembleCopy<TSnapshot>(
        BundesligaTypedCopyRequest<TSnapshot> request,
        PredictionGenerationProvenanceV2 source,
        BundesligaPredictionContextObservationV2 targetContext,
        string storageNamespace,
        Instant generationTime,
        string predictionIdentity,
        int repredictionIndex) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetContext);
        targetContext.Require(request.TargetCurrent.Authority, request.TargetCurrent.Identity);
        RequireGenerationTime(generationTime);
        var target = request.TargetCurrent.Snapshot switch
        {
            TypedMatchSnapshot match => (match.Key, match.SnapshotHash),
            TypedBonusSnapshot bonus => (bonus.Key, bonus.SnapshotHash),
            _ => throw new InvalidDataException("Unsupported copy snapshot type.")
        };
        var provenance = PredictionGenerationProvenanceV2.Create(
            request.TargetCurrent.Authority, storageNamespace, target.Key, target.SnapshotHash,
            source.PostingKey, source.PostingSnapshotHash,
            request.TargetCurrent.Identity.RouteId, request.TargetCurrent.Identity.ProfileId,
            request.TargetCurrent.Identity.GenerationInputContract, source.PredictionIdentity,
            source.Prompt, source.ModelConfig, source.ServiceTier, targetContext.Provenance,
            generationTime, predictionIdentity, repredictionIndex,
            new PredictionGenerationUsageV2(0, 0, 0));
        request.RequireMatchingCopyProvenance(provenance);
        return provenance;
    }

    private static (TRequest Request, TCandidate Candidate) RequireAccepted<TRequest, TCandidate>(
        TRequest? request, TCandidate? candidate, PredictionCopyCompatibilityV2Decision decision)
        where TRequest : class where TCandidate : class
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.Succeeded || request is null || candidate is null)
            throw new InvalidDataException("Copy provenance assembly requires an accepted complete copy plan.");
        return (request, candidate);
    }

    private static void RequireGenerationTime(Instant value)
    {
        if (value == default || value == Instant.MinValue || value == Instant.MaxValue)
            throw new InvalidDataException("Generation time must be an exact non-sentinel instant.");
    }
}
