using EHonda.Optional.Core;
using KicktippIntegration.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Base class for KicktippClient tests providing WireMock server and factory methods.
/// </summary>
public abstract class KicktippClientTests_Base : IAsyncDisposable
{
    protected WireMockServer Server { get; private set; } = null!;

    [Before(Test)]
    public void StartServer()
    {
        Server = WireMockServer.Start();
    }

    [After(Test)]
    public void StopServer()
    {
        Server?.Stop();
    }

    public ValueTask DisposeAsync()
    {
        Server?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a KicktippClient configured to use the WireMock server.
    /// </summary>
    protected static KicktippClient CreateClient(
        WireMockServer server,
        Option<FakeLogger<KicktippClient>> logger = default,
        Option<IMemoryCache> cache = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<KicktippClient>());
        var actualCache = cache.Or(() => new MemoryCache(new MemoryCacheOptions()));

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(server.Urls[0])
        };

        return new KicktippClient(httpClient, actualLogger, actualCache);
    }

    /// <summary>
    /// Stubs a GET request to return HTML content.
    /// </summary>
    /// <param name="path">The URL path (e.g., "/test-community/tabellen").</param>
    /// <param name="htmlContent">The HTML content to return.</param>
    protected void StubHtmlResponse(string path, string htmlContent)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(htmlContent));
    }

    /// <summary>
    /// Stubs a GET request to return a 404 Not Found.
    /// </summary>
    /// <param name="path">The URL path.</param>
    protected void StubNotFound(string path)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404));
    }

    /// <summary>
    /// Stubs a GET request using an encrypted fixture file.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="fixtureName">Name of the fixture (without .html.enc extension).</param>
    protected void StubWithFixture(string path, string fixtureName)
    {
        var htmlContent = FixtureLoader.LoadFixture(fixtureName);
        StubHtmlResponse(path, htmlContent);
    }
}
