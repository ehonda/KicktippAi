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

    [Test]
    public async Task Getting_placed_predictions_parses_real_tippabgabe_page()
    {
        // Arrange - use real tippabgabe snapshot from kicktipp-snapshots directory
        StubWithSnapshot("/test-community/tippabgabe", "tippabgabe");
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync("test-community");

        // Assert - verify we got matches from the real fixture
        // The snapshot has 9 matches for matchday 16
        await Assert.That(predictions).HasCount().EqualTo(9);
        
        // Verify specific matches are present (from the actual snapshot data)
        var frankfurtMatch = predictions.Keys.FirstOrDefault(m => m.HomeTeam == "Eintracht Frankfurt");
        await Assert.That(frankfurtMatch).IsNotNull();
        await Assert.That(frankfurtMatch!.AwayTeam).IsEqualTo("Borussia Dortmund");
        await Assert.That(frankfurtMatch.Matchday).IsEqualTo(16);
        
        // First match should be Frankfurt vs Dortmund on 09.01.26 20:30
        await Assert.That(frankfurtMatch.StartsAt.Year).IsEqualTo(2026);
        await Assert.That(frankfurtMatch.StartsAt.Month).IsEqualTo(1);
        await Assert.That(frankfurtMatch.StartsAt.Day).IsEqualTo(9);
        
        // Verify Bayern match is present
        var bayernMatch = predictions.Keys.FirstOrDefault(m => m.HomeTeam == "FC Bayern München");
        await Assert.That(bayernMatch).IsNotNull();
        await Assert.That(bayernMatch!.AwayTeam).IsEqualTo("VfL Wolfsburg");
        
        // Predictions should be null since no values are entered in the snapshot
        await Assert.That(predictions[frankfurtMatch]).IsNull();
    }
}
