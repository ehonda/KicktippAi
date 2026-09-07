using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Wm26RecentHistory;

namespace Orchestrator.Commands.Operations.Dev;

public sealed record CompetitionCollectorExecutionContext(
    CompetitionCollectionProfile Profile,
    string Community,
    string CommunityContext,
    string? Matchdays,
    bool FullSeason,
    string RecentHistoryDateMap,
    bool DryRun,
    bool Verbose,
    string? MarkdownSummaryOutput,
    CompetitionContextSourceCycleInvocation? ContextSourceCycle = null);

public interface ICompetitionProfileCollectorExecutor
{
    Task<ContextSourceCyclePreparation?> PrepareContextSourcesAsync(
        CompetitionCollectorExecutionContext context,
        CancellationToken cancellationToken = default) => Task.FromResult<ContextSourceCyclePreparation?>(null);

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
                    FullSeason = context.FullSeason,
                    ExpectedMatchCount = context.Profile.ExpectedMatchCount,
                    ExpectedMatchesPerMatchday = context.Profile.ExpectedMatchesPerMatchday,
                    DryRun = context.DryRun,
                    Verbose = context.Verbose,
                    MarkdownSummaryOutput = context.MarkdownSummaryOutput
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

    public Task<ContextSourceCyclePreparation?> PrepareContextSourcesAsync(
        CompetitionCollectorExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ContextSourceCycleCoordinator.ExecuteIfEnabledAsync(
            context.Profile.ContextSourceFeatures.ClubEloEnabled,
            context.Profile.ContextSourceFeatures.RostersEnabled,
            () => ActivatorUtilities.CreateInstance<ContextSourceCycleCoordinator>(_serviceProvider),
            async (coordinator, enabled) =>
            {
                var invocation = context.ContextSourceCycle ?? CreateDevelopmentInvocation(context.Profile.Competition, context.CommunityContext);
                invocation.Validate();
                BundesligaContextSourceContract.ValidateConsumerAuthority(invocation.Identity.Scope, invocation.CurrentLaneId, context.CommunityContext);
                var nowValue = DateTimeOffset.UtcNow;
                var now = new DateTimeOffset(nowValue.UtcDateTime.Ticks - nowValue.UtcDateTime.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
                var cycle = new BundesligaContextSourceOuterCycle(invocation.Identity, now, now, invocation.ProducerLaneId,
                    invocation.ExpectedConsumers, enabled, BundesligaContextSourceCycleStatus.Claiming);
                var artifactStore = invocation.Identity.Scope == BundesligaContextSourceScope.ProductionLive
                    ? _serviceProvider.GetRequiredService<IContextSourceBundleArtifactStore>()
                    : null;
                return await coordinator.PrepareAsync(
                    new ContextSourceCycleRequest(cycle, enabled, context.DryRun, invocation.CurrentLaneId),
                    artifactStore,
                    cancellationToken);
            });
    }

    private static CompetitionContextSourceCycleInvocation CreateDevelopmentInvocation(string competition, string communityContext)
    {
        BundesligaContextSourceContract.ValidateConsumerAuthority(BundesligaContextSourceScope.Development, BundesligaContextSourceContract.DevelopmentLane, communityContext);
        var uuid = Guid.CreateVersion7().ToString("D").ToLowerInvariant();
        return new CompetitionContextSourceCycleInvocation(
            BundesligaContextSourceCycleIdentity.Development(competition, uuid),
            BundesligaContextSourceContract.DevelopmentLane,
            BundesligaContextSourceContract.DevelopmentLane,
            BundesligaContextSourceContract.DevelopmentConsumers);
    }

    private TCommand Create<TCommand>() where TCommand : notnull
    {
        return ActivatorUtilities.CreateInstance<TCommand>(_serviceProvider);
    }
}
