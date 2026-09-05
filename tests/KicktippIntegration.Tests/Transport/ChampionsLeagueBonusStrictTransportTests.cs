using System.Net;
using KicktippIntegration.Transport;
using KicktippIntegration.Tests.Shared;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace KicktippIntegration.Tests.Transport;

[NotInParallel("ChampionsLeagueStrictTransportLoopback")]
public sealed class ChampionsLeagueBonusStrictTransportTests : WireMockTestBase
{
    [Test]
    public async Task Direct_ok_posts_once_to_the_exact_action_and_shared_cookie_is_sent()
    {
        var origin = Origin();
        var cookies = new CookieContainer();
        cookies.Add(origin, new Cookie("session", "shared", "/"));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, cookies);

        using var response = await transport.PostAndResolveResponseOnceAsync([new("field", "value")]);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var request = Server.LogEntries.Single();
        await Assert.That(request.RequestMessage.Method).IsEqualTo("POST");
        await Assert.That(request.RequestMessage.Path).IsEqualTo("/schadensfresse/tippabgabeForm");
        await Assert.That(request.RequestMessage.Query?.Count ?? 0).IsEqualTo(0);
        await Assert.That(request.RequestMessage.Body).IsEqualTo("field=value");
        await Assert.That(request.RequestMessage.Headers!["Cookie"].Single()).Contains("session=shared");
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST"
                                                   && entry.RequestMessage.Path == "/schadensfresse/tippabgabe"))
            .IsEqualTo(0);
    }

    [Test]
    [Arguments(302)]
    [Arguments(303)]
    public async Task Safe_redirect_is_followed_once_with_a_bodyless_get(int statusCode)
    {
        var origin = Origin();
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Location", "/schadensfresse/tippabgabe?bonus=true"));
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());

        using var response = await transport.PostAndResolveResponseOnceAsync([new("field", "value")]);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        var followed = Server.LogEntries.Single(entry => entry.RequestMessage.Method == "GET");
        await Assert.That(followed.RequestMessage.Query!["bonus"].Single()).IsEqualTo("true");
        await Assert.That(followed.RequestMessage.Body).IsNull();
    }

    [Test]
    [Arguments(301)]
    [Arguments(304)]
    [Arguments(305)]
    [Arguments(306)]
    [Arguments(307)]
    [Arguments(308)]
    [Arguments(401)]
    [Arguments(403)]
    [Arguments(429)]
    [Arguments(500)]
    public async Task Every_non_allowlisted_status_fails_after_one_post_without_following(int statusCode)
    {
        var origin = Origin();
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithHeader("Location", "/schadensfresse/tippabgabe?bonus=true"));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());

        await Assert.That(() => transport.PostAndResolveResponseOnceAsync([new("field", "value")]))
            .Throws<HttpRequestException>();

        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "GET")).IsEqualTo(0);
    }

    [Test]
    [Arguments("/info/profil/login")]
    [Arguments("/schadensfresse/tippabgabe?bonus=false")]
    [Arguments("http://example.test/schadensfresse/tippabgabe?bonus=true")]
    [Arguments("https://www.kicktipp.de/schadensfresse/tippabgabe?bonus=true#fragment")]
    public async Task Ambiguous_or_wrong_redirect_location_is_never_followed(string location)
    {
        var origin = Origin();
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", location));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());

        await Assert.That(() => transport.PostAndResolveResponseOnceAsync([new("field", "value")]))
            .Throws<InvalidDataException>();

        await Assert.That(Server.LogEntries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Missing_redirect_location_is_never_followed()
    {
        var origin = Origin();
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(303));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());

        await Assert.That(() => transport.PostAndResolveResponseOnceAsync([new("field", "value")]))
            .Throws<InvalidDataException>();

        await Assert.That(Server.LogEntries.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Final_get_is_one_exact_bodyless_request_and_does_not_follow_redirects()
    {
        var origin = Origin();
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabe").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", "/info/profil/login"));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());

        await Assert.That(() => transport.GetOnceAsync()).Throws<HttpRequestException>();

        var request = Server.LogEntries.Single();
        await Assert.That(request.RequestMessage.Method).IsEqualTo("GET");
        await Assert.That(request.RequestMessage.Query!["bonus"].Single()).IsEqualTo("true");
        await Assert.That(request.RequestMessage.Body).IsNull();
    }

    [Test]
    public async Task Cancellation_after_server_observes_post_is_reported_unknown_and_not_retried()
    {
        var origin = Origin();
        var requestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Server.Given(Request.Create().WithPath("/schadensfresse/tippabgabeForm").UsingPost())
            .RespondWith(Response.Create().WithCallback(_ =>
            {
                requestObserved.TrySetResult();
                Thread.Sleep(1_000);
                return new WireMock.ResponseMessage { StatusCode = 200 };
            }));
        using var transport = new ChampionsLeagueBonusStrictTransport(origin, new CookieContainer());
        using var cancellation = new CancellationTokenSource();
        var post = transport.PostAndResolveResponseOnceAsync([new("field", "value")], cancellation.Token);
        await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel();

        await Assert.That(() => post)
            .Throws<InvalidOperationException>()
            .WithMessageContaining("outcome is unknown");

        await Task.Delay(1_000);
        await Assert.That(Server.LogEntries.Count(entry => entry.RequestMessage.Method == "POST")).IsEqualTo(1);
    }

    [Test]
    [Arguments("https://www.kicktipp.de/path")]
    [Arguments("https://www.kicktipp.de/?query=true")]
    [Arguments("https://user@www.kicktipp.de/")]
    [Arguments("ftp://www.kicktipp.de/")]
    public async Task Origin_must_be_an_absolute_http_authority_only(string value)
    {
        await Assert.That(() => new ChampionsLeagueBonusStrictTransport(
                new Uri(value), new CookieContainer()))
            .Throws<ArgumentException>();
    }

    private Uri Origin() => new(ServerUrl + "/");
}
