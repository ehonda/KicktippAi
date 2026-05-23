using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Wm26RecentHistory;

public sealed class Wm26RecentHistoryExportDateMapCommand
    : AsyncCommand<Wm26RecentHistoryExportDateMapSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<Wm26RecentHistoryExportDateMapCommand> _logger;

    public Wm26RecentHistoryExportDateMapCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<Wm26RecentHistoryExportDateMapCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Wm26RecentHistoryExportDateMapSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var competition = CompetitionResolver.ResolveCompetition(
                settings.Competition,
                communityContext: settings.CommunityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);
            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);

            _console.MarkupLine($"[green]Exporting WM26 recent-history date map for:[/] [yellow]{settings.CommunityContext}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");

            var existingEntries = await ReadExistingEntries(settings.Output, cancellationToken);
            var existingByKey = existingEntries
                .GroupBy(CreateDateMapKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new Queue<HistoryDateMapEntry>(group),
                    StringComparer.OrdinalIgnoreCase);
            var exportedEntries = new List<HistoryDateMapEntry>();
            var preservedCount = 0;

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

                var documentEntries = HistoryCsvUtility.ExtractDateMapEntries(documentName, document.Content);
                foreach (var entry in documentEntries)
                {
                    if (existingByKey.TryGetValue(CreateDateMapKey(entry), out var existingEntriesForRow) &&
                        existingEntriesForRow.TryDequeue(out var existing))
                    {
                        exportedEntries.Add(entry with
                        {
                            PlayedAt = existing.PlayedAt,
                            SourceName = existing.SourceName,
                            SourceUrl = existing.SourceUrl,
                            VerifiedAt = existing.VerifiedAt,
                            Notes = existing.Notes
                        });

                        if (!string.IsNullOrWhiteSpace(existing.PlayedAt))
                        {
                            preservedCount++;
                        }
                    }
                    else
                    {
                        exportedEntries.Add(entry);
                    }
                }

                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]  Exported {documentEntries.Count} row(s) from {documentName}[/]");
                }
            }

            var outputDirectory = Path.GetDirectoryName(settings.Output);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(
                settings.Output,
                HistoryCsvUtility.WriteDateMapEntries(exportedEntries),
                cancellationToken);

            _console.MarkupLine($"[green]Exported {exportedEntries.Count} date-map row(s) to {settings.Output}[/]");
            _console.MarkupLine($"[dim]Preserved {preservedCount} existing played date(s)[/]");

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export WM26 recent-history date map");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static async Task<IReadOnlyList<HistoryDateMapEntry>> ReadExistingEntries(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<HistoryDateMapEntry>();
        }

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return HistoryCsvUtility.ReadDateMapEntries(content);
    }

    private static bool IsRecentHistoryDocument(string documentName)
    {
        return documentName.StartsWith("recent-history-", StringComparison.OrdinalIgnoreCase)
               && documentName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateDateMapKey(HistoryDateMapEntry entry)
    {
        return string.Join('|',
            entry.DocumentName.Trim(),
            entry.Competition.Trim(),
            entry.HomeTeam.Trim(),
            entry.AwayTeam.Trim(),
            entry.Score.Trim(),
            entry.Annotation.Trim());
    }
}
