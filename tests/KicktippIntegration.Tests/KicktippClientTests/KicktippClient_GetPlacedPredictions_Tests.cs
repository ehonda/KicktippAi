namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetPlacedPredictionsAsync method.
/// </summary>
public class KicktippClient_GetPlacedPredictions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_placed_predictions_returns_empty_dictionary_on_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).IsEmpty();
    }

    [Test]
    public async Task Getting_placed_predictions_returns_empty_dictionary_when_table_is_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
                <div class="content"><p>No predictions</p></div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).IsEmpty();
    }

    [Test]
    public async Task Getting_placed_predictions_extracts_existing_predictions()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert
        await Assert.That(predictions).HasCount().EqualTo(3);
        
        // First match has prediction 2:1
        var firstMatch = predictions.Keys.First(m => m.HomeTeam == "Team A");
        var firstPrediction = predictions[firstMatch];
        await Assert.That(firstPrediction).IsNotNull();
        await Assert.That(firstPrediction!.HomeGoals).IsEqualTo(2);
        await Assert.That(firstPrediction.AwayGoals).IsEqualTo(1);
        
        // Second match has no prediction
        var secondMatch = predictions.Keys.First(m => m.HomeTeam == "Team C");
        await Assert.That(predictions[secondMatch]).IsNull();
        
        // Third match has prediction 0:3
        var thirdMatch = predictions.Keys.First(m => m.HomeTeam == "Team E");
        var thirdPrediction = predictions[thirdMatch];
        await Assert.That(thirdPrediction).IsNotNull();
        await Assert.That(thirdPrediction!.HomeGoals).IsEqualTo(0);
        await Assert.That(thirdPrediction.AwayGoals).IsEqualTo(3);
    }

    [Test]
    public async Task Getting_placed_predictions_handles_partial_predictions()
    {
        // Arrange - only home goals entered
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" value="2" />
                            <input type="text" name="gast" value="" />
                        </td>
                    </tr>
                </tbody>
            </table>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert - partial prediction should be null
        await Assert.That(predictions).HasCount().EqualTo(1);
        var match = predictions.Keys.First();
        await Assert.That(predictions[match]).IsNull();
    }

    [Test]
    public async Task Getting_placed_predictions_inherits_time_from_previous_row()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-predictions");
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert - second match should have inherited time from first
        var secondMatch = predictions.Keys.First(m => m.HomeTeam == "Team C");
        // Both first and second matches should have same start time due to inheritance
        var firstMatch = predictions.Keys.First(m => m.HomeTeam == "Team A");
        await Assert.That(secondMatch.StartsAt).IsEqualTo(firstMatch.StartsAt);
    }
}
