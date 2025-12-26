using KicktippIntegration.Tests.Infrastructure;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetStandingsAsync method.
/// </summary>
public class KicktippClient_GetStandings_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_standings_returns_empty_list_on_404()
    {
        // Arrange
        StubNotFound("/test-community/tabellen");
        var client = CreateClient(Server);

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert
        await Assert.That(standings).IsEmpty();
    }

    [Test]
    public async Task Getting_standings_returns_empty_list_when_table_is_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Kicktipp</title></head>
            <body>
                <div class="content">
                    <p>No standings available</p>
                </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tabellen", html);
        var client = CreateClient(Server);

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert
        await Assert.That(standings).IsEmpty();
    }

    [Test]
    [Skip("We need to setup the page first")]
    [FixtureRequired]
    public async Task Getting_standings_parses_real_standings_page()
    {
        // Arrange
        StubWithFixture("/test-community/tabellen", "tabellen");
        var client = CreateClient(Server);

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert - verify we got some standings
        await Assert.That(standings).IsNotEmpty();
        
        // Verify first standing has expected properties populated
        var first = standings[0];
        await Assert.That(first.Position).IsGreaterThan(0);
        await Assert.That(first.TeamName).IsNotNull().And.IsNotEmpty();
    }
}
