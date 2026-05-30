using ContextProviders.Kicktipp;
using Microsoft.Extensions.Logging;
using Orchestrator.Commands.Operations.CollectContext;
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
        ILogger<CollectContextLineupsCommand> lineupsLogger)
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

        _console.MarkupLine(
            $"[yellow]collect-context-dev dev preset enabled - will update WM26 context for {Markup.Escape(community)}[/]");

        var kicktippExitCode = await _kicktippCommand.ExecuteWithSettingsAsync(
            new CollectContextKicktippSettings
            {
                CommunityContext = communityContext,
                Competition = competition,
                Matchdays = settings.Matchdays,
                DryRun = settings.DryRun,
                Verbose = settings.Verbose
            },
            cancellationToken);

        if (kicktippExitCode != 0)
        {
            return kicktippExitCode;
        }

        var fifaExitCode = await _fifaCommand.ExecuteWithSettingsAsync(
            new CollectContextFifaSettings
            {
                CommunityContext = communityContext,
                Competition = competition,
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
                Competition = competition,
                DryRun = settings.DryRun,
                Verbose = settings.Verbose
            },
            cancellationToken);
    }
}
