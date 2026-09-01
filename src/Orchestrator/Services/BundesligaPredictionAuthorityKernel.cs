using EHonda.KicktippAi.Core;

namespace Orchestrator.Services;

public sealed class BundesligaPredictionAuthorityKernel : IBundesligaPredictionAuthorityKernel
{
    private readonly BundesligaPredictionRouteRegistry _routes;
    private readonly IBundesligaTypedPredictionAuthorityRepository _repository;

    public BundesligaPredictionAuthorityKernel(
        BundesligaPredictionRouteRegistry routes,
        IBundesligaTypedPredictionAuthorityRepository repository)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public BundesligaPreparedCurrent<TypedMatchSnapshot> PrepareCurrent(
        BundesligaValidatedMatchItem item,
        string selectionId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var selection = _routes.GetRequiredSelection(selectionId, item.Authority, item);
        var current = BundesligaTypedCurrentRequest<TypedMatchSnapshot>.Create(
            item.Authority,
            item.Snapshot,
            selection.ModelConfig,
            selection.CreateCurrentIdentity(),
            _routes.Routes);
        return BundesligaPreparedCurrent<TypedMatchSnapshot>.Create(current, selection);
    }

    public BundesligaPreparedCurrent<TypedBonusSnapshot> PrepareCurrent(
        BundesligaValidatedBonusItem item,
        string selectionId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var selection = _routes.GetRequiredSelection(selectionId, item.Authority, item);
        var current = BundesligaTypedCurrentRequest<TypedBonusSnapshot>.Create(
            item.Authority,
            item.Snapshot,
            selection.ModelConfig,
            selection.CreateCurrentIdentity(),
            _routes.Routes);
        return BundesligaPreparedCurrent<TypedBonusSnapshot>.Create(current, selection);
    }

    public async Task<BundesligaMatchCopyPlan> PrepareMatchCopyAsync(
        BundesligaValidatedMatchItem targetItem,
        string targetSelectionId,
        BundesligaCopyBindingGeneration binding,
        BundesligaValidatedMatchItem sourceItem,
        string sourceSelectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetItem);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(sourceItem);
        cancellationToken.ThrowIfCancellationRequested();

        var targetPrepared = PrepareCurrent(targetItem, targetSelectionId);
        var targetCurrent = targetPrepared.Current;
        var targetSelection = targetPrepared.RegisteredSelection;
        var bindingEntry = RequireBindingEntry(
            binding, targetItem.Key, sourceItem.Key, BundesligaPredictionItemKind.Match);
        RequireCopyAuthorities(targetItem, sourceItem, binding);
        var sourcePrepared = PrepareCurrent(sourceItem, sourceSelectionId);
        var sourceCurrent = sourcePrepared.Current;
        var sourceSelection = sourcePrepared.RegisteredSelection;

        var sourceRow = await _repository.GetCurrentTypedMatchPredictionAsync(
            sourceCurrent, cancellationToken)
            ?? throw new InvalidDataException("Exact typed match source current row is missing.");
        BindActualSourceRow(sourceRow.Provenance, sourceSelection);

