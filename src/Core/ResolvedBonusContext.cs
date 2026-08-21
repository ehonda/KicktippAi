using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Immutable provenance for the exact headed documents selected for one Bundesliga bonus prompt.
/// </summary>
[JsonConverter(typeof(ResolvedBonusContextManifestJsonConverter))]
public sealed class ResolvedBonusContextManifest
{
    private ResolvedBonusContextManifest(
        string competition,
        string communityContext,
        ImmutableArray<ResolvedBonusContextDocument> documents,
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
    public ImmutableArray<ResolvedBonusContextDocument> Documents { get; }
    public string RosterPublicationSnapshotId { get; }
    public string ClubEloPublicationSnapshotId { get; }

    public static ResolvedBonusContextManifest Create(
        string competition,
        string communityContext,
        IEnumerable<ResolvedBonusContextDocument> documents,
        string rosterPublicationSnapshotId,
        string clubEloPublicationSnapshotId)
    {
        if (!string.Equals(competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A resolved bonus-context manifest must use the canonical bundesliga-2026-27 competition ID.",
                nameof(competition));
        }

        ValidateScopeValue(communityContext, nameof(communityContext));
        ValidateSnapshotId(rosterPublicationSnapshotId, nameof(rosterPublicationSnapshotId));
        ValidateSnapshotId(clubEloPublicationSnapshotId, nameof(clubEloPublicationSnapshotId));

        var ordered = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
        ValidateDocuments(ordered, nameof(documents));
        return new ResolvedBonusContextManifest(
            competition,
            communityContext,
            ordered,
            rosterPublicationSnapshotId,
            clubEloPublicationSnapshotId);
    }

    public static void ValidateForCommunity(ResolvedBonusContextManifest manifest, string communityContext)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateScopeValue(communityContext, nameof(communityContext));
        if (!string.Equals(manifest.Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
            || !string.Equals(manifest.CommunityContext, communityContext, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Resolved Bundesliga bonus-context manifest scope does not match the prediction scope.");
        }

        ValidateDocuments(manifest.Documents, nameof(manifest.Documents));
        ValidateSnapshotId(manifest.RosterPublicationSnapshotId, nameof(manifest.RosterPublicationSnapshotId));
        ValidateSnapshotId(manifest.ClubEloPublicationSnapshotId, nameof(manifest.ClubEloPublicationSnapshotId));
    }

    private static void ValidateDocuments(
        ImmutableArray<ResolvedBonusContextDocument> documents,
        string parameterName)
    {
        if (documents.Length < 2)
        {
            throw new ArgumentException(
                "A Bundesliga bonus-context manifest requires the two aggregate baseline documents.",
                parameterName);
        }

        foreach (var document in documents)
        {
            if (document is null
                || document.Version < 0
                || !DocumentPublicationContract.IsLowercaseSha256(document.ContentSha256)
                || document.Kind is not ("Context" or "Kpi")
                || string.IsNullOrWhiteSpace(document.Name)
                || !string.Equals(document.Name, document.Name.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Resolved bonus-context documents require canonical kind/name, a nonnegative version, and a lowercase SHA-256 content hash.",
                    parameterName);
            }
        }

        if (!IsDocument(
                documents[0],
                "Kpi",
                BundesligaDocumentPublication.ClubEloRankingsDocumentName)
            || !IsDocument(
                documents[1],
                "Kpi",
                BundesligaRosterPublicationContract.SquadSummaryDocumentName))
        {
            throw new ArgumentException(
                "Bundesliga bonus-context documents must begin with club-elo-rankings and team-squad-summary in that order.",
                parameterName);
        }

        var validRosterNames = BundesligaTeamManifest.Default.Entries
            .Select(team => $"roster-{team.TeamSlug}")
            .ToHashSet(StringComparer.Ordinal);
        var rosterNames = documents.Skip(2).Select(document => document.Name).ToArray();
        if (documents.Skip(2).Any(document => !string.Equals(document.Kind, "Context", StringComparison.Ordinal)
                                               || !validRosterNames.Contains(document.Name))
            || rosterNames.Distinct(StringComparer.Ordinal).Count() != rosterNames.Length
            || !rosterNames.SequenceEqual(rosterNames.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Bundesliga bonus-context roster documents must be unique canonical roster names in manifest-slug order.",
                parameterName);
        }
    }

    private static bool IsDocument(ResolvedBonusContextDocument document, string kind, string name) =>
        string.Equals(document.Kind, kind, StringComparison.Ordinal)
        && string.Equals(document.Name, name, StringComparison.Ordinal);

    private static void ValidateScopeValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Bonus-context scope values cannot have surrounding whitespace.", parameterName);
        }
    }

