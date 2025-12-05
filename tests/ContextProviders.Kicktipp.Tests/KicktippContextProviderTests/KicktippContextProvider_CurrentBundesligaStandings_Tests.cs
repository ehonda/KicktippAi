using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;

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
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses
            1,FC Bayern München,10,25,30:10,30,10,8,1,1
            2,Borussia Dortmund,10,22,28:12,28,12,7,1,2
            3,RB Leipzig,10,20,22:14,22,14,6,2,2

            """;
        await Assert.That(NormalizeLineEndings(context.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
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
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses

            """;
        await Assert.That(NormalizeLineEndings(context.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }
}
