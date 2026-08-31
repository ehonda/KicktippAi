using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaGenerationPredecessor
{
    private BundesligaGenerationPredecessor(int generation, string sha256) =>
        (Generation, Sha256) = (generation, sha256);

    public int Generation { get; }
    public string Sha256 { get; }

    public static BundesligaGenerationPredecessor Create(int generation, string sha256)
    {
        BundesligaPredictionContractValidation.Generation(generation, nameof(generation));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new BundesligaGenerationPredecessor(generation, sha256);
    }
}

public sealed class BundesligaIdentitySeedEntry
{
    private BundesligaIdentitySeedEntry(
        string routeId,
        TypedMatchSnapshot? matchSnapshot,
        TypedBonusSnapshot? bonusSnapshot)
    {
        RouteId = routeId;
        MatchSnapshot = matchSnapshot;
        BonusSnapshot = bonusSnapshot;
    }

    public string RouteId { get; }
    public TypedMatchSnapshot? MatchSnapshot { get; }
    public TypedBonusSnapshot? BonusSnapshot { get; }
    public StableLocalItemKey Key => MatchSnapshot?.Key ?? BonusSnapshot!.Key;
    public BundesligaPredictionSnapshotHash SnapshotHash =>
        MatchSnapshot?.SnapshotHash ?? BonusSnapshot!.SnapshotHash;
    public BundesligaSeasonSubcompetition Subcompetition =>
        MatchSnapshot?.Subcompetition ?? BonusSnapshot!.Subcompetition;

    public static BundesligaIdentitySeedEntry ForMatch(
        string routeId,
        TypedMatchSnapshot snapshot,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateRoute(routeId, snapshot.Key.ItemKind, snapshot.Subcompetition, routes);
        return new BundesligaIdentitySeedEntry(routeId, snapshot, null);
    }

    public static BundesligaIdentitySeedEntry ForBonus(
        string routeId,
        TypedBonusSnapshot snapshot,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateRoute(routeId, snapshot.Key.ItemKind, snapshot.Subcompetition, routes);
        return new BundesligaIdentitySeedEntry(routeId, null, snapshot);
    }

    private static void ValidateRoute(
        string routeId,
        BundesligaPredictionItemKind itemKind,
        BundesligaSeasonSubcompetition subcompetition,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        routes.Require(routeId, itemKind, subcompetition);
    }
}

public sealed class BundesligaIdentitySeedGeneration
{
    public const string SchemaVersionValue = "bundesliga-identity-seed-v1";
    private readonly ImmutableArray<BundesligaIdentitySeedEntry> _entries;

    private BundesligaIdentitySeedGeneration(
        string postingCommunity,
        int generation,
        BundesligaGenerationPredecessor? predecessor,
        string sourceEvidenceIdentity,
        IEnumerable<BundesligaIdentitySeedEntry> entries)
    {
        PostingCommunity = postingCommunity;
        Generation = generation;
        Predecessor = predecessor;
        SourceEvidenceIdentity = sourceEvidenceIdentity;
        _entries = entries.ToImmutableArray();
        CanonicalSha256 = BundesligaPredictionCanonicalJson.Sha256(SerializeCanonical());
    }

    public string SchemaVersion => SchemaVersionValue;
    public string SeasonPartition => BundesligaPredictionAuthority.SeasonPartitionValue;
    public string PostingCommunity { get; }
    public int Generation { get; }
    public BundesligaGenerationPredecessor? Predecessor { get; }
    public string SourceEvidenceIdentity { get; }
    public IReadOnlyList<BundesligaIdentitySeedEntry> Entries => _entries;
    public string CanonicalSha256 { get; }
    public BundesligaIdentitySeedReference Reference =>
        BundesligaIdentitySeedReference.Create(Generation, CanonicalSha256);

