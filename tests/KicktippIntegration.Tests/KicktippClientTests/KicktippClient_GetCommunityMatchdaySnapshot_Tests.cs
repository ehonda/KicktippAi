using KicktippIntegration;

namespace KicktippIntegration.Tests.KicktippClientTests;

public class KicktippClient_GetCommunityMatchdaySnapshot_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_snapshot_ignores_points_subscript_when_parsing_scoreline()
    {
        StubWithSyntheticFixtureAndParams(
            "/ehonda-ai-arena/tippuebersicht",
            "ehonda-ai-arena",
            "tippuebersicht-score-subscript-regression",
            ("spieltagIndex", "1"));
        var client = CreateClient();

        var snapshot = await client.GetCommunityMatchdaySnapshotAsync("ehonda-ai-arena", 1);

        await Assert.That(snapshot).IsNotNull();
        var participant = snapshot!.Participants.Single(candidate => candidate.DisplayName == "gpt-5");
        await Assert.That(participant.Predictions).HasCount().EqualTo(1);
        await Assert.That(participant.Predictions[0].Status).IsEqualTo(KicktippCommunityPredictionStatus.Placed);
        await Assert.That(participant.Predictions[0].Prediction).IsNotNull();
        await Assert.That(participant.Predictions[0].Prediction!.HomeGoals).IsEqualTo(3);
        await Assert.That(participant.Predictions[0].Prediction!.AwayGoals).IsEqualTo(1);
        await Assert.That(participant.Predictions[0].AwardedPoints).IsEqualTo(2);
    }

    [Test]
    public async Task Getting_snapshot_parses_finished_match_predictions_and_missed_cells()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>6. Spieltag</a></div>
            <table id="spielplanSpiele">
                <tbody>
                    <tr data-url="/test-community/tippuebersicht/spiel?tippspielId=101">
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td><span class="kicktipp-heim">2</span><span class="kicktipp-gast">1</span></td>
                    </tr>
                    <tr data-url="/test-community/tippuebersicht/spiel?tippspielId=102">
                        <td>23.08.25 15:30</td>
                        <td>Team C</td>
                        <td>Team D</td>
                        <td><span class="kicktipp-heim"></span><span class="kicktipp-gast"></span></td>
                    </tr>
                    <tr data-url="/test-community/tippuebersicht/spiel?tippspielId=103">
                        <td>24.08.25 17:30</td>
                        <td>Team E</td>
                        <td>Team F</td>
                        <td><span class="kicktipp-heim">0</span><span class="kicktipp-gast">0</span></td>
                    </tr>
                </tbody>
            </table>
            <table id="ranking" class="tippuebersicht">
                <thead>
                    <tr>
                        <th class="name">Name</th>
                        <th class="ereignis ereignis0" data-index="0" data-spiel="true"><a href="/test-community/tippuebersicht/spiel?tippspielId=101"></a></th>
                        <th class="ereignis ereignis1" data-index="1" data-spiel="true"><a href="/test-community/tippuebersicht/spiel?tippspielId=102"></a></th>
                        <th class="ereignis ereignis2" data-index="2" data-spiel="true"><a href="/test-community/tippuebersicht/spiel?tippspielId=103"></a></th>
                        <th class="spieltagspunkte">P</th>
                        <th class="gesamtpunkte">G</th>
                    </tr>
                </thead>
                <tbody>
                    <tr class="teilnehmer" data-teilnehmer-id="p1">
                        <td class="mg_class"><div class="mg_name">Alice</div></td>
                        <td class="ereignis ereignis0">2:1<sub class="p">4</sub></td>
                        <td class="ereignis ereignis1"></td>
                        <td class="ereignis ereignis2"></td>
                        <td class="spieltagspunkte">4</td>
                        <td class="gesamtpunkte">33</td>
                    </tr>
                    <tr class="teilnehmer" data-teilnehmer-id="p2">
                        <td class="mg_class"><div class="mg_name">Bob</div></td>
                        <td class="ereignis ereignis0"></td>
                        <td class="ereignis ereignis1">1:1</td>
                        <td class="ereignis ereignis2">0:1</td>
                        <td class="spieltagspunkte">0</td>
                        <td class="gesamtpunkte">19</td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "6"));
        var client = CreateClient();

        var snapshot = await client.GetCommunityMatchdaySnapshotAsync("test-community", 6);

        await Assert.That(snapshot).IsNotNull();
        await Assert.That(snapshot!.Matchday).IsEqualTo(6);
        await Assert.That(snapshot.Outcomes).HasCount().EqualTo(3);
        await Assert.That(snapshot.Participants).HasCount().EqualTo(2);

        var alice = snapshot.Participants.Single(participant => participant.ParticipantId == "p1");
        await Assert.That(alice.DisplayName).IsEqualTo("Alice");
        await Assert.That(alice.MatchdayPoints).IsEqualTo(4);
        await Assert.That(alice.TotalPoints).IsEqualTo(33);
        await Assert.That(alice.Predictions).HasCount().EqualTo(2);
        await Assert.That(alice.Predictions[0].Status).IsEqualTo(KicktippCommunityPredictionStatus.Placed);
        await Assert.That(alice.Predictions[0].Prediction!.HomeGoals).IsEqualTo(2);
        await Assert.That(alice.Predictions[0].AwardedPoints).IsEqualTo(4);
        await Assert.That(alice.Predictions[1].Status).IsEqualTo(KicktippCommunityPredictionStatus.Missed);
        await Assert.That(alice.Predictions[1].Prediction).IsNull();
        await Assert.That(alice.Predictions[1].AwardedPoints).IsEqualTo(0);

        var bob = snapshot.Participants.Single(participant => participant.ParticipantId == "p2");
        await Assert.That(bob.Predictions).HasCount().EqualTo(2);
        await Assert.That(bob.Predictions[0].Status).IsEqualTo(KicktippCommunityPredictionStatus.Missed);
        await Assert.That(bob.Predictions[1].Status).IsEqualTo(KicktippCommunityPredictionStatus.Placed);
        await Assert.That(bob.Predictions[1].Prediction!.AwayGoals).IsEqualTo(1);
        await Assert.That(bob.Predictions[1].AwardedPoints).IsEqualTo(0);
    }

    [Test]
    public async Task Getting_snapshot_treats_blank_row_as_all_missed_predictions_and_uses_cache()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>8. Spieltag</a></div>
            <table id="spielplanSpiele">
                <tbody>
                    <tr data-url="/test-community/tippuebersicht/spiel?tippspielId=801">
                        <td>22.09.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td><span class="kicktipp-heim">1</span><span class="kicktipp-gast">0</span></td>
                    </tr>
                    <tr data-url="/test-community/tippuebersicht/spiel?tippspielId=802">
                        <td>23.09.25 20:30</td>
                        <td>Team C</td>
                        <td>Team D</td>
                        <td><span class="kicktipp-heim">2</span><span class="kicktipp-gast">2</span></td>
                    </tr>
                </tbody>
            </table>
            <table id="ranking" class="tippuebersicht">
                <thead>
                    <tr>
                        <th class="ereignis ereignis0" data-index="0" data-spiel="true"><a href="/test-community/tippuebersicht/spiel?tippspielId=801"></a></th>
                        <th class="ereignis ereignis1" data-index="1" data-spiel="true"><a href="/test-community/tippuebersicht/spiel?tippspielId=802"></a></th>
                        <th class="spieltagspunkte">P</th>
                        <th class="gesamtpunkte">G</th>
                    </tr>
                </thead>
                <tbody>
                    <tr class="teilnehmer" data-teilnehmer-id="p3">
                        <td class="mg_class"><div class="mg_name">Charlie</div></td>
                        <td class="ereignis ereignis0"></td>
                        <td class="ereignis ereignis1"></td>
                        <td class="spieltagspunkte">0</td>
                        <td class="gesamtpunkte">0</td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponseWithParams(
            "/test-community/tippuebersicht",
            html,
            ("spieltagIndex", "8"));
        var client = CreateClient();

        var first = await client.GetCommunityMatchdaySnapshotAsync("test-community", 8);
        var second = await client.GetCommunityMatchdaySnapshotAsync("test-community", 8);

        await Assert.That(first).IsNotNull();
        await Assert.That(first!.Participants).HasCount().EqualTo(1);
        await Assert.That(first.Participants[0].Predictions).HasCount().EqualTo(2);
        await Assert.That(first.Participants[0].Predictions.All(prediction => prediction.Status == KicktippCommunityPredictionStatus.Missed)).IsTrue();
        await Assert.That(first.Participants[0].Predictions.All(prediction => prediction.AwardedPoints == 0)).IsTrue();
        await Assert.That(second).IsNotNull();
        await Assert.That(GetRequestsForPath("/test-community/tippuebersicht")
                .Count(entry => entry.RequestMessage.Method == "GET"))
            .IsEqualTo(1);
    }
}
