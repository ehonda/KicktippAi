using NodaTime;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaGenerationInputContractReference
{
    private BundesligaGenerationInputContractReference(string contractId, string sha256) =>
        (ContractId, Sha256) = (contractId, sha256);

    public string ContractId { get; }
    public string Sha256 { get; }

    public static BundesligaGenerationInputContractReference Create(string contractId, string sha256)
    {
        BundesligaPredictionContractValidation.Identifier(contractId, nameof(contractId));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new BundesligaGenerationInputContractReference(contractId, sha256);
    }
}

public sealed record BundesligaTypedCurrentIdentity
{
    private BundesligaTypedCurrentIdentity(
        string routeId,
        string profileId,
        BundesligaGenerationInputContractReference generationInputContract) =>
        (RouteId, ProfileId, GenerationInputContract) =
        (routeId, profileId, generationInputContract);

    public string RouteId { get; }
    public string ProfileId { get; }
    public BundesligaGenerationInputContractReference GenerationInputContract { get; }

    public static BundesligaTypedCurrentIdentity Create(
        string routeId,
        string profileId,
        BundesligaGenerationInputContractReference generationInputContract)
    {
        BundesligaPredictionContractValidation.Identifier(routeId, nameof(routeId));
        BundesligaPredictionContractValidation.Identifier(profileId, nameof(profileId));
        ArgumentNullException.ThrowIfNull(generationInputContract);
        return new BundesligaTypedCurrentIdentity(routeId, profileId, generationInputContract);
    }
}

/// <summary>One exact identity shared by every typed current operation.</summary>
public sealed class BundesligaTypedCurrentRequest<TSnapshot>
    where TSnapshot : class
{
    private BundesligaTypedCurrentRequest(
        BundesligaPredictionAuthority authority,
        TSnapshot snapshot,
        PredictionModelConfig modelConfig,
        BundesligaTypedCurrentIdentity identity)
    {
        Authority = authority;
        Snapshot = snapshot;
        ModelConfig = modelConfig;
        Identity = identity;
    }

    public BundesligaPredictionAuthority Authority { get; }
    public TSnapshot Snapshot { get; }
    public PredictionModelConfig ModelConfig { get; }
    public BundesligaTypedCurrentIdentity Identity { get; }

    public static BundesligaTypedCurrentRequest<TSnapshot> Create(
        BundesligaPredictionAuthority authority,
        TSnapshot snapshot,
        PredictionModelConfig modelConfig,
        BundesligaTypedCurrentIdentity identity,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(modelConfig);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(routes);

        var (key, subcompetition, itemKind) = SnapshotIdentity(snapshot);
        if (!string.Equals(key.PostingCommunity, authority.PostingCommunity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Current request snapshot is outside its Posting Community authority.");
        }

        routes.Require(identity.RouteId, itemKind, subcompetition);
        RequirePinnedModel(modelConfig);
        return new BundesligaTypedCurrentRequest<TSnapshot>(authority, snapshot, modelConfig, identity);
    }

    public void RequireMatchingProvenance(PredictionGenerationProvenanceV2 provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        var (key, _, _) = SnapshotIdentity(Snapshot);
        var hash = Snapshot switch
        {
            TypedMatchSnapshot match => match.SnapshotHash,
            TypedBonusSnapshot bonus => bonus.SnapshotHash,
            _ => throw new InvalidDataException("Unsupported typed current snapshot.")
        };
        if (provenance.Authority != Authority
            || provenance.PostingKey != key
            || provenance.PostingSnapshotHash != hash
            || !string.Equals(provenance.RouteId, Identity.RouteId, StringComparison.Ordinal)
            || !string.Equals(provenance.ProfileId, Identity.ProfileId, StringComparison.Ordinal)
            || provenance.GenerationInputContract != Identity.GenerationInputContract
            || provenance.ModelConfig != ModelConfig)
        {
            throw new InvalidDataException("Save provenance does not match the exact typed current request identity.");
        }
    }

    internal static (StableLocalItemKey Key, BundesligaSeasonSubcompetition Subcompetition, BundesligaPredictionItemKind ItemKind)
        SnapshotIdentity(TSnapshot snapshot) => snapshot switch
        {
            TypedMatchSnapshot match => (match.Key, match.Subcompetition, BundesligaPredictionItemKind.Match),
            TypedBonusSnapshot bonus => (bonus.Key, bonus.Subcompetition, BundesligaPredictionItemKind.Bonus),
            _ => throw new InvalidDataException("Only typed Bundesliga match and bonus snapshots are supported.")
        };

    private static void RequirePinnedModel(PredictionModelConfig modelConfig)
    {
        if (!modelConfig.HasPinnedRuntimeIdentity
            || modelConfig.ReasoningEffort is null
            || modelConfig.MaxOutputTokenCount is null
            || modelConfig.PromptName is null
            || modelConfig.PromptVersion is null)
        {
            throw new InvalidDataException("Typed current identity requires a fully pinned model and prompt configuration.");
        }
    }
}

/// <summary>The one request shape shared by copy-candidate and save-copy.</summary>
public sealed class BundesligaTypedCopyRequest<TSnapshot>
    where TSnapshot : class
{
    private BundesligaTypedCopyRequest(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        PredictionCopyCompatibilityV2Decision decision)
    {
        Input = input;
        Decision = decision;
    }

    public PredictionCopyCompatibilityV2Input<TSnapshot> Input { get; }
    public BundesligaTypedCurrentRequest<TSnapshot> TargetCurrent => Input.TargetCurrent;
    public BundesligaTypedCurrentRequest<TSnapshot> SourceCurrent => Input.SourceCurrent;
    public BundesligaCopyBindingGeneration Binding => Input.Binding;
    public BundesligaCopyBindingEntry BindingEntry => Input.BindingEntry;
    public PredictionCopyCompatibilityV2Decision Decision { get; }

    public static BundesligaTypedCopyRequest<TSnapshot> Create(
        PredictionCopyCompatibilityV2Input<TSnapshot> input,
        PredictionCopyCompatibilityV2Decision decision)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.Succeeded || !decision.IsBoundTo(input))
        {
            throw new InvalidDataException(
                "Copy request requires the accepted compatibility decision bound to this exact authority, binding, and current identity.");
        }

        return new BundesligaTypedCopyRequest<TSnapshot>(input, decision);
    }

    public void RequireMatchingTargetProvenance(PredictionGenerationProvenanceV2 provenance) =>
        TargetCurrent.RequireMatchingProvenance(provenance);
}

