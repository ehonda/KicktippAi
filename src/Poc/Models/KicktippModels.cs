namespace KicktippAi.Poc.Models;

/// <summary>
/// Represents a football match with betting odds, based on the Python Match class from kicktipp-cli
/// </summary>
public class Match
{
    public string HomeTeam { get; set; } = string.Empty;
    public string RoadTeam { get; set; } = string.Empty;
    public DateTimeOffset? MatchDate { get; set; }

    public override string ToString()
    {
        var dateStr = MatchDate?.ToString("dd.MM.yyyy HH:mm") ?? "TBD";
        return $"{dateStr} '{HomeTeam}' vs. '{RoadTeam}'";
    }
}

/// <summary>
/// Represents a bet prediction with home and away goals
/// </summary>
public class BetPrediction
{
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }

    public override string ToString()
    {
        return $"{HomeGoals}:{AwayGoals}";
    }
}

/// <summary>
/// Simple predictor for generating random bets, inspired by Python SimplePredictor
/// </summary>
public class SimplePredictor
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Generate a simple random prediction for a match
    /// </summary>
    public BetPrediction Predict(Match match)
    {
        // Simple random logic: most common football scores
        var possibleScores = new[]
        {
            new BetPrediction { HomeGoals = 1, AwayGoals = 0 },
            new BetPrediction { HomeGoals = 2, AwayGoals = 0 },
            new BetPrediction { HomeGoals = 2, AwayGoals = 1 },
            new BetPrediction { HomeGoals = 1, AwayGoals = 1 },
            new BetPrediction { HomeGoals = 3, AwayGoals = 1 },
            new BetPrediction { HomeGoals = 0, AwayGoals = 1 },
            new BetPrediction { HomeGoals = 0, AwayGoals = 2 },
            new BetPrediction { HomeGoals = 1, AwayGoals = 2 },
            new BetPrediction { HomeGoals = 3, AwayGoals = 0 },
            new BetPrediction { HomeGoals = 0, AwayGoals = 0 }
        };
        
        return possibleScores[_random.Next(possibleScores.Length)];
    }
}

/// <summary>
/// Represents login credentials loaded from environment
/// </summary>
public class KicktippCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
