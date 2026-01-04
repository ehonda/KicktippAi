using EHonda.Optional.Core;
using KicktippIntegration.Tests.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Testing;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Base class for KicktippClient tests providing WireMock server and factory methods.
/// </summary>
public abstract class KicktippClientTests_Base : WireMockTestBase
{
    /// <summary>
    /// Creates a KicktippClient configured to use the WireMock server.
    /// </summary>
    protected KicktippClient CreateClient(
        Option<FakeLogger<KicktippClient>> logger = default,
        Option<IMemoryCache> cache = default)
    {
        var actualLogger = logger.Or(() => new FakeLogger<KicktippClient>());
        var actualCache = cache.Or(() => new MemoryCache(new MemoryCacheOptions()));

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(ServerUrl)
        };

        return new KicktippClient(httpClient, actualLogger, actualCache);
    }
}
