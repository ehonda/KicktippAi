namespace Core;

/// <summary>
/// Repository interface for persisting and retrieving KPI context documents.
/// </summary>
public interface IKpiRepository
{
    /// <summary>
    /// Saves a KPI document.
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
    Task SaveKpiDocumentAsync(
        string documentId, 
        string name, 
        string content, 
        string description, 
        string documentType, 
        string[] tags,
        string communityContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a KPI document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KPI document if found, otherwise null.</returns>
    Task<KpiDocument?> GetKpiDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all KPI documents for a specific community context.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of KPI documents for the specified community.</returns>
    Task<IReadOnlyList<KpiDocument>> GetAllKpiDocumentsAsync(string communityContext, CancellationToken cancellationToken = default);
}
