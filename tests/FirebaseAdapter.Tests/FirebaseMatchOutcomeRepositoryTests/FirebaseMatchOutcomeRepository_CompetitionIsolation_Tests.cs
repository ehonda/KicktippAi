using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

public class FirebaseMatchOutcomeRepository_CompetitionIsolation_Tests(FirestoreFixture fixture)
    : FirebaseMatchOutcomeRepositoryAdditionalCoverageTests_Base(fixture)
{
    [Test]
    public async Task Match_outcome_ids_and_queries_are_partitioned_by_competition_and_community()
    {
        const string communityContext = "shared-community";
        var bundesligaRepository = CreateRepository();
        var worldCupRepository = CreateRepository(
            competition: NullableOption.Some(CompetitionIds.FifaWorldCup2026));
        var outcome = CreateOutcome();

        await Fixture.Db.Collection("match-outcomes")
            .Document(outcome.TippSpielId!)
            .SetAsync(new Dictionary<string, object>
            {
                ["id"] = outcome.TippSpielId!,
                ["homeTeam"] = outcome.HomeTeam,
                ["awayTeam"] = outcome.AwayTeam,
                ["startsAt"] = Timestamp.FromDateTimeOffset(outcome.StartsAt.ToInstant().ToDateTimeOffset()),
                ["matchday"] = outcome.Matchday,
                ["availability"] = nameof(MatchOutcomeAvailability.Completed),
                ["tippSpielId"] = outcome.TippSpielId!,
                ["createdAt"] = Timestamp.GetCurrentTimestamp(),
                ["updatedAt"] = Timestamp.GetCurrentTimestamp(),
                ["communityContext"] = communityContext
            });
        await bundesligaRepository.UpsertMatchOutcomeAsync(outcome, communityContext);
        await worldCupRepository.UpsertMatchOutcomeAsync(outcome, communityContext);

        var bundesligaOutcomes = await bundesligaRepository.GetMatchdayOutcomesAsync(outcome.Matchday, communityContext);
        var worldCupOutcomes = await worldCupRepository.GetMatchdayOutcomesAsync(outcome.Matchday, communityContext);
        var snapshot = await Fixture.Db.Collection("match-outcomes").GetSnapshotAsync();
        var ids = snapshot.Documents.Select(document => document.Id).ToArray();

        await Assert.That(bundesligaOutcomes).Count().IsEqualTo(1);
        await Assert.That(worldCupOutcomes).Count().IsEqualTo(1);
        await Assert.That(ids).Contains($"{CompetitionIds.Bundesliga2026_27}_{communityContext}_{outcome.TippSpielId}");
        await Assert.That(ids).Contains($"{CompetitionIds.FifaWorldCup2026}_{communityContext}_{outcome.TippSpielId}");
        await Assert.That(ids).Contains(outcome.TippSpielId!);
    }
}
