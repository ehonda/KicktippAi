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
    private readonly string _competition;

    private const string KpiCollectionName = "kpi-documents";

    public FirebaseKpiRepository(FirestoreDb firestoreDb, ILogger<FirebaseKpiRepository> logger, string competition = "bundesliga-2025-26")
    {
        _firestoreDb = firestoreDb;
        _logger = logger;
        _competition = competition;
    }

    /// <summary>
    /// Saves a KPI document with versioning support.
    /// Creates a new version if content differs from the latest version.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="name">The document name.</param>
    /// <param name="content">The document content.</param>
    /// <param name="description">The document description.</param>
    /// <param name="documentType">The document type.</param>
    /// <param name="tags">Tags for categorizing the document.</param>
    /// <param name="communityContext">The community context for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version number of the saved document.</returns>
    public async Task<int> SaveKpiDocumentAsync(
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
            
            // Get the latest version to check if content has changed
            var latestVersion = await GetLatestVersionAsync(documentId, communityContext, cancellationToken);
            var version = 0;
            
            if (latestVersion >= 0)
            {
                // Check if content has changed
                var latestDocument = await GetKpiDocumentAsync(documentId, communityContext, latestVersion, cancellationToken);
                if (latestDocument != null && latestDocument.Content == content)
                {
                    _logger.LogInformation("Content unchanged for KPI document: {DocumentId} version {Version}", documentId, latestVersion);
                    return latestVersion; // Return existing version if content is the same
                }
                
                version = latestVersion + 1;
                _logger.LogDebug("Creating new version {Version} for KPI document: {DocumentId}", version, documentId);
            }
            else
            {
                _logger.LogDebug("Creating first version (0) for new KPI document: {DocumentId}", documentId);
            }
            
            // Create versioned document ID: "{documentId}_{communityContext}_{version}"
            var versionedDocumentId = $"{documentId}_{communityContext}_{version}";
            var docRef = _firestoreDb.Collection(KpiCollectionName).Document(versionedDocumentId);
            
            var firestoreKpiDocument = new FirestoreKpiDocument
            {
                Id = versionedDocumentId,
                DocumentId = documentId,
                Name = name,
                Content = content,
                Description = description,
                DocumentType = documentType,
                Tags = tags,
                Version = version,
                CreatedAt = now,
                UpdatedAt = now,
                Competition = _competition,
                CommunityContext = communityContext
            };

            await docRef.SetAsync(firestoreKpiDocument, cancellationToken: cancellationToken);

            _logger.LogInformation("Created KPI document: {DocumentId} version {Version} for community: {CommunityContext}", 
                documentId, version, communityContext);
            
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save KPI document: {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the latest version of a KPI document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest KPI document if found, otherwise null.</returns>
    public async Task<KpiDocument?> GetKpiDocumentAsync(string documentId, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestVersion = await GetLatestVersionAsync(documentId, communityContext, cancellationToken);
            if (latestVersion < 0)
            {
                return null;
            }
            
            return await GetKpiDocumentAsync(documentId, communityContext, latestVersion, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest KPI document: {DocumentId} for community: {CommunityContext}", 
                documentId, communityContext);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific version of a KPI document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="version">The specific version to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KPI document version if found, otherwise null.</returns>
    public async Task<KpiDocument?> GetKpiDocumentAsync(string documentId, string communityContext, int version, CancellationToken cancellationToken = default)
    {
        try
        {
            var versionedDocumentId = $"{documentId}_{communityContext}_{version}";
            var docRef = _firestoreDb.Collection(KpiCollectionName).Document(versionedDocumentId);
            var snapshot = await docRef.GetSnapshotAsync(cancellationToken);

            if (!snapshot.Exists)
            {
                return null;
            }

            var firestoreDoc = snapshot.ConvertTo<FirestoreKpiDocument>();
            return new KpiDocument(
                firestoreDoc.DocumentId,
                firestoreDoc.Name,
                firestoreDoc.Content,
                firestoreDoc.Description,
                firestoreDoc.DocumentType,
                firestoreDoc.Tags,
                firestoreDoc.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get KPI document: {DocumentId} version {Version} for community: {CommunityContext}", 
                documentId, version, communityContext);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all versions of a KPI document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all versions of the KPI document.</returns>
    public async Task<IReadOnlyList<KpiDocument>> GetKpiDocumentVersionsAsync(string documentId, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(KpiCollectionName)
                .WhereEqualTo("documentId", documentId)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .OrderBy("version");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var versions = new List<KpiDocument>();
            foreach (var document in snapshot.Documents)
            {
                var firestoreDoc = document.ConvertTo<FirestoreKpiDocument>();
                versions.Add(new KpiDocument(
                    firestoreDoc.DocumentId,
                    firestoreDoc.Name,
                    firestoreDoc.Content,
                    firestoreDoc.Description,
                    firestoreDoc.DocumentType,
                    firestoreDoc.Tags,
                    firestoreDoc.Version));
            }

            _logger.LogInformation("Retrieved {Count} versions for KPI document: {DocumentId} in community: {CommunityContext}", 
                versions.Count, documentId, communityContext);
            
            return versions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get versions for KPI document: {DocumentId} in community: {CommunityContext}", 
                documentId, communityContext);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all KPI documents for a specific community context (latest versions only).
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

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            // Group by documentId and get the latest version for each
            var documentGroups = snapshot.Documents
                .Select(doc => doc.ConvertTo<FirestoreKpiDocument>())
                .GroupBy(doc => doc.DocumentId)
                .ToList();

            var latestDocuments = new List<KpiDocument>();
            foreach (var group in documentGroups)
            {
                var latestDoc = group.OrderByDescending(d => d.Version).First();
                latestDocuments.Add(new KpiDocument(
                    latestDoc.DocumentId,
                    latestDoc.Name,
                    latestDoc.Content,
                    latestDoc.Description,
                    latestDoc.DocumentType,
                    latestDoc.Tags,
                    latestDoc.Version));
            }

            _logger.LogInformation("Retrieved {Count} latest KPI documents for community: {CommunityContext}", 
                latestDocuments.Count, communityContext);
            
            return latestDocuments.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all KPI documents for community: {CommunityContext}", communityContext);
            throw;
        }
    }

    /// <summary>
    /// Gets the latest version number for a specific KPI document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version number, or -1 if the document doesn't exist.</returns>
    public async Task<int> GetLatestVersionAsync(string documentId, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(KpiCollectionName)
                .WhereEqualTo("documentId", documentId)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", _competition)
                .OrderByDescending("version")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            if (snapshot.Documents.Count == 0)
            {
                return -1; // Document doesn't exist
            }

            var latestDoc = snapshot.Documents.First().ConvertTo<FirestoreKpiDocument>();
            return latestDoc.Version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest version for KPI document: {DocumentId} in community: {CommunityContext}", 
                documentId, communityContext);
            throw;
        }
    }
}
