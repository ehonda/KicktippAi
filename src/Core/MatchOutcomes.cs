using NodaTime;

namespace EHonda.KicktippAi.Core;

public enum MatchOutcomeAvailability
{
    Pending,
    Completed
}

public enum MatchOutcomeWriteDisposition
{
    Created,
    Updated,
    Unchanged
}

public record CollectedMatchOutcome(
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt,
    int Matchday,
    int? HomeGoals,
    int? AwayGoals,
    MatchOutcomeAvailability Availability,
    string? TippSpielId = null)
{
    public bool HasOutcome => Availability == MatchOutcomeAvailability.Completed;
}

public record PersistedMatchOutcome(
    string CommunityContext,
    string Competition,
    string HomeTeam,
    string AwayTeam,
    ZonedDateTime StartsAt,
    int Matchday,
    int? HomeGoals,
    int? AwayGoals,
    MatchOutcomeAvailability Availability,
    string? TippSpielId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool HasOutcome => Availability == MatchOutcomeAvailability.Completed;
}

public record MatchOutcomeUpsertResult(
    MatchOutcomeWriteDisposition Disposition,
    PersistedMatchOutcome Outcome);
