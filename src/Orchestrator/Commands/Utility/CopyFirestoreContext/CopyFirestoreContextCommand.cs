using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.CopyFirestoreContext;

public sealed class CopyFirestoreContextCommand : AsyncCommand<CopyFirestoreContextSettings>
{
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

    private static IReadOnlyList<string> SplitCsvOption(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record ContextLoadResult(
        IReadOnlyList<ContextDocument> Documents,
        IReadOnlyList<string> MissingMessages);

    private sealed record KpiLoadResult(
        IReadOnlyList<KpiDocument> Documents,
        IReadOnlyList<string> MissingMessages);
}
