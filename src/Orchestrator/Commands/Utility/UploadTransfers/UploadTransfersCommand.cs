using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Utility.UploadTransfers;

public class UploadTransfersCommand : AsyncCommand<UploadTransfersSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<UploadTransfersCommand> _logger;

    public UploadTransfersCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<UploadTransfersCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, UploadTransfersSettings settings)
    {

        try
        {
            var docName = $"{settings.TeamAbbreviation.ToLowerInvariant()}-transfers.csv";
            _console.MarkupLine($"[green]Upload Transfers command initialized for document:[/] [yellow]{docName}[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{settings.CommunityContext}[/]");
            if (settings.Verbose) _console.MarkupLine("[dim]Verbose mode enabled[/]");

            // JSON file path produced by Create-TransfersDocument.ps1 firebase mode
            var jsonPath = Path.Combine("transfers-documents", "output", settings.CommunityContext, $"{docName}.json");
            if (!File.Exists(jsonPath))
            {
                _console.MarkupLine($"[red]Transfers document JSON not found:[/] {jsonPath}");
                _console.MarkupLine("[dim]Run Create-TransfersDocument.ps1 in firebase mode first.[/]");
                return 1;
            }

            _console.MarkupLine($"[blue]Reading transfers document from:[/] {jsonPath}");
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var transfersDoc = JsonSerializer.Deserialize<TransfersDocumentJson>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transfersDoc == null)
            {
                _console.MarkupLine("[red]Failed to parse transfers document JSON[/]");
                return 1;
            }

            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Document Name: {transfersDoc.DocumentName}[/]");
                _console.MarkupLine($"[dim]Community Context: {transfersDoc.CommunityContext}[/]");
                _console.MarkupLine($"[dim]Content length: {transfersDoc.Content.Length} characters[/]");
            }

            // Create Firebase services using factory (factory handles env var loading)
            var contextRepo = _firebaseServiceFactory.CreateContextRepository();
            var existing = await contextRepo.GetLatestContextDocumentAsync(transfersDoc.DocumentName, transfersDoc.CommunityContext);
            if (existing != null)
            {
                _console.MarkupLine($"[blue]Found existing transfers document '{transfersDoc.DocumentName}' (version {existing.Version})[/]");
                if (settings.Verbose)
                {
                    _console.MarkupLine("[dim]Checking for changes...[/]");
                }
            }
            else
            {
                _console.MarkupLine($"[blue]No existing transfers document found - will create version 0[/]");
            }

            var savedVersion = await contextRepo.SaveContextDocumentAsync(
                transfersDoc.DocumentName,
                transfersDoc.Content,
                transfersDoc.CommunityContext);

            if (existing != null && savedVersion == null)
            {
                _console.MarkupLine($"[green]✓ Content unchanged - transfers document remains at version {existing.Version}[/]");
            }
            else if (existing != null)
            {
                _console.MarkupLine($"[green]✓ Content changed - created new version {savedVersion}[/]");
            }
            else
            {
                _console.MarkupLine($"[green]✓ Created transfers document version {savedVersion}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in upload-transfers command");
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private class TransfersDocumentJson
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommunityContext { get; set; } = string.Empty;
    }
}
