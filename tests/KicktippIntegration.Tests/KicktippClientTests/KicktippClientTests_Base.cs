using EHonda.Optional.Core;
using KicktippIntegration.Tests.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Base class for KicktippClient tests providing WireMock server and factory methods.
/// </summary>
public abstract class KicktippClientTests_Base : WireMockTestBase
{
    /// <summary>
    /// Creates a KicktippClient configured to use the WireMock server.
    /// Uses NullableOption for parameters that have null guards in the constructor,
    /// allowing null-guard tests to pass null explicitly.
    /// </summary>
    protected KicktippClient CreateClient(
        NullableOption<HttpClient> httpClient = default,
        NullableOption<ILogger<KicktippClient>> logger = default,
        NullableOption<IMemoryCache> cache = default)
    {
        var actualHttpClient = httpClient.Or(() => new HttpClient { BaseAddress = new Uri(ServerUrl) });
        var actualLogger = logger.Or(() => new FakeLogger<KicktippClient>());
        var actualCache = cache.Or(() => new MemoryCache(new MemoryCacheOptions()));

        return new KicktippClient(actualHttpClient!, actualLogger!, actualCache!);
    }
}
