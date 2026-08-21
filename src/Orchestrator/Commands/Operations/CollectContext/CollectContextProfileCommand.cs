using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

public sealed class CollectContextProfileCommand : AsyncCommand<CollectContextProfileSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ICompetitionCollectionProfileResolver _profileResolver;
    private readonly ICompetitionProfileCollectorExecutor _collectorExecutor;

    public CollectContextProfileCommand(
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
        CollectContextProfileSettings settings,
        CancellationToken cancellationToken)
    {
        CompetitionCollectionProfile profile;
        string communityContext;
        try
        {
            communityContext = settings.CommunityContext.Trim();
            var targetCompetition = CompetitionResolver.ResolveTargetCompetition(
                settings.Competition,
                communityContext);
            profile = _profileResolver.ResolveCompetition(targetCompetition);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }

        var request = new CompetitionProfileCollectionRequest(
            profile,
            communityContext,
            communityContext,
            settings.Matchdays,
            settings.RecentHistoryDateMap,
            settings.DryRun,
            settings.Verbose,
            settings.MarkdownSummaryOutput);
        return await CompetitionProfileCollectionRunner.ExecuteAsync(
            _console,
            _collectorExecutor,
            request,
            cancellationToken);
    }
}
