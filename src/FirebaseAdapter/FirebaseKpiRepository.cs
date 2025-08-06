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
    private readonly string _kpiCollectionName;
    private readonly string _competition;
    private readonly string _community;

    public FirebaseKpiRepository(FirestoreDb firestoreDb, ILogger<FirebaseKpiRepository> logger, string community)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(community))
            throw new ArgumentException("Community cannot be null or empty", nameof(community));
            
        _community = community;
        
        // Make collection name community-specific
        _kpiCollectionName = $"kpiDocuments-{community}";
        _competition = $"bundesliga-2025-26-{community}";
        
        _logger.LogInformation("Firebase KPI repository initialized for community: {Community}", community);
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SaveKpiDocumentAsync(
        string documentId, 
        string name, 
        string content, 
        string description, 
        string documentType, 
        string[] tags, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = Timestamp.GetCurrentTimestamp();
            
            var firestoreKpiDocument = new FirestoreKpiDocument
            {
                Id = documentId,
                DocumentId = documentId,
                Name = name,
                Content = content,
                Description = description,
                DocumentType = documentType,
                Tags = tags,
                UpdatedAt = now,
                Competition = _competition
            };

            // Check if document already exists to set created timestamp
            var existingDoc = await _firestoreDb.Collection(_kpiCollectionName)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            if (existingDoc.Exists)
            {
                var existing = existingDoc.ConvertTo<FirestoreKpiDocument>();
                firestoreKpiDocument.CreatedAt = existing.CreatedAt;
                _logger.LogDebug("Updating existing KPI document: {DocumentId}", documentId);
            }
            else
            {
                firestoreKpiDocument.CreatedAt = now;
                _logger.LogDebug("Creating new KPI document: {DocumentId}", documentId);
            }

            await _firestoreDb.Collection(_kpiCollectionName)
                .Document(documentId)
                .SetAsync(firestoreKpiDocument, cancellationToken: cancellationToken);

            _logger.LogInformation("Saved KPI document: {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save KPI document: {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a KPI document from Firestore.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KPI document if found, otherwise null.</returns>
    public async Task<KpiDocument?> GetKpiDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _firestoreDb.Collection(_kpiCollectionName)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            if (!document.Exists)
            {
                return null;
            }

            var firestoreDoc = document.ConvertTo<FirestoreKpiDocument>();
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
            _logger.LogError(ex, "Failed to get KPI document: {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all KPI documents from Firestore.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all KPI documents.</returns>
    public async Task<IReadOnlyList<KpiDocument>> GetAllKpiDocumentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_kpiCollectionName)
                .WhereEqualTo("competition", _competition);
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

            return kpiDocuments.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all KPI documents");
            throw;
        }
    }
}
