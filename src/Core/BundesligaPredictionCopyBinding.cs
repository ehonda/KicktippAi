using System.Collections.Immutable;
using System.Text.Json;

namespace EHonda.KicktippAi.Core;

public sealed record BundesligaBonusOptionProjection(string SourceOptionId, string PostingOptionId);

public sealed class BundesligaCopyBindingEntry
{
    private readonly ImmutableArray<BundesligaBonusOptionProjection> _optionProjection;

    private BundesligaCopyBindingEntry(
        string routeId,
        StableLocalItemKey postingKey,
        BundesligaPredictionSnapshotHash postingSnapshotHash,
        BundesligaIdentitySeedReference postingSeed,
        StableLocalItemKey sourceKey,
        BundesligaPredictionSnapshotHash sourceSnapshotHash,
        BundesligaIdentitySeedReference sourceSeed,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection)
    {
        RouteId = routeId;
        PostingKey = postingKey;
        PostingSnapshotHash = postingSnapshotHash;
        PostingSeed = postingSeed;
        SourceKey = sourceKey;
        SourceSnapshotHash = sourceSnapshotHash;
        SourceSeed = sourceSeed;
        _optionProjection = optionProjection.ToImmutableArray();
    }

    public string RouteId { get; }
    public StableLocalItemKey PostingKey { get; }
    public BundesligaPredictionSnapshotHash PostingSnapshotHash { get; }
    public BundesligaIdentitySeedReference PostingSeed { get; }
    public StableLocalItemKey SourceKey { get; }
    public BundesligaPredictionSnapshotHash SourceSnapshotHash { get; }
    public BundesligaIdentitySeedReference SourceSeed { get; }
    public IReadOnlyList<BundesligaBonusOptionProjection> OptionProjection => _optionProjection;

    public static BundesligaCopyBindingEntry CreateMatch(
        string routeId,
        BundesligaIdentitySeedGeneration postingSeed,
        StableLocalItemKey postingKey,
        BundesligaIdentitySeedGeneration sourceSeed,
        StableLocalItemKey sourceKey,
        BundesligaPredictionRouteCatalog routes)
    {
        var pair = RequirePair(postingSeed, postingKey, sourceSeed, sourceKey);
        if (pair.Posting.MatchSnapshot is null || pair.Source.MatchSnapshot is null)
        {
            throw new InvalidDataException("Match Copy Binding endpoints must both be match snapshots.");
        }

        routes.Require(routeId, BundesligaPredictionItemKind.Match, pair.Posting.Subcompetition);
        if (pair.Posting.Subcompetition != pair.Source.Subcompetition)
        {
            throw new InvalidDataException("Match Copy Binding endpoints have different subcompetitions.");
        }

        return new BundesligaCopyBindingEntry(
            routeId,
            postingKey,
            pair.Posting.SnapshotHash,
            postingSeed.Reference,
            sourceKey,
            pair.Source.SnapshotHash,
            sourceSeed.Reference,
            []);
    }

    public static BundesligaCopyBindingEntry CreateBonus(
        string routeId,
        BundesligaIdentitySeedGeneration postingSeed,
        StableLocalItemKey postingKey,
        BundesligaIdentitySeedGeneration sourceSeed,
        StableLocalItemKey sourceKey,
        IEnumerable<BundesligaBonusOptionProjection> optionProjection,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(optionProjection);
        var pair = RequirePair(postingSeed, postingKey, sourceSeed, sourceKey);
        if (pair.Posting.BonusSnapshot is not { } posting
            || pair.Source.BonusSnapshot is not { } source)
        {
            throw new InvalidDataException("Bonus Copy Binding endpoints must both be bonus snapshots.");
        }

        routes.Require(routeId, BundesligaPredictionItemKind.Bonus, posting.Subcompetition);
        if (posting.Subcompetition != source.Subcompetition)
        {
            throw new InvalidDataException("Bonus Copy Binding endpoints have different subcompetitions.");
        }

        var projection = optionProjection.ToArray();
        if (projection.Any(item => item is null))
        {
            throw new InvalidDataException("Bonus option projection cannot contain null entries.");
        }

        EnsureExactProjection(
            source.Options.Select(option => option.Id),
            posting.Options.Select(option => option.Id),
            projection);
        return new BundesligaCopyBindingEntry(
            routeId,
            postingKey,
            pair.Posting.SnapshotHash,
            postingSeed.Reference,
            sourceKey,
            pair.Source.SnapshotHash,
            sourceSeed.Reference,
            projection.OrderBy(item => item.SourceOptionId, StringComparer.Ordinal));
    }

