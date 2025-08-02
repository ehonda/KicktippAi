using Core;

namespace KicktippIntegration;

/// <summary>
/// Interface for the Kicktipp client responsible for interacting with kicktipp.de
/// Authentication is handled automatically via dependency injection
/// </summary>
public interface IKicktippClient
{
    /// <summary>
    /// Get open predictions for a specific community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <returns>List of matches with open predictions</returns>
    Task<List<Match>> GetOpenPredictionsAsync(string community);

    /// <summary>
    /// Place a bet for a specific match in a community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <param name="match">The match to bet on</param>
    /// <param name="prediction">The bet prediction</param>
    /// <param name="overrideBet">If true, overrides an existing bet for this match</param>
    /// <returns>True if the bet was placed successfully</returns>
    Task<bool> PlaceBetAsync(string community, Match match, BetPrediction prediction, bool overrideBet = false);

    /// <summary>
    /// Place multiple bets for matches in a community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <param name="bets">Dictionary of matches and their corresponding predictions</param>
    /// <param name="overrideBets">If true, overrides existing bets for the matches</param>
    /// <returns>True if all bets were placed successfully</returns>
    Task<bool> PlaceBetsAsync(string community, Dictionary<Match, BetPrediction> bets, bool overrideBets = false);

    /// <summary>
    /// Get the current standings (league table) for a specific community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <returns>List of team standings ordered by position</returns>
    Task<List<TeamStanding>> GetStandingsAsync(string community);

    /// <summary>
    /// Get matches with detailed information including recent history for both teams
    /// </summary>
    /// <param name="community">The community name</param>
    /// <returns>List of matches with their recent history context</returns>
    Task<List<MatchWithHistory>> GetMatchesWithHistoryAsync(string community);

    /// <summary>
    /// Get placed predictions for the current matchday
    /// </summary>
    /// <param name="community">The community name</param>
    /// <returns>Dictionary of matches and their placed predictions</returns>
    Task<Dictionary<Match, BetPrediction?>> GetPlacedPredictionsAsync(string community);

    /// <summary>
    /// Get open bonus questions for a specific community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <returns>List of bonus questions with open predictions</returns>
    Task<List<BonusQuestion>> GetOpenBonusQuestionsAsync(string community);

    /// <summary>
    /// Place bonus predictions for a community
    /// </summary>
    /// <param name="community">The community name</param>
    /// <param name="predictions">Dictionary of bonus question IDs and their corresponding predictions</param>
    /// <param name="overridePredictions">If true, overrides existing predictions for the questions</param>
    /// <returns>True if all predictions were placed successfully</returns>
    Task<bool> PlaceBonusPredictionsAsync(string community, Dictionary<string, BonusPrediction> predictions, bool overridePredictions = false);
}

/// <summary>
/// Represents login credentials for kicktipp.de
/// Used for dependency injection configuration
/// </summary>
public record KicktippCredentials(string Username, string Password)
{
    public bool IsValid => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}

/// <summary>
/// Configuration class for Kicktipp credentials
/// Used with IOptions pattern for dependency injection
/// </summary>
public class KicktippOptions
{
    public const string ConfigurationSectionName = "Kicktipp";
    
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
    public KicktippCredentials ToCredentials() => new(Username, Password);
}

/// <summary>
/// Represents a bet prediction with home and away goals
/// </summary>
public record BetPrediction(int HomeGoals, int AwayGoals)
{
    public override string ToString() => $"{HomeGoals}:{AwayGoals}";
}
