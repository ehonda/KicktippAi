using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using FirebaseAdapter;

namespace Orchestrator.Commands.Utility.ListKpi;

public class ListKpiCommand : AsyncCommand<ListKpiSettings>
{
    private readonly IAnsiConsole _console;

    public ListKpiCommand(IAnsiConsole console)
    {
        _console = console;
    }

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
            
            _console.MarkupLine($"[green]List KPI command initialized for community context:[/] [yellow]{settings.CommunityContext}[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            // Get Firebase KPI repository
            var kpiRepository = serviceProvider.GetRequiredService<IKpiRepository>();
            
            var table = new Table();
            table.AddColumn("Document Name");
            table.AddColumn("Version");
            table.AddColumn("Content Preview");
            table.AddColumn("Description");

            int documentCount = 0;
            
            // Get all latest documents directly from repository for better version support
            var kpiDocuments = await kpiRepository.GetAllKpiDocumentsAsync(settings.CommunityContext);
            
            foreach (var document in kpiDocuments)
            {
                var preview = document.Content.Length > 100 
                    ? document.Content.Substring(0, 100) + "..." 
                    : document.Content;
                
                var description = document.Description.Length > 50
                    ? document.Description.Substring(0, 50) + "..."
                    : document.Description;
                
                table.AddRow(
                    $"[yellow]{document.DocumentName}[/]",
                    $"[blue]v{document.Version}[/]",
                    $"[dim]{preview.Replace("\n", " ").Replace("\t", " ")}[/]",
                    $"[dim]{description}[/]");
                
                documentCount++;
            }
            
            _console.Write(table);
            _console.MarkupLine($"[green]Found {documentCount} KPI document(s)[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in list-kpi command");
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
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
