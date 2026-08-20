using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Immutable provenance for every context document that entered a Bundesliga match prompt.
/// Reserved roster and Club Elo entries are anchored by their complete publication snapshots.
/// </summary>
[JsonConverter(typeof(ResolvedMatchContextManifestJsonConverter))]
public sealed class ResolvedMatchContextManifest
{
    private ResolvedMatchContextManifest(
        string competition,
        string communityContext,
        ImmutableArray<ResolvedMatchContextDocument> documents,
        string rosterPublicationSnapshotId,
        string clubEloPublicationSnapshotId)
    {
        Competition = competition;
        CommunityContext = communityContext;
        Documents = documents;
        RosterPublicationSnapshotId = rosterPublicationSnapshotId;
        ClubEloPublicationSnapshotId = clubEloPublicationSnapshotId;
    }

    public string Competition { get; }
    public string CommunityContext { get; }
    public ImmutableArray<ResolvedMatchContextDocument> Documents { get; }
    public string RosterPublicationSnapshotId { get; }
    public string ClubEloPublicationSnapshotId { get; }

    public static ResolvedMatchContextManifest Create(
        string competition,
        string communityContext,
        IEnumerable<ResolvedMatchContextDocument> documents,
        string rosterPublicationSnapshotId,
        string clubEloPublicationSnapshotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(rosterPublicationSnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clubEloPublicationSnapshotId);
        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new ArgumentException("A resolved match-context manifest must use the canonical bundesliga-2026-27 competition ID.", nameof(competition));
        }
        var ordered = documents?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(documents));
        if (ordered.Length != 11
            || ordered.Select(document => document.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Any(document => string.IsNullOrWhiteSpace(document.Name)
                                       || document.Version < 0
                                       || !string.Equals(document.Kind, "Context", StringComparison.Ordinal)
                                       || !DocumentPublicationContract.IsLowercaseSha256(document.ContentSha256)))
        {
            throw new ArgumentException("A Bundesliga match context manifest must contain exactly eleven unique Context documents with nonnegative versions and lowercase SHA-256 content hashes.", nameof(documents));
        }
        ValidateSnapshotId(rosterPublicationSnapshotId, nameof(rosterPublicationSnapshotId));
        ValidateSnapshotId(clubEloPublicationSnapshotId, nameof(clubEloPublicationSnapshotId));

        return new ResolvedMatchContextManifest(
            competition,
            communityContext,
            ordered,
            rosterPublicationSnapshotId,
            clubEloPublicationSnapshotId);
    }

    public static void ValidateForMatch(ResolvedMatchContextManifest manifest, Match match, string communityContext)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(match);
        if (!string.Equals(manifest.CommunityContext, communityContext, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Resolved Bundesliga context manifest community scope does not match the prediction scope.");
        }

        var expected = MatchContextDocumentCatalog.ForMatch(match, communityContext, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames;
        if (!manifest.Documents.Select(document => document.Name).SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Resolved Bundesliga context manifest document names or canonical order do not match the match contract.");
        }
    }

    private static void ValidateSnapshotId(string snapshotId, string parameterName)
    {
        if (snapshotId.Length != DocumentPublicationContract.Sha256HexLength
            || snapshotId.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Publication snapshot IDs must be lowercase SHA-256 values.", parameterName);
        }
    }
}

public sealed class ResolvedMatchContextDocument
{
    public ResolvedMatchContextDocument(string name, int version, string kind, string contentSha256)
    {
        Name = name;
        Version = version;
        Kind = kind;
        ContentSha256 = contentSha256;
    }

    public string Name { get; }
    public int Version { get; }
    public string Kind { get; }
    public string ContentSha256 { get; }
}

