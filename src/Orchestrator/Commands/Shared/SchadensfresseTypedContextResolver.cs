using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Shared;

/// <summary>
/// The complete, exact identity required to resolve the rules-only schadensfresse context.
/// This deliberately contains no prompt, model, fixture, or question route.
/// </summary>
public sealed record SchadensfresseTypedContextResolutionRequest(
    string SeasonPartition,
    string CommunityContext,
    BundesligaSeasonSubcompetition BundesligaSeasonSubcompetition,
    string ProfileId,
    string RoutingSeedSha256,
    ResolvedTypedContextManifest? ExistingGenerationManifest = null);

/// <summary>
/// A value-safe rules-only resolution. Its quality limitation is intentional: no sporting,
/// roster, history, prompt, or model context has been selected or attested here.
/// </summary>
public sealed class SchadensfresseTypedContextResolution
{
    internal SchadensfresseTypedContextResolution(
        IEnumerable<DocumentContext> documents,
        ResolvedTypedContextManifest generationManifest,
        ResolvedTypedContextPublicationBinding currentBinding,
        BonusContextMeasurement measurement)
    {
        Documents = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
        if (Documents.Length != SchadensfresseTypedContextProfiles.MaximumDocuments)
        {
            throw new ArgumentException("Rules-only resolution must contain exactly one document.", nameof(documents));
        }

        GenerationManifest = generationManifest ?? throw new ArgumentNullException(nameof(generationManifest));
        CurrentBinding = currentBinding ?? throw new ArgumentNullException(nameof(currentBinding));
        Measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
    }

    public ImmutableArray<DocumentContext> Documents { get; }
    public ResolvedTypedContextManifest GenerationManifest { get; }
    public ResolvedTypedContextPublicationBinding CurrentBinding { get; }
    public BonusContextMeasurement Measurement { get; }
    public string QualityLimitation => "Rules-only context: no sporting, roster, history, prompt, or model context is selected.";
}

/// <summary>
/// Resolves one directly addressed ADR-0060 publication binding and its exact immutable
/// document. It is a read-only rules gate and intentionally cannot discover a latest document,
/// write a binding, select a prompt, or construct a model service.
/// </summary>
public sealed class SchadensfresseTypedContextResolver
{
    private readonly IResolvedTypedContextPublicationBindingRepository _bindingRepository;
    private readonly IContextRepository _contextRepository;
    private readonly TimeProvider _timeProvider;

