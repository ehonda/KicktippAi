using EHonda.KicktippAi.Core;
using NodaTime;

namespace KicktippIntegration.Tests.KicktippClientTests;

public class KicktippClient_PlaceBets_KnownFailures_Tests : KicktippClientTests_Base
{
    private static Match CreateTestMatch(string homeTeam = "Team A", string awayTeam = "Team B", int matchday = 1)
    {
        var instant = Instant.FromUtc(2025, 8, 22, 20, 30);
        var zone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
        var zonedDateTime = instant.InZone(zone);
        return new Match(homeTeam, awayTeam, zonedDateTime, matchday);
    }

    [Test]
    public async Task Placing_bets_returns_false_on_draw_not_allowed_banner()
    {
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse(
            "/test-community/tippabgabe",
            responseBody: LoadSyntheticFixtureContent("test-community", "tippabgabe-unentschieden-nicht-moeglich"));

        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(1, 1) }
        };

        var result = await client.PlaceBetsAsync("test-community", bets);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_returns_false_on_draw_not_allowed_query_flag()
    {
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");

        Server
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/test-community/tippabgabe")
                .UsingPost())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(302)
                .WithHeader(
                    "Location",
                    $"{ServerUrl}/test-community/tippabgabe?unentschiedenNichtMoeglich=true&spieltagIndex=11&bonus=false&bannerTippschein=true&tippsaisonId=4503298"));

        StubHtmlResponseWithParams(
            "/test-community/tippabgabe",
            LoadSyntheticFixtureContent("test-community", "tippabgabe-unentschieden-nicht-moeglich"),
            ("unentschiedenNichtMoeglich", "true"),
            ("spieltagIndex", "11"),
            ("bonus", "false"),
            ("bannerTippschein", "true"),
            ("tippsaisonId", "4503298"));

        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(1, 1) }
        };

        var result = await client.PlaceBetsAsync("test-community", bets);

        await Assert.That(result).IsFalse();
    }
}
