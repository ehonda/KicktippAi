using NodaTime;

namespace EHonda.KicktippAi.Core;

/// <summary>
/// Represents a scheduled match between two teams.
/// </summary>
/// <param name="HomeTeam">The name of the home team.</param>
/// <param name="AwayTeam">The name of the away team.</param>
/// <param name="StartsAt">
/// The scheduled start time of the match.
/// <para>
/// <b>Important:</b> For cancelled matches (<see cref="IsCancelled"/> = true), this value is inherited from 
/// the previous match in the time slot order. This preserves database key consistency since the 
/// composite key (HomeTeam, AwayTeam, StartsAt, ...) must remain stable. See the cancellation handling
/// documentation at <c>docs/features/cancelled-matches.md</c> for details.
/// </para>
/// </param>
/// <param name="Matchday">The matchday number (e.g., 1-34 for Bundesliga).</param>
/// <param name="IsCancelled">
/// Indicates whether the match has been cancelled (German: "Abgesagt").
/// <para>
/// When true, the match appears on the Kicktipp page with "Abgesagt" instead of a date/time.
/// Cancelled matches can still receive predictions, but their <see cref="StartsAt"/> value 
/// is inherited from the previous match slot (not a valid scheduled time).
/// </para>
/// <para>
/// <b>Design Decision:</b> We continue processing cancelled matches rather than skipping them because:
/// (1) users can still place predictions on Kicktipp, and (2) when rescheduled, we want to have
/// a prediction in place. The old prediction will carry over when the match gets a new time slot.
/// See <c>docs/features/cancelled-matches.md</c> for complete rationale.
/// </para>
/// </param>
public record Match(
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt,
    int Matchday,
    bool IsCancelled = false);
