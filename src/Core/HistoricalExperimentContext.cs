using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Read-only access to the legacy-ID Bundesliga 2025/26 context rows used by historical experiments.
/// This boundary intentionally exposes no mutation operations.
/// </summary>
public interface IHistoricalExperimentContextReader
{
    Task<ContextDocument?> GetContextDocumentAtOrBeforeAsync(
        string documentName,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        CancellationToken cancellationToken = default);

    Task<ContextDocument?> GetContextDocumentAsync(
        string documentName,
        int version,
        string communityContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exact, hash-bound provenance for the seven-document Bundesliga 2025/26 historical experiment route.
/// This contract is separate from the eleven-document live Bundesliga 2026/27 publication manifest.
/// </summary>
public sealed record ResolvedHistoricalExperimentContextManifest(
    string CompatibilityMode,
    string Competition,
    string CommunityContext,
    DateTimeOffset EvaluationTimestamp,
    IReadOnlyList<ResolvedHistoricalExperimentContextDocument> Documents,
    string ManifestSha256)
{
    public const string LegacyIdHashV1 = "bundesliga-2025-26-legacy-id-hash-v1";

    public static ResolvedHistoricalExperimentContextManifest Create(
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        IEnumerable<ResolvedHistoricalExperimentContextDocument> documents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        var ordered = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
        var hash = ComputeManifestSha256(
            LegacyIdHashV1,
            CompetitionIds.Bundesliga2025_26,
            communityContext,
            evaluationTimestamp,
            ordered);
        var manifest = new ResolvedHistoricalExperimentContextManifest(
            LegacyIdHashV1,
            CompetitionIds.Bundesliga2025_26,
            communityContext,
            evaluationTimestamp,
            ordered,
            hash);
        Validate(manifest);
        return manifest;
    }

    public static void Validate(ResolvedHistoricalExperimentContextManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.CompatibilityMode, LegacyIdHashV1, StringComparison.Ordinal)
            || !string.Equals(manifest.Competition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(manifest.CommunityContext))
        {
            throw new InvalidDataException("Historical experiment context scope or compatibility mode is invalid.");
        }

        if (manifest.Documents is null
            || manifest.Documents.Count != 7
            || manifest.Documents.Select(document => document.Name).Distinct(StringComparer.Ordinal).Count() != 7
            || manifest.Documents.Any(document => string.IsNullOrWhiteSpace(document.Name)
                                                  || document.Version < 0
                                                  || !string.Equals(
                                                      document.SourceDocumentId,
                                                      BuildLegacyDocumentId(document.Name, manifest.CommunityContext, document.Version),
                                                      StringComparison.Ordinal)
                                                  || document.CreatedAt > manifest.EvaluationTimestamp
                                                  || !DocumentPublicationContract.IsLowercaseSha256(document.ContentSha256)))
        {
            throw new InvalidDataException(
                "A Bundesliga 2025/26 historical experiment context manifest must contain exactly seven unique documents with valid exact identities, timestamps, and lowercase SHA-256 hashes.");
        }

        var expectedHash = ComputeManifestSha256(
            manifest.CompatibilityMode,
            manifest.Competition,
            manifest.CommunityContext,
            manifest.EvaluationTimestamp,
            manifest.Documents);
        if (!string.Equals(manifest.ManifestSha256, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Historical experiment context manifest hash does not match its bound content identities.");
        }
    }

    public static string BuildLegacyDocumentId(string documentName, string communityContext, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Historical context version must be nonnegative.");
        }

        return $"{documentName}_{communityContext}_{version}";
    }

    public static void ValidateForMatch(
        ResolvedHistoricalExperimentContextManifest manifest,
        Match match,
        string communityContext)
    {
        ArgumentNullException.ThrowIfNull(match);
        Validate(manifest);
        if (!string.Equals(manifest.CommunityContext, communityContext, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Historical experiment context manifest community scope does not match the experiment scope.");
        }

        var expectedNames = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            match,
            communityContext).RequiredDocumentNames;
        if (!manifest.Documents.Select(document => document.Name).SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Historical experiment context manifest document names or order do not match the canonical seven-document Bundesliga 2025/26 contract.");
        }
    }

    private static string ComputeManifestSha256(
        string compatibilityMode,
        string competition,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        IEnumerable<ResolvedHistoricalExperimentContextDocument> documents)
    {
        var fields = new List<string>
        {
            compatibilityMode,
            competition,
            communityContext,
            evaluationTimestamp.ToString("O", CultureInfo.InvariantCulture)
        };
        foreach (var document in documents)
        {
            fields.Add(document.Name);
            fields.Add(document.Version.ToString(CultureInfo.InvariantCulture));
            fields.Add(document.SourceDocumentId);
            fields.Add(document.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            fields.Add(document.ContentSha256);
        }

        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (var field in fields)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(field);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        return Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant();
    }
}

public sealed record ResolvedHistoricalExperimentContextDocument(
    string Name,
    int Version,
    string SourceDocumentId,
    DateTimeOffset CreatedAt,
    string ContentSha256);

public sealed record ResolvedHistoricalExperimentContext(
    IReadOnlyList<DocumentContext> Documents,
    ResolvedHistoricalExperimentContextManifest Manifest);

/// <summary>Resolves and verifies the immutable seven-document historical experiment context.</summary>
public sealed class Bundesliga2025_26HistoricalExperimentContextResolver
{
    private readonly IHistoricalExperimentContextReader _reader;

    public Bundesliga2025_26HistoricalExperimentContextResolver(IHistoricalExperimentContextReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<ResolvedHistoricalExperimentContext> ResolveAtTimestampAsync(
        Match match,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        CancellationToken cancellationToken = default)
    {
        var result = await TryResolveAtTimestampCoreAsync(
            match,
            communityContext,
            evaluationTimestamp,
            cancellationToken);
        return result.Context
            ?? throw new InvalidOperationException(
                $"Historical context document '{result.MissingDocumentName}' had no version at or before {evaluationTimestamp:O}.");
    }

    /// <summary>
    /// Resolves a historical context when all exact producer-era documents exist at the boundary.
    /// A genuinely absent document returns <see langword="null"/>; malformed data still fails closed.
    /// </summary>
    public async Task<ResolvedHistoricalExperimentContext?> TryResolveAtTimestampAsync(
        Match match,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        CancellationToken cancellationToken = default)
    {
        var result = await TryResolveAtTimestampCoreAsync(
            match,
            communityContext,
            evaluationTimestamp,
            cancellationToken);
        return result.Context;
    }

    private async Task<(ResolvedHistoricalExperimentContext? Context, string? MissingDocumentName)> TryResolveAtTimestampCoreAsync(
        Match match,
        string communityContext,
        DateTimeOffset evaluationTimestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        var requiredNames = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            match,
            communityContext).RequiredDocumentNames;
        var documents = new List<DocumentContext>(requiredNames.Count);
        var entries = new List<ResolvedHistoricalExperimentContextDocument>(requiredNames.Count);
        foreach (var name in requiredNames)
        {
            var document = await _reader.GetContextDocumentAtOrBeforeAsync(
                name,
                communityContext,
                evaluationTimestamp,
                cancellationToken);
            if (document is null)
            {
                return (null, name);
            }
            if (!string.Equals(document.DocumentName, name, StringComparison.Ordinal)
                || document.Version < 0
                || document.CreatedAt > evaluationTimestamp)
            {
                throw new InvalidDataException(
                    $"Historical context document '{name}' resolved to an invalid identity or timestamp.");
            }

            documents.Add(new DocumentContext(document.DocumentName, document.Content));
            entries.Add(new ResolvedHistoricalExperimentContextDocument(
                document.DocumentName,
                document.Version,
                ResolvedHistoricalExperimentContextManifest.BuildLegacyDocumentId(
                    document.DocumentName,
                    communityContext,
                    document.Version),
                document.CreatedAt,
                DocumentPublicationContract.ComputeContentSha256(document.Content)));
        }

        var manifest = ResolvedHistoricalExperimentContextManifest.Create(
            communityContext,
            evaluationTimestamp,
            entries);
        ResolvedHistoricalExperimentContextManifest.ValidateForMatch(manifest, match, communityContext);
        return (new ResolvedHistoricalExperimentContext(documents.AsReadOnly(), manifest), null);
    }

    public async Task<ResolvedHistoricalExperimentContext> ResolveRecordedAsync(
        Match match,
        ResolvedHistoricalExperimentContextManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ResolvedHistoricalExperimentContextManifest.ValidateForMatch(manifest, match, manifest.CommunityContext);
        var documents = new List<DocumentContext>(manifest.Documents.Count);
        foreach (var expected in manifest.Documents)
        {
            var document = await _reader.GetContextDocumentAsync(
                expected.Name,
                expected.Version,
                manifest.CommunityContext,
                cancellationToken)
                ?? throw new InvalidDataException(
                    $"Recorded historical context version '{expected.Name}' v{expected.Version} is missing.");
            var actualHash = DocumentPublicationContract.ComputeContentSha256(document.Content);
            if (!string.Equals(document.DocumentName, expected.Name, StringComparison.Ordinal)
                || document.Version != expected.Version
                || document.CreatedAt != expected.CreatedAt
                || document.CreatedAt > manifest.EvaluationTimestamp
                || !string.Equals(actualHash, expected.ContentSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Recorded historical context version '{expected.Name}' v{expected.Version} drifted from its exact identity, timestamp, or content hash.");
            }

            documents.Add(new DocumentContext(document.DocumentName, document.Content));
        }

        return new ResolvedHistoricalExperimentContext(documents.AsReadOnly(), manifest);
    }
}
