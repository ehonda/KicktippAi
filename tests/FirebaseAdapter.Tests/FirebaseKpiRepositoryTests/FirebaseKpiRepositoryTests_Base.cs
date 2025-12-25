using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseKpiRepositoryTests;

/// <summary>
/// Base class for <see cref="FirebaseKpiRepository"/> integration tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fixture Sharing:</b> Uses a single shared Firestore emulator container across all test classes
/// via <c>SharedType.Keyed</c>. This dramatically reduces test execution time compared to
/// creating a container per test class.
/// </para>
/// <para>
/// <b>Collection Isolation:</b> Tests in this class operate on the <c>kpi-documents</c> collection.
/// They run sequentially within this group (via <see cref="FirestoreFixture.KpiParallelKey"/>)
/// but can run <b>in parallel</b> with tests from other collection groups (ContextDocuments, Predictions).
/// </para>
/// <para>
/// <b>Test Isolation:</b> <see cref="ClearKpiDocumentsAsync"/> is called before each test
/// to ensure a clean state. This clears only the <c>kpi-documents</c> collection,
/// preserving data in other collections for parallel test groups.
/// </para>
/// <para>
/// See <c>.github/instructions/firebase-adapter-tests.instructions.md</c> for complete documentation.
/// </para>
/// </remarks>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.KpiParallelKey)]
public abstract class FirebaseKpiRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    /// <summary>
    /// Clears the <c>kpi-documents</c> collection before each test to ensure isolation.
    /// </summary>
    /// <remarks>
    /// Uses collection-specific clearing to enable parallel execution with other collection groups.
    /// Do NOT change this to <see cref="FirestoreFixture.ClearDataAsync"/> as it would break parallelization.
    /// </remarks>
    [Before(Test)]
    public async Task ClearKpiDocumentsAsync()
    {
        await Fixture.ClearKpiDocumentsAsync();
    }

    /// <summary>
    /// Creates a FirebaseKpiRepository instance with optional dependency overrides.
    /// </summary>
    /// <param name="firestoreDb">Optional FirestoreDb. Defaults to the fixture's Db. Use NullableOption to pass null for null guard tests.</param>
    /// <param name="logger">Optional logger. Defaults to a new FakeLogger. Use NullableOption to pass null for null guard tests.</param>
    /// <param name="competition">Optional competition string. Defaults to "bundesliga-2025-26".</param>
    /// <returns>A configured FirebaseKpiRepository instance.</returns>
    protected FirebaseKpiRepository CreateRepository(
        NullableOption<FirestoreDb> firestoreDb = default,
        NullableOption<FakeLogger<FirebaseKpiRepository>> logger = default,
        Option<string> competition = default)
    {
        var actualDb = firestoreDb.Or(() => Fixture.Db);
        var actualLogger = logger.Or(() => new FakeLogger<FirebaseKpiRepository>());
        var actualCompetition = competition.Or("bundesliga-2025-26");
        return new FirebaseKpiRepository(actualDb!, actualLogger!, actualCompetition);
    }
}
