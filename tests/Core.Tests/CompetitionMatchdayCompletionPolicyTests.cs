using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class CompetitionMatchdayCompletionPolicyTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    [Arguments(7)]
    [Arguments(8)]
    public async Task Bundesliga_is_incomplete_with_zero_through_eight_completed_fixtures(int count)
    {
        var policy = CompetitionMatchdayCompletionPolicies.Get(CompetitionIds.Bundesliga2026_27);

        var isComplete = policy.IsComplete(CreateFixtures(count));

        await Assert.That(isComplete).IsFalse();
    }

    [Test]
    public async Task Bundesliga_is_complete_with_exactly_nine_distinct_completed_fixtures()
    {
        var policy = CompetitionMatchdayCompletionPolicies.Get(CompetitionIds.Bundesliga2026_27);

        var isComplete = policy.IsComplete(CreateFixtures(9));

        await Assert.That(policy.ExpectedMatchesPerMatchday).IsEqualTo(9);
        await Assert.That(isComplete).IsTrue();
    }

    [Test]
    public async Task Bundesliga_is_incomplete_with_a_pending_duplicate_extra_or_blank_fixture()
    {
        var policy = CompetitionMatchdayCompletionPolicies.Get(CompetitionIds.Bundesliga2026_27);
        var pending = CreateFixtures(9).ToArray();
        pending[8] = pending[8] with { Availability = MatchOutcomeAvailability.Pending };
        var duplicate = CreateFixtures(9).ToArray();
        duplicate[8] = duplicate[8] with { TippSpielId = duplicate[0].TippSpielId };
        var extra = CreateFixtures(10);

        await Assert.That(policy.IsComplete(pending)).IsFalse();
        await Assert.That(policy.IsComplete(duplicate)).IsFalse();
        await Assert.That(policy.IsComplete(extra)).IsFalse();

        foreach (var blankId in new string?[] { null, string.Empty, " " })
        {
            var blank = CreateFixtures(9).ToArray();
            blank[8] = blank[8] with { TippSpielId = blankId };
            await Assert.That(policy.IsComplete(blank)).IsFalse();
        }
    }

    [Test]
    public async Task Wm26_accepts_any_nonempty_distinct_all_completed_matchday()
    {
        var policy = CompetitionMatchdayCompletionPolicies.Get(CompetitionIds.FifaWorldCup2026);

        await Assert.That(policy.ExpectedMatchesPerMatchday).IsNull();
        await Assert.That(policy.IsComplete([])).IsFalse();
        await Assert.That(policy.IsComplete(CreateFixtures(1))).IsTrue();
        await Assert.That(policy.IsComplete(CreateFixtures(8))).IsTrue();

        var pending = CreateFixtures(2).ToArray();
        pending[1] = pending[1] with { Availability = MatchOutcomeAvailability.Pending };
        var duplicate = CreateFixtures(2).ToArray();
        duplicate[1] = duplicate[1] with { TippSpielId = duplicate[0].TippSpielId };
        var blank = CreateFixtures(1).ToArray();
        blank[0] = blank[0] with { TippSpielId = " " };

        await Assert.That(policy.IsComplete(pending)).IsFalse();
        await Assert.That(policy.IsComplete(duplicate)).IsFalse();
        await Assert.That(policy.IsComplete(blank)).IsFalse();
    }

    [Test]
    public async Task Unknown_competitions_fail_fast()
    {
        await Assert.That(() => CompetitionMatchdayCompletionPolicies.Get("unknown-competition"))
            .Throws<NotSupportedException>()
            .WithMessageContaining("does not define a matchday completion policy");
        await Assert.That(() => CompetitionMatchdayCompletionPolicies.Get(CompetitionIds.Bundesliga2025_26))
            .Throws<NotSupportedException>();
    }

    private static IReadOnlyList<MatchdayCompletionFixture> CreateFixtures(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new MatchdayCompletionFixture(
                $"tippspiel-{index}",
                MatchOutcomeAvailability.Completed))
            .ToArray();
    }
}
