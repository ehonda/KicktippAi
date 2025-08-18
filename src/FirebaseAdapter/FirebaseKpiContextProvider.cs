using System.Runtime.CompilerServices;
using Core;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firebase-based context provider for KPI documents.
/// Retrieves KPI documents from Firestore for use in bonus predictions.
/// </summary>
public class FirebaseKpiContextProvider
{
    private readonly IKpiRepository _kpiRepository;
    private readonly ILogger<FirebaseKpiContextProvider> _logger;

    public FirebaseKpiContextProvider(IKpiRepository kpiRepository, ILogger<FirebaseKpiContextProvider> logger)
    {
        _kpiRepository = kpiRepository ?? throw new ArgumentNullException(nameof(kpiRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all KPI documents as context for predictions for a specific community.
    /// This method provides all available KPI documents for bonus predictions.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing KPI data.</returns>
    public async IAsyncEnumerable<DocumentContext> GetContextAsync(string communityContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all KPI documents for context in community: {CommunityContext}", communityContext);

        IReadOnlyList<KpiDocument> kpiDocuments;
        try
        {
            kpiDocuments = await _kpiRepository.GetAllKpiDocumentsAsync(communityContext, cancellationToken);
            _logger.LogInformation("Found {DocumentCount} KPI documents for context in community: {CommunityContext}", kpiDocuments.Count, communityContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve KPI documents for context in community: {CommunityContext}", communityContext);
            throw;
        }

        foreach (var kpiDocument in kpiDocuments)
        {
            _logger.LogDebug("Providing KPI document context: {DocumentName}", kpiDocument.DocumentName);
            
            yield return new DocumentContext(
                Name: kpiDocument.DocumentName,
                Content: kpiDocument.Content);
        }
    }

    /// <summary>
    /// Gets KPI context specifically for bonus questions for a specific community.
    /// This is an alias for GetContextAsync() since KPI documents are primarily used for bonus predictions.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing KPI data for bonus questions.</returns>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextByCommunityAsync(string communityContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var context in GetContextAsync(communityContext, cancellationToken))
        {
            yield return context;
        }
    }

    /// <summary>
    /// Gets KPI context specifically tailored for a bonus question based on its content.
    /// Note: This method uses a default community context and may not return the correct documents for multi-community setups.
    /// Consider using the overload with communityContext parameter instead.
    /// </summary>
    /// <param name="questionText">The text of the bonus question to provide context for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing relevant KPI data for the specific question.</returns>
    [Obsolete("Use GetBonusQuestionContextAsync(string questionText, string communityContext, CancellationToken cancellationToken) instead for proper multi-community support")]
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(string questionText, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Using deprecated GetBonusQuestionContextAsync without community context. Consider upgrading to community-aware version.");
        _logger.LogDebug("Retrieving targeted KPI context for question: {QuestionText}", questionText);

        // Always include team data for all bonus questions
        var teamDataDocument = await GetKpiDocumentContextAsync("team-data", "default", cancellationToken);
        if (teamDataDocument != null)
        {
            yield return teamDataDocument;
        }

        // For trainer/manager change questions, also include manager data
        if (IsTrainerChangeQuestion(questionText))
        {
            _logger.LogDebug("Detected trainer/manager change question, including manager data");
            var managerDataDocument = await GetKpiDocumentContextAsync("manager-data", "default", cancellationToken);
            if (managerDataDocument != null)
            {
                yield return managerDataDocument;
            }
        }
        
        // For relegation questions, also include manager data (manager experience affects team performance)
        if (IsRelegationQuestion(questionText))
        {
            _logger.LogDebug("Detected relegation question, including manager data");
            var managerDataDocument = await GetKpiDocumentContextAsync("manager-data", "default", cancellationToken);
            if (managerDataDocument != null)
            {
                yield return managerDataDocument;
            }
        }
    }

    /// <summary>
    /// Gets KPI context specifically tailored for a bonus question based on its content and community.
    /// This method provides targeted context by including additional relevant documents based on question patterns.
    /// </summary>
    /// <param name="questionText">The text of the bonus question to provide context for.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing relevant KPI data for the specific question.</returns>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(string questionText, string communityContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving targeted KPI context for question: {QuestionText} in community: {CommunityContext}", questionText, communityContext);

        // For now, we'll get all documents for the community and filter based on question patterns
        // In the future, we could make GetKpiDocumentContextAsync community-aware too
        
        // Always include team data for all bonus questions
        await foreach (var context in GetContextAsync(communityContext, cancellationToken))
        {
            // Filter for team-data document
            if (context.Name.Contains("team-data", StringComparison.OrdinalIgnoreCase))
            {
                yield return context;
            }
            
            // For trainer/manager change questions, also include manager data
            else if (IsTrainerChangeQuestion(questionText) && context.Name.Contains("manager-data", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Detected trainer/manager change question, including manager data");
                yield return context;
            }
            
            // For relegation questions, also include manager data
            else if (IsRelegationQuestion(questionText) && context.Name.Contains("manager-data", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Detected relegation question, including manager data");
                yield return context;
            }
        }
    }

    /// <summary>
    /// Determines if a bonus question is about trainer/manager changes based on its text.
    /// </summary>
    /// <param name="questionText">The text of the bonus question.</param>
    /// <returns>True if the question is about trainer/manager changes, false otherwise.</returns>
    private static bool IsTrainerChangeQuestion(string questionText)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            return false;

        var lowerText = questionText.ToLowerInvariant();
        
        // Check for German trainer/manager change keywords
        return lowerText.Contains("trainerwechsel") || 
               lowerText.Contains("trainer") ||
               lowerText.Contains("cheftrainer") ||
               lowerText.Contains("entlassung") ||
               lowerText.Contains("entlassen") ||
               lowerText.Contains("manager") ||
               lowerText.Contains("coach");
    }

    /// <summary>
    /// Determines if a bonus question is about relegation based on its text.
    /// </summary>
    /// <param name="questionText">The text of the bonus question.</param>
    /// <returns>True if the question is about relegation, false otherwise.</returns>
    private static bool IsRelegationQuestion(string questionText)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            return false;

        var lowerText = questionText.ToLowerInvariant();
        
        // Check for German relegation keywords
        return lowerText.Contains("16-18") || 
               lowerText.Contains("plätze 16-18") ||
               lowerText.Contains("abstieg") ||
               lowerText.Contains("relegation") ||
               lowerText.Contains("abstiegsplätze") ||
               lowerText.Contains("absteiger");
    }

    /// <summary>
    /// Gets a specific KPI document by its ID.
    /// </summary>
    /// <param name="documentId">The ID of the KPI document to retrieve.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The document context for the specified KPI document, or null if not found.</returns>
    public async Task<DocumentContext?> GetKpiDocumentContextAsync(string documentId, string communityContext, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving specific KPI document: {DocumentId} for community: {CommunityContext}", documentId, communityContext);

        try
        {
            var kpiDocument = await _kpiRepository.GetKpiDocumentAsync(documentId, communityContext, cancellationToken);
            
            if (kpiDocument == null)
            {
                _logger.LogWarning("KPI document not found: {DocumentId} for community: {CommunityContext}", documentId, communityContext);
                return null;
            }

            _logger.LogDebug("Found KPI document: {DocumentId} for community: {CommunityContext}", documentId, communityContext);
            
            return new DocumentContext(
                Name: kpiDocument.DocumentName,
                Content: kpiDocument.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve KPI document: {DocumentId} for community: {CommunityContext}", documentId, communityContext);
            throw;
        }
    }

}
