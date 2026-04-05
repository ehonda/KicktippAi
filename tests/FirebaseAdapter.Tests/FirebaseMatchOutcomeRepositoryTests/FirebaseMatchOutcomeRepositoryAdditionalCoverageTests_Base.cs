using EHonda.KicktippAi.Core;
using NodaTime;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public abstract class FirebaseMatchOutcomeRepositoryAdditionalCoverageTests_Base(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    protected static CollectedMatchOutcome CreateOutcome(
        string homeTeam = "FC Bayern München",
        string awayTeam = "Borussia Dortmund",
        int matchday = 25,
        MatchOutcomeAvailability availability = MatchOutcomeAvailability.Completed,
        int? homeGoals = 2,
        int? awayGoals = 1,
        string? tippSpielId = "tippspiel-1")
    {
        return new CollectedMatchOutcome(
            homeTeam,
            awayTeam,
            Instant.FromUtc(2025, 3, 15, 15, 30).InUtc(),
            matchday,
            homeGoals,
            awayGoals,
            availability,
            tippSpielId);
    }
}
