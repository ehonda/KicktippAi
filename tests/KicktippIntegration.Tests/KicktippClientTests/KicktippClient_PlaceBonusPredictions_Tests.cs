using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.PlaceBonusPredictionsAsync method.
/// </summary>
public class KicktippClient_PlaceBonusPredictions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Placing_bonus_predictions_returns_false_on_get_404()
    {
        // Arrange
        StubNotFoundWithParams("/test-community/tippabgabe", new Dictionary<string, string> { ["bonus"] = "true" });
        var client = CreateClient();
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["101"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bonus_predictions_returns_true_when_no_predictions()
    {
        // Arrange
        var client = CreateClient();
        var predictions = new Dictionary<string, BonusPrediction>();

        // Act - empty dictionary, no HTTP calls needed
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Placing_bonus_predictions_submits_single_select_correctly()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions",
            new Dictionary<string, string> { ["bonus"] = "true" });
        StubPostResponseWithParams(
            "/test-community/tippabgabe",
            new Dictionary<string, string> { ["bonus"] = "true" });
        var client = CreateClient();
        
        // Use the actual form field name from the synthetic fixture
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["102"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        await Assert.That(formData["zusatzfragenSelect[1].auswahl"]).IsEqualTo("102");
    }

    [Test]
    public async Task Placing_bonus_predictions_returns_false_on_post_failure()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions",
            new Dictionary<string, string> { ["bonus"] = "true" });
        StubPostResponseWithParams(
            "/test-community/tippabgabe",
            new Dictionary<string, string> { ["bonus"] = "true" },
            500);
        var client = CreateClient();
        
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["101"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bonus_predictions_returns_false_when_form_missing()
    {
        // Arrange
        var html = "<html><body><p>No form</p></body></html>";
        StubHtmlResponseWithParams(
            "/test-community/tippabgabe", 
            html,
            new Dictionary<string, string> { ["bonus"] = "true" });
        var client = CreateClient();
        
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["101"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Placing_bonus_predictions_preserves_hidden_fields()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions",
            new Dictionary<string, string> { ["bonus"] = "true" });
        StubPostResponseWithParams(
            "/test-community/tippabgabe",
            new Dictionary<string, string> { ["bonus"] = "true" });
        var client = CreateClient();
        
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["101"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Should include hidden fields from the form
        await Assert.That(formData.ContainsKey("_charset_")).IsTrue();
        await Assert.That(formData["_charset_"]).IsEqualTo("UTF-8");
    }

    [Test]
    public async Task Placing_bonus_predictions_includes_submit_button()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions",
            new Dictionary<string, string> { ["bonus"] = "true" });
        StubPostResponseWithParams(
            "/test-community/tippabgabe",
            new Dictionary<string, string> { ["bonus"] = "true" });
        var client = CreateClient();
        
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { "zusatzfragenSelect[1].auswahl", new BonusPrediction(["101"]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync("test-community", predictions);

        // Assert
        await Assert.That(result).IsTrue();
        
        var postRequests = GetRequestsForPath("/test-community/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Should include the submit button
        await Assert.That(formData.ContainsKey("submitbutton")).IsTrue();
    }

    // NOTE: Real fixture test for PlaceBonusPredictionsAsync using tippabgabe-bonus.html is not included
    // because the snapshot was captured when all bonus questions were already locked (past deadline).
    // The snapshot shows "nichttippbar" class on all questions, meaning they cannot be edited.
    // To add a real fixture test, capture a bonus page during a period when bonus questions are still open.
}
