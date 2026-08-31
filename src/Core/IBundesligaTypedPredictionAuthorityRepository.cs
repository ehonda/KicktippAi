using System.Collections.Immutable;
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

    internal void RequireMatchingSourceProvenance(PredictionGenerationProvenanceV2 provenance) =>
        SourceCurrent.RequireMatchingProvenance(provenance);
}

public sealed class TypedMatchPredictionRecord
{
    private TypedMatchPredictionRecord(Prediction prediction, PredictionGenerationProvenanceV2 provenance) =>
        (Prediction, Provenance) = (prediction, provenance);

    public Prediction Prediction { get; }
    public PredictionGenerationProvenanceV2 Provenance { get; }

    public static TypedMatchPredictionRecord Create(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> current,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(prediction);
        ArgumentNullException.ThrowIfNull(provenance);
        current.RequireMatchingProvenance(provenance);
        if (prediction.HomeGoals < 0 || prediction.AwayGoals < 0)
        {
            throw new InvalidDataException("Typed match prediction goals cannot be negative.");
        }
        return new TypedMatchPredictionRecord(CopyPrediction(prediction), provenance);
    }

    private static Prediction CopyPrediction(Prediction prediction)
    {
        if (prediction.Justification is not { } justification) return new Prediction(prediction.HomeGoals, prediction.AwayGoals);
        ArgumentNullException.ThrowIfNull(justification.ContextSources);
        ArgumentNullException.ThrowIfNull(justification.ContextSources.MostValuable);
        ArgumentNullException.ThrowIfNull(justification.ContextSources.LeastValuable);
        ArgumentNullException.ThrowIfNull(justification.Uncertainties);
        BundesligaPredictionContractValidation.ExactText(justification.KeyReasoning, "keyReasoning");
        var most = CopySources(justification.ContextSources.MostValuable);
        var least = CopySources(justification.ContextSources.LeastValuable);
        var uncertainties = justification.Uncertainties.Select(value =>
        {
            BundesligaPredictionContractValidation.ExactText(value, "uncertainty");
            return value;
        }).ToImmutableArray();
        return new Prediction(prediction.HomeGoals, prediction.AwayGoals,
            new PredictionJustification(justification.KeyReasoning,
                new PredictionJustificationContextSources(most, least), uncertainties));
    }

    private static ImmutableArray<PredictionJustificationContextSource> CopySources(
        IReadOnlyList<PredictionJustificationContextSource> sources) => sources.Select(source =>
        {
            if (source is null) throw new InvalidDataException("Prediction context source cannot be null.");
            BundesligaPredictionContractValidation.Identifier(source.DocumentName, "documentName");
            BundesligaPredictionContractValidation.ExactText(source.Details, "details");
            return new PredictionJustificationContextSource(source.DocumentName, source.Details);
        }).ToImmutableArray();
}

public sealed class TypedBonusPredictionRecord
{
    private readonly ImmutableArray<string> _selectedOptionIds;
    private TypedBonusPredictionRecord(IEnumerable<string> selectedOptionIds, PredictionGenerationProvenanceV2 provenance)
    {
        _selectedOptionIds = selectedOptionIds.ToImmutableArray();
        Provenance = provenance;
    }

    public IReadOnlyList<string> SelectedOptionIds => _selectedOptionIds;
    public PredictionGenerationProvenanceV2 Provenance { get; }
    public BonusPrediction ToBonusPrediction() => new(_selectedOptionIds.ToList());

    public static TypedBonusPredictionRecord Create(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> current,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(prediction);
        ArgumentNullException.ThrowIfNull(prediction.SelectedOptionIds);
        ArgumentNullException.ThrowIfNull(provenance);
        current.RequireMatchingProvenance(provenance);
        var selected = prediction.SelectedOptionIds.ToArray();
        if (selected.Length == 0 || selected.Length > current.Snapshot.MaxSelections
            || selected.Any(string.IsNullOrWhiteSpace)
            || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length
            || selected.Any(id => current.Snapshot.Options.All(option => !string.Equals(option.Id, id, StringComparison.Ordinal))))
        {
            throw new InvalidDataException("Typed bonus prediction selections must be exact, unique, and within the snapshot limit.");
        }
        return new TypedBonusPredictionRecord(selected, provenance);
    }
}

