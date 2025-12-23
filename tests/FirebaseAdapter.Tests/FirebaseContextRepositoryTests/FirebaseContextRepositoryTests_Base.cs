using FirebaseAdapter.Tests.Fixtures;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Base class for FirebaseContextRepository integration tests.
/// Uses a Firestore emulator container shared per test class.
/// Tests run sequentially to ensure data isolation via ClearDataAsync.
/// </summary>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.PerClass)]
[NotInParallel("FirestoreEmulator")]
public abstract class FirebaseContextRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    /// <summary>
    /// Clears all data from the emulator before each test to ensure isolation.
    /// </summary>
    [Before(Test)]
    public async Task ClearTestData()
    {
        await Fixture.ClearDataAsync();
    }
}
