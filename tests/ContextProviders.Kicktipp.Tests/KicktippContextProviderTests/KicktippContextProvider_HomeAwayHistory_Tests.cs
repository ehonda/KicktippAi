using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;

using Match = EHonda.KicktippAi.Core.Match;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_HomeAwayHistory_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_home_history_returns_correct_document_name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert - uses home team abbreviation
        await Assert.That(context.Name).IsEqualTo("home-history-fcb.csv");
    }

    [Test]
    public async Task Getting_away_history_returns_correct_document_name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.AwayHistory(TestHomeTeam, TestAwayTeam);

        // Assert - uses away team abbreviation
        await Assert.That(context.Name).IsEqualTo("away-history-bvb.csv");
    }

    [Test]
    public async Task Getting_home_history_returns_correct_csv_format()
    {
        // Arrange
        var homeHistory = new List<MatchResult>
        {
            new(
                Competition: "1.BL",
                HomeTeam: "FC Bayern München",
                AwayTeam: "VfB Stuttgart",
                HomeGoals: 4,
                AwayGoals: 0,
                Outcome: MatchOutcome.Win,
                Annotation: null),
            new(
                Competition: "1.BL",
                HomeTeam: "FC Bayern München",
                AwayTeam: "Werder Bremen",
                HomeGoals: 2,
                AwayGoals: 2,
                Outcome: MatchOutcome.Draw,
                Annotation: null)
        };

        var mockClient = CreateMockKicktippClient();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((homeHistory, []));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,4:0,
            1.BL,FC Bayern München,Werder Bremen,2:2,

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_away_history_returns_correct_csv_format()
    {
        // Arrange
        var awayHistory = new List<MatchResult>
        {
            new(
                Competition: "1.BL",
                HomeTeam: "VfB Stuttgart",
                AwayTeam: "Borussia Dortmund",
                HomeGoals: 1,
                AwayGoals: 3,
                Outcome: MatchOutcome.Win,
                Annotation: null),
            new(
                Competition: "DFB",
                HomeTeam: "1. FC Köln",
                AwayTeam: "Borussia Dortmund",
                HomeGoals: 0,
                AwayGoals: 2,
                Outcome: MatchOutcome.Win,
                Annotation: "nach Verlängerung")
        };

        var mockClient = CreateMockKicktippClient();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(([], awayHistory));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.AwayHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,VfB Stuttgart,Borussia Dortmund,1:3,
            DFB,1. FC Köln,Borussia Dortmund,0:2,nach Verlängerung

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_home_history_with_empty_data_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(([], []));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_away_history_with_empty_data_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(([], []));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.AwayHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }

    [Test]
    public async Task Getting_home_history_handles_null_goals()
    {
        // Arrange
        var homeHistory = new List<MatchResult>
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
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((homeHistory, []));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert - null goals should result in ":" score
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,:,

            """;
        await Assert.That(context.Content).IsEqualTo(expectedCsv);
    }
}
