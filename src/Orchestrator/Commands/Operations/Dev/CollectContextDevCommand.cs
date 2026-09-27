using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class CollectContextDevCommand : AsyncCommand<CollectContextDevSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ICompetitionCollectionProfileResolver _profileResolver;
    private readonly ICompetitionProfileCollectorExecutor _collectorExecutor;

    public CollectContextDevCommand(
        IAnsiConsole console,
        ICompetitionCollectionProfileResolver profileResolver,
        ICompetitionProfileCollectorExecutor collectorExecutor)
    {
        _console = console;
        _profileResolver = profileResolver;
        _collectorExecutor = collectorExecutor;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextDevSettings settings,
        CancellationToken cancellationToken)
    {
        CompetitionCollectionProfile profile;
        try
        {
            profile = _profileResolver.ResolveForDevelopment(settings.Community, settings.Competition);
            profile = profile with { ContextSourceFeatures = new(settings.EnableClubEloSource, settings.EnableRosterSource) };
            if (settings.ContextSourceOnly)
                CompetitionProfileCollectionRunner.ValidateSourceOnly(profile, settings.Matchdays, settings.FullSeason);
            if (profile.ContextSourceFeatures.AnyEnabled)
                EHonda.KicktippAi.Core.BundesligaContextSourceContract.ValidateConsumerAuthority(
                    EHonda.KicktippAi.Core.BundesligaContextSourceScope.Development,
                    EHonda.KicktippAi.Core.BundesligaContextSourceContract.DevelopmentLane,
                    string.IsNullOrWhiteSpace(settings.CommunityContext) ? settings.Community.Trim() : settings.CommunityContext.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }

        var community = settings.Community.Trim();
        var communityContext = string.IsNullOrWhiteSpace(settings.CommunityContext)
            ? community
            : settings.CommunityContext.Trim();
        var request = new CompetitionProfileCollectionRequest(
            profile,
            community,
            communityContext,
            settings.Matchdays,
            settings.FullSeason,
            settings.RecentHistoryDateMap,
            settings.DryRun,
            settings.Verbose,
            ContextSourceOnly: settings.ContextSourceOnly);
        return await CompetitionProfileCollectionRunner.ExecuteAsync(
            _console,
            _collectorExecutor,
            request,
            cancellationToken);
    }
}
