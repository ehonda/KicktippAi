namespace EHonda.KicktippAi.Core;

/// <summary>
/// Stable metadata for a context document version resolved during prompt reconstruction.
/// </summary>
public record ResolvedContextDocumentVersion(
    string DocumentName,
    int Version,
    DateTimeOffset CreatedAt,
    string Content);

/// <summary>
/// Reconstructed match prompt inputs for a historical prediction.
/// </summary>
public record ReconstructedMatchPredictionPrompt(
    Match Match,
    string Model,
    string CommunityContext,
    bool IncludeJustification,
    DateTimeOffset PromptTimestamp,
    string PromptTemplatePath,
    string SystemPrompt,
    string MatchJson,
    IReadOnlyList<string> ContextDocumentNames,
    IReadOnlyList<ResolvedContextDocumentVersion> ResolvedContextDocuments);

/// <summary>
/// Reconstructs prompt inputs for historical match predictions.
/// </summary>
public interface IMatchPromptReconstructionService
{
    /// <summary>
    /// Reconstructs the prompt inputs used for a stored match prediction.
    /// </summary>
    /// <param name="match">The match whose historical prediction should be reconstructed.</param>
    /// <param name="model">The model that was used for the stored prediction.</param>
    /// <param name="communityContext">The community context used for the stored prediction.</param>
    /// <param name="includeJustification">Whether to reconstruct the justification prompt variant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reconstructed prompt inputs if the prediction exists; otherwise <c>null</c>.</returns>
    Task<ReconstructedMatchPredictionPrompt?> ReconstructMatchPredictionPromptAsync(
        Match match,
        string model,
        string communityContext,
        bool includeJustification = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconstructs the prompt inputs for a match at an explicit evaluation timestamp.
    /// </summary>
    /// <param name="match">The match whose prompt should be reconstructed.</param>
    /// <param name="model">The model whose template should be used.</param>
    /// <param name="communityContext">The community context used for document resolution.</param>
    /// <param name="promptTimestamp">The exact timestamp to use when resolving versioned context documents.</param>
    /// <param name="requiredContextDocumentNames">Context documents that must exist at the supplied timestamp.</param>
    /// <param name="optionalContextDocumentNames">Context documents that should be included when available.</param>
    /// <param name="includeJustification">Whether to reconstruct the justification prompt variant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reconstructed prompt inputs.</returns>
    Task<ReconstructedMatchPredictionPrompt> ReconstructMatchPredictionPromptAtTimestampAsync(
        Match match,
        string model,
        string communityContext,
        DateTimeOffset promptTimestamp,
        IReadOnlyList<string> requiredContextDocumentNames,
        IReadOnlyList<string>? optionalContextDocumentNames = null,
        bool includeJustification = false,
        CancellationToken cancellationToken = default);
}
