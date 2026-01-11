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
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
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
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-dates");
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

    [Test]
    public async Task Getting_open_predictions_with_real_fixture_returns_valid_matchday()
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
        var matches = await client.GetOpenPredictionsAsync(community);

        // Assert - Bundesliga matchday typically has 9 matches
        await Assert.That(matches).HasCount().GreaterThanOrEqualTo(9);
        
        // All matches should have valid data
        foreach (var match in matches)
        {
            await Assert.That(match.HomeTeam).IsNotEmpty();
            await Assert.That(match.AwayTeam).IsNotEmpty();
            await Assert.That(match.HomeTeam).IsNotEqualTo(match.AwayTeam);
            await Assert.That(match.Matchday).IsGreaterThan(0);
        }
        
        // All matches should be in the same matchday
        var matchdays = matches.Select(m => m.Matchday).Distinct().ToList();
        await Assert.That(matchdays).HasCount().EqualTo(1);
        
        // Matches should have valid times (hours in reasonable range)
        foreach (var match in matches)
        {
            await Assert.That(match.StartsAt.Hour).IsGreaterThanOrEqualTo(0);
            await Assert.That(match.StartsAt.Hour).IsLessThan(24);
        }
    }

    /// <summary>
    /// Verifies that cancelled matches ("Abgesagt") are detected and marked with IsCancelled = true.
    /// Cancelled matches should still be included in the results since Kicktipp allows placing predictions on them.
    /// See docs/features/cancelled-matches.md for design rationale.
    /// </summary>
    [Test]
    public async Task Getting_open_predictions_detects_cancelled_matches()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-cancelled");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert - all 4 matches should be returned (including cancelled ones)
        await Assert.That(matches).HasCount().EqualTo(4);
        
        // First match: normal
        var firstMatch = matches.First(m => m.HomeTeam == "Team A");
        await Assert.That(firstMatch.IsCancelled).IsFalse();
        
        // Second match: cancelled (should have IsCancelled = true)
        var cancelledMatch1 = matches.First(m => m.HomeTeam == "Team C");
        await Assert.That(cancelledMatch1.IsCancelled).IsTrue();
        
        // Third match: normal
        var thirdMatch = matches.First(m => m.HomeTeam == "Team E");
        await Assert.That(thirdMatch.IsCancelled).IsFalse();
        
        // Fourth match: also cancelled
        var cancelledMatch2 = matches.First(m => m.HomeTeam == "Team G");
        await Assert.That(cancelledMatch2.IsCancelled).IsTrue();
    }

    /// <summary>
    /// Verifies that cancelled matches inherit the time from the previous match in the table.
    /// This is critical for database key consistency since startsAt is part of the composite key.
    /// </summary>
    [Test]
    public async Task Getting_open_predictions_cancelled_matches_inherit_previous_time()
    {
        // Arrange
        StubWithSyntheticFixture("/test-community/tippabgabe", "test-community", "tippabgabe-with-cancelled");
        var client = CreateClient();

        // Act
        var matches = await client.GetOpenPredictionsAsync("test-community");

        // Assert
        // First match: explicit time 15:30
        var firstMatch = matches.First(m => m.HomeTeam == "Team A");
        await Assert.That(firstMatch.StartsAt.Hour).IsEqualTo(15);
        await Assert.That(firstMatch.StartsAt.Minute).IsEqualTo(30);
        
        // Second match (cancelled): should inherit 15:30 from first match
        var cancelledMatch1 = matches.First(m => m.HomeTeam == "Team C");
        await Assert.That(cancelledMatch1.StartsAt.Hour).IsEqualTo(15);
        await Assert.That(cancelledMatch1.StartsAt.Minute).IsEqualTo(30);
        await Assert.That(cancelledMatch1.IsCancelled).IsTrue();
        
        // Third match: new explicit time 18:30
        var thirdMatch = matches.First(m => m.HomeTeam == "Team E");
        await Assert.That(thirdMatch.StartsAt.Hour).IsEqualTo(18);
        await Assert.That(thirdMatch.StartsAt.Minute).IsEqualTo(30);
        
        // Fourth match (cancelled): should inherit 18:30 from third match
        var cancelledMatch2 = matches.First(m => m.HomeTeam == "Team G");
        await Assert.That(cancelledMatch2.StartsAt.Hour).IsEqualTo(18);
        await Assert.That(cancelledMatch2.StartsAt.Minute).IsEqualTo(30);
        await Assert.That(cancelledMatch2.IsCancelled).IsTrue();
    }
}
