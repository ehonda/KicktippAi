using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Moq;
using NodaTime;
using TUnit.Core;

using Match = EHonda.KicktippAi.Core.Match;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public abstract class KicktippContextProviderTests_Base
{
    protected const string TestCommunity = "ehonda-test-buli";
    protected const string TestHomeTeam = "FC Bayern München";
    protected const string TestAwayTeam = "Borussia Dortmund";

    protected static KicktippContextProvider CreateProvider(
        NullableOption<IKicktippClient> kicktippClient = default,
        NullableOption<string> community = default,
        NullableOption<string> communityContext = default)
    {
        var actualKicktippClient = kicktippClient.Or(() => CreateMockKicktippClient().Object);
        var actualCommunity = community.Or(TestCommunity);
        var actualCommunityContext = communityContext.Or(TestCommunity);

        return new KicktippContextProvider(actualKicktippClient!, actualCommunity!, actualCommunityContext);
    }

    protected static Mock<IKicktippClient> CreateMockKicktippClient(
        Option<List<TeamStanding>> standings = default,
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<(List<MatchResult> Home, List<MatchResult> Away)> homeAwayHistory = default,
        Option<List<MatchResult>> headToHeadHistory = default,
        Option<List<HeadToHeadResult>> headToHeadDetailedHistory = default)
    {
        var mock = new Mock<IKicktippClient>();

        // Setup default returns
        mock.Setup(c => c.GetStandingsAsync(It.IsAny<string>()))
            .ReturnsAsync(standings.Or(CreateTestStandings));

        mock.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(matchesWithHistory.Or(CreateTestMatchesWithHistory));

        mock.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(homeAwayHistory.Or(() => (CreateTestMatchResults("HomeHistory"), CreateTestMatchResults("AwayHistory"))));

        mock.Setup(c => c.GetHeadToHeadHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(headToHeadHistory.Or(() => CreateTestMatchResults("H2H")));

        mock.Setup(c => c.GetHeadToHeadDetailedHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(headToHeadDetailedHistory.Or(CreateTestHeadToHeadResults));

        return mock;
    }

    protected static List<TeamStanding> CreateTestStandings()
    {
        return
        [
            new TeamStanding(1, "FC Bayern München", 10, 25, 30, 10, 20, 8, 1, 1),
            new TeamStanding(2, "Borussia Dortmund", 10, 22, 28, 12, 16, 7, 1, 2),
            new TeamStanding(3, "RB Leipzig", 10, 20, 22, 14, 8, 6, 2, 2)
        ];
    }

    protected static List<MatchWithHistory> CreateTestMatchesWithHistory()
    {
        var match = new Match(
            TestHomeTeam,
            TestAwayTeam,
            new ZonedDateTime(
                Instant.FromUtc(2025, 12, 7, 15, 30),
                DateTimeZone.Utc),
            15);

        return
        [
            new MatchWithHistory(
                match,
                CreateTestMatchResults(TestHomeTeam),
                CreateTestMatchResults(TestAwayTeam))
        ];
    }

    protected static List<MatchResult> CreateTestMatchResults(string context = "Test")
    {
        return
        [
            new MatchResult(
                "1.BL",
                "FC Bayern München",
                "VfB Stuttgart",
                3,
                1,
                MatchOutcome.Win,
                null),
            new MatchResult(
                "1.BL",
                "RB Leipzig",
                "FC Bayern München",
                1,
                1,
                MatchOutcome.Draw,
                null),
            new MatchResult(
                "DFB",
                "FC Bayern München",
                "1. FC Köln",
                5,
                0,
                MatchOutcome.Win,
                null)
        ];
    }

    protected static List<HeadToHeadResult> CreateTestHeadToHeadResults()
    {
        return
        [
            new HeadToHeadResult(
                "1.BL 2024/25",
                "5. Spieltag",
                "2024-09-28",
                "Borussia Dortmund",
                "FC Bayern München",
                "1:5",
                null),
            new HeadToHeadResult(
                "1.BL 2023/24",
                "27. Spieltag",
                "2024-03-30",
                "FC Bayern München",
                "Borussia Dortmund",
                "0:2",
                null),
            new HeadToHeadResult(
                "DFB 2022/23",
                "Achtelfinale",
                "2023-02-01",
                "FC Bayern München",
                "Borussia Dortmund",
                "2:1",
                "nach Verlängerung")
        ];
    }
}
