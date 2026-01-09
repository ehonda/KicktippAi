using KicktippIntegration;
using KicktippIntegration.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Commands.Utility.Snapshots;

namespace Orchestrator.Infrastructure.Factories;

/// <summary>
/// Default implementation of <see cref="IKicktippClientFactory"/>.
/// </summary>
/// <remarks>
/// Reads credentials from KICKTIPP_USERNAME and KICKTIPP_PASSWORD environment variables.
/// </remarks>
public sealed class KicktippClientFactory : IKicktippClientFactory
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lazy<IKicktippClient> _client;
    private readonly Lazy<KicktippOptions> _credentials;

    public KicktippClientFactory(
        IMemoryCache memoryCache,
        ILoggerFactory loggerFactory)
    {
        _memoryCache = memoryCache;
        _loggerFactory = loggerFactory;
        _credentials = new Lazy<KicktippOptions>(LoadCredentials);
        _client = new Lazy<IKicktippClient>(InitializeClient);
    }

    /// <inheritdoc />
    public IKicktippClient CreateClient() => _client.Value;

    /// <inheritdoc />
    public HttpClient CreateAuthenticatedHttpClient()
    {
        var options = Options.Create(_credentials.Value);

        // Create the authentication handler
        var authLogger = _loggerFactory.CreateLogger<KicktippAuthenticationHandler>();
        var authHandler = new KicktippAuthenticationHandler(options, authLogger)
        {
            InnerHandler = new HttpClientHandler()
        };

        // Create HttpClient with the auth handler
        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://www.kicktipp.de"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        return httpClient;
    }

    /// <inheritdoc />
    public SnapshotClient CreateSnapshotClient()
    {
        var httpClient = CreateAuthenticatedHttpClient();
        var logger = _loggerFactory.CreateLogger<SnapshotClient>();
        return new SnapshotClient(httpClient, logger);
    }

    private static KicktippOptions LoadCredentials()
    {
        var username = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME");
        var password = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD");

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("KICKTIPP_USERNAME environment variable is required");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("KICKTIPP_PASSWORD environment variable is required");
        }

        return new KicktippOptions
        {
            Username = username,
            Password = password
        };
    }

    private IKicktippClient InitializeClient()
    {
        var options = Options.Create(_credentials.Value);

        // Create the authentication handler
        var authLogger = _loggerFactory.CreateLogger<KicktippAuthenticationHandler>();
        var authHandler = new KicktippAuthenticationHandler(options, authLogger)
        {
            InnerHandler = new HttpClientHandler()
        };

        // Create HttpClient with the auth handler
        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("https://www.kicktipp.de"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        // Create and return the client
        var clientLogger = _loggerFactory.CreateLogger<KicktippClient>();
        return new KicktippClient(httpClient, clientLogger, _memoryCache);
    }
}
