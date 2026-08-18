using System.Collections.Immutable;
using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firestore transaction boundary for complete mixed context/KPI publications.
/// </summary>
public sealed class FirebaseDocumentPublicationRepository : IDocumentPublicationRepository
{
    private const string ContextCollection = "context-documents";
    private const string KpiCollection = "kpi-documents";
    private const string HeadsCollection = "document-publication-heads";
    private const string SnapshotsCollection = "document-publication-snapshots";

    private readonly FirestoreDb _db;
    private readonly ILogger<FirebaseDocumentPublicationRepository> _logger;

    public FirebaseDocumentPublicationRepository(
        FirestoreDb db,
        ILogger<FirebaseDocumentPublicationRepository> logger,
        string competition)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        Competition = competition.Trim();
    }

    public string Competition { get; }

    public async Task<DocumentPublicationResult> PublishAsync(
        DocumentPublicationDefinition definition,
        DocumentPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        DocumentPublicationContract.ValidateRequest(Competition, definition, request);
        var scope = new DocumentPublicationScope(Competition, request.CommunityContext, definition.PublicationSet);
        var ordered = DocumentPublicationContract.ValidateAndOrder(request.Documents);
        var targetId = DocumentPublicationContract.ComputeSnapshotId(ordered);

        return await _db.RunTransactionAsync(async transaction =>
        {
            // Read and validate only the head envelope first. A stale caller must fail before
            // corrupt current or target graphs are inspected.
            var currentSnapshotId = await LoadHeadSnapshotIdAsync(transaction, scope);
            DocumentPublicationContract.EnsureExpectedHead(
                scope,
                request.ExpectedPreviousSnapshotId,
                currentSnapshotId);

            var current = currentSnapshotId is null
                ? null
                : await LoadSnapshotAsync(transaction, scope, definition, currentSnapshotId)
                  ?? throw new InvalidDataException("Publication head references a missing snapshot.");
            var target = string.Equals(targetId, currentSnapshotId, StringComparison.Ordinal)
                ? current
                : await LoadSnapshotAsync(transaction, scope, definition, targetId);
            var disposition = DocumentPublicationContract.DecideTransition(
                scope,
                request.ExpectedPreviousSnapshotId,
                currentSnapshotId,
                targetId,
                target is not null);

            if (disposition == DocumentPublicationDisposition.Unchanged)
            {
                return new DocumentPublicationResult(disposition, current!.Snapshot);
            }

            if (disposition == DocumentPublicationDisposition.Reactivated)
            {
                transaction.Set(HeadReference(scope), ToHead(scope, targetId));
                return new DocumentPublicationResult(disposition, target!.Snapshot);
            }

            var changed = ordered
                .Where(payload =>
                {
                    var existing = current?.Snapshot.Documents.SingleOrDefault(entry => entry.Key == payload.Key);
                    return existing is null
                           || !string.Equals(
                               existing.ContentSha256,
                               DocumentPublicationContract.ComputeContentSha256(payload.Content),
                               StringComparison.Ordinal);
                })
                .ToArray();
            var nextVersions = new Dictionary<DocumentPublicationKey, int>();
            foreach (var payload in changed)
            {
                nextVersions[payload.Key] = await GetNextVersionAsync(transaction, scope, payload.Key);
            }

            // One timestamp is deliberately allocated after every read. Firestore may retry this
            // callback, but each successful attempt gives all newly-created rows one instant.
            var createdAt = Timestamp.GetCurrentTimestamp();

            var entries = ImmutableArray.CreateBuilder<DocumentPublicationEntry>(ordered.Length);
            foreach (var payload in ordered)
            {
                var existing = current?.Snapshot.Documents.SingleOrDefault(entry => entry.Key == payload.Key);
                var contentHash = DocumentPublicationContract.ComputeContentSha256(payload.Content);
                if (existing is not null && string.Equals(existing.ContentSha256, contentHash, StringComparison.Ordinal))
                {
                    entries.Add(existing);
                    continue;
                }

                var version = nextVersions[payload.Key];
                entries.Add(new DocumentPublicationEntry(payload.Kind, payload.Name, version, contentHash));
                WritePayload(transaction, scope, payload, version, createdAt);
            }

            var snapshot = new DocumentPublicationSnapshot(
                scope.Competition,
                scope.CommunityContext,
                scope.PublicationSet,
                targetId,
                current?.Snapshot.SnapshotId,
                createdAt.ToDateTimeOffset(),
                request.MetadataJson,
                entries);
            transaction.Create(SnapshotReference(scope, targetId), ToFirestoreSnapshot(snapshot));
            transaction.Set(HeadReference(scope), ToHead(scope, targetId));
            return new DocumentPublicationResult(disposition, snapshot);
        }, cancellationToken: cancellationToken);
    }

    public async Task<LoadedDocumentPublication?> GetLastKnownGoodAsync(
        DocumentPublicationDefinition definition,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        DocumentPublicationContract.ValidateScope(new DocumentPublicationScope(Competition, communityContext, definition.PublicationSet));
        DocumentPublicationContract.ValidateRequest(
            Competition,
            definition,
            new DocumentPublicationRequest(communityContext, null, definition.RequiredDocuments.Select(key => new DocumentPublicationPayload(
                key.Kind,
                key.Name,
                string.Empty,
                key.Kind == DocumentPublicationKind.Kpi ? "validation" : null))));

        var scope = new DocumentPublicationScope(Competition, communityContext, definition.PublicationSet);
        return await _db.RunTransactionAsync(async transaction =>
        {
            var snapshotId = await LoadHeadSnapshotIdAsync(transaction, scope);
            return snapshotId is null
                ? null
                : await LoadSnapshotAsync(transaction, scope, definition, snapshotId)
                  ?? throw new InvalidDataException("Publication head references a missing snapshot.");
        }, cancellationToken: cancellationToken);
    }

    private async Task<string?> LoadHeadSnapshotIdAsync(
        Transaction transaction,
        DocumentPublicationScope scope)
    {
        var reference = HeadReference(scope);
        var headSnapshot = await transaction.GetSnapshotAsync(reference);
        if (!headSnapshot.Exists)
        {
            return null;
        }

        if (headSnapshot.Id != reference.Id || !string.Equals(headSnapshot.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication head identity is corrupt.");
        }

        var head = headSnapshot.ConvertTo<FirestoreDocumentPublicationHead>();
        if (head.Competition != scope.Competition
            || head.CommunityContext != scope.CommunityContext
            || head.PublicationSet != scope.PublicationSet)
        {
            throw new InvalidDataException("Publication head scope is corrupt.");
        }

        return head.SnapshotId;
    }

    private async Task<LoadedDocumentPublication?> LoadSnapshotAsync(
        Transaction transaction,
        DocumentPublicationScope scope,
        DocumentPublicationDefinition definition,
        string snapshotId)
    {
        var reference = SnapshotReference(scope, snapshotId);
        var metadata = await transaction.GetSnapshotAsync(reference);
        if (!metadata.Exists)
        {
            return null;
        }

        if (metadata.Id != reference.Id || !string.Equals(metadata.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication snapshot metadata identity is corrupt.");
        }

        var stored = metadata.ConvertTo<FirestoreDocumentPublicationSnapshot>();
        if (!string.Equals(stored.SnapshotId, snapshotId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication snapshot metadata does not match its requested snapshot ID.");
        }

        var snapshot = FromFirestoreSnapshot(stored);
        var documents = ImmutableArray.CreateBuilder<PublishedDocument>(snapshot.Documents.Length);
        foreach (var entry in snapshot.Documents)
        {
            var payload = await ReadPayloadAsync(transaction, scope, entry);
            if (payload is null)
            {
                throw new InvalidDataException("Publication snapshot references a missing payload.");
            }

            documents.Add(payload);
        }

        DocumentPublicationContract.ValidateLoaded(scope.Competition, scope.CommunityContext, definition, snapshot, documents);
        return new LoadedDocumentPublication(snapshot, documents);
    }

    private async Task<PublishedDocument?> ReadPayloadAsync(Transaction transaction, DocumentPublicationScope scope, DocumentPublicationEntry entry)
    {
        var reference = PayloadReference(scope, entry.Key, entry.Version);
        var snapshot = await transaction.GetSnapshotAsync(reference);
        if (!snapshot.Exists)
        {
            return null;
        }

        if (snapshot.Id != reference.Id || !string.Equals(snapshot.Reference.Path, reference.Path, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Publication payload identity is corrupt.");
        }

        if (entry.Kind == DocumentPublicationKind.Context)
        {
            var value = snapshot.ConvertTo<FirestoreContextDocument>();
            return new PublishedDocument(value.Competition, value.CommunityContext, value.PublicationSet,
                entry.Kind, value.DocumentName, value.Version, value.Content, null, value.CreatedAt.ToDateTimeOffset());
        }

        var kpi = snapshot.ConvertTo<FirestoreKpiDocument>();
        return new PublishedDocument(kpi.Competition, kpi.CommunityContext, kpi.PublicationSet,
            entry.Kind, kpi.DocumentName, kpi.Version, kpi.Content, kpi.Description, kpi.CreatedAt.ToDateTimeOffset());
    }

    private async Task<int> GetNextVersionAsync(Transaction transaction, DocumentPublicationScope scope, DocumentPublicationKey key)
    {
        var query = _db.Collection(CollectionFor(key.Kind))
            .WhereEqualTo("competition", scope.Competition)
            .WhereEqualTo("communityContext", scope.CommunityContext)
            .WhereEqualTo("documentName", key.Name)
            .OrderByDescending("version")
            .Limit(1);
        var snapshot = await transaction.GetSnapshotAsync(query);
        if (snapshot.Count == 0)
        {
            return 0;
        }

        return snapshot.Documents[0].GetValue<int>("version") + 1;
    }

    private void WritePayload(
        Transaction transaction,
        DocumentPublicationScope scope,
        DocumentPublicationPayload payload,
        int version,
        Timestamp createdAt)
    {
        var reference = PayloadReference(scope, payload.Key, version);
        if (payload.Kind == DocumentPublicationKind.Context)
        {
            transaction.Create(reference, new FirestoreContextDocument
            {
                Id = reference.Id,
                Competition = scope.Competition,
                CommunityContext = scope.CommunityContext,
                PublicationSet = scope.PublicationSet,
                DocumentName = payload.Name,
                Content = payload.Content,
                Version = version,
                CreatedAt = createdAt
            });
            return;
        }

        transaction.Create(reference, new FirestoreKpiDocument
        {
            Id = reference.Id,
            Competition = scope.Competition,
            CommunityContext = scope.CommunityContext,
            PublicationSet = scope.PublicationSet,
            DocumentName = payload.Name,
            Content = payload.Content,
            Description = payload.Description!,
            Version = version,
            CreatedAt = createdAt
        });
    }

    private DocumentReference HeadReference(DocumentPublicationScope scope) =>
        _db.Collection(HeadsCollection).Document(DocumentPublicationContract.ComputeHeadId(scope));

    private DocumentReference SnapshotReference(DocumentPublicationScope scope, string snapshotId) =>
        _db.Collection(SnapshotsCollection).Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, snapshotId));

    private DocumentReference PayloadReference(DocumentPublicationScope scope, DocumentPublicationKey key, int version) =>
        _db.Collection(CollectionFor(key.Kind)).Document(
            BuildPublicationPayloadId(scope, key.Name, version));

    internal static string BuildPublicationPayloadId(DocumentPublicationScope scope, string documentName, int version) =>
        $"{DocumentPublicationContract.ComputeHeadId(scope)}_{documentName}_{version}";

    private static string CollectionFor(DocumentPublicationKind kind) =>
        kind == DocumentPublicationKind.Context ? ContextCollection : KpiCollection;

    private static FirestoreDocumentPublicationHead ToHead(DocumentPublicationScope scope, string snapshotId) => new()
    {
        Competition = scope.Competition,
        CommunityContext = scope.CommunityContext,
        PublicationSet = scope.PublicationSet,
        SnapshotId = snapshotId
    };

    private static FirestoreDocumentPublicationSnapshot ToFirestoreSnapshot(DocumentPublicationSnapshot snapshot) => new()
    {
        Competition = snapshot.Competition,
        CommunityContext = snapshot.CommunityContext,
        PublicationSet = snapshot.PublicationSet,
        SnapshotId = snapshot.SnapshotId,
        PreviousSnapshotId = snapshot.PreviousSnapshotId,
        CreatedAt = Timestamp.FromDateTime(snapshot.CreatedAt.UtcDateTime),
        MetadataJson = snapshot.MetadataJson,
        Documents = snapshot.Documents.Select(entry => new FirestoreDocumentPublicationEntry
        {
            Kind = entry.Kind.ToString(), Name = entry.Name, Version = entry.Version, ContentSha256 = entry.ContentSha256
        }).ToList()
    };

    private static DocumentPublicationSnapshot FromFirestoreSnapshot(FirestoreDocumentPublicationSnapshot snapshot) => new(
        snapshot.Competition, snapshot.CommunityContext, snapshot.PublicationSet, snapshot.SnapshotId,
        snapshot.PreviousSnapshotId, snapshot.CreatedAt.ToDateTimeOffset(), snapshot.MetadataJson,
        snapshot.Documents.Select(entry => new DocumentPublicationEntry(
            Enum.Parse<DocumentPublicationKind>(entry.Kind, ignoreCase: false), entry.Name, entry.Version, entry.ContentSha256)));
}
