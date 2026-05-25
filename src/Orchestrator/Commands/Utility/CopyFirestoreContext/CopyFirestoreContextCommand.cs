using System.Text;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.CopyFirestoreContext;

public sealed class CopyFirestoreContextCommand : AsyncCommand<CopyFirestoreContextSettings>
{
    private const string LineupPrefix = "lineup-";
    private const string LineupsKpiDocumentName = "lineups";
    private const string SquadStatusColumn = "Squad_Status";

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<CopyFirestoreContextCommand> _logger;

    public CopyFirestoreContextCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<CopyFirestoreContextCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CopyFirestoreContextSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefixes = SplitCsvOption(settings.ContextPrefix);
            var kpiDocumentNames = SplitCsvOption(settings.KpiDocument);
            var competition = CompetitionResolver.ResolveCompetition(
                settings.Competition,
                communityContext: settings.TargetCommunityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);

            _console.MarkupLine($"[green]Copy Firestore context command initialized[/]");
            _console.MarkupLine($"[blue]Source community context:[/] [yellow]{settings.SourceCommunityContext}[/]");
            _console.MarkupLine($"[blue]Target community context:[/] [yellow]{settings.TargetCommunityContext}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no Firestore documents will be written[/]");
            }

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(repositoryCompetition);

            var sourceContextDocuments = await LoadSourceContextDocumentsAsync(
                contextRepository,
                settings.SourceCommunityContext,
                prefixes,
                cancellationToken);
            var sourceKpiDocuments = await LoadSourceKpiDocumentsAsync(
                kpiRepository,
                settings.SourceCommunityContext,
                kpiDocumentNames,
                cancellationToken);

            if (sourceContextDocuments.MissingMessages.Count > 0 || sourceKpiDocuments.MissingMessages.Count > 0)
            {
                foreach (var message in sourceContextDocuments.MissingMessages.Concat(sourceKpiDocuments.MissingMessages))
                {
                    _console.MarkupLine($"[red]{message}[/]");
                }

                return 1;
            }

            var lineupStatus = ValidateLineupStatusIfNeeded(prefixes, kpiDocumentNames, sourceContextDocuments.Documents, sourceKpiDocuments.Documents);
            if (lineupStatus.ValidationError is not null)
            {
                _console.MarkupLine($"[red]{lineupStatus.ValidationError}[/]");
                return 1;
            }

            if (settings.Verbose)
            {
                foreach (var document in sourceContextDocuments.Documents)
                {
                    _console.MarkupLine($"[dim]  Context: {document.DocumentName} (version {document.Version})[/]");
                }

                foreach (var document in sourceKpiDocuments.Documents)
                {
                    _console.MarkupLine($"[dim]  KPI: {document.DocumentName} (version {document.Version})[/]");
                }
            }

            if (settings.DryRun)
            {
                _console.MarkupLine($"[magenta]Would copy {sourceContextDocuments.Documents.Count} context document(s) and {sourceKpiDocuments.Documents.Count} KPI document(s)[/]");
                PrintLineupStatus(lineupStatus.Status);
                return 0;
            }

            var savedContextCount = 0;
            var unchangedContextCount = 0;
            foreach (var document in sourceContextDocuments.Documents)
            {
                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    document.DocumentName,
                    document.Content,
                    settings.TargetCommunityContext,
                    cancellationToken);

                if (savedVersion.HasValue)
                {
                    savedContextCount++;
                }
                else
                {
                    unchangedContextCount++;
                }
            }

            var savedKpiCount = 0;
            foreach (var document in sourceKpiDocuments.Documents)
            {
                await kpiRepository.SaveKpiDocumentAsync(
                    document.DocumentName,
                    document.Content,
                    document.Description,
                    settings.TargetCommunityContext,
                    cancellationToken);
                savedKpiCount++;
            }

            _console.MarkupLine($"[green]Copied {savedContextCount} context document(s) and {savedKpiCount} KPI document(s)[/]");
            if (unchangedContextCount > 0)
            {
                _console.MarkupLine($"[dim]Unchanged context document(s): {unchangedContextCount}[/]");
            }

            PrintLineupStatus(lineupStatus.Status);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in copy-firestore-context command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task<ContextLoadResult> LoadSourceContextDocumentsAsync(
        IContextRepository contextRepository,
        string sourceCommunityContext,
        IReadOnlyList<string> prefixes,
        CancellationToken cancellationToken)
    {
        if (prefixes.Count == 0)
        {
            return new ContextLoadResult([], []);
        }

        var documentNames = await contextRepository.GetContextDocumentNamesAsync(sourceCommunityContext, cancellationToken);
        var selectedDocumentNames = documentNames
            .Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingMessages = new List<string>();
        if (selectedDocumentNames.Count == 0)
        {
            missingMessages.Add($"No source context documents found for prefix(es): {string.Join(", ", prefixes)}");
            return new ContextLoadResult([], missingMessages);
        }

        var documents = new List<ContextDocument>();
        foreach (var documentName in selectedDocumentNames)
        {
            var document = await contextRepository.GetLatestContextDocumentAsync(
                documentName,
                sourceCommunityContext,
                cancellationToken);

            if (document is null)
            {
                missingMessages.Add($"Missing source context document: {documentName}");
            }
            else
            {
                documents.Add(document);
            }
        }

        return new ContextLoadResult(documents, missingMessages);
    }

    private async Task<KpiLoadResult> LoadSourceKpiDocumentsAsync(
        IKpiRepository kpiRepository,
        string sourceCommunityContext,
        IReadOnlyList<string> kpiDocumentNames,
        CancellationToken cancellationToken)
    {
        var documents = new List<KpiDocument>();
        var missingMessages = new List<string>();

        foreach (var documentName in kpiDocumentNames)
        {
            var document = await kpiRepository.GetKpiDocumentAsync(
                documentName,
                sourceCommunityContext,
                cancellationToken);

            if (document is null)
            {
                missingMessages.Add($"Missing source KPI document: {documentName}");
            }
            else
            {
                documents.Add(document);
            }
        }

        return new KpiLoadResult(documents, missingMessages);
    }

    private static LineupStatusValidationResult ValidateLineupStatusIfNeeded(
        IReadOnlyList<string> prefixes,
        IReadOnlyList<string> kpiDocumentNames,
        IReadOnlyList<ContextDocument> contextDocuments,
        IReadOnlyList<KpiDocument> kpiDocuments)
    {
        var shouldValidate = prefixes.Any(prefix => string.Equals(prefix, LineupPrefix, StringComparison.OrdinalIgnoreCase))
                             || kpiDocumentNames.Any(name => string.Equals(name, LineupsKpiDocumentName, StringComparison.OrdinalIgnoreCase));
        if (!shouldValidate)
        {
            return new LineupStatusValidationResult(null, null);
        }

        var statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in contextDocuments)
        {
            var result = ExtractSquadStatuses(document.Content);
            if (result.Error is not null)
            {
                return new LineupStatusValidationResult(null, $"{document.DocumentName}: {result.Error}");
            }

            foreach (var status in result.Statuses)
            {
                statuses.Add(status);
            }
        }

        foreach (var document in kpiDocuments.Where(document =>
                     string.Equals(document.DocumentName, LineupsKpiDocumentName, StringComparison.OrdinalIgnoreCase)))
        {
            var result = ExtractSquadStatuses(document.Content);
            if (result.Error is not null)
            {
                return new LineupStatusValidationResult(null, $"{document.DocumentName}: {result.Error}");
            }

            foreach (var status in result.Statuses)
            {
                statuses.Add(status);
            }
        }

        if (statuses.Count == 0)
        {
            return new LineupStatusValidationResult(null, "No Squad_Status values found in copied lineup documents");
        }

        if (statuses.Count > 1)
        {
            return new LineupStatusValidationResult(null, $"Mixed Squad_Status values found in copied lineup documents: {string.Join(", ", statuses.OrderBy(status => status, StringComparer.OrdinalIgnoreCase))}");
        }

        var statusValue = statuses.Single().ToLowerInvariant();
        if (statusValue is not "provisional" and not "official")
        {
            return new LineupStatusValidationResult(null, $"Unsupported Squad_Status value '{statusValue}' in copied lineup documents");
        }

        return new LineupStatusValidationResult(statusValue, null);
    }

    private static SquadStatusExtractionResult ExtractSquadStatuses(string content)
    {
        using var reader = new StringReader(content);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new SquadStatusExtractionResult([], "CSV content is empty");
        }

        var header = ParseCsvLine(headerLine);
        var statusIndex = header.FindIndex(column => string.Equals(column, SquadStatusColumn, StringComparison.OrdinalIgnoreCase));
        if (statusIndex < 0)
        {
            return new SquadStatusExtractionResult([], $"CSV header is missing {SquadStatusColumnForMessage()}");
        }

        var statuses = new List<string>();
        string? line;
        var lineNumber = 1;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (statusIndex >= fields.Count || string.IsNullOrWhiteSpace(fields[statusIndex]))
            {
                return new SquadStatusExtractionResult([], $"Missing {SquadStatusColumnForMessage()} value at line {lineNumber}");
            }

            statuses.Add(fields[statusIndex].Trim());
        }

        return new SquadStatusExtractionResult(statuses, null);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (current == ',' && !inQuotes)
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(current);
            }
        }

        fields.Add(builder.ToString());
        return fields;
    }

    private static IReadOnlyList<string> SplitCsvOption(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private void PrintLineupStatus(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            _console.MarkupLine($"[green]Lineup source status:[/] {status}");
        }
    }

    private static string SquadStatusColumnForMessage()
    {
        return SquadStatusColumn;
    }

    private sealed record ContextLoadResult(
        IReadOnlyList<ContextDocument> Documents,
        IReadOnlyList<string> MissingMessages);

    private sealed record KpiLoadResult(
        IReadOnlyList<KpiDocument> Documents,
        IReadOnlyList<string> MissingMessages);

    private sealed record LineupStatusValidationResult(
        string? Status,
        string? ValidationError);

    private sealed record SquadStatusExtractionResult(
        IReadOnlyList<string> Statuses,
        string? Error);
}
