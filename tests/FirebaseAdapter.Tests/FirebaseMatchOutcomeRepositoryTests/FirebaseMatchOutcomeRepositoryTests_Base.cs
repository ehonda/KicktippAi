using EHonda.Optional.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
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
}
