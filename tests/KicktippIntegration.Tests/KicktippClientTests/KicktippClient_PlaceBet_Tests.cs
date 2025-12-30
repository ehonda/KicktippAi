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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
}
