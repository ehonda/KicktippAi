using NodaTime;

namespace Core;

/// <summary>
/// Represents a match result from a team's recent history
/// </summary>
public record MatchResult(
    string Competition,      // e.g., "KL-WM", "1.BL", "DFB"
    string HomeTeam,
    string AwayTeam,
    int? HomeGoals,          // null if match hasn't been played yet
    int? AwayGoals,          // null if match hasn't been played yet
    MatchOutcome Outcome     // Win, Draw, Loss from the perspective of the team we're tracking
);

/// <summary>
/// Represents the outcome of a match from a specific team's perspective
/// </summary>
public enum MatchOutcome
{
    Win,
    Draw,
    Loss,
    Pending  // For future matches
}

/// <summary>
/// Represents a match with its recent history context for both teams
/// </summary>
public record MatchWithHistory(
    Match Match,
    List<MatchResult> HomeTeamHistory,
    List<MatchResult> AwayTeamHistory);
