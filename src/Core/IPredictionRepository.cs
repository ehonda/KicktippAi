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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SavePredictionAsync(Match match, Prediction prediction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a prediction for a specific match.
    /// </summary>
    /// <param name="match">The match to get the prediction for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prediction if found, otherwise null.</returns>
    Task<Prediction?> GetPredictionAsync(Match match, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a prediction for a specific match by team names and start time.
    /// </summary>
    /// <param name="homeTeam">The home team name.</param>
    /// <param name="awayTeam">The away team name.</param>
    /// <param name="startsAt">The match start time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prediction if found, otherwise null.</returns>
    Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all matches for a specific match day.
    /// </summary>
    /// <param name="matchDay">The match day number (1-34 for Bundesliga).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matches for the specified match day.</returns>
    Task<IReadOnlyList<Match>> GetMatchDayAsync(int matchDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all matches with their predictions for a specific match day.
    /// </summary>
    /// <param name="matchDay">The match day number (1-34 for Bundesliga).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matches with their predictions (if any) for the specified match day.</returns>
    Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all predictions made so far.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all match predictions.</returns>
    Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a prediction exists for a specific match.
    /// </summary>
    /// <param name="match">The match to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a prediction exists, otherwise false.</returns>
    Task<bool> HasPredictionAsync(Match match, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a match combined with its prediction (if any).
/// </summary>
public record MatchPrediction(
    Match Match,
    Prediction? Prediction);