/// <summary>Canonical, fail-closed JSON contract shared by persisted and file-backed manifests.</summary>
public sealed class ResolvedMatchContextManifestJsonConverter : JsonConverter<ResolvedMatchContextManifest>
{
    public override ResolvedMatchContextManifest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var json = JsonDocument.ParseValue(ref reader);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Resolved context manifest must be an object.");
        }

        var properties = json.RootElement.EnumerateObject().ToArray();
        var expected = new[] { "competition", "communityContext", "documents", "rosterPublicationSnapshotId", "clubEloPublicationSnapshotId" };
        if (!properties.Select(property => property.Name).SequenceEqual(expected, StringComparer.Ordinal)
            || properties[2].Value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Resolved context manifest has an unknown, missing, duplicate, or noncanonical field.");
        }

        var documents = properties[2].Value.EnumerateArray().Select(ParseDocument).ToArray();
        try
        {
            return ResolvedMatchContextManifest.Create(
                RequiredString(properties[0].Value, "competition"),
                RequiredString(properties[1].Value, "communityContext"),
                documents,
                RequiredString(properties[3].Value, "rosterPublicationSnapshotId"),
                RequiredString(properties[4].Value, "clubEloPublicationSnapshotId"));
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Resolved context manifest is invalid.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, ResolvedMatchContextManifest value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("competition", value.Competition);
        writer.WriteString("communityContext", value.CommunityContext);
        writer.WritePropertyName("documents");
        writer.WriteStartArray();
        foreach (var document in value.Documents)
        {
            writer.WriteStartObject();
            writer.WriteString("name", document.Name);
            writer.WriteNumber("version", document.Version);
            writer.WriteString("kind", document.Kind);
            writer.WriteString("contentSha256", document.ContentSha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("rosterPublicationSnapshotId", value.RosterPublicationSnapshotId);
        writer.WriteString("clubEloPublicationSnapshotId", value.ClubEloPublicationSnapshotId);
        writer.WriteEndObject();
    }

    private static ResolvedMatchContextDocument ParseDocument(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Resolved context manifest document must be an object.");
        }

        var properties = element.EnumerateObject().ToArray();
        var expected = new[] { "name", "version", "kind", "contentSha256" };
        if (!properties.Select(property => property.Name).SequenceEqual(expected, StringComparer.Ordinal)
            || properties[1].Value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("Resolved context manifest document has an unknown, missing, duplicate, or noncanonical field.");
        }

        return new ResolvedMatchContextDocument(
            RequiredString(properties[0].Value, "name"),
            properties[1].Value.GetInt32(),
            RequiredString(properties[2].Value, "kind"),
            RequiredString(properties[3].Value, "contentSha256"));
    }

    private static string RequiredString(JsonElement element, string fieldName) =>
        element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!
            : throw new JsonException($"Resolved context manifest field '{fieldName}' must be a nonempty string.");
}

public sealed record ResolvedBundesligaMatchContext(
    ImmutableArray<DocumentContext> Documents,
    ImmutableArray<ResolvedContextDocumentVersion> ResolvedDocuments,
    ResolvedMatchContextManifest Manifest);

/// <summary>
/// The single live read boundary for Bundesliga match context. Generic documents use their normal
/// versioned repository path; the four reserved team documents always use one validated head per
/// publication set and are never queried through generic latest-version APIs.
/// </summary>
public sealed class BundesligaMatchContextResolver
{
    private readonly IContextRepository _contextRepository;
    private readonly IDocumentPublicationRepository _publicationRepository;

