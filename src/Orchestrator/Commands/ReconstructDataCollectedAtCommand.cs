using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using FirebaseAdapter;
using System.Globalization;
using CsvHelper;
using EHonda.KicktippAi.Core;

namespace Orchestrator.Commands;

/// <summary>
/// Command for reconstructing Data_Collected_At column in history context documents.
/// </summary>
public class ReconstructDataCollectedAtCommand : AsyncCommand<ReconstructDataCollectedAtSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ReconstructDataCollectedAtSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<ReconstructDataCollectedAtCommand>();
        
        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(settings.CommunityContext))
            {
                AnsiConsole.MarkupLine("[red]Error: Community context is required[/]");
                return 1;
            }
            
            // Use CommunityContext directly
            var communityContext = settings.CommunityContext;
            
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, communityContext, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Reconstruct Data_Collected_At command initialized[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database[/]");
            }
            
            // Execute the reconstruction workflow
            await ExecuteReconstruction(serviceProvider, settings, communityContext, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing reconstruct-data-collected-at command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static async Task ExecuteReconstruction(IServiceProvider serviceProvider, ReconstructDataCollectedAtSettings settings, string communityContext, ILogger logger)
    {
        var contextRepository = serviceProvider.GetService<IContextRepository>();
        
        if (contextRepository == null)
        {
            AnsiConsole.MarkupLine("[red]Database not available - context repository not configured[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[blue]Reconstructing Data_Collected_At for community:[/] [yellow]{communityContext}[/]");
        
        // Get all context document names
        var allDocumentNames = await contextRepository.GetContextDocumentNamesAsync(communityContext);
        
        // Filter for history documents only
        var historyDocuments = allDocumentNames
            .Where(name => IsHistoryDocument(name))
            .ToList();
        
        if (!historyDocuments.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No history documents found for this community[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {historyDocuments.Count} history documents to process[/]");
        
        var processedCount = 0;
        var errorCount = 0;
        
        foreach (var documentName in historyDocuments)
        {
            try
            {
                AnsiConsole.MarkupLine($"[cyan]Processing:[/] {documentName}");
                
                var reconstructed = await ReconstructDocumentVersions(contextRepository, documentName, communityContext, settings.Verbose, settings.DryRun);
                
                if (!settings.DryRun && reconstructed)
                {
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[green]  ✓ Reconstructed {documentName}[/]");
                    }
                    processedCount++;
                }
                else if (settings.DryRun)
                {
                    AnsiConsole.MarkupLine($"[magenta]  Dry run - would reconstruct {documentName}[/]");
                }
                else
                {
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]  - Skipped {documentName} (already has Data_Collected_At or no changes needed)[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reconstruct document {DocumentName}", documentName);
                AnsiConsole.MarkupLine($"[red]  ✗ Failed to reconstruct {documentName}: {ex.Message}[/]");
                errorCount++;
            }
        }
        
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[magenta]✓ Dry run completed - would have processed {historyDocuments.Count} documents[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓ Reconstruction completed![/]");
            AnsiConsole.MarkupLine($"[green]  Processed: {processedCount} documents[/]");
            if (errorCount > 0)
            {
                AnsiConsole.MarkupLine($"[red]  Errors: {errorCount} documents[/]");
            }
        }
    }
    
    private static bool IsHistoryDocument(string documentName)
    {
        return documentName.StartsWith("recent-history-", StringComparison.OrdinalIgnoreCase) ||
               documentName.StartsWith("home-history-", StringComparison.OrdinalIgnoreCase) ||
               documentName.StartsWith("away-history-", StringComparison.OrdinalIgnoreCase);
    }
    
    private static async Task<bool> ReconstructDocumentVersions(IContextRepository contextRepository, string documentName, string communityContext, bool verbose, bool dryRun)
    {
        // Get all versions of the document
        var allVersions = await contextRepository.GetContextDocumentVersionsAsync(documentName, communityContext);
        
        if (!allVersions.Any())
        {
            return false;
        }
        
        // Check if the latest version already has Data_Collected_At column
        var latestVersion = allVersions.OrderByDescending(v => v.Version).First();
        if (HasDataCollectedAtColumn(latestVersion.Content))
        {
            if (verbose)
            {
                Console.WriteLine($"    Document {documentName} already has Data_Collected_At column");
            }
            return false;
        }
        
        // Process each version to add Data_Collected_At column
        var allMatches = new Dictionary<string, string>(); // matchKey -> dataCollectedAt
        
        foreach (var version in allVersions.OrderBy(v => v.Version))
        {
            if (verbose)
            {
                Console.WriteLine($"    Processing version {version.Version} (created: {version.CreatedAt:yyyy-MM-dd})");
            }
            
            var currentMatches = ExtractMatchesFromCsv(version.Content);
            var dateCollected = version.CreatedAt.ToString("yyyy-MM-dd");
            
            // For version 0, tag as (initial)
            if (version.Version == 0)
            {
                dateCollected += " (initial)";
            }
            
            foreach (var match in currentMatches)
            {
                if (!allMatches.ContainsKey(match))
                {
                    allMatches[match] = dateCollected;
                }
            }
            
            // Update the document content with Data_Collected_At column
            var updatedContent = AddDataCollectedAtColumn(version.Content, currentMatches, allMatches);
            
            // Save the updated version (this will overwrite the existing version)
            if (!dryRun)
            {
                await UpdateDocumentVersion(contextRepository, documentName, communityContext, version.Version, updatedContent);
            }
        }
        
        return true;
    }
    
    private static bool HasDataCollectedAtColumn(string csvContent)
    {
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }
        
        var header = lines[0];
        return header.Contains("Data_Collected_At", StringComparison.OrdinalIgnoreCase);
    }
    
    private static HashSet<string> ExtractMatchesFromCsv(string csvContent)
    {
        var matches = new HashSet<string>();
        
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        try
        {
            csv.Read();
            csv.ReadHeader();
            
            while (csv.Read())
            {
                var competition = csv.GetField("Competition") ?? "";
                var homeTeam = csv.GetField("Home_Team") ?? "";
                var awayTeam = csv.GetField("Away_Team") ?? "";
                var score = csv.GetField("Score") ?? "";
                
                // Create a unique key for the match
                var matchKey = $"{competition}|{homeTeam}|{awayTeam}|{score}";
                matches.Add(matchKey);
            }
        }
        catch (Exception)
        {
            // If CSV parsing fails, return empty set
        }
        
        return matches;
    }
    
    private static string AddDataCollectedAtColumn(string csvContent, HashSet<string> currentMatches, Dictionary<string, string> allMatches)
    {
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        using var writer = new StringWriter();
        using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        try
        {
            csv.Read();
            csv.ReadHeader();
            
            // Write new header with Data_Collected_At after Competition
            csvWriter.WriteField("Competition");
            csvWriter.WriteField("Data_Collected_At");
            csvWriter.WriteField("Home_Team");
            csvWriter.WriteField("Away_Team");
            csvWriter.WriteField("Score");
            csvWriter.NextRecord();
            
            while (csv.Read())
            {
                var competition = csv.GetField("Competition") ?? "";
                var homeTeam = csv.GetField("Home_Team") ?? "";
                var awayTeam = csv.GetField("Away_Team") ?? "";
                var score = csv.GetField("Score") ?? "";
                
                var matchKey = $"{competition}|{homeTeam}|{awayTeam}|{score}";
                var dataCollectedAt = allMatches.TryGetValue(matchKey, out var date) ? date : "";
                
                csvWriter.WriteField(competition);
                csvWriter.WriteField(dataCollectedAt);
                csvWriter.WriteField(homeTeam);
                csvWriter.WriteField(awayTeam);
                csvWriter.WriteField(score);
                csvWriter.NextRecord();
            }
        }
        catch (Exception)
        {
            // If parsing fails, return original content
            return csvContent;
        }
        
        return writer.ToString();
    }
    
    private static async Task UpdateDocumentVersion(IContextRepository contextRepository, string documentName, string communityContext, int version, string updatedContent)
    {
        await contextRepository.UpdateContextDocumentVersionAsync(documentName, version, updatedContent, communityContext);
    }
    
    private static void ConfigureServices(IServiceCollection services, ReconstructDataCollectedAtSettings settings, string communityContext, ILogger logger)
    {
        // Add logging
        services.AddSingleton(logger);
        
        // Add logging services for other components
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add Firebase database if credentials are available
        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
        
        if (!string.IsNullOrEmpty(firebaseProjectId) && !string.IsNullOrEmpty(firebaseServiceAccountJson))
        {
            services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, communityContext);
            logger.LogInformation("Firebase database integration enabled for project: {ProjectId}, community: {Community}", firebaseProjectId, communityContext);
        }
        else
        {
            logger.LogWarning("Firebase credentials not found. Database integration disabled.");
            logger.LogInformation("Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables to enable database features");
        }
    }
}
