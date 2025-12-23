using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
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

    /// <summary>
    /// Creates a FirebaseContextRepository instance with optional dependency overrides.
    /// </summary>
    /// <param name="logger">Optional logger. Defaults to a new FakeLogger.</param>
    /// <returns>A configured FirebaseContextRepository instance.</returns>
    protected FirebaseContextRepository CreateRepository(
        Option<FakeLogger<FirebaseContextRepository>> logger = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<FirebaseContextRepository>());
        return new FirebaseContextRepository(Fixture.Db, actualLogger);
    }
}
