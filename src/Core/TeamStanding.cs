namespace Core;

/// <summary>
/// Represents a team's standing in a league table
/// </summary>
public record TeamStanding(
    int Position,
    string TeamName,
    int GamesPlayed,
    int Points,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Wins,
    int Draws,
    int Losses)
{
    /// <summary>
    /// Goals formatted as "for:against" (e.g., "15:8")
    /// </summary>
    public string GoalsFormatted => $"{GoalsFor}:{GoalsAgainst}";
}
