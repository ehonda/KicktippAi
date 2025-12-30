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

    [Test]
    public async Task Getting_matches_with_history_parses_real_spielinfo_snapshots()
    {
        // Arrange - use real snapshots from kicktipp-snapshots directory
        // The tippabgabe page contains the actual community name "ehonda-test-buli" in all its links
        // The client extracts these links and uses them directly
        StubWithSnapshot("/ehonda-test-buli/tippabgabe", "tippabgabe");
        
        // First spielinfo page (Frankfurt vs Dortmund) - no ansicht parameter in the initial link
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-01",
            ("tippsaisonId", "3684392"),
            ("tippspielId", "1384231935"));
        
        // Subsequent pages navigated via prevnextNext links - these have ansicht=1 in the snapshot HTML
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-02",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231933"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-03",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231934"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-04",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231931"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-05",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231932"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-06",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231939"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-07",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231938"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-08",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231936"));
        
        StubWithSnapshotAndParams("/ehonda-test-buli/spielinfo",
            "spielinfo-09",
            ("tippsaisonId", "3684392"),
            ("ansicht", "1"),
            ("tippspielId", "1384231937"));
        
        var client = CreateClient();

        // Act - use the actual community name from the snapshot
        var matches = await client.GetMatchesWithHistoryAsync("ehonda-test-buli");

        // Assert - should get 9 matches with history
        await Assert.That(matches).HasCount().EqualTo(9);
        
        // Verify first match details (Frankfurt vs Dortmund from spielinfo-01)
        var frankfurtMatch = matches.FirstOrDefault(m => m.Match.HomeTeam == "Eintracht Frankfurt");
        await Assert.That(frankfurtMatch).IsNotNull();
        await Assert.That(frankfurtMatch!.Match.AwayTeam).IsEqualTo("Borussia Dortmund");
        await Assert.That(frankfurtMatch.HomeTeamHistory).IsNotEmpty();
        await Assert.That(frankfurtMatch.AwayTeamHistory).IsNotEmpty();
        
        // Verify Frankfurt's history (8 recent matches)
        await Assert.That(frankfurtMatch.HomeTeamHistory).HasCount().EqualTo(8);
        
        // Verify Bayern match is present (from spielinfo-09)
        var bayernMatch = matches.FirstOrDefault(m => m.Match.HomeTeam == "FC Bayern München");
        await Assert.That(bayernMatch).IsNotNull();
        await Assert.That(bayernMatch!.Match.AwayTeam).IsEqualTo("VfL Wolfsburg");
    }
}
