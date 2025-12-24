using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

/// <summary>
/// Base class for <see cref="FirebasePredictionRepository"/> integration tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fixture Sharing:</b> Uses a single shared Firestore emulator container across all test classes
/// via <c>SharedType.Keyed</c>. This dramatically reduces test execution time compared to
/// creating a container per test class.
/// </para>
/// <para>
/// <b>Collection Isolation:</b> Tests in this class operate on the prediction-related collections:
/// <c>match-predictions</c>, <c>matches</c>, and <c>bonus-predictions</c>.
/// They run sequentially within this group (via <see cref="FirestoreFixture.PredictionsParallelKey"/>)
/// but can run <b>in parallel</b> with tests from other collection groups (ContextDocuments, Kpi).
/// </para>
/// <para>
/// <b>Test Isolation:</b> <see cref="ClearPredictionsAsync"/> is called before each test
/// to ensure a clean state. This clears only the prediction-related collections,
/// preserving data in other collections for parallel test groups.
/// </para>
/// <para>
/// See <c>.github/instructions/firebase-adapter-tests.instructions.md</c> for complete documentation.
/// </para>
/// </remarks>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.PredictionsParallelKey)]
public abstract class FirebasePredictionRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    /// <summary>
    /// Clears the prediction-related collections before each test to ensure isolation.
    /// </summary>
    /// <remarks>
    /// Clears <c>match-predictions</c>, <c>matches</c>, and <c>bonus-predictions</c> collections.
    /// Uses collection-specific clearing to enable parallel execution with other collection groups.
    /// Do NOT change this to <see cref="FirestoreFixture.ClearDataAsync"/> as it would break parallelization.
    /// </remarks>
    [Before(Test)]
    public async Task ClearPredictionsAsync()
    {
        await Fixture.ClearPredictionsAsync();
    }

    /// <summary>
    /// Creates a FirebasePredictionRepository instance with optional dependency overrides.
    /// </summary>
    /// <param name="logger">Optional logger. Defaults to a new FakeLogger.</param>
    /// <returns>A configured FirebasePredictionRepository instance.</returns>
    protected FirebasePredictionRepository CreateRepository(
        Option<FakeLogger<FirebasePredictionRepository>> logger = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<FirebasePredictionRepository>());
        return new FirebasePredictionRepository(Fixture.Db, actualLogger);
    }
}