    internal static void EnsureExactProjection(
        IEnumerable<string> sourceOptionIds,
        IEnumerable<string> postingOptionIds,
        IReadOnlyCollection<BundesligaBonusOptionProjection> projection)
    {
        var source = sourceOptionIds.Order(StringComparer.Ordinal).ToArray();
        var posting = postingOptionIds.Order(StringComparer.Ordinal).ToArray();
        var projectedSource = projection.Select(item => item.SourceOptionId)
            .Order(StringComparer.Ordinal).ToArray();
        var projectedPosting = projection.Select(item => item.PostingOptionId)
            .Order(StringComparer.Ordinal).ToArray();
        if (!source.SequenceEqual(projectedSource, StringComparer.Ordinal)
            || !posting.SequenceEqual(projectedPosting, StringComparer.Ordinal)
            || projectedSource.Distinct(StringComparer.Ordinal).Count() != projection.Count
            || projectedPosting.Distinct(StringComparer.Ordinal).Count() != projection.Count)
        {
            throw new InvalidDataException(
                "Bonus option projection must be complete, one-to-one, and exact for source and posting options.");
        }
    }

    private static (BundesligaIdentitySeedEntry Posting, BundesligaIdentitySeedEntry Source) RequirePair(
        BundesligaIdentitySeedGeneration postingSeed,
        StableLocalItemKey postingKey,
        BundesligaIdentitySeedGeneration sourceSeed,
        StableLocalItemKey sourceKey)
    {
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(postingKey);
        ArgumentNullException.ThrowIfNull(sourceSeed);
        ArgumentNullException.ThrowIfNull(sourceKey);
        var posting = postingSeed.RequireEntry(postingKey);
        var source = sourceSeed.RequireEntry(sourceKey);
        if (postingKey.ItemKind != sourceKey.ItemKind)
        {
            throw new InvalidDataException("Copy Binding endpoints have different item kinds.");
        }

        return (posting, source);
    }
}

public sealed class BundesligaCopyBindingGeneration
{
    public const string SchemaVersionValue = "bundesliga-copy-binding-v1";
    private readonly ImmutableArray<BundesligaCopyBindingEntry> _entries;

    private BundesligaCopyBindingGeneration(
        string postingCommunity,
        string sourceCommunity,
        int generation,
        BundesligaGenerationPredecessor? predecessor,
        string sourceEvidenceIdentity,
        BundesligaIdentitySeedReference postingSeed,
        BundesligaIdentitySeedReference sourceSeed,
        IEnumerable<BundesligaCopyBindingEntry> entries)
    {
        PostingCommunity = postingCommunity;
        SourceCommunity = sourceCommunity;
        Generation = generation;
        Predecessor = predecessor;
        SourceEvidenceIdentity = sourceEvidenceIdentity;
        PostingSeed = postingSeed;
        SourceSeed = sourceSeed;
        _entries = entries.ToImmutableArray();
        CanonicalSha256 = BundesligaPredictionCanonicalJson.Sha256(SerializeCanonical());
    }

    public string SchemaVersion => SchemaVersionValue;
    public string SeasonPartition => BundesligaPredictionAuthority.SeasonPartitionValue;
    public string PostingCommunity { get; }
    public string SourceCommunity { get; }
    public int Generation { get; }
    public BundesligaGenerationPredecessor? Predecessor { get; }
    public string SourceEvidenceIdentity { get; }
    public BundesligaIdentitySeedReference PostingSeed { get; }
    public BundesligaIdentitySeedReference SourceSeed { get; }
    public IReadOnlyList<BundesligaCopyBindingEntry> Entries => _entries;
    public string CanonicalSha256 { get; }
    public BundesligaCopyBindingReference Reference =>
        BundesligaCopyBindingReference.Create(Generation, CanonicalSha256);

