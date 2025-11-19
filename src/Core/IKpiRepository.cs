namespace EHonda.KicktippAi.Core;

/// <summary>
/// Repository interface for persisting and retrieving KPI context documents.
/// </summary>
public interface IKpiRepository
{
    /// <summary>
    /// Saves a KPI document with versioning support.
    /// Creates a new version if content differs from the latest version.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="content">The document content.</param>
    /// <param name="description">The document description.</param>
    /// <param name="communityContext">The community context for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version number of the saved document.</returns>
    Task<int> SaveKpiDocumentAsync(
        string documentName, 
        string content, 
        string description, 
        string communityContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest version of a KPI document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest KPI document if found, otherwise null.</returns>
    Task<KpiDocument?> GetKpiDocumentAsync(string documentName, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of a KPI document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="version">The specific version to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The KPI document version if found, otherwise null.</returns>
    Task<KpiDocument?> GetKpiDocumentAsync(string documentName, string communityContext, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all versions of a KPI document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all versions of the KPI document.</returns>
    Task<IReadOnlyList<KpiDocument>> GetKpiDocumentVersionsAsync(string documentName, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all KPI documents for a specific community context (latest versions only).
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of KPI documents for the specified community.</returns>
    Task<IReadOnlyList<KpiDocument>> GetAllKpiDocumentsAsync(string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest version number for a specific KPI document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version number, or -1 if the document doesn't exist.</returns>
    Task<int> GetLatestVersionAsync(string documentName, string communityContext, CancellationToken cancellationToken = default);
}
