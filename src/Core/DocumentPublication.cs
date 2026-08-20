using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public enum DocumentPublicationKind
{
    Context,
    Kpi
}

public enum DocumentPublicationDisposition
{
    Published,
    Unchanged,
    Reactivated
}

/// <summary>
/// The scope of one independently publishable document set. Content snapshot IDs deliberately
/// exclude this scope; head and immutable-metadata identities include it.
/// </summary>
public sealed record DocumentPublicationScope(
    string Competition,
    string CommunityContext,
    string PublicationSet);

public sealed record DocumentPublicationKey(DocumentPublicationKind Kind, string Name);

/// <summary>
/// An immutable definition of the complete document set that can be published together.
/// </summary>
public sealed record DocumentPublicationDefinition
{
    public DocumentPublicationDefinition(string publicationSet, IEnumerable<DocumentPublicationKey> requiredDocuments)
    {
        PublicationSet = publicationSet;
        RequiredDocuments = DocumentPublicationContract.ValidateAndOrderKeys(requiredDocuments);
    }

    public string PublicationSet { get; }

    public ImmutableArray<DocumentPublicationKey> RequiredDocuments { get; }
}

/// <summary>
/// Exact valid UTF-8 text for one document. String content avoids retaining caller-owned mutable
/// byte arrays across transaction retries; its UTF-8 encoding is the hashed content bytes.
/// </summary>
public sealed record DocumentPublicationPayload(
    DocumentPublicationKind Kind,
    string Name,
    string Content,
    string? Description = null)
{
    public DocumentPublicationKey Key => new(Kind, Name);
}

public sealed record DocumentPublicationEntry(
    DocumentPublicationKind Kind,
    string Name,
    int Version,
    string ContentSha256)
{
    public DocumentPublicationKey Key => new(Kind, Name);
}

public sealed record DocumentPublicationRequest
{
    public DocumentPublicationRequest(
        string communityContext,
        string? expectedPreviousSnapshotId,
        IEnumerable<DocumentPublicationPayload> documents,
        string metadataJson = "{}")
    {
        CommunityContext = communityContext;
        ExpectedPreviousSnapshotId = expectedPreviousSnapshotId;
        Documents = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
        MetadataJson = metadataJson;
    }

    public string CommunityContext { get; }

    public string? ExpectedPreviousSnapshotId { get; }

    public ImmutableArray<DocumentPublicationPayload> Documents { get; }

    public string MetadataJson { get; }
}

public sealed record DocumentPublicationSnapshot
{
    public DocumentPublicationSnapshot(
        string competition,
        string communityContext,
        string publicationSet,
        string snapshotId,
        string? previousSnapshotId,
        DateTimeOffset createdAt,
        string metadataJson,
        IEnumerable<DocumentPublicationEntry> documents)
    {
        Competition = competition;
        CommunityContext = communityContext;
        PublicationSet = publicationSet;
        SnapshotId = snapshotId;
        PreviousSnapshotId = previousSnapshotId;
        CreatedAt = createdAt;
        MetadataJson = metadataJson;
        Documents = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
    }

    public string Competition { get; }
    public string CommunityContext { get; }
    public string PublicationSet { get; }
    public string SnapshotId { get; }
    public string? PreviousSnapshotId { get; }
    public DateTimeOffset CreatedAt { get; }
    public string MetadataJson { get; }
    public ImmutableArray<DocumentPublicationEntry> Documents { get; }

    public DocumentPublicationScope Scope => new(Competition, CommunityContext, PublicationSet);
}

/// <summary>
/// One immutable payload row resolved at the exact version named by a publication snapshot.
/// </summary>
public sealed record PublishedDocument(
    string Competition,
    string CommunityContext,
    string PublicationSet,
    DocumentPublicationKind Kind,
    string Name,
    int Version,
    string Content,
    string? Description,
    DateTimeOffset CreatedAt)
{
    public DocumentPublicationKey Key => new(Kind, Name);

    public DocumentPublicationScope Scope => new(Competition, CommunityContext, PublicationSet);
}

