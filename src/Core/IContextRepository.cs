namespace EHonda.KicktippAi.Core;

public sealed record ContextDocumentWrite(string DocumentName, string Content);

/// <summary>
/// Describes one document selected by an atomic publication transaction.
/// <see cref="Version"/> retains the legacy created-version/null contract, while
/// <see cref="EffectiveVersion"/> identifies the immutable row selected by the transaction
/// whether the row was created or already contained the requested bytes.
/// </summary>
public sealed record ContextDocumentSaveResult(string DocumentName, int? Version)
{
    public ContextDocumentSaveResult(string documentName, int? version, int effectiveVersion)
        : this(documentName, version)
    {
        if (effectiveVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(effectiveVersion));
        }

        EffectiveVersion = effectiveVersion;
    }

    public int? CreatedVersion => Version;

    /// <summary>
    /// The exact immutable version selected inside the transaction. Legacy implementations may
    /// leave this null; publication gates that need immutable identity must require a value.
    /// </summary>
    public int? EffectiveVersion { get; init; }
}

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
    /// Atomically saves a complete set of ordinary context documents. The operation either publishes every changed
    /// document or publishes none; unchanged documents retain their existing versions. Every result identifies the
    /// effective immutable version selected inside the transaction while preserving the legacy created-version/null
    /// value in <see cref="ContextDocumentSaveResult.Version"/>.
    /// </summary>
    Task<IReadOnlyList<ContextDocumentSaveResult>> SaveContextDocumentsAtomicallyAsync(
        IReadOnlyList<ContextDocumentWrite> documents,
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
    /// Retrieves the latest version of a context document that existed at the specified timestamp.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="createdAtOrEarlier">The timestamp that the resolved document must not exceed.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest context document whose creation time is less than or equal to the supplied timestamp, or null if none match.</returns>
    Task<ContextDocument?> GetContextDocumentByTimestampAsync(
        string documentName,
        DateTimeOffset createdAtOrEarlier,
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

    /// <summary>
    /// Retrieves all versions of a context document.
    /// </summary>
    /// <param name="documentName">The document name.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all versions of the context document, ordered by version.</returns>
    Task<IReadOnlyList<ContextDocument>> GetContextDocumentVersionsAsync(
        string documentName, 
        string communityContext, 
        CancellationToken cancellationToken = default);

}
