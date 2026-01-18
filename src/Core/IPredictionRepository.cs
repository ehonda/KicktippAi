using NodaTime;

namespace EHonda.KicktippAi.Core;

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
    Task SavePredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default);

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
    Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Gets the current reprediction index for a specific match using the specified model and community context.
    /// </summary>
    /// <param name="match">The match to check.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current reprediction index, or -1 if no prediction exists.</returns>
    Task<int> GetMatchRepredictionIndexAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default);

    // ============================================================================
    // CANCELLED MATCH LOOKUPS (Team Names Only - No startsAt Constraint)
    // ============================================================================
    //
    // These methods exist to handle a specific edge case with cancelled matches:
    //
    // PROBLEM:
    // When a match is cancelled ("Abgesagt" on Kicktipp), the match time is no longer
    // displayed. Different Kicktipp pages handle this inconsistently:
    //   - tippabgabe page: Shows multiple matches in a table, allowing time inheritance
    //     from the previous row (e.g., 15:30 from the match above)
    //   - spielinfo pages: Show one match at a time, so there's no "previous row" to
    //     inherit from, resulting in DateTimeOffset.MinValue as fallback
    //
    // This causes the same cancelled match to have different `startsAt` values depending
    // on which page was scraped, leading to database lookup mismatches:
    //   - MatchdayCommand uses spielinfo → startsAt = MinValue
    //   - VerifyCommand uses tippabgabe → startsAt = inherited time (e.g., 15:30)
    //
    // SOLUTION:
    // For cancelled matches ONLY, we query by team names without the startsAt constraint.
    // This finds the prediction regardless of which startsAt value was used when storing.
    // We order by createdAt descending to get the most recent prediction.
    //
    // WHY NOT CHANGE THE NORMAL FLOW:
    // The startsAt constraint is important for non-cancelled matches because:
    //   1. It's part of the natural composite key (teams can play multiple times)
    //   2. It ensures we don't accidentally retrieve predictions for rescheduled matches
    //   3. It maintains data integrity for the vast majority of matches
    //
    // Cancelled matches are rare edge cases where this constraint causes more problems
    // than it solves, so we relax it only for this specific scenario.
    // ============================================================================

    /// <summary>
    /// Retrieves a prediction for a cancelled match by team names only (ignoring startsAt).
    /// <para>
    /// This method should ONLY be used for cancelled matches where the startsAt value
    /// may be inconsistent across different Kicktipp pages. For normal matches, use
    /// <see cref="GetPredictionAsync(Match, string, string, CancellationToken)"/> instead.
    /// </para>
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent prediction if found, otherwise null.</returns>
    Task<Prediction?> GetCancelledMatchPredictionAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves prediction metadata for a cancelled match by team names only (ignoring startsAt).
    /// <para>
    /// This method should ONLY be used for cancelled matches where the startsAt value
    /// may be inconsistent across different Kicktipp pages. For normal matches, use
    /// <see cref="GetPredictionMetadataAsync(Match, string, string, CancellationToken)"/> instead.
    /// </para>
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent prediction metadata if found, otherwise null.</returns>
    Task<PredictionMetadata?> GetCancelledMatchPredictionMetadataAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the reprediction index for a cancelled match by team names only (ignoring startsAt).
    /// <para>
    /// This method should ONLY be used for cancelled matches where the startsAt value
    /// may be inconsistent across different Kicktipp pages. For normal matches, use
    /// <see cref="GetMatchRepredictionIndexAsync(Match, string, string, CancellationToken)"/> instead.
    /// </para>
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current reprediction index, or -1 if no prediction exists.</returns>
    Task<int> GetCancelledMatchRepredictionIndexAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current reprediction index for a specific bonus question using the specified model and community context.
    /// </summary>
    /// <param name="questionText">The text of the bonus question.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="communityContext">The community context used for the prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current reprediction index, or -1 if no prediction exists.</returns>
    Task<int> GetBonusRepredictionIndexAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a repredicted match prediction with the next reprediction index.
    /// </summary>
    /// <param name="match">The match to predict.</param>
    /// <param name="prediction">The prediction for the match.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="tokenUsage">JSON string containing token usage information from the API.</param>
    /// <param name="cost">The cost in USD for generating the prediction.</param>
    /// <param name="communityContext">The community context (rules) used for the prediction.</param>
    /// <param name="contextDocumentNames">Names of context documents used for this prediction.</param>
    /// <param name="repredictionIndex">The reprediction index for this prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveRepredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a repredicted bonus prediction with the next reprediction index.
    /// </summary>
    /// <param name="bonusQuestion">The original bonus question (for text observability).</param>
    /// <param name="bonusPrediction">The bonus prediction to save.</param>
    /// <param name="model">The AI model used to generate the prediction.</param>
    /// <param name="tokenUsage">JSON string containing token usage information from the API.</param>
    /// <param name="cost">The cost in USD for generating the prediction.</param>
    /// <param name="communityContext">The community context (rules) used for the prediction.</param>
    /// <param name="contextDocumentNames">Names of context documents used for this prediction.</param>
    /// <param name="repredictionIndex">The reprediction index for this prediction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveBonusRepredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get match prediction costs and counts grouped by reprediction index for cost analysis.
    /// Used specifically by the cost command to include all repredictions.
    /// </summary>
    /// <param name="model">The AI model used to generate predictions.</param>
    /// <param name="communityContext">The community context used for predictions.</param>
    /// <param name="matchdays">Optional list of matchdays to filter by. If null, all matchdays are included.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping reprediction index to (cost, count) tuple.</returns>
    Task<Dictionary<int, (double cost, int count)>> GetMatchPredictionCostsByRepredictionIndexAsync(string model, string communityContext, List<int>? matchdays = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get bonus prediction costs and counts grouped by reprediction index for cost analysis.
    /// Used specifically by the cost command to include all repredictions.
    /// </summary>
    /// <param name="model">The AI model used to generate predictions.</param>
    /// <param name="communityContext">The community context used for predictions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping reprediction index to (cost, count) tuple.</returns>
    Task<Dictionary<int, (double cost, int count)>> GetBonusPredictionCostsByRepredictionIndexAsync(string model, string communityContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique matchdays that have predictions stored.
    /// Used by the cost command to discover available matchdays when no filter is specified.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A sorted list of matchday numbers.</returns>
    Task<List<int>> GetAvailableMatchdaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique AI models that have predictions stored.
    /// Used by the cost command to discover available models when no filter is specified.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of model names.</returns>
    Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique community contexts that have predictions stored.
    /// Used by the cost command to discover available community contexts when no filter is specified.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of community context names.</returns>
    Task<List<string>> GetAvailableCommunityContextsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a match combined with its prediction (if any).
/// </summary>
public record MatchPrediction(
    Match Match,
    Prediction? Prediction);
