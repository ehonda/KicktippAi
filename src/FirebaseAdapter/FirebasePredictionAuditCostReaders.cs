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

public interface ILegacyFirebasePredictionAuditCostReader
{
    string AuthorityLabel { get; }
    Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface ITypedFirebasePredictionAuditCostReader
{
    string AuthorityLabel { get; }
    Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Reads only the retained legacy Bundesliga authority.</summary>
public sealed class FirebaseLegacyPredictionAuditCostReader
    : ILegacyFirebasePredictionAuditCostReader
{
    public const string LegacyAuthorityLabel = "legacy:bundesliga-2026-27";
    private readonly FirestoreDb _db;

    public FirebaseLegacyPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public string AuthorityLabel => LegacyAuthorityLabel;

    public async Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matchTask = _db.Collection("match-predictions")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .GetSnapshotAsync(cancellationToken);
        var bonusTask = _db.Collection("bonus-predictions")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .GetSnapshotAsync(cancellationToken);
        await Task.WhenAll(matchTask, bonusTask);
        return matchTask.Result.Documents
            .Select(document => FirebasePredictionAuditCostRow.FromLegacy(
                AuthorityLabel, "match-predictions", document, "match"))
            .Concat(bonusTask.Result.Documents.Select(document =>
                FirebasePredictionAuditCostRow.FromLegacy(
                    AuthorityLabel, "bonus-predictions", document, "bonus")))
            .OrderBy(row => row.PhysicalCollection, StringComparer.Ordinal)
            .ThenBy(row => row.DocumentId, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}

/// <summary>Reads only the exact typed-v1 authority and exposes no current capability.</summary>
public sealed class FirebaseTypedPredictionAuditCostReader
    : ITypedFirebasePredictionAuditCostReader
{
    private readonly FirestoreDb _db;

    public FirebaseTypedPredictionAuditCostReader(FirestoreDb db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public string AuthorityLabel => FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch;

    public async Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matchTask = ReadCollectionAsync(
            FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
            "match",
            cancellationToken);
        var bonusTask = ReadCollectionAsync(
            FirebaseBundesligaTypedPredictionCollections.BonusPredictions,
            "bonus",
            cancellationToken);
        await Task.WhenAll(matchTask, bonusTask);
        return matchTask.Result.Concat(bonusTask.Result)
            .OrderBy(row => row.PhysicalCollection, StringComparer.Ordinal)
            .ThenBy(row => row.DocumentId, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private async Task<IReadOnlyList<FirebasePredictionAuditCostRow>> ReadCollectionAsync(
        string collection,
        string itemKind,
        CancellationToken cancellationToken)
    {
        var snapshot = await _db.Collection(collection).GetSnapshotAsync(cancellationToken);
        var rows = ImmutableArray.CreateBuilder<FirebasePredictionAuditCostRow>();
        foreach (var document in snapshot.Documents)
        {
            var data = document.ToDictionary();
            if (!string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "epoch"),
                    AuthorityLabel,
                    StringComparison.Ordinal)
                || !string.Equals(
                    FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "authorityEpoch"),
                    AuthorityLabel,
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

            PredictionGenerationProvenanceV2 provenance;
            try
            {
                provenance = PredictionGenerationProvenanceV2.DeserializeCanonical(
                    Convert.FromBase64String(
                        FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(
                            data,
                            "provenanceCanonicalBase64")));
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Typed audit row provenance is malformed.", exception);
            }
            ValidateRepeatedTypedIdentity(data, provenance, collection, itemKind);
            rows.Add(FirebasePredictionAuditCostRow.FromTyped(
                AuthorityLabel,
                collection,
                document.Id,
                provenance));
        }
        return rows.ToImmutable();
    }

    private static void ValidateRepeatedTypedIdentity(
        IReadOnlyDictionary<string, object> data,
        PredictionGenerationProvenanceV2 provenance,
        string collection,
        string itemKind)
    {
        var authority = provenance.Authority;
        var expectedKind = provenance.PostingKey.ItemKind == BundesligaPredictionItemKind.Match
            ? "match"
            : "bonus";
        if (!string.Equals(authority.AuthorityEpoch, FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch, StringComparison.Ordinal)
            || !string.Equals(provenance.PhysicalStorageNamespace, collection, StringComparison.Ordinal)
            || !string.Equals(itemKind, expectedKind, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "postingCommunity"), authority.PostingCommunity, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "predictionSourceCommunity"), authority.PredictionSourceCommunity, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "communityContext"), authority.CommunityContext, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "kicktippItemId"), provenance.PostingKey.KicktippItemId, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "snapshotSha256"), provenance.PostingSnapshotHash.Sha256, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "routeId"), provenance.RouteId, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "profileId"), provenance.ProfileId, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "generationInputContractId"), provenance.GenerationInputContract.ContractId, StringComparison.Ordinal)
            || !string.Equals(FirebaseBundesligaTypedPredictionAuthorityRepository.ReadString(data, "generationInputContractSha256"), provenance.GenerationInputContract.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed audit row repeated identity contradicts its canonical provenance.");
        }
    }
}