    public BundesligaMatchContextResolver(
        IContextRepository contextRepository,
        IDocumentPublicationRepository publicationRepository)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _publicationRepository = publicationRepository ?? throw new ArgumentNullException(nameof(publicationRepository));
    }

    public async Task<ResolvedBundesligaMatchContext> ResolveLiveAsync(
        Match match,
        string communityContext,
        Func<string, CancellationToken, Task<DocumentContext?>>? onDemandDocument = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        if (!string.Equals(_publicationRepository.Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Bundesliga match context resolver requires a bundesliga-2026-27 publication repository.");
        }

        var selection = MatchContextDocumentCatalog.ForMatch(match, communityContext, CompetitionIds.Bundesliga2026_27);
        var reservedNames = selection.RequiredDocumentNames
            .Where(IsReservedDocumentName)
            .ToHashSet(StringComparer.Ordinal);
        var documents = new List<DocumentContext>(selection.RequiredDocumentNames.Count);
        var entries = new List<ResolvedMatchContextDocument>(selection.RequiredDocumentNames.Count);
        var resolvedDocuments = new List<ResolvedContextDocumentVersion>(selection.RequiredDocumentNames.Count);

        // Only the seven ordinary contract documents use the generic repository. If a caller
        // supplies an on-demand document, materialize it and re-read its exact stored version
        // before it can enter a persisted prompt. Reserved documents never enter this branch.
        foreach (var name in selection.RequiredDocumentNames.Where(name => !reservedNames.Contains(name)))
        {
            var document = await _contextRepository.GetLatestContextDocumentAsync(name, communityContext, cancellationToken);
            if (document is null)
            {
                var generated = onDemandDocument is null
                    ? null
                    : await onDemandDocument(name, cancellationToken);
                if (generated is null || !string.Equals(generated.Name, name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Missing required Bundesliga context document '{name}'.");
                }

                var savedVersion = await _contextRepository.SaveContextDocumentAsync(
                    name,
                    generated.Content,
                    communityContext,
                    cancellationToken);
                // A null save result means an equal version may have been materialized by a
                // concurrent writer. Resolve that version once, then re-read it by its exact
                // identity before recording it in the immutable manifest.
                if (savedVersion is not int version)
                {
                    var latest = await _contextRepository.GetLatestContextDocumentAsync(name, communityContext, cancellationToken);
                    version = latest?.Version ?? -1;
                }

                document = version >= 0
                    ? await _contextRepository.GetContextDocumentAsync(name, version, communityContext, cancellationToken)
                    : null;
                if (document is null
                    || document.Version != version
                    || !string.Equals(document.Content, generated.Content, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Could not materialize and verify on-demand Bundesliga context document '{name}'.");
                }
            }

            if (!string.Equals(document.DocumentName, name, StringComparison.Ordinal) || document.Version < 0)
            {
                throw new InvalidDataException($"Resolved ordinary Bundesliga context document '{name}' has an invalid exact identity.");
            }

            documents.Add(new DocumentContext(document.DocumentName, document.Content));
            entries.Add(new ResolvedMatchContextDocument(
                document.DocumentName,
                document.Version,
                "Context",
                DocumentPublicationContract.ComputeContentSha256(document.Content)));
            resolvedDocuments.Add(new ResolvedContextDocumentVersion(document.DocumentName, document.Version, document.CreatedAt, document.Content));
        }

        var rosters = await RequireHeadAsync(BundesligaDocumentPublication.Rosters, communityContext, cancellationToken);
        var clubElo = await RequireHeadAsync(BundesligaDocumentPublication.ClubElo, communityContext, cancellationToken);
        ValidateSemanticPublication(BundesligaDocumentPublication.Rosters, rosters);
        ValidateSemanticPublication(BundesligaDocumentPublication.ClubElo, clubElo);
        AppendReserved(selection.RequiredDocumentNames.Where(IsRosterDocumentName), rosters, documents, entries, resolvedDocuments);
        AppendReserved(selection.RequiredDocumentNames.Where(IsClubEloDocumentName), clubElo, documents, entries, resolvedDocuments);

        var manifest = ResolvedMatchContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            communityContext,
            entries,
            rosters.Snapshot.SnapshotId,
            clubElo.Snapshot.SnapshotId);
        return new ResolvedBundesligaMatchContext(documents.ToImmutableArray(), resolvedDocuments.ToImmutableArray(), manifest);
    }

    public async Task<ResolvedBundesligaMatchContext> ResolveRecordedAsync(
        Match match,
        ResolvedMatchContextManifest manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The resolved context manifest is not for bundesliga-2026-27.");
        }

        var selection = MatchContextDocumentCatalog.ForMatch(match, manifest.CommunityContext, manifest.Competition);
        ResolvedMatchContextManifest.ValidateForMatch(manifest, match, manifest.CommunityContext);
        var recorded = manifest.Documents.ToDictionary(document => document.Name, StringComparer.Ordinal);
        if (!selection.RequiredDocumentNames.All(recorded.ContainsKey) || recorded.Count != selection.RequiredDocumentNames.Count)
        {
            throw new InvalidDataException("The resolved Bundesliga context manifest does not match the required eleven-document contract.");
        }

        var documents = new List<DocumentContext>(selection.RequiredDocumentNames.Count);
        var resolvedDocuments = new List<ResolvedContextDocumentVersion>(selection.RequiredDocumentNames.Count);
        var entries = new List<ResolvedMatchContextDocument>();
        foreach (var name in selection.RequiredDocumentNames.Where(name => !IsReservedDocumentName(name)))
        {
            var entry = recorded[name];
            var document = await _contextRepository.GetContextDocumentAsync(name, entry.Version, manifest.CommunityContext, cancellationToken);
            if (document is null)
            {
                throw new InvalidDataException($"Recorded context version '{name}' v{entry.Version} is missing.");
            }
            if (!string.Equals(document.DocumentName, name, StringComparison.Ordinal)
                || document.Version != entry.Version
                || !string.Equals(
                    DocumentPublicationContract.ComputeContentSha256(document.Content),
                    entry.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Recorded context version '{name}' v{entry.Version} resolved to a different identity or content hash.");
            }

            documents.Add(new DocumentContext(document.DocumentName, document.Content));
            resolvedDocuments.Add(new ResolvedContextDocumentVersion(document.DocumentName, document.Version, document.CreatedAt, document.Content));
        }

        var rosters = await RequireSnapshotAsync(BundesligaDocumentPublication.Rosters, manifest.CommunityContext, manifest.RosterPublicationSnapshotId, cancellationToken);
        var clubElo = await RequireSnapshotAsync(BundesligaDocumentPublication.ClubElo, manifest.CommunityContext, manifest.ClubEloPublicationSnapshotId, cancellationToken);
        ValidateSemanticPublication(BundesligaDocumentPublication.Rosters, rosters);
        ValidateSemanticPublication(BundesligaDocumentPublication.ClubElo, clubElo);
        AppendReserved(selection.RequiredDocumentNames.Where(IsRosterDocumentName), rosters, documents, entries, resolvedDocuments, recorded);
        AppendReserved(selection.RequiredDocumentNames.Where(IsClubEloDocumentName), clubElo, documents, entries, resolvedDocuments, recorded);
        return new ResolvedBundesligaMatchContext(documents.ToImmutableArray(), resolvedDocuments.ToImmutableArray(), manifest);
    }

    private async Task<LoadedDocumentPublication> RequireHeadAsync(DocumentPublicationDefinition definition, string communityContext, CancellationToken cancellationToken) =>
        await _publicationRepository.GetLastKnownGoodAsync(definition, communityContext, cancellationToken)
        ?? throw new InvalidOperationException($"Missing valid headed {definition.PublicationSet} publication for Bundesliga context.");

    private async Task<LoadedDocumentPublication> RequireSnapshotAsync(DocumentPublicationDefinition definition, string communityContext, string snapshotId, CancellationToken cancellationToken) =>
        await _publicationRepository.GetSnapshotAsync(definition, communityContext, snapshotId, cancellationToken)
        ?? throw new InvalidDataException($"Recorded {definition.PublicationSet} publication snapshot '{snapshotId}' is missing.");

    private static void ValidateSemanticPublication(DocumentPublicationDefinition definition, LoadedDocumentPublication publication)
    {
        if (ReferenceEquals(definition, BundesligaDocumentPublication.Rosters))
        {
            _ = BundesligaRosterPublication.ReconstructLastKnownGood(publication);
            return;
        }

        if (ReferenceEquals(definition, BundesligaDocumentPublication.ClubElo))
        {
            _ = BundesligaClubEloPublication.ReconstructLastKnownGood(publication);
            return;
        }

        throw new InvalidOperationException($"Unexpected Bundesliga publication definition '{definition.PublicationSet}'.");
    }

    private static void AppendReserved(
        IEnumerable<string> requiredNames,
        LoadedDocumentPublication publication,
        ICollection<DocumentContext> documents,
        ICollection<ResolvedMatchContextDocument> entries,
        ICollection<ResolvedContextDocumentVersion> resolvedDocuments,
        IReadOnlyDictionary<string, ResolvedMatchContextDocument>? recorded = null)
    {
        var expectedNames = requiredNames.ToArray();
        if (expectedNames.Length == 0)
        {
            throw new InvalidDataException($"Publication snapshot '{publication.Snapshot.SnapshotId}' has no required match-team documents.");
        }

        foreach (var name in expectedNames)
        {
            var document = publication.Documents.SingleOrDefault(document => document.Name == name)
                ?? throw new InvalidDataException(
                    $"Publication snapshot '{publication.Snapshot.SnapshotId}' is missing required match-team document '{name}'.");
            if (recorded is not null
                && (!recorded.TryGetValue(name, out var entry)
                    || entry.Version != document.Version
                    || !string.Equals(
                        entry.ContentSha256,
                        DocumentPublicationContract.ComputeContentSha256(document.Content),
                        StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"Recorded reserved context version or content hash '{name}' does not match publication snapshot '{publication.Snapshot.SnapshotId}'.");
            }

            documents.Add(new DocumentContext(document.Name, document.Content));
            entries.Add(new ResolvedMatchContextDocument(
                document.Name,
                document.Version,
                "Context",
                DocumentPublicationContract.ComputeContentSha256(document.Content)));
            resolvedDocuments.Add(new ResolvedContextDocumentVersion(document.Name, document.Version, document.CreatedAt, document.Content));
        }
    }

    private static bool IsReservedDocumentName(string name) => IsRosterDocumentName(name) || IsClubEloDocumentName(name);

    private static bool IsRosterDocumentName(string name) => name.StartsWith("roster-", StringComparison.Ordinal);

    private static bool IsClubEloDocumentName(string name) => name.StartsWith("club-elo-", StringComparison.Ordinal);
}