    public static BundesligaIdentitySeedGeneration Create(
        string postingCommunity,
        int generation,
        BundesligaGenerationPredecessor? predecessor,
        string sourceEvidenceIdentity,
        IEnumerable<BundesligaIdentitySeedEntry> entries,
        BundesligaPredictionRouteCatalog routes)
    {
        BundesligaPredictionContractValidation.Community(postingCommunity, nameof(postingCommunity));
        BundesligaPredictionContractValidation.Generation(generation, nameof(generation));
        BundesligaPredictionContractValidation.Identifier(sourceEvidenceIdentity, nameof(sourceEvidenceIdentity));
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(routes);
        ValidatePredecessor(generation, predecessor);

        var materialized = entries
            .OrderBy(entry => BundesligaPredictionCanonicalJson.ItemKind(entry.Key.ItemKind), StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.KicktippItemId, StringComparer.Ordinal)
            .ToArray();
        if (materialized.Length == 0)
        {
            throw new InvalidDataException("Identity Seed Generation must contain at least one item.");
        }

        foreach (var entry in materialized)
        {
            if (entry is null
                || !string.Equals(entry.Key.SeasonPartition, BundesligaPredictionAuthority.SeasonPartitionValue, StringComparison.Ordinal)
                || !string.Equals(entry.Key.PostingCommunity, postingCommunity, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Identity Seed Generation entry has a cross-season or cross-community key.");
            }

            routes.Require(entry.RouteId, entry.Key.ItemKind, entry.Subcompetition);
        }

        var duplicate = materialized
            .GroupBy(entry => entry.Key)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Duplicate local item key '{duplicate.Key.KicktippItemId}'.");
        }

        return new BundesligaIdentitySeedGeneration(
            postingCommunity,
            generation,
            predecessor,
            sourceEvidenceIdentity,
            materialized);
    }

    public byte[] SerializeCanonical() => BundesligaIdentitySeedCanonicalJson.Serialize(this);

    public static BundesligaIdentitySeedGeneration DeserializeCanonical(
        ReadOnlySpan<byte> bytes,
        BundesligaPredictionRouteCatalog routes) =>
        BundesligaIdentitySeedCanonicalJson.Deserialize(bytes, routes);

    public BundesligaIdentitySeedEntry RequireEntry(StableLocalItemKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.SingleOrDefault(entry => entry.Key == key)
            ?? throw new InvalidDataException(
                $"Item '{key.KicktippItemId}' is absent from pinned seed generation {Generation}.");
    }

    private static void ValidatePredecessor(
        int generation,
        BundesligaGenerationPredecessor? predecessor)
    {
        if (generation == 1 && predecessor is not null)
        {
            throw new InvalidDataException("Generation 1 cannot declare a predecessor.");
        }

        if (generation > 1 && (predecessor is null || predecessor.Generation != generation - 1))
        {
            throw new InvalidDataException(
                "Every additive generation after 1 must pin the immediately preceding generation and hash.");
        }
    }
}

