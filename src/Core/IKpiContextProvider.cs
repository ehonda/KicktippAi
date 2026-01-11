namespace EHonda.KicktippAi.Core;

/// <summary>
/// Context provider for KPI documents used in bonus predictions.
/// </summary>
public interface IKpiContextProvider
{
    /// <summary>
    /// Gets all KPI documents as context for predictions for a specific community.
    /// </summary>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing KPI data.</returns>
    IAsyncEnumerable<DocumentContext> GetContextAsync(string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets KPI context specifically tailored for a bonus question based on its content and community.
    /// </summary>
    /// <param name="questionText">The text of the bonus question to provide context for.</param>
    /// <param name="communityContext">The community context to filter by.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of document contexts containing relevant KPI data for the specific question.</returns>
    IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(
        string questionText,
        string communityContext,
        CancellationToken cancellationToken = default);
}
