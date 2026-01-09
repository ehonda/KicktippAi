using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Utility.ListKpi;

public class ListKpiCommand : AsyncCommand<ListKpiSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;

    public ListKpiCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseServiceFactory)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ListKpiSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<ListKpiCommand>();
        
        try
        {
            _console.MarkupLine($"[green]List KPI command initialized for community context:[/] [yellow]{settings.CommunityContext}[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            // Create Firebase services using factory (factory handles env var loading)
            var kpiRepository = _firebaseServiceFactory.CreateKpiRepository();
            
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
}
