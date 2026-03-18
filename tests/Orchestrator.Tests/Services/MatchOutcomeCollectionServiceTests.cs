using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Services;

namespace Orchestrator.Tests.Services;

public class MatchOutcomeCollectionServiceTests
{
    [Test]
    public async Task Dry_run_reports_matchday_counts_without_persisting_outcomes()
    {
        var communityContext = "test-community";
        var outcomeRepository = new Mock<IMatchOutcomeRepository>();
        outcomeRepository.Setup(r => r.GetIncompleteMatchdaysAsync(communityContext, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync([24]);

        var client = new Mock<IKicktippClient>();
        client.Setup(c => c.GetCurrentTippuebersichtMatchdayAsync(communityContext))
            .ReturnsAsync(25);
        client.Setup(c => c.GetMatchdayOutcomesAsync(communityContext, 24))
            .ReturnsAsync(
            [
                CreateCollectedOutcome("FC Bayern München", "Borussia Dortmund", 24, MatchOutcomeAvailability.Completed, 2, 1),
                CreateCollectedOutcome("RB Leipzig", "1. FSV Mainz 05", 24, MatchOutcomeAvailability.Pending, null, null)
            ]);

        var service = CreateService(client, outcomeRepository);

        var result = await service.CollectAsync(communityContext, dryRun: true);

        await Assert.That(result.CurrentMatchday).IsEqualTo(25);
        await Assert.That(result.IncompleteMatchdays).HasCount().EqualTo(1);
        await Assert.That(result.MatchdaySummaries).HasCount().EqualTo(1);

        var summary = result.MatchdaySummaries.Single();
        await Assert.That(summary.Matchday).IsEqualTo(24);
        await Assert.That(summary.FetchedMatches).IsEqualTo(2);
        await Assert.That(summary.CompletedMatches).IsEqualTo(1);
        await Assert.That(summary.PendingMatches).IsEqualTo(1);
        await Assert.That(summary.CreatedCount).IsEqualTo(0);
        await Assert.That(summary.UpdatedCount).IsEqualTo(0);
        await Assert.That(summary.UnchangedCount).IsEqualTo(0);

        outcomeRepository.Verify(
            r => r.UpsertMatchOutcomeAsync(It.IsAny<CollectedMatchOutcome>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Non_dry_run_persists_each_outcome_and_groups_dispositions_per_matchday()
    {
        var communityContext = "test-community";
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var day24Created = CreateCollectedOutcome("FC Bayern München", "Borussia Dortmund", 24, MatchOutcomeAvailability.Completed, 2, 1);
        var day24Updated = CreateCollectedOutcome("VfB Stuttgart", "SC Freiburg", 24, MatchOutcomeAvailability.Completed, 3, 2);
        var day25Unchanged = CreateCollectedOutcome("RB Leipzig", "1. FC Union Berlin", 25, MatchOutcomeAvailability.Pending, null, null);

        var outcomeRepository = new Mock<IMatchOutcomeRepository>();
        outcomeRepository.Setup(r => r.GetIncompleteMatchdaysAsync(communityContext, 25, cancellationToken))
            .ReturnsAsync([24, 25]);
        outcomeRepository.Setup(r => r.UpsertMatchOutcomeAsync(It.IsAny<CollectedMatchOutcome>(), communityContext, cancellationToken))
            .ReturnsAsync((CollectedMatchOutcome outcome, string _, CancellationToken _) => CreateUpsertResult(
                outcome.HomeTeam switch
                {
                    "FC Bayern München" => MatchOutcomeWriteDisposition.Created,
                    "VfB Stuttgart" => MatchOutcomeWriteDisposition.Updated,
                    _ => MatchOutcomeWriteDisposition.Unchanged
                },
                outcome,
                communityContext));

        var client = new Mock<IKicktippClient>();
        client.Setup(c => c.GetCurrentTippuebersichtMatchdayAsync(communityContext))
            .ReturnsAsync(25);
        client.Setup(c => c.GetMatchdayOutcomesAsync(communityContext, 24))
            .ReturnsAsync([day24Created, day24Updated]);
        client.Setup(c => c.GetMatchdayOutcomesAsync(communityContext, 25))
            .ReturnsAsync([day25Unchanged]);

        var service = CreateService(client, outcomeRepository);

        var result = await service.CollectAsync(communityContext, dryRun: false, cancellationToken);

        await Assert.That(result.MatchdaySummaries).HasCount().EqualTo(2);

        var firstSummary = result.MatchdaySummaries[0];
        await Assert.That(firstSummary.Matchday).IsEqualTo(24);
        await Assert.That(firstSummary.FetchedMatches).IsEqualTo(2);
        await Assert.That(firstSummary.CompletedMatches).IsEqualTo(2);
        await Assert.That(firstSummary.PendingMatches).IsEqualTo(0);
        await Assert.That(firstSummary.CreatedCount).IsEqualTo(1);
        await Assert.That(firstSummary.UpdatedCount).IsEqualTo(1);
        await Assert.That(firstSummary.UnchangedCount).IsEqualTo(0);

        var secondSummary = result.MatchdaySummaries[1];
        await Assert.That(secondSummary.Matchday).IsEqualTo(25);
        await Assert.That(secondSummary.FetchedMatches).IsEqualTo(1);
        await Assert.That(secondSummary.CompletedMatches).IsEqualTo(0);
        await Assert.That(secondSummary.PendingMatches).IsEqualTo(1);
        await Assert.That(secondSummary.CreatedCount).IsEqualTo(0);
        await Assert.That(secondSummary.UpdatedCount).IsEqualTo(0);
        await Assert.That(secondSummary.UnchangedCount).IsEqualTo(1);

        outcomeRepository.Verify(r => r.GetIncompleteMatchdaysAsync(communityContext, 25, cancellationToken), Times.Once);
        outcomeRepository.Verify(r => r.UpsertMatchOutcomeAsync(day24Created, communityContext, cancellationToken), Times.Once);
        outcomeRepository.Verify(r => r.UpsertMatchOutcomeAsync(day24Updated, communityContext, cancellationToken), Times.Once);
        outcomeRepository.Verify(r => r.UpsertMatchOutcomeAsync(day25Unchanged, communityContext, cancellationToken), Times.Once);
    }

    [Test]
    public async Task No_incomplete_matchdays_returns_empty_summary_without_fetching_outcomes()
    {
        var communityContext = "test-community";
        var outcomeRepository = new Mock<IMatchOutcomeRepository>();
        outcomeRepository.Setup(r => r.GetIncompleteMatchdaysAsync(communityContext, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var client = new Mock<IKicktippClient>();
        client.Setup(c => c.GetCurrentTippuebersichtMatchdayAsync(communityContext))
            .ReturnsAsync(25);

        var service = CreateService(client, outcomeRepository);

        var result = await service.CollectAsync(communityContext, dryRun: false);

        await Assert.That(result.IncompleteMatchdays).IsEmpty();
        await Assert.That(result.MatchdaySummaries).IsEmpty();

        client.Verify(c => c.GetMatchdayOutcomesAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        outcomeRepository.Verify(
            r => r.UpsertMatchOutcomeAsync(It.IsAny<CollectedMatchOutcome>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static MatchOutcomeCollectionService CreateService(
        Mock<IKicktippClient> client,
        Mock<IMatchOutcomeRepository> outcomeRepository)
    {
        var kicktippClientFactory = new Mock<IKicktippClientFactory>();
        kicktippClientFactory.Setup(f => f.CreateClient()).Returns(client.Object);

        var firebaseServiceFactory = new Mock<IFirebaseServiceFactory>();
        firebaseServiceFactory.Setup(f => f.CreateMatchOutcomeRepository()).Returns(outcomeRepository.Object);

        return new MatchOutcomeCollectionService(
            firebaseServiceFactory.Object,
            kicktippClientFactory.Object,
            new FakeLogger<MatchOutcomeCollectionService>());
    }

    private static CollectedMatchOutcome CreateCollectedOutcome(
        string homeTeam,
        string awayTeam,
        int matchday,
        MatchOutcomeAvailability availability,
        int? homeGoals,
        int? awayGoals)
    {
        return new CollectedMatchOutcome(
            homeTeam,
            awayTeam,
            Instant.FromUtc(2025, 3, 15, 15, 30).InUtc(),
            matchday,
            homeGoals,
            awayGoals,
            availability,
            $"{matchday}-{homeTeam}-{awayTeam}");
    }

    private static MatchOutcomeUpsertResult CreateUpsertResult(
        MatchOutcomeWriteDisposition disposition,
        CollectedMatchOutcome outcome,
        string communityContext)
    {
        return new MatchOutcomeUpsertResult(
            disposition,
            new PersistedMatchOutcome(
                communityContext,
                "Bundesliga",
                outcome.HomeTeam,
                outcome.AwayTeam,
                outcome.StartsAt,
                outcome.Matchday,
                outcome.HomeGoals,
                outcome.AwayGoals,
                outcome.Availability,
                outcome.TippSpielId,
                new DateTimeOffset(2025, 3, 15, 15, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 3, 15, 15, 30, 0, TimeSpan.Zero)));
    }
}
