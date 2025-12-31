using EHonda.KicktippAi.Core;
using NodaTime;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.PlaceBetsAsync method.
/// </summary>
public class KicktippClient_PlaceBets_Tests : KicktippClientTests_Base
{
    private static Match CreateTestMatch(string homeTeam = "Team A", string awayTeam = "Team B", int matchday = 1)
    {
        var instant = Instant.FromUtc(2025, 8, 22, 20, 30);
        var zone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
        var zonedDateTime = instant.InZone(zone);
        return new Match(homeTeam, awayTeam, zonedDateTime, matchday);
    }

    [Test]
    public async Task Placing_bets_returns_false_on_get_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch(), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_returns_true_when_no_bets_to_place()
    {
        // Arrange - Still need to stub the page because it makes the GET request
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>();

        // Act - Empty dictionary, but still accesses the page
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert - Returns true, no bets placed
        await Assert.That(result).IsTrue();
        
        // No POST should be made when there are no bets to place
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Placing_bets_submits_multiple_bets_correctly()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) },
            { CreateTestMatch("Team E", "Team F"), new BetPrediction(0, 3) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        // First match
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("1");
        // Third match (second in our bets dictionary)
        await Assert.That(formData["spieltippForms[3].heimTipp"]).IsEqualTo("0");
        await Assert.That(formData["spieltippForms[3].gastTipp"]).IsEqualTo("3");
    }

    [Test]
    public async Task Placing_bets_skips_existing_bets_when_override_false()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        
        // Team A vs Team B already has prediction, Team C vs Team D doesn't
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(5, 5) }, // Existing bet
            { CreateTestMatch("Team C", "Team D"), new BetPrediction(1, 1) }   // No existing bet
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets, overrideBets: false);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // First match should keep existing values
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("1");
        
        // Second match should have new values
        await Assert.That(formData["spieltippForms[2].heimTipp"]).IsEqualTo("1");
        await Assert.That(formData["spieltippForms[2].gastTipp"]).IsEqualTo("1");
    }

    [Test]
    public async Task Placing_bets_overrides_existing_bets_when_override_true()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(5, 5) } // Existing bet to override
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets, overrideBets: true);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("5");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("5");
    }

    [Test]
    public async Task Placing_bets_preserves_bets_not_in_dictionary()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        
        // Only bet on Team C vs Team D, not on Team A vs Team B or Team E vs Team F
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team C", "Team D"), new BetPrediction(4, 4) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Existing predictions should be preserved
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("1");
        await Assert.That(formData["spieltippForms[3].heimTipp"]).IsEqualTo("0");
        await Assert.That(formData["spieltippForms[3].gastTipp"]).IsEqualTo("3");
        
        // New bet should be added
        await Assert.That(formData["spieltippForms[2].heimTipp"]).IsEqualTo("4");
        await Assert.That(formData["spieltippForms[2].gastTipp"]).IsEqualTo("4");
    }

    [Test]
    public async Task Placing_bets_returns_false_on_post_failure()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe", 500);
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_returns_false_when_form_missing()
    {
        // Arrange
        var html = "<html><body><p>No form</p></body></html>";
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch(), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_returns_false_when_content_area_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <form id="tippabgabeForm">
                <p>No content area</p>
            </form>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch(), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_returns_false_when_tbody_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm">
                    <p>No table body</p>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch(), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bets_skips_matches_with_missing_input_names()
    {
        // Arrange - match input fields exist but have no name attribute
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="/test-community/tippabgabe">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="heimTipp" type="text" />
                                    <input id="gastTipp" type="text" />
                                </td>
                            </tr>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team C</td>
                                <td>Team D</td>
                                <td>
                                    <input id="2_heimTipp" name="spieltippForms[2].heimTipp" type="text" />
                                    <input id="2_gastTipp" name="spieltippForms[2].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <button type="submit" name="submitbutton">Submit</button>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }, // Missing input names
            { CreateTestMatch("Team C", "Team D"), new BetPrediction(3, 2) }  // Valid input names
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert - should succeed but only Team C vs D bet was placed
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Team C vs D should have bet placed
        await Assert.That(formData["spieltippForms[2].heimTipp"]).IsEqualTo("3");
        await Assert.That(formData["spieltippForms[2].gastTipp"]).IsEqualTo("2");
        
        // Team A vs B fields should not be in form data (no name)
        await Assert.That(formData.Keys.Where(k => k.Contains("[1]"))).IsEmpty();
    }

    [Test]
    public async Task Placing_bets_uses_input_submit_button()
    {
        // Arrange - use input element for submit instead of button
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="/test-community/tippabgabe">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="1_heimTipp" name="spieltippForms[1].heimTipp" type="text" />
                                    <input id="1_gastTipp" name="spieltippForms[1].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <input type="submit" name="inputSubmit" value="Save" />
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData).ContainsKey("inputSubmit");
        await Assert.That(formData["inputSubmit"]).IsEqualTo("Save");
    }

    [Test]
    public async Task Placing_bets_uses_fallback_submit_button_when_none_found()
    {
        // Arrange - no submit button in form
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="/test-community/tippabgabe">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="1_heimTipp" name="spieltippForms[1].heimTipp" type="text" />
                                    <input id="1_gastTipp" name="spieltippForms[1].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        // Should use fallback submit button name
        await Assert.That(formData).ContainsKey("submitbutton");
    }

    [Test]
    public async Task Placing_bets_uses_button_element_submit()
    {
        // Arrange - use button element for submit
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="/test-community/tippabgabe">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="1_heimTipp" name="spieltippForms[1].heimTipp" type="text" />
                                    <input id="1_gastTipp" name="spieltippForms[1].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <button type="submit" name="buttonSubmit" value="SaveBets">Save</button>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData).ContainsKey("buttonSubmit");
        await Assert.That(formData["buttonSubmit"]).IsEqualTo("SaveBets");
    }

    [Test]
    public async Task Placing_bets_handles_relative_form_action()
    {
        // Arrange - form action is a relative path (no leading slash)
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="submit-bets">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="1_heimTipp" name="spieltippForms[1].heimTipp" type="text" />
                                    <input id="1_gastTipp" name="spieltippForms[1].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <button type="submit" name="submitbutton">Submit</button>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/submit-bets");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify POST was made to the relative path (appended to community)
        var postRequests = GetRequestsForPath("/test-community/submit-bets")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Placing_bets_skips_rows_with_empty_team_names()
    {
        // Arrange - some rows have empty team names
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div id="kicktipp-content">
                <form id="tippabgabeForm" action="/test-community/tippabgabe">
                    <input type="hidden" name="spieltagIndex" value="1" />
                    <table>
                        <tbody>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td></td>
                                <td></td>
                                <td>
                                    <input id="1_heimTipp" name="spieltippForms[1].heimTipp" type="text" />
                                    <input id="1_gastTipp" name="spieltippForms[1].gastTipp" type="text" />
                                </td>
                            </tr>
                            <tr>
                                <td>22.08.25 20:30</td>
                                <td>Team A</td>
                                <td>Team B</td>
                                <td>
                                    <input id="2_heimTipp" name="spieltippForms[2].heimTipp" type="text" />
                                    <input id="2_gastTipp" name="spieltippForms[2].gastTipp" type="text" />
                                </td>
                            </tr>
                        </tbody>
                    </table>
                    <button type="submit" name="submitbutton">Submit</button>
                </form>
            </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var bets = new Dictionary<Match, BetPrediction>
        {
            { CreateTestMatch("Team A", "Team B"), new BetPrediction(2, 1) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Team A vs B should have bet placed
        await Assert.That(formData["spieltippForms[2].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[2].gastTipp"]).IsEqualTo("1");
    }

    [Test]
    public async Task Placing_bets_with_real_fixture_submits_form_correctly()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        StubWithRealFixture(community, "tippabgabe");
        StubPostResponse($"/{community}/tippabgabe");
        
        var client = CreateClient();
        
        // First, get the matches from the page to find real match data
        var existingPredictions = await client.GetPlacedPredictionsAsync(community);
        
        // Take the first two matches and create bets for them
        var matches = existingPredictions.Keys.Take(2).ToList();
        await Assert.That(matches).HasCount().GreaterThanOrEqualTo(2);
        
        var bets = new Dictionary<Match, BetPrediction>
        {
            { matches[0], new BetPrediction(2, 1) },
            { matches[1], new BetPrediction(1, 0) }
        };

        // Act
        var result = await client.PlaceBetsAsync(community, bets);

        // Assert - should succeed
        await Assert.That(result).IsTrue();
        
        // Verify a POST was made
        var postRequests = GetRequestsForPath($"/{community}/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        // Verify form data contains the expected structure
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Verify first bet (2:1) was submitted correctly
        var bet1HomeFields = formData.Where(kv => kv.Key.EndsWith(".heimTipp") && kv.Value == "2").ToList();
        var bet1AwayFields = formData.Where(kv => kv.Key.EndsWith(".gastTipp") && kv.Value == "1").ToList();
        
        await Assert.That(bet1HomeFields).HasCount().EqualTo(1);
        await Assert.That(bet1AwayFields).HasCount().EqualTo(1);
        
        // The fields should be for the same match ID
        var bet1HomeMatchId = bet1HomeFields.First().Key.Replace(".heimTipp", "");
        var bet1AwayMatchId = bet1AwayFields.First().Key.Replace(".gastTipp", "");
        await Assert.That(bet1HomeMatchId).IsEqualTo(bet1AwayMatchId);
        
        // Verify second bet (1:0) was submitted correctly
        var bet2HomeFields = formData.Where(kv => kv.Key.EndsWith(".heimTipp") && kv.Value == "1").ToList();
        var bet2AwayFields = formData.Where(kv => kv.Key.EndsWith(".gastTipp") && kv.Value == "0").ToList();
        
        await Assert.That(bet2HomeFields).HasCount().EqualTo(1);
        await Assert.That(bet2AwayFields).HasCount().EqualTo(1);
        
        // The fields should be for the same match ID
        var bet2HomeMatchId = bet2HomeFields.First().Key.Replace(".heimTipp", "");
        var bet2AwayMatchId = bet2AwayFields.First().Key.Replace(".gastTipp", "");
        await Assert.That(bet2HomeMatchId).IsEqualTo(bet2AwayMatchId);
        
        // The two bets should be for different matches
        await Assert.That(bet1HomeMatchId).IsNotEqualTo(bet2HomeMatchId);
        
        // Verify required hidden fields are present
        await Assert.That(formData).ContainsKey("spieltagIndex");
        await Assert.That(formData).ContainsKey("tippsaisonId");
        await Assert.That(formData).ContainsKey("submitbutton");
    }
}
