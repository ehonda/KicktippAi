using KicktippIntegration.Tests.Infrastructure;

namespace KicktippIntegration.Tests.KicktippAuthenticationHandlerTests;

/// <summary>
/// Tests using real login page fixtures to verify parsing invariants.
/// </summary>
public class KicktippAuthenticationHandler_RealFixture_Tests : KicktippAuthenticationHandlerTests_Base
{
    private const string RealCommunity = "ehonda-test-buli";

    [Test]
    public async Task Real_login_page_contains_login_form()
    {
        // Arrange - use encrypted real fixture
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (structure, required elements) not concrete values.
        StubWithRealFixture(LoginPath, RealCommunity, "login");
        StubSuccessfulLoginAction();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act - should successfully parse the real login page
        var response = await client.GetAsync("/test-community/tabellen");

        // Assert
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        
        // Verify login was performed (form was found and parsed)
        var loginActionRequests = GetRequestsForPath(LoginActionPath).ToList();
        await Assert.That(loginActionRequests).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Real_login_page_form_has_required_input_fields()
    {
        // Arrange
        StubWithRealFixture(LoginPath, RealCommunity, "login");
        StubSuccessfulLoginAction();
        StubHtmlResponse("/test-community/tabellen", "<html><body>Standings</body></html>");

        var handler = CreateHandler();
        var client = CreateHttpClientWithHandler(handler);

        // Act
        await client.GetAsync("/test-community/tabellen");

        // Assert - verify credentials were sent (form has kennung and passwort fields)
        var loginActionRequests = GetRequestsForPath(LoginActionPath).ToList();
        var formData = ParseFormData(loginActionRequests[0].RequestMessage.Body);

        await Assert.That(formData).ContainsKey("kennung");
        await Assert.That(formData).ContainsKey("passwort");
    }

    [Test]
    public async Task Real_login_page_fixture_can_be_loaded()
    {
        // This test verifies the fixture exists and can be decrypted
        // It will fail if the fixture is missing or the encryption key is not set
        var content = LoadRealFixtureContent(RealCommunity, "login");
        
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsGreaterThan(0);
    }
}
