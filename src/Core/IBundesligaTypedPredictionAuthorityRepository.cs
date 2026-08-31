using NodaTime;

namespace EHonda.KicktippAi.Core;

public sealed record TypedMatchPredictionRecord(
    Prediction Prediction,
    PredictionGenerationProvenanceV2 Provenance);

public sealed record TypedBonusPredictionRecord(
    BonusPrediction Prediction,
    PredictionGenerationProvenanceV2 Provenance);

public sealed record TypedPredictionMetadataV2(
    string PredictionIdentity,
    int RepredictionIndex,
    Instant CreatedAt,
    PredictionGenerationProvenanceV2 Provenance);

public sealed record TypedMatchCopyCandidate(
    TypedMatchSnapshot SourceSnapshot,
    TypedMatchPredictionRecord SourcePrediction);

public sealed record TypedBonusCopyCandidate(
    TypedBonusSnapshot SourceSnapshot,
    TypedBonusPredictionRecord SourcePrediction);

/// <summary>
/// The only prediction repository capability admitted to Bundesliga 2026/27
/// current commands. Every call is exact-authority, exact-snapshot, and
/// exact-model addressed; every write requires complete immutable provenance.
/// </summary>
public interface IBundesligaTypedPredictionAuthorityRepository
{
    Task<TypedMatchPredictionRecord?> GetCurrentTypedMatchPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<TypedPredictionMetadataV2?> GetCurrentTypedMatchPredictionMetadataAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<bool> HasCurrentTypedMatchPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentTypedMatchRepredictionIndexAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedMatchPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedMatchRepredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot snapshot,
        PredictionModelConfig modelConfig,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex,
        int maximumRepredictions,
        CancellationToken cancellationToken = default);

    Task<TypedMatchCopyCandidate?> GetTypedMatchCopyCandidateAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot postingSnapshot,
        TypedMatchSnapshot sourceSnapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedMatchCopyAsync(
        BundesligaPredictionAuthority authority,
        TypedMatchSnapshot postingSnapshot,
        TypedMatchSnapshot sourceSnapshot,
        PredictionModelConfig modelConfig,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        PredictionCopyCompatibilityV2Result compatibility,
        CancellationToken cancellationToken = default);

    Task<TypedBonusPredictionRecord?> GetCurrentTypedBonusPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<TypedPredictionMetadataV2?> GetCurrentTypedBonusPredictionMetadataAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<bool> HasCurrentTypedBonusPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentTypedBonusRepredictionIndexAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedBonusPredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedBonusRepredictionAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot snapshot,
        PredictionModelConfig modelConfig,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex,
        int maximumRepredictions,
        CancellationToken cancellationToken = default);

    Task<TypedBonusCopyCandidate?> GetTypedBonusCopyCandidateAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot postingSnapshot,
        TypedBonusSnapshot sourceSnapshot,
        PredictionModelConfig modelConfig,
        CancellationToken cancellationToken = default);

    Task SaveCurrentTypedBonusCopyAsync(
        BundesligaPredictionAuthority authority,
        TypedBonusSnapshot postingSnapshot,
        TypedBonusSnapshot sourceSnapshot,
        PredictionModelConfig modelConfig,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        PredictionCopyCompatibilityV2Result compatibility,
        CancellationToken cancellationToken = default);
}
