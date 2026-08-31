using Microsoft.Extensions.Caching.Memory;
using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetMatchesWithHistoryAsync method.
/// </summary>
public class KicktippClient_GetMatchesWithHistory_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Competition_cancellation_overload_is_a_default_interface_member_for_existing_IKicktippClient_implementations()
    {
        var method = typeof(IKicktippClient).GetMethod(
            nameof(IKicktippClient.GetOpenPredictionsAsync),
            [typeof(string), typeof(string), typeof(CancellationToken)]);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.IsAbstract).IsFalse();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_history_returns_true_empty_before_requesting_spielinfo_or_outcomes()
    {
        StubHtmlResponse("/schadensfresse/tippabgabe", "<html><head><title>Kicktipp</title></head><body><table id=\"tippabgabeSpiele\"><tbody><tr><td>30.08.26 15:30</td><td>SC Freiburg</td><td>Werder Bremen</td><td>geschlossen</td></tr></tbody></table></body></html>");
        var client = CreateClient();

        var matches = await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27);

        await Assert.That(matches).IsEmpty();
        await Assert.That(GetRequestsForPath("/schadensfresse/spielinfo")).IsEmpty();
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht")).IsEmpty();
    }

    [Test]
    [Arguments("https://example.invalid/schadensfresse/spielinfo?tippspielId=1662323366")]
    [Arguments("/schadensfresse/not-spielinfo?tippspielId=1662323366")]
    [Arguments("/schadensfresse/spielinfo")]
    [Arguments("/schadensfresse/spielinfo?tippspielId=1662323366&unexpected=1")]
    [Arguments("/schadensfresse/spielinfo?tippspielId=1662323366&tippspielId=1662323366")]
    [Arguments("/schadensfresse/spielinfo?tippspielId=1662323366#fragment")]
    public async Task Getting_schadensfresse_Bundesliga_history_rejects_an_invalid_spielinfo_source_before_request(string href)
    {
        StubSchadensfresseHistoryTippabgabe(href);
        var client = CreateClient();

        await Assert.That(async () => await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
        await Assert.That(GetRequestsForPath("/schadensfresse/spielinfo")).IsEmpty();
    }

    [Test]
    [Arguments(201, false)]
    [Arguments(200, true)]
    public async Task Getting_schadensfresse_Bundesliga_history_rejects_non_200_or_login_spielinfo_responses(int status, bool login)
    {
        StubSchadensfresseHistoryTippabgabe("/schadensfresse/spielinfo?tippspielId=1662323366");
        var body = login
            ? "<html><head><title>Login</title></head><body><form id=\"loginFormular\"></form></body></html>"
            : "<html><body></body></html>";
        Server.Given(WireMock.RequestBuilders.Request.Create().WithPath("/schadensfresse/spielinfo").UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create().WithStatusCode(status).WithBody(body));
        var client = CreateClient();

        await Assert.That(async () => await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_history_rejects_a_non_200_success_tippabgabe_response()
    {
        StubStatusCode("/schadensfresse/tippabgabe", 201);
        var client = CreateClient();

        await Assert.That(async () => await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_history_rejects_a_redirect_that_changes_the_spielinfo_fixture_ID()
    {
        const string fixtureId = "1662323366";
        StubSchadensfresseHistoryTippabgabe($"/schadensfresse/spielinfo?tippspielId={fixtureId}");
        Server.Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/schadensfresse/spielinfo")
                .WithParam("tippspielId", new WireMock.Matchers.ExactMatcher(fixtureId))
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location", $"{ServerUrl}/schadensfresse/spielinfo?tippspielId=1662323362"));
        StubHtmlResponseWithParams("/schadensfresse/spielinfo", "<html><body></body></html>", ("tippspielId", "1662323362"));
        var client = CreateClient();

        await Assert.That(async () => await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
    }

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_history_rejects_looping_navigation_before_a_repeated_request()
    {
        const string fixtureId = "1662323366";
        StubSchadensfresseHistoryTippabgabe($"/schadensfresse/spielinfo?tippspielId={fixtureId}");
        StubHtmlResponseWithParams(
            "/schadensfresse/spielinfo",
            $"<html><body><table class=\"tippabgabe\"><tbody><tr><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td><td><input type=\"text\"/><input type=\"text\"/></td></tr></tbody></table><div class=\"prevnextNext\"><a href=\"/schadensfresse/spielinfo?tippspielId={fixtureId}\">next</a></div></body></html>",
            ("tippspielId", fixtureId));
        var client = CreateClient();

        await Assert.That(async () => await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27))
            .Throws<KicktippFixtureIdentityException>();
        await Assert.That(GetRequestsForPath("/schadensfresse/spielinfo")).Count().IsEqualTo(1);
    }

    private void StubSchadensfresseHistoryTippabgabe(string spielinfoHref) =>
        StubHtmlResponse(
            "/schadensfresse/tippabgabe",
            $"<html><head><title>Kicktipp</title></head><body><input name=\"spieltagIndex\" value=\"1\"/><table id=\"tippabgabeSpiele\"><tbody><tr><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td><td><input type=\"text\" name=\"spieltippForms[1662323366][heim]\"/><input type=\"text\" name=\"spieltippForms[1662323366][gast]\"/></td></tr></tbody></table><a href=\"{spielinfoHref}\">Spielinfos</a></body></html>");

    [Test]
    public async Task Getting_schadensfresse_Bundesliga_match_history_carries_the_exact_seed_backed_fixture_IDs_to_production_matches()
    {
        const string firstId = "1662323362";
        const string secondId = "1662323366";
        var tippabgabe = $$"""
            <html><head><title>Kicktipp</title></head><body><input name="spieltagIndex" value="1" />
            <table id="tippabgabeSpiele"><tbody>
            <tr><td>30.08.26 15:30</td><td>SC Freiburg</td><td>Werder Bremen</td><td><input type="text" name="spieltippForms[{{firstId}}][heim]"/><input type="text" name="spieltippForms[{{firstId}}][gast]"/></td></tr>
            <tr><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td><td><input type="text" name="spieltippForms[{{secondId}}][heim]"/><input type="text" name="spieltippForms[{{secondId}}][gast]"/></td></tr>
            </tbody></table><a href="/schadensfresse/spielinfo?tippspielId={{firstId}}">Spielinfos</a></body></html>
            """;
        var firstSpielinfo = """
            <html><body><table class="tippabgabe"><tbody><tr><td>30.08.26 15:30</td><td>SC Freiburg</td><td>Werder Bremen</td><td><input type="text"/><input type="text"/></td></tr></tbody></table>
            <div class="prevnextNext"><a href="/schadensfresse/spielinfo?tippspielId=1662323366">next</a></div></body></html>
            """;
        var secondSpielinfo = """
            <html><body><table class="tippabgabe"><tbody><tr><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td><td><input type="text"/><input type="text"/></td></tr></tbody></table>
            <div class="prevnextNext disabled"><a></a></div></body></html>
            """;
        var outcomes = $$"""
            <html><body><table id="spielplanSpiele"><tbody>
            <tr class="clickable" data-url="/schadensfresse/tippuebersicht/spiel?tippsaisonId=5746822&amp;spieltagIndex=1&amp;tippspielId={{firstId}}"><td>30.08.26 15:30</td><td>SC Freiburg</td><td>Werder Bremen</td></tr>
            <tr class="clickable" data-url="/schadensfresse/tippuebersicht/spiel?tippsaisonId=5746822&amp;spieltagIndex=1&amp;tippspielId={{secondId}}"><td>30.08.26 17:30</td><td>FC Augsburg</td><td>FC Schalke 04</td></tr>
            </tbody></table></body></html>
            """;
        static string Detail(string time) => $"<html><body><div><span class=\"spieldaten-infos-label\">Wettbewerb</span><span class=\"spieldaten-infos-value\">1. Bundesliga 2026/27</span></div><div><span class=\"spieldaten-infos-label\">Spieltag</span><span class=\"spieldaten-infos-value\">1. Spieltag</span></div><div><span class=\"spieldaten-infos-label\">Termin</span><span class=\"spieldaten-infos-value\">{time}</span></div><div><span class=\"spieldaten-infos-label\">Tipptermin</span><span class=\"spieldaten-infos-value\">{time}</span></div></body></html>";

        StubHtmlResponse("/schadensfresse/tippabgabe", tippabgabe);
        StubHtmlResponseWithParams("/schadensfresse/spielinfo", firstSpielinfo, ("tippspielId", firstId));
        StubHtmlResponseWithParams("/schadensfresse/spielinfo", secondSpielinfo, ("tippspielId", secondId));
        StubHtmlResponseWithParams("/schadensfresse/tippuebersicht", outcomes, ("spieltagIndex", "1"));
        StubHtmlResponseWithParams("/schadensfresse/tippuebersicht/spiel", Detail("30.08.26 15:30"), ("tippsaisonId", "5746822"), ("spieltagIndex", "1"), ("tippspielId", firstId));
        StubHtmlResponseWithParams("/schadensfresse/tippuebersicht/spiel", Detail("30.08.26 17:30"), ("tippsaisonId", "5746822"), ("spieltagIndex", "1"), ("tippspielId", secondId));
        var client = CreateClient();

        var matches = await client.GetMatchesWithHistoryAsync("schadensfresse", CompetitionIds.Bundesliga2026_27);

        await Assert.That(matches.Select(item => item.Match.KicktippFixtureId)).IsEquivalentTo([firstId, secondId]);
        await Assert.That(matches.All(item => item.Match.KicktippRoundName == "1. Spieltag" &&
                                             item.Match.BundesligaSeasonSubcompetition == BundesligaSeasonSubcompetition.Bundesliga &&
                                             item.Match.ResultBasis == ResultBasis.RegularTime90Minutes)).IsTrue();
        await Assert.That(GetRequestsForPath("/schadensfresse/tippabgabe")).Count().IsEqualTo(1);
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht")).Count().IsEqualTo(1);
        await Assert.That(GetRequestsForPath("/schadensfresse/tippuebersicht/spiel")).Count().IsEqualTo(2);
        await Assert.That(string.Join(",", GetRequestsForPath("/schadensfresse/tippuebersicht/spiel")
                .Select(entry => entry.RequestMessage.Query!["tippspielId"].Single())))
            .IsEqualTo($"{firstId},{secondId}");
        await Assert.That(string.Join(",", Server.LogEntries.Select(entry => entry.RequestMessage.Path)))
            .IsEqualTo("/schadensfresse/tippabgabe,/schadensfresse/spielinfo,/schadensfresse/spielinfo,/schadensfresse/tippuebersicht,/schadensfresse/tippuebersicht/spiel,/schadensfresse/tippuebersicht/spiel");
    }

    [Test]
    public async Task Getting_world_cup_match_history_includes_equivalent_knockout_data()
    {
        var tippabgabeHtml = """
            <!DOCTYPE html><html><body>
            <input type="hidden" name="spieltagIndex" value="37" />
            <div class="spieltagsauswahl"><div class="prevnextTitle"><a>Sechzehntelfinale</a></div></div>
            <table id="tippabgabeSpiele"><tbody><tr>
                <td>28.06.26 21:00</td><td>South Africa</td><td>Canada</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td>
            </tr></tbody></table>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body></html>
            """;
        var spielinfoHtml = """
            <!DOCTYPE html><html><body>
            <table class="tippabgabe"><tbody><tr>
                <td>28.06.26 21:00</td><td>South Africa</td><td>Canada</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td>
            </tr></tbody></table>
            <div class="prevnextNext disabled"><a></a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubHtmlResponse("/test-community/spielinfo", spielinfoHtml);
        var client = CreateClient();

        var matches = await client.GetMatchesWithHistoryAsync("test-community", CompetitionIds.FifaWorldCup2026);

        await Assert.That(matches).HasCount().EqualTo(1);
        var data = matches[0].Match.CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.RoundOf32);
        await Assert.That(data.KicktippRoundName).IsEqualTo("Sechzehntelfinale");
    }

    [Test]
    public async Task Getting_world_cup_match_history_uses_known_round_without_penalty_marker()
    {
        var tippabgabeHtml = """
            <!DOCTYPE html><html><body>
            <input type="hidden" name="spieltagIndex" value="37" />
            <div class="spieltagsauswahl"><div class="prevnextTitle"><a>Sechzehntelfinale</a></div></div>
            <table id="tippabgabeSpiele"><tbody>
                <tr>
                    <td>28.06.26 21:00</td><td>South Africa</td><td>Canada</td><td>0:1</td>
                </tr>
                <tr>
                    <td>02.07.26 02:00</td><td>USA</td><td>Bosnia-Herzegovina</td><td>
                        <input type="text" /><input type="text" />
                    </td>
                </tr>
            </tbody></table>
            <a href="/test-community/spielinfo?tippspielId=2">Tippabgabe mit Spielinfos</a>
            </body></html>
            """;
        var spielinfoHtml = """
            <!DOCTYPE html><html><body>
            <table class="tippabgabe"><tbody><tr>
                <td>02.07.26 02:00</td><td>USA</td><td>Bosnia-Herzegovina</td><td>
                    <input type="text" /><input type="text" />
                </td>
            </tr></tbody></table>
            <div class="prevnextNext disabled"><a></a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubHtmlResponse("/test-community/spielinfo", spielinfoHtml);
        var client = CreateClient();

        var matches = await client.GetMatchesWithHistoryAsync("test-community", CompetitionIds.FifaWorldCup2026);

        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Match.HomeTeam).IsEqualTo("USA");
        await Assert.That(matches[0].Match.AwayTeam).IsEqualTo("Bosnia-Herzegovina");
        var data = matches[0].Match.CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.RoundOf32);
        await Assert.That(data.ResultBasis)
            .IsEqualTo(FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout);
    }

    [Test]
    public async Task Getting_world_cup_match_history_distinguishes_matches_in_shared_finale_round()
    {
        var tippabgabeHtml = """
            <!DOCTYPE html><html><body>
            <input type="hidden" name="spieltagIndex" value="15" />
            <div class="spieltagsauswahl"><div class="prevnextTitle"><a>Finale</a></div></div>
            <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body></html>
            """;
        var thirdPlaceHtml = """
            <!DOCTYPE html><html><body>
            <table class="tippabgabe"><tbody><tr>
                <td>18.07.26 23:00</td><td>France</td><td>England</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td>
            </tr></tbody></table>
            <div class="prevnextNext"><a href="/test-community/spielinfo?tippspielId=2">Next</a></div>
            </body></html>
            """;
        var finalHtml = """
            <!DOCTYPE html><html><body>
            <table class="tippabgabe"><tbody><tr>
                <td>19.07.26 21:00</td><td>Spain</td><td>Argentina</td><td>
                    <span class="kicktipp-spielabschnitt-markierung">n.E.</span>
                    <input type="text" /><input type="text" />
                </td>
            </tr></tbody></table>
            <div class="prevnextNext disabled"><a></a></div>
            </body></html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", tippabgabeHtml);
        StubHtmlResponseWithParams(
            "/test-community/spielinfo", thirdPlaceHtml, ("tippspielId", "1"));
        StubHtmlResponseWithParams(
            "/test-community/spielinfo", finalHtml, ("tippspielId", "2"));
        var client = CreateClient();

        var matches = await client.GetMatchesWithHistoryAsync(
            "test-community", CompetitionIds.FifaWorldCup2026);

        var thirdPlaceData = matches.Single(item => item.Match.HomeTeam == "France")
            .Match.CompetitionSpecificData as FifaWorldCup2026MatchData;
        var finalData = matches.Single(item => item.Match.HomeTeam == "Spain")
            .Match.CompetitionSpecificData as FifaWorldCup2026MatchData;
        await Assert.That(thirdPlaceData!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.ThirdPlacePlayoff);
        await Assert.That(finalData!.Stage).IsEqualTo(FifaWorldCup2026KnockoutStage.Final);
    }

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
        StubWithSyntheticFixture("/test-community/spielinfo", "test-community", "spielinfo-first");
        
        // Second spielinfo page with disabled next (last page)
        StubHtmlResponseWithParams("/test-community/spielinfo", 
            LoadSyntheticFixtureContent("test-community", "spielinfo-last"),
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
    public async Task Getting_matches_with_history_for_matchday_fetches_matchday_tippabgabe_page()
    {
        // Arrange - set up matchday-specific tippabgabe with spielinfo link
        var tippabgabeHtml = """
            <!DOCTYPE html>
            <html>
            <body>
                <div class="prevnextTitle"><a>2. Spieltag</a></div>
                <a href="/test-community/spielinfo?tippspielId=1">Tippabgabe mit Spielinfos</a>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippabgabe",
            tippabgabeHtml,
            ("spieltagIndex", "2"));
        StubWithSyntheticFixture("/test-community/spielinfo", "test-community", "spielinfo-last");

        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community", 2);

        // Assert
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Match.Matchday).IsEqualTo(2);
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
        StubWithSyntheticFixture("/test-community/spielinfo", "test-community", "spielinfo-last");
        
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
        StubWithSyntheticFixture("/test-community/spielinfo", "test-community", "spielinfo-last");
        
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
    public async Task Getting_matches_with_history_with_real_fixtures_returns_matches_with_history()
    {
        // Arrange - use encrypted real fixtures for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        
        // The tippabgabe page contains links to spielinfo pages
        StubWithRealFixture(community, "tippabgabe");
        
        // Setup all 9 spielinfo pages (matchday typically has 9 matches)
        // First spielinfo page - no ansicht parameter in the initial link
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-01",
            ("tippsaisonId", "3684392"),
            ("tippspielId", "1384231935"));
        
        // Subsequent pages have ansicht=1
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-02",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231933"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-03",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231934"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-04",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231931"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-05",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231932"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-06",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231939"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-07",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231938"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-08",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231936"));
        StubWithRealFixtureAndParams($"/{community}/spielinfo", community, "spielinfo-09",
            ("tippsaisonId", "3684392"), ("ansicht", "1"), ("tippspielId", "1384231937"));
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync(community);

        // Assert - Bundesliga matchday has 9 matches
        await Assert.That(matches).HasCount().EqualTo(9);
        
        // All matches should have valid structure
        foreach (var matchWithHistory in matches)
        {
            // Match details should be valid
            await Assert.That(matchWithHistory.Match.HomeTeam).IsNotEmpty();
            await Assert.That(matchWithHistory.Match.AwayTeam).IsNotEmpty();
            await Assert.That(matchWithHistory.Match.Matchday).IsGreaterThan(0);
            
            // History should be populated
            await Assert.That(matchWithHistory.HomeTeamHistory).IsNotNull();
            await Assert.That(matchWithHistory.AwayTeamHistory).IsNotNull();
            
            // History should contain recent matches (typically up to 8)
            await Assert.That(matchWithHistory.HomeTeamHistory.Count).IsGreaterThan(0);
            await Assert.That(matchWithHistory.AwayTeamHistory.Count).IsGreaterThan(0);
        }
        
        // All matches should be in the same matchday
        var matchdays = matches.Select(m => m.Match.Matchday).Distinct().ToList();
        await Assert.That(matchdays).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Getting_matches_with_history_returns_empty_list_when_spielinfo_link_has_empty_href()
    {
        // Arrange - tippabgabe page with spielinfo link but empty href
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <a href="">Tippabgabe mit Spielinfos</a>
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
    public async Task Getting_matches_with_history_returns_empty_on_exception_in_extraction()
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
        
        // Spielinfo page with malformed content that may cause issues
        var malformedHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextNext disabled"></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td></td>
                        <td></td>
                        <td></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/spielinfo", malformedHtml);
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert - should return empty list gracefully, skipping the malformed match
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_matches_with_history_stops_on_spielinfo_404_during_navigation()
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
        
        // First spielinfo page with next link pointing to non-existent page
        var firstPageHtml = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <div class="prevnextNext"><a href="/test-community/spielinfo?tippspielId=2"><span class="kicktipp-icon-arrow-right"></span></a></div>
            <table class="tippabgabe">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Home Team 1</td>
                        <td>Away Team 1</td>
                        <td><input type="text" /><input type="text" /></td>
                    </tr>
                </tbody>
            </table>
            <table class="spielinfoHeim"><tbody></tbody></table>
            <table class="spielinfoGast"><tbody></tbody></table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/spielinfo", firstPageHtml);
        
        // Second spielinfo page returns 404
        StubNotFoundWithParams("/test-community/spielinfo", ("tippspielId", "2"));
        
        var client = CreateClient();

        // Act
        var matches = await client.GetMatchesWithHistoryAsync("test-community");

        // Assert - should return the first match and stop gracefully
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].Match.HomeTeam).IsEqualTo("Home Team 1");
    }
}
