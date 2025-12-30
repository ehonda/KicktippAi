using Microsoft.Extensions.Caching.Memory;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetMatchesWithHistoryAsync method.
/// </summary>
public class KicktippClient_GetMatchesWithHistory_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_matches_with_history_returns_empty_list_on_tippabgabe_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_matches_with_history_returns_empty_list_when_spielinfo_link_missing()
    {
        // Arrange - tippabgabe page without spielinfo link
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_matches_with_history_navigates_through_spielinfo_pages()
    {
        // Arrange - set up tippabgabe with spielinfo link
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        
        // First spielinfo page with next link
        StubWithSyntheticFixture("/test-community/spielinfo", "spielinfo-first");
        
        // Second spielinfo page with disabled next (last page)
        StubHtmlResponseWithParams("/test-community/spielinfo", 
            LoadSyntheticFixtureContent("spielinfo-last"),
            ("tippspielId", "2"));
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(2);
        await Assert.That(matches[0].Match.HomeTeam).IsEqualTo("Home Team 1");
        await Assert.That(matches[1].Match.HomeTeam).IsEqualTo("Home Team 2");
    }

    [Test]
    public async Task Getting_matches_with_history_extracts_team_history()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubWithSyntheticFixture("/test-community/spielinfo", "spielinfo-last");
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(1);
        
        // Check home team history
        var homeHistory = matches[0].HomeTeamHistory;
        await Assert.That(homeHistory).IsNotEmpty();
        await Assert.That(homeHistory[0].HomeGoals).IsEqualTo(4);
        await Assert.That(homeHistory[0].AwayGoals).IsEqualTo(0);
        
        // Check away team history
        var awayHistory = matches[0].AwayTeamHistory;
        await Assert.That(awayHistory).IsNotEmpty();
    }

    [Test]
    public async Task Getting_matches_with_history_uses_cache()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubWithSyntheticFixture("/test-community/spielinfo", "spielinfo-last");
        
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(cache: cache);

        // Act - first call should hit server
        var firstResult = await client.GetMatchesWithHistoryAsync("test-community");
        var requestsAfterFirst = Server.LogEntries.Count();
        
        // Second call should use cache
        var secondResult = await client.GetMatchesWithHistoryAsync("test-community");
        var requestsAfterSecond = Server.LogEntries.Count();

        // Assert
        await Assert.That(firstResult).HasCount().EqualTo(1);
        await Assert.That(secondResult).HasCount().EqualTo(1);
        await Assert.That(requestsAfterSecond).IsEqualTo(requestsAfterFirst); // No new requests
    }

    [Test]
    public async Task Getting_matches_with_history_handles_spielinfo_404()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubNotFound("/test-community/spielinfo");
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert - should return empty list gracefully
        await Assert.That(matches).IsEmpty();
    }

    private static string LoadSyntheticFixtureContent(string name)
    {
        var fixturesDir = Infrastructure.FixtureLoader.GetFixturesDirectory();
        var syntheticDir = Path.Combine(fixturesDir, "Synthetic");
        return File.ReadAllText(Path.Combine(syntheticDir, $"{name}.html"));
    }
}
