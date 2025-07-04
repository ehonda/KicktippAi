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
/// Represents login credentials loaded from environment
/// </summary>
public class KicktippCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
