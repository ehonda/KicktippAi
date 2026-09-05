using KicktippIntegration;
using KicktippIntegration.Authentication;
using KicktippIntegration.Transport;
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
    private static readonly Uri ProductionOrigin = new("https://www.kicktipp.de");
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

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
        return CreateGenericAuthenticatedHttpClient(
            ProductionOrigin,
            new System.Net.CookieContainer());
    }

    /// <inheritdoc />
    public ISnapshotClient CreateSnapshotClient()
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

    private IKicktippClient InitializeClient() => BuildClient(ProductionOrigin);

    internal KicktippClient BuildClient(Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var cookies = new System.Net.CookieContainer();
        var httpClient = CreateGenericAuthenticatedHttpClient(origin, cookies);
        var strictTransport = new ChampionsLeagueBonusStrictTransport(origin, cookies, TimeSpan.FromMinutes(2));
        var clientLogger = _loggerFactory.CreateLogger<KicktippClient>();
        return new KicktippClient(httpClient, clientLogger, _memoryCache, strictTransport);
    }

    private HttpClient CreateGenericAuthenticatedHttpClient(Uri origin, System.Net.CookieContainer cookies)
    {
        var primaryHandler = new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        var options = Options.Create(_credentials.Value);
        var authLogger = _loggerFactory.CreateLogger<KicktippAuthenticationHandler>();
        var authHandler = new KicktippAuthenticationHandler(
            options,
            authLogger,
            origin.GetLeftPart(UriPartial.Authority))
        {
            InnerHandler = primaryHandler
        };
        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = origin,
            Timeout = TimeSpan.FromMinutes(2)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", BrowserUserAgent);
        return httpClient;
    }
}
