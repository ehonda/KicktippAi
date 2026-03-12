using Google.Cloud.Firestore;
using Grpc.Core;
using Testcontainers.Firestore;
using TUnit.Core.Interfaces;

namespace TestUtilities;

/// <summary>
/// Shared Testcontainers-based Firestore emulator fixture for integration tests.
/// </summary>
/// <remarks>
/// This fixture is shared across test projects so repository tests and higher-level
/// integration tests use the same emulator lifecycle and collection-clearing behavior.
/// </remarks>
public sealed class FirestoreFixture : IAsyncInitializer, IAsyncDisposable
{
    public const string SharedKey = "FirestoreEmulator";
    public const string ContextDocumentsParallelKey = "Firestore:ContextDocuments";
    public const string PredictionsParallelKey = "Firestore:Predictions";
    public const string KpiParallelKey = "Firestore:Kpi";
    public const string OrchestratorIntegrationParallelKey = "Firestore:OrchestratorIntegration";

    private const string ContextDocumentsCollection = "context-documents";
    private const string MatchPredictionsCollection = "match-predictions";
    private const string MatchesCollection = "matches";
    private const string BonusPredictionsCollection = "bonus-predictions";
    private const string KpiDocumentsCollection = "kpi-documents";
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

    public async Task ClearContextDocumentsAsync()
    {
        await ClearCollectionAsync(ContextDocumentsCollection);
    }

    public async Task ClearPredictionsAsync()
    {
        await Task.WhenAll(
            ClearCollectionAsync(MatchPredictionsCollection),
            ClearCollectionAsync(MatchesCollection),
            ClearCollectionAsync(BonusPredictionsCollection));
    }

    public async Task ClearKpiDocumentsAsync()
    {
        await ClearCollectionAsync(KpiDocumentsCollection);
    }

    public async Task ClearOrchestratorIntegrationAsync()
    {
        await Task.WhenAll(
            ClearPredictionsAsync(),
            ClearContextDocumentsAsync());
    }

    private async Task ClearCollectionAsync(string collectionName)
    {
        using var httpClient = new HttpClient();
        var endpoint = _container.GetEmulatorEndpoint().TrimEnd('/');
        var deleteUrl = $"{endpoint}/emulator/v1/projects/{ProjectId}/databases/(default)/documents/{collectionName}";

        var response = await httpClient.DeleteAsync(deleteUrl);
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
