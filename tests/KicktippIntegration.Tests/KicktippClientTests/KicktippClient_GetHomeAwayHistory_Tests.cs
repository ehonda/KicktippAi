using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetHomeAwayHistoryAsync method.
/// </summary>
public class KicktippClient_GetHomeAwayHistory_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_home_away_history_returns_empty_on_tippabgabe_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(homeHistory).IsEmpty();
        await Assert.That(awayHistory).IsEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_uses_ansicht_2_parameter()
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
        
        // Only respond if ansicht=2 is used (home/away history view)
        var spielinfoHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Home Team</td>
                        <td>Away Team</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            <div class="spielinfo-table">
                <h2>Home Team</h2>
                <table class="spielinfoHeim">
                    <tbody>
                        <tr>
                            <td>1.BL</td>
                            <td class="nw sieg">Home Team</td>
                            <td class="nw">Opponent A</td>
                            <td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">2</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">0</span></span></span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div class="spielinfo-table">
                <h2>Away Team</h2>
                <table class="spielinfoGast">
                    <tbody>
                        <tr>
                            <td>1.BL</td>
                            <td class="nw">Opponent B</td>
                            <td class="nw niederlage">Away Team</td>
                            <td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">1</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">0</span></span></span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", spielinfoHtml,
            ("tippspielId", "1"), ("ansicht", "2"));
        
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Home Team", "Away Team");

        // Assert
        await Assert.That(homeHistory).IsNotEmpty();
        await Assert.That(awayHistory).IsNotEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_extracts_correct_history_tables()
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
        
        var spielinfoHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>FC Bayern</td>
                        <td>BVB</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            <div class="spielinfo-table">
                <table class="spielinfoHeim">
                    <tbody>
                        <tr>
                            <td>1.BL</td>
                            <td class="nw sieg">FC Bayern</td>
                            <td class="nw">Opponent</td>
                            <td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">3</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">1</span></span></span></td>
                        </tr>
                        <tr>
                            <td>CL</td>
                            <td class="nw remis">FC Bayern</td>
                            <td class="nw">Another</td>
                            <td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">2</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">2</span></span></span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
            <div class="spielinfo-table">
                <table class="spielinfoGast">
                    <tbody>
                        <tr>
                            <td>DFB</td>
                            <td class="nw">Team X</td>
                            <td class="nw niederlage">BVB</td>
                            <td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">4</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">2</span></span></span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", spielinfoHtml,
            ("tippspielId", "1"), ("ansicht", "2"));
        
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "FC Bayern", "BVB");

        // Assert
        await Assert.That(homeHistory).IsEquivalentTo([
            new MatchResult("1.BL", "FC Bayern", "Opponent", 3, 1, MatchOutcome.Win),
            new MatchResult("CL", "FC Bayern", "Another", 2, 2, MatchOutcome.Draw)
        ]);
        
        await Assert.That(awayHistory).IsEquivalentTo([
            new MatchResult("DFB", "Team X", "BVB", 4, 2, MatchOutcome.Loss)
        ]);
    }

    [Test]
    public async Task Getting_home_away_history_navigates_to_find_match()
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
        
        // First page - wrong match
        var firstPage = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext"><a href="/test-community/spielinfo?tippspielId=2"><span class="kicktipp-icon-arrow-right"></span></a></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Wrong Home</td>
                        <td>Wrong Away</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", firstPage,
            ("tippspielId", "1"), ("ansicht", "2"));
        
        // Second page - correct match
        var secondPage = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext disabled"></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Target Home</td>
                        <td>Target Away</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            <table class="spielinfoHeim"><tbody><tr><td>1.BL</td><td class="nw">Target Home</td><td class="nw">X</td><td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">1</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">0</span></span></span></td></tr></tbody></table>
            <table class="spielinfoGast"><tbody><tr><td>1.BL</td><td class="nw">Y</td><td class="nw">Target Away</td><td><span class="kicktipp-ergebnis"><span class="kicktipp-abschnitt kicktipp-abpfiff"><span class="kicktipp-heim">0</span><span class="kicktipp-tortrenner">:</span><span class="kicktipp-gast">1</span></span></span></td></tr></tbody></table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/spielinfo", secondPage,
            ("tippspielId", "2"), ("ansicht", "2"));
        
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Target Home", "Target Away");

        // Assert
        await Assert.That(homeHistory).IsNotEmpty();
        await Assert.That(awayHistory).IsNotEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_returns_empty_when_match_not_found()
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
        
        var page = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext disabled"></div>
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
        StubHtmlResponseWithParams("/test-community/spielinfo", page,
            ("tippspielId", "1"), ("ansicht", "2"));
        
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "NonExistent", "Missing");

        // Assert
        await Assert.That(homeHistory).IsEmpty();
        await Assert.That(awayHistory).IsEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_with_real_fixture_returns_team_histories()
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
        
        // Stub the spielinfo page with ansicht=2 for home/away history
        // Uses the -homeaway fixture variant which contains the actual home/away view data
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-01-homeaway",
            ("tippsaisonId", "3684392"),
            ("tippspielId", "1384231935"),
            ("ansicht", "2"));

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync(
            community,
            firstMatch.HomeTeam,
            firstMatch.AwayTeam);

        // Assert - should have history for both teams
        // History typically contains up to 8 recent matches
        await Assert.That(homeHistory.Count).IsGreaterThan(0);
        await Assert.That(awayHistory.Count).IsGreaterThan(0);
        
        // All history entries should have valid match results
        foreach (var result in homeHistory)
        {
            await Assert.That(result.HomeTeam).IsNotEmpty();
            await Assert.That(result.AwayTeam).IsNotEmpty();
            // Goals are nullable (null if match not yet played)
            // For history, they should be non-null and non-negative
            await Assert.That(result.HomeGoals).IsNotNull();
            await Assert.That(result.AwayGoals).IsNotNull();
            await Assert.That(result.HomeGoals!.Value).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.AwayGoals!.Value).IsGreaterThanOrEqualTo(0);
        }
        
        foreach (var result in awayHistory)
        {
            await Assert.That(result.HomeTeam).IsNotEmpty();
            await Assert.That(result.AwayTeam).IsNotEmpty();
            await Assert.That(result.HomeGoals).IsNotNull();
            await Assert.That(result.AwayGoals).IsNotNull();
            await Assert.That(result.HomeGoals!.Value).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.AwayGoals!.Value).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Getting_home_away_history_returns_empty_when_spielinfo_link_has_empty_href()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <a href="">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(homeHistory).IsEmpty();
        await Assert.That(awayHistory).IsEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_returns_empty_when_spielinfo_returns_404()
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
        StubNotFoundWithParams("/test-community/spielinfo", ("tippspielId", "1"), ("ansicht", "2"));
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(homeHistory).IsEmpty();
        await Assert.That(awayHistory).IsEmpty();
    }

    [Test]
    public async Task Getting_home_away_history_returns_empty_when_spielinfo_link_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="content"><p>No spielinfo link here</p></div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var (homeHistory, awayHistory) = await client.GetHomeAwayHistoryAsync("test-community", "Team A", "Team B");

        // Assert
        await Assert.That(homeHistory).IsEmpty();
        await Assert.That(awayHistory).IsEmpty();
    }
}