internal static class BundesligaIdentitySeedCanonicalJson
{
    public static byte[] Serialize(BundesligaIdentitySeedGeneration seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", seed.SchemaVersion);
            writer.WriteString("seasonPartition", seed.SeasonPartition);
            writer.WriteString("postingCommunity", seed.PostingCommunity);
            writer.WriteNumber("generation", seed.Generation);
            writer.WritePropertyName("predecessor");
            WritePredecessor(writer, seed.Predecessor);
            writer.WriteString("sourceEvidenceIdentity", seed.SourceEvidenceIdentity);
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (var entry in seed.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("routeId", entry.RouteId);
                writer.WritePropertyName("snapshotHash");
                BundesligaPredictionSnapshotCanonicalJson.WriteHash(writer, entry.SnapshotHash);
                writer.WritePropertyName("snapshot");
                writer.WriteRawValue(
                    entry.MatchSnapshot?.SerializeCanonical()
                        ?? entry.BonusSnapshot!.SerializeCanonical(),
                    skipInputValidation: true);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static BundesligaIdentitySeedGeneration Deserialize(
        ReadOnlySpan<byte> bytes,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Identity Seed Generation");
        var root = document.RootElement;
        BundesligaPredictionCanonicalJson.Properties(
            root,
            "schemaVersion",
            "seasonPartition",
            "postingCommunity",
            "generation",
            "predecessor",
            "sourceEvidenceIdentity",
            "entries");
        if (!string.Equals(
                BundesligaPredictionCanonicalJson.String(root, "schemaVersion"),
                BundesligaIdentitySeedGeneration.SchemaVersionValue,
                StringComparison.Ordinal)
            || !string.Equals(
                BundesligaPredictionCanonicalJson.String(root, "seasonPartition"),
                BundesligaPredictionAuthority.SeasonPartitionValue,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Identity Seed Generation has an unknown schema or season.");
        }

        var entriesElement = root.GetProperty("entries");
        if (entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Identity seed entries must be an array.");
        }

        var entries = entriesElement.EnumerateArray().Select(element => ParseEntry(element, routes)).ToArray();
        var seed = BundesligaIdentitySeedGeneration.Create(
            BundesligaPredictionCanonicalJson.String(root, "postingCommunity"),
            BundesligaPredictionCanonicalJson.Int32(root, "generation"),
            ReadPredecessor(root.GetProperty("predecessor")),
            BundesligaPredictionCanonicalJson.String(root, "sourceEvidenceIdentity"),
            entries,
            routes);
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, Serialize(seed), "Identity Seed Generation");
        return seed;
    }

    private static BundesligaIdentitySeedEntry ParseEntry(
        JsonElement element,
        BundesligaPredictionRouteCatalog routes)
    {
        BundesligaPredictionCanonicalJson.Properties(element, "routeId", "snapshotHash", "snapshot");
        var routeId = BundesligaPredictionCanonicalJson.String(element, "routeId");
        var expectedHash = BundesligaPredictionSnapshotCanonicalJson.ReadHash(
            element.GetProperty("snapshotHash"));
        var snapshotElement = element.GetProperty("snapshot");
        if (snapshotElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Identity seed snapshot must be an object.");
        }

        var snapshotBytes = Encoding.UTF8.GetBytes(snapshotElement.GetRawText());
        var schema = BundesligaPredictionCanonicalJson.String(snapshotElement, "schemaVersion");
        BundesligaIdentitySeedEntry entry = schema switch
        {
            TypedMatchSnapshot.SchemaVersionValue => BundesligaIdentitySeedEntry.ForMatch(
                routeId,
                TypedMatchSnapshot.DeserializeCanonical(snapshotBytes),
                routes),
            TypedBonusSnapshot.SchemaVersionValue => BundesligaIdentitySeedEntry.ForBonus(
                routeId,
                TypedBonusSnapshot.DeserializeCanonical(snapshotBytes),
                routes),
            _ => throw new InvalidDataException($"Unknown identity seed snapshot schema '{schema}'.")
        };
        if (entry.SnapshotHash != expectedHash)
        {
            throw new InvalidDataException("Identity seed snapshot hash does not match canonical snapshot bytes.");
        }

        return entry;
    }

    private static void WritePredecessor(
        Utf8JsonWriter writer,
        BundesligaGenerationPredecessor? predecessor)
    {
        if (predecessor is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("generation", predecessor.Generation);
        writer.WriteString("sha256", predecessor.Sha256);
        writer.WriteEndObject();
    }

    private static BundesligaGenerationPredecessor? ReadPredecessor(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        BundesligaPredictionCanonicalJson.Properties(element, "generation", "sha256");
        return BundesligaGenerationPredecessor.Create(
            BundesligaPredictionCanonicalJson.Int32(element, "generation"),
            BundesligaPredictionCanonicalJson.String(element, "sha256"));
    }
}
