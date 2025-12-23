using Google.Cloud.Firestore;
using Grpc.Core;
using Testcontainers.Firestore;
using TUnit.Core.Interfaces;

namespace FirebaseAdapter.Tests.Fixtures;

/// <summary>
/// Testcontainers-based fixture that starts a Firestore emulator for integration tests.
/// Uses the official Google Cloud CLI emulators image.
/// </summary>
public sealed class FirestoreFixture : IAsyncInitializer, IAsyncDisposable
{
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
    /// Clears all data from the emulator.
    /// Call this between tests to ensure test isolation.
    /// </summary>
    public async Task ClearDataAsync()
    {
        using var httpClient = new HttpClient();
        var endpoint = _container.GetEmulatorEndpoint().TrimEnd('/');
        var deleteUrl = $"{endpoint}/emulator/v1/projects/{ProjectId}/databases/(default)/documents";

        var response = await httpClient.DeleteAsync(deleteUrl);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
