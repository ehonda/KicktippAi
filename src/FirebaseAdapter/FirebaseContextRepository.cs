using Core;
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

    public FirebaseContextRepository(FirestoreDb firestoreDb, ILogger<FirebaseContextRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _contextDocumentsCollection = "context-documents";
        _competition = "bundesliga-2025-26";
        
        _logger.LogInformation("Firebase context repository initialized");
    }

    public async Task<int?> SaveContextDocumentAsync(string documentName, string content, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the latest version to check if content differs
            var latestDocument = await GetLatestContextDocumentAsync(documentName, communityContext, cancellationToken);
            
            // If content is the same, don't save a new version
            if (latestDocument != null && latestDocument.Content == content)
            {
                _logger.LogInformation("Context document {DocumentName} content unchanged, skipping save", documentName);
                return null;
            }
            
            // Determine the next version number
            var nextVersion = latestDocument?.Version + 1 ?? 0;
            
            var now = Timestamp.GetCurrentTimestamp();
            var documentId = $"{documentName}_{communityContext}_{nextVersion}";
            
            var firestoreDocument = new FirestoreContextDocument
            {
                Id = documentId,
                DocumentName = documentName,
                Content = content,
                Version = nextVersion,
                CreatedAt = now,
                Competition = _competition,
                CommunityContext = communityContext
            };
            
            var docRef = _firestoreDb.Collection(_contextDocumentsCollection).Document(documentId);
            await docRef.SetAsync(firestoreDocument, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Saved context document {DocumentName} version {Version} for community {CommunityContext}", 
                documentName, nextVersion, communityContext);
            
            return nextVersion;
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
                .OrderByDescending("version")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            if (!snapshot.Documents.Any())
            {
                return null;
            }

            var firestoreDoc = snapshot.Documents.First().ConvertTo<FirestoreContextDocument>();
            return ConvertToContextDocument(firestoreDoc);
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
            var documentId = $"{documentName}_{communityContext}_{version}";
            var docRef = _firestoreDb.Collection(_contextDocumentsCollection).Document(documentId);
            var snapshot = await docRef.GetSnapshotAsync(cancellationToken);
            
            if (!snapshot.Exists)
            {
                return null;
            }

            var firestoreDoc = snapshot.ConvertTo<FirestoreContextDocument>();
            return ConvertToContextDocument(firestoreDoc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context document {DocumentName} version {Version} for community {CommunityContext}", 
                documentName, version, communityContext);
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
                .Select("documentName");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var documentNames = snapshot.Documents
                .Select(doc => doc.GetValue<string>("documentName"))
                .Distinct()
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

    private static ContextDocument ConvertToContextDocument(FirestoreContextDocument firestoreDoc)
    {
        return new ContextDocument(
            firestoreDoc.DocumentName,
            firestoreDoc.Content,
            firestoreDoc.Version,
            firestoreDoc.CreatedAt.ToDateTime());
    }
}
