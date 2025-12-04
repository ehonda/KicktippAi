using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;

using Match = EHonda.KicktippAi.Core.Match;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_RecentHistory_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_recent_history_returns_correct_document_name_with_abbreviation()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.RecentHistory(TestHomeTeam);

        // Assert - FC Bayern München abbreviates to "fcb"
        await Assert.That(context.Name).IsEqualTo("recent-history-fcb.csv");
    }

    [Test]
    public async Task Getting_recent_history_returns_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.RecentHistory(TestHomeTeam);

        // Assert - verify exact CSV output
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,3:1,
            1.BL,RB Leipzig,FC Bayern München,1:1,
            DFB,FC Bayern München,1. FC Köln,5:0,

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_recent_history_for_unknown_team_returns_empty_csv()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.RecentHistory("Unknown Team FC");

        // Assert
        await Assert.That(context.Name).IsEqualTo("recent-history-utf.csv"); // Unknown Team FC → utf
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_recent_history_handles_pending_matches()
    {
        // Arrange
        var matchResults = new List<MatchResult>
        {
            new(
                Competition: "1.BL",
                HomeTeam: "FC Bayern München",
                AwayTeam: "VfB Stuttgart",
                HomeGoals: null,
                AwayGoals: null,
                Outcome: MatchOutcome.Pending,
                Annotation: null)
        };

        var mockClient = CreateMockKicktippClient();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(
            [
                    new MatchWithHistory(
                        Match: new Match(TestHomeTeam, TestAwayTeam, default, 15),
                        HomeTeamHistory: matchResults,
                        AwayTeamHistory: [])
            ]);
        var provider = CreateProvider(Option.Some(mockClient.Object));        // Act
        var context = await provider.RecentHistory(TestHomeTeam);

        // Assert - pending matches should have empty score
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,,

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }
}
