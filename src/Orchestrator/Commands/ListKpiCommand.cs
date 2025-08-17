using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using FirebaseAdapter;
using Core;

namespace Orchestrator.Commands;

public class ListKpiCommand : AsyncCommand<ListKpiSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListKpiSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<ListKpiCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]List KPI command initialized for community context:[/] [yellow]{settings.CommunityContext}[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
                if (!string.IsNullOrWhiteSpace(settings.Tags))
                {
                    AnsiConsole.MarkupLine($"[dim]Filtering by tags: {settings.Tags}[/]");
                }
            }
            
            // Get Firebase KPI repository
            var kpiRepository = serviceProvider.GetRequiredService<IKpiRepository>();
            
            var table = new Table();
            table.AddColumn("Document ID");
            table.AddColumn("Name");
            table.AddColumn("Type");
            table.AddColumn("Version");
            table.AddColumn("Content Preview");
            
            if (settings.Verbose)
            {
                table.AddColumn("Tags");
            }

            int documentCount = 0;
            
            // Get all latest documents directly from repository for better version support
            var kpiDocuments = await kpiRepository.GetAllKpiDocumentsAsync(settings.CommunityContext);
            
            foreach (var document in kpiDocuments)
            {
                // Filter by tags if specified
                if (!string.IsNullOrWhiteSpace(settings.Tags))
                {
                    var tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToLowerInvariant())
                        .ToArray();
                    
                    var documentTagsLower = document.Tags.Select(t => t.ToLowerInvariant()).ToArray();
                    if (!tags.Any(tag => documentTagsLower.Contains(tag)))
                    {
                        continue; // Skip this document if it doesn't match the tag filter
                    }
                }
                
                var preview = document.Content.Length > 100 
                    ? document.Content.Substring(0, 100) + "..." 
                    : document.Content;
                
                if (settings.Verbose)
                {
                    table.AddRow(
                        $"[yellow]{document.DocumentId}[/]",
                        document.Name,
                        document.DocumentType,
                        $"[blue]v{document.Version}[/]",
                        $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]",
                        string.Join(", ", document.Tags));
                }
                else
                {
                    table.AddRow(
                        $"[yellow]{document.DocumentId}[/]",
                        document.Name,
                        document.DocumentType,
                        $"[blue]v{document.Version}[/]",
                        $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]");
                }
                
                documentCount++;
            }
            
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[green]Found {documentCount} KPI document(s)[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in list-kpi command");
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
    
    private static void ConfigureServices(IServiceCollection services, ListKpiSettings settings, ILogger logger)
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
        
        logger.LogDebug("Services configured for list-kpi command");
    }
}