        var input = PredictionCopyCompatibilityV2Input<TypedMatchSnapshot>.Create(
            targetCurrent,
            sourceCurrent,
            targetItem.PostingSeed,
            sourceItem.PostingSeed,
            binding,
            bindingEntry,
            RequireCopyCompatibility(targetSelection),
            RequireCopyCompatibility(sourceSelection));
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);
        if (!decision.Succeeded)
        {
            return BundesligaMatchCopyPlan.Rejected(decision);
        }

        var request = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(input, decision);
        var candidate = await _repository.GetTypedMatchCopyCandidateAsync(request, cancellationToken)
            ?? throw new InvalidDataException("Accepted match copy candidate is missing.");
        if (!ReferenceEquals(candidate.SourceCurrent, sourceCurrent)
            || !PredictionContentEquality.Equals(
                candidate.SourcePrediction.Prediction, sourceRow.Prediction)
            || !candidate.SourcePrediction.Provenance.SerializeCanonical()
                .SequenceEqual(sourceRow.Provenance.SerializeCanonical())
            || !string.Equals(
                candidate.CopyRequestFingerprint, decision.BoundFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Match copy candidate drifted from the exact pre-compatibility source row or decision.");
        }

        return BundesligaMatchCopyPlan.Accepted(decision, request, candidate);
    }

    public async Task<BundesligaBonusCopyPlan> PrepareBonusCopyAsync(
        BundesligaValidatedBonusItem targetItem,
        string targetSelectionId,
        BundesligaCopyBindingGeneration binding,
        BundesligaValidatedBonusItem sourceItem,
        string sourceSelectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetItem);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(sourceItem);
        cancellationToken.ThrowIfCancellationRequested();

        var targetPrepared = PrepareCurrent(targetItem, targetSelectionId);
        var targetCurrent = targetPrepared.Current;
        var targetSelection = targetPrepared.RegisteredSelection;
        var bindingEntry = RequireBindingEntry(
            binding, targetItem.Key, sourceItem.Key, BundesligaPredictionItemKind.Bonus);
        RequireCopyAuthorities(targetItem, sourceItem, binding);
        var sourcePrepared = PrepareCurrent(sourceItem, sourceSelectionId);
        var sourceCurrent = sourcePrepared.Current;
        var sourceSelection = sourcePrepared.RegisteredSelection;

        var sourceRow = await _repository.GetCurrentTypedBonusPredictionAsync(
            sourceCurrent, cancellationToken)
            ?? throw new InvalidDataException("Exact typed bonus source current row is missing.");
        BindActualSourceRow(sourceRow.Provenance, sourceSelection);

        var input = PredictionCopyCompatibilityV2Input<TypedBonusSnapshot>.Create(
            targetCurrent,
            sourceCurrent,
            targetItem.PostingSeed,
            sourceItem.PostingSeed,
            binding,
            bindingEntry,
            RequireCopyCompatibility(targetSelection),
            RequireCopyCompatibility(sourceSelection));
        var decision = PredictionCopyCompatibilityV2.Evaluate(input);
        if (!decision.Succeeded)
        {
            return BundesligaBonusCopyPlan.Rejected(decision);
        }

        var request = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(input, decision);
        var candidate = await _repository.GetTypedBonusCopyCandidateAsync(request, cancellationToken)
            ?? throw new InvalidDataException("Accepted bonus copy candidate is missing.");
        if (!ReferenceEquals(candidate.SourceCurrent, sourceCurrent)
            || !candidate.SourcePrediction.SelectedOptionIds.SequenceEqual(
                sourceRow.SelectedOptionIds, StringComparer.Ordinal)
            || !candidate.SourcePrediction.Provenance.SerializeCanonical()
                .SequenceEqual(sourceRow.Provenance.SerializeCanonical())
            || !string.Equals(
                candidate.CopyRequestFingerprint, decision.BoundFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Bonus copy candidate drifted from the exact pre-compatibility source row or decision.");
        }

        var mapped = candidate.SourcePrediction.SelectedOptionIds.Select(sourceOptionId =>
            decision.OptionProjection.Single(projection => string.Equals(
                projection.SourceOptionId, sourceOptionId, StringComparison.Ordinal)).PostingOptionId);
        return BundesligaBonusCopyPlan.Accepted(decision, request, candidate, mapped);
    }

    private static PredictionCopyCompatibilityContractV2 RequireCopyCompatibility(
        BundesligaPredictionRouteSelection selection) =>
        selection.CopyCompatibility
        ?? throw new InvalidDataException(
            $"Registered selection '{selection.SelectionId}' has no copy compatibility policy.");

    private static void BindActualSourceRow(
        PredictionGenerationProvenanceV2 provenance,
        BundesligaPredictionRouteSelection selection)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        var contract = RequireCopyCompatibility(selection);
        if (!string.Equals(provenance.RouteId, selection.Route.RouteId, StringComparison.Ordinal)
            || !string.Equals(
                provenance.Authority.CommunityContext, selection.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(provenance.ProfileId, selection.ProfileId, StringComparison.Ordinal)
            || provenance.GenerationInputContract != selection.GenerationInputContract
            || provenance.ModelConfig != selection.ModelConfig
            || provenance.Prompt != contract.Prompt
            || !string.Equals(
                provenance.Context.RulesManifestId, contract.Rules.Identity, StringComparison.Ordinal)
            || !string.Equals(
                provenance.Context.RulesManifestSha256, contract.Rules.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Actual typed source row does not match its exact registered route, prompt, model, context, profile, generation-input, or rules policy.");
        }
    }

    private static BundesligaCopyBindingEntry RequireBindingEntry(
        BundesligaCopyBindingGeneration binding,
        StableLocalItemKey postingKey,
        StableLocalItemKey sourceKey,
        BundesligaPredictionItemKind itemKind)
    {
        var entry = binding.Entries.SingleOrDefault(candidate =>
            candidate.PostingKey == postingKey && candidate.SourceKey == sourceKey)
            ?? throw new InvalidDataException("Exact source/posting Copy Binding entry is missing.");
        if (entry.PostingKey.ItemKind != itemKind || entry.SourceKey.ItemKind != itemKind)
        {
            throw new InvalidDataException("Copy Binding entry has the wrong item kind.");
        }
        return entry;
    }

    private static void RequireCopyAuthorities(
        BundesligaValidatedMatchItem target,
        BundesligaValidatedMatchItem source,
        BundesligaCopyBindingGeneration binding) =>
        RequireCopyAuthorities(target.Authority, target.PostingSeed, source.Authority, source.PostingSeed, binding);

    private static void RequireCopyAuthorities(
        BundesligaValidatedBonusItem target,
        BundesligaValidatedBonusItem source,
        BundesligaCopyBindingGeneration binding) =>
        RequireCopyAuthorities(target.Authority, target.PostingSeed, source.Authority, source.PostingSeed, binding);

    private static void RequireCopyAuthorities(
        BundesligaPredictionAuthority target,
        BundesligaIdentitySeedGeneration targetSeed,
        BundesligaPredictionAuthority source,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaCopyBindingGeneration binding)
    {
        if (target.Mode != BundesligaPredictionAuthorityMode.Copy
            || source.Mode != BundesligaPredictionAuthorityMode.Direct
            || target.CopyBinding != binding.Reference
            || target.PostingSeed != targetSeed.Reference
            || target.SourceSeed != sourceSeed.Reference
            || source.PostingSeed != sourceSeed.Reference
            || source.SourceSeed != sourceSeed.Reference
            || binding.PostingSeed != targetSeed.Reference
            || binding.SourceSeed != sourceSeed.Reference
            || !string.Equals(target.PostingCommunity, binding.PostingCommunity, StringComparison.Ordinal)
            || !string.Equals(target.PredictionSourceCommunity, binding.SourceCommunity, StringComparison.Ordinal)
            || !string.Equals(source.PostingCommunity, binding.SourceCommunity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Copy plan authority, seeds, or binding are not exact.");
        }
    }
}
