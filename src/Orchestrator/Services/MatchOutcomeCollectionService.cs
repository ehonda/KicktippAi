using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Services;

public record MatchdayOutcomeCollectionSummary(
    int Matchday,
    int FetchedMatches,
    int CompletedMatches,
    int PendingMatches,
    int CreatedCount,
    int UpdatedCount,
    int UnchangedCount);

public record MatchOutcomeCollectionResult(
    int CurrentMatchday,
    IReadOnlyList<int> IncompleteMatchdays,
    IReadOnlyList<MatchdayOutcomeCollectionSummary> MatchdaySummaries);

public class MatchOutcomeCollectionService
{
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ILogger<MatchOutcomeCollectionService> _logger;

    public MatchOutcomeCollectionService(
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        ILogger<MatchOutcomeCollectionService> logger)
    {
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _logger = logger;
    }

    public async Task<MatchOutcomeCollectionResult> CollectAsync(
        string communityContext,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var matchOutcomeRepository = _firebaseServiceFactory.CreateMatchOutcomeRepository();

        var currentMatchday = await kicktippClient.GetCurrentTippuebersichtMatchdayAsync(communityContext);
        var incompleteMatchdays = await matchOutcomeRepository.GetIncompleteMatchdaysAsync(
            communityContext,
            currentMatchday,
            cancellationToken);

        var summaries = new List<MatchdayOutcomeCollectionSummary>();

        foreach (var matchday in incompleteMatchdays)
        {
            var outcomes = await kicktippClient.GetMatchdayOutcomesAsync(communityContext, matchday);
            var createdCount = 0;
            var updatedCount = 0;
            var unchangedCount = 0;

            if (!dryRun)
            {
                foreach (var outcome in outcomes)
                {
                    var result = await matchOutcomeRepository.UpsertMatchOutcomeAsync(outcome, communityContext, cancellationToken);
                    switch (result.Disposition)
                    {
                        case MatchOutcomeWriteDisposition.Created:
                            createdCount++;
                            break;
                        case MatchOutcomeWriteDisposition.Updated:
                            updatedCount++;
                            break;
                        default:
                            unchangedCount++;
                            break;
                    }
                }
            }

            summaries.Add(new MatchdayOutcomeCollectionSummary(
                matchday,
                outcomes.Count,
                outcomes.Count(outcome => outcome.HasOutcome),
                outcomes.Count(outcome => !outcome.HasOutcome),
                createdCount,
                updatedCount,
                unchangedCount));
        }

        _logger.LogInformation(
            "Outcome collection evaluated current matchday {CurrentMatchday} and selected {IncompleteMatchdayCount} incomplete matchdays for community {CommunityContext}",
            currentMatchday,
            incompleteMatchdays.Count,
            communityContext);

        return new MatchOutcomeCollectionResult(
            currentMatchday,
            incompleteMatchdays,
            summaries.AsReadOnly());
    }
}
