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
}