public sealed record LoadedDocumentPublication
{
    public LoadedDocumentPublication(
        DocumentPublicationSnapshot snapshot,
        IEnumerable<PublishedDocument> documents)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Documents = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
    }

    public DocumentPublicationSnapshot Snapshot { get; }

    public ImmutableArray<PublishedDocument> Documents { get; }
}

public sealed record DocumentPublicationResult(
    DocumentPublicationDisposition Disposition,
    DocumentPublicationSnapshot Snapshot);

public sealed class DocumentPublicationConcurrencyException : InvalidOperationException
{
    public DocumentPublicationConcurrencyException(
        DocumentPublicationScope scope,
        string? expectedSnapshotId,
        string? actualSnapshotId)
        : base(
            $"Publication head mismatch for '{scope.PublicationSet}' in " +
            $"'{scope.Competition}/{scope.CommunityContext}': expected " +
            $"'{expectedSnapshotId ?? "<none>"}', actual '{actualSnapshotId ?? "<none>"}'.")
    {
        Scope = scope;
        ExpectedSnapshotId = expectedSnapshotId;
        ActualSnapshotId = actualSnapshotId;
    }

    public DocumentPublicationScope Scope { get; }

    public string? ExpectedSnapshotId { get; }

    public string? ActualSnapshotId { get; }
}

public interface IDocumentPublicationRepository
{
    /// <summary>The sole competition partition served by this repository instance.</summary>
    string Competition { get; }

