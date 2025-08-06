using System.Runtime.CompilerServices;
using Core;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firebase-based context provider for KPI documents.
/// Retrieves KPI documents from Firestore for use in bonus predictions.
/// </summary>
public class FirebaseKpiContextProvider : IContextProvider<DocumentContext>
{
    private readonly IKpiRepository _kpiRepository;
    private readonly ILogger<FirebaseKpiContextProvider> _logger;

    public FirebaseKpiContextProvider(IKpiRepository kpiRepository, ILogger<FirebaseKpiContextProvider> logger)
    {
        _kpiRepository = kpiRepository ?? throw new ArgumentNullException(nameof(kpiRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all KPI documents as context for predictions.
    /// This method provides all available KPI documents for bonus predictions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing KPI data.</returns>
    public async IAsyncEnumerable<DocumentContext> GetContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all KPI documents for context");

        IReadOnlyList<KpiDocument> kpiDocuments;
        try
        {
            kpiDocuments = await _kpiRepository.GetAllKpiDocumentsAsync(cancellationToken);
            _logger.LogInformation("Found {DocumentCount} KPI documents for context", kpiDocuments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve KPI documents for context");
            throw;
        }

        foreach (var kpiDocument in kpiDocuments)
        {
            _logger.LogDebug("Providing KPI document context: {DocumentId}", kpiDocument.DocumentId);
            
            yield return new DocumentContext(
                Name: $"{kpiDocument.Name} ({kpiDocument.DocumentType})",
                Content: kpiDocument.Content);
        }
    }

    /// <summary>
    /// Gets KPI context specifically for bonus questions.
    /// This is an alias for GetContextAsync() since KPI documents are primarily used for bonus predictions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing KPI data for bonus questions.</returns>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var context in GetContextAsync(cancellationToken))
        {
            yield return context;
        }
    }

    /// <summary>
    /// Gets a specific KPI document by its ID.
    /// </summary>
    /// <param name="documentId">The ID of the KPI document to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The document context for the specified KPI document, or null if not found.</returns>
    public async Task<DocumentContext?> GetKpiDocumentContextAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving specific KPI document: {DocumentId}", documentId);

        try
        {
            var kpiDocument = await _kpiRepository.GetKpiDocumentAsync(documentId, cancellationToken);
            
            if (kpiDocument == null)
            {
                _logger.LogWarning("KPI document not found: {DocumentId}", documentId);
                return null;
            }

            _logger.LogDebug("Found KPI document: {DocumentId}", documentId);
            
            return new DocumentContext(
                Name: $"{kpiDocument.Name} ({kpiDocument.DocumentType})",
                Content: kpiDocument.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve KPI document: {DocumentId}", documentId);
            throw;
        }
    }

    /// <summary>
    /// Gets KPI documents filtered by specific tags.
    /// </summary>
    /// <param name="tags">The tags to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts for KPI documents matching the specified tags.</returns>
    public async IAsyncEnumerable<DocumentContext> GetKpiDocumentsByTagsAsync(string[] tags, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving KPI documents with tags: {Tags}", string.Join(", ", tags));

        IReadOnlyList<KpiDocument> allKpiDocuments;
        try
        {
            allKpiDocuments = await _kpiRepository.GetAllKpiDocumentsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve KPI documents by tags: {Tags}", string.Join(", ", tags));
            throw;
        }

        var filteredDocuments = allKpiDocuments.Where(doc => 
            tags.Any(tag => doc.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));

        var filteredList = filteredDocuments.ToList();
        _logger.LogInformation("Found {DocumentCount} KPI documents matching tags: {Tags}", 
            filteredList.Count, string.Join(", ", tags));

        foreach (var kpiDocument in filteredList)
        {
            _logger.LogDebug("Providing tagged KPI document context: {DocumentId} (tags: {DocumentTags})", 
                kpiDocument.DocumentId, string.Join(", ", kpiDocument.Tags));
            
            yield return new DocumentContext(
                Name: $"{kpiDocument.Name} ({kpiDocument.DocumentType})",
                Content: kpiDocument.Content);
        }
    }
}
