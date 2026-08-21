using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Wm26RecentHistory;

namespace Orchestrator.Commands.Operations.Dev;

public sealed record CompetitionCollectorExecutionContext(
    CompetitionCollectionProfile Profile,
    string Community,
    string CommunityContext,
    string? Matchdays,
    string RecentHistoryDateMap,
    bool DryRun,
    bool Verbose);

public interface ICompetitionProfileCollectorExecutor
{
    Task<int> ExecuteAsync(
        CompetitionCollector collector,
        CompetitionCollectorExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed class CompetitionProfileCollectorExecutor : ICompetitionProfileCollectorExecutor
{
    private readonly IServiceProvider _serviceProvider;

    public CompetitionProfileCollectorExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<int> ExecuteAsync(
        CompetitionCollector collector,
        CompetitionCollectorExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return collector switch
        {
            CompetitionCollector.Kicktipp => Create<CollectContextKicktippCommand>().ExecuteWithSettingsAsync(
                new CollectContextKicktippSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    Matchdays = context.Matchdays,
                    ExpectedMatchesPerMatchday = context.Profile.ExpectedMatchesPerMatchday,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.Wm26HistoryPlayedDates => Create<Wm26RecentHistoryApplyDateMapCommand>().ExecuteWithSettingsAsync(
                new Wm26RecentHistoryApplyDateMapSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    Input = context.RecentHistoryDateMap,
                    ApplyKnownOnly = true,
                    PreserveCollectedOnOrAfter = "2026-06-11",
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.FifaRankings => Create<CollectContextFifaCommand>().ExecuteWithSettingsAsync(
                new CollectContextFifaSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.NationalLineups => Create<CollectContextLineupsCommand>().ExecuteWithSettingsAsync(
                new CollectContextLineupsSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.ClubElo => Create<CollectContextClubEloCommand>().ExecuteWithSettingsAsync(
                new CollectContextClubEloSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.Rosters => Create<CollectContextRostersCommand>().ExecuteWithSettingsAsync(
                new CollectContextRostersSettings
                {
                    CommunityContext = context.CommunityContext,
                    Competition = context.Profile.Competition,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose
                },
                cancellationToken),
            CompetitionCollector.BundesligaHistoryPlayedDates => throw new InvalidOperationException(
                "BundesligaHistoryPlayedDates is included in the immediately preceding Kicktipp collector and must not be invoked separately."),
            _ => throw new ArgumentOutOfRangeException(nameof(collector), collector, "Unsupported profile collector.")
        };
    }

    private TCommand Create<TCommand>() where TCommand : notnull
    {
        return ActivatorUtilities.CreateInstance<TCommand>(_serviceProvider);
    }
}
