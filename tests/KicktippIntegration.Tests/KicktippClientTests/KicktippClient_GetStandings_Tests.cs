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
    [FixtureRequired]
    public async Task Getting_standings_parses_real_standings_page()
    {
        // Arrange
        StubWithFixture("/test-community/tabellen", "tabellen");
        var client = CreateClient();

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert - verify complete parsing of the standings table
        // The "tabellen" fixture is from the Bundesliga, which has 18 teams
        await Assert.That(standings).HasCount().EqualTo(18);
        
        // Verify structure of standings: positions should be 1-18 in order
        await Assert.That(standings.Select(s => s.Position))
            .IsEquivalentTo(Enumerable.Range(1, 18));
        
        // Verify all standings have required data populated
        foreach (var standing in standings)
        {
            await Assert.That(standing.TeamName).IsNotNull().And.IsNotEmpty();
            await Assert.That(standing.GamesPlayed).IsGreaterThanOrEqualTo(0);
            await Assert.That(standing.Points).IsGreaterThanOrEqualTo(0);
        }
        
        // Verify first place team has most points (sanity check for parsing)
        var topTeam = standings.First();
        var lastTeam = standings.Last();
        await Assert.That(topTeam.Points).IsGreaterThanOrEqualTo(lastTeam.Points);
    }

    [Test]
    public async Task Getting_standings_parses_real_standings_snapshot()
    {
        // Arrange - use the unencrypted snapshot from kicktipp-snapshots
        StubWithSnapshot("/test-community/tabellen", "tabellen");
        var client = CreateClient();

        // Act
        var standings = await client.GetStandingsAsync("test-community");

        // Assert - the snapshot has Bundesliga standings with 18 teams
        await Assert.That(standings).HasCount().EqualTo(18);
        
        // Verify specific team data from the actual snapshot
        var bayern = standings.FirstOrDefault(s => s.TeamName == "FC Bayern München");
        await Assert.That(bayern).IsNotNull();
        await Assert.That(bayern!.Position).IsEqualTo(1);
        await Assert.That(bayern.GamesPlayed).IsEqualTo(15);
        await Assert.That(bayern.Points).IsEqualTo(41);
        await Assert.That(bayern.GoalsFor).IsEqualTo(55);
        await Assert.That(bayern.GoalsAgainst).IsEqualTo(11);
        await Assert.That(bayern.GoalDifference).IsEqualTo(44);
        await Assert.That(bayern.Wins).IsEqualTo(13);
        await Assert.That(bayern.Draws).IsEqualTo(2);
        await Assert.That(bayern.Losses).IsEqualTo(0);
        
        // Verify second place team
        var dortmund = standings.FirstOrDefault(s => s.TeamName == "Borussia Dortmund");
        await Assert.That(dortmund).IsNotNull();
        await Assert.That(dortmund!.Position).IsEqualTo(2);
        await Assert.That(dortmund.Points).IsEqualTo(32);
        
        // Verify last place team
        var mainz = standings.FirstOrDefault(s => s.TeamName == "FSV Mainz 05");
        await Assert.That(mainz).IsNotNull();
        await Assert.That(mainz!.Position).IsEqualTo(18);
        await Assert.That(mainz.Points).IsEqualTo(8);
    }
}
