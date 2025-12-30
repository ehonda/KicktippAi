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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
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
    public async Task Placing_bets_parses_real_tippabgabe_snapshot()
    {
        // Arrange - use real snapshot from kicktipp-snapshots directory
        // The tippabgabe.html has 9 Bundesliga matchday 16 games from January 2026
        //
        // NOTE: The form action in the snapshot is "/ehonda-test-buli/tippabgabe"
        // so POST is made to that path, not the community parameter path
        StubWithSnapshot("/test-community/tippabgabe", "tippabgabe");
        StubPostResponse("/ehonda-test-buli/tippabgabe");  // Form action from snapshot
        
        var client = CreateClient();
        
        // Create match instances with exact team names from the snapshot
        var instant1 = Instant.FromUtc(2026, 1, 9, 19, 30); // Frankfurt vs Dortmund
        var instant2 = Instant.FromUtc(2026, 1, 10, 14, 30); // Heidenheim vs Köln
        var zone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];
        
        var bets = new Dictionary<Match, BetPrediction>
        {
            { new Match("Eintracht Frankfurt", "Borussia Dortmund", instant1.InZone(zone), 16), new BetPrediction(2, 1) },
            { new Match("1. FC Heidenheim 1846", "1. FC Köln", instant2.InZone(zone), 16), new BetPrediction(1, 0) }
        };

        // Act
        var result = await client.PlaceBetsAsync("test-community", bets);

        // Assert - should succeed
        await Assert.That(result).IsTrue();
        
        // Verify correct form data was submitted (POST goes to form action URL)
        var postRequests = GetRequestsForPath("/ehonda-test-buli/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Frankfurt vs Dortmund (match ID: 1384231935)
        await Assert.That(formData).ContainsKey("spieltippForms[1384231935].heimTipp");
        await Assert.That(formData["spieltippForms[1384231935].heimTipp"]).IsEqualTo("2");
        await Assert.That(formData["spieltippForms[1384231935].gastTipp"]).IsEqualTo("1");
        
        // Heidenheim vs Köln (match ID: 1384231933)
        await Assert.That(formData).ContainsKey("spieltippForms[1384231933].heimTipp");
        await Assert.That(formData["spieltippForms[1384231933].heimTipp"]).IsEqualTo("1");
        await Assert.That(formData["spieltippForms[1384231933].gastTipp"]).IsEqualTo("0");
    }
}
