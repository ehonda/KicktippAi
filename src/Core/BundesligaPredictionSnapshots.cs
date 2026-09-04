using System.Collections.Immutable;
using System.Text.Json;
using NodaTime;

namespace EHonda.KicktippAi.Core;

public sealed record StableLocalItemKey
{
    private StableLocalItemKey(
        string seasonPartition,
        string postingCommunity,
        BundesligaPredictionItemKind itemKind,
        string kicktippItemId) =>
        (SeasonPartition, PostingCommunity, ItemKind, KicktippItemId) =
        (seasonPartition, postingCommunity, itemKind, kicktippItemId);

    public string SeasonPartition { get; }
    public string PostingCommunity { get; }
    public BundesligaPredictionItemKind ItemKind { get; }
    public string KicktippItemId { get; }

    public static StableLocalItemKey Create(
        string seasonPartition,
        string postingCommunity,
        BundesligaPredictionItemKind itemKind,
        string kicktippItemId)
    {
        if (!string.Equals(
            seasonPartition,
            BundesligaPredictionAuthority.SeasonPartitionValue,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stable Local Item Key has the wrong season partition.");
        }

        BundesligaPredictionContractValidation.Community(postingCommunity, nameof(postingCommunity));
        BundesligaPredictionContractValidation.EnumValue(itemKind, nameof(itemKind));
        BundesligaPredictionContractValidation.Identifier(kicktippItemId, nameof(kicktippItemId));
        return new StableLocalItemKey(seasonPartition, postingCommunity, itemKind, kicktippItemId);
    }
}

public sealed record BundesligaPredictionSnapshotHash
{
    private BundesligaPredictionSnapshotHash(string schemaVersion, string sha256) =>
        (SchemaVersion, Sha256) = (schemaVersion, sha256);

    public string SchemaVersion { get; }
    public string Sha256 { get; }

    public static BundesligaPredictionSnapshotHash Create(string schemaVersion, string sha256)
    {
        BundesligaPredictionContractValidation.Identifier(schemaVersion, nameof(schemaVersion));
        BundesligaPredictionContractValidation.Sha256(sha256, nameof(sha256));
        return new BundesligaPredictionSnapshotHash(schemaVersion, sha256);
    }
}

public sealed record BundesligaFixtureScheduleEvidence(
    string KicktippFixtureId,
    bool IsCancelled,
    string? ScheduledInstant,
    bool IsInheritedFromPriorRow = false);

public sealed record BundesligaFixtureDetailScheduleEvidence(
    string KicktippFixtureId,
    string? Termin);

public sealed record BundesligaResolvedScheduledInstant
{
    internal BundesligaResolvedScheduledInstant(string kicktippFixtureId, Instant value) =>
        (KicktippFixtureId, Value) = (kicktippFixtureId, value);

    public string KicktippFixtureId { get; }
    public Instant Value { get; }
}

public static class BundesligaScheduledInstantResolver
{
    public static BundesligaResolvedScheduledInstant Resolve(
        BundesligaFixtureScheduleEvidence fixture,
        IEnumerable<BundesligaFixtureDetailScheduleEvidence> details)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(details);
        BundesligaPredictionContractValidation.Identifier(
            fixture.KicktippFixtureId,
            nameof(fixture.KicktippFixtureId));

        if (fixture.IsCancelled || fixture.IsInheritedFromPriorRow
            || string.IsNullOrEmpty(fixture.ScheduledInstant))
        {
            throw new InvalidDataException(
                "Cancelled, empty, or inherited fixture schedule evidence is not authoritative.");
        }

        var fixtureInstant = BundesligaPredictionCanonicalJson.ParseInstant(
            fixture.ScheduledInstant,
            "fixture scheduled instant");
        var materialized = details.ToArray();
        if (materialized.Length != 1)
        {
            throw new InvalidDataException(
                "Exactly one structured fixture detail Termin is required.");
        }

