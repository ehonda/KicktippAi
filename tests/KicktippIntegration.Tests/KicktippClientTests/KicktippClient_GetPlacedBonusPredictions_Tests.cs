using EHonda.KicktippAi.Core;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetPlacedBonusPredictionsAsync method.
/// </summary>
public class KicktippClient_GetPlacedBonusPredictions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_placed_bonus_predictions_returns_empty_dictionary_on_404()
    {
        // Arrange
        StubNotFoundWithParams("/test-community/tippabgabe", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).IsEmpty();
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_returns_empty_dictionary_when_table_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body><p>No bonus</p></body>
            </html>
            """;
        StubHtmlResponseWithParams("/test-community/tippabgabe", html, ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).IsEmpty();
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_uses_bonus_true_parameter()
    {
        // Arrange - only respond if bonus=true
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions-with-predictions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).IsNotEmpty();
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_extracts_selected_single_option()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions-with-predictions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert - key is the form field name, not the question text
        var championshipPrediction = predictions.FirstOrDefault(p => p.Key == "bonusForms[1].antwortIds");
        await Assert.That(championshipPrediction.Value).IsNotNull();
        await Assert.That(championshipPrediction.Value!.SelectedOptionIds).IsEquivalentTo(["102"]);
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_extracts_multiple_selected_options()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions-with-predictions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert - key is the first select element's form field name
        var relegationPrediction = predictions.FirstOrDefault(p => p.Key == "bonusForms[2].antwortIds[0]");
        await Assert.That(relegationPrediction.Value).IsNotNull();
        await Assert.That(relegationPrediction.Value!.SelectedOptionIds).IsEquivalentTo(["201", "203"]);
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_returns_null_for_unanswered_questions()
    {
        // Arrange
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "test-community", "bonus-questions-with-predictions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert
        var topScorerPrediction = predictions.FirstOrDefault(p => p.Key == "Top scorer team?");
        await Assert.That(topScorerPrediction.Value).IsNull();
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_with_real_fixture_returns_predictions()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        // 
        // NOTE: This test is skipped because the current fixture was captured when all bonus 
        // questions were locked. Locked questions show answers as text divs, not <select> elements,
        // and the client only parses <select> elements. This is a known limitation.
        const string community = "ehonda-test-buli";
        StubWithRealFixtureAndParams($"/{community}/tippabgabe", community, "tippabgabe-bonus",
            ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync(community);

        // Assert - should have predictions with valid structure
        // Note: The return type is Dictionary<string, BonusPrediction?> where key is question text
        await Assert.That(predictions.Count).IsGreaterThan(0);
        
        foreach (var (questionText, prediction) in predictions)
        {
            await Assert.That(questionText).IsNotEmpty();
            
            if (prediction != null)
            {
                await Assert.That(prediction.SelectedOptionIds).IsNotEmpty();
            }
        }
    }
}
