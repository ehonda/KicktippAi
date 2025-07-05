using Core;

namespace KicktippIntegration;

/// <summary>
/// Interface for the Kicktipp client responsible for interacting with kicktipp.de
/// </summary>
public interface IKicktippClient
{
    /// <summary>
    /// Attempt to login to kicktipp.de with provided credentials
    /// </summary>
    /// <param name="credentials">Username and password</param>
    /// <returns>True if login was successful</returns>
    Task<bool> LoginAsync(KicktippCredentials credentials);

    /// <summary>
    /// Extract and return the login token for future use
    /// </summary>
    /// <returns>The login token or null if not available</returns>
    Task<KicktippLoginToken?> GetLoginTokenAsync();

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
}

/// <summary>
/// Represents login credentials for kicktipp.de
/// </summary>
public record KicktippCredentials(string Username, string Password)
{
    public bool IsValid => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}

/// <summary>
/// Represents a kicktipp.de login token for authenticated requests
/// </summary>
public record KicktippLoginToken(string Token, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsValid => !string.IsNullOrEmpty(Token) && !IsExpired;
}

/// <summary>
/// Represents a bet prediction with home and away goals
/// </summary>
public record BetPrediction(int HomeGoals, int AwayGoals)
{
    public override string ToString() => $"{HomeGoals}:{AwayGoals}";
}