public sealed record TypedMatchPredictionRecord(Prediction Prediction, PredictionGenerationProvenanceV2 Provenance);
public sealed record TypedBonusPredictionRecord(BonusPrediction Prediction, PredictionGenerationProvenanceV2 Provenance);
public sealed record TypedPredictionMetadataV2(
    string PredictionIdentity,
    int RepredictionIndex,
    Instant CreatedAt,
    PredictionGenerationProvenanceV2 Provenance);
public sealed record TypedMatchCopyCandidate(TypedMatchSnapshot SourceSnapshot, TypedMatchPredictionRecord SourcePrediction);
public sealed record TypedBonusCopyCandidate(TypedBonusSnapshot SourceSnapshot, TypedBonusPredictionRecord SourcePrediction);

public interface IBundesligaTypedPredictionAuthorityRepository
{
    Task<TypedMatchPredictionRecord?> GetCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request, CancellationToken cancellationToken = default);
    Task<TypedPredictionMetadataV2?> GetCurrentTypedMatchPredictionMetadataAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request, CancellationToken cancellationToken = default);
    Task<bool> HasCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request, CancellationToken cancellationToken = default);
    Task<int> GetCurrentTypedMatchRepredictionIndexAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        Prediction prediction, PredictionGenerationProvenanceV2 provenance, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedMatchRepredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        Prediction prediction, PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex, int maximumRepredictions, CancellationToken cancellationToken = default);
    Task<TypedMatchCopyCandidate?> GetTypedMatchCopyCandidateAsync(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> request, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedMatchCopyAsync(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> request,
        Prediction prediction, PredictionGenerationProvenanceV2 provenance, CancellationToken cancellationToken = default);

    Task<TypedBonusPredictionRecord?> GetCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request, CancellationToken cancellationToken = default);
    Task<TypedPredictionMetadataV2?> GetCurrentTypedBonusPredictionMetadataAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request, CancellationToken cancellationToken = default);
    Task<bool> HasCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request, CancellationToken cancellationToken = default);
    Task<int> GetCurrentTypedBonusRepredictionIndexAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        BonusPrediction prediction, PredictionGenerationProvenanceV2 provenance, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedBonusRepredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        BonusPrediction prediction, PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex, int maximumRepredictions, CancellationToken cancellationToken = default);
    Task<TypedBonusCopyCandidate?> GetTypedBonusCopyCandidateAsync(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> request, CancellationToken cancellationToken = default);
    Task SaveCurrentTypedBonusCopyAsync(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> request,
        BonusPrediction prediction, PredictionGenerationProvenanceV2 provenance, CancellationToken cancellationToken = default);
}
