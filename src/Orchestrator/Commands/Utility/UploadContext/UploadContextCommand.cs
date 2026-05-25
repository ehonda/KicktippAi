using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Utility.UploadContext;

public sealed class UploadContextCommand : AsyncCommand<UploadContextSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<UploadContextCommand> _logger;

    public UploadContextCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<UploadContextCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        UploadContextSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var inputPath = Path.GetFullPath(settings.Input);
            if (!File.Exists(inputPath))
            {
                _console.MarkupLine($"[red]Context document JSON not found:[/] {inputPath}");
                return 1;
            }

            await using var stream = File.OpenRead(inputPath);
            var document = await JsonSerializer.DeserializeAsync<ContextDocumentJson>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (document is null)
            {
                _console.MarkupLine("[red]Failed to parse context document JSON[/]");
                return 1;
            }

            if (!ValidateDocument(document))
            {
                return 1;
            }

            var competition = CompetitionResolver.ResolveCompetition(
                settings.Competition,
                communityContext: document.CommunityContext);
            var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);

            _console.MarkupLine($"[green]Upload context command initialized for document:[/] [yellow]{document.DocumentName}[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{document.CommunityContext}[/]");
            _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
            _console.MarkupLine($"[blue]Reading context document from:[/] {inputPath}");

            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
                _console.MarkupLine($"[dim]Content length: {document.Content.Length} characters[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no Firestore document will be written[/]");
                _console.MarkupLine($"[magenta]Would upload context document:[/] {document.DocumentName}");
                return 0;
            }

            var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
            var existingDocument = await contextRepository.GetLatestContextDocumentAsync(
                document.DocumentName,
                document.CommunityContext,
                cancellationToken);

            if (existingDocument is null)
            {
                _console.MarkupLine($"[blue]No existing context document found for '{document.DocumentName}' - will create version 0[/]");
            }
            else
            {
                _console.MarkupLine($"[blue]Found existing context document '{document.DocumentName}' (version {existingDocument.Version})[/]");
            }

            var savedVersion = await contextRepository.SaveContextDocumentAsync(
                document.DocumentName,
                document.Content,
                document.CommunityContext,
                cancellationToken);

            if (existingDocument is not null && savedVersion is null)
            {
                _console.MarkupLine($"[green]Content unchanged - context document '[/][white]{document.DocumentName}[/][green]' remains at version {existingDocument.Version}[/]");
            }
            else if (existingDocument is not null)
            {
                _console.MarkupLine($"[green]Content changed - created new version {savedVersion} for context document '[/][white]{document.DocumentName}[/][green]'[/]");
            }
            else
            {
                _console.MarkupLine($"[green]Created context document '[/][white]{document.DocumentName}[/][green]' as version {savedVersion}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in upload-context command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private bool ValidateDocument(ContextDocumentJson document)
    {
        if (string.IsNullOrWhiteSpace(document.DocumentName))
        {
            _console.MarkupLine("[red]Context document JSON is missing documentName[/]");
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.CommunityContext))
        {
            _console.MarkupLine("[red]Context document JSON is missing communityContext[/]");
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            _console.MarkupLine("[red]Context document JSON is missing content[/]");
            return false;
        }

        return true;
    }

    private sealed class ContextDocumentJson
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommunityContext { get; set; } = string.Empty;
    }
}
