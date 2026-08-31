using System.Collections.Immutable;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using NodaTime;

namespace FirebaseAdapter;

/// <summary>Explicitly labelled evidence that cannot be converted into a current prediction.</summary>
public sealed class FirebasePredictionAuditCostRow
{
    private FirebasePredictionAuditCostRow(
        string authorityLabel,
        string physicalCollection,
        string documentId,
        string itemKind,
        string? predictionIdentity,
        int repredictionIndex,
        Instant createdAt,
        long? inputTokens,
        long? outputTokens,
        decimal costUsd)
    {
        AuthorityLabel = authorityLabel;
        PhysicalCollection = physicalCollection;
        DocumentId = documentId;
        ItemKind = itemKind;
        PredictionIdentity = predictionIdentity;
        RepredictionIndex = repredictionIndex;
        CreatedAt = createdAt;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
    }

    public string AuthorityLabel { get; }
    public string PhysicalCollection { get; }
    public string DocumentId { get; }
    public string ItemKind { get; }
    public string? PredictionIdentity { get; }
    public int RepredictionIndex { get; }
    public Instant CreatedAt { get; }
    public long? InputTokens { get; }
    public long? OutputTokens { get; }
    public decimal CostUsd { get; }
    public bool IsCurrentAuthoritative => false;

    internal static FirebasePredictionAuditCostRow FromTyped(
        string authorityLabel,
        string physicalCollection,
        string documentId,
        PredictionGenerationProvenanceV2 provenance) =>
        new(
            authorityLabel,
            physicalCollection,
            documentId,
            provenance.PostingKey.ItemKind == BundesligaPredictionItemKind.Match ? "match" : "bonus",
            provenance.PredictionIdentity,
            provenance.RepredictionIndex,
            provenance.GenerationTime,
            provenance.TargetGenerationUsage.InputTokens,
            provenance.TargetGenerationUsage.OutputTokens,
            provenance.TargetGenerationUsage.CostUsd);

    internal static FirebasePredictionAuditCostRow FromLegacy(
        string authorityLabel,
        string physicalCollection,
        DocumentSnapshot snapshot,
        string itemKind)
    {
        var data = snapshot.ToDictionary();
        var createdAt = data.TryGetValue("createdAt", out var rawCreated) && rawCreated is Timestamp timestamp
            ? Instant.FromDateTimeOffset(timestamp.ToDateTimeOffset())
            : throw new InvalidDataException("Legacy audit row has no valid createdAt timestamp.");
        var index = data.TryGetValue("repredictionIndex", out var rawIndex) && rawIndex is long storedIndex
            && storedIndex >= 0 && storedIndex < int.MaxValue
                ? checked((int)storedIndex)
                : throw new InvalidDataException("Legacy audit row has no valid reprediction index.");
        var cost = data.TryGetValue("cost", out var rawCost) ? rawCost switch
        {
            double value when value >= 0 => checked((decimal)value),
            long value when value >= 0 => value,
            _ => throw new InvalidDataException("Legacy audit row has an invalid cost.")
        } : throw new InvalidDataException("Legacy audit row has no cost.");
        return new FirebasePredictionAuditCostRow(
            authorityLabel,
            physicalCollection,
            snapshot.Id,
            itemKind,
            null,
            index,
            createdAt,
            null,
            null,
            cost);
    }
}

public interface IFirebasePredictionAuditCostReader
{
    string AuthorityLabel { get; }
    string PhysicalCollection { get; }
    string ItemKind { get; }
    Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ILegacyFirebaseMatchPredictionAuditCostReader : IFirebasePredictionAuditCostReader
{
}

public interface ILegacyFirebaseBonusPredictionAuditCostReader : IFirebasePredictionAuditCostReader
{
}

public interface ITypedFirebaseMatchPredictionAuditCostReader : IFirebasePredictionAuditCostReader
{
}

public interface ITypedFirebaseBonusPredictionAuditCostReader : IFirebasePredictionAuditCostReader
{
}

public sealed class FirebaseLegacyMatchPredictionAuditCostReader
    : ILegacyFirebaseMatchPredictionAuditCostReader
{
    private readonly FirestoreDb _db;
    public FirebaseLegacyMatchPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));
    public string AuthorityLabel => FirebasePredictionAuditCostReaderSupport.LegacyAuthorityLabel;
    public string PhysicalCollection => "match-predictions";
    public string ItemKind => "match";
    public Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default) =>
        FirebasePredictionAuditCostReaderSupport.ReadLegacyAsync(
            _db, AuthorityLabel, PhysicalCollection, ItemKind, cancellationToken);
}

