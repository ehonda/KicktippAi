using NodaTime;

namespace Core;

/// <summary>
/// Repository interface for persisting and retrieving match predictions.
/// Currently designed for Bundesliga 2025/26 season.
/// </summary>
public interface IPredictionRepository
{
    /// <summary>
    /// Saves a prediction for a specific match.
    /// </summary>
    /// <param name="match">The match to predict.</param>
    /// <param name="prediction">The prediction for the match.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="tokenUsage">JSON string containing token usage information from the API.</param>
    /// <param name="cost">The cost in USD for generating the prediction.</param>
    /// <param name="communityContext">The community context (rules) used for the prediction.</param>
    /// <param name="contextDocumentNames">Names of context documents used for this prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SavePredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a prediction for a specific match using the specified model and community context.
    /// </summary>
    /// <param name="match">The match to get the prediction for.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prediction if found, otherwise null.</returns>
    Task<Prediction?> GetPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full prediction metadata for a specific match using the specified model and community context.
    /// Includes context document names and timestamps for outdated checks.
    /// </summary>
    /// <param name="match">The match to get the prediction for.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prediction metadata if found, otherwise null.</returns>
    Task<PredictionMetadata?> GetPredictionMetadataAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a prediction for a specific match by team names, start time, model, and community context.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="startsAt">The match start time.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prediction if found, otherwise null.</returns>
    Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all matches for a specific match day.
    /// </summary>
    /// <param name="matchDay">The match day number (1-34 for Bundesliga).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matches for the specified match day.</returns>
    Task<IReadOnlyList<Match>> GetMatchDayAsync(int matchDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all matches with their predictions for a specific match day using the specified model and community context.
    /// </summary>
    /// <param name="matchDay">The match day number (1-34 for Bundesliga).</param>
    /// <param name="model">The AI model used to generate the predictions.</param>
    /// <param name="communityContext">The community context used for the predictions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matches with their predictions (if any) for the specified match day.</returns>
    Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all predictions made with the specified model and community context.
    /// </summary>
    /// <param name="model">The AI model used to generate the predictions.</param>
    /// <param name="communityContext">The community context used for the predictions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all match predictions for the specified model and community context.</returns>
    Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a prediction exists for a specific match using the specified model and community context.
    /// </summary>
    /// <param name="match">The match to check.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a prediction exists, otherwise false.</returns>
    Task<bool> HasPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a bonus prediction for a specific question.
    /// </summary>
    /// <param name="bonusQuestion">The original bonus question (for text observability).</param>
    /// <param name="bonusPrediction">The bonus prediction to save.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="tokenUsage">JSON string containing token usage information from the API.</param>
    /// <param name="cost">The cost in USD for generating the prediction.</param>
    /// <param name="communityContext">The community context (rules) used for the prediction.</param>
    /// <param name="contextDocumentNames">Names of context documents used for this prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a bonus prediction for a specific question using the specified model and community context.
    /// </summary>
    /// <param name="questionId">The ID of the bonus question.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bonus prediction if found, otherwise null.</returns>
    Task<BonusPrediction?> GetBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a bonus prediction by question text and community context.
    /// This allows lookup for the same question text with different form IDs.
    /// </summary>
    /// <param name="questionText">The text of the bonus question.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bonus prediction if found, otherwise null.</returns>
    Task<BonusPrediction?> GetBonusPredictionByTextAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full bonus prediction metadata by question text and community context.
    /// Includes context document names and timestamps for outdated checks.
    /// </summary>
    /// <param name="questionText">The text of the bonus question.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bonus prediction metadata if found, otherwise null.</returns>
    Task<BonusPredictionMetadata?> GetBonusPredictionMetadataByTextAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all bonus predictions made with the specified model and community context.
    /// </summary>
    /// <param name="model">The AI model used to generate the predictions.</param>
    /// <param name="communityContext">The community context used for the predictions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all bonus predictions for the specified model and community context.</returns>
    Task<IReadOnlyList<BonusPrediction>> GetAllBonusPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a bonus prediction exists for a specific question using the specified model and community context.
    /// </summary>
    /// <param name="questionId">The ID of the bonus question.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a bonus prediction exists, otherwise false.</returns>
    Task<bool> HasBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a match combined with its prediction (if any).
/// </summary>
public record MatchPrediction(
    Match Match,
    Prediction? Prediction);