public sealed class TypedPredictionMetadataV2
{
    private TypedPredictionMetadataV2(
        string predictionIdentity, int repredictionIndex, Instant createdAt,
        PredictionGenerationProvenanceV2 provenance) =>
        (PredictionIdentity, RepredictionIndex, CreatedAt, Provenance) =
        (predictionIdentity, repredictionIndex, createdAt, provenance);

    public string PredictionIdentity { get; }
    public int RepredictionIndex { get; }
    public Instant CreatedAt { get; }
    public PredictionGenerationProvenanceV2 Provenance { get; }

    public static TypedPredictionMetadataV2 Create<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> current,
        string predictionIdentity,
        int repredictionIndex,
        Instant createdAt,
        Instant observedAt,
        PredictionGenerationProvenanceV2 provenance) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(provenance);
        BundesligaPredictionContractValidation.Identifier(predictionIdentity, nameof(predictionIdentity));
        current.RequireMatchingProvenance(provenance);
        if (repredictionIndex < 0 || repredictionIndex == int.MaxValue
            || repredictionIndex != provenance.RepredictionIndex)
        {
            throw new InvalidDataException("Typed prediction metadata has an invalid or inconsistent reprediction index.");
        }
        if (!string.Equals(predictionIdentity, provenance.PredictionIdentity, StringComparison.Ordinal)
            || createdAt == default || createdAt == Instant.MinValue || createdAt == Instant.MaxValue
            || createdAt != provenance.GenerationTime || createdAt > observedAt)
        {
            throw new InvalidDataException("Typed prediction metadata identity or timestamp is not current-authoritative.");
        }
        return new TypedPredictionMetadataV2(predictionIdentity, repredictionIndex, createdAt, provenance);
    }
}

public sealed class TypedMatchCopyCandidate
{
    private TypedMatchCopyCandidate(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> sourceCurrent,
        TypedMatchPredictionRecord sourcePrediction, string copyRequestFingerprint) =>
        (SourceCurrent, SourcePrediction, CopyRequestFingerprint) =
        (sourceCurrent, sourcePrediction, copyRequestFingerprint);
    public BundesligaTypedCurrentRequest<TypedMatchSnapshot> SourceCurrent { get; }
    public TypedMatchSnapshot SourceSnapshot => SourceCurrent.Snapshot;
    public TypedMatchPredictionRecord SourcePrediction { get; }
    public string CopyRequestFingerprint { get; }

    public static TypedMatchCopyCandidate Create(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> request,
        TypedMatchPredictionRecord sourcePrediction)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sourcePrediction);
        request.RequireMatchingSourceProvenance(sourcePrediction.Provenance);
        return new TypedMatchCopyCandidate(
            request.SourceCurrent, sourcePrediction, request.Decision.BoundFingerprint);
    }
}

public sealed class TypedBonusCopyCandidate
{
    private TypedBonusCopyCandidate(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> sourceCurrent,
        TypedBonusPredictionRecord sourcePrediction, string copyRequestFingerprint) =>
        (SourceCurrent, SourcePrediction, CopyRequestFingerprint) =
        (sourceCurrent, sourcePrediction, copyRequestFingerprint);
    public BundesligaTypedCurrentRequest<TypedBonusSnapshot> SourceCurrent { get; }
    public TypedBonusSnapshot SourceSnapshot => SourceCurrent.Snapshot;
    public TypedBonusPredictionRecord SourcePrediction { get; }
    public string CopyRequestFingerprint { get; }

    public static TypedBonusCopyCandidate Create(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> request,
        TypedBonusPredictionRecord sourcePrediction)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sourcePrediction);
        request.RequireMatchingSourceProvenance(sourcePrediction.Provenance);
        return new TypedBonusCopyCandidate(
            request.SourceCurrent, sourcePrediction, request.Decision.BoundFingerprint);
    }
}

public sealed class TypedMatchCopySaveRequest
{
    private TypedMatchCopySaveRequest(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> copyRequest,
        TypedMatchCopyCandidate sourceCandidate,
        Prediction prediction,
        PredictionGenerationProvenanceV2 targetProvenance) =>
        (CopyRequest, SourceCandidate, Prediction, TargetProvenance) =
        (copyRequest, sourceCandidate, prediction, targetProvenance);
    public BundesligaTypedCopyRequest<TypedMatchSnapshot> CopyRequest { get; }
    public TypedMatchCopyCandidate SourceCandidate { get; }
    public Prediction Prediction { get; }
    public PredictionGenerationProvenanceV2 TargetProvenance { get; }

