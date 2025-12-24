using EHonda.Optional.Core;
using FirebaseAdapter.Tests.Fixtures;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

/// <summary>
/// Base class for <see cref="FirebaseContextRepository"/> integration tests.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fixture Sharing:</b> Uses a single shared Firestore emulator container across all test classes
/// via <c>SharedType.Keyed</c>. This dramatically reduces test execution time compared to
/// creating a container per test class.
/// </para>
/// <para>
/// <b>Collection Isolation:</b> Tests in this class operate on the <c>context-documents</c> collection.
/// They run sequentially within this group (via <see cref="FirestoreFixture.ContextDocumentsParallelKey"/>)
/// but can run <b>in parallel</b> with tests from other collection groups (Predictions, Kpi).
/// </para>
/// <para>
/// <b>Test Isolation:</b> <see cref="ClearContextDocumentsAsync"/> is called before each test
/// to ensure a clean state. This clears only the <c>context-documents</c> collection,
/// preserving data in other collections for parallel test groups.
/// </para>
/// <para>
/// See <c>.github/instructions/firebase-adapter-tests.instructions.md</c> for complete documentation.
/// </para>
/// </remarks>
[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.ContextDocumentsParallelKey)]
public abstract class FirebaseContextRepositoryTests_Base(FirestoreFixture fixture)
{
    protected FirestoreFixture Fixture { get; } = fixture;

    /// <summary>
    /// Clears the <c>context-documents</c> collection before each test to ensure isolation.
    /// </summary>
    /// <remarks>
    /// Uses collection-specific clearing to enable parallel execution with other collection groups.
    /// Do NOT change this to <see cref="FirestoreFixture.ClearDataAsync"/> as it would break parallelization.
    /// </remarks>
    [Before(Test)]
    public async Task ClearContextDocumentsAsync()
    {
        await Fixture.ClearContextDocumentsAsync();
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
