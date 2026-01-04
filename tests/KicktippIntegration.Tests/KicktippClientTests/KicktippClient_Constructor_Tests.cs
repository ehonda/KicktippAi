using EHonda.Optional.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient constructor validation.
/// </summary>
public class KicktippClient_Constructor_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Passing_null_httpClient_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateClient(httpClient: NullableOption.Some<HttpClient>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Test]
    public async Task Passing_null_logger_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateClient(logger: NullableOption.Some<ILogger<KicktippClient>>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public async Task Passing_null_cache_throws_ArgumentNullException()
    {
        await Assert.That(() => CreateClient(cache: NullableOption.Some<IMemoryCache>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("cache");
    }
}
