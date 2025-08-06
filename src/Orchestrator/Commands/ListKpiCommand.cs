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
            
            AnsiConsole.MarkupLine($"[green]List KPI command initialized for community:[/] [yellow]{settings.Community}[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
                if (!string.IsNullOrWhiteSpace(settings.Tags))
                {
                    AnsiConsole.MarkupLine($"[dim]Filtering by tags: {settings.Tags}[/]");
                }
            }
            
            // Get Firebase KPI context provider
            var kpiContextProvider = serviceProvider.GetRequiredService<FirebaseKpiContextProvider>();
            
            var table = new Table();
            table.AddColumn("Document ID");
            table.AddColumn("Name");
            table.AddColumn("Type");
            table.AddColumn("Content Preview");
            
            if (settings.Verbose)
            {
                table.AddColumn("Tags");
            }

            int documentCount = 0;
            
            if (!string.IsNullOrWhiteSpace(settings.Tags))
            {
                // Filter by tags
                var tags = settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToArray();
                
                await foreach (var documentContext in kpiContextProvider.GetKpiDocumentsByTagsAsync(tags))
                {
                    var preview = documentContext.Content.Length > 100 
                        ? documentContext.Content.Substring(0, 100) + "..." 
                        : documentContext.Content;
                    
                    if (settings.Verbose)
                    {
                        // We need to get the document details for tags - this is a limitation of the current design
                        table.AddRow(
                            $"[yellow]{documentContext.Name.Split(' ')[0]}[/]",
                            documentContext.Name,
                            documentContext.Name.Contains("(") ? documentContext.Name.Split('(')[1].TrimEnd(')') : "unknown",
                            $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]",
                            "[dim]N/A[/]");
                    }
                    else
                    {
                        table.AddRow(
                            $"[yellow]{documentContext.Name.Split(' ')[0]}[/]",
                            documentContext.Name,
                            documentContext.Name.Contains("(") ? documentContext.Name.Split('(')[1].TrimEnd(')') : "unknown",
                            $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]");
                    }
                    
                    documentCount++;
                }
            }
            else
            {
                // Get all documents
                await foreach (var documentContext in kpiContextProvider.GetContextAsync())
                {
                    var preview = documentContext.Content.Length > 100 
                        ? documentContext.Content.Substring(0, 100) + "..." 
                        : documentContext.Content;
                    
                    if (settings.Verbose)
                    {
                        table.AddRow(
                            $"[yellow]{documentContext.Name.Split(' ')[0]}[/]",
                            documentContext.Name,
                            documentContext.Name.Contains("(") ? documentContext.Name.Split('(')[1].TrimEnd(')') : "unknown",
                            $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]",
                            "[dim]N/A[/]");
                    }
                    else
                    {
                        table.AddRow(
                            $"[yellow]{documentContext.Name.Split(' ')[0]}[/]",
                            documentContext.Name,
                            documentContext.Name.Contains("(") ? documentContext.Name.Split('(')[1].TrimEnd(')') : "unknown",
                            $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]");
                    }
                    
                    documentCount++;
                }
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
        
        // Configure Firebase
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
        }, settings.Community);
        
        logger.LogDebug("Services configured for list-kpi command");
    }
}
