using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;

namespace Orchestrator.Commands.Utility.UploadKpi;

public class UploadKpiCommand : AsyncCommand<UploadKpiSettings>
{
    private readonly IAnsiConsole _console;

    public UploadKpiCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, UploadKpiSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<UploadKpiCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            _console.MarkupLine($"[green]Upload KPI command initialized for document:[/] [yellow]{settings.DocumentName}[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{settings.CommunityContext}[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            // Check if the JSON file exists in the community-context specific subfolder
            var jsonFilePath = Path.Combine("kpi-documents", "output", settings.CommunityContext, $"{settings.DocumentName}.json");
            if (!File.Exists(jsonFilePath))
            {
                _console.MarkupLine($"[red]KPI document file not found:[/] {jsonFilePath}");
                _console.MarkupLine($"[dim]Run the PowerShell script with firebase mode to create the document first.[/]");
                return 1;
            }
            
            _console.MarkupLine($"[blue]Reading KPI document from:[/] {jsonFilePath}");
            
            // Read and parse the JSON file
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var kpiDocument = JsonSerializer.Deserialize<KpiDocumentJson>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (kpiDocument == null)
            {
                _console.MarkupLine("[red]Failed to parse KPI document JSON[/]");
                return 1;
            }
            
            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Document Name: {kpiDocument.DocumentName}[/]");
                _console.MarkupLine($"[dim]Community Context: {kpiDocument.CommunityContext}[/]");
                _console.MarkupLine($"[dim]Content length: {kpiDocument.Content.Length} characters[/]");
            }
            
            // Get Firebase KPI repository
            var kpiRepository = serviceProvider.GetRequiredService<IKpiRepository>();
            
            // Check if document already exists for this community context
            var existingDocument = await kpiRepository.GetKpiDocumentAsync(kpiDocument.DocumentName, kpiDocument.CommunityContext);
            
            if (existingDocument != null)
            {
                _console.MarkupLine($"[blue]Found existing KPI document '{kpiDocument.DocumentName}' (version {existingDocument.Version})[/]");
                _console.MarkupLine($"[blue]Checking for content changes...[/]");
                
                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]Current content length: {existingDocument.Content.Length} characters[/]");
                    _console.MarkupLine($"[dim]New content length: {kpiDocument.Content.Length} characters[/]");
                }
            }
            else
            {
                _console.MarkupLine($"[blue]No existing KPI document found for '{kpiDocument.DocumentName}' - will create version 0[/]");
            }
            
            // Upload the document (versioning is handled automatically by the repository)
            _console.MarkupLine($"[blue]Processing KPI document...[/]");
            
            var savedVersion = await kpiRepository.SaveKpiDocumentAsync(
                kpiDocument.DocumentName,
                kpiDocument.Content,
                kpiDocument.Description,
                kpiDocument.CommunityContext);
                
            if (existingDocument != null && savedVersion == existingDocument.Version)
            {
                _console.MarkupLine($"[green]✓ Content unchanged - KPI document '[/][white]{kpiDocument.DocumentName}[/][green]' remains at version {savedVersion}[/]");
            }
            else if (existingDocument != null)
            {
                _console.MarkupLine($"[green]✓ Content changed - Created new version {savedVersion} for KPI document '[/][white]{kpiDocument.DocumentName}[/][green]'[/]");
            }
            else
            {
                _console.MarkupLine($"[green]✓ Successfully created KPI document '[/][white]{kpiDocument.DocumentName}[/][green]' as version {savedVersion}[/]");
            }
            
            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Document saved to unified kpi-documents collection with community context: {kpiDocument.CommunityContext}[/]");
                _console.MarkupLine($"[dim]Document version: {savedVersion}[/]");
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in upload-kpi command");
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
    
    private static void ConfigureServices(IServiceCollection services, UploadKpiSettings settings, ILogger logger)
    {
        // Add logging services
        services.AddLogging();
        
        // Configure Firebase (no community parameter needed for unified collection)
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
        
        logger.LogDebug("Services configured for upload-kpi command");
    }
    
    /// <summary>
    /// JSON model for deserializing KPI document files.
    /// </summary>
    private class KpiDocumentJson
    {
        public string DocumentName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CommunityContext { get; set; } = string.Empty;
    }
}
