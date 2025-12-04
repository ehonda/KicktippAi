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
        Option<string?> communityContext = default)
    {
        var actualKicktippClient = kicktippClient.Or(() => CreateMockKicktippClient().Object);
        var actualCommunity = community.Or(TestCommunity);
        var actualCommunityContext = communityContext.Or((string?)TestCommunity);

        return new KicktippContextProvider(actualKicktippClient!, actualCommunity!, actualCommunityContext);
    }

    protected static Mock<IKicktippClient> CreateMockKicktippClient()
    {
        var mock = new Mock<IKicktippClient>();

        // Setup default returns
        mock.Setup(c => c.GetStandingsAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestStandings());

        mock.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchesWithHistory());

        mock.Setup(c => c.GetHomeAwayHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((CreateTestMatchResults("HomeHistory"), CreateTestMatchResults("AwayHistory")));

        mock.Setup(c => c.GetHeadToHeadHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(CreateTestMatchResults("H2H"));

        mock.Setup(c => c.GetHeadToHeadDetailedHistoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(CreateTestHeadToHeadResults());

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
            HomeTeam: TestHomeTeam,
            AwayTeam: TestAwayTeam,
            StartsAt: new ZonedDateTime(
                Instant.FromUtc(2025, 12, 7, 15, 30),
                DateTimeZone.Utc),
            Matchday: 15);

        return
        [
            new MatchWithHistory(
                Match: match,
                HomeTeamHistory: CreateTestMatchResults(TestHomeTeam),
                AwayTeamHistory: CreateTestMatchResults(TestAwayTeam))
        ];
    }

    protected static List<MatchResult> CreateTestMatchResults(string context = "Test")
    {
        return
        [
            new MatchResult(
                Competition: "1.BL",
                HomeTeam: "FC Bayern München",
                AwayTeam: "VfB Stuttgart",
                HomeGoals: 3,
                AwayGoals: 1,
                Outcome: MatchOutcome.Win,
                Annotation: null),
            new MatchResult(
                Competition: "1.BL",
                HomeTeam: "RB Leipzig",
                AwayTeam: "FC Bayern München",
                HomeGoals: 1,
                AwayGoals: 1,
                Outcome: MatchOutcome.Draw,
                Annotation: null),
            new MatchResult(
                Competition: "DFB",
                HomeTeam: "FC Bayern München",
                AwayTeam: "1. FC Köln",
                HomeGoals: 5,
                AwayGoals: 0,
                Outcome: MatchOutcome.Win,
                Annotation: null)
        ];
    }

    protected static List<HeadToHeadResult> CreateTestHeadToHeadResults()
    {
        return
        [
            new HeadToHeadResult(
                League: "1.BL 2024/25",
                Matchday: "5. Spieltag",
                PlayedAt: "2024-09-28",
                HomeTeam: "Borussia Dortmund",
                AwayTeam: "FC Bayern München",
                Score: "1:5",
                Annotation: null),
            new HeadToHeadResult(
                League: "1.BL 2023/24",
                Matchday: "27. Spieltag",
                PlayedAt: "2024-03-30",
                HomeTeam: "FC Bayern München",
                AwayTeam: "Borussia Dortmund",
                Score: "0:2",
                Annotation: null),
            new HeadToHeadResult(
                League: "DFB 2022/23",
                Matchday: "Achtelfinale",
                PlayedAt: "2023-02-01",
                HomeTeam: "FC Bayern München",
                AwayTeam: "Borussia Dortmund",
                Score: "2:1",
                Annotation: "nach Verlängerung")
        ];
    }
}
