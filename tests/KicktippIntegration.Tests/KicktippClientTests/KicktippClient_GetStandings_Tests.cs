using KicktippIntegration.Tests.Infrastructure;

namespace KicktippIntegration.Tests.KicktippClientTests;

/// <summary>
/// Tests for KicktippClient.GetStandingsAsync method.
/// </summary>
public class KicktippClient_GetStandings_Tests : KicktippClientTests_Base
{
    [Test]
    public async Task Getting_standings_returns_empty_list_on_404()
    {
        // Arrange
        StubNotFound("/test-community/tabellen");
        var client = CreateClient();

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert
        await Assert.That(standings).IsEmpty();
    }

    [Test]
    public async Task Getting_standings_returns_empty_list_when_table_is_missing()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Kicktipp</title></head>
            <body>
                <div class="content">
                    <p>No standings available</p>
                </div>
            </body>
            </html>
            """;
        StubHtmlResponse("/test-community/tabellen", html);
        var client = CreateClient();

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert
        await Assert.That(standings).IsEmpty();
    }

    [Test]
    public async Task Getting_standings_with_real_fixture_returns_valid_bundesliga_table()
    {
        // Arrange - use encrypted real fixture for the ehonda-test-buli community
        // 
        // REAL FIXTURE TESTING STRATEGY:
        // - Real fixtures contain actual data from Kicktipp pages and may change when updated.
        // - Test invariants (counts, structure, required fields) not concrete values.
        // - Concrete data assertions belong in synthetic fixture tests for stability.
        const string community = "ehonda-test-buli";
        StubWithRealFixture(community, "tabellen");
        var client = CreateClient();

        // Act
        var standings = await client.GetStandingsAsync(community);

        // Assert - Bundesliga has 18 teams
        await Assert.That(standings).HasCount().EqualTo(18);
        
        // Verify positions are sequential from 1 to 18
        var positions = standings.Select(s => s.Position).OrderBy(p => p).ToList();
        await Assert.That(positions).IsEquivalentTo(Enumerable.Range(1, 18));
        
        // Verify all teams have valid data (non-negative values)
        foreach (var standing in standings)
        {
            await Assert.That(standing.TeamName).IsNotEmpty();
            await Assert.That(standing.GamesPlayed).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.Points).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.Wins).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.Draws).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.Losses).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.GoalsFor).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.GoalsAgainst).IsGreaterThanOrEqualTo(0);
            
            // Verify games played = wins + draws + losses
            await Assert.That(standing.GamesPlayed)
                .IsEqualTo(standing.Wins + standing.Draws + standing.Losses);
            
            // Verify goal difference = goals for - goals against
            await Assert.That(standing.GoalDifference)
                .IsEqualTo(standing.GoalsFor - standing.GoalsAgainst);
        }
        
        // Verify first place has highest points (or at least not lower than second)
        var firstPlace = standings.First(s => s.Position == 1);
        var secondPlace = standings.First(s => s.Position == 2);
        await Assert.That(firstPlace.Points).IsGreaterThanOrEqualTo(secondPlace.Points);
    }
}
