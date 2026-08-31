using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using NodaTime;

namespace FirebaseAdapter;

public static class FirebaseBundesligaTypedPredictionCollections
{
    public const string AuthorityEpoch = BundesligaPredictionAuthority.AuthorityEpochValue;
    public const string MatchPredictions = "match-predictions-bundesliga-2026-27-typed-v1";
    public const string BonusPredictions = "bonus-predictions-bundesliga-2026-27-typed-v1";
    public const string ItemSnapshots = "matches-bundesliga-2026-27-typed-v1";
}

/// <summary>
/// Firestore authority for exactly one immutable Bundesliga 2026/27 typed epoch.
/// It has no legacy repository capability and never queries a legacy collection.
/// </summary>
public sealed class FirebaseBundesligaTypedPredictionAuthorityRepository
    : IBundesligaTypedPredictionAuthorityRepository
{
    private const string HeadKind = "current-head";
    private const string PredictionKind = "prediction";
    private const string SnapshotKind = "item-snapshot";
    private static readonly JsonSerializerOptions PredictionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FirestoreDb _db;
    private readonly string _authorityEpoch;

    public FirebaseBundesligaTypedPredictionAuthorityRepository(
        FirestoreDb db,
        string authorityEpoch)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        if (!string.Equals(
                authorityEpoch,
                FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Typed Firebase authority epoch must be exactly '{FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch}'.");
        }

        _authorityEpoch = authorityEpoch;
    }

    public async Task<TypedMatchPredictionRecord?> GetCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        CancellationToken cancellationToken = default) =>
        (await ReadCurrentAsync(request, cancellationToken))?.MatchPrediction;

    public async Task<TypedPredictionMetadataV2?> GetCurrentTypedMatchPredictionMetadataAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        CancellationToken cancellationToken = default)
    {
        var row = await ReadCurrentAsync(request, cancellationToken);
        return row is null ? null : CreateMetadata(request, row);
    }

    public async Task<bool> HasCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        CancellationToken cancellationToken = default) =>
        await ReadCurrentAsync(request, cancellationToken) is not null;

    public async Task<int> GetCurrentTypedMatchRepredictionIndexAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        CancellationToken cancellationToken = default) =>
        (await ReadCurrentAsync(request, cancellationToken))?.RepredictionIndex ?? -1;

    public Task SaveCurrentTypedMatchPredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        CancellationToken cancellationToken = default)
    {
        _ = TypedMatchPredictionRecord.Create(request, prediction, provenance);
        return SaveInitialAsync(request, prediction, provenance, cancellationToken);
    }

    public Task SaveCurrentTypedMatchRepredictionAsync(
        BundesligaTypedCurrentRequest<TypedMatchSnapshot> request,
        Prediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex,
        int maximumRepredictions,
        CancellationToken cancellationToken = default)
    {
        _ = TypedMatchPredictionRecord.Create(request, prediction, provenance);
        return SaveRepredictionAsync(
            request,
            prediction,
            provenance,
            expectedCurrentRepredictionIndex,
            maximumRepredictions,
            cancellationToken);
    }

    public async Task<TypedMatchCopyCandidate?> GetTypedMatchCopyCandidateAsync(
        BundesligaTypedCopyRequest<TypedMatchSnapshot> request,
        CancellationToken cancellationToken = default)
    {
        ValidateCopyRequest(request);
        var source = await ReadCurrentAsync(request.SourceCurrent, cancellationToken);
        return source is null
            ? null
            : TypedMatchCopyCandidate.Create(request, source.MatchPrediction!);
    }

    public Task SaveCurrentTypedMatchCopyAsync(
        TypedMatchCopySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCopyRequest(request.CopyRequest);
        if (request.TargetProvenance.RepredictionIndex != 0)
        {
            throw new InvalidDataException("Initial typed copy provenance must use reprediction index zero.");
        }
        var target = CreateEnvelope(request.CopyRequest.TargetCurrent);
        ValidateProvenance(
            request.CopyRequest.TargetCurrent,
            request.TargetProvenance,
            expectedIndex: 0,
            target.PredictionCollection);

        cancellationToken.ThrowIfCancellationRequested();
        return _db.RunTransactionAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storedSource = await ReadCurrentAsync(transaction, request.CopyRequest.SourceCurrent);
            if (storedSource is null
                || !PredictionContentEquality.Equals(
                    storedSource.MatchPrediction!.Prediction,
                    request.SourceCandidate.SourcePrediction.Prediction)
                || storedSource.Provenance.CanonicalSha256
                    != request.SourceCandidate.SourcePrediction.Provenance.CanonicalSha256)
            {
                throw new InvalidDataException("Typed match copy source is missing or no longer equals the bound source row.");
            }

            var reads = await ReadInitialDocumentsAsync(transaction, target);
            RequireInitialTargetAvailable(reads);
            WriteInitialDocuments(
                transaction,
                target,
                reads,
                request.Prediction,
                request.TargetProvenance);
            return true;
        });
    }

    public async Task<TypedBonusPredictionRecord?> GetCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        CancellationToken cancellationToken = default) =>
        (await ReadCurrentAsync(request, cancellationToken))?.BonusPrediction;

    public async Task<TypedPredictionMetadataV2?> GetCurrentTypedBonusPredictionMetadataAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        CancellationToken cancellationToken = default)
    {
        var row = await ReadCurrentAsync(request, cancellationToken);
        return row is null ? null : CreateMetadata(request, row);
    }

    public async Task<bool> HasCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        CancellationToken cancellationToken = default) =>
        await ReadCurrentAsync(request, cancellationToken) is not null;

    public async Task<int> GetCurrentTypedBonusRepredictionIndexAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        CancellationToken cancellationToken = default) =>
        (await ReadCurrentAsync(request, cancellationToken))?.RepredictionIndex ?? -1;

    public Task SaveCurrentTypedBonusPredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        CancellationToken cancellationToken = default)
    {
        _ = TypedBonusPredictionRecord.Create(request, prediction, provenance);
        return SaveInitialAsync(request, prediction, provenance, cancellationToken);
    }

    public Task SaveCurrentTypedBonusRepredictionAsync(
        BundesligaTypedCurrentRequest<TypedBonusSnapshot> request,
        BonusPrediction prediction,
        PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex,
        int maximumRepredictions,
        CancellationToken cancellationToken = default)
    {
        _ = TypedBonusPredictionRecord.Create(request, prediction, provenance);
        return SaveRepredictionAsync(
            request,
            prediction,
            provenance,
            expectedCurrentRepredictionIndex,
            maximumRepredictions,
            cancellationToken);
    }

    public async Task<TypedBonusCopyCandidate?> GetTypedBonusCopyCandidateAsync(
        BundesligaTypedCopyRequest<TypedBonusSnapshot> request,
        CancellationToken cancellationToken = default)
    {
        ValidateCopyRequest(request);
        var source = await ReadCurrentAsync(request.SourceCurrent, cancellationToken);
        return source is null
            ? null
            : TypedBonusCopyCandidate.Create(request, source.BonusPrediction!);
    }

    public Task SaveCurrentTypedBonusCopyAsync(
        TypedBonusCopySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCopyRequest(request.CopyRequest);
        if (request.TargetProvenance.RepredictionIndex != 0)
        {
            throw new InvalidDataException("Initial typed copy provenance must use reprediction index zero.");
        }
        var target = CreateEnvelope(request.CopyRequest.TargetCurrent);
        ValidateProvenance(
            request.CopyRequest.TargetCurrent,
            request.TargetProvenance,
            expectedIndex: 0,
            target.PredictionCollection);

        cancellationToken.ThrowIfCancellationRequested();
        return _db.RunTransactionAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storedSource = await ReadCurrentAsync(transaction, request.CopyRequest.SourceCurrent);
            if (storedSource is null
                || !storedSource.BonusPrediction!.SelectedOptionIds.SequenceEqual(
                    request.SourceCandidate.SourcePrediction.SelectedOptionIds,
                    StringComparer.Ordinal)
                || storedSource.Provenance.CanonicalSha256
                    != request.SourceCandidate.SourcePrediction.Provenance.CanonicalSha256)
            {
                throw new InvalidDataException("Typed bonus copy source is missing or no longer equals the bound source row.");
            }

            var reads = await ReadInitialDocumentsAsync(transaction, target);
            RequireInitialTargetAvailable(reads);
            WriteInitialDocuments(
                transaction,
                target,
                reads,
                request.SelectedOptionIds,
                request.TargetProvenance);
            return true;
        });
    }

    internal string CurrentFingerprint<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request) where TSnapshot : class =>
        CreateEnvelope(request).Fingerprint;

    private async Task SaveInitialAsync<TSnapshot, TPayload>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        TPayload payload,
        PredictionGenerationProvenanceV2 provenance,
        CancellationToken cancellationToken) where TSnapshot : class
    {
        var envelope = CreateEnvelope(request);
        ValidateProvenance(request, provenance, expectedIndex: 0, envelope.PredictionCollection);
        cancellationToken.ThrowIfCancellationRequested();
        await _db.RunTransactionAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reads = await ReadInitialDocumentsAsync(transaction, envelope);
            RequireInitialTargetAvailable(reads);
            WriteInitialDocuments(transaction, envelope, reads, payload, provenance);
            return true;
        });
    }

    private async Task SaveRepredictionAsync<TSnapshot, TPayload>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        TPayload payload,
        PredictionGenerationProvenanceV2 provenance,
        int expectedCurrentRepredictionIndex,
        int maximumRepredictions,
        CancellationToken cancellationToken) where TSnapshot : class
    {
        if (expectedCurrentRepredictionIndex < 0
            || maximumRepredictions < 0
            || expectedCurrentRepredictionIndex >= maximumRepredictions
            || expectedCurrentRepredictionIndex == int.MaxValue)
        {
            throw new InvalidDataException("Typed reprediction bounds are invalid or exhausted.");
        }

        var nextIndex = checked(expectedCurrentRepredictionIndex + 1);
        var envelope = CreateEnvelope(request);
        ValidateProvenance(request, provenance, nextIndex, envelope.PredictionCollection);
        cancellationToken.ThrowIfCancellationRequested();
        await _db.RunTransactionAsync(async transaction =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headReference = HeadReference(envelope);
            var rowReference = PredictionReference(envelope, nextIndex);
            var head = await transaction.GetSnapshotAsync(headReference);
            var snapshot = envelope.HasSeparateItemSnapshot
                ? await transaction.GetSnapshotAsync(SnapshotReference(envelope))
                : null;
            var row = await transaction.GetSnapshotAsync(rowReference);

            if (!head.Exists)
            {
                throw new InvalidOperationException("Typed reprediction requires an existing current head.");
            }
            ValidateStoredEnvelope(head, envelope, HeadKind);
            if (snapshot is not null)
            {
                ValidateStoredEnvelope(snapshot, envelope, SnapshotKind);
            }
            if (ReadIndex(head, "currentRepredictionIndex") != expectedCurrentRepredictionIndex)
            {
                throw new InvalidOperationException("Typed reprediction lost its expected-current concurrency gate.");
            }
            if (row.Exists)
            {
                throw new InvalidOperationException("Typed reprediction target index already exists.");
            }

            transaction.Create(rowReference, CreatePredictionDocument(envelope, payload, provenance));
            transaction.Set(headReference, CreateHeadDocument(envelope, provenance));
            return true;
        });
    }

    private async Task<StoredCurrent?> ReadCurrentAsync<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        CancellationToken cancellationToken) where TSnapshot : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var envelope = CreateEnvelope(request);
        var head = await HeadReference(envelope).GetSnapshotAsync(cancellationToken);
        if (!head.Exists)
        {
            return null;
        }
        ValidateStoredEnvelope(head, envelope, HeadKind);
        var currentIndex = ReadIndex(head, "currentRepredictionIndex");
        var row = await PredictionReference(envelope, currentIndex).GetSnapshotAsync(cancellationToken);
        var snapshot = envelope.HasSeparateItemSnapshot
            ? await SnapshotReference(envelope).GetSnapshotAsync(cancellationToken)
            : null;
        return MaterializeCurrent(request, envelope, head, row, snapshot);
    }

    private async Task<StoredCurrent?> ReadCurrentAsync<TSnapshot>(
        Transaction transaction,
        BundesligaTypedCurrentRequest<TSnapshot> request) where TSnapshot : class
    {
        var envelope = CreateEnvelope(request);
        var head = await transaction.GetSnapshotAsync(HeadReference(envelope));
        if (!head.Exists)
        {
            return null;
        }
        ValidateStoredEnvelope(head, envelope, HeadKind);
        var currentIndex = ReadIndex(head, "currentRepredictionIndex");
        var row = await transaction.GetSnapshotAsync(PredictionReference(envelope, currentIndex));
        var snapshot = envelope.HasSeparateItemSnapshot
            ? await transaction.GetSnapshotAsync(SnapshotReference(envelope))
            : null;
        return MaterializeCurrent(request, envelope, head, row, snapshot);
    }

    private StoredCurrent MaterializeCurrent<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        CurrentEnvelope envelope,
        DocumentSnapshot head,
        DocumentSnapshot row,
        DocumentSnapshot? snapshot) where TSnapshot : class
    {
        ValidateStoredEnvelope(row, envelope, PredictionKind);
        if (envelope.HasSeparateItemSnapshot)
        {
            ValidateStoredEnvelope(
                snapshot ?? throw new InvalidDataException("Typed match item snapshot document is missing."),
                envelope,
                SnapshotKind);
        }
        var index = ReadIndex(head, "currentRepredictionIndex");
        if (ReadIndex(row, "repredictionIndex") != index)
        {
            throw new InvalidDataException("Typed prediction row index does not match its exact current head.");
        }

        var data = row.ToDictionary();
        var provenance = DeserializeProvenance(data);
        ValidateProvenance(request, provenance, index, envelope.PredictionCollection);
        if (!string.Equals(
                ReadString(head.ToDictionary(), "currentPredictionIdentity"),
                provenance.PredictionIdentity,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed prediction head references a different prediction identity.");
        }

        return request.Snapshot switch
        {
            TypedMatchSnapshot => new StoredCurrent(
                TypedMatchPredictionRecord.Create(
                    (BundesligaTypedCurrentRequest<TypedMatchSnapshot>)(object)request,
                    DeserializeMatchPrediction(data),
                    provenance),
                null,
                provenance,
                index),
            TypedBonusSnapshot => new StoredCurrent(
                null,
                TypedBonusPredictionRecord.Create(
                    (BundesligaTypedCurrentRequest<TypedBonusSnapshot>)(object)request,
                    DeserializeBonusPrediction(data),
                    provenance),
                provenance,
                index),
            _ => throw new InvalidDataException("Unsupported typed current snapshot.")
        };
    }

    private static TypedPredictionMetadataV2 CreateMetadata<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        StoredCurrent row) where TSnapshot : class =>
        TypedPredictionMetadataV2.Create(
            request,
            row.Provenance.PredictionIdentity,
            row.RepredictionIndex,
            row.Provenance.GenerationTime,
            SystemClock.Instance.GetCurrentInstant(),
            row.Provenance);

    private static void ValidateProvenance<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request,
        PredictionGenerationProvenanceV2 provenance,
        int expectedIndex,
        string expectedPhysicalNamespace) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provenance);
        request.RequireMatchingProvenance(provenance);
        if (provenance.RepredictionIndex != expectedIndex
            || !string.Equals(
                provenance.PhysicalStorageNamespace,
                expectedPhysicalNamespace,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Typed provenance reprediction index or physical namespace does not match the addressed row.");
        }
    }

    private void ValidateCopyRequest<TSnapshot>(
        BundesligaTypedCopyRequest<TSnapshot> request) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAuthority(request.TargetCurrent.Authority);
        ValidateAuthority(request.SourceCurrent.Authority);
    }

    private void ValidateAuthority(BundesligaPredictionAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        if (!string.Equals(authority.AuthorityEpoch, _authorityEpoch, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed request authority is outside the configured Firebase epoch.");
        }
    }

    private CurrentEnvelope CreateEnvelope<TSnapshot>(
        BundesligaTypedCurrentRequest<TSnapshot> request) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAuthority(request.Authority);
        var (key, snapshotHash, itemKind, canonicalSnapshot, collection, hasSeparateItemSnapshot) = request.Snapshot switch
        {
            TypedMatchSnapshot match => (
                match.Key,
                match.SnapshotHash,
                BundesligaPredictionItemKind.Match,
                match.SerializeCanonical(),
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
                true),
            TypedBonusSnapshot bonus => (
                bonus.Key,
                bonus.SnapshotHash,
                BundesligaPredictionItemKind.Bonus,
                bonus.SerializeCanonical(),
                FirebaseBundesligaTypedPredictionCollections.BonusPredictions,
                false),
            _ => throw new InvalidDataException("Only typed match and bonus snapshots can address Firebase typed authority.")
        };

        var authority = request.Authority;
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["authorityEpoch"] = authority.AuthorityEpoch,
            ["authorityMode"] = authority.Mode == BundesligaPredictionAuthorityMode.Direct ? "direct" : "copy",
            ["seasonPartition"] = authority.SeasonPartition,
            ["postingCommunity"] = authority.PostingCommunity,
            ["predictionSourceCommunity"] = authority.PredictionSourceCommunity,
            ["communityContext"] = authority.CommunityContext,
            ["postingSeedGeneration"] = authority.PostingSeed.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["postingSeedSha256"] = authority.PostingSeed.Sha256,
            ["sourceSeedGeneration"] = authority.SourceSeed.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sourceSeedSha256"] = authority.SourceSeed.Sha256,
            ["copyBindingGeneration"] = authority.CopyBinding?.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["copyBindingSha256"] = authority.CopyBinding?.Sha256 ?? string.Empty,
            ["itemKind"] = itemKind == BundesligaPredictionItemKind.Match ? "match" : "bonus",
            ["keySeasonPartition"] = key.SeasonPartition,
            ["keyPostingCommunity"] = key.PostingCommunity,
            ["keyItemKind"] = itemKind == BundesligaPredictionItemKind.Match ? "match" : "bonus",
            ["kicktippItemId"] = key.KicktippItemId,
            ["snapshotSchemaVersion"] = snapshotHash.SchemaVersion,
            ["snapshotSha256"] = snapshotHash.Sha256,
            ["snapshotCanonicalBase64"] = Convert.ToBase64String(canonicalSnapshot),
            ["routeId"] = request.Identity.RouteId,
            ["profileId"] = request.Identity.ProfileId,
            ["generationInputContractId"] = request.Identity.GenerationInputContract.ContractId,
            ["generationInputContractSha256"] = request.Identity.GenerationInputContract.Sha256,
            ["model"] = request.ModelConfig.Model,
            ["reasoningEffort"] = request.ModelConfig.ReasoningEffort!,
            ["maxOutputTokenCount"] = request.ModelConfig.MaxOutputTokenCount!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["promptName"] = request.ModelConfig.PromptName!,
            ["promptVersion"] = request.ModelConfig.PromptVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var canonicalIdentity = JsonSerializer.SerializeToUtf8Bytes(fields);
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(canonicalIdentity));
        fields["currentFingerprint"] = fingerprint;
        return new CurrentEnvelope(
            collection,
            fingerprint,
            fields.ToImmutableSortedDictionary(StringComparer.Ordinal),
            hasSeparateItemSnapshot);
    }

    private async Task<InitialReads> ReadInitialDocumentsAsync(
        Transaction transaction,
        CurrentEnvelope envelope)
    {
        var head = await transaction.GetSnapshotAsync(HeadReference(envelope));
        var snapshot = envelope.HasSeparateItemSnapshot
            ? await transaction.GetSnapshotAsync(SnapshotReference(envelope))
            : null;
        var row = await transaction.GetSnapshotAsync(PredictionReference(envelope, 0));
        return new InitialReads(head, snapshot, row);
    }

    private static void RequireInitialTargetAvailable(InitialReads reads)
    {
        if (reads.Head.Exists || reads.Row.Exists)
        {
            throw new InvalidOperationException("Typed initial prediction already exists for the exact current identity.");
        }
    }

    private void WriteInitialDocuments<TPayload>(
        Transaction transaction,
        CurrentEnvelope envelope,
        InitialReads reads,
        TPayload payload,
        PredictionGenerationProvenanceV2 provenance)
    {
        if (reads.Snapshot is { Exists: true })
        {
            ValidateStoredEnvelope(reads.Snapshot, envelope, SnapshotKind);
        }
        else if (envelope.HasSeparateItemSnapshot)
        {
            transaction.Create(SnapshotReference(envelope), CreateSnapshotDocument(envelope));
        }
        transaction.Create(
            PredictionReference(envelope, 0),
            CreatePredictionDocument(envelope, payload, provenance));
        transaction.Create(HeadReference(envelope), CreateHeadDocument(envelope, provenance));
    }

    private Dictionary<string, object> CreateSnapshotDocument(CurrentEnvelope envelope) =>
        CreateDocument(envelope, SnapshotKind);

    private Dictionary<string, object> CreateHeadDocument(
        CurrentEnvelope envelope,
        PredictionGenerationProvenanceV2 provenance)
    {
        var document = CreateDocument(envelope, HeadKind);
        document["currentRepredictionIndex"] = provenance.RepredictionIndex;
        document["currentPredictionIdentity"] = provenance.PredictionIdentity;
        return document;
    }

    private Dictionary<string, object> CreatePredictionDocument<TPayload>(
        CurrentEnvelope envelope,
        TPayload payload,
        PredictionGenerationProvenanceV2 provenance)
    {
        var document = CreateDocument(envelope, PredictionKind);
        document["repredictionIndex"] = provenance.RepredictionIndex;
        document["predictionIdentity"] = provenance.PredictionIdentity;
        document["provenanceCanonicalBase64"] = Convert.ToBase64String(provenance.SerializeCanonical());
        document["createdAt"] = Timestamp.FromDateTimeOffset(provenance.GenerationTime.ToDateTimeOffset());
        switch (payload)
        {
            case Prediction match:
                document["predictionPayloadKind"] = "match";
                document["predictionJson"] = JsonSerializer.Serialize(match, PredictionJsonOptions);
                break;
            case BonusPrediction bonus:
                document["predictionPayloadKind"] = "bonus";
                document["selectedOptionIds"] = bonus.SelectedOptionIds.ToArray();
                break;
            case IReadOnlyList<string> selections:
                document["predictionPayloadKind"] = "bonus";
                document["selectedOptionIds"] = selections.ToArray();
                break;
            default:
                throw new InvalidDataException("Unsupported typed prediction payload.");
        }
        return document;
    }

    private Dictionary<string, object> CreateDocument(CurrentEnvelope envelope, string kind)
    {
        var document = envelope.IdentityFields.ToDictionary(
            field => field.Key,
            field => (object)field.Value,
            StringComparer.Ordinal);
        document["epoch"] = _authorityEpoch;
        document["documentKind"] = kind;
        return document;
    }

    private void ValidateStoredEnvelope(
        DocumentSnapshot snapshot,
        CurrentEnvelope envelope,
        string expectedKind)
    {
        if (!snapshot.Exists)
        {
            throw new InvalidDataException($"Typed Firebase {expectedKind} document is missing.");
        }
        var data = snapshot.ToDictionary();
        if (!string.Equals(ReadString(data, "epoch"), _authorityEpoch, StringComparison.Ordinal)
            || !string.Equals(ReadString(data, "documentKind"), expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed Firebase document epoch or kind does not match its configured authority.");
        }
        foreach (var field in envelope.IdentityFields)
        {
            if (!string.Equals(ReadString(data, field.Key), field.Value, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Typed Firebase document identity field '{field.Key}' does not match the exact request.");
            }
        }
    }

    private static PredictionGenerationProvenanceV2 DeserializeProvenance(
        IReadOnlyDictionary<string, object> data)
    {
        try
        {
            var bytes = Convert.FromBase64String(ReadString(data, "provenanceCanonicalBase64"));
            var provenance = PredictionGenerationProvenanceV2.DeserializeCanonical(bytes);
            if (!bytes.SequenceEqual(provenance.SerializeCanonical()))
            {
                throw new InvalidDataException("Stored typed provenance is not canonical.");
            }
            return provenance;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidDataException("Stored typed provenance is invalid.", exception);
        }
    }

    private static Prediction DeserializeMatchPrediction(IReadOnlyDictionary<string, object> data)
    {
        if (!string.Equals(ReadString(data, "predictionPayloadKind"), "match", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Typed match row carries the wrong payload kind.");
        }
        try
        {
            return JsonSerializer.Deserialize<Prediction>(
                       ReadString(data, "predictionJson"),
                       PredictionJsonOptions)
                   ?? throw new InvalidDataException("Stored typed match payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Stored typed match payload is invalid.", exception);
        }
    }

    private static BonusPrediction DeserializeBonusPrediction(IReadOnlyDictionary<string, object> data)
    {
        if (!string.Equals(ReadString(data, "predictionPayloadKind"), "bonus", StringComparison.Ordinal)
            || !data.TryGetValue("selectedOptionIds", out var value)
            || value is not IEnumerable<object> objects)
        {
            throw new InvalidDataException("Typed bonus row carries the wrong payload kind or selection shape.");
        }
        return new BonusPrediction(objects.Select(item => item as string
            ?? throw new InvalidDataException("Typed bonus selection ID is not a string.")).ToList());
    }

    private static int ReadIndex(DocumentSnapshot snapshot, string name) =>
        ReadIndex(snapshot.ToDictionary(), name);

    private static int ReadIndex(IReadOnlyDictionary<string, object> data, string name)
    {
        if (!data.TryGetValue(name, out var value)
            || value is not long raw
            || raw < 0
            || raw >= int.MaxValue)
        {
            throw new InvalidDataException($"Typed Firebase index '{name}' is missing or invalid.");
        }
        return checked((int)raw);
    }

    internal static string ReadString(IReadOnlyDictionary<string, object> data, string name)
    {
        if (!data.TryGetValue(name, out var value) || value is not string text)
        {
            throw new InvalidDataException($"Typed Firebase field '{name}' is missing or not a string.");
        }
        return text;
    }

    private CollectionReference PredictionCollection(CurrentEnvelope envelope) =>
        _db.Collection(envelope.PredictionCollection);

    private DocumentReference HeadReference(CurrentEnvelope envelope) =>
        PredictionCollection(envelope).Document($"{envelope.Fingerprint}-head");

    private DocumentReference PredictionReference(CurrentEnvelope envelope, int index) =>
        PredictionCollection(envelope).Document($"{envelope.Fingerprint}-r{index:D10}");

    private DocumentReference SnapshotReference(CurrentEnvelope envelope) =>
        _db.Collection(FirebaseBundesligaTypedPredictionCollections.ItemSnapshots)
            .Document($"{envelope.Fingerprint}-snapshot");

    private sealed record CurrentEnvelope(
        string PredictionCollection,
        string Fingerprint,
        ImmutableSortedDictionary<string, string> IdentityFields,
        bool HasSeparateItemSnapshot);

    private sealed record InitialReads(
        DocumentSnapshot Head,
        DocumentSnapshot? Snapshot,
        DocumentSnapshot Row);

    private sealed record StoredCurrent(
        TypedMatchPredictionRecord? MatchPrediction,
        TypedBonusPredictionRecord? BonusPrediction,
        PredictionGenerationProvenanceV2 Provenance,
        int RepredictionIndex);
}