public sealed class FirebaseLegacyBonusPredictionAuditCostReader
    : ILegacyFirebaseBonusPredictionAuditCostReader
{
    private readonly FirestoreDb _db;
    public FirebaseLegacyBonusPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));
    public string AuthorityLabel => FirebasePredictionAuditCostReaderSupport.LegacyAuthorityLabel;
    public string PhysicalCollection => "bonus-predictions";
    public string ItemKind => "bonus";
    public Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default) =>
        FirebasePredictionAuditCostReaderSupport.ReadLegacyAsync(
            _db, AuthorityLabel, PhysicalCollection, ItemKind, cancellationToken);
}

public sealed class FirebaseTypedMatchPredictionAuditCostReader
    : ITypedFirebaseMatchPredictionAuditCostReader
{
    private readonly FirestoreDb _db;
    public FirebaseTypedMatchPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));
    public string AuthorityLabel => FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch;
    public string PhysicalCollection => FirebaseBundesligaTypedPredictionCollections.MatchPredictions;
    public string ItemKind => "match";
    public Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default) =>
        FirebasePredictionAuditCostReaderSupport.ReadTypedAsync(
            _db, AuthorityLabel, PhysicalCollection, ItemKind, cancellationToken);
}

public sealed class FirebaseTypedBonusPredictionAuditCostReader
    : ITypedFirebaseBonusPredictionAuditCostReader
{
    private readonly FirestoreDb _db;
    public FirebaseTypedBonusPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));
    public string AuthorityLabel => FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch;
    public string PhysicalCollection => FirebaseBundesligaTypedPredictionCollections.BonusPredictions;
    public string ItemKind => "bonus";
    public Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default) =>
        FirebasePredictionAuditCostReaderSupport.ReadTypedAsync(
            _db, AuthorityLabel, PhysicalCollection, ItemKind, cancellationToken);
}

internal static class FirebasePredictionAuditCostReaderSupport
{
    internal const string LegacyAuthorityLabel = "legacy:bundesliga-2026-27";

