using EHonda.KicktippAi.Core;
using NodaTime;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.PlaceBetAsync method.
/// </summary>
public class KicktippClient_PlaceBet_Tests : KicktippClientTests_Base
{
    private static Match CreateTestMatch(string homeTeam = "Team A", string awayTeam = "Team B")
    {
        var instant = Instant.FromUtc(2025, 8, 22, 20, 30);
        var zone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
        var zonedDateTime = instant.InZone(zone);
        return new Match(homeTeam, awayTeam, zonedDateTime, 1);
    }

    [Test]
    public async Task Placing_bet_returns_false_on_get_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch();
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bet_returns_false_when_form_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body><p>No form</p></body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();
        var match = CreateTestMatch();
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bet_returns_false_when_match_not_found()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        var client = CreateClient();
        var match = CreateTestMatch("NonExistent Home", "NonExistent Away");
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bet_submits_form_data_correctly()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B");
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify POST was made with correct form data
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData).ContainsKey("spieltippForms[1].heimTipp");
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("1");
    }

    [Test]
    public async Task Placing_bet_skips_when_existing_bet_and_override_false()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B"); // Already has prediction 2:1
        var prediction = new BetPrediction(3, 0);

        // Act - Returns true because existing bet is considered successful (no action needed)
        var result = await client.PlaceBetAsync("test-community", match, prediction, overrideBet: false);

        // Assert - Method returns true because bet exists (even though skipped)
        await Assert.That(result).IsTrue();
        
        // Verify no POST was made (existing bet was kept)
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Placing_bet_overrides_when_existing_bet_and_override_true()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B");
        var prediction = new BetPrediction(3, 0);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction, overrideBet: true);

        // Assert
        await Assert.That(result).IsTrue();
        
        // Verify new prediction was submitted
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData["spieltippForms[1].heimTipp"]).IsEqualTo("3");
        await Assert.That(formData["spieltippForms[1].gastTipp"]).IsEqualTo("0");
    }

    [Test]
    public async Task Placing_bet_returns_false_on_post_failure()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe", 500);
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B");
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bet_includes_hidden_fields_in_submission()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B");
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Hidden fields from the form
        await Assert.That(formData).ContainsKey("spieltagIndex");
        await Assert.That(formData["spieltagIndex"]).IsEqualTo("5");
    }

    [Test]
    public async Task Placing_bet_includes_submit_button_in_form_data()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
        StubPostResponse("/test-community/tippabgabe");
        var client = CreateClient();
        var match = CreateTestMatch("Team A", "Team B");
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync("test-community", match, prediction);

        // Assert
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData).ContainsKey("submitbutton");
    }

    [Test]
    public async Task Placing_bet_with_real_fixture_submits_form_correctly()
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
        
        // First, get the matches from the page to find a real match
        var existingPredictions = await client.GetPlacedPredictionsAsync(community);
        await Assert.That(existingPredictions).HasCount().GreaterThan(0);
        
        var match = existingPredictions.Keys.First();
        var prediction = new BetPrediction(2, 1);

        // Act
        var result = await client.PlaceBetAsync(community, match, prediction);

        // Assert - should succeed
        await Assert.That(result).IsTrue();
        
        // Verify a POST was made
        var postRequests = GetRequestsForPath($"/{community}/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        // Verify form data contains the expected structure
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Find the field that was updated with our prediction value
        // The form contains fields like spieltippForms[N].heimTipp and spieltippForms[N].gastTipp
        var fieldsWithHome2 = formData.Where(kv => kv.Key.EndsWith(".heimTipp") && kv.Value == "2").ToList();
        var fieldsWithAway1 = formData.Where(kv => kv.Key.EndsWith(".gastTipp") && kv.Value == "1").ToList();
        
        // There should be exactly one match with our prediction (2:1)
        await Assert.That(fieldsWithHome2).HasCount().EqualTo(1);
        await Assert.That(fieldsWithAway1).HasCount().EqualTo(1);
        
        // The fields should be for the same match ID (same form index)
        var homeFieldName = fieldsWithHome2.First().Key;
        var awayFieldName = fieldsWithAway1.First().Key;
        var homeMatchId = homeFieldName.Replace(".heimTipp", "");
        var awayMatchId = awayFieldName.Replace(".gastTipp", "");
        await Assert.That(homeMatchId).IsEqualTo(awayMatchId);
        
        // Verify required hidden fields are present
        await Assert.That(formData).ContainsKey("spieltagIndex");
        await Assert.That(formData).ContainsKey("tippsaisonId");
        await Assert.That(formData).ContainsKey("submitbutton");
    }
}
