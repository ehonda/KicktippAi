using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Moq;
using TUnit.Core;
using TestUtilities.StringAssertions;

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
                "1.BL",
                "FC Bayern München",
                "VfB Stuttgart",
                4,
                0,
                MatchOutcome.Win,
                null),
            new(
                "1.BL",
                "FC Bayern München",
                "Werder Bremen",
                2,
                2,
                MatchOutcome.Draw,
                null)
        };

        var mockClient = CreateMockKicktippClient(homeAwayHistory: (homeHistory, new List<MatchResult>()));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,4:0,
            1.BL,FC Bayern München,Werder Bremen,2:2,

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_away_history_returns_correct_csv_format()
    {
        // Arrange
        var awayHistory = new List<MatchResult>
        {
            new(
                "1.BL",
                "VfB Stuttgart",
                "Borussia Dortmund",
                1,
                3,
                MatchOutcome.Win,
                null),
            new(
                "DFB",
                "1. FC Köln",
                "Borussia Dortmund",
                0,
                2,
                MatchOutcome.Win,
                "nach Verlängerung")
        };

        var mockClient = CreateMockKicktippClient(homeAwayHistory: (new List<MatchResult>(), awayHistory));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.AwayHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,VfB Stuttgart,Borussia Dortmund,1:3,
            DFB,1. FC Köln,Borussia Dortmund,0:2,nach Verlängerung

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_home_history_with_empty_data_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient(homeAwayHistory: (new List<MatchResult>(), new List<MatchResult>()));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_away_history_with_empty_data_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient(homeAwayHistory: (new List<MatchResult>(), new List<MatchResult>()));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.AwayHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_home_history_handles_null_goals()
    {
        // Arrange
        var homeHistory = new List<MatchResult>
        {
            new(
                "1.BL",
                "FC Bayern München",
                "VfB Stuttgart",
                null,
                null,
                MatchOutcome.Pending,
                null)
        };

        var mockClient = CreateMockKicktippClient(homeAwayHistory: (homeHistory, new List<MatchResult>()));
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HomeHistory(TestHomeTeam, TestAwayTeam);

        // Assert - null goals should result in empty score
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,,

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_home_history_propagates_exceptions_from_client()
    {
        // Arrange
        var mockClient = new Mock<IKicktippClient>();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));
        
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act & Assert - exception should propagate, not be swallowed
        await Assert.That(() => provider.HomeHistory(TestHomeTeam, TestAwayTeam)!)
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Getting_away_history_propagates_exceptions_from_client()
    {
        // Arrange
        var mockClient = new Mock<IKicktippClient>();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());
        mockClient.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));
        
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act & Assert - exception should propagate, not be swallowed
        await Assert.That(() => provider.AwayHistory(TestHomeTeam, TestAwayTeam)!)
            .Throws<InvalidOperationException>();
    }
}
