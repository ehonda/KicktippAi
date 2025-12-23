using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Base class for FirebaseKpiRepository integration tests.
/// Uses a Firestore emulator container shared per test class.
/// Tests run sequentially to ensure data isolation via ClearDataAsync.
/// </summary>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.PerClass)]
[NotInParallel("FirestoreEmulator")]
public abstract class FirebaseKpiRepositoryTests_Base(FirestoreFixture fixture)
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
    /// Creates a FirebaseKpiRepository instance with optional dependency overrides.
    /// </summary>
    /// <param name="logger">Optional logger. Defaults to a new FakeLogger.</param>
    /// <param name="competition">Optional competition string. Defaults to "bundesliga-2025-26".</param>
    /// <returns>A configured FirebaseKpiRepository instance.</returns>
    protected FirebaseKpiRepository CreateRepository(
        Option<FakeLogger<FirebaseKpiRepository>> logger = default,
        Option<string> competition = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<FirebaseKpiRepository>());
        var actualCompetition = competition.Or("bundesliga-2025-26");
        return new FirebaseKpiRepository(Fixture.Db, actualLogger, actualCompetition);
    }
}