    public static TypedMatchCopySaveRequest Create(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> copyRequest,
        TypedMatchCopyCandidate sourceCandidate,
        Prediction prediction,
        PredictionGenerationProvenanceV2 targetProvenance)
    {
        ArgumentNullException.ThrowIfNull(copyRequest);
        ArgumentNullException.ThrowIfNull(sourceCandidate);
        ArgumentNullException.ThrowIfNull(prediction);
        ArgumentNullException.ThrowIfNull(targetProvenance);
        copyRequest.RequireMatchingTargetProvenance(targetProvenance);
        copyRequest.RequireMatchingSourceProvenance(sourceCandidate.SourcePrediction.Provenance);
        if (!ReferenceEquals(sourceCandidate.SourceCurrent, copyRequest.SourceCurrent)
            || !string.Equals(sourceCandidate.CopyRequestFingerprint, copyRequest.Decision.BoundFingerprint, StringComparison.Ordinal)
            || !string.Equals(targetProvenance.SourcePredictionIdentity,
                sourceCandidate.SourcePrediction.Provenance.PredictionIdentity, StringComparison.Ordinal)
            || !PredictionContentEquality.Equals(prediction, sourceCandidate.SourcePrediction.Prediction))
        {
            throw new InvalidDataException("Copy save source row or source prediction identity is inconsistent.");
        }
        var validated = TypedMatchPredictionRecord.Create(copyRequest.TargetCurrent, prediction, targetProvenance);
        return new TypedMatchCopySaveRequest(copyRequest, sourceCandidate, validated.Prediction, targetProvenance);
    }
}

public sealed class TypedBonusCopySaveRequest
{
    private TypedBonusCopySaveRequest(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> copyRequest,
        TypedBonusCopyCandidate sourceCandidate,
        IEnumerable<string> selectedOptionIds,
        PredictionGenerationProvenanceV2 targetProvenance)
    {
        CopyRequest = copyRequest;
        SourceCandidate = sourceCandidate;
        SelectedOptionIds = selectedOptionIds.ToImmutableArray();
        TargetProvenance = targetProvenance;
    }
    public BundesligaTypedCopyRequest<TypedBonusSnapshot> CopyRequest { get; }
    public TypedBonusCopyCandidate SourceCandidate { get; }
    public IReadOnlyList<string> SelectedOptionIds { get; }
    public PredictionGenerationProvenanceV2 TargetProvenance { get; }

    public static TypedBonusCopySaveRequest Create(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> copyRequest,
        TypedBonusCopyCandidate sourceCandidate,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 targetProvenance)
    {
        ArgumentNullException.ThrowIfNull(copyRequest);
        ArgumentNullException.ThrowIfNull(sourceCandidate);
        ArgumentNullException.ThrowIfNull(prediction);
        ArgumentNullException.ThrowIfNull(targetProvenance);
        copyRequest.RequireMatchingTargetProvenance(targetProvenance);
        copyRequest.RequireMatchingSourceProvenance(sourceCandidate.SourcePrediction.Provenance);
        if (!ReferenceEquals(sourceCandidate.SourceCurrent, copyRequest.SourceCurrent)
            || !string.Equals(sourceCandidate.CopyRequestFingerprint, copyRequest.Decision.BoundFingerprint, StringComparison.Ordinal)
            || !string.Equals(targetProvenance.SourcePredictionIdentity,
                sourceCandidate.SourcePrediction.Provenance.PredictionIdentity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Bonus copy save source row or source prediction identity is inconsistent.");
        }
        var validated = TypedBonusPredictionRecord.Create(copyRequest.TargetCurrent, prediction, targetProvenance);
        var mapped = sourceCandidate.SourcePrediction.SelectedOptionIds.Select(sourceId =>
            copyRequest.Decision.OptionProjection.Single(mapping =>
                string.Equals(mapping.SourceOptionId, sourceId, StringComparison.Ordinal)).PostingOptionId);
        if (!validated.SelectedOptionIds.SequenceEqual(mapped, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Bonus copy save payload does not equal the exact bound option projection.");
        }
        return new TypedBonusCopySaveRequest(copyRequest, sourceCandidate, validated.SelectedOptionIds, targetProvenance);
    }
}

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
        TypedMatchCopySaveRequest request, CancellationToken cancellationToken = default);

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
        TypedBonusCopySaveRequest request, CancellationToken cancellationToken = default);
}
