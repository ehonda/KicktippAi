using System.Globalization;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using NodaTime;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Wm26RecentHistory;

public sealed class Wm26RecentHistoryApplyDateMapCommand
    : AsyncCommand<Wm26RecentHistoryApplyDateMapSettings>
{
    private static readonly DateTimeZone BerlinTimeZone = DateTimeZoneProviders.Tzdb["Europe/Berlin"];

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
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(
        Wm26RecentHistoryApplyDateMapSettings settings,
        CancellationToken cancellationToken = default)
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
            var applyOptions = CreateApplyOptions(settings);
            var predictionRepository = settings.ApplyKnownOnly && applyOptions.PreserveCollectedOnOrAfter.HasValue
                ? _firebaseServiceFactory.CreatePredictionRepository(repositoryCompetition)
                : null;
            var predictionLookupCache = new Dictionary<PredictionLookupKey, Match?>();

            _console.MarkupLine($"[green]Applying WM26 recent-history date map for:[/] [yellow]{settings.CommunityContext}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
            if (settings.ApplyKnownOnly)
            {
                _console.MarkupLine("[blue]Apply-known-only mode enabled - unmapped rows will be preserved[/]");
                if (applyOptions.PreserveCollectedOnOrAfter.HasValue)
                {
                    _console.MarkupLine(
                        $"[blue]Resolving WM tournament date-only rows dated on or after from stored predictions:[/] [yellow]{applyOptions.PreserveCollectedOnOrAfter.Value:yyyy-MM-dd}[/]");
                }
            }

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
                return settings.ApplyKnownOnly ? 0 : 1;
            }

            var plannedUpdates = new List<PlannedUpdate>();
            var missingEntries = new List<HistoryDateMapEntry>();
            var missingPredictionEntries = new List<HistoryDateMapEntry>();

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

                var predictionDateEntries = await BuildPredictionDateEntriesAsync(
                    documentName,
                    document.Content,
                    applyOptions,
                    predictionRepository,
                    settings.CommunityContext,
                    predictionLookupCache,
                    cancellationToken);
                var documentApplyOptions = applyOptions with { PredictionDateEntries = predictionDateEntries };
                var result = HistoryCsvUtility.ApplyDateMap(documentName, document.Content, dateMapEntries, documentApplyOptions);
                if (result.MissingEntries.Count > 0)
                {
                    missingEntries.AddRange(result.MissingEntries);
                }

                if (result.MissingPredictionEntries.Count > 0)
                {
                    missingPredictionEntries.AddRange(result.MissingPredictionEntries);
                }

                plannedUpdates.Add(new PlannedUpdate(documentName, document, result));

                if (settings.Verbose)
                {
                    _console.MarkupLine(
                        $"[dim]  Checked {documentName}: {result.RowCount} row(s), " +
                        $"{result.UpdatedRowCount} updated, {result.PreservedRowCount} preserved, " +
                        $"{result.SkippedRowCount} skipped[/]");
                }
            }

            if (missingEntries.Count > 0)
            {
                PrintMissingEntries(missingEntries);
                return 1;
            }

            if (missingPredictionEntries.Count > 0)
            {
                PrintMissingPredictionEntries(missingPredictionEntries);
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

    private static HistoryDateMapApplyOptions CreateApplyOptions(Wm26RecentHistoryApplyDateMapSettings settings)
    {
        DateOnly? preserveCollectedOnOrAfter = null;
        if (!string.IsNullOrWhiteSpace(settings.PreserveCollectedOnOrAfter))
        {
            preserveCollectedOnOrAfter = DateOnly.ParseExact(
                settings.PreserveCollectedOnOrAfter.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        return new HistoryDateMapApplyOptions(
            settings.ApplyKnownOnly,
            preserveCollectedOnOrAfter);
    }

    private async Task<IReadOnlyList<HistoryDateMapEntry>> BuildPredictionDateEntriesAsync(
        string documentName,
        string content,
        HistoryDateMapApplyOptions applyOptions,
        IPredictionRepository? predictionRepository,
        string communityContext,
        Dictionary<PredictionLookupKey, Match?> predictionLookupCache,
        CancellationToken cancellationToken)
    {
        if (!applyOptions.PreserveCollectedOnOrAfter.HasValue || predictionRepository is null)
        {
            return Array.Empty<HistoryDateMapEntry>();
        }

        var rows = HistoryCsvUtility.ExtractRowsRequiringPredictionPlayedAt(
            documentName,
            content,
            applyOptions.PreserveCollectedOnOrAfter.Value);
        if (rows.Count == 0)
        {
            return Array.Empty<HistoryDateMapEntry>();
        }

        var entries = new List<HistoryDateMapEntry>();
        foreach (var row in rows)
        {
            var lookupKey = new PredictionLookupKey(row.HomeTeam.Trim(), row.AwayTeam.Trim());
            if (!predictionLookupCache.TryGetValue(lookupKey, out var match))
            {
                match = await predictionRepository.GetLatestPredictedMatchByTeamsAsync(
                    row.HomeTeam,
                    row.AwayTeam,
                    communityContext,
                    cancellationToken);
                predictionLookupCache[lookupKey] = match;
            }

            if (match is null)
            {
                continue;
            }

            entries.Add(row with { PlayedAt = FormatPlayedAt(match.StartsAt) });
        }

        return entries;
    }

    private static string FormatPlayedAt(ZonedDateTime startsAt)
    {
        var local = startsAt.WithZone(BerlinTimeZone);
        var dateTimeOffset = new DateTimeOffset(
            local.LocalDateTime.ToDateTimeUnspecified(),
            local.Offset.ToTimeSpan());

        return dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
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

    private void PrintMissingPredictionEntries(IReadOnlyList<HistoryDateMapEntry> missingEntries)
    {
        _console.MarkupLine($"[red]Missing stored predictions for {missingEntries.Count} unmapped WM tournament recent-history row(s)[/]");

        foreach (var entry in missingEntries.Take(20))
        {
            _console.MarkupLine(
                "[red]  Missing prediction:[/] " +
                $"{entry.DocumentName} | {entry.Competition} | {entry.HomeTeam} vs {entry.AwayTeam} | " +
                $"{entry.Score} | {entry.Annotation} | collected {entry.PlayedAt}");
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

    private sealed record PredictionLookupKey(string HomeTeam, string AwayTeam);
}
