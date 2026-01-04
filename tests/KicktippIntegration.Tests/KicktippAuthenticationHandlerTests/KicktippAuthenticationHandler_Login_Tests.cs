namespace KicktippIntegration.Tests.KicktippAuthenticationHandlerTests;

/// <summary>
/// Tests for KicktippAuthenticationHandler login functionality.
/// </summary>
public class KicktippAuthenticationHandler_Login_Tests : KicktippAuthenticationHandlerTests_Base
{
    [Test]
    public async Task Sending_request_performs_login_and_forwards_request()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act
        var response = await client.GetAsync("/test-community/tabellen");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        var content = await response.Content.ReadAsStringAsync();
        await Assert.That(content).Contains("Standings");

        // Verify login was performed
        var loginPageRequests = GetRequestsForPath(LoginPath);
        await Assert.That(loginPageRequests.Count()).IsEqualTo(1);

        var loginActionRequests = GetRequestsForPath(LoginActionPath);
        await Assert.That(loginActionRequests.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Login_sends_credentials_in_form_data()
    {
        // Arrange
        const string username = "myuser";
        const string password = "mypassword";

        StubSuccessfulLoginFlow();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var options = CreateDefaultOptions(username, password);
        var handler = CreateHandler(EHonda.Optional.Core.Option.Some(options));
        var client = CreateHttpClientWithHandler(handler);

        // Act
        await client.GetAsync("/test-community/tabellen");

        // Assert - verify form data contains credentials
        var loginActionRequests = GetRequestsForPath(LoginActionPath).ToList();
        await Assert.That(loginActionRequests).HasCount().EqualTo(1);

        var formData = ParseFormData(loginActionRequests[0].RequestMessage.Body);
        await Assert.That(formData).ContainsKey("kennung");
        await Assert.That(formData).ContainsKey("passwort");
        await Assert.That(formData["kennung"]).IsEqualTo(username);
        await Assert.That(formData["passwort"]).IsEqualTo(password);
    }

    [Test]
    public async Task Login_includes_hidden_form_fields()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act
        await client.GetAsync("/test-community/tabellen");

        // Assert - verify hidden fields from the form are included
        var loginActionRequests = GetRequestsForPath(LoginActionPath).ToList();
        await Assert.That(loginActionRequests).HasCount().EqualTo(1);

        var formData = ParseFormData(loginActionRequests[0].RequestMessage.Body);
        // The login-form.html includes _charset_ and source as hidden fields
        await Assert.That(formData).ContainsKey("_charset_");
        await Assert.That(formData["_charset_"]).IsEqualTo("UTF-8");
    }

    [Test]
    public async Task Subsequent_requests_do_not_trigger_login_again()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");
        StubHtmlResponse("/test-community/tippabgabe", "<html><body>Betting</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act - make multiple requests
        await client.GetAsync("/test-community/tabellen");
        await client.GetAsync("/test-community/tippabgabe");
        await client.GetAsync("/test-community/tabellen");

        // Assert - login should only happen once
        var loginPageRequests = GetRequestsForPath(LoginPath);
        await Assert.That(loginPageRequests.Count()).IsEqualTo(1);

        var loginActionRequests = GetRequestsForPath(LoginActionPath);
        await Assert.That(loginActionRequests.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Login_failure_throws_unauthorized_access_exception()
    {
        // Arrange
        StubFailedLoginFlow();

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act & Assert
        await Assert.That(async () => await client.GetAsync("/test-community/tabellen"))
            .Throws<UnauthorizedAccessException>()
            .WithMessageContaining("login failed");
    }

    [Test]
    public async Task Missing_login_form_throws_invalid_operation_exception()
    {
        // Arrange
        StubLoginPageWithoutForm();

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act & Assert
        await Assert.That(async () => await client.GetAsync("/test-community/tabellen"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("login form");
    }

    [Test]
    public async Task Login_page_fetch_failure_throws_http_request_exception()
    {
        // Arrange
        StubStatusCode(LoginPath, 500);

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act & Assert
        await Assert.That(async () => await client.GetAsync("/test-community/tabellen"))
            .Throws<HttpRequestException>()
            .WithMessageContaining("login page");
    }

    [Test]
    public async Task Invalid_credentials_configured_throws_invalid_operation_exception()
    {
        // Arrange - empty credentials
        var options = CreateDefaultOptions(username: "", password: "");
        var handler = CreateHandler(EHonda.Optional.Core.Option.Some(options));
        var client = CreateHttpClientWithHandler(handler);

        // Act & Assert
        await Assert.That(async () => await client.GetAsync("/test-community/tabellen"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("credentials");
    }
}
