using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Wm26RecentHistory;

public sealed class Wm26RecentHistoryApplyDateMapCommand
    : AsyncCommand<Wm26RecentHistoryApplyDateMapSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<Wm26RecentHistoryApplyDateMapCommand> _logger;

    public Wm26RecentHistoryApplyDateMapCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<Wm26RecentHistoryApplyDateMapCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Wm26RecentHistoryApplyDateMapSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(settings.Input))
            {
                _console.MarkupLine($"[red]Date map not found:[/] {settings.Input}");
                return 1;
            }

            var dateMapContent = await File.ReadAllTextAsync(settings.Input, cancellationToken);
            var dateMapEntries = HistoryCsvUtility.ReadDateMapEntries(dateMapContent);
            if (dateMapEntries.Count == 0)
            {
                _console.MarkupLine("[red]Date map has no rows[/]");
                return 1;
            }

            var competition = CompetitionResolver.ResolveCompetition(
                settings.Competition,
                communityContext: settings.CommunityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);
            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);

            _console.MarkupLine($"[green]Applying WM26 recent-history date map for:[/] [yellow]{settings.CommunityContext}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no Firestore documents will be written[/]");
            }

            var documentNames = await contextRepository.GetContextDocumentNamesAsync(
                settings.CommunityContext,
                cancellationToken);
            var historyDocumentNames = documentNames
                .Where(IsRecentHistoryDocument)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            if (historyDocumentNames.Count == 0)
            {
                _console.MarkupLine("[yellow]No recent-history documents found[/]");
                return 1;
            }

            var plannedUpdates = new List<PlannedUpdate>();
            var missingEntries = new List<HistoryDateMapEntry>();

            foreach (var documentName in historyDocumentNames)
            {
                var document = await contextRepository.GetLatestContextDocumentAsync(
                    documentName,
                    settings.CommunityContext,
                    cancellationToken);
                if (document is null)
                {
                    continue;
                }

                var result = HistoryCsvUtility.ApplyDateMap(documentName, document.Content, dateMapEntries);
                if (result.MissingEntries.Count > 0)
                {
                    missingEntries.AddRange(result.MissingEntries);
                }

                plannedUpdates.Add(new PlannedUpdate(documentName, document, result));

                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]  Checked {documentName}: {result.RowCount} row(s)[/]");
                }
            }

            if (missingEntries.Count > 0)
            {
                PrintMissingEntries(missingEntries);
                return 1;
            }

            var savedCount = 0;
            var unchangedCount = 0;

            foreach (var update in plannedUpdates)
            {
                if (update.Document.Content == update.Result.Content)
                {
                    unchangedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Skipped {update.DocumentName} (content unchanged)[/]");
                    }

                    continue;
                }

                if (settings.DryRun)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save:[/] {update.DocumentName}");
                    savedCount++;
                    continue;
                }

                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    update.DocumentName,
                    update.Result.Content,
                    settings.CommunityContext,
                    cancellationToken);

                if (savedVersion.HasValue)
                {
                    savedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[green]  Saved {update.DocumentName} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    unchangedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Skipped {update.DocumentName} (repository reported unchanged)[/]");
                    }
                }
            }

            var completionMessage = settings.DryRun
                ? $"[magenta]Date-map dry run completed - {savedCount} document(s) would be saved[/]"
                : $"[green]Date-map apply completed - saved {savedCount} document(s)[/]";
            _console.MarkupLine(completionMessage);
            _console.MarkupLine($"[dim]Unchanged: {unchangedCount} document(s)[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply WM26 recent-history date map");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private void PrintMissingEntries(IReadOnlyList<HistoryDateMapEntry> missingEntries)
    {
        _console.MarkupLine($"[red]Date map is missing exact Played_At values for {missingEntries.Count} row(s)[/]");

        foreach (var entry in missingEntries.Take(20))
        {
            _console.MarkupLine(
                "[red]  Missing:[/] " +
                $"{entry.DocumentName} | {entry.Competition} | {entry.HomeTeam} vs {entry.AwayTeam} | " +
                $"{entry.Score} | {entry.Annotation}");
        }

        if (missingEntries.Count > 20)
        {
            _console.MarkupLine($"[dim]  ... and {missingEntries.Count - 20} more[/]");
        }
    }

    private static bool IsRecentHistoryDocument(string documentName)
    {
        return documentName.StartsWith("recent-history-", StringComparison.OrdinalIgnoreCase)
               && documentName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PlannedUpdate(
        string DocumentName,
        ContextDocument Document,
        HistoryDateMapApplyResult Result);
}
