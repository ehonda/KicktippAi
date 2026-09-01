using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Services;

public interface IBundesligaPredictionAuthorityKernel
{
    BundesligaTypedCurrentRequest<TypedMatchSnapshot> PrepareCurrent(
        BundesligaValidatedMatchItem item,
        string selectionId);

    BundesligaTypedCurrentRequest<TypedBonusSnapshot> PrepareCurrent(
        BundesligaValidatedBonusItem item,
        string selectionId);

    Task<BundesligaMatchCopyPlan> PrepareMatchCopyAsync(
        BundesligaValidatedMatchItem targetItem,
        string targetSelectionId,
        BundesligaCopyBindingGeneration binding,
        BundesligaValidatedMatchItem sourceItem,
        string sourceSelectionId,
        CancellationToken cancellationToken = default);

    Task<BundesligaBonusCopyPlan> PrepareBonusCopyAsync(
        BundesligaValidatedBonusItem targetItem,
        string targetSelectionId,
        BundesligaCopyBindingGeneration binding,
        BundesligaValidatedBonusItem sourceItem,
        string sourceSelectionId,
        CancellationToken cancellationToken = default);
}

public sealed class BundesligaMatchCopyPlan
{
    private BundesligaMatchCopyPlan(
        PredictionCopyCompatibilityV2Decision decision,
        BundesligaTypedCopyRequest<TypedMatchSnapshot>? request,
        TypedMatchCopyCandidate? candidate,
        Prediction? prediction) =>
        (Decision, Request, Candidate, Prediction) = (decision, request, candidate, prediction);

    public PredictionCopyCompatibilityV2Decision Decision { get; }
    public bool IsAccepted => Request is not null;
    public BundesligaTypedCopyRequest<TypedMatchSnapshot>? Request { get; }
    public TypedMatchCopyCandidate? Candidate { get; }
    public Prediction? Prediction { get; }

    internal static BundesligaMatchCopyPlan Rejected(PredictionCopyCompatibilityV2Decision decision) =>
        new(decision, null, null, null);

    internal static BundesligaMatchCopyPlan Accepted(
        PredictionCopyCompatibilityV2Decision decision,
        BundesligaTypedCopyRequest<TypedMatchSnapshot> request,
        TypedMatchCopyCandidate candidate) =>
        new(decision, request, candidate, candidate.SourcePrediction.Prediction);
}

public sealed class BundesligaBonusCopyPlan
{
    private readonly ImmutableArray<string> _mappedPostingOptionIds;

    private BundesligaBonusCopyPlan(
        PredictionCopyCompatibilityV2Decision decision,
        BundesligaTypedCopyRequest<TypedBonusSnapshot>? request,
        TypedBonusCopyCandidate? candidate,
        IEnumerable<string> mappedPostingOptionIds)
    {
        Decision = decision;
        Request = request;
        Candidate = candidate;
        _mappedPostingOptionIds = mappedPostingOptionIds.ToImmutableArray();
    }

    public PredictionCopyCompatibilityV2Decision Decision { get; }
    public bool IsAccepted => Request is not null;
    public BundesligaTypedCopyRequest<TypedBonusSnapshot>? Request { get; }
    public TypedBonusCopyCandidate? Candidate { get; }
    public IReadOnlyList<string> MappedPostingOptionIds => _mappedPostingOptionIds;

    internal static BundesligaBonusCopyPlan Rejected(PredictionCopyCompatibilityV2Decision decision) =>
        new(decision, null, null, []);

    internal static BundesligaBonusCopyPlan Accepted(
        PredictionCopyCompatibilityV2Decision decision,
        BundesligaTypedCopyRequest<TypedBonusSnapshot> request,
        TypedBonusCopyCandidate candidate,
        IEnumerable<string> mappedPostingOptionIds) =>
        new(decision, request, candidate, mappedPostingOptionIds);
}