    public static BundesligaCopyBindingGeneration Create(
        string postingCommunity,
        string sourceCommunity,
        int generation,
        BundesligaGenerationPredecessor? predecessor,
        string sourceEvidenceIdentity,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        IEnumerable<BundesligaCopyBindingEntry> entries)
    {
        BundesligaPredictionContractValidation.Community(postingCommunity, nameof(postingCommunity));
        BundesligaPredictionContractValidation.Community(sourceCommunity, nameof(sourceCommunity));
        BundesligaPredictionContractValidation.Generation(generation, nameof(generation));
        BundesligaPredictionContractValidation.Identifier(sourceEvidenceIdentity, nameof(sourceEvidenceIdentity));
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(sourceSeed);
        ArgumentNullException.ThrowIfNull(entries);
        if (!string.Equals(postingSeed.PostingCommunity, postingCommunity, StringComparison.Ordinal)
            || !string.Equals(sourceSeed.PostingCommunity, sourceCommunity, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Copy Binding seed communities do not match its community pair.");
        }

        if (generation == 1 && predecessor is not null
            || generation > 1 && (predecessor is null || predecessor.Generation != generation - 1))
        {
            throw new InvalidDataException("Copy Binding predecessor is not the immediately prior generation.");
        }

        var materialized = entries
            .OrderBy(entry => BundesligaPredictionCanonicalJson.ItemKind(entry.PostingKey.ItemKind), StringComparer.Ordinal)
            .ThenBy(entry => entry.PostingKey.KicktippItemId, StringComparer.Ordinal)
            .ToArray();
        if (materialized.Length == 0)
        {
            throw new InvalidDataException("Copy Binding Generation must contain at least one mapping.");
        }

        foreach (var entry in materialized)
        {
            if (entry is null
                || !string.Equals(entry.PostingKey.PostingCommunity, postingCommunity, StringComparison.Ordinal)
                || !string.Equals(entry.SourceKey.PostingCommunity, sourceCommunity, StringComparison.Ordinal)
                || entry.PostingSeed != postingSeed.Reference
                || entry.SourceSeed != sourceSeed.Reference)
            {
                throw new InvalidDataException("Copy Binding entry is outside the pinned community/seed pair.");
            }
        }

        EnsureUnique(materialized.Select(entry => entry.PostingKey), "posting endpoint");
        EnsureUnique(materialized.Select(entry => entry.SourceKey), "source endpoint");
        return new BundesligaCopyBindingGeneration(
            postingCommunity,
            sourceCommunity,
            generation,
            predecessor,
            sourceEvidenceIdentity,
            postingSeed.Reference,
            sourceSeed.Reference,
            materialized);
    }

    public byte[] SerializeCanonical() => BundesligaCopyBindingCanonicalJson.Serialize(this);

    public static BundesligaCopyBindingGeneration DeserializeCanonical(
        ReadOnlySpan<byte> bytes,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaPredictionRouteCatalog routes) =>
        BundesligaCopyBindingCanonicalJson.Deserialize(bytes, postingSeed, sourceSeed, routes);

    public BundesligaCopyBindingEntry RequirePostingItem(StableLocalItemKey key) =>
        _entries.SingleOrDefault(entry => entry.PostingKey == key)
        ?? throw new InvalidDataException($"Posting item '{key.KicktippItemId}' is unbound.");

    private static void EnsureUnique(IEnumerable<StableLocalItemKey> keys, string description)
    {
        var duplicate = keys.GroupBy(key => key).FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Duplicate Copy Binding {description} '{duplicate.Key.KicktippItemId}'.");
        }
    }
}

