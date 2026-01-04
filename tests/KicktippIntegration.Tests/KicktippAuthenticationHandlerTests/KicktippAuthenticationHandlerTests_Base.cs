using EHonda.Optional.Core;
using KicktippIntegration.Authentication;
using KicktippIntegration.Tests.Shared;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace KicktippIntegration.Tests.KicktippAuthenticationHandlerTests;

/// <summary>
/// Base class for KicktippAuthenticationHandler tests.
/// </summary>
public abstract class KicktippAuthenticationHandlerTests_Base : WireMockTestBase
{
    protected const string LoginPath = "/info/profil/login";
    protected const string LoginActionPath = "/info/profil/loginaction";

    /// <summary>
    /// Creates a KicktippAuthenticationHandler configured to use the WireMock server.
    /// </summary>
    protected KicktippAuthenticationHandler CreateHandler(
        Option<IOptions<KicktippOptions>> options = default,
        Option<FakeLogger<KicktippAuthenticationHandler>> logger = default)
    {
        var actualOptions = options.Or(() => CreateDefaultOptions());
        var actualLogger = logger.Or(() => new FakeLogger<KicktippAuthenticationHandler>());

        // Pass the WireMock server URL as the base URL for testing
        return new KicktippAuthenticationHandler(actualOptions, actualLogger, ServerUrl)
        {
            InnerHandler = new HttpClientHandler()
        };
    }

    /// <summary>
    /// Creates an HttpClient that uses the given authentication handler and points to WireMock.
    /// </summary>
    protected HttpClient CreateHttpClientWithHandler(KicktippAuthenticationHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(ServerUrl)
        };
    }

    /// <summary>
    /// Creates default Kicktipp options with test credentials.
    /// </summary>
    protected static IOptions<KicktippOptions> CreateDefaultOptions(
        string username = "testuser",
        string password = "testpassword")
    {
        return Options.Create(new KicktippOptions
        {
            Username = username,
            Password = password
        });
    }

    /// <summary>
    /// Stubs the login page GET request with the login form.
    /// </summary>
    protected void StubLoginPageWithForm()
    {
        StubWithSyntheticFixture(LoginPath, "login", "login-form");
    }

    /// <summary>
    /// Stubs the login page GET request with a page missing the form.
    /// </summary>
    protected void StubLoginPageWithoutForm()
    {
        StubWithSyntheticFixture(LoginPath, "login", "login-form-missing");
    }

    /// <summary>
    /// Stubs the login action POST request to return a successful redirect (non-login URL).
    /// Uses a 302 redirect to simulate the real Kicktipp behavior where successful login
    /// redirects to the home page, and this affects the RequestUri seen by the handler.
    /// </summary>
    protected void StubSuccessfulLoginAction()
    {
        var successContent = LoadSyntheticFixtureContent("login", "login-success-redirect");
        
        // First stub the POST to redirect to a success page
        Server
            .Given(Request.Create()
                .WithPath(LoginActionPath)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", "/meine-tipprunden"));

        // Then stub the redirected page
        Server
            .Given(Request.Create()
                .WithPath("/meine-tipprunden")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(successContent));
    }

    /// <summary>
    /// Stubs the login action POST request to return the login page again (failed login).
    /// </summary>
    protected void StubFailedLoginAction()
    {
        var failedContent = LoadSyntheticFixtureContent("login", "login-failed");
        
        // Failed login shows the login form again with an error
        Server
            .Given(Request.Create()
                .WithPath(LoginActionPath)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(failedContent));
    }

    /// <summary>
    /// Stubs a complete successful login flow (GET login page + POST credentials).
    /// </summary>
    protected void StubSuccessfulLoginFlow()
    {
        StubLoginPageWithForm();
        StubSuccessfulLoginAction();
    }

    /// <summary>
    /// Stubs a failed login flow (GET login page + POST returns login form with error).
    /// </summary>
    protected void StubFailedLoginFlow()
    {
        StubLoginPageWithForm();
        StubFailedLoginAction();
    }

    /// <summary>
    /// Stubs a GET request with a callback that can return different responses.
    /// Useful for testing re-authentication scenarios.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="responseProvider">A function returning (statusCode, htmlBody) tuple.</param>
    protected void StubHtmlResponseWithCallback(string path, Func<(int statusCode, string body)> responseProvider)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    var (statusCode, body) = responseProvider();
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = statusCode,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = new WireMock.Types.WireMockList<string>("text/html; charset=utf-8")
                        },
                        BodyData = new WireMock.Util.BodyData
                        {
                            DetectedBodyType = WireMock.Types.BodyType.String,
                            BodyAsString = body
                        }
                    };
                }));
    }

    /// <summary>
    /// Stubs a POST request with a callback that can return different responses.
    /// Useful for testing re-authentication scenarios with POST requests that have body content.
    /// </summary>
    /// <param name="path">The URL path.</param>
    /// <param name="responseProvider">A function returning (statusCode, htmlBody) tuple.</param>
    protected void StubPostResponseWithCallback(string path, Func<(int statusCode, string body)> responseProvider)
    {
        Server
            .Given(Request.Create()
                .WithPath(path)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithCallback(_ =>
                {
                    var (statusCode, body) = responseProvider();
                    return new WireMock.ResponseMessage
                    {
                        StatusCode = statusCode,
                        Headers = new Dictionary<string, WireMock.Types.WireMockList<string>>
                        {
                            ["Content-Type"] = new WireMock.Types.WireMockList<string>("text/html; charset=utf-8")
                        },
                        BodyData = new WireMock.Util.BodyData
                        {
                            DetectedBodyType = WireMock.Types.BodyType.String,
                            BodyAsString = body
                        }
                    };
                }));
    }

    /// <summary>
    /// Stubs the login action POST to redirect to a non-login URL that still contains
    /// the login form. This tests the edge case where URL-based success detection passes
    /// but form-presence check catches the failed login.
    /// </summary>
    protected void StubLoginActionReturnsNonLoginUrlWithForm()
    {
        // Redirect to a non-login URL
        Server
            .Given(Request.Create()
                .WithPath(LoginActionPath)
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", "/some-error-page"));

        // That page still has the login form (unusual but possible edge case)
        var loginFormContent = LoadSyntheticFixtureContent("login", "login-form");
        Server
            .Given(Request.Create()
                .WithPath("/some-error-page")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(loginFormContent));
    }
}
