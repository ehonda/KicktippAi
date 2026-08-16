using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_MatchdayCompletionPolicy_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryAdditionalCoverageTests_Base(fixture)
{
    [Test]
    public async Task Bundesliga_requires_exactly_nine_completed_distinct_nonblank_tippspiel_ids()
    {
        var repository = CreateRepository();

        await SeedRawOutcomeAsync("doc-1", "duplicate", 1, MatchOutcomeAvailability.Completed);
        await SeedRawOutcomeAsync("doc-2", "duplicate", 1, MatchOutcomeAvailability.Completed);
        foreach (var index in Enumerable.Range(3, 7))
        {
            await SeedRawOutcomeAsync($"doc-{index}", $"tippspiel-{index}", 1, MatchOutcomeAvailability.Completed);
        }

        foreach (var index in Enumerable.Range(1, 10))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Extra Home {index}",
                    awayTeam: $"Extra Away {index}",
                    matchday: 2,
                    tippSpielId: $"extra-{index}"),
                "test-community");
        }

        foreach (var index in Enumerable.Range(1, 8))
        {
            await repository.UpsertMatchOutcomeAsync(
                CreateOutcome(
                    homeTeam: $"Blank Home {index}",
                    awayTeam: $"Blank Away {index}",
                    matchday: 3,
                    tippSpielId: $"blank-{index}"),
                "test-community");
        }

        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(homeTeam: "Blank Home 9", awayTeam: "Blank Away 9", matchday: 3, tippSpielId: " "),
            "test-community");

        var incomplete = await repository.GetIncompleteMatchdaysAsync("test-community", 3);

        await Assert.That(incomplete).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Wm26_remains_variable_but_requires_nonempty_distinct_completed_fixtures()
    {
        var repository = CreateRepository(competition: NullableOption.Some(CompetitionIds.FifaWorldCup2026));
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(matchday: 1, tippSpielId: "wm-1"),
            "test-community");
        await repository.UpsertMatchOutcomeAsync(
            CreateOutcome(
                homeTeam: "Pending Home",
                awayTeam: "Pending Away",
                matchday: 2,
                availability: MatchOutcomeAvailability.Pending,
                homeGoals: null,
                awayGoals: null,
                tippSpielId: "wm-2"),
            "test-community");

        var incomplete = await repository.GetIncompleteMatchdaysAsync("test-community", 3);

        await Assert.That(incomplete).IsEquivalentTo([2, 3]);
    }

    [Test]
    public async Task Unknown_competition_is_rejected_before_a_repository_can_query()
    {
        await Assert.That(() => new FirebaseMatchOutcomeRepository(
                Fixture.Db,
                new FakeLogger<FirebaseMatchOutcomeRepository>(),
                "unknown-competition"))
            .Throws<NotSupportedException>();
    }

    private async Task SeedRawOutcomeAsync(
        string documentId,
        string? tippSpielId,
        int matchday,
        MatchOutcomeAvailability availability)
    {
        await Fixture.Db.Collection("match-outcomes").Document(documentId).SetAsync(new Dictionary<string, object?>
        {
            ["id"] = documentId,
            ["competition"] = CompetitionIds.Bundesliga2026_27,
            ["communityContext"] = "test-community",
            ["homeTeam"] = $"Home {documentId}",
            ["awayTeam"] = $"Away {documentId}",
            ["startsAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            ["matchday"] = matchday,
            ["homeGoals"] = 1,
            ["awayGoals"] = 0,
            ["availability"] = availability.ToString(),
            ["tippSpielId"] = tippSpielId,
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        });
    }
}
