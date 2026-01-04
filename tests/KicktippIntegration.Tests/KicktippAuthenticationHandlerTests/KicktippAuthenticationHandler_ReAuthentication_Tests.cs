namespace KicktippIntegration.Tests.KicktippAuthenticationHandlerTests;

/// <summary>
/// Tests for KicktippAuthenticationHandler re-authentication behavior.
/// </summary>
public class KicktippAuthenticationHandler_ReAuthentication_Tests : KicktippAuthenticationHandlerTests_Base
{
    [Test]
    public async Task Receiving_401_triggers_re_authentication()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        
        // Use a stateful response provider
        var requestCount = 0;
        StubHtmlResponseWithCallback("/test-community/tabellen", () =>
        {
            requestCount++;
            return requestCount == 1 
                ? (401, "<html><body>Unauthorized</body></html>") 
                : (200, "<html><body>Standings</body></html>");
        });

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act
        var response = await client.GetAsync("/test-community/tabellen");

        // Assert - request should eventually succeed after re-auth
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        // Login should have happened twice (initial + re-auth)
        var loginPageRequests = GetRequestsForPath(LoginPath);
        await Assert.That(loginPageRequests.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Receiving_403_triggers_re_authentication()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        
        // Use a stateful response provider
        var requestCount = 0;
        StubHtmlResponseWithCallback("/test-community/tabellen", () =>
        {
            requestCount++;
            return requestCount == 1 
                ? (403, "<html><body>Forbidden</body></html>") 
                : (200, "<html><body>Standings</body></html>");
        });

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act
        var response = await client.GetAsync("/test-community/tabellen");

        // Assert - request should eventually succeed after re-auth
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        // Login should have happened twice (initial + re-auth)
        var loginPageRequests = GetRequestsForPath(LoginPath);
        await Assert.That(loginPageRequests.Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Successful_request_after_login_does_not_trigger_re_auth()
    {
        // Arrange
        StubSuccessfulLoginFlow();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act - make a successful request
        var response = await client.GetAsync("/test-community/tabellen");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();

        // Only one login should have occurred
        var loginPageRequests = GetRequestsForPath(LoginPath);
        await Assert.That(loginPageRequests.Count()).IsEqualTo(1);
    }
}
