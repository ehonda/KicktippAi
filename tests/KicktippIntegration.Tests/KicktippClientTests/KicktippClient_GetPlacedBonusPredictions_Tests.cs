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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions-with-predictions", ("bonus", "true"));
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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions-with-predictions", ("bonus", "true"));
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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions-with-predictions", ("bonus", "true"));
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
        StubWithSyntheticFixtureAndParams("/test-community/tippabgabe", "bonus-questions-with-predictions", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert
        var topScorerPrediction = predictions.FirstOrDefault(p => p.Key == "Top scorer team?");
        await Assert.That(topScorerPrediction.Value).IsNull();
    }

    [Test]
    public async Task Getting_placed_bonus_predictions_returns_empty_for_locked_questions_snapshot()
    {
        // Arrange - use real snapshot from kicktipp-snapshots directory
        // The tippabgabe-bonus.html shows locked questions with "nichttippbar" class
        // and answers displayed as text divs, not select elements.
        // The client only parses select elements, so locked questions return empty.
        StubWithSnapshotAndParams("/test-community/tippabgabe", "tippabgabe-bonus", ("bonus", "true"));
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedBonusPredictionsAsync("test-community");

        // Assert - returns empty because all questions are locked (no select elements)
        // The snapshot shows 8 bonus questions with answers like:
        // - "Welche Mannschaften belegen die Plätze 16-18?" -> 1. FC Heidenheim 1846, Hamburger SV, FC St. Pauli
        // - "Wer wird Deutscher Meister?" -> FC Bayern München
        // But these are shown as read-only text, not form elements
        await Assert.That(predictions).IsEmpty();
    }
}