internal static class BundesligaCopyBindingCanonicalJson
{
    public static byte[] Serialize(BundesligaCopyBindingGeneration binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", binding.SchemaVersion);
            writer.WriteString("seasonPartition", binding.SeasonPartition);
            writer.WriteString("postingCommunity", binding.PostingCommunity);
            writer.WriteString("sourceCommunity", binding.SourceCommunity);
            writer.WriteNumber("generation", binding.Generation);
            writer.WritePropertyName("predecessor");
            WritePredecessor(writer, binding.Predecessor);
            writer.WriteString("sourceEvidenceIdentity", binding.SourceEvidenceIdentity);
            writer.WritePropertyName("postingSeed");
            WriteSeed(writer, binding.PostingSeed);
            writer.WritePropertyName("sourceSeed");
            WriteSeed(writer, binding.SourceSeed);
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (var entry in binding.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("routeId", entry.RouteId);
                writer.WritePropertyName("postingKey");
                BundesligaPredictionSnapshotCanonicalJson.WriteKey(writer, entry.PostingKey);
                writer.WritePropertyName("postingSnapshotHash");
                BundesligaPredictionSnapshotCanonicalJson.WriteHash(writer, entry.PostingSnapshotHash);
                writer.WritePropertyName("sourceKey");
                BundesligaPredictionSnapshotCanonicalJson.WriteKey(writer, entry.SourceKey);
                writer.WritePropertyName("sourceSnapshotHash");
                BundesligaPredictionSnapshotCanonicalJson.WriteHash(writer, entry.SourceSnapshotHash);
                writer.WritePropertyName("optionProjection");
                writer.WriteStartArray();
                foreach (var option in entry.OptionProjection)
                {
                    writer.WriteStartObject();
                    writer.WriteString("sourceOptionId", option.SourceOptionId);
                    writer.WriteString("postingOptionId", option.PostingOptionId);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static BundesligaCopyBindingGeneration Deserialize(
        ReadOnlySpan<byte> bytes,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaPredictionRouteCatalog routes)
    {
        ArgumentNullException.ThrowIfNull(postingSeed);
        ArgumentNullException.ThrowIfNull(sourceSeed);
        ArgumentNullException.ThrowIfNull(routes);
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Copy Binding Generation");
        var root = document.RootElement;
        BundesligaPredictionCanonicalJson.Properties(
            root,
            "schemaVersion",
            "seasonPartition",
            "postingCommunity",
            "sourceCommunity",
            "generation",
            "predecessor",
            "sourceEvidenceIdentity",
            "postingSeed",
            "sourceSeed",
            "entries");
        if (!string.Equals(
                BundesligaPredictionCanonicalJson.String(root, "schemaVersion"),
                BundesligaCopyBindingGeneration.SchemaVersionValue,
                StringComparison.Ordinal)
            || !string.Equals(
                BundesligaPredictionCanonicalJson.String(root, "seasonPartition"),
                BundesligaPredictionAuthority.SeasonPartitionValue,
                StringComparison.Ordinal)
            || ReadSeed(root.GetProperty("postingSeed")) != postingSeed.Reference
            || ReadSeed(root.GetProperty("sourceSeed")) != sourceSeed.Reference)
        {
            throw new InvalidDataException("Copy Binding schema, season, or pinned seed reference is invalid.");
        }

        var entriesElement = root.GetProperty("entries");
        if (entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Copy Binding entries must be an array.");
        }

        var entries = entriesElement.EnumerateArray()
            .Select(entry => ParseEntry(entry, postingSeed, sourceSeed, routes))
            .ToArray();
        var binding = BundesligaCopyBindingGeneration.Create(
            BundesligaPredictionCanonicalJson.String(root, "postingCommunity"),
            BundesligaPredictionCanonicalJson.String(root, "sourceCommunity"),
            BundesligaPredictionCanonicalJson.Int32(root, "generation"),
            ReadPredecessor(root.GetProperty("predecessor")),
            BundesligaPredictionCanonicalJson.String(root, "sourceEvidenceIdentity"),
            postingSeed,
            sourceSeed,
            entries);
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, Serialize(binding), "Copy Binding Generation");
        return binding;
    }

    private static BundesligaCopyBindingEntry ParseEntry(
        JsonElement element,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedGeneration sourceSeed,
        BundesligaPredictionRouteCatalog routes)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "routeId",
            "postingKey",
            "postingSnapshotHash",
            "sourceKey",
            "sourceSnapshotHash",
            "optionProjection");
        var routeId = BundesligaPredictionCanonicalJson.String(element, "routeId");
        var postingKey = BundesligaPredictionSnapshotCanonicalJson.ReadKey(element.GetProperty("postingKey"));
        var sourceKey = BundesligaPredictionSnapshotCanonicalJson.ReadKey(element.GetProperty("sourceKey"));
        var postingEntry = postingSeed.RequireEntry(postingKey);
        var sourceEntry = sourceSeed.RequireEntry(sourceKey);
        if (postingEntry.SnapshotHash
                != BundesligaPredictionSnapshotCanonicalJson.ReadHash(element.GetProperty("postingSnapshotHash"))
            || sourceEntry.SnapshotHash
                != BundesligaPredictionSnapshotCanonicalJson.ReadHash(element.GetProperty("sourceSnapshotHash")))
        {
            throw new InvalidDataException("Copy Binding snapshot hash does not match its pinned seed.");
        }

        var optionsElement = element.GetProperty("optionProjection");
        if (optionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Copy Binding option projection must be an array.");
        }

        var options = optionsElement.EnumerateArray().Select(option =>
        {
            BundesligaPredictionCanonicalJson.Properties(option, "sourceOptionId", "postingOptionId");
            return new BundesligaBonusOptionProjection(
                BundesligaPredictionCanonicalJson.String(option, "sourceOptionId"),
                BundesligaPredictionCanonicalJson.String(option, "postingOptionId"));
        }).ToArray();
        return postingKey.ItemKind switch
        {
            BundesligaPredictionItemKind.Match when options.Length == 0 =>
                BundesligaCopyBindingEntry.CreateMatch(
                    routeId,
                    postingSeed,
                    postingKey,
                    sourceSeed,
                    sourceKey,
                    routes),
            BundesligaPredictionItemKind.Bonus => BundesligaCopyBindingEntry.CreateBonus(
                routeId,
                postingSeed,
                postingKey,
                sourceSeed,
                sourceKey,
                options,
                routes),
            _ => throw new InvalidDataException("Match Copy Binding cannot contain option projection rows.")
        };
    }

    private static void WriteSeed(Utf8JsonWriter writer, BundesligaIdentitySeedReference seed)
    {
        writer.WriteStartObject();
        writer.WriteNumber("generation", seed.Generation);
        writer.WriteString("sha256", seed.Sha256);
        writer.WriteEndObject();
    }

    private static BundesligaIdentitySeedReference ReadSeed(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(element, "generation", "sha256");
        return BundesligaIdentitySeedReference.Create(
            BundesligaPredictionCanonicalJson.Int32(element, "generation"),
            BundesligaPredictionCanonicalJson.String(element, "sha256"));
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