    private static void ValidateSnapshotId(string snapshotId, string parameterName)
    {
        if (!DocumentPublicationContract.IsLowercaseSha256(snapshotId))
        {
            throw new ArgumentException("Publication snapshot IDs must be lowercase SHA-256 values.", parameterName);
        }
    }
}

public sealed class ResolvedBonusContextDocument
{
    public ResolvedBonusContextDocument(string kind, string name, int version, string contentSha256)
    {
        Kind = kind;
        Name = name;
        Version = version;
        ContentSha256 = contentSha256;
    }

    public string Kind { get; }
    public string Name { get; }
    public int Version { get; }
    public string ContentSha256 { get; }
}

/// <summary>Canonical, fail-closed JSON contract for persisted Bundesliga bonus provenance.</summary>
public sealed class ResolvedBonusContextManifestJsonConverter : JsonConverter<ResolvedBonusContextManifest>
{
    private static readonly string[] RootProperties =
    [
        "competition",
        "communityContext",
        "documents",
        "rosterPublicationSnapshotId",
        "clubEloPublicationSnapshotId"
    ];

    private static readonly string[] DocumentProperties = ["kind", "name", "version", "contentSha256"];

    public override ResolvedBonusContextManifest Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var json = JsonDocument.ParseValue(ref reader);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Resolved bonus-context manifest must be an object.");
        }

        var properties = json.RootElement.EnumerateObject().ToArray();
        if (!properties.Select(property => property.Name).SequenceEqual(RootProperties, StringComparer.Ordinal)
            || properties[2].Value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException(
                "Resolved bonus-context manifest has an unknown, missing, duplicate, or noncanonical field.");
        }

        try
        {
            return ResolvedBonusContextManifest.Create(
                RequiredString(properties[0].Value, "competition"),
                RequiredString(properties[1].Value, "communityContext"),
                properties[2].Value.EnumerateArray().Select(ParseDocument),
                RequiredString(properties[3].Value, "rosterPublicationSnapshotId"),
                RequiredString(properties[4].Value, "clubEloPublicationSnapshotId"));
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Resolved bonus-context manifest is invalid.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        ResolvedBonusContextManifest value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("competition", value.Competition);
        writer.WriteString("communityContext", value.CommunityContext);
        writer.WritePropertyName("documents");
        writer.WriteStartArray();
        foreach (var document in value.Documents)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", document.Kind);
            writer.WriteString("name", document.Name);
            writer.WriteNumber("version", document.Version);
            writer.WriteString("contentSha256", document.ContentSha256);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteString("rosterPublicationSnapshotId", value.RosterPublicationSnapshotId);
        writer.WriteString("clubEloPublicationSnapshotId", value.ClubEloPublicationSnapshotId);
        writer.WriteEndObject();
    }

    private static ResolvedBonusContextDocument ParseDocument(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Resolved bonus-context manifest document must be an object.");
        }

        var properties = element.EnumerateObject().ToArray();
        if (!properties.Select(property => property.Name).SequenceEqual(DocumentProperties, StringComparer.Ordinal)
            || properties[2].Value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException(
                "Resolved bonus-context manifest document has an unknown, missing, duplicate, or noncanonical field.");
        }

        return new ResolvedBonusContextDocument(
            RequiredString(properties[0].Value, "kind"),
            RequiredString(properties[1].Value, "name"),
            properties[2].Value.GetInt32(),
            RequiredString(properties[3].Value, "contentSha256"));
    }

    private static string RequiredString(JsonElement element, string fieldName) =>
        element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!
            : throw new JsonException(
                $"Resolved bonus-context manifest field '{fieldName}' must be a nonempty string.");
}

