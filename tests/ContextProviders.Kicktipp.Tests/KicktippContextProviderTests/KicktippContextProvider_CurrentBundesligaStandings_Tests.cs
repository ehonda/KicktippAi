using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using TUnit.Core;
using TestUtilities.StringAssertions;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_CurrentBundesligaStandings_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_standings_returns_correct_document_name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.CurrentBundesligaStandings();

        // Assert
        await Assert.That(context.Name).IsEqualTo("bundesliga-standings.csv");
    }

    [Test]
    public async Task Getting_standings_returns_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.CurrentBundesligaStandings();

        // Assert - verify exact CSV output
        var expectedCsv = """
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses,Group
            1,FC Bayern München,10,25,30:10,30,10,8,1,1,
            2,Borussia Dortmund,10,22,28:12,28,12,7,1,2,
            3,RB Leipzig,10,20,22:14,22,14,6,2,2,

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_standings_with_empty_list_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient(standings: new List<TeamStanding>());
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.CurrentBundesligaStandings();

        // Assert
        var expectedCsv = """
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses,Group

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_world_cup_standings_returns_world_cup_document_name_and_group_column()
    {
        var standings = new List<TeamStanding>
        {
            new(1, "Germany", 2, 6, 5, 1, 4, 2, 0, 0, "Gruppe A")
        };
        var mockClient = CreateMockKicktippClient(standings: standings);
        var provider = CreateProvider(
            Option.Some(mockClient.Object),
            competition: Option.Some(CompetitionIds.FifaWorldCup2026));

        var context = await provider.CurrentStandings();

        await Assert.That(context.Name).IsEqualTo("fifa-world-cup-2026-standings.csv");
        await Assert.That(context.Content).Contains("Group");
        await Assert.That(context.Content).Contains("Gruppe A");
    }
}