    public SchadensfresseTypedContextResolver(
        IResolvedTypedContextPublicationBindingRepository bindingRepository,
        IContextRepository contextRepository,
        TimeProvider timeProvider)
    {
        _bindingRepository = bindingRepository ?? throw new ArgumentNullException(nameof(bindingRepository));
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SchadensfresseTypedContextResolution> ResolveAsync(
        SchadensfresseTypedContextResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request); // This is deliberately before the sole clock read and all repository activity.
        cancellationToken.ThrowIfCancellationRequested();

        var evaluationInstant = _timeProvider.GetUtcNow();
        var key = new ResolvedTypedContextPublicationBindingKey(
            request.SeasonPartition,
            request.CommunityContext,
            request.ProfileId,
            request.RoutingSeedSha256);
        var binding = await _bindingRepository.GetExactAsync(key, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The exact schadensfresse publication binding is missing.");

        SchadensfresseTypedContextProfiles.ValidateBindingStructure(binding);
        SchadensfresseTypedContextProfiles.ValidateBindingFreshness(binding, evaluationInstant);
        ValidateBindingMatchesRequest(binding, request, key);

        var published = await _contextRepository.GetContextDocumentAsync(
            binding.Document.Name,
            binding.Document.Version,
            binding.CommunityContext,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The exact immutable schadensfresse rules document is missing.");
        ValidateExactDocument(published, binding.Document);

        var document = new DocumentContext(published.DocumentName, published.Content);
        var measurement = BonusContextBudgetEstimator.Measure([document]);
        var effectiveManifest = request.ExistingGenerationManifest is null
            ? CreateCurrentGenerationManifest(binding)
            : ValidateExistingGenerationManifest(request.ExistingGenerationManifest, request, binding, document, measurement);

        if (request.ExistingGenerationManifest is null)
        {
            ValidateManifest(effectiveManifest, document, measurement, requireFreshness: true, evaluationInstant);
        }

        return new SchadensfresseTypedContextResolution([document], effectiveManifest, binding, measurement);
    }

    private static void ValidateRequest(SchadensfresseTypedContextResolutionRequest request)
    {
        if (!string.Equals(request.SeasonPartition, SchadensfresseTypedContextProfiles.SeasonPartition, StringComparison.Ordinal)
            || !string.Equals(request.CommunityContext, SchadensfresseTypedContextProfiles.CommunityContext, StringComparison.Ordinal)
            || !SchadensfresseTypedContextProfiles.TryGetSubcompetition(request.ProfileId, out var expectedSubcompetition)
            || request.BundesligaSeasonSubcompetition != expectedSubcompetition
            || !TypedContextCanonicalJson.IsLowercaseSha256(request.RoutingSeedSha256))
        {
            throw new InvalidDataException("The invocation is not an exact canonical schadensfresse rules-only profile.");
        }
    }

    private static void ValidateBindingMatchesRequest(
        ResolvedTypedContextPublicationBinding binding,
        SchadensfresseTypedContextResolutionRequest request,
        ResolvedTypedContextPublicationBindingKey key)
    {
        if (binding.Key != key
            || binding.BundesligaSeasonSubcompetition != request.BundesligaSeasonSubcompetition
            || !string.Equals(binding.SeasonPartition, request.SeasonPartition, StringComparison.Ordinal)
            || !string.Equals(binding.CommunityContext, request.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(binding.ProfileId, request.ProfileId, StringComparison.Ordinal)
            || !string.Equals(binding.RoutingSeedSha256, request.RoutingSeedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The exact publication binding does not match the current invocation.");
        }
    }

    private static void ValidateExactDocument(ContextDocument published, ResolvedTypedContextDocument expected)
    {
        if (!string.Equals(published.DocumentName, expected.Name, StringComparison.Ordinal)
            || published.Version != expected.Version
            || !string.Equals(DocumentPublicationContract.ComputeContentSha256(published.Content), expected.ContentSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The immutable rules document readback does not match the publication binding.");
        }
    }

    private static ResolvedTypedContextManifest CreateCurrentGenerationManifest(ResolvedTypedContextPublicationBinding binding) =>
        new(
            binding.SeasonPartition,
            binding.CommunityContext,
            binding.BundesligaSeasonSubcompetition,
            binding.ProfileId,
            binding.RoutingSeedSha256,
            binding.RulesObservedAt,
            binding.RulesSchemaVersion,
            binding.CanonicalRulesSha256,
            [binding.Document]);

    private static ResolvedTypedContextManifest ValidateExistingGenerationManifest(
        ResolvedTypedContextManifest manifest,
        SchadensfresseTypedContextResolutionRequest request,
        ResolvedTypedContextPublicationBinding binding,
        DocumentContext document,
        BonusContextMeasurement measurement)
    {
        // Canonical serialization/readback is a structural gate only. Historical provenance may be old.
        var canonical = manifest.SerializeCanonical();
        var reread = ResolvedTypedContextManifest.DeserializeCanonical(canonical);
        ValidateManifest(reread, document, measurement, requireFreshness: false, default);

        if (!string.Equals(reread.SeasonPartition, request.SeasonPartition, StringComparison.Ordinal)
            || !string.Equals(reread.CommunityContext, request.CommunityContext, StringComparison.Ordinal)
            || reread.BundesligaSeasonSubcompetition != request.BundesligaSeasonSubcompetition
            || !string.Equals(reread.ProfileId, request.ProfileId, StringComparison.Ordinal)
            || !string.Equals(reread.RoutingSeedSha256, request.RoutingSeedSha256, StringComparison.Ordinal)
            || !string.Equals(reread.RulesSchemaVersion, binding.RulesSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(reread.CanonicalRulesSha256, binding.CanonicalRulesSha256, StringComparison.Ordinal)
            || !reread.Documents.SequenceEqual([binding.Document]))
        {
            throw new InvalidDataException("The immutable generation manifest drifts from the current rules binding or invocation.");
        }

        // Preserve the caller's immutable generation record rather than replacing its observation.
        return manifest;
    }

    private static void ValidateManifest(
        ResolvedTypedContextManifest manifest,
        DocumentContext document,
        BonusContextMeasurement measurement,
        bool requireFreshness,
        DateTimeOffset evaluationInstant)
    {
        SchadensfresseTypedContextProfiles.ValidateManifestStructure(manifest, measurement.Utf8Bytes);
        if (manifest.Documents.Count != 1
            || !string.Equals(manifest.Documents[0].Name, document.Name, StringComparison.Ordinal)
            || !string.Equals(manifest.Documents[0].ContentSha256, DocumentPublicationContract.ComputeContentSha256(document.Content), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The generation manifest does not bind the exact rendered rules document.");
        }

        if (requireFreshness)
        {
            SchadensfresseTypedContextProfiles.ValidateManifestFreshness(manifest, evaluationInstant);
        }
    }
}
