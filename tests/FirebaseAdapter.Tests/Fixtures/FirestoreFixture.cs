using Google.Cloud.Firestore;
using Grpc.Core;
using Testcontainers.Firestore;
using TUnit.Core.Interfaces;

namespace FirebaseAdapter.Tests.Fixtures;

/// <summary>
/// Testcontainers-based fixture that starts a Firestore emulator for integration tests.
/// Uses the official Google Cloud CLI emulators image.
/// </summary>
/// <remarks>
/// <para>
/// <b>Architecture Overview:</b>
/// This fixture is designed to be <b>shared across all test classes</b> using
/// <c>[ClassDataSource&lt;FirestoreFixture&gt;(Shared = SharedType.Keyed, Key = "FirestoreEmulator")]</c>.
/// This significantly reduces test execution time by starting only one Docker container instead of one per test class.
/// </para>
/// 
/// <para>
/// <b>Why Keyed over PerAssembly?</b>
/// Both achieve single-container sharing, but <c>SharedType.Keyed</c> is preferred because:
/// <list type="bullet">
///   <item><description>The key "FirestoreEmulator" explicitly documents what resource is shared</description></item>
///   <item><description>Future flexibility: if we need multiple fixture types, Keyed allows fine-grained control</description></item>
///   <item><description>Searching for the key string reveals all tests sharing that resource</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Collection-Based Parallelization:</b>
/// Our repositories operate on isolated Firestore collections:
/// <list type="bullet">
///   <item><description><c>FirebaseContextRepository</c> → <c>context-documents</c></description></item>
///   <item><description><c>FirebasePredictionRepository</c> → <c>match-predictions</c>, <c>matches</c>, <c>bonus-predictions</c></description></item>
///   <item><description><c>FirebaseKpiRepository</c> → <c>kpi-documents</c></description></item>
/// </list>
/// Since collections don't overlap, tests targeting different repositories can run <b>in parallel</b>.
/// Use collection-specific clear methods (e.g., <see cref="ClearContextDocumentsAsync"/>) to enable this parallelization.
/// </para>
/// 
/// <para>
/// <b>Test Isolation Pattern:</b>
/// Tests within the same repository group must run sequentially (via <c>[NotInParallel("Firestore:GroupKey")]</c>)
/// and call the collection-specific clear method in <c>[Before(Test)]</c> to ensure isolation.
/// </para>
/// 
/// <para>
/// See <c>.github/instructions/firebase-adapter-tests.instructions.md</c> for complete documentation.
/// </para>
/// </remarks>
public sealed class FirestoreFixture : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>
    /// The shared fixture key used by all test classes.
    /// Use this constant in <c>[ClassDataSource&lt;FirestoreFixture&gt;(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]</c>.
    /// </summary>
    public const string SharedKey = "FirestoreEmulator";

    /// <summary>
    /// Parallel constraint key for tests using <c>context-documents</c> collection.
    /// Tests with this key run sequentially with each other, but in parallel with other collection groups.
    /// </summary>
    public const string ContextDocumentsParallelKey = "Firestore:ContextDocuments";

    /// <summary>
    /// Parallel constraint key for tests using prediction-related collections
    /// (<c>match-predictions</c>, <c>matches</c>, <c>bonus-predictions</c>).
    /// Tests with this key run sequentially with each other, but in parallel with other collection groups.
    /// </summary>
    public const string PredictionsParallelKey = "Firestore:Predictions";

    /// <summary>
    /// Parallel constraint key for tests using <c>kpi-documents</c> collection.
    /// Tests with this key run sequentially with each other, but in parallel with other collection groups.
    /// </summary>
    public const string KpiParallelKey = "Firestore:Kpi";

    // Collection names matching the repository implementations
    private const string ContextDocumentsCollection = "context-documents";
    private const string MatchPredictionsCollection = "match-predictions";
    private const string MatchesCollection = "matches";
    private const string BonusPredictionsCollection = "bonus-predictions";
    private const string KpiDocumentsCollection = "kpi-documents";

    /// <summary>
    /// Pin to a specific emulator image tag for reproducible builds.
    /// This is the latest tag as of 2025-12-23.
    /// </summary>
    private const string EmulatorImageTag = "google/cloud-sdk:550.0.0-emulators";

    private readonly FirestoreContainer _container;

    public FirestoreDb Db { get; private set; } = null!;
    public string ProjectId { get; } = $"test-project-{Guid.NewGuid():N}";

    public FirestoreFixture()
    {
        _container = new FirestoreBuilder()
            .WithImage(EmulatorImageTag)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var firestoreDbBuilder = new FirestoreDbBuilder
        {
            ProjectId = ProjectId,
            Endpoint = _container.GetEmulatorEndpoint(),
            ChannelCredentials = ChannelCredentials.Insecure
        };

        Db = await firestoreDbBuilder.BuildAsync();
    }

    /// <summary>
    /// Clears only the <c>context-documents</c> collection.
    /// Use this in <c>[Before(Test)]</c> for <c>FirebaseContextRepository</c> tests.
    /// </summary>
    public async Task ClearContextDocumentsAsync()
    {
        await ClearCollectionAsync(ContextDocumentsCollection);
    }

    /// <summary>
    /// Clears the prediction-related collections: <c>match-predictions</c>, <c>matches</c>, and <c>bonus-predictions</c>.
    /// Use this in <c>[Before(Test)]</c> for <c>FirebasePredictionRepository</c> tests.
    /// </summary>
    public async Task ClearPredictionsAsync()
    {
        // Clear all three collections in parallel for efficiency
        await Task.WhenAll(
            ClearCollectionAsync(MatchPredictionsCollection),
            ClearCollectionAsync(MatchesCollection),
            ClearCollectionAsync(BonusPredictionsCollection));
    }

    /// <summary>
    /// Clears only the <c>kpi-documents</c> collection.
    /// Use this in <c>[Before(Test)]</c> for <c>FirebaseKpiRepository</c> tests.
    /// </summary>
    public async Task ClearKpiDocumentsAsync()
    {
        await ClearCollectionAsync(KpiDocumentsCollection);
    }

    /// <summary>
    /// Clears a specific collection from the emulator.
    /// </summary>
    /// <param name="collectionName">The name of the collection to clear.</param>
    private async Task ClearCollectionAsync(string collectionName)
    {
        // The Firestore emulator supports deleting a specific collection path
        using var httpClient = new HttpClient();
        var endpoint = _container.GetEmulatorEndpoint().TrimEnd('/');
        var deleteUrl = $"{endpoint}/emulator/v1/projects/{ProjectId}/databases/(default)/documents/{collectionName}";

        var response = await httpClient.DeleteAsync(deleteUrl);
        // Note: 404 is acceptable if the collection doesn't exist yet
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
