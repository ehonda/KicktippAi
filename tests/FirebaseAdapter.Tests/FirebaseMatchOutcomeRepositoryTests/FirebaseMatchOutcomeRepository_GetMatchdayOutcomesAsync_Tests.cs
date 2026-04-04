using EHonda.KicktippAi.Core;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_GetMatchdayOutcomesAsync_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_matchday_outcomes_returns_ordered_results_for_requested_matchday()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeTeam: "Borussia Dortmund", awayTeam: "Team B", tippSpielId: "2"),
            "test-community");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeTeam: "FC Augsburg", awayTeam: "Team A", tippSpielId: "1"),
            "test-community");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeTeam: "FC Bayern München", awayTeam: "Team C", matchday: 26, tippSpielId: "3"),
            "test-community");

        var outcomes = await repository.GetMatchdayOutcomesAsync(25, "test-community");

        await Assert.That(outcomes).HasCount().EqualTo(2);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("Borussia Dortmund");
        await Assert.That(outcomes[1].HomeTeam).IsEqualTo("FC Augsburg");
    }

    [Test]
    public async Task Getting_matchday_outcomes_filters_by_community()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(CreateOutcome(tippSpielId: "1"), "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeTeam: "RB Leipzig", awayTeam: "Mainz", tippSpielId: "2"),
            "community-b");

        var outcomes = await repository.GetMatchdayOutcomesAsync(25, "community-a");

        await Assert.That(outcomes).HasCount().EqualTo(1);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("FC Bayern München");
    }

    [Test]
    public async Task Getting_matchday_outcomes_returns_empty_when_no_matches_exist()
    {
        var repository = CreateRepository();

        var outcomes = await repository.GetMatchdayOutcomesAsync(25, "test-community");

        await Assert.That(outcomes).IsEmpty();
    }

    [Test]
    public async Task GetMatchdayOutcomesAsync_returns_only_requested_matchday_for_community()
    {
        var repository = CreateRepository();

        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "Team A",
                awayTeam: "Team B",
                matchday: 7,
                availability: MatchOutcomeAvailability.Completed,
                homeGoals: 2,
                awayGoals: 1,
                tippSpielId: "7-Team A-Team B"),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "Team C",
                awayTeam: "Team D",
                matchday: 8,
                availability: MatchOutcomeAvailability.Completed,
                homeGoals: 1,
                awayGoals: 0,
                tippSpielId: "8-Team C-Team D"),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "Team E",
                awayTeam: "Team F",
                matchday: 7,
                availability: MatchOutcomeAvailability.Completed,
                homeGoals: 3,
                awayGoals: 2,
                tippSpielId: "7-Team E-Team F"),
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
            CreateOutcome(
                homeTeam: "Werder Bremen",
                awayTeam: "Team B",
                matchday: 7,
                availability: MatchOutcomeAvailability.Completed,
                homeGoals: 2,
                awayGoals: 1,
                tippSpielId: "7-Werder Bremen-Team B"),
            "community-a");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "FC Augsburg",
                awayTeam: "Team D",
                matchday: 7,
                availability: MatchOutcomeAvailability.Completed,
                homeGoals: 1,
                awayGoals: 0,
                tippSpielId: "7-FC Augsburg-Team D"),
            "community-a");

        var outcomes = await repository.GetMatchdayOutcomesAsync(7, "community-a");

        await Assert.That(outcomes).HasCount().EqualTo(2);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("FC Augsburg");
        await Assert.That(outcomes[1].HomeTeam).IsEqualTo("Werder Bremen");
    }
}
