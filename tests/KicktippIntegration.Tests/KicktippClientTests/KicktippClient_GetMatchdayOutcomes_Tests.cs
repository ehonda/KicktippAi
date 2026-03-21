using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

public class KicktippClient_GetMatchdayOutcomes_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_current_tippuebersicht_matchday_returns_default_when_page_is_missing()
    {
        StubNotFound("/test-community/tippuebersicht");
        var client = CreateClient();

        var matchday = await client.GetCurrentTippuebersichtMatchdayAsync("test-community");

        await Assert.That(matchday).IsEqualTo(1);
    }

    [Test]
    public async Task Getting_matchday_outcomes_returns_empty_when_page_is_missing()
    {
        StubNotFound("/test-community/tippuebersicht");
        var client = CreateClient();

        var outcomes = await client.GetMatchdayOutcomesAsync("test-community", 5);

        await Assert.That(outcomes).IsEmpty();
    }

    [Test]
    public async Task Getting_matchday_outcomes_parses_completed_pending_and_cancelled_rows()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>5. Spieltag</a></div>
            <table id="spielplanSpiele">
                <tbody>
                    <tr data-url="/test-community/spielinfo?tippspielId=101">
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td><span class="kicktipp-heim">2</span><span class="kicktipp-gast">1</span></td>
                    </tr>
                    <tr data-url="/test-community/spielinfo?tippspielId=102">
                        <td>Abgesagt</td>
                        <td>Team C</td>
                        <td>Team D</td>
                        <td><span class="kicktipp-heim"></span><span class="kicktipp-gast"></span></td>
                    </tr>
                    <tr data-url="/test-community/spielinfo?tippspielId=103">
                        <td></td>
                        <td>Team E</td>
                        <td>Team F</td>
                        <td><span class="kicktipp-heim">0</span><span class="kicktipp-gast">0</span></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "5"));
        var client = CreateClient();

        var outcomes = await client.GetMatchdayOutcomesAsync("test-community", 5);

        await Assert.That(outcomes).HasCount().EqualTo(3);

        await Assert.That(outcomes[0].Availability).IsEqualTo(MatchOutcomeAvailability.Completed);
        await Assert.That(outcomes[0].HomeGoals).IsEqualTo(2);
        await Assert.That(outcomes[0].TippSpielId).IsEqualTo("101");

        await Assert.That(outcomes[1].Availability).IsEqualTo(MatchOutcomeAvailability.Pending);
        await Assert.That(outcomes[1].HomeGoals).IsNull();
        await Assert.That(outcomes[1].TippSpielId).IsEqualTo("102");

        await Assert.That(outcomes[2].Availability).IsEqualTo(MatchOutcomeAvailability.Completed);
        await Assert.That(outcomes[2].HomeGoals).IsEqualTo(0);
        await Assert.That(outcomes[2].TippSpielId).IsEqualTo("103");
        await Assert.That(outcomes[1].StartsAt.ToInstant()).IsEqualTo(outcomes[0].StartsAt.ToInstant());
        await Assert.That(outcomes[2].StartsAt.ToInstant()).IsEqualTo(outcomes[0].StartsAt.ToInstant());
    }

    [Test]
    public async Task Getting_matchday_outcomes_uses_cache_on_second_call()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>7. Spieltag</a></div>
            <table id="spielplanSpiele">
                <tbody>
                    <tr data-url="/test-community/spielinfo?tippspielId=701">
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td><span class="kicktipp-heim">1</span><span class="kicktipp-gast">0</span></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "7"));
        var client = CreateClient();

        var first = await client.GetMatchdayOutcomesAsync("test-community", 7);
        var second = await client.GetMatchdayOutcomesAsync("test-community", 7);

        await Assert.That(first).HasCount().EqualTo(1);
        await Assert.That(second).HasCount().EqualTo(1);
        await Assert.That(GetRequestsForPath("/test-community/tippuebersicht")
                .Count(entry => entry.RequestMessage.Method == "GET"))
            .IsEqualTo(1);
    }

    [Test]
    public async Task Getting_matchday_outcomes_returns_empty_when_match_table_is_missing()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>3. Spieltag</a></div>
            <p>No table available</p>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "3"));
        var client = CreateClient();

        var outcomes = await client.GetMatchdayOutcomesAsync("test-community", 3);

        await Assert.That(outcomes).IsEmpty();
    }

    [Test]
    public async Task Getting_matchday_outcomes_uses_min_value_when_date_cannot_be_parsed()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <input name="spieltagIndex" value="4" />
            <table id="spielplanSpiele">
                <tbody>
                    <tr data-url="/test-community/spielinfo?tippspielId=401">
                        <td>not-a-date</td>
                        <td>Broken A</td>
                        <td>Broken B</td>
                        <td><span class="kicktipp-heim">2</span><span class="kicktipp-gast">1</span></td>
                    </tr>
                    <tr data-url="/test-community/spielinfo?tippspielId=402">
                        <td>22.08.25 20:30</td>
                        <td>Valid A</td>
                        <td>Valid B</td>
                        <td><span class="kicktipp-heim">3</span><span class="kicktipp-gast">2</span></td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "4"));
        var client = CreateClient();

        var outcomes = await client.GetMatchdayOutcomesAsync("test-community", 4);

        await Assert.That(outcomes).HasCount().EqualTo(2);
        await Assert.That(outcomes[0].HomeTeam).IsEqualTo("Broken A");
        await Assert.That(outcomes[0].StartsAt.ToDateTimeOffset()).IsEqualTo(DateTimeOffset.MinValue);
        await Assert.That(outcomes[1].HomeTeam).IsEqualTo("Valid A");
        await Assert.That(await client.GetCurrentTippuebersichtMatchdayAsync("test-community")).IsEqualTo(1);
    }
}