        var detail = materialized[0]
            ?? throw new InvalidDataException("Structured fixture detail must not be null.");
        if (!string.Equals(detail.KicktippFixtureId, fixture.KicktippFixtureId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Fixture and detail IDs conflict.");
        }

        if (string.IsNullOrEmpty(detail.Termin))
        {
            throw new InvalidDataException("Structured fixture detail Termin is empty.");
        }

        var detailInstant = BundesligaPredictionCanonicalJson.ParseInstant(detail.Termin, "Termin");
        if (detailInstant != fixtureInstant)
        {
            throw new InvalidDataException("Fixture and detail scheduled instants conflict.");
        }

        return new BundesligaResolvedScheduledInstant(fixture.KicktippFixtureId, fixtureInstant);
    }
}

public sealed class TypedMatchSnapshot : IEquatable<TypedMatchSnapshot>
{
    public const string SchemaVersionValue = "bundesliga-match-snapshot-v1";

    private TypedMatchSnapshot(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition,
        string exactRound,
        ResultBasis resultBasis,
        string homeTeam,
        string awayTeam,
        int matchday,
        Instant scheduledInstant)
    {
        Key = key;
        Subcompetition = subcompetition;
        ExactRound = exactRound;
        ResultBasis = resultBasis;
        HomeTeam = homeTeam;
        AwayTeam = awayTeam;
        Matchday = matchday;
        ScheduledInstant = scheduledInstant;
        SnapshotHash = BundesligaPredictionSnapshotHash.Create(
            SchemaVersionValue,
            BundesligaPredictionCanonicalJson.Sha256(SerializeCanonical()));
    }

    public string SchemaVersion => SchemaVersionValue;
    public StableLocalItemKey Key { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }
    public string ExactRound { get; }
    public ResultBasis ResultBasis { get; }
    public string HomeTeam { get; }
    public string AwayTeam { get; }
    public int Matchday { get; }
    public Instant ScheduledInstant { get; }
    public BundesligaPredictionSnapshotHash SnapshotHash { get; }

    public static TypedMatchSnapshot Create(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition,
        string exactRound,
        ResultBasis resultBasis,
        string homeTeam,
        string awayTeam,
        int matchday,
        BundesligaResolvedScheduledInstant scheduledInstant)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(scheduledInstant);
        BundesligaPredictionContractValidation.EnumValue(subcompetition, nameof(subcompetition));
        BundesligaPredictionContractValidation.EnumValue(resultBasis, nameof(resultBasis));
        if (key.ItemKind != BundesligaPredictionItemKind.Match
            || !string.Equals(key.KicktippItemId, scheduledInstant.KicktippFixtureId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Match snapshot key and scheduled evidence do not identify the same fixture.");
        }

        BundesligaPredictionContractValidation.ExactText(exactRound, nameof(exactRound));
        BundesligaPredictionContractValidation.ExactText(homeTeam, nameof(homeTeam));
        BundesligaPredictionContractValidation.ExactText(awayTeam, nameof(awayTeam));
        if (string.Equals(homeTeam, awayTeam, StringComparison.Ordinal) || matchday < 1)
        {
            throw new InvalidDataException("Match snapshot teams or matchday are invalid.");
        }

        BundesligaPredictionCanonicalJson.FormatInstant(scheduledInstant.Value);
        return new TypedMatchSnapshot(
            key,
            subcompetition,
            exactRound,
            resultBasis,
            homeTeam,
            awayTeam,
            matchday,
            scheduledInstant.Value);
    }

    public byte[] SerializeCanonical() => BundesligaPredictionSnapshotCanonicalJson.Serialize(this);

    public static TypedMatchSnapshot DeserializeCanonical(ReadOnlySpan<byte> bytes) =>
        BundesligaPredictionSnapshotCanonicalJson.DeserializeMatch(bytes);

