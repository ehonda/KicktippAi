using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging.Testing;

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

    [Test]
    public async Task Placing_bonus_predictions_with_real_fixture_submits_form_correctly()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        StubWithRealFixtureAndParams($"/{community}/tippabgabe", community, "tippabgabe-bonus",
            ("bonus", "true"));
        // The form might POST without the bonus parameter, so stub both
        StubPostResponseWithParams(
            $"/{community}/tippabgabe",
            new Dictionary<string, string> { ["bonus"] = "true" });
        StubPostResponse($"/{community}/tippabgabe");
        
        var logger = new FakeLogger<KicktippClient>();
        var client = CreateClient(logger: logger);
        
        // First, get the bonus questions from the page to find real field names
        var existingQuestions = await client.GetOpenBonusQuestionsAsync(community);
        
        // Take the first question and create a prediction for it
        await Assert.That(existingQuestions).HasCount().GreaterThanOrEqualTo(1);
        
        var firstQuestion = existingQuestions.First();
        await Assert.That(firstQuestion.Options).HasCount().GreaterThanOrEqualTo(1);
        await Assert.That(firstQuestion.FormFieldName).IsNotNull();
        
        // Select the first option
        var selectedOption = firstQuestion.Options.First();
        var predictions = new Dictionary<string, BonusPrediction>
        {
            { firstQuestion.FormFieldName!, new BonusPrediction([selectedOption.Id]) }
        };

        // Act
        var result = await client.PlaceBonusPredictionsAsync(community, predictions);

        // Assert - should succeed
        // If this fails, check the logger output for debugging info
        if (!result)
        {
            var logMessages = string.Join("\n", logger.Collector.GetSnapshot()
                .Select(e => $"[{e.Level}] {e.Message}"));
            throw new Exception($"PlaceBonusPredictionsAsync returned false.\nLogger output:\n{logMessages}");
        }
        await Assert.That(result).IsTrue();
        
        // Verify a POST was made
        var postRequests = GetRequestsForPath($"/{community}/tippabgabe")
            .Where(r => r.RequestMessage.Method == "POST");
        await Assert.That(postRequests.Count()).IsEqualTo(1);
        
        // Verify form data contains the expected structure
        var formData = ParseFormData(postRequests.First().RequestMessage.Body);
        
        // Verify the prediction was submitted correctly
        await Assert.That(formData).ContainsKey(firstQuestion.FormFieldName!);
        await Assert.That(formData[firstQuestion.FormFieldName!]).IsEqualTo(selectedOption.Id);
        
        // Verify required hidden fields are present
        await Assert.That(formData).ContainsKey("_charset_");
        await Assert.That(formData).ContainsKey("submitbutton");
    }
}
