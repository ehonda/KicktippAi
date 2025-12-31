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
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-predictions");
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
    public async Task Getting_placed_predictions_with_real_fixture_returns_valid_matchday()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        StubWithRealFixture(community, "tippabgabe");
        var client = CreateClient();

        // Act
        var predictions = await client.GetPlacedPredictionsAsync(community);

        // Assert - Bundesliga matchday typically has 9 matches
        await Assert.That(predictions.Count).IsGreaterThanOrEqualTo(9);
        
        // All matches should have valid data
        foreach (var (match, _) in predictions)
        {
            await Assert.That(match.HomeTeam).IsNotEmpty();
            await Assert.That(match.AwayTeam).IsNotEmpty();
            await Assert.That(match.HomeTeam).IsNotEqualTo(match.AwayTeam);
            await Assert.That(match.Matchday).IsGreaterThan(0);
        }
        
        // All matches in a matchday should have the same matchday number
        var matchdays = predictions.Keys.Select(m => m.Matchday).Distinct().ToList();
        await Assert.That(matchdays).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Getting_placed_predictions_handles_non_numeric_prediction_values()
    {
        // Arrange - prediction values that are not parseable as integers
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
                            <input type="text" name="heim" value="abc" />
                            <input type="text" name="gast" value="xyz" />
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

        // Assert - match should be present but prediction should be null (unparseable)
        await Assert.That(predictions).HasCount().EqualTo(1);
        var match = predictions.Keys.First();
        await Assert.That(predictions[match]).IsNull();
    }

    [Test]
    public async Task Getting_placed_predictions_handles_rows_with_too_few_cells()
    {
        // Arrange - row with fewer than required cells
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
                    </tr>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team B</td>
                        <td>Team C</td>
                        <td>
                            <input type="text" name="heim" value="2" />
                            <input type="text" name="gast" value="1" />
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

        // Assert - only valid row should be parsed
        await Assert.That(predictions).HasCount().EqualTo(1);
        var match = predictions.Keys.First();
        await Assert.That(match.HomeTeam).IsEqualTo("Team B");
    }

    [Test]
    public async Task Getting_placed_predictions_handles_first_row_without_time()
    {
        // Arrange - first row has no time, cannot inherit from previous
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td></td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" value="2" />
                            <input type="text" name="gast" value="1" />
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

        // Assert - match should still be created with default time
        await Assert.That(predictions).HasCount().EqualTo(1);
        var match = predictions.Keys.First();
        await Assert.That(match.HomeTeam).IsEqualTo("Team A");
    }
}