    public bool Equals(TypedMatchSnapshot? other) => other is not null
        && Key == other.Key
        && Subcompetition == other.Subcompetition
        && string.Equals(ExactRound, other.ExactRound, StringComparison.Ordinal)
        && ResultBasis == other.ResultBasis
        && string.Equals(HomeTeam, other.HomeTeam, StringComparison.Ordinal)
        && string.Equals(AwayTeam, other.AwayTeam, StringComparison.Ordinal)
        && Matchday == other.Matchday
        && ScheduledInstant == other.ScheduledInstant;

    public override bool Equals(object? obj) => Equals(obj as TypedMatchSnapshot);
    public override int GetHashCode() => HashCode.Combine(
        Key,
        Subcompetition,
        ExactRound,
        ResultBasis,
        HomeTeam,
        AwayTeam,
        Matchday,
        ScheduledInstant);
}

public sealed record TypedBonusSnapshotOption(string Id, string Text);

public sealed class TypedBonusSnapshot : IEquatable<TypedBonusSnapshot>
{
    public const string SchemaVersionValue = "bundesliga-bonus-snapshot-v1";
    private readonly ImmutableArray<TypedBonusSnapshotOption> _options;

    private TypedBonusSnapshot(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition,
        string text,
        Instant deadline,
        int maxSelections,
        IEnumerable<TypedBonusSnapshotOption> options)
    {
        Key = key;
        Subcompetition = subcompetition;
        Text = text;
        Deadline = deadline;
        MaxSelections = maxSelections;
        _options = options.ToImmutableArray();
        SnapshotHash = BundesligaPredictionSnapshotHash.Create(
            SchemaVersionValue,
            BundesligaPredictionCanonicalJson.Sha256(SerializeCanonical()));
    }

    public string SchemaVersion => SchemaVersionValue;
    public StableLocalItemKey Key { get; }
    public BundesligaSeasonSubcompetition Subcompetition { get; }
    public string Text { get; }
    public Instant Deadline { get; }
    public int MaxSelections { get; }
    public IReadOnlyList<TypedBonusSnapshotOption> Options => _options;
    public BundesligaPredictionSnapshotHash SnapshotHash { get; }

    public static TypedBonusSnapshot Create(
        StableLocalItemKey key,
        BundesligaSeasonSubcompetition subcompetition,
        string text,
        Instant deadline,
        int maxSelections,
        IEnumerable<TypedBonusSnapshotOption> options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);
        BundesligaPredictionContractValidation.EnumValue(subcompetition, nameof(subcompetition));
        if (key.ItemKind != BundesligaPredictionItemKind.Bonus)
        {
            throw new InvalidDataException("Bonus snapshot requires a bonus Stable Local Item Key.");
        }

        BundesligaPredictionContractValidation.ExactText(text, nameof(text));
        BundesligaPredictionCanonicalJson.FormatInstant(deadline);
        var materialized = options.ToArray();
        if (materialized.Length == 0 || maxSelections < 1 || maxSelections > materialized.Length)
        {
            throw new InvalidDataException("Bonus snapshot option count or maximum selections is invalid.");
        }

        foreach (var option in materialized)
        {
            if (option is null)
            {
                throw new InvalidDataException("Bonus snapshot option must not be null.");
            }

            BundesligaPredictionContractValidation.Identifier(option.Id, nameof(option.Id));
            BundesligaPredictionContractValidation.ExactText(option.Text, nameof(option.Text));
        }

        EnsureUnique(materialized.Select(option => option.Id), "bonus option ID");
        EnsureUnique(materialized.Select(option => option.Text), "bonus option text");
        return new TypedBonusSnapshot(key, subcompetition, text, deadline, maxSelections, materialized);
    }

    public byte[] SerializeCanonical() => BundesligaPredictionSnapshotCanonicalJson.Serialize(this);

    public static TypedBonusSnapshot DeserializeCanonical(ReadOnlySpan<byte> bytes) =>
        BundesligaPredictionSnapshotCanonicalJson.DeserializeBonus(bytes);

