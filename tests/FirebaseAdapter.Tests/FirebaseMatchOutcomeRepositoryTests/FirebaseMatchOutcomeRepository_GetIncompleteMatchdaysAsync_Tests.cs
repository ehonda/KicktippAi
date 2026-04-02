using EHonda.KicktippAi.Core;
using NodaTime;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_GetIncompleteMatchdaysAsync_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryTests_Base(fixture)
{
    [Test]
    public async Task GetIncompleteMatchdaysAsync_returns_missing_and_pending_matchdays_up_to_current_matchday()
    {
        var repository = CreateRepository();

        foreach (var matchIndex in Enumerable.Range(1, 9))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateCollectedOutcome($"Team {matchIndex}", $"Opponent {matchIndex}", 1, MatchOutcomeAvailability.Completed, 1, 0),
                "community-a");
        }

        foreach (var matchIndex in Enumerable.Range(1, 8))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateCollectedOutcome($"Club {matchIndex}", $"Rival {matchIndex}", 2, MatchOutcomeAvailability.Completed, 2, 1),
                "community-a");
        }

        await repository.UpsertMatchOutcomeAsync(
            CreateCollectedOutcome("Pending A", "Pending B", 3, MatchOutcomeAvailability.Pending, null, null),
            "community-a");

        var incompleteMatchdays = await repository.GetIncompleteMatchdaysAsync("community-a", 4);

        await Assert.That(incompleteMatchdays).IsEquivalentTo([2, 3, 4]);
    }

    [Test]
    public async Task GetIncompleteMatchdaysAsync_filters_by_community_and_current_matchday()
    {
        var repository = CreateRepository();

        foreach (var matchIndex in Enumerable.Range(1, 9))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateCollectedOutcome($"Team {matchIndex}", $"Opponent {matchIndex}", 1, MatchOutcomeAvailability.Completed, 1, 0),
                "community-a");
            await repository.UpsertMatchOutcomeAsync(
                CreateCollectedOutcome($"Other {matchIndex}", $"Else {matchIndex}", 2, MatchOutcomeAvailability.Completed, 1, 0),
                "community-b");
        }

        var incompleteMatchdays = await repository.GetIncompleteMatchdaysAsync("community-a", 1);

        await Assert.That(incompleteMatchdays).IsEmpty();
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