    internal static async Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadLegacyAsync(
        FirestoreDb db,
        string authorityLabel,
        string collection,
        string itemKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await db.Collection(collection)
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .GetSnapshotAsync(cancellationToken);
        return snapshot.Documents
            .Select(document => FirebasePredictionAuditCostRow.FromLegacy(
                authorityLabel, collection, document, itemKind))
            .OrderBy(row => row.DocumentId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    internal static async Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadTypedAsync(
        FirestoreDb db,
        string authorityLabel,
        string collection,
        string itemKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await db.Collection(collection).GetSnapshotAsync(cancellationToken);
        var rows = ImmutableArray.CreateBuilder<FirebasePredictionAuditCostRow>();
        foreach (var document in snapshot.Documents)
        {
            var data = document.ToDictionary();
            if (!string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "epoch"),
                    authorityLabel,
                    StringComparison.Ordinal)
                || !string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "authorityEpoch"),
                    authorityLabel,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Typed audit row is outside its configured authority epoch.");
            }

            if (!string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "documentKind"),
                    "prediction",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "itemKind"),
                    itemKind,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Typed audit row item kind does not match its physical collection.");
            }

            byte[] provenanceBytes;
            PredictionGenerationProvenanceV2 provenance;
            try
            {
                provenanceBytes = Convert.FromBase64String(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(
                        data,
                        "provenanceCanonicalBase64"));
                provenance = PredictionGenerationProvenanceV2.DeserializeCanonical(provenanceBytes);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Typed audit row provenance is malformed.", exception);
            }
            if (!provenanceBytes.SequenceEqual(provenance.SerializeCanonical()))
            {
                throw new InvalidDataException("Typed audit row provenance is not canonical.");
            }
            ValidateRepeatedTypedIdentity(document, data, provenance, collection, itemKind);
            rows.Add(FirebasePredictionAuditCostRow.FromTyped(
                authorityLabel,
                collection,
                document.Id,
                provenance));
        }
        return rows.ToImmutable();
    }

    private static void ValidateRepeatedTypedIdentity(
        DocumentSnapshot document,
        IReadOnlyDictionary<string, object> data,
        PredictionGenerationProvenanceV2 provenance,
        string collection,
        string itemKind)
    {
        var expectedKind = provenance.PostingKey.ItemKind switch
        {
            BundesligaPredictionItemKind.Match => "match",
            BundesligaPredictionItemKind.Bonus => "bonus",
            _ => throw new InvalidDataException("Typed audit row provenance has an unknown item kind.")
        };
        if (!string.Equals(provenance.Authority.AuthorityEpoch, FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(provenance.PhysicalStorageNamespace, collection, StringComparison.Ordinal)
            || !string.Equals(itemKind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed audit row repeated identity contradicts its canonical provenance.");
        }

        byte[] snapshotBytes;
        try
        {
            snapshotBytes = Convert.FromBase64String(
                FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(
                    data, "snapshotCanonicalBase64"));
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Typed audit row snapshot is malformed.", exception);
        }

        var (snapshotKey, snapshotHash) = itemKind switch
        {
            "match" => ReadMatchSnapshot(snapshotBytes),
            "bonus" => ReadBonusSnapshot(snapshotBytes),
            _ => throw new InvalidDataException("Typed audit reader has an unknown item kind.")
        };
        if (snapshotKey != provenance.PostingKey || snapshotHash != provenance.PostingSnapshotHash)
        {
            throw new InvalidDataException("Typed audit row snapshot contradicts its canonical provenance.");
        }

        var identity = BundesligaTypedCurrentIdentity.Create(
            provenance.RouteId,
            provenance.ProfileId,
            provenance.GenerationInputContract);
        var expectedFields = FirebaseBundesligaTypedPredictionAuthorityRepository.BuildCanonicalIdentityFields(
            provenance.Authority,
            snapshotKey,
            snapshotHash,
            snapshotBytes,
            provenance.PostingKey.ItemKind,
            identity,
            provenance.ModelConfig);
        foreach (var field in expectedFields)
        {
            if (!string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, field.Key),
                    field.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Typed audit row repeated identity field '{field.Key}' contradicts its canonical provenance.");
            }
        }

        var index = ReadIndex(data, "repredictionIndex");
        var expectedAddress = $"{expectedFields["currentFingerprint"]}-r{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (index != provenance.RepredictionIndex
            || !string.Equals(document.Id, expectedAddress, StringComparison.Ordinal)
            || !string.Equals(
                FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "predictionIdentity"),
                provenance.PredictionIdentity,
                StringComparison.Ordinal)
            || !string.Equals(
                FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "predictionPayloadKind"),
                itemKind,
                StringComparison.Ordinal)
            || !data.TryGetValue("createdAt", out var rawCreatedAt)
            || rawCreatedAt is not Timestamp createdAt
            || Instant.FromDateTimeOffset(createdAt.ToDateTimeOffset()) != provenance.GenerationTime)
        {
            throw new InvalidDataException(
                "Typed audit row address, index, prediction identity, payload kind, or creation time contradicts its canonical provenance.");
        }
    }

    private static (StableLocalItemKey Key, BundesligaPredictionSnapshotHash Hash) ReadMatchSnapshot(
        byte[] bytes)
    {
        var snapshot = TypedMatchSnapshot.DeserializeCanonical(bytes);
        if (!bytes.SequenceEqual(snapshot.SerializeCanonical()))
        {
            throw new InvalidDataException("Typed audit match snapshot is not canonical.");
        }
        return (snapshot.Key, snapshot.SnapshotHash);
    }

    private static (StableLocalItemKey Key, BundesligaPredictionSnapshotHash Hash) ReadBonusSnapshot(
        byte[] bytes)
    {
        var snapshot = TypedBonusSnapshot.DeserializeCanonical(bytes);
        if (!bytes.SequenceEqual(snapshot.SerializeCanonical()))
        {
            throw new InvalidDataException("Typed audit bonus snapshot is not canonical.");
        }
        return (snapshot.Key, snapshot.SnapshotHash);
    }

    private static int ReadIndex(IReadOnlyDictionary<string, object> data, string name)
    {
        if (!data.TryGetValue(name, out var value)
            || value is not long raw
            || raw < 0
            || raw >= int.MaxValue)
        {
            throw new InvalidDataException($"Typed audit row index '{name}' is missing or invalid.");
        }
        return checked((int)raw);
    }
}
