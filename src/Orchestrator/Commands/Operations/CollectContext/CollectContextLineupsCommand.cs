using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Command for uploading WM26 lineup context and KPI documents.
/// </summary>
public sealed class CollectContextLineupsCommand : AsyncCommand<CollectContextLineupsSettings>
{
    private const string LineupsDocumentName = "lineups";
    private const string LineupsDescription =
        "WM26 lineups for all participants, used for the top scorer team bonus question.";

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IWm26LineupSource _lineupSource;
    private readonly ILogger<CollectContextLineupsCommand> _logger;

    public CollectContextLineupsCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IWm26LineupSource lineupSource,
        ILogger<CollectContextLineupsCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _lineupSource = lineupSource;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextLineupsSettings settings,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(
        CollectContextLineupsSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.CommunityContext))
            {
                _console.MarkupLine("[red]Error: Community context is required[/]");
                return 1;
            }

            var communityContext = settings.CommunityContext.Trim();
            var competition = CompetitionResolver.ResolveCompetition(settings.Competition, communityContext, communityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);

            _console.MarkupLine("[green]Collect-context lineups command initialized[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
            _console.MarkupLine($"[blue]Using lineup seed:[/] [yellow]{Markup.Escape(settings.Seed)}[/]");
            _console.MarkupLine($"[blue]Using team manifest:[/] [yellow]{Markup.Escape(settings.Teams)}[/]");

            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }

            var source = await _lineupSource.CollectAsync(
                new Wm26LineupSourceRequest(settings.Seed, settings.Teams, settings.DuckDbPath),
                cancellationToken);

            _console.MarkupLine($"[blue]Resolved lineup seed:[/] [yellow]{Markup.Escape(source.SeedPath)}[/]");
            _console.MarkupLine($"[blue]Resolved team manifest:[/] [yellow]{Markup.Escape(source.TeamsPath)}[/]");
            _console.MarkupLine($"[blue]Using Transfermarkt DuckDB:[/] [yellow]{Markup.Escape(source.DuckDbPath)}[/]");
            _console.MarkupLine($"[blue]Seed rows:[/] [yellow]{source.SeedRowCount}[/]");
            _console.MarkupLine($"[blue]Generated lineup context documents:[/] [yellow]{source.ContextDocuments.Count}[/]");
            PrintHeaderOnlyReport(source);
            PrintMissingSourceDataReport(source);

            if (settings.DryRun)
            {
                foreach (var document in source.ContextDocuments)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save context document:[/] {Markup.Escape(document.DocumentName)}");
                }

                _console.MarkupLine($"[magenta]  Dry run - would save KPI document:[/] {LineupsDocumentName}");
                _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {source.ContextDocuments.Count} context documents and 1 KPI document[/]");
                return 0;
            }

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(repositoryCompetition);

            var savedContextCount = 0;
            var skippedContextCount = 0;
            foreach (var document in source.ContextDocuments)
            {
                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    document.DocumentName,
                    document.Content,
                    communityContext,
                    cancellationToken);

                if (savedVersion.HasValue)
                {
                    savedContextCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[green]  ✓ Saved {Markup.Escape(document.DocumentName)} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    skippedContextCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  - Skipped {Markup.Escape(document.DocumentName)} (content unchanged)[/]");
                    }
                }
            }

            var existingKpiDocument = await kpiRepository.GetKpiDocumentAsync(
                LineupsDocumentName,
                communityContext,
                cancellationToken);
            var savedKpiVersion = await kpiRepository.SaveKpiDocumentAsync(
                LineupsDocumentName,
                source.KpiContent,
                LineupsDescription,
                communityContext,
                cancellationToken);
            var kpiChanged = existingKpiDocument is null
                             || !string.Equals(existingKpiDocument.Content, source.KpiContent, StringComparison.Ordinal);

            _console.MarkupLine("[green]✓ WM26 lineup context collection completed![/]");
            _console.MarkupLine($"[green]  Saved: {savedContextCount} context documents[/]");
            _console.MarkupLine($"[dim]  Skipped: {skippedContextCount} context documents (unchanged)[/]");
            _console.MarkupLine(kpiChanged
                ? $"[green]  KPI document {LineupsDocumentName} saved as version {savedKpiVersion}[/]"
                : $"[dim]  KPI document {LineupsDocumentName} unchanged at version {savedKpiVersion}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collect-context lineups command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private void PrintHeaderOnlyReport(Wm26LineupCollection source)
    {
        if (source.HeaderOnlyTeams.Count == 0)
        {
            _console.MarkupLine("[green]Header-only lineup context payloads: none[/]");
            return;
        }

        _console.MarkupLine($"[yellow]Header-only lineup context payloads:[/] {source.HeaderOnlyTeams.Count}");
        foreach (var team in source.HeaderOnlyTeams)
        {
            _console.MarkupLine($"[yellow]  - {Markup.Escape(team.Name)} ({Markup.Escape(team.Slug)})[/]");
        }
    }

    private void PrintMissingSourceDataReport(Wm26LineupCollection source)
    {
        if (source.MissingSourceData.Count == 0)
        {
            _console.MarkupLine("[green]Missing lineup source data: none[/]");
            return;
        }

        _console.MarkupLine("[yellow]Missing lineup source data detected:[/]");
        foreach (var group in source.MissingSourceData.GroupBy(item => (item.TeamSlug, item.TeamName)))
        {
            var players = string.Join(
                ", ",
                group.Select(item => $"{item.PlayerName} ({string.Join(", ", item.Fields)})"));
            var plural = group.Count() == 1 ? "player" : "players";
            _console.MarkupLine(
                $"[yellow]  - {Markup.Escape(group.Key.TeamName)} ({Markup.Escape(group.Key.TeamSlug)}): supplemental data missing for {group.Count()} {plural}: {Markup.Escape(players)}[/]");
        }
    }
}
