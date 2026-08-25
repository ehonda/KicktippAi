using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class BundesligaFullSeasonHistorySelectorTests
{
    [Test]
    public async Task Selector_returns_exact_accepted_names_and_semantic_fixture_roles()
    {
        var schedule = CollectContextKicktippCommand_FullSeason_Tests.CreateFullSeasonSchedule()
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<MatchWithHistory>)pair.Value);

        var requests = BundesligaFullSeasonHistorySelector.Select(schedule);

        await Assert.That(requests).Count().IsEqualTo(54);
        await Assert.That(requests.Select(request => request.DocumentName).ToHashSet(StringComparer.Ordinal)
            .SetEquals(BundesligaHistoryPlayedDateMap.ExpectedDocumentNames)).IsTrue();
        await Assert.That(requests.All(request => request.Matchday is 1 or 2)).IsTrue();
        await Assert.That(requests
            .Where(request => request.Kind == BundesligaCanonicalHistoryKind.Recent)
            .All(request => request.Matchday == 1
                            && (request.Match.HomeTeam == request.TeamName
                                || request.Match.AwayTeam == request.TeamName))).IsTrue();
        await Assert.That(requests
            .Where(request => request.Kind == BundesligaCanonicalHistoryKind.Home)
            .All(request => request.Match.HomeTeam == request.TeamName)).IsTrue();
        await Assert.That(requests
            .Where(request => request.Kind == BundesligaCanonicalHistoryKind.Away)
            .All(request => request.Match.AwayTeam == request.TeamName)).IsTrue();

        var vfb = requests.Where(request => request.DocumentName.EndsWith("-vfb.csv", StringComparison.Ordinal))
            .ToDictionary(request => request.Kind);
        await Assert.That(vfb[BundesligaCanonicalHistoryKind.Recent].Matchday).IsEqualTo(1);
        await Assert.That(vfb[BundesligaCanonicalHistoryKind.Away].Matchday).IsEqualTo(1);
        await Assert.That(vfb[BundesligaCanonicalHistoryKind.Home].Matchday).IsEqualTo(2);
    }

    [Test]
    public async Task Selector_rejects_a_role_whose_earliest_fixture_is_outside_the_accepted_two_matchdays()
    {
        var schedule = CollectContextKicktippCommand_FullSeason_Tests.CreateFullSeasonSchedule();
        var team = BundesligaTeamManifest.Default.Entries[0].KicktippName;
        foreach (var matchday in new[] { 1, 2 })
        {
            var fixture = schedule[matchday].SingleOrDefault(match => match.Match.HomeTeam == team);
            if (fixture is null)
            {
                continue;
            }

            var index = schedule[matchday].IndexOf(fixture);
            schedule[matchday][index] = fixture with
            {
                Match = fixture.Match with
                {
                    HomeTeam = fixture.Match.AwayTeam,
                    AwayTeam = fixture.Match.HomeTeam
                }
            };
        }

        var typed = schedule.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MatchWithHistory>)pair.Value);

        var exception = await Assert.That(() => BundesligaFullSeasonHistorySelector.Select(typed))
            .Throws<InvalidDataException>();
        await Assert.That(exception!.Message).Contains("accepted ADR-0032 inventory");
    }
}
