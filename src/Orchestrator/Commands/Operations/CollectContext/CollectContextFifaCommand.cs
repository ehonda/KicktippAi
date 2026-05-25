using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Command for uploading WM26 FIFA ranking context and KPI documents.
/// </summary>
public sealed class CollectContextFifaCommand : AsyncCommand<CollectContextFifaSettings>
{
    private const string FifaRankingsDocumentName = "fifa-rankings";
    private const string FifaRankingsDescription = "WM26 FIFA rankings for all participants, used as KPI context for bonus predictions.";

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IFifaRankingSource _fifaRankingSource;
    private readonly ILogger<CollectContextFifaCommand> _logger;

    public CollectContextFifaCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IFifaRankingSource fifaRankingSource,
        ILogger<CollectContextFifaCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _fifaRankingSource = fifaRankingSource;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextFifaSettings settings,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(
        CollectContextFifaSettings settings,
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
            var collectionDate = DateOnly.FromDateTime(DateTime.UtcNow);

            _console.MarkupLine("[green]Collect-context fifa command initialized[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");

            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }

            var source = await _fifaRankingSource.CollectLatestAsync(collectionDate, cancellationToken);
            _console.MarkupLine($"[blue]Using FIFA ranking schedule:[/] [yellow]{Markup.Escape(source.ScheduleId)}[/]");
            _console.MarkupLine($"[blue]FIFA ranking published at:[/] [yellow]{source.PublicationDateUtc:O}[/]");
            _console.MarkupLine($"[blue]Collection date:[/] [yellow]{source.CollectionDate:yyyy-MM-dd}[/]");
            _console.MarkupLine($"[blue]Source ranking rows:[/] [yellow]{source.SourceRowCount}[/]");
            _console.MarkupLine($"[blue]Mapped WM26 teams:[/] [yellow]{source.MappedTeamCount}[/]");

            if (settings.DryRun)
            {
                foreach (var rankingFile in source.ContextDocuments)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save context document:[/] {Markup.Escape(rankingFile.DocumentName)}");
                }

                _console.MarkupLine($"[magenta]  Dry run - would save KPI document:[/] {FifaRankingsDocumentName}");
                _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {source.ContextDocuments.Count} context documents and 1 KPI document[/]");
                return 0;
            }

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(repositoryCompetition);

            var savedContextCount = 0;
            var skippedContextCount = 0;

            foreach (var rankingFile in source.ContextDocuments)
            {
                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    rankingFile.DocumentName,
                    rankingFile.Content,
                    communityContext,
                    cancellationToken);

                if (savedVersion.HasValue)
                {
                    savedContextCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[green]  ✓ Saved {Markup.Escape(rankingFile.DocumentName)} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    skippedContextCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  - Skipped {Markup.Escape(rankingFile.DocumentName)} (content unchanged)[/]");
                    }
                }
            }

            var existingKpiDocument = await kpiRepository.GetKpiDocumentAsync(
                FifaRankingsDocumentName,
                communityContext,
                cancellationToken);
            var savedKpiVersion = await kpiRepository.SaveKpiDocumentAsync(
                FifaRankingsDocumentName,
                source.KpiContent,
                FifaRankingsDescription,
                communityContext,
                cancellationToken);

            var kpiChanged = existingKpiDocument is null || !string.Equals(existingKpiDocument.Content, source.KpiContent, StringComparison.Ordinal);

            _console.MarkupLine("[green]✓ FIFA ranking context collection completed![/]");
            _console.MarkupLine($"[green]  Saved: {savedContextCount} context documents[/]");
            _console.MarkupLine($"[dim]  Skipped: {skippedContextCount} context documents (unchanged)[/]");
            _console.MarkupLine(kpiChanged
                ? $"[green]  KPI document {FifaRankingsDocumentName} saved as version {savedKpiVersion}[/]"
                : $"[dim]  KPI document {FifaRankingsDocumentName} unchanged at version {savedKpiVersion}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collect-context fifa command");
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}
