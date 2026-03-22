using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using NodaTime;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseMatchOutcomeRepositoryTests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.MatchOutcomesParallelKey)]
public abstract class FirebaseMatchOutcomeRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    [Before(Test)]
    public async Task ClearMatchOutcomesAsync()
    {
        await Fixture.ClearMatchOutcomesAsync();
    }

    protected FirebaseMatchOutcomeRepository CreateRepository(
        NullableOption<FirestoreDb> firestoreDb = default,
        NullableOption<FakeLogger<FirebaseMatchOutcomeRepository>> logger = default)
    {
        var actualDb = firestoreDb.Or(() => Fixture.Db);
        var actualLogger = logger.Or(() => new FakeLogger<FirebaseMatchOutcomeRepository>());
        return new FirebaseMatchOutcomeRepository(actualDb!, actualLogger!);
    }

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
