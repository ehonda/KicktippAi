using ContextProviders.Kicktipp;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Wm26RecentHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class CollectContextDevCommand : AsyncCommand<CollectContextDevSettings>
{
    private readonly IAnsiConsole _console;
    private readonly CollectContextKicktippCommand _kicktippCommand;
    private readonly CollectContextFifaCommand _fifaCommand;
    private readonly CollectContextLineupsCommand _lineupsCommand;
    private readonly Wm26RecentHistoryApplyDateMapCommand _recentHistoryDateMapCommand;

    public CollectContextDevCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IContextProviderFactory contextProviderFactory,
        MatchOutcomeCollectionService matchOutcomeCollectionService,
        IFifaRankingSource fifaRankingSource,
        IWm26LineupSource lineupSource,
        ILogger<CollectContextKicktippCommand> kicktippLogger,
        ILogger<CollectContextFifaCommand> fifaLogger,
        ILogger<CollectContextLineupsCommand> lineupsLogger,
        ILogger<Wm26RecentHistoryApplyDateMapCommand> recentHistoryDateMapLogger)
    {
        _console = console;
        _kicktippCommand = new CollectContextKicktippCommand(
            console,
            firebaseServiceFactory,
            kicktippClientFactory,
            contextProviderFactory,
            matchOutcomeCollectionService,
            kicktippLogger);
        _fifaCommand = new CollectContextFifaCommand(
            console,
            firebaseServiceFactory,
            fifaRankingSource,
            fifaLogger);
        _lineupsCommand = new CollectContextLineupsCommand(
            console,
            firebaseServiceFactory,
            lineupSource,
            lineupsLogger);
        _recentHistoryDateMapCommand = new Wm26RecentHistoryApplyDateMapCommand(
            console,
            firebaseServiceFactory,
            recentHistoryDateMapLogger);
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextDevSettings settings,
        CancellationToken cancellationToken)
    {
        if (!CompetitionResolver.IsDevCommunity(settings.Community))
        {
            var supportedCommunities = string.Join(", ", CompetitionResolver.SupportedDevCommunities);
            _console.MarkupLine(
                $"[red]Error:[/] collect-context-dev is only available for supported development communities: [yellow]{Markup.Escape(supportedCommunities)}[/]");
            return 1;
        }

        var community = settings.Community.Trim();
        var communityContext = string.IsNullOrWhiteSpace(settings.CommunityContext)
            ? community
            : settings.CommunityContext.Trim();
        var competition = string.IsNullOrWhiteSpace(settings.Competition)
            ? null
            : settings.Competition.Trim();
        var resolvedCompetition = CompetitionResolver.ResolveCompetition(
            competition,
            community,
            communityContext);

        _console.MarkupLine(
            $"[yellow]collect-context-dev dev preset enabled - will update WM26 context for {Markup.Escape(community)}[/]");

        var kicktippExitCode = await _kicktippCommand.ExecuteWithSettingsAsync(
            new CollectContextKicktippSettings
            {
                CommunityContext = communityContext,
                Competition = resolvedCompetition,
                Matchdays = settings.Matchdays,
                DryRun = settings.DryRun,
                Verbose = settings.Verbose
            },
            cancellationToken);

        if (kicktippExitCode != 0)
        {
            return kicktippExitCode;
        }

        if (CompetitionResolver.IsWorldCupCompetition(resolvedCompetition))
        {
            var dateMapExitCode = await _recentHistoryDateMapCommand.ExecuteWithSettingsAsync(
                new Wm26RecentHistoryApplyDateMapSettings
                {
                    CommunityContext = communityContext,
                    Competition = resolvedCompetition,
                    Input = settings.RecentHistoryDateMap,
                    ApplyKnownOnly = true,
                    PreserveCollectedOnOrAfter = "2026-06-11",
                    DryRun = settings.DryRun,
                    Verbose = settings.Verbose
                },
                cancellationToken);

            if (dateMapExitCode != 0)
            {
                return dateMapExitCode;
            }
        }

        var fifaExitCode = await _fifaCommand.ExecuteWithSettingsAsync(
            new CollectContextFifaSettings
            {
                CommunityContext = communityContext,
                Competition = resolvedCompetition,
                DryRun = settings.DryRun,
                Verbose = settings.Verbose
            },
            cancellationToken);

        if (fifaExitCode != 0)
        {
            return fifaExitCode;
        }

        return await _lineupsCommand.ExecuteWithSettingsAsync(
            new CollectContextLineupsSettings
            {
                CommunityContext = communityContext,
                Competition = resolvedCompetition,
                DryRun = settings.DryRun,
                Verbose = settings.Verbose
            },
            cancellationToken);
    }
}
