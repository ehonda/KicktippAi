using NodaTime;

namespace Core;

/// <summary>
/// Represents a head-to-head match result with detailed breakdown for CSV export
/// </summary>
public record HeadToHeadResult(
    string League,       // e.g., "1.BL 2024/25", "DFB 2016/17"
    string Matchday,     // e.g., "27. Spieltag", "2. Runde"
    string PlayedAt,     // Date when the match was played (if available)
    string HomeTeam,     // Home team name
    string AwayTeam,     // Away team name
    string Score,        // Final score, e.g., "0:1", "1:3"
    string? Annotation = null // e.g., "nach Elfmeterschießen", "nach Verlängerung"
);
