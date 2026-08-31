using System.Collections.Immutable;

namespace EHonda.KicktippAi.Core;

public enum PredictionCopyCompatibilityV2Failure
{
    None,
    BindingIdentityMismatch,
    SnapshotHashMismatch,
    RouteMismatch,
    SubcompetitionMismatch,
    ResultBasisMismatch,
    MatchSemanticsMismatch,
    SelectionLimitMismatch,
    RulesOrScoringMismatch,
    PromptModelMismatch,
    OptionMeaningMismatch
}

public sealed record PredictionCopyCompatibilityEvidenceV2
{
    public PredictionCopyCompatibilityEvidenceV2(
        string compatibilityContractId,
        bool rulesAndScoringEquivalent,
        bool promptModelContractEquivalent,
        bool optionMeaningEquivalent)
    {
        BundesligaPredictionContractValidation.Identifier(
            compatibilityContractId,
            nameof(compatibilityContractId));
        CompatibilityContractId = compatibilityContractId;
        RulesAndScoringEquivalent = rulesAndScoringEquivalent;
        PromptModelContractEquivalent = promptModelContractEquivalent;
        OptionMeaningEquivalent = optionMeaningEquivalent;
    }

    public string CompatibilityContractId { get; }
    public bool RulesAndScoringEquivalent { get; }
    public bool PromptModelContractEquivalent { get; }
    public bool OptionMeaningEquivalent { get; }
}

public sealed class PredictionCopyCompatibilityV2Result
{
    private readonly ImmutableArray<BundesligaBonusOptionProjection> _optionProjection;

    private PredictionCopyCompatibilityV2Result(
        bool succeeded,
        PredictionCopyCompatibilityV2Failure failure,
        string compatibilityContractId,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
    {
        Succeeded = succeeded;
        Failure = failure;
        CompatibilityContractId = compatibilityContractId;
        _optionProjection = optionProjection.ToImmutableArray();
    }

    public bool Succeeded { get; }
    public PredictionCopyCompatibilityV2Failure Failure { get; }
    public string CompatibilityContractId { get; }
    public IReadOnlyList<BundesligaBonusOptionProjection> OptionProjection => _optionProjection;

    internal static PredictionCopyCompatibilityV2Result Success(
        string compatibilityContractId,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection) =>
        new(true, PredictionCopyCompatibilityV2Failure.None, compatibilityContractId, optionProjection);

    internal static PredictionCopyCompatibilityV2Result Rejected(
        string compatibilityContractId,
        PredictionCopyCompatibilityV2Failure failure) =>
        new(false, failure, compatibilityContractId, []);
}

/// <summary>Complete typed copy decision. It never returns degraded compatibility.</summary>
public static class PredictionCopyCompatibilityV2
{
    public static PredictionCopyCompatibilityV2Result EvaluateMatch(
        BundesligaCopyBindingEntry binding,
        TypedMatchSnapshot postingSnapshot,
        TypedMatchSnapshot sourceSnapshot,
        string requestedRouteId,
        PredictionCopyCompatibilityEvidenceV2 evidence)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(postingSnapshot);
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(evidence);
        BundesligaPredictionContractValidation.Identifier(requestedRouteId, nameof(requestedRouteId));

