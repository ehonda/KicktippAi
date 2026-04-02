using EHonda.KicktippAi.Core;
using NodaTime;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_GetMatchdayOutcomesAsync_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task GetMatchdayOutcomesAsync_returns_only_requested_matchday_for_community()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("Team A", "Team B", 7, MatchOutcomeAvailability.Completed, 2, 1),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("Team C", "Team D", 8, MatchOutcomeAvailability.Completed, 1, 0),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("Team E", "Team F", 7, MatchOutcomeAvailability.Completed, 3, 2),
            "community-b");

        var outcomes = await repository.GetMatchdayOutcomesAsync(7, "community-a");

        await Assert.That(outcomes).HasCount().EqualTo(1);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("Team A");
        await Assert.That(outcomes[0].AwayTeam).IsEqualTo("Team B");
        await Assert.That(outcomes[0].Matchday).IsEqualTo(7);
    }

    [Test]
    public async Task GetMatchdayOutcomesAsync_returns_results_sorted_by_home_team()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("Werder Bremen", "Team B", 7, MatchOutcomeAvailability.Completed, 2, 1),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("FC Augsburg", "Team D", 7, MatchOutcomeAvailability.Completed, 1, 0),
            "community-a");

        var outcomes = await repository.GetMatchdayOutcomesAsync(7, "community-a");

        await Assert.That(outcomes).HasCount().EqualTo(2);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("FC Augsburg");
        await Assert.That(outcomes[1].HomeTeam).IsEqualTo("Werder Bremen");
    }

    private static CollectedMatchOutcome CreateCollectedOutcome(
        string homeTeam,
        string awayTeam,
        int matchday,
        MatchOutcomeAvailability availability,
        int? homeGoals,
        int? awayGoals)
    {
        return new CollectedMatchOutcome(
            homeTeam,
            awayTeam,
            Instant.FromUtc(2026, 2, 15, 14, 30).InUtc(),
            matchday,
            homeGoals,
            awayGoals,
            availability,
            $"{matchday}-{homeTeam}-{awayTeam}");
    }
}
