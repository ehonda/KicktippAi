using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
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
    private static readonly IReadOnlyList<string> LineupColumns =
    [
        "Team",
        "Data_Collected_At",
        "Role",
        "Name",
        "Age",
        "Position",
        "Market_Value_EUR"
    ];

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IWm26LineupSource _lineupSource;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CollectContextLineupsCommand> _logger;

    public CollectContextLineupsCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IWm26LineupSource lineupSource,
        TimeProvider timeProvider,
        ILogger<CollectContextLineupsCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _lineupSource = lineupSource;
        _timeProvider = timeProvider;
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

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var collectionDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            var freshenedSource = await ApplyFreshnessDatesAsync(
                source,
                contextRepository,
                communityContext,
                collectionDate,
                cancellationToken);

            if (settings.DryRun)
            {
                foreach (var document in freshenedSource.ContextDocuments)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save context document:[/] {Markup.Escape(document.DocumentName)}");
                }

                _console.MarkupLine($"[magenta]  Dry run - would save KPI document:[/] {LineupsDocumentName}");
                _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {freshenedSource.ContextDocuments.Count} context documents and 1 KPI document[/]");
                return 0;
            }

            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(repositoryCompetition);

            var savedContextCount = 0;
            var skippedContextCount = 0;
            foreach (var document in freshenedSource.ContextDocuments)
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
                freshenedSource.KpiContent,
                LineupsDescription,
                communityContext,
                cancellationToken);
            var kpiChanged = existingKpiDocument is null
                             || !string.Equals(existingKpiDocument.Content, freshenedSource.KpiContent, StringComparison.Ordinal);

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

    private static async Task<FreshenedLineupCollection> ApplyFreshnessDatesAsync(
        Wm26LineupCollection source,
        IContextRepository contextRepository,
        string communityContext,
        DateOnly collectionDate,
        CancellationToken cancellationToken)
    {
        var documents = new List<Wm26LineupDocument>();
        var aggregateRows = new List<LineupCsvRow>();
        var collectionDateText = collectionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        foreach (var document in source.ContextDocuments)
        {
            var currentRows = ReadLineupRows(document.DocumentName, document.Content, "Generated lineup context document");
            var existingDocument = await contextRepository.GetLatestContextDocumentAsync(
                document.DocumentName,
                communityContext,
                cancellationToken);
            var adjustedRows = existingDocument is null
                ? currentRows
                : ApplyExistingFreshnessDates(
                    currentRows,
                    ReadLineupRows(document.DocumentName, existingDocument.Content, "Existing lineup context document"),
                    collectionDateText);

            aggregateRows.AddRange(adjustedRows);
            documents.Add(document with { Content = RenderLineupRows(adjustedRows) });
        }

        return new FreshenedLineupCollection(documents, RenderLineupRows(aggregateRows));
    }

    private static IReadOnlyList<LineupCsvRow> ApplyExistingFreshnessDates(
        IReadOnlyList<LineupCsvRow> currentRows,
        IReadOnlyList<LineupCsvRow> existingRows,
        string collectionDate)
    {
        var existingEntries = existingRows
            .Select((row, index) => new ExistingLineupRow(row, index))
            .ToList();
        var existingEntriesByKey = existingEntries
            .GroupBy(entry => GetRowKey(entry.Row))
            .ToDictionary(
                group => group.Key,
                group => new Queue<ExistingLineupRow>(group));
        var matchedExistingIndexes = new HashSet<int>();
        var adjustedRows = new List<LineupCsvRow>(currentRows.Count);

        for (var index = 0; index < currentRows.Count; index++)
        {
            var currentRow = currentRows[index];
            var existingEntry = FindExistingEntry(currentRow, index);
            if (existingEntry is null)
            {
                adjustedRows.Add(currentRow);
                continue;
            }

            var existingRow = existingEntry.Value.Row;
            adjustedRows.Add(HasNonDateChange(currentRow, existingRow)
                ? currentRow with { DataCollectedAt = collectionDate }
                : currentRow with { DataCollectedAt = existingRow.DataCollectedAt });
        }

        return adjustedRows;

        static LineupRowKey GetRowKey(LineupCsvRow row)
        {
            return new LineupRowKey(row.Team, row.Role, row.Name);
        }

        ExistingLineupRow? FindExistingEntry(LineupCsvRow currentRow, int index)
        {
            if (existingEntriesByKey.TryGetValue(GetRowKey(currentRow), out var candidates))
            {
                while (candidates.Count > 0)
                {
                    var candidate = candidates.Dequeue();
                    if (matchedExistingIndexes.Add(candidate.Index))
                    {
                        return candidate;
                    }
                }
            }

            if (currentRows.Count == existingRows.Count
                && index < existingRows.Count
                && matchedExistingIndexes.Add(index))
            {
                var candidateRow = existingRows[index];
                if (string.Equals(currentRow.Team, candidateRow.Team, StringComparison.Ordinal)
                    && string.Equals(currentRow.Role, candidateRow.Role, StringComparison.Ordinal))
                {
                    return new ExistingLineupRow(candidateRow, index);
                }

                matchedExistingIndexes.Remove(index);
            }

            return null;
        }
    }

    private static bool HasNonDateChange(LineupCsvRow currentRow, LineupCsvRow existingRow)
    {
        return !string.Equals(currentRow.Team, existingRow.Team, StringComparison.Ordinal)
               || !string.Equals(currentRow.Role, existingRow.Role, StringComparison.Ordinal)
               || !string.Equals(currentRow.Name, existingRow.Name, StringComparison.Ordinal)
               || !string.Equals(currentRow.Age, existingRow.Age, StringComparison.Ordinal)
               || !string.Equals(currentRow.Position, existingRow.Position, StringComparison.Ordinal)
               || !string.Equals(currentRow.MarketValueEur, existingRow.MarketValueEur, StringComparison.Ordinal);
    }

    private static IReadOnlyList<LineupCsvRow> ReadLineupRows(
        string documentName,
        string content,
        string label)
    {
        try
        {
            using var reader = new StringReader(content);
            using var csv = new CsvReader(
                reader,
                new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null,
                    MissingFieldFound = null,
                    TrimOptions = TrimOptions.Trim
                });

            if (!csv.Read())
            {
                throw new InvalidOperationException("missing header row");
            }

            csv.ReadHeader();
            ValidateLineupColumns(csv.HeaderRecord ?? [], label, documentName);

            var rows = new List<LineupCsvRow>();
            while (csv.Read())
            {
                var row = new LineupCsvRow(
                    GetTrimmedField(csv, "Team"),
                    GetTrimmedField(csv, "Data_Collected_At"),
                    GetTrimmedField(csv, "Role"),
                    GetTrimmedField(csv, "Name"),
                    GetTrimmedField(csv, "Age"),
                    GetTrimmedField(csv, "Position"),
                    GetTrimmedField(csv, "Market_Value_EUR"));
                ValidateLineupRow(row, csv.Context?.Parser?.Row ?? 0, label, documentName);
                rows.Add(row);
            }

            return rows;
        }
        catch (Exception ex) when (ex is CsvHelperException or InvalidOperationException)
        {
            throw new InvalidOperationException($"{label} {documentName} is malformed: {ex.Message}", ex);
        }
    }

    private static string RenderLineupRows(IEnumerable<LineupCsvRow> rows)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(
            writer,
            new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                NewLine = "\r\n"
            });

        foreach (var column in LineupColumns)
        {
            csv.WriteField(column);
        }

        csv.NextRecord();

        foreach (var row in rows)
        {
            csv.WriteField(row.Team);
            csv.WriteField(row.DataCollectedAt);
            csv.WriteField(row.Role);
            csv.WriteField(row.Name);
            csv.WriteField(row.Age);
            csv.WriteField(row.Position);
            csv.WriteField(row.MarketValueEur);
            csv.NextRecord();
        }

        return writer.ToString();
    }

    private static void ValidateLineupColumns(
        IReadOnlyList<string> headers,
        string label,
        string documentName)
    {
        var missing = LineupColumns
            .Where(column => !headers.Contains(column, StringComparer.Ordinal))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"{label} {documentName} is missing required column(s): {string.Join(", ", missing)}");
        }
    }

    private static void ValidateLineupRow(
        LineupCsvRow row,
        int lineNumber,
        string label,
        string documentName)
    {
        foreach (var (column, value) in new[]
                 {
                     ("Team", row.Team),
                     ("Data_Collected_At", row.DataCollectedAt),
                     ("Role", row.Role),
                     ("Name", row.Name)
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{label} {documentName} line {lineNumber}: missing {column}");
            }
        }

        if (!DateOnly.TryParseExact(
                row.DataCollectedAt,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw new InvalidOperationException(
                $"{label} {documentName} line {lineNumber}: Data_Collected_At must use YYYY-MM-DD, got {row.DataCollectedAt}");
        }
    }

    private static string GetTrimmedField(CsvReader csv, string column)
    {
        return (csv.GetField(column) ?? string.Empty).Trim();
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

    private sealed record FreshenedLineupCollection(
        IReadOnlyList<Wm26LineupDocument> ContextDocuments,
        string KpiContent);

    private sealed record LineupCsvRow(
        string Team,
        string DataCollectedAt,
        string Role,
        string Name,
        string Age,
        string Position,
        string MarketValueEur);

    private readonly record struct LineupRowKey(
        string Team,
        string Role,
        string Name);

    private readonly record struct ExistingLineupRow(
        LineupCsvRow Row,
        int Index);
}