public sealed record ResolvedBonusContext
{
    public ResolvedBonusContext(
        IEnumerable<DocumentContext> documents,
        ResolvedBonusContextManifest manifest,
        ResolvedBonusContextSelection selection)
    {
        Documents = documents?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(documents));
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
        if (!Enum.IsDefined(selection.Category)
            || selection.SelectedDocumentNames.IsDefault
            || selection.ExcludedDocuments.IsDefault
            || selection.SelectedDocumentNames.Any(string.IsNullOrWhiteSpace)
            || selection.SelectedDocumentNames.Distinct(StringComparer.Ordinal).Count()
            != selection.SelectedDocumentNames.Length
            || selection.ExcludedDocuments.Any(exclusion =>
                exclusion is null || string.IsNullOrWhiteSpace(exclusion.Document.Name))
            || selection.ExcludedDocuments.Select(exclusion => exclusion.Document.Name)
                .Distinct(StringComparer.Ordinal)
                .Count() != selection.ExcludedDocuments.Length
            || selection.ExcludedDocuments.Any(exclusion =>
                selection.SelectedDocumentNames.Contains(exclusion.Document.Name, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "Resolved bonus context selection requires a known category and unique, disjoint canonical selected/excluded documents.",
                nameof(selection));
        }

        ImmutableArray<BonusContextDocumentExclusion> expectedExclusions;
        try
        {
            expectedExclusions = BonusContextSelectionPolicy.GetCanonicalExclusions(
                selection.Category,
                selection.SelectedDocumentNames);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Resolved bonus context selection does not use canonical Bundesliga roster documents.",
                nameof(selection),
                exception);
        }

        if (!selection.ExcludedDocuments.SequenceEqual(expectedExclusions))
        {
            throw new ArgumentException(
                "Resolved bonus context selection does not contain the exact canonical exclusion ledger.",
                nameof(selection));
        }

        if (!Documents.Select(document => document.Name)
                .SequenceEqual(manifest.Documents.Select(document => document.Name), StringComparer.Ordinal)
            || Documents.Where((document, index) => !string.Equals(
                    DocumentPublicationContract.ComputeContentSha256(document.Content),
                    manifest.Documents[index].ContentSha256,
                    StringComparison.Ordinal))
                .Any())
        {
            throw new ArgumentException(
                "Resolved bonus context documents do not match the immutable manifest names, order, or hashes.",
                nameof(documents));
        }

        if (!Documents.Select(document => document.Name)
                .SequenceEqual(selection.SelectedDocumentNames, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Resolved bonus context selection does not match the exact document names and order.",
                nameof(selection));
        }

        var measurement = BonusContextBudgetEstimator.Measure(Documents);
        if (measurement.Utf8Bytes != selection.EstimatedUtf8Bytes
            || measurement.EstimatedTokens != selection.EstimatedTokens)
        {
            throw new ArgumentException(
                "Resolved bonus context selection does not match the deterministic context-size estimate.",
                nameof(selection));
        }

        BonusContextBudgetEstimator.EnsureFits(Documents.Length, measurement, selection.Budget);
    }

    public ImmutableArray<DocumentContext> Documents { get; }
    public ResolvedBonusContextManifest Manifest { get; }
    public ResolvedBonusContextSelection Selection { get; }
}

/// <summary>Optional capability for providers that return exact Bundesliga bonus provenance.</summary>
public interface IResolvedBonusContextProvider
{
    Task<ResolvedBonusContext> ResolveBonusQuestionContextAsync(
        BonusQuestion question,
        string communityContext,
        CancellationToken cancellationToken = default,
        BonusContextBudget? budget = null);
}

/// <summary>Optional capability for prediction stores that persist exact Bundesliga bonus provenance.</summary>
public interface IResolvedBonusContextPredictionRepository
{
    Task SaveBonusPredictionWithResolvedContextAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedBonusContextManifest resolvedContextManifest,
        bool overrideCreatedAt = false,
        CancellationToken cancellationToken = default);

    Task SaveBonusRepredictionWithResolvedContextAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        int repredictionIndex,
        ResolvedBonusContextManifest resolvedContextManifest,
        CancellationToken cancellationToken = default);
}
