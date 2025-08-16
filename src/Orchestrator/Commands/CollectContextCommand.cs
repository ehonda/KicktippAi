using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Core;
using KicktippIntegration;
using ContextProviders.Kicktipp;
using FirebaseAdapter;

namespace Orchestrator.Commands;

/// <summary>
/// Command for collecting context documents and storing them in the database.
/// </summary>
public class CollectContextCommand : AsyncCommand<CollectContextSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CollectContextSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<CollectContextCommand>();
        
        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.Subcommand))
            {
                AnsiConsole.MarkupLine("[red]Error: Subcommand is required[/]");
                return 1;
            }

            if (settings.Subcommand.ToLowerInvariant() != "kicktipp")
            {
                AnsiConsole.MarkupLine($"[red]Error: Unknown subcommand '{settings.Subcommand}'. Available: kicktipp[/]");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(settings.Community))
            {
                AnsiConsole.MarkupLine("[red]Error: Community is required[/]");
                return 1;
            }
            
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Collect-context command initialized:[/] [yellow]{settings.Subcommand}[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }
            
            // Execute the context collection workflow
            await ExecuteContextCollectionWorkflow(serviceProvider, settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing collect-context command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private static async Task ExecuteContextCollectionWorkflow(IServiceProvider serviceProvider, CollectContextSettings settings, ILogger logger)
    {
        if (settings.Subcommand.ToLowerInvariant() != "kicktipp")
        {
            AnsiConsole.MarkupLine($"[red]Unknown subcommand: {settings.Subcommand}[/]");
            return;
        }

        await ExecuteKicktippContextCollection(serviceProvider, settings, logger);
    }

    private static async Task ExecuteKicktippContextCollection(IServiceProvider serviceProvider, CollectContextSettings settings, ILogger logger)
    {
        var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
        var contextProvider = serviceProvider.GetRequiredService<KicktippContextProvider>();
        var contextRepository = serviceProvider.GetService<IContextRepository>();
        
        if (contextRepository == null)
        {
            AnsiConsole.MarkupLine("[red]Database not available - context repository not configured[/]");
            return;
        }
        
        // Determine community context (use explicit setting or fall back to community name)
        string communityContext = settings.CommunityContext ?? settings.Community;
        
        AnsiConsole.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        AnsiConsole.MarkupLine("[blue]Getting current matchday matches...[/]");
        
        // Step 1: Get current matchday matches
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.Community);
        
        if (!matchesWithHistory.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No matches found for current matchday[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");
        
        // Step 2: Collect all unique context documents for all matches
        var allContextDocuments = new Dictionary<string, string>(); // documentName -> content
        
        foreach (var matchWithHistory in matchesWithHistory)
        {
            var match = matchWithHistory.Match;
            AnsiConsole.MarkupLine($"[cyan]Collecting context for:[/] {match.HomeTeam} vs {match.AwayTeam}");
            
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
                            AnsiConsole.MarkupLine($"[dim]  Collected context document: {contextDoc.Name}[/]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to collect context for match {HomeTeam} vs {AwayTeam}", match.HomeTeam, match.AwayTeam);
                AnsiConsole.MarkupLine($"[red]  ✗ Failed to collect context: {ex.Message}[/]");
            }
        }
        
        AnsiConsole.MarkupLine($"[green]Collected {allContextDocuments.Count} unique context documents[/]");
        
        // Step 3: Save context documents to database
        var savedCount = 0;
        var skippedCount = 0;
        
        foreach (var (documentName, content) in allContextDocuments)
        {
            try
            {
                if (settings.DryRun)
                {
                    AnsiConsole.MarkupLine($"[magenta]  Dry run - would save:[/] {documentName}");
                    continue;
                }
                
                var savedVersion = await contextRepository.SaveContextDocumentAsync(
                    documentName, 
                    content, 
                    communityContext);
                
                if (savedVersion.HasValue)
                {
                    savedCount++;
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[green]  ✓ Saved {documentName} as version {savedVersion.Value}[/]");
                    }
                }
                else
                {
                    skippedCount++;
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]  - Skipped {documentName} (content unchanged)[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save context document {DocumentName}", documentName);
                AnsiConsole.MarkupLine($"[red]  ✗ Failed to save {documentName}: {ex.Message}[/]");
            }
        }
        
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[magenta]✓ Dry run completed - would have processed {allContextDocuments.Count} documents[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ Context collection completed![/]");
            AnsiConsole.MarkupLine($"[green]  Saved: {savedCount} documents[/]");
            AnsiConsole.MarkupLine($"[dim]  Skipped: {skippedCount} documents (unchanged)[/]");
        }
    }
    
    private static void ConfigureServices(IServiceCollection services, CollectContextSettings settings, ILogger logger)
    {
        // Add logging
        services.AddSingleton(logger);
        
        // Get Kicktipp credentials from environment
        var username = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME");
        var password = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD");
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("KICKTIPP_USERNAME and KICKTIPP_PASSWORD environment variables are required");
        }
        
        // Configure Kicktipp credentials
        services.Configure<KicktippOptions>(options =>
        {
            options.Username = username;
            options.Password = password;
        });
        
        // Add Kicktipp integration
        services.AddKicktippClient();
        
        // Add Firebase database if credentials are available
        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
        
        if (!string.IsNullOrEmpty(firebaseProjectId) && !string.IsNullOrEmpty(firebaseServiceAccountJson))
        {
            services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, settings.Community);
            logger.LogInformation("Firebase database integration enabled for project: {ProjectId}, community: {Community}", firebaseProjectId, settings.Community);
        }
        else
        {
            logger.LogWarning("Firebase credentials not found. Database integration disabled.");
            logger.LogInformation("Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables to enable database features");
        }
        
        // Add context provider
        services.AddSingleton<KicktippContextProvider>(provider =>
        {
            var kicktippClient = provider.GetRequiredService<IKicktippClient>();
            string communityContext = settings.CommunityContext ?? settings.Community;
            return new KicktippContextProvider(kicktippClient, settings.Community, communityContext);
        });
    }
}
