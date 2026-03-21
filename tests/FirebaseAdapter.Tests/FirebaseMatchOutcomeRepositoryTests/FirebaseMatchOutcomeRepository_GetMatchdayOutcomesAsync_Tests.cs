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
        await Assert.That(outcomes.Select(outcome => outcome.HomeTeam).ToArray())
            .IsEquivalentTo(["FC Augsburg", "Borussia Dortmund"]);
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
}
