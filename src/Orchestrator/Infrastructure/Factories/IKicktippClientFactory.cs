using KicktippIntegration;
using Orchestrator.Commands.Utility.Snapshots;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Factory for creating Kicktipp client services.
/// </summary>
/// <remarks>
/// The factory reads credentials from environment variables and provides
/// methods to create clients.
/// </remarks>
public interface IKicktippClientFactory
{
    /// <summary>
    /// Creates a Kicktipp client configured with credentials from environment variables.
    /// </summary>
    /// <returns>A configured Kicktipp client instance.</returns>
    IKicktippClient CreateClient();

    /// <summary>
    /// Creates an authenticated HTTP client for direct HTTP requests to Kicktipp.
    /// </summary>
    /// <returns>An HttpClient configured with authentication.</returns>
    /// <remarks>
    /// Use this for low-level HTTP operations that don't
    /// use the structured <see cref="IKicktippClient"/> API.
    /// </remarks>
    HttpClient CreateAuthenticatedHttpClient();

    /// <summary>
    /// Creates a snapshot client for fetching HTML snapshots from Kicktipp.
    /// </summary>
    /// <returns>A configured <see cref="ISnapshotClient"/> instance.</returns>
    ISnapshotClient CreateSnapshotClient();
}
