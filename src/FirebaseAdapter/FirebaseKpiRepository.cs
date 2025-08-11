using Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firebase Firestore implementation of the KPI repository.
/// </summary>
public class FirebaseKpiRepository : IKpiRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebaseKpiRepository> _logger;
    private const string KpiCollectionName = "kpi-documents";
    private readonly string _competition;

    public FirebaseKpiRepository(FirestoreDb firestoreDb, ILogger<FirebaseKpiRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _competition = "bundesliga-2025-26";
        
        _logger.LogInformation("Firebase KPI repository initialized with unified collection");
    }

    /// <summary>
    /// Saves a KPI document to Firestore.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="name">The document name.</param>
    /// <param name="content">The document content.</param>
    /// <param name="description">The document description.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="tags">Tags for categorizing the document.</param>
    /// <param name="communityContext">The community context for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveKpiDocumentAsync(
        string documentId, 
        string name, 
        string content, 
        string description, 
        string documentType, 
        string[] tags,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = Timestamp.GetCurrentTimestamp();
            
            // Look for existing document with same documentId and communityContext
            var existingQuery = await _firestoreDb.Collection(KpiCollectionName)
                .WhereEqualTo("documentId", documentId)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);

            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;

            if (existingQuery.Documents.Count > 0)
            {
                // Update existing document
                var existingDoc = existingQuery.Documents.First();
                docRef = existingDoc.Reference;
                isUpdate = true;
                
                var existingData = existingDoc.ConvertTo<FirestoreKpiDocument>();
                existingCreatedAt = existingData.CreatedAt;
                
                _logger.LogDebug("Updating existing KPI document: {DocumentId} for community: {CommunityContext} (Firestore ID: {FirestoreId})", 
                    documentId, communityContext, existingDoc.Id);
            }
            else
            {
                // Create new document with GUID
                var firestoreDocumentId = Guid.NewGuid().ToString();
                docRef = _firestoreDb.Collection(KpiCollectionName).Document(firestoreDocumentId);
                
                _logger.LogDebug("Creating new KPI document: {DocumentId} for community: {CommunityContext} (Firestore ID: {FirestoreId})", 
                    documentId, communityContext, firestoreDocumentId);
            }
            
            var firestoreKpiDocument = new FirestoreKpiDocument
            {
                Id = docRef.Id,
                DocumentId = documentId,
                Name = name,
                Content = content,
                Description = description,
                DocumentType = documentType,
                Tags = tags,
                UpdatedAt = now,
                CreatedAt = existingCreatedAt ?? now,
                Competition = _competition,
                CommunityContext = communityContext
            };

            await docRef.SetAsync(firestoreKpiDocument, cancellationToken: cancellationToken);

            var action = isUpdate ? "Updated" : "Created";
            _logger.LogInformation("{Action} KPI document: {DocumentId} for community: {CommunityContext}", action, documentId, communityContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save KPI document: {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a KPI document from Firestore by documentId and communityContext.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KPI document if found, otherwise null.</returns>
    public async Task<KpiDocument?> GetKpiDocumentAsync(string documentId, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = await _firestoreDb.Collection(KpiCollectionName)
                .WhereEqualTo("documentId", documentId)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);

            if (query.Documents.Count == 0)
            {
                return null;
            }

            var firestoreDoc = query.Documents.First().ConvertTo<FirestoreKpiDocument>();
            return new KpiDocument(
                firestoreDoc.DocumentId,
                firestoreDoc.Name,
                firestoreDoc.Content,
                firestoreDoc.Description,
                firestoreDoc.DocumentType,
                firestoreDoc.Tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get KPI document: {DocumentId} for community: {CommunityContext}", documentId, communityContext);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all KPI documents from Firestore for a specific community context.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of KPI documents for the specified community.</returns>
    public async Task<IReadOnlyList<KpiDocument>> GetAllKpiDocumentsAsync(string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(KpiCollectionName)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("communityContext", communityContext);
                // Removed .OrderBy("createdAt") to avoid requiring a composite index

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var kpiDocuments = new List<KpiDocument>();
            foreach (var document in snapshot.Documents)
            {
                var firestoreDoc = document.ConvertTo<FirestoreKpiDocument>();
                kpiDocuments.Add(new KpiDocument(
                    firestoreDoc.DocumentId,
                    firestoreDoc.Name,
                    firestoreDoc.Content,
                    firestoreDoc.Description,
                    firestoreDoc.DocumentType,
                    firestoreDoc.Tags));
            }

            _logger.LogInformation("Retrieved {Count} KPI documents for community: {CommunityContext}", kpiDocuments.Count, communityContext);
            return kpiDocuments.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all KPI documents for community: {CommunityContext}", communityContext);
            throw;
        }
    }
}