    Task<DocumentPublicationResult> PublishAsync(
        DocumentPublicationDefinition definition,
        DocumentPublicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one complete valid publication through head, immutable snapshot metadata, and
    /// the exact versions listed in that snapshot. This is the live read path for reserved sets.
    /// </summary>
    Task<LoadedDocumentPublication?> GetLastKnownGoodAsync(
        DocumentPublicationDefinition definition,
        string communityContext,
        CancellationToken cancellationToken = default);

    /// <summary>Loads and validates one immutable publication snapshot by its recorded identity.</summary>
    Task<LoadedDocumentPublication?> GetSnapshotAsync(
        DocumentPublicationDefinition definition,
        string communityContext,
        string snapshotId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This publication repository does not support immutable snapshot reads.");
}

public static class DocumentPublicationContract
{
    public const int Sha256HexLength = 64;

    public static ImmutableArray<DocumentPublicationPayload> ValidateAndOrder(
        IEnumerable<DocumentPublicationPayload> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var byKey = new Dictionary<DocumentPublicationKey, DocumentPublicationPayload>();
        foreach (var document in documents)
        {
            ArgumentNullException.ThrowIfNull(document);
            ValidateKind(document.Kind, nameof(documents));
            ValidateName(document.Name, nameof(documents));
            ValidateUtf8Content(document.Content, nameof(documents));

            if (document.Kind == DocumentPublicationKind.Kpi && string.IsNullOrWhiteSpace(document.Description))
            {
                throw new ArgumentException($"KPI publication document '{document.Name}' requires a description.", nameof(documents));
            }

            if (document.Kind == DocumentPublicationKind.Context && !string.IsNullOrEmpty(document.Description))
            {
                throw new ArgumentException($"Context publication document '{document.Name}' cannot have a KPI description.", nameof(documents));
            }

            if (!byKey.TryAdd(document.Key, document))
            {
                throw new ArgumentException($"Duplicate publication document '{document.Kind}:{document.Name}'.", nameof(documents));
            }
        }

        if (byKey.Count == 0)
        {
            throw new ArgumentException("At least one publication document is required.", nameof(documents));
        }

        return byKey.Values.OrderBy(document => document.Kind).ThenBy(document => document.Name, StringComparer.Ordinal).ToImmutableArray();
    }

    public static ImmutableArray<DocumentPublicationKey> ValidateAndOrderKeys(
        IEnumerable<DocumentPublicationKey> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var unique = new HashSet<DocumentPublicationKey>();
        foreach (var document in documents)
        {
            ArgumentNullException.ThrowIfNull(document);
            ValidateKind(document.Kind, nameof(documents));
            ValidateName(document.Name, nameof(documents));
            if (!unique.Add(document))
            {
                throw new ArgumentException($"Duplicate required publication document '{document.Kind}:{document.Name}'.", nameof(documents));
            }
        }

        if (unique.Count == 0)
        {
            throw new ArgumentException("At least one required publication document is required.", nameof(documents));
        }

        return unique.OrderBy(document => document.Kind).ThenBy(document => document.Name, StringComparer.Ordinal).ToImmutableArray();
    }

    public static string ComputeContentSha256(string content)
    {
        return Convert.ToHexString(SHA256.HashData(GetUtf8Bytes(content))).ToLowerInvariant();
    }

    public static bool IsLowercaseSha256(string? value) =>
        value?.Length == Sha256HexLength
        && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public static string DecodeUtf8(ReadOnlySpan<byte> content) => StrictUtf8.GetString(content);

    public static string ComputeSnapshotId(IEnumerable<DocumentPublicationPayload> documents)
    {
        var ordered = ValidateAndOrder(documents);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var document in ordered)
        {
            AppendLengthPrefixed(hash, Encoding.UTF8.GetBytes(document.Kind.ToString()));
            AppendLengthPrefixed(hash, Encoding.UTF8.GetBytes(document.Name));
            AppendLengthPrefixed(hash, GetUtf8Bytes(document.Content));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string ComputeHeadId(DocumentPublicationScope scope)
    {
        ValidateScope(scope);
        return ComputeLengthPrefixedSha256(
            GetUtf8Bytes(scope.Competition),
            GetUtf8Bytes(scope.CommunityContext),
            GetUtf8Bytes(scope.PublicationSet));
    }

    public static string ComputeSnapshotMetadataId(DocumentPublicationScope scope, string snapshotId)
    {
        ValidateScope(scope);
        ValidateSha256(snapshotId, nameof(snapshotId));
        return ComputeLengthPrefixedSha256(
            GetUtf8Bytes(scope.Competition),
            GetUtf8Bytes(scope.CommunityContext),
            GetUtf8Bytes(scope.PublicationSet),
            GetUtf8Bytes(snapshotId));
    }

    /// <summary>
    /// Decides the immutable head transition after first applying compare-and-swap. A reactivated
    /// snapshot keeps its original immutable metadata, including its original predecessor.
    /// </summary>
    public static DocumentPublicationDisposition DecideTransition(
        DocumentPublicationScope scope,
        string? expectedPreviousSnapshotId,
        string? currentSnapshotId,
        string targetSnapshotId,
        bool targetSnapshotAlreadyExists)
    {
        EnsureExpectedHead(scope, expectedPreviousSnapshotId, currentSnapshotId);
        ValidateSha256(targetSnapshotId, nameof(targetSnapshotId));

        if (string.Equals(targetSnapshotId, currentSnapshotId, StringComparison.Ordinal))
        {
            return DocumentPublicationDisposition.Unchanged;
        }

        return targetSnapshotAlreadyExists
            ? DocumentPublicationDisposition.Reactivated
            : DocumentPublicationDisposition.Published;
    }

    /// <summary>
    /// Validates and applies the compare-and-swap precondition before an adapter reads a
    /// snapshot graph. This makes a stale request fail before unrelated corrupt target data can
    /// affect the outcome.
    /// </summary>
    public static void EnsureExpectedHead(
        DocumentPublicationScope scope,
        string? expectedPreviousSnapshotId,
        string? currentSnapshotId)
    {
        ValidateScope(scope);
        ValidateOptionalSha256(expectedPreviousSnapshotId, nameof(expectedPreviousSnapshotId));
        ValidateOptionalSha256(currentSnapshotId, nameof(currentSnapshotId));
        if (!string.Equals(expectedPreviousSnapshotId, currentSnapshotId, StringComparison.Ordinal))
        {
            throw new DocumentPublicationConcurrencyException(scope, expectedPreviousSnapshotId, currentSnapshotId);
        }
    }

    public static void ValidateRequest(
        string competition,
        DocumentPublicationDefinition definition,
        DocumentPublicationRequest request)
    {
        ValidateDefinition(competition, definition);
        ArgumentNullException.ThrowIfNull(request);
        ValidateScopeValue(request.CommunityContext, nameof(request.CommunityContext));
        ValidateOptionalSha256(request.ExpectedPreviousSnapshotId, nameof(request.ExpectedPreviousSnapshotId));
        var documents = ValidateAndOrder(request.Documents);
        if (!definition.RequiredDocuments.SequenceEqual(documents.Select(document => document.Key)))
        {
            throw new ArgumentException("Publication documents must exactly match the definition.", nameof(request));
        }

        ValidateMetadataJson(request.MetadataJson);
    }

    public static void ValidateLoaded(
        string expectedCompetition,
        string expectedCommunityContext,
        DocumentPublicationDefinition definition,
        DocumentPublicationSnapshot snapshot,
        IEnumerable<PublishedDocument> documents)
    {
        ValidateDefinition(expectedCompetition, definition);
        ValidateScopeValue(expectedCommunityContext, nameof(expectedCommunityContext));
        ArgumentNullException.ThrowIfNull(snapshot);
        var expectedScope = new DocumentPublicationScope(expectedCompetition, expectedCommunityContext, definition.PublicationSet);
        if (snapshot.Scope != expectedScope)
        {
            throw new InvalidDataException("Publication snapshot scope does not match the expected live publication scope.");
        }

        ValidateSnapshot(expectedScope, definition, snapshot);
        ArgumentNullException.ThrowIfNull(documents);
        var loaded = documents.ToImmutableArray();
        if (loaded.Length != snapshot.Documents.Length)
        {
            throw new InvalidDataException($"Publication snapshot '{snapshot.SnapshotId}' loaded {loaded.Length} payloads; expected {snapshot.Documents.Length}.");
        }

        var snapshotPayloads = ImmutableArray.CreateBuilder<DocumentPublicationPayload>(loaded.Length);
        for (var index = 0; index < loaded.Length; index++)
        {
            var payload = loaded[index] ?? throw new InvalidDataException("Publication payload cannot be null.");
            var entry = snapshot.Documents[index];
            if (payload.Scope != expectedScope
                || payload.Key != entry.Key
                || payload.Version != entry.Version)
            {
                throw new InvalidDataException($"Publication snapshot '{snapshot.SnapshotId}' payload identity does not match entry {index}.");
            }

            var actualHash = ComputeContentSha256(payload.Content);
            if (!string.Equals(actualHash, entry.ContentSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Publication snapshot '{snapshot.SnapshotId}' payload hash mismatch for '{entry.Kind}:{entry.Name}'.");
            }

            snapshotPayloads.Add(new DocumentPublicationPayload(payload.Kind, payload.Name, payload.Content, payload.Description));
        }

        var actualSnapshotId = ComputeSnapshotId(snapshotPayloads);
        if (!string.Equals(actualSnapshotId, snapshot.SnapshotId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Publication snapshot ID mismatch: stored '{snapshot.SnapshotId}', computed '{actualSnapshotId}'.");
        }
    }

    public static void ValidateScope(DocumentPublicationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateScopeValue(scope.Competition, nameof(scope.Competition));
        ValidateScopeValue(scope.CommunityContext, nameof(scope.CommunityContext));
        ValidateScopeValue(scope.PublicationSet, nameof(scope.PublicationSet));
    }

    private static void ValidateSnapshot(
        DocumentPublicationScope expectedScope,
        DocumentPublicationDefinition definition,
        DocumentPublicationSnapshot snapshot)
    {
        ValidateScope(snapshot.Scope);
        if (snapshot.Scope != expectedScope)
        {
            throw new InvalidDataException("Publication snapshot scope does not match the expected live publication scope.");
        }

        ValidateSha256(snapshot.SnapshotId, nameof(snapshot.SnapshotId));
        ValidateOptionalSha256(snapshot.PreviousSnapshotId, nameof(snapshot.PreviousSnapshotId));
        ValidateMetadataJson(snapshot.MetadataJson);
        if (snapshot.Documents.Length != definition.RequiredDocuments.Length)
        {
            throw new InvalidDataException(
                $"Publication snapshot '{snapshot.SnapshotId}' contains {snapshot.Documents.Length} documents; " +
                $"expected {definition.RequiredDocuments.Length}.");
        }

        for (var index = 0; index < definition.RequiredDocuments.Length; index++)
        {
            var entry = snapshot.Documents[index] ?? throw new InvalidDataException("Publication snapshot entry cannot be null.");
            if (entry.Key != definition.RequiredDocuments[index])
            {
                throw new InvalidDataException(
                    $"Publication snapshot '{snapshot.SnapshotId}' document {index} is " +
                    $"'{entry.Kind}:{entry.Name}'; expected " +
                    $"'{definition.RequiredDocuments[index].Kind}:{definition.RequiredDocuments[index].Name}'.");
            }

            if (entry.Version < 0)
            {
                throw new InvalidDataException(
                    $"Publication snapshot '{snapshot.SnapshotId}' has negative version {entry.Version} for '{entry.Kind}:{entry.Name}'.");
            }

            ValidateSha256(entry.ContentSha256, nameof(entry.ContentSha256));
        }
    }

    private static void ValidateDefinition(string competition, DocumentPublicationDefinition definition)
    {
        ValidateScopeValue(competition, nameof(competition));
        ArgumentNullException.ThrowIfNull(definition);
        ValidateScopeValue(definition.PublicationSet, nameof(definition.PublicationSet));
        _ = ValidateAndOrderKeys(definition.RequiredDocuments);
        BundesligaDocumentPublication.ThrowIfRedefinedReservedDefinition(competition, definition);
    }

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static byte[] GetUtf8Bytes(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return StrictUtf8.GetBytes(content);
    }

    private static void ValidateUtf8Content(string content, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(content, parameterName);
        _ = GetUtf8Bytes(content);
    }

    private static void ValidateName(string value, string parameterName)
    {
        ValidateScopeValue(value, parameterName);
        if (value.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Publication document names cannot contain '/'.", parameterName);
        }
    }

    private static void ValidateKind(DocumentPublicationKind kind, string parameterName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(parameterName, kind, "Unknown publication document kind.");
        }
    }

    private static string ComputeLengthPrefixedSha256(params byte[][] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values)
        {
            AppendLengthPrefixed(hash, value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void ValidateScopeValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Publication scope values cannot have leading or trailing whitespace.", parameterName);
        }
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        if (!IsLowercaseSha256(value))
        {
            throw new InvalidDataException($"{parameterName} must be a lowercase SHA-256 value.");
        }
    }

    private static void ValidateOptionalSha256(string? value, string parameterName)
    {
        if (value is not null)
        {
            ValidateSha256(value, parameterName);
        }
    }

    private static void ValidateMetadataJson(string metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataJson);
        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Publication metadata JSON must have an object root.", nameof(metadataJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Publication metadata must be valid JSON.", nameof(metadataJson), exception);
        }
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}

public static class BundesligaDocumentPublication
{
    public const string RosterPublicationSet = "rosters";
    public const string ClubEloPublicationSet = "club-elo";
    public const string ClubEloRankingsDocumentName = "club-elo-rankings";

    public static DocumentPublicationDefinition Rosters { get; } = new(
        RosterPublicationSet,
        BundesligaRosterPublicationContract.GetRequiredDocuments().Select(document => new DocumentPublicationKey(
            document.Kind == BundesligaRosterPublicationDocumentKind.Context
                ? DocumentPublicationKind.Context
                : DocumentPublicationKind.Kpi,
            document.Name)));

    public static DocumentPublicationDefinition ClubElo { get; } = new(
        ClubEloPublicationSet,
        BundesligaTeamManifest.Default.Entries
            .OrderBy(team => team.TeamSlug, StringComparer.Ordinal)
            .Select(team => new DocumentPublicationKey(DocumentPublicationKind.Context, $"club-elo-{team.TeamSlug}.csv"))
            .Append(new DocumentPublicationKey(DocumentPublicationKind.Kpi, ClubEloRankingsDocumentName)));

    public static bool IsReserved(string competition, DocumentPublicationKind kind, string documentName)
    {
        return string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
               && (IsRosterKey(kind, documentName) || IsClubEloKey(kind, documentName));
    }

    public static void ThrowIfReservedForGenericMutation(string competition, DocumentPublicationKind kind, string documentName)
    {
        if (IsReserved(competition, kind, documentName))
        {
            throw new InvalidOperationException($"'{kind}:{documentName}' is reserved for atomic Bundesliga document publication.");
        }
    }

    /// <summary>
    /// Resolves the canonical publication definition that owns a concrete Bundesliga document
    /// key. Generic repositories use this only to find immutable publication-scoped payload IDs;
    /// non-canonical reserved-looking names deliberately retain their legacy lookup behavior.
    /// </summary>
    public static bool TryGetOwningDefinition(
        string competition,
        DocumentPublicationKind kind,
        string documentName,
        out DocumentPublicationDefinition? definition)
    {
        definition = null;
        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            return false;
        }

        if (Rosters.RequiredDocuments.Any(key => key.Kind == kind && string.Equals(key.Name, documentName, StringComparison.Ordinal)))
        {
            definition = Rosters;
            return true;
        }

        if (ClubElo.RequiredDocuments.Any(key => key.Kind == kind && string.Equals(key.Name, documentName, StringComparison.Ordinal)))
        {
            definition = ClubElo;
            return true;
        }

        return false;
    }

    internal static void ThrowIfRedefinedReservedDefinition(string competition, DocumentPublicationDefinition definition)
    {
        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            return;
        }

        if (ReferenceEquals(definition, Rosters) || ReferenceEquals(definition, ClubElo))
        {
            return;
        }

        if (definition.RequiredDocuments.Any(key => IsRosterKey(key.Kind, key.Name)))
        {
            throw new ArgumentException(
                "Any definition containing a reserved Bundesliga roster key must use BundesligaDocumentPublication.Rosters.",
                nameof(definition));
        }

        if (definition.RequiredDocuments.Any(key => IsClubEloKey(key.Kind, key.Name)))
        {
            throw new ArgumentException(
                "Any definition containing a reserved Bundesliga Club Elo key must use BundesligaDocumentPublication.ClubElo.",
                nameof(definition));
        }

        if (string.Equals(definition.PublicationSet, RosterPublicationSet, StringComparison.Ordinal))
        {
            throw new ArgumentException("The reserved Bundesliga roster definition must use BundesligaDocumentPublication.Rosters.", nameof(definition));
        }

        if (string.Equals(definition.PublicationSet, ClubEloPublicationSet, StringComparison.Ordinal))
        {
            throw new ArgumentException("The reserved Bundesliga Club Elo definition must use BundesligaDocumentPublication.ClubElo.", nameof(definition));
        }
    }

    private static bool IsRosterKey(DocumentPublicationKind kind, string documentName)
    {
        return (kind == DocumentPublicationKind.Context
                && (string.Equals(documentName, BundesligaRosterPublicationContract.AggregateRosterDocumentName, StringComparison.Ordinal)
                    || documentName.StartsWith("roster-", StringComparison.Ordinal)))
               || (kind == DocumentPublicationKind.Kpi
                   && string.Equals(documentName, BundesligaRosterPublicationContract.SquadSummaryDocumentName, StringComparison.Ordinal));
    }

    private static bool IsClubEloKey(DocumentPublicationKind kind, string documentName)
    {
        return (kind == DocumentPublicationKind.Context
                && documentName.StartsWith("club-elo-", StringComparison.Ordinal)
                && documentName.EndsWith(".csv", StringComparison.Ordinal))
               || (kind == DocumentPublicationKind.Kpi
                   && string.Equals(documentName, ClubEloRankingsDocumentName, StringComparison.Ordinal));
    }
}
