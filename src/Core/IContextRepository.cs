namespace Core;

/// <summary>
/// Repository interface for persisting and retrieving versioned context documents.
/// </summary>
public interface IContextRepository
{
    /// <summary>
    /// Saves a context document with automatic versioning.
    /// Only saves if the content differs from the latest version.
    /// </summary>
    /// <param name="documentName">The context document name.</param>
    /// <param name="content">The document content.</param>
    /// <param name="communityContext">The community context for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version number of the saved document, or null if no save was needed.</returns>
    Task<int?> SaveContextDocumentAsync(
        string documentName, 
        string content, 
        string communityContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest version of a context document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest context document if found, otherwise null.</returns>
    Task<ContextDocument?> GetLatestContextDocumentAsync(
        string documentName, 
        string communityContext, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of a context document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="version">The document version.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The context document if found, otherwise null.</returns>
    Task<ContextDocument?> GetContextDocumentAsync(
        string documentName, 
        int version, 
        string communityContext, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all context document names for a specific community context.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of document names for the specified community.</returns>
    Task<IReadOnlyList<string>> GetContextDocumentNamesAsync(
        string communityContext, 
        CancellationToken cancellationToken = default);
}
