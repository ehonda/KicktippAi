using EHonda.KicktippAi.Core;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_GetIncompleteMatchdaysAsync_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Getting_incomplete_matchdays_returns_all_missing_matchdays_up_to_current_matchday()
    {
        var repository = CreateRepository();

        var incomplete = await repository.GetIncompleteMatchdaysAsync("test-community", 3);

        await Assert.That(incomplete).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Getting_incomplete_matchdays_marks_matchday_as_complete_when_nine_completed_matches_exist()
    {
        var repository = CreateRepository();

        foreach (var index in Enumerable.Range(1, 9))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Team {index:00}",
                    awayTeam: $"Away {index:00}",
                    matchday: 1,
                    availability: MatchOutcomeAvailability.Completed,
                    homeGoals: index,
                    awayGoals: index - 1,
                    tippSpielId: $"1-{index}"),
                "test-community");
        }

        var incomplete = await repository.GetIncompleteMatchdaysAsync("test-community", 1);

        await Assert.That(incomplete).IsEmpty();
    }

    [Test]
    public async Task Getting_incomplete_matchdays_marks_matchday_as_incomplete_when_any_match_is_pending()
    {
        var repository = CreateRepository();

        foreach (var index in Enumerable.Range(1, 8))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Team {index:00}",
                    awayTeam: $"Away {index:00}",
                    matchday: 2,
                    availability: MatchOutcomeAvailability.Completed,
                    homeGoals: index,
                    awayGoals: 0,
                    tippSpielId: $"2-{index}"),
                "test-community");
        }

        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "Pending Home",
                awayTeam: "Pending Away",
                matchday: 2,
                availability: MatchOutcomeAvailability.Pending,
                homeGoals: null,
                awayGoals: null,
                tippSpielId: "2-9"),
            "test-community");

        var incomplete = await repository.GetIncompleteMatchdaysAsync("test-community", 2);

        await Assert.That(incomplete).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task Getting_incomplete_matchdays_ignores_other_communities_and_future_matchdays()
    {
        var repository = CreateRepository();

        foreach (var index in Enumerable.Range(1, 9))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Complete {index:00}",
                    awayTeam: $"Away {index:00}",
                    matchday: 1,
                    availability: MatchOutcomeAvailability.Completed,
                    homeGoals: 1,
                    awayGoals: 0,
                    tippSpielId: $"a-{index}"),
                "community-a");

            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Other {index:00}",
                    awayTeam: $"Away {index:00}",
                    matchday: 3,
                    availability: MatchOutcomeAvailability.Completed,
                    homeGoals: 1,
                    awayGoals: 0,
                    tippSpielId: $"b-{index}"),
                "community-b");
        }

        var incomplete = await repository.GetIncompleteMatchdaysAsync("community-a", 2);

        await Assert.That(incomplete).IsEquivalentTo([2]);
    }
}
