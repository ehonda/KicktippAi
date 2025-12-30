namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetOpenPredictionsAsync method.
/// </summary>
public class KicktippClient_GetOpenPredictions_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_open_predictions_returns_empty_list_on_404()
    {
        // Arrange
        StubNotFound("/test-community/tippabgabe");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_open_predictions_returns_empty_list_when_table_is_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Kicktipp</title></head>
            <body>
                <div class="content">
                    <p>No predictions available</p>
                </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tippabgabe", html);
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsEmpty();
    }

    [Test]
    public async Task Getting_open_predictions_parses_matches_with_date_inheritance()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(3);
        
        // First match has explicit date
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Team A");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Team B");
        await Assert.That(matches[0].Matchday).IsEqualTo(5);
        
        // Second match inherits date from first
        await Assert.That(matches[1].HomeTeam).IsEqualTo("Team C");
        await Assert.That(matches[1].AwayTeam).IsEqualTo("Team D");
        
        // Third match has new explicit date
        await Assert.That(matches[2].HomeTeam).IsEqualTo("Team E");
        await Assert.That(matches[2].AwayTeam).IsEqualTo("Team F");
    }

    [Test]
    public async Task Getting_open_predictions_extracts_matchday_from_title()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "tippabgabe-with-dates");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(5);
    }

    [Test]
    public async Task Getting_open_predictions_handles_rows_without_betting_inputs()
    {
        // Arrange - HTML with rows that have too few cells or no betting inputs
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
                        <td><span>Already played</span></td>
                    </tr>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team D</td>
                        <td>Team E</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
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
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert - only the row with betting inputs should be included
        await Assert.That(matches).HasCount().EqualTo(1);
        await Assert.That(matches[0].HomeTeam).IsEqualTo("Team D");
        await Assert.That(matches[0].AwayTeam).IsEqualTo("Team E");
    }

    [Test]
    public async Task Getting_open_predictions_handles_exception_in_row_parsing()
    {
        // Arrange - HTML that might cause parsing issues but should be handled gracefully
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <div class="prevnextTitle"><a>1. Spieltag</a></div>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Valid Team A</td>
                        <td>Valid Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
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
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Getting_open_predictions_extracts_matchday_from_hidden_input_as_fallback()
    {
        // Arrange - HTML with hidden input for matchday but no title
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <input name="spieltagIndex" value="7" />
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
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
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(7);
    }

    [Test]
    public async Task Getting_open_predictions_defaults_matchday_to_1_when_not_found()
    {
        // Arrange - HTML without matchday information
        var html = """
            <!DOCTYPE html>
            <html>
            <body>
            <table id="tippabgabeSpiele">
                <tbody>
                    <tr>
                        <td>22.08.25 20:30</td>
                        <td>Team A</td>
                        <td>Team B</td>
                        <td>
                            <input type="text" name="heim" />
                            <input type="text" name="gast" />
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
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        await Assert.That(matches).IsNotEmpty();
        await Assert.That(matches[0].Matchday).IsEqualTo(1);
    }
}
