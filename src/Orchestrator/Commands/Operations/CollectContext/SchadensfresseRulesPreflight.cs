using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Read-only boundary for ADR-0059. It deliberately validates every source/provenance identity
/// before a caller can fetch a prompt, construct a model service, or attempt publication.
/// Persistence of the successor resolvedTypedContextManifest is owned by the storage lane.
/// </summary>
public sealed class SchadensfresseRulesPreflight
{
    private readonly SchadensfresseLiveRulesExtractor _extractor;

    public SchadensfresseRulesPreflight(SchadensfresseLiveRulesExtractor extractor) =>
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));

    public async Task<SchadensfresseLiveRulesObservation> ValidateAsync(
        BundesligaSeasonRoutingSeed seed,
        ReadOnlyMemory<byte> markdownBytes,
        string expectedDocumentName,
        int expectedVersion,
        SchadensfresseRulesPublicationReadback? immutableReadback,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var observation = await _extractor.ExtractAsync(now, cancellationToken);
        SchadensfresseRulesPublicationGate.Validate(
            observation.Rules,
            observation.ObservedAt,
            now,
            seed.RulesSchemaVersion,
            seed.CanonicalRulesSha256,
            markdownBytes.Span,
            seed.CommunityRulesContentSha256,
            expectedDocumentName,
            expectedVersion,
            immutableReadback);
        return observation;
    }

    public Task<SchadensfresseLiveRulesObservation> ObserveAsync(
        BundesligaSeasonRoutingSeed seed,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);
        if (!string.Equals(seed.RulesSchemaVersion, SchadensfresseRulesCanonicalJson.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(seed.CanonicalRulesSha256, SchadensfresseRulesCanonicalJson.CanonicalSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Schadensfresse rules seed does not pin the accepted v1 semantic identity.");
        return _extractor.ExtractAsync(now, cancellationToken);
    }

    public static string ValidatePublicationCandidate(SchadensfresseLiveRulesObservation observation, BundesligaSeasonRoutingSeed seed, ReadOnlySpan<byte> markdownBytes, DateTimeOffset now) =>
        SchadensfresseRulesPublicationGate.ValidateCandidate(observation.Rules, observation.ObservedAt, now, seed.RulesSchemaVersion, seed.CanonicalRulesSha256, markdownBytes, seed.CommunityRulesContentSha256);

    public static void ValidateImmutableReadback(
        SchadensfresseRulesPublicationReadback readback,
        string expectedDocumentName,
        int expectedVersion,
        string expectedContentSha256) =>
        SchadensfresseRulesPublicationGate.ValidateReadback(
            readback,
            expectedDocumentName,
            expectedVersion,
            expectedContentSha256);

    /// <summary>
    /// Builds all three canonical ADR-0060 bindings from one authenticated observation and one
    /// exact immutable-document readback. Persistence remains the caller's explicit boundary.
    /// </summary>
    public static IReadOnlyList<ResolvedTypedContextPublicationBinding> CreatePublicationBindingCandidates(
        SchadensfresseLiveRulesObservation observation,
        BundesligaSeasonRoutingSeed seed,
        SchadensfresseRulesPublicationReadback immutableReadback,
        string expectedContentSha256,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(immutableReadback);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentSha256);
        ValidateImmutableReadback(
            immutableReadback,
            SchadensfresseRulesPublicationGate.DocumentName,
            immutableReadback.Version,
            expectedContentSha256);
        return CreatePublicationBindings(
            observation,
            seed,
            new ResolvedTypedContextDocument(
                SchadensfresseTypedContextProfiles.RulesDocumentKind,
                immutableReadback.DocumentName,
                immutableReadback.Version,
                immutableReadback.ContentSha256),
            now);
    }

    /// <summary>Validates all mandatory profiles without a binding write, for dry-run use.</summary>
    public static void ValidatePublicationBindingProfiles(
        SchadensfresseLiveRulesObservation observation,
        BundesligaSeasonRoutingSeed seed,
        string expectedContentSha256,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedContentSha256);
        _ = CreatePublicationBindings(
            observation,
            seed,
            new ResolvedTypedContextDocument(
                SchadensfresseTypedContextProfiles.RulesDocumentKind,
                SchadensfresseRulesPublicationGate.DocumentName,
                version: 0,
                expectedContentSha256),
            now);
    }

    /// <summary>Tests and callers with an already authenticated observation can exercise the no-network gate.</summary>
    public static void ValidateObservation(
        SchadensfresseLiveRulesObservation observation,
        BundesligaSeasonRoutingSeed seed,
        ReadOnlySpan<byte> markdownBytes,
        string expectedDocumentName,
        int expectedVersion,
        SchadensfresseRulesPublicationReadback? immutableReadback,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(seed);
        SchadensfresseRulesPublicationGate.Validate(
            observation.Rules,
            observation.ObservedAt,
            now,
            seed.RulesSchemaVersion,
            seed.CanonicalRulesSha256,
            markdownBytes,
            seed.CommunityRulesContentSha256,
            expectedDocumentName,
            expectedVersion,
            immutableReadback);
    }

    private static IReadOnlyList<ResolvedTypedContextPublicationBinding> CreatePublicationBindings(
        SchadensfresseLiveRulesObservation observation,
        BundesligaSeasonRoutingSeed seed,
        ResolvedTypedContextDocument document,
        DateTimeOffset now)
    {
        if (!string.Equals(seed.RulesSchemaVersion, SchadensfresseRulesCanonicalJson.SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(seed.CanonicalRulesSha256, SchadensfresseRulesCanonicalJson.CanonicalSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Schadensfresse rules seed does not pin the accepted v1 semantic identity.");
        }

        var bindings = RequiredProfileIds.Select(profileId =>
        {
            if (!SchadensfresseTypedContextProfiles.TryGetSubcompetition(profileId, out var subcompetition))
            {
                throw new InvalidDataException($"Required schadensfresse publication-binding profile '{profileId}' is absent or malformed.");
            }

            var binding = new ResolvedTypedContextPublicationBinding(
                SchadensfresseTypedContextProfiles.SeasonPartition,
                SchadensfresseTypedContextProfiles.CommunityContext,
                profileId,
                seed.CanonicalSha256,
                subcompetition,
                observation.ObservedAt,
                seed.RulesSchemaVersion,
                seed.CanonicalRulesSha256,
                document);
            SchadensfresseTypedContextProfiles.ValidateBindingStructure(binding);
            SchadensfresseTypedContextProfiles.ValidateBindingFreshness(binding, now);
            return binding;
        }).ToArray();

        if (bindings.Length != RequiredProfileIds.Length
            || bindings.Select(binding => binding.Key).Distinct().Count() != RequiredProfileIds.Length)
        {
            throw new InvalidDataException("Schadensfresse publication-binding profile set is partial or contains duplicate exact keys.");
        }

        return bindings;
    }

    private static readonly string[] RequiredProfileIds =
    [
        "schadensfresse-dfb-pokal-rules-only-v1",
        "schadensfresse-champions-league-match-rules-only-v1",
        "schadensfresse-champions-league-bonus-rules-only-v1"
    ];
}
