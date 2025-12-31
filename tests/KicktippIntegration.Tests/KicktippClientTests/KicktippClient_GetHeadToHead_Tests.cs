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

    // NOTE: Real fixture test for GetHeadToHeadHistoryAsync is not included because
    // the kicktipp-snapshots directory does not contain ansicht=3 (Direkter Vergleich) pages.
    // The existing snapshots (spielinfo-01 through spielinfo-09) are all ansicht=1 (Ergebnisse).
    // To add a real fixture test, capture a page with URL like:
    // /community/spielinfo?tippsaisonId=xxx&tippspielId=xxx&ansicht=3
}