        var commonFailure = CommonFailure(
            binding,
            postingSnapshot.Key,
            postingSnapshot.SnapshotHash,
            sourceSnapshot.Key,
            sourceSnapshot.SnapshotHash,
            requestedRouteId,
            postingSnapshot.Subcompetition,
            sourceSnapshot.Subcompetition);
        if (commonFailure != PredictionCopyCompatibilityV2Failure.None)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                commonFailure);
        }

        if (postingSnapshot.ResultBasis != sourceSnapshot.ResultBasis)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.ResultBasisMismatch);
        }

        if (!string.Equals(postingSnapshot.HomeTeam, sourceSnapshot.HomeTeam, StringComparison.Ordinal)
            || !string.Equals(postingSnapshot.AwayTeam, sourceSnapshot.AwayTeam, StringComparison.Ordinal)
            || !string.Equals(postingSnapshot.ExactRound, sourceSnapshot.ExactRound, StringComparison.Ordinal))
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.MatchSemanticsMismatch);
        }

        return EvaluateEvidence(evidence, optionMeaningRequired: false);
    }

    public static PredictionCopyCompatibilityV2Result EvaluateBonus(
        BundesligaCopyBindingEntry binding,
        TypedBonusSnapshot postingSnapshot,
        TypedBonusSnapshot sourceSnapshot,
        string requestedRouteId,
        PredictionCopyCompatibilityEvidenceV2 evidence)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(postingSnapshot);
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(evidence);
        BundesligaPredictionContractValidation.Identifier(requestedRouteId, nameof(requestedRouteId));

        var commonFailure = CommonFailure(
            binding,
            postingSnapshot.Key,
            postingSnapshot.SnapshotHash,
            sourceSnapshot.Key,
            sourceSnapshot.SnapshotHash,
            requestedRouteId,
            postingSnapshot.Subcompetition,
            sourceSnapshot.Subcompetition);
        if (commonFailure != PredictionCopyCompatibilityV2Failure.None)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                commonFailure);
        }

        if (postingSnapshot.MaxSelections != sourceSnapshot.MaxSelections)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.SelectionLimitMismatch);
        }

        try
        {
            BundesligaCopyBindingEntry.EnsureExactProjection(
                sourceSnapshot.Options.Select(option => option.Id),
                postingSnapshot.Options.Select(option => option.Id),
                binding.OptionProjection.ToArray());
        }
        catch (InvalidDataException)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.BindingIdentityMismatch);
        }

        var evidenceResult = EvaluateEvidence(evidence, optionMeaningRequired: true);
        return evidenceResult.Succeeded
            ? PredictionCopyCompatibilityV2Result.Success(
                evidence.CompatibilityContractId,
                binding.OptionProjection)
            : evidenceResult;
    }

    private static PredictionCopyCompatibilityV2Failure CommonFailure(
        BundesligaCopyBindingEntry binding,
        StableLocalItemKey postingKey,
        BundesligaPredictionSnapshotHash postingHash,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceHash,
        string requestedRouteId,
        BundesligaSeasonSubcompetition postingSubcompetition,
        BundesligaSeasonSubcompetition sourceSubcompetition)
    {
        if (binding.PostingKey != postingKey || binding.SourceKey != sourceKey)
        {
            return PredictionCopyCompatibilityV2Failure.BindingIdentityMismatch;
        }

        if (binding.PostingSnapshotHash != postingHash || binding.SourceSnapshotHash != sourceHash)
        {
            return PredictionCopyCompatibilityV2Failure.SnapshotHashMismatch;
        }

        if (!string.Equals(binding.RouteId, requestedRouteId, StringComparison.Ordinal))
        {
            return PredictionCopyCompatibilityV2Failure.RouteMismatch;
        }

        return postingSubcompetition == sourceSubcompetition
            ? PredictionCopyCompatibilityV2Failure.None
            : PredictionCopyCompatibilityV2Failure.SubcompetitionMismatch;
    }

    private static PredictionCopyCompatibilityV2Result EvaluateEvidence(
        PredictionCopyCompatibilityEvidenceV2 evidence,
        bool optionMeaningRequired)
    {
        if (!evidence.RulesAndScoringEquivalent)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.RulesOrScoringMismatch);
        }

        if (!evidence.PromptModelContractEquivalent)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.PromptModelMismatch);
        }

        if (optionMeaningRequired && !evidence.OptionMeaningEquivalent)
        {
            return PredictionCopyCompatibilityV2Result.Rejected(
                evidence.CompatibilityContractId,
                PredictionCopyCompatibilityV2Failure.OptionMeaningMismatch);
        }

        return PredictionCopyCompatibilityV2Result.Success(evidence.CompatibilityContractId, []);
    }
}
