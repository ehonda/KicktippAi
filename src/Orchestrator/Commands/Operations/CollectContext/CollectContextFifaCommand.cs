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
    public const string DefaultSourceRoot = "data/wm26";

    private const string ContextDocumentsSubdirectory = "context-documents";
    private const string KpiDocumentsSubdirectory = "kpi-documents";
    private const string RankingFilePattern = "fifa-ranking-*.csv";
    private const string FifaRankingsDocumentName = "fifa-rankings";
    private const string FifaRankingsFileName = "fifa-rankings.csv";
    private const string FifaRankingsDescription = "WM26 FIFA rankings for all participants, used as KPI context for bonus predictions.";

    private static readonly string[] ExpectedRankingHeader = ["team", HistoryCsvUtility.DataCollectedAtColumnName, "rank", "ELO"];

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<CollectContextFifaCommand> _logger;

    public CollectContextFifaCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<CollectContextFifaCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
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
            var sourceRoot = ResolveSourceRoot(settings.SourceRoot);
            var contextDirectory = Path.Combine(sourceRoot, ContextDocumentsSubdirectory);
            var kpiDirectory = Path.Combine(sourceRoot, KpiDocumentsSubdirectory);
            var kpiFilePath = Path.Combine(kpiDirectory, FifaRankingsFileName);

            _console.MarkupLine("[green]Collect-context fifa command initialized[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{Markup.Escape(competition)}[/]");
            _console.MarkupLine($"[blue]Using source root:[/] [yellow]{Markup.Escape(sourceRoot)}[/]");

            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }

            var source = await LoadAndValidateSourceAsync(contextDirectory, kpiFilePath, cancellationToken);
            if (source.Errors.Count > 0)
            {
                _console.MarkupLine("[red]FIFA ranking source validation failed:[/]");
                foreach (var error in source.Errors.Take(20))
                {
                    _console.MarkupLine($"[red]  - {Markup.Escape(error)}[/]");
                }

                if (source.Errors.Count > 20)
                {
                    _console.MarkupLine($"[red]  - ...and {source.Errors.Count - 20} more errors[/]");
                }

                return 1;
            }

            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Validated {source.RankingFiles.Count} per-team ranking files[/]");
            }

            if (settings.DryRun)
            {
                foreach (var rankingFile in source.RankingFiles)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save context document:[/] {Markup.Escape(rankingFile.DocumentName)}");
                }

                _console.MarkupLine($"[magenta]  Dry run - would save KPI document:[/] {FifaRankingsDocumentName}");
                _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {source.RankingFiles.Count} context documents and 1 KPI document[/]");
                return 0;
            }

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository(repositoryCompetition);

            var savedContextCount = 0;
            var skippedContextCount = 0;

            foreach (var rankingFile in source.RankingFiles)
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

    private static string ResolveSourceRoot(string sourceRoot)
    {
        var trimmedSourceRoot = string.IsNullOrWhiteSpace(sourceRoot)
            ? DefaultSourceRoot
            : sourceRoot.Trim();

        var fullPath = Path.IsPathRooted(trimmedSourceRoot)
            ? trimmedSourceRoot
            : Path.Combine(SolutionPathUtility.FindSolutionRoot(), trimmedSourceRoot);

        return Path.GetFullPath(fullPath);
    }

    private static async Task<FifaRankingSource> LoadAndValidateSourceAsync(
        string contextDirectory,
        string kpiFilePath,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var rankingFiles = new List<FifaRankingSourceFile>();

        if (!Directory.Exists(contextDirectory))
        {
            errors.Add($"Context documents directory not found: {contextDirectory}");
        }
        else
        {
            var filePaths = Directory
                .GetFiles(contextDirectory, RankingFilePattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (filePaths.Count == 0)
            {
                errors.Add($"No ranking CSVs found in {contextDirectory} matching {RankingFilePattern}");
            }

            foreach (var filePath in filePaths)
            {
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                errors.AddRange(ValidateRankingCsv(filePath, content));
                rankingFiles.Add(new FifaRankingSourceFile(Path.GetFileName(filePath), content));
            }
        }

        var kpiContent = string.Empty;
        if (!File.Exists(kpiFilePath))
        {
            errors.Add($"KPI rankings CSV not found: {kpiFilePath}");
        }
        else
        {
            kpiContent = await File.ReadAllTextAsync(kpiFilePath, cancellationToken);
            errors.AddRange(ValidateRankingCsv(kpiFilePath, kpiContent));
        }

        return new FifaRankingSource(rankingFiles, kpiContent, errors);
    }

    private static IEnumerable<string> ValidateRankingCsv(string filePath, string content)
    {
        var errors = new List<string>();
        using var reader = new StringReader(content);

        var header = reader.ReadLine();
        if (header is null)
        {
            errors.Add($"{filePath}: file is empty");
            return errors;
        }

        var headerColumns = SplitCsvLine(header);
        if (headerColumns.Length > 0)
        {
            headerColumns[0] = headerColumns[0].TrimStart('\uFEFF');
        }

        if (!headerColumns.SequenceEqual(ExpectedRankingHeader, StringComparer.Ordinal))
        {
            errors.Add($"{filePath}: expected header {string.Join(",", ExpectedRankingHeader)}");
            return errors;
        }

        var lineNumber = 1;
        var dataRowCount = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            dataRowCount++;
            var fields = SplitCsvLine(line);
            if (fields.Length < ExpectedRankingHeader.Length)
            {
                errors.Add($"{filePath}: row {lineNumber} has fewer columns than expected");
                continue;
            }

            if (string.IsNullOrWhiteSpace(fields[1]))
            {
                errors.Add($"{filePath}: row {lineNumber} has empty {HistoryCsvUtility.DataCollectedAtColumnName}");
            }
        }

        if (dataRowCount == 0)
        {
            errors.Add($"{filePath}: file has no ranking rows");
        }

        return errors;
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.TrimEnd('\r').Split(',');
    }

    private sealed record FifaRankingSource(
        IReadOnlyList<FifaRankingSourceFile> RankingFiles,
        string KpiContent,
        IReadOnlyList<string> Errors);

    private sealed record FifaRankingSourceFile(string DocumentName, string Content);
}
