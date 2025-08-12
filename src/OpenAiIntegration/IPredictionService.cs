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

    /// <summary>
    /// Predicts the answer to a bonus question based on the question and context documents
    /// </summary>
    /// <param name="bonusQuestion">The bonus question to predict</param>
    /// <param name="contextDocuments">Additional context documents to inform the prediction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A prediction for the bonus question, or null if prediction fails</returns>
    Task<BonusPrediction?> PredictBonusQuestionAsync(
        BonusQuestion bonusQuestion,
        IEnumerable<DocumentContext> contextDocuments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file path of the match prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the match prompt file</returns>
    string GetMatchPromptPath();

    /// <summary>
    /// Gets the file path of the bonus question prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the bonus prompt file</returns>
    string GetBonusPromptPath();
}
