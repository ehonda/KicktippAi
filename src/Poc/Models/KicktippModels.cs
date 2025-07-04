namespace KicktippAi.Poc.Models;

/// <summary>
/// Represents a football match with betting odds, based on the Python Match class from kicktipp-cli
/// </summary>
public class Match
{
    public string HomeTeam { get; set; } = string.Empty;
    public string RoadTeam { get; set; } = string.Empty;
    public DateTime? MatchDate { get; set; }
    public decimal RateHome { get; set; }
    public decimal RateDeuce { get; set; }
    public decimal RateRoad { get; set; }

    public (decimal Home, decimal Deuce, decimal Road) Odds => (RateHome, RateDeuce, RateRoad);

    public override string ToString()
    {
        var dateStr = MatchDate?.ToString("dd.MM.yyyy HH:mm") ?? "TBD";
        return $"{dateStr} '{HomeTeam}' vs. '{RoadTeam}' ({RateHome:F2};{RateDeuce:F2};{RateRoad:F2})";
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