    public bool Equals(TypedBonusSnapshot? other) => other is not null
        && Key == other.Key
        && Subcompetition == other.Subcompetition
        && string.Equals(Text, other.Text, StringComparison.Ordinal)
        && Deadline == other.Deadline
        && MaxSelections == other.MaxSelections
        && _options.SequenceEqual(other._options);

    public override bool Equals(object? obj) => Equals(obj as TypedBonusSnapshot);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        hash.Add(Subcompetition);
        hash.Add(Text, StringComparer.Ordinal);
        hash.Add(Deadline);
        hash.Add(MaxSelections);
        foreach (var option in _options)
        {
            hash.Add(option);
        }

        return hash.ToHashCode();
    }

    private static void EnsureUnique(IEnumerable<string> values, string description)
    {
        var duplicate = values.GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate {description} '{duplicate.Key}'.");
        }
    }
}

internal static class BundesligaPredictionSnapshotCanonicalJson
{
    public static byte[] Serialize(TypedMatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", snapshot.SchemaVersion);
            writer.WritePropertyName("key");
            WriteKey(writer, snapshot.Key);
            writer.WriteString("subcompetition", snapshot.Subcompetition.ToSerializedValue());
            writer.WriteString("exactRound", snapshot.ExactRound);
            writer.WriteString("resultBasis", snapshot.ResultBasis.ToSerializedValue());
            writer.WriteString("homeTeam", snapshot.HomeTeam);
            writer.WriteString("awayTeam", snapshot.AwayTeam);
            writer.WriteNumber("matchday", snapshot.Matchday);
            writer.WriteString(
                "scheduledInstant",
                BundesligaPredictionCanonicalJson.FormatInstant(snapshot.ScheduledInstant));
            writer.WriteEndObject();
        });
    }

    public static byte[] Serialize(TypedBonusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return BundesligaPredictionCanonicalJson.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", snapshot.SchemaVersion);
            writer.WritePropertyName("key");
            WriteKey(writer, snapshot.Key);
            writer.WriteString("subcompetition", snapshot.Subcompetition.ToSerializedValue());
            writer.WriteString("text", snapshot.Text);
            writer.WriteString("deadline", BundesligaPredictionCanonicalJson.FormatInstant(snapshot.Deadline));
            writer.WriteNumber("maxSelections", snapshot.MaxSelections);
            writer.WritePropertyName("options");
            writer.WriteStartArray();
            foreach (var option in snapshot.Options)
            {
                writer.WriteStartObject();
                writer.WriteString("id", option.Id);
                writer.WriteString("text", option.Text);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    public static TypedMatchSnapshot DeserializeMatch(ReadOnlySpan<byte> bytes)
    {
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Typed match snapshot");
        var root = document.RootElement;
        BundesligaPredictionCanonicalJson.Properties(
            root,
            "schemaVersion",
            "key",
            "subcompetition",
            "exactRound",
            "resultBasis",
            "homeTeam",
            "awayTeam",
            "matchday",
            "scheduledInstant");
        if (!string.Equals(
            BundesligaPredictionCanonicalJson.String(root, "schemaVersion"),
            TypedMatchSnapshot.SchemaVersionValue,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unknown typed match snapshot schema.");
        }

        var key = ReadKey(root.GetProperty("key"));
        if (!BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(
            BundesligaPredictionCanonicalJson.String(root, "subcompetition"),
            out var subcompetition))
        {
            throw new InvalidDataException("Unknown match subcompetition.");
        }

        if (!BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(
            BundesligaPredictionCanonicalJson.String(root, "resultBasis"),
            out var resultBasis))
        {
            throw new InvalidDataException("Unknown result basis.");
        }

        var scheduled = BundesligaPredictionCanonicalJson.Instant(root, "scheduledInstant");
        var snapshot = TypedMatchSnapshot.Create(
            key,
            subcompetition,
            BundesligaPredictionCanonicalJson.String(root, "exactRound"),
            resultBasis,
            BundesligaPredictionCanonicalJson.String(root, "homeTeam"),
            BundesligaPredictionCanonicalJson.String(root, "awayTeam"),
            BundesligaPredictionCanonicalJson.Int32(root, "matchday"),
            new BundesligaResolvedScheduledInstant(key.KicktippItemId, scheduled));
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, Serialize(snapshot), "Typed match snapshot");
        return snapshot;
    }

    public static TypedBonusSnapshot DeserializeBonus(ReadOnlySpan<byte> bytes)
    {
        using var document = BundesligaPredictionCanonicalJson.Parse(bytes, "Typed bonus snapshot");
        var root = document.RootElement;
        BundesligaPredictionCanonicalJson.Properties(
            root,
            "schemaVersion",
            "key",
            "subcompetition",
            "text",
            "deadline",
            "maxSelections",
            "options");
        if (!string.Equals(
            BundesligaPredictionCanonicalJson.String(root, "schemaVersion"),
            TypedBonusSnapshot.SchemaVersionValue,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unknown typed bonus snapshot schema.");
        }

        var key = ReadKey(root.GetProperty("key"));
        if (!BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(
            BundesligaPredictionCanonicalJson.String(root, "subcompetition"),
            out var subcompetition))
        {
            throw new InvalidDataException("Unknown bonus subcompetition.");
        }

        var optionsElement = root.GetProperty("options");
        if (optionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Bonus options must be an array.");
        }

        var options = optionsElement.EnumerateArray().Select(option =>
        {
            BundesligaPredictionCanonicalJson.Properties(option, "id", "text");
            return new TypedBonusSnapshotOption(
                BundesligaPredictionCanonicalJson.String(option, "id"),
                BundesligaPredictionCanonicalJson.String(option, "text"));
        }).ToArray();
        var snapshot = TypedBonusSnapshot.Create(
            key,
            subcompetition,
            BundesligaPredictionCanonicalJson.String(root, "text"),
            BundesligaPredictionCanonicalJson.Instant(root, "deadline"),
            BundesligaPredictionCanonicalJson.Int32(root, "maxSelections"),
            options);
        BundesligaPredictionCanonicalJson.RequireCanonical(bytes, Serialize(snapshot), "Typed bonus snapshot");
        return snapshot;
    }

    internal static void WriteKey(Utf8JsonWriter writer, StableLocalItemKey key)
    {
        writer.WriteStartObject();
        writer.WriteString("seasonPartition", key.SeasonPartition);
        writer.WriteString("postingCommunity", key.PostingCommunity);
        writer.WriteString("itemKind", BundesligaPredictionCanonicalJson.ItemKind(key.ItemKind));
        writer.WriteString("kicktippItemId", key.KicktippItemId);
        writer.WriteEndObject();
    }

    internal static StableLocalItemKey ReadKey(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(
            element,
            "seasonPartition",
            "postingCommunity",
            "itemKind",
            "kicktippItemId");
        return StableLocalItemKey.Create(
            BundesligaPredictionCanonicalJson.String(element, "seasonPartition"),
            BundesligaPredictionCanonicalJson.String(element, "postingCommunity"),
            BundesligaPredictionCanonicalJson.ParseItemKind(
                BundesligaPredictionCanonicalJson.String(element, "itemKind")),
            BundesligaPredictionCanonicalJson.String(element, "kicktippItemId"));
    }

    internal static void WriteHash(Utf8JsonWriter writer, BundesligaPredictionSnapshotHash hash)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", hash.SchemaVersion);
        writer.WriteString("sha256", hash.Sha256);
        writer.WriteEndObject();
    }

    internal static BundesligaPredictionSnapshotHash ReadHash(JsonElement element)
    {
        BundesligaPredictionCanonicalJson.Properties(element, "schemaVersion", "sha256");
        return BundesligaPredictionSnapshotHash.Create(
            BundesligaPredictionCanonicalJson.String(element, "schemaVersion"),
            BundesligaPredictionCanonicalJson.String(element, "sha256"));
    }
}
