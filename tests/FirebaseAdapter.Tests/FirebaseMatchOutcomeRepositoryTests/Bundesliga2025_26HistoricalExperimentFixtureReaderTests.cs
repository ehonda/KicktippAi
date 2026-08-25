using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.MatchOutcomesParallelKey)]
public sealed class Bundesliga2025_26HistoricalExperimentFixtureReaderTests(FirestoreFixture fixture)
{
    private const string Community = "pes-squad";

    [Before(Test)]
    public async Task ClearMatchOutcomesAsync()
    {
        await fixture.ClearMatchOutcomesAsync();
    }

    [Test]
    public async Task Read_returns_only_completed_canonical_legacy_fixtures()
    {
        await SeedAsync("102", "102", "Borussia Dortmund", MatchOutcomeAvailability.Completed, 2, 1);
        await SeedAsync("101", "101", "FC Augsburg", MatchOutcomeAvailability.Completed, 1, 0);
        await SeedAsync("103", "103", "FC Bayern München", MatchOutcomeAvailability.Pending, null, null);

        var outcomes = await CreateReader().GetCompletedMatchdayFixturesAsync(7, Community);

        await Assert.That(outcomes).HasCount().EqualTo(2);
        await Assert.That(outcomes.Select(outcome => outcome.HomeTeam).ToArray())
            .IsEquivalentTo(["Borussia Dortmund", "FC Augsburg"]);
        await Assert.That(outcomes.All(outcome => outcome.HasOutcome
                                                  && outcome.HomeGoals is not null
                                                  && outcome.AwayGoals is not null
                                                  && outcome.Competition == CompetitionIds.Bundesliga2025_26
                                                  && outcome.CommunityContext == Community)).IsTrue();
    }

    [Test]
    public async Task Read_rejects_nonlegacy_document_identity()
    {
        await SeedAsync(
            $"{CompetitionIds.Bundesliga2025_26}_{Community}_101",
            "101",
            "FC Augsburg",
            MatchOutcomeAvailability.Completed,
            1,
            0);

        await Assert.That(() => CreateReader().GetCompletedMatchdayFixturesAsync(7, Community))
            .Throws<InvalidDataException>()
            .WithMessageContaining("legacy identity");
    }

    [Test]
    public async Task Read_rejects_completed_fixture_without_score()
    {
        await SeedAsync("101", "101", "FC Augsburg", MatchOutcomeAvailability.Completed, null, 0);

        await Assert.That(() => CreateReader().GetCompletedMatchdayFixturesAsync(7, Community))
            .Throws<InvalidDataException>()
            .WithMessageContaining("missing its score");
    }

    [Test]
    public async Task Reader_contract_exposes_no_write_operations()
    {
        var publicMethods = typeof(IHistoricalExperimentFixtureReader).GetMethods();

        await Assert.That(publicMethods.All(method => method.Name.StartsWith("Get", StringComparison.Ordinal))).IsTrue();
    }

    private Bundesliga2025_26HistoricalExperimentFixtureReader CreateReader() =>
        new(fixture.Db, new FakeLogger<Bundesliga2025_26HistoricalExperimentFixtureReader>());

    private async Task SeedAsync(
        string documentId,
        string tippSpielId,
        string homeTeam,
        MatchOutcomeAvailability availability,
        int? homeGoals,
        int? awayGoals)
    {
        var now = DateTimeOffset.UtcNow;
        await fixture.Db.Collection("match-outcomes").Document(documentId).SetAsync(new Dictionary<string, object?>
        {
            ["homeTeam"] = homeTeam,
            ["awayTeam"] = "VfB Stuttgart",
            ["startsAt"] = Timestamp.FromDateTimeOffset(new DateTimeOffset(2026, 3, 1, 18, 30, 0, TimeSpan.Zero)),
            ["matchday"] = 7,
            ["homeGoals"] = homeGoals,
            ["awayGoals"] = awayGoals,
            ["availability"] = availability.ToString(),
            ["tippspielId"] = tippSpielId,
            ["createdAt"] = Timestamp.FromDateTimeOffset(now.AddDays(-1)),
            ["updatedAt"] = Timestamp.FromDateTimeOffset(now),
            ["competition"] = CompetitionIds.Bundesliga2025_26,
            ["communityContext"] = Community
        });
    }
}
