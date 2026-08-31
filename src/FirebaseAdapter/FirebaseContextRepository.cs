using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FirebaseAdapter;

/// <summary>
/// Firebase Firestore implementation of the context repository.
/// </summary>
public class FirebaseContextRepository : IContextRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebaseContextRepository> _logger;
    private readonly string _contextDocumentsCollection;
    private readonly string _competition;

    public FirebaseContextRepository(
        FirestoreDb firestoreDb,
        ILogger<FirebaseContextRepository> logger,
        string competition)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _contextDocumentsCollection = "context-documents";
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);
        _competition = competition.Trim();
        
        _logger.LogInformation("Firebase context repository initialized");
    }

    public async Task<int?> SaveContextDocumentAsync(string documentName, string content, string communityContext, CancellationToken cancellationToken = default)
    {
        BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
            _competition,
            DocumentPublicationKind.Context,
            documentName);
        try
        {
            var savedVersion = await _firestoreDb.RunTransactionAsync(async transaction =>
            {
                var query = _firestoreDb.Collection(_contextDocumentsCollection)
                    .WhereEqualTo("documentName", documentName)
                    .WhereEqualTo("communityContext", communityContext)
                    .WhereEqualTo("competition", _competition)
                    .OrderByDescending("version");
                var matchingRows = await transaction.GetSnapshotAsync(query);
                // Publication-scoped payloads share this collection. They participate in the
                // version ceiling so a generic row can never collide with an existing payload,
                // but only an ordinary row is eligible for the same-content no-op decision.
                var existing = matchingRows.Documents
                    .Where(document => string.IsNullOrEmpty(document.ConvertTo<FirestoreContextDocument>().PublicationSet))
                    .Select(document => ValidateOrdinaryContextDocument(
                        document.ConvertTo<FirestoreContextDocument>(),
                        documentName,
                        communityContext,
                        expectedVersion: null,
                        document.Id))
                    .FirstOrDefault();
                if (existing is not null && string.Equals(existing.Content, content, StringComparison.Ordinal))
                {
                    return (int?)null;
                }

                var nextVersion = matchingRows.Documents.Count == 0
                    ? 0
                    : matchingRows.Documents.Max(document => document.GetValue<int>("version")) + 1;
                var documentId = BuildDocumentId(documentName, communityContext, nextVersion);
                var reference = _firestoreDb.Collection(_contextDocumentsCollection).Document(documentId);
                transaction.Create(reference, new FirestoreContextDocument
                {
                    Id = documentId,
                    DocumentName = documentName,
                    Content = content,
                    Version = nextVersion,
                    CreatedAt = Timestamp.GetCurrentTimestamp(),
                    Competition = _competition,
                    CommunityContext = communityContext
                });
                return (int?)nextVersion;
            }, cancellationToken: cancellationToken);

            _logger.LogInformation("Saved context document {DocumentName} version {Version} for community {CommunityContext}",
                documentName, savedVersion, communityContext);

            return savedVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save context document {DocumentName} for community {CommunityContext}", 
                documentName, communityContext);
            throw;
        }
    }

    public async Task<ContextDocument?> GetLatestContextDocumentAsync(string documentName, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_contextDocumentsCollection)
                .WhereEqualTo("documentName", documentName)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .OrderByDescending("version");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents
                .Select(document => new { Document = document, Value = document.ConvertTo<FirestoreContextDocument>() })
                // Older ordinary rows do not have a publicationSet property. They remain eligible.
                .Where(candidate => string.IsNullOrEmpty(candidate.Value.PublicationSet))
                .Select(candidate => ValidateOrdinaryContextDocument(
                    candidate.Value, documentName, communityContext, expectedVersion: null, candidate.Document.Id))
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve latest context document {DocumentName} for community {CommunityContext}", 
                documentName, communityContext);
            throw;
        }
    }

    public async Task<ContextDocument?> GetContextDocumentAsync(string documentName, int version, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            if (BundesligaDocumentPublication.TryGetOwningDefinition(
                    _competition,
                    DocumentPublicationKind.Context,
                    documentName,
                    out var definition))
            {
                var scope = new DocumentPublicationScope(_competition, communityContext, definition!.PublicationSet);
                var publicationId = FirebaseDocumentPublicationRepository.BuildPublicationPayloadId(scope, documentName, version);
                var publicationSnapshot = await _firestoreDb.Collection(_contextDocumentsCollection)
                    .Document(publicationId)
                    .GetSnapshotAsync(cancellationToken);
                if (publicationSnapshot.Exists)
                {
                    var publicationDocument = publicationSnapshot.ConvertTo<FirestoreContextDocument>();
                    if (publicationDocument.Competition != scope.Competition
                        || publicationDocument.CommunityContext != scope.CommunityContext
                        || publicationDocument.PublicationSet != scope.PublicationSet
                        || publicationDocument.DocumentName != documentName
                        || publicationDocument.Version != version)
                    {
                        throw new InvalidDataException("Publication-scoped context payload identity is corrupt.");
                    }

                    return new ContextDocument(
                        publicationDocument.DocumentName,
                        publicationDocument.Content,
                        publicationDocument.Version,
                        publicationDocument.CreatedAt.ToDateTimeOffset());
                }
            }

            var documentId = BuildDocumentId(documentName, communityContext, version);
            var docRef = _firestoreDb.Collection(_contextDocumentsCollection).Document(documentId);
            var snapshot = await docRef.GetSnapshotAsync(cancellationToken);
            
            if (!snapshot.Exists)
            {
                return null;
            }

            return ValidateOrdinaryContextDocument(
                snapshot.ConvertTo<FirestoreContextDocument>(), documentName, communityContext, version, snapshot.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context document {DocumentName} version {Version} for community {CommunityContext}", 
                documentName, version, communityContext);
            throw;
        }
    }

    public async Task<ContextDocument?> GetContextDocumentByTimestampAsync(
        string documentName,
        DateTimeOffset createdAtOrEarlier,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_contextDocumentsCollection)
                .WhereEqualTo("documentName", documentName)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .WhereLessThanOrEqualTo("createdAt", Timestamp.FromDateTime(createdAtOrEarlier.UtcDateTime))
                .OrderByDescending("createdAt")
                .OrderByDescending("version");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents
                .Select(document => new { Document = document, Value = document.ConvertTo<FirestoreContextDocument>() })
                .Where(candidate => string.IsNullOrEmpty(candidate.Value.PublicationSet))
                .Select(candidate => ValidateOrdinaryContextDocument(
                    candidate.Value, documentName, communityContext, expectedVersion: null, candidate.Document.Id))
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve context document {DocumentName} at timestamp {CreatedAt} for community {CommunityContext}",
                documentName,
                createdAtOrEarlier,
                communityContext);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetContextDocumentNamesAsync(string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_contextDocumentsCollection)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .Select("documentName", "publicationSet");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            var documentNames = snapshot.Documents
                .Where(doc => !doc.ContainsField("publicationSet")
                    || string.IsNullOrEmpty(doc.GetValue<string>("publicationSet")))
                .Select(doc => doc.GetValue<string>("documentName"))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
            
            return documentNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context document names for community {CommunityContext}", communityContext);
            throw;
        }
    }

    public async Task<IReadOnlyList<ContextDocument>> GetContextDocumentVersionsAsync(string documentName, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_contextDocumentsCollection)
                .WhereEqualTo("documentName", documentName)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .OrderBy("version");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var documents = snapshot.Documents
                .Select(document => new { Document = document, Value = document.ConvertTo<FirestoreContextDocument>() })
                .Where(candidate => string.IsNullOrEmpty(candidate.Value.PublicationSet))
                .Select(candidate => ValidateOrdinaryContextDocument(
                    candidate.Value, documentName, communityContext, expectedVersion: null, candidate.Document.Id))
                .ToList()
                .AsReadOnly();
            
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context document versions for {DocumentName} in community {CommunityContext}", 
                documentName, communityContext);
            throw;
        }
    }

    private ContextDocument ValidateOrdinaryContextDocument(
        FirestoreContextDocument firestoreDoc,
        string documentName,
        string communityContext,
        int? expectedVersion,
        string documentId)
    {
        if (!string.Equals(documentId, BuildDocumentId(documentName, communityContext, firestoreDoc.Version), StringComparison.Ordinal)
            || !string.Equals(firestoreDoc.Competition, _competition, StringComparison.Ordinal)
            || !string.Equals(firestoreDoc.CommunityContext, communityContext, StringComparison.Ordinal)
            || !string.Equals(firestoreDoc.DocumentName, documentName, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(firestoreDoc.PublicationSet)
            || expectedVersion is not null && firestoreDoc.Version != expectedVersion.Value)
        {
            throw new InvalidDataException("Ordinary context document scope or exact identity is corrupt.");
        }

        return new ContextDocument(
            firestoreDoc.DocumentName,
            firestoreDoc.Content,
            firestoreDoc.Version,
            firestoreDoc.CreatedAt.ToDateTimeOffset());
    }

    public async Task<IReadOnlyList<ContextDocumentSaveResult>> SaveContextDocumentsAtomicallyAsync(
        IReadOnlyList<ContextDocumentWrite> documents,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one context document is required for an atomic save.", nameof(documents));
        }

        var orderedDocuments = documents.OrderBy(document => document.DocumentName, StringComparer.Ordinal).ToArray();
        foreach (var document in orderedDocuments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(document.DocumentName);
            ArgumentNullException.ThrowIfNull(document.Content);
            BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
                _competition,
                DocumentPublicationKind.Context,
                document.DocumentName);
        }
        var duplicate = orderedDocuments.GroupBy(document => document.DocumentName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Atomic context save contains duplicate document '{duplicate.Key}'.", nameof(documents));
        }
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var results = await _firestoreDb.RunTransactionAsync(async transaction =>
            {
                var pending = new List<(ContextDocumentWrite Document, int? Version, DocumentReference? Reference)>();

                // Firestore transactions require every read to happen before the first write. Build the entire batch
                // and validate every ordinary row before staging any create operation.
                foreach (var document in orderedDocuments)
                {
                    var query = _firestoreDb.Collection(_contextDocumentsCollection)
                        .WhereEqualTo("documentName", document.DocumentName)
                        .WhereEqualTo("communityContext", communityContext)
                        .WhereEqualTo("competition", _competition)
                        .OrderByDescending("version");
                    var matchingRows = await transaction.GetSnapshotAsync(query);
                    var ordinaryRows = matchingRows.Documents
                        .Where(snapshot => string.IsNullOrEmpty(snapshot.ConvertTo<FirestoreContextDocument>().PublicationSet))
                        .Select(snapshot => ValidateOrdinaryContextDocument(
                            snapshot.ConvertTo<FirestoreContextDocument>(),
                            document.DocumentName,
                            communityContext,
                            expectedVersion: null,
                            snapshot.Id))
                        .ToArray();
                    var existing = ordinaryRows.FirstOrDefault();
                    if (existing is not null && string.Equals(existing.Content, document.Content, StringComparison.Ordinal))
                    {
                        pending.Add((document, null, null));
                        continue;
                    }

                    var nextVersion = matchingRows.Documents.Count == 0
                        ? 0
                        : checked(matchingRows.Documents.Max(snapshot => snapshot.GetValue<int>("version")) + 1);
                    var documentId = BuildDocumentId(document.DocumentName, communityContext, nextVersion);
                    pending.Add((document, nextVersion,
                        _firestoreDb.Collection(_contextDocumentsCollection).Document(documentId)));
                }

                var createdAt = Timestamp.GetCurrentTimestamp();
                foreach (var item in pending.Where(item => item.Version.HasValue))
                {
                    var documentId = item.Reference!.Id;
                    transaction.Create(item.Reference, new FirestoreContextDocument
                    {
                        Id = documentId,
                        DocumentName = item.Document.DocumentName,
                        Content = item.Document.Content,
                        Version = item.Version!.Value,
                        CreatedAt = createdAt,
                        Competition = _competition,
                        CommunityContext = communityContext
                    });
                }

                return (IReadOnlyList<ContextDocumentSaveResult>)pending
                    .Select(item => new ContextDocumentSaveResult(item.Document.DocumentName, item.Version))
                    .ToArray();
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Atomically saved {SavedCount} of {DocumentCount} context documents for community {CommunityContext}",
                results.Count(result => result.Version.HasValue), results.Count, communityContext);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to atomically save {DocumentCount} context documents for community {CommunityContext}",
                documents.Count, communityContext);
            throw;
        }
    }

    private string BuildDocumentId(string documentName, string communityContext, int version)
    {
        return $"{_competition}_{documentName}_{communityContext}_{version}";
    }
}
