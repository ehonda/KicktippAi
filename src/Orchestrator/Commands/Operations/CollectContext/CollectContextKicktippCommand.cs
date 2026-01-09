using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.CollectContext;

/// <summary>
/// Command for collecting Kicktipp context documents and storing them in the database.
/// </summary>
public class CollectContextKicktippCommand : AsyncCommand<CollectContextKicktippSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IContextProviderFactory _contextProviderFactory;

    public CollectContextKicktippCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IContextProviderFactory contextProviderFactory)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _contextProviderFactory = contextProviderFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CollectContextKicktippSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<CollectContextKicktippCommand>();
        
        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.CommunityContext))
            {
                _console.MarkupLine("[red]Error: Community context is required[/]");
                return 1;
            }
            
            _console.MarkupLine($"[green]Collect-context kicktipp command initialized[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }
            
            // Execute the context collection workflow
            await ExecuteKicktippContextCollection(settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing collect-context kicktipp command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task ExecuteKicktippContextCollection(CollectContextKicktippSettings settings, ILogger logger)
    {
        // Create services using factories (factories handle env var loading)
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var contextRepository = _firebaseServiceFactory.CreateContextRepository();
        
        // Create context provider using factory
        var contextProvider = _contextProviderFactory.CreateKicktippContextProvider(
            kicktippClient, settings.CommunityContext, settings.CommunityContext);
        
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{settings.CommunityContext}[/]");
        _console.MarkupLine("[blue]Getting current matchday matches...[/]");
        
        // Step 1: Get current matchday matches
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.CommunityContext);
        
        if (!matchesWithHistory.Any())
        {
            _console.MarkupLine("[yellow]No matches found for current matchday[/]");
            return;
        }
        
        _console.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");
        
        // Step 2: Collect all unique context documents for all matches
        var allContextDocuments = new Dictionary<string, string>(); // documentName -> content
        
        foreach (var matchWithHistory in matchesWithHistory)
        {
            var match = matchWithHistory.Match;
            _console.MarkupLine($"[cyan]Collecting context for:[/] {match.HomeTeam} vs {match.AwayTeam}");
            
            try
            {
                // Get context for this specific match
                await foreach (var contextDoc in contextProvider.GetMatchContextAsync(match.HomeTeam, match.AwayTeam))
                {
                    // Use the document name as key to avoid duplicates
                    if (!allContextDocuments.ContainsKey(contextDoc.Name))
                    {
                        allContextDocuments[contextDoc.Name] = contextDoc.Content;
                        
                        if (settings.Verbose)
                        {
                            _console.MarkupLine($"[dim]  Collected context document: {contextDoc.Name}[/]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect context for match {HomeTeam} vs {AwayTeam}", match.HomeTeam, match.AwayTeam);
                _console.MarkupLine($"[red]  ✗ Failed to collect context: {ex.Message}[/]");
            }
        }
        
        _console.MarkupLine($"[green]Collected {allContextDocuments.Count} unique context documents[/]");
        
        // Step 3: Save context documents to database
        var savedCount = 0;
        var skippedCount = 0;
        var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        
        foreach (var (documentName, content) in allContextDocuments)
        {
            try
            {
                if (settings.DryRun)
                {
                    _console.MarkupLine($"[magenta]  Dry run - would save:[/] {documentName}");
                    continue;
                }
                
                // Check if this is a history document that needs Data_Collected_At column
                string finalContent = content;
                if (IsHistoryDocument(documentName))
                {
                    // Get the previous version to compare against
                    var previousDocument = await contextRepository.GetLatestContextDocumentAsync(documentName, settings.CommunityContext);
                    var previousContent = previousDocument?.Content;
                    
                    // Add Data_Collected_At column with current date for new matches
                    finalContent = HistoryCsvUtility.AddDataCollectedAtColumn(content, previousContent, currentDate);
                    
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Added Data_Collected_At column to {documentName}[/]");
                    }
                }
                
                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    documentName, 
                    finalContent, 
                    settings.CommunityContext);
                
                if (savedVersion.HasValue)
                {
                    savedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[green]  ✓ Saved {documentName} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    skippedCount++;
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  - Skipped {documentName} (content unchanged)[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save context document {DocumentName}", documentName);
                _console.MarkupLine($"[red]  ✗ Failed to save {documentName}: {ex.Message}[/]");
            }
        }
        
        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run completed - would have processed {allContextDocuments.Count} documents[/]");
        }
        else
        {
            _console.MarkupLine($"[green]✓ Context collection completed![/]");
            _console.MarkupLine($"[green]  Saved: {savedCount} documents[/]");
            _console.MarkupLine($"[dim]  Skipped: {skippedCount} documents (unchanged)[/]");
        }
    }
    
    private static bool IsHistoryDocument(string documentName)
    {
        return documentName.StartsWith("recent-history-", StringComparison.OrdinalIgnoreCase) ||
               documentName.StartsWith("home-history-", StringComparison.OrdinalIgnoreCase) ||
               documentName.StartsWith("away-history-", StringComparison.OrdinalIgnoreCase);
    }
}
