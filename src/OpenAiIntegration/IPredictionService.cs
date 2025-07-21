using Core;

namespace OpenAiIntegration;

/// <summary>
/// Service for predicting match outcomes using AI models
/// </summary>
public interface IPredictionService
{
    /// <summary>
    /// Predicts the outcome of a match based on the match data and context documents
    /// </summary>
    /// <param name="match">The match to predict</param>
    /// <param name="contextDocuments">Additional context documents to inform the prediction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A prediction containing the predicted goals for both teams, or null if prediction fails</returns>
    Task<Prediction?> PredictMatchAsync(
        Match match, 
        IEnumerable<DocumentContext> contextDocuments, 
        CancellationToken cancellationToken = default);
}
