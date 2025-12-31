using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetHeadToHeadHistoryAsync and GetHeadToHeadDetailedHistoryAsync methods.
/// </summary>
public class KicktippClient_GetHeadToHead_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_head_to_head_returns_empty_list_on_tippabgabe_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(history).IsEmpty();
    }

    [Test]
    public async Task Getting_head_to_head_returns_empty_list_when_spielinfo_link_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="content"><p>No spielinfo</p></div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(history).IsEmpty();
    }

    [Test]
    public async Task Getting_head_to_head_uses_ansicht_3_parameter()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        
        // The client should append ansicht=3 for head-to-head view
        // Only respond if the exact parameter is used
        StubHtmlResponseWithParams("/test-community/spielinfo",
            LoadSyntheticFixtureContent("test-community", "spielinfo-head-to-head"),
            ("tippspielId", "1"),
            ("ansicht", "3"));
        
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadHistoryAsync("test-community", "Team Alpha", "Team Beta");

        // Assert
        await Assert.That(history).IsNotEmpty();
    }

    [Test]
    public async Task Getting_detailed_head_to_head_parses_annotations()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubHtmlResponseWithParams("/test-community/spielinfo",
            LoadSyntheticFixtureContent("test-community", "spielinfo-head-to-head"),
            ("tippspielId", "1"),
            ("ansicht", "3"));
        
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadDetailedHistoryAsync("test-community", "Team Alpha", "Team Beta");

        // Assert
        await Assert.That(history).HasCount().EqualTo(3);
        
        // First match has no annotation
        await Assert.That(history[0].Annotation).IsNull();
        
        // Second match has "nach Verlängerung"
        await Assert.That(history[1].Annotation).IsEqualTo("nach Verlängerung");
        
        // Third match has "nach Elfmeterschießen"
        await Assert.That(history[2].Annotation).IsEqualTo("nach Elfmeterschießen");
    }

    [Test]
    public async Task Getting_head_to_head_navigates_to_find_correct_match()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        
        // First page doesn't have the match we're looking for
        var firstPageHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext"><a href="/test-community/spielinfo?tippspielId=2"><span class="kicktipp-icon-arrow-right"></span></a></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Wrong Team 1</td>
                        <td>Wrong Team 2</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", firstPageHtml, 
            ("tippspielId", "1"), ("ansicht", "3"));
        
        // Second page has the match
        StubHtmlResponseWithParams("/test-community/spielinfo",
            LoadSyntheticFixtureContent("test-community", "spielinfo-head-to-head"),
            ("tippspielId", "2"),
            ("ansicht", "3"));
        
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadHistoryAsync("test-community", "Team Alpha", "Team Beta");

        // Assert
        await Assert.That(history).IsNotEmpty();
    }

    [Test]
    public async Task Getting_head_to_head_returns_empty_when_match_not_found()
    {
        // Arrange
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        
        // Page doesn't have the match and no next link
        var pageHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext disabled"><a></a></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Other Team</td>
                        <td>Another Team</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", pageHtml,
            ("tippspielId", "1"), ("ansicht", "3"));
        
        var client = CreateClient();

        // Act
        var history = await client.GetHeadToHeadHistoryAsync("test-community", "NonExistent Home", "NonExistent Away");

        // Assert
        await Assert.That(history).IsEmpty();
    }

    [Test]
    public async Task Getting_head_to_head_with_real_fixture_returns_match_history()
    {
        // Arrange - use encrypted real fixtures for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        
        // The tippabgabe page provides team names
        StubWithRealFixture(community, "tippabgabe");
        
        // First, get matches to find valid team names from the fixture
        var client = CreateClient();
        var matches = await client.GetOpenPredictionsAsync(community);
        var firstMatch = matches.First();
        
        // Stub the spielinfo page with ansicht=3 for head-to-head history
        // Uses the -h2h fixture variant which contains the actual head-to-head view data
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-01-h2h",
            ("tippsaisonId", "3684392"),
            ("tippspielId", "1384231935"),
            ("ansicht", "3"));

        // Act
        var history = await client.GetHeadToHeadHistoryAsync(
            community,
            firstMatch.HomeTeam,
            firstMatch.AwayTeam);

        // Assert - head-to-head history may be empty if teams haven't played before
        // But if there is history, it should be valid
        foreach (var result in history)
        {
            await Assert.That(result.HomeTeam).IsNotEmpty();
            await Assert.That(result.AwayTeam).IsNotEmpty();
            // Goals should be non-null and non-negative for historical matches
            await Assert.That(result.HomeGoals).IsNotNull();
            await Assert.That(result.AwayGoals).IsNotNull();
            await Assert.That(result.HomeGoals!.Value).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.AwayGoals!.Value).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Getting_detailed_head_to_head_with_real_fixture_returns_match_history_with_annotations()
    {
        // Arrange - use encrypted real fixtures for the ehonda-test-buli community
        const string community = "ehonda-test-buli";
        
        // The tippabgabe page provides team names
        StubWithRealFixture(community, "tippabgabe");
        
        // First, get matches to find valid team names from the fixture
        var client = CreateClient();
        var matches = await client.GetOpenPredictionsAsync(community);
        var firstMatch = matches.First();
        
        // Stub the spielinfo page with ansicht=3 for head-to-head history
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-01-h2h",
            ("tippsaisonId", "3684392"),
            ("tippspielId", "1384231935"),
            ("ansicht", "3"));

        // Act
        var history = await client.GetHeadToHeadDetailedHistoryAsync(
            community,
            firstMatch.HomeTeam,
            firstMatch.AwayTeam);

        // Assert - detailed history includes annotations
        foreach (var result in history)
        {
            await Assert.That(result.HomeTeam).IsNotEmpty();
            await Assert.That(result.AwayTeam).IsNotEmpty();
            await Assert.That(result.Score).IsNotEmpty();
            // Annotation can be null (no extra time/penalties) or a string
        }
    }
}

