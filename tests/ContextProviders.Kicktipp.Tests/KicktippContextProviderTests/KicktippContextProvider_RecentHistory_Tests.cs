using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using TUnit.Core;
using TestUtilities.StringAssertions;

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
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_recent_history_for_unknown_bundesliga_team_fails_fast()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        await Assert.That(async () => await provider.RecentHistory("Unknown Team FC"))
            .Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task Getting_recent_history_handles_pending_matches()
    {
        // Arrange
        var matchResults = new List<MatchResult>
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

        var matchesWithHistory = new List<MatchWithHistory>
        {
            new(
                new Match(TestHomeTeam, TestAwayTeam, default, 15),
                matchResults,
                new List<MatchResult>())
        };

        var mockClient = CreateMockKicktippClient(matchesWithHistory: matchesWithHistory);
        var provider = CreateProvider(Option.Some(mockClient.Object));        // Act
        var context = await provider.RecentHistory(TestHomeTeam);

        // Assert - pending matches should have empty score
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,,

            """;
        await Assert.That(context.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Same_global_recent_history_name_can_have_different_fixture_scoped_bytes()
    {
        const string team = "VfB Stuttgart";
        var completed = new MatchResult(
            "DFB",
            "FC Hansa Rostock",
            team,
            0,
            4,
            MatchOutcome.Win);
        var pendingEarlierFixture = new MatchResult(
            "1.BL",
            "FC Bayern München",
            team,
            null,
            null,
            MatchOutcome.Pending);
        var matchday1 = new List<MatchWithHistory>
        {
            new(
                new Match("FC Bayern München", team, default, 1),
                [],
                [completed])
        };
        var matchday2 = new List<MatchWithHistory>
        {
            new(
                new Match(team, "1. FC Köln", default, 2),
                [pendingEarlierFixture, completed],
                [])
        };
        var client = CreateMockKicktippClient();
        client.Setup(value => value.GetMatchesWithHistoryAsync(
                TestCommunity,
                1,
                CompetitionIds.Bundesliga2026_27))
            .ReturnsAsync(matchday1);
        client.Setup(value => value.GetMatchesWithHistoryAsync(
                TestCommunity,
                2,
                CompetitionIds.Bundesliga2026_27))
            .ReturnsAsync(matchday2);
        var provider1 = CreateProvider(Option.Some(client.Object), matchday: 1);
        var provider2 = CreateProvider(Option.Some(client.Object), matchday: 2);

        var fromMatchday1 = await provider1.RecentHistory(team);
        var fromMatchday2 = await provider2.RecentHistory(team);

        await Assert.That(fromMatchday1.Name).IsEqualTo("recent-history-vfb.csv");
        await Assert.That(fromMatchday2.Name).IsEqualTo(fromMatchday1.Name);
        await Assert.That(fromMatchday2.Content).IsNotEqualTo(fromMatchday1.Content);
        await Assert.That(DocumentPublicationContract.ComputeContentSha256(fromMatchday2.Content))
            .IsNotEqualTo(DocumentPublicationContract.ComputeContentSha256(fromMatchday1.Content));
        await Assert.That(fromMatchday1.Content).DoesNotContain("FC Bayern München,VfB Stuttgart,,");
        await Assert.That(fromMatchday2.Content).Contains("FC Bayern München,VfB Stuttgart,,");
    }
}
