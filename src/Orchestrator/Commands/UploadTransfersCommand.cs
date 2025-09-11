using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Text.Json;
using FirebaseAdapter;
using Core;

namespace Orchestrator.Commands;

public class UploadTransfersCommand : AsyncCommand<UploadTransfersSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UploadTransfersSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<UploadTransfersCommand>();

        try
        {
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var provider = services.BuildServiceProvider();

            var docName = $"{settings.TeamAbbreviation.ToLowerInvariant()}-transfers.csv";
            AnsiConsole.MarkupLine($"[green]Upload Transfers command initialized for document:[/] [yellow]{docName}[/]");
            AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{settings.CommunityContext}[/]");
            if (settings.Verbose) AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");

            // JSON file path produced by Create-TransfersDocument.ps1 firebase mode
            var jsonPath = Path.Combine("transfers-documents", "output", settings.CommunityContext, $"{docName}.json");
            if (!File.Exists(jsonPath))
            {
                AnsiConsole.MarkupLine($"[red]Transfers document JSON not found:[/] {jsonPath}");
                AnsiConsole.MarkupLine("[dim]Run Create-TransfersDocument.ps1 in firebase mode first.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[blue]Reading transfers document from:[/] {jsonPath}");
            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var transfersDoc = JsonSerializer.Deserialize<TransfersDocumentJson>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (transfersDoc == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to parse transfers document JSON[/]");
                return 1;
            }

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Document Name: {transfersDoc.DocumentName}[/]");
                AnsiConsole.MarkupLine($"[dim]Community Context: {transfersDoc.CommunityContext}[/]");
                AnsiConsole.MarkupLine($"[dim]Content length: {transfersDoc.Content.Length} characters[/]");
            }

            var contextRepo = provider.GetRequiredService<IContextRepository>();
            var existing = await contextRepo.GetLatestContextDocumentAsync(transfersDoc.DocumentName, transfersDoc.CommunityContext);
            if (existing != null)
            {
                AnsiConsole.MarkupLine($"[blue]Found existing transfers document '{transfersDoc.DocumentName}' (version {existing.Version})[/]");
                if (settings.Verbose)
                {
                    AnsiConsole.MarkupLine("[dim]Checking for changes...[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[blue]No existing transfers document found - will create version 0[/]");
            }

            var savedVersion = await contextRepo.SaveContextDocumentAsync(
                transfersDoc.DocumentName,
                transfersDoc.Content,
                transfersDoc.CommunityContext);

            if (existing != null && savedVersion == null)
            {
                AnsiConsole.MarkupLine($"[green]✓ Content unchanged - transfers document remains at version {existing.Version}[/]");
            }
            else if (existing != null)
            {
                AnsiConsole.MarkupLine($"[green]✓ Content changed - created new version {savedVersion}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]✓ Created transfers document version {savedVersion}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in upload-transfers command");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services, UploadTransfersSettings settings, ILogger logger)
    {
        services.AddLogging();

        services.AddFirebaseDatabase(options =>
        {
            var serviceAccountPath = PathUtility.GetFirebaseJsonPath();
            var projectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new InvalidOperationException("FIREBASE_PROJECT_ID environment variable is required");
            }
            options.ServiceAccountPath = serviceAccountPath;
            options.ProjectId = projectId;
            logger.LogDebug("Firebase configured with project ID: {ProjectId}", projectId);
        });
    }

    private class TransfersDocumentJson
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommunityContext { get; set; } = string.Empty;
    }
}
