using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Core;
using KicktippIntegration;
using FirebaseAdapter;

namespace Orchestrator.Commands;

public class VerifyMatchdayCommand : AsyncCommand<VerifySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VerifySettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<VerifyMatchdayCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Verify matchday command initialized[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.Agent)
            {
                AnsiConsole.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }
            
            if (settings.InitMatchday)
            {
                AnsiConsole.MarkupLine("[cyan]Init matchday mode enabled - will return error if no predictions exist[/]");
            }
            
            // Execute the verification workflow
            var hasDiscrepancies = await ExecuteVerificationWorkflow(serviceProvider, settings, logger);
            
            return hasDiscrepancies ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing verify matchday command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private static async Task<bool> ExecuteVerificationWorkflow(IServiceProvider serviceProvider, VerifySettings settings, ILogger logger)
    {
        var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
        
        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = serviceProvider.GetService<IPredictionRepository>();
        if (predictionRepository == null)
        {
            AnsiConsole.MarkupLine("[red]Error: Database not configured. Cannot verify predictions without database access.[/]");
            AnsiConsole.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        AnsiConsole.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        AnsiConsole.MarkupLine("[blue]Getting placed predictions from Kicktipp...[/]");
        
        // Step 1: Get placed predictions from Kicktipp
        var placedPredictions = await kicktippClient.GetPlacedPredictionsAsync(settings.Community);
        
        if (!placedPredictions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No matches found on Kicktipp[/]");
            return false;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {placedPredictions.Count} matches on Kicktipp[/]");
        
        AnsiConsole.MarkupLine("[blue]Retrieving predictions from database...[/]");
        
        var hasDiscrepancies = false;
        var totalMatches = 0;
        var matchesWithPlacedPredictions = 0;
        var matchesWithDatabasePredictions = 0;
        var matchingPredictions = 0;
        
        // Step 2: For each match, compare with database predictions
        foreach (var (match, kicktippPrediction) in placedPredictions)
        {
            totalMatches++;
            
            try
            {
                // Get prediction from database
                if (settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]  Looking up: {match.HomeTeam} vs {match.AwayTeam} at {match.StartsAt}[/]");
                }
                
                var databasePrediction = await predictionRepository.GetPredictionAsync(match);
                
                if (kicktippPrediction != null)
                {
                    matchesWithPlacedPredictions++;
                }
                
                if (databasePrediction != null)
                {
                    matchesWithDatabasePredictions++;
                    if (settings.Verbose && !settings.Agent)
                    {
                        AnsiConsole.MarkupLine($"[dim]  Found database prediction: {databasePrediction.HomeGoals}:{databasePrediction.AwayGoals}[/]");
                    }
                }
                else if (settings.Verbose && !settings.Agent)
                {
                    AnsiConsole.MarkupLine($"[dim]  No database prediction found[/]");
                }
                
                // Compare predictions
                var isMatchingPrediction = ComparePredictions(kicktippPrediction, databasePrediction);
                
                if (isMatchingPrediction)
                {
                    matchingPredictions++;
                    
                    if (settings.Verbose)
                    {
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[green]✓ {match.HomeTeam} vs {match.AwayTeam}[/] [dim](match)[/]");
                        }
                        else
                        {
                            var predictionText = kicktippPrediction?.ToString() ?? "no prediction";
                            AnsiConsole.MarkupLine($"[green]✓ {match.HomeTeam} vs {match.AwayTeam}:[/] {predictionText} [dim](match)[/]");
                        }
                    }
                }
                else
                {
                    hasDiscrepancies = true;
                    
                    if (settings.Agent)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}[/] [dim](mismatch)[/]");
                    }
                    else
                    {
                        var kicktippText = kicktippPrediction?.ToString() ?? "no prediction";
                        var databaseText = databasePrediction != null ? $"{databasePrediction.HomeGoals}:{databasePrediction.AwayGoals}" : "no prediction";
                        
                        AnsiConsole.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}:[/]");
                        AnsiConsole.MarkupLine($"  [yellow]Kicktipp:[/] {kicktippText}");
                        AnsiConsole.MarkupLine($"  [yellow]Database:[/] {databaseText}");
                    }
                }
            }
            catch (Exception ex)
            {
                hasDiscrepancies = true;
                logger.LogError(ex, "Error verifying prediction for {Match}", $"{match.HomeTeam} vs {match.AwayTeam}");
                
                if (settings.Agent)
                {
                    AnsiConsole.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}[/] [dim](error)[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}:[/] Error during verification");
                }
            }
        }
        
        // Step 3: Display summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Verification Summary:[/]");
        AnsiConsole.MarkupLine($"  Total matches: {totalMatches}");
        AnsiConsole.MarkupLine($"  Matches with Kicktipp predictions: {matchesWithPlacedPredictions}");
        AnsiConsole.MarkupLine($"  Matches with database predictions: {matchesWithDatabasePredictions}");
        AnsiConsole.MarkupLine($"  Matching predictions: {matchingPredictions}");
        
        // Check for init-matchday mode first
        if (settings.InitMatchday && matchesWithDatabasePredictions == 0)
        {
            AnsiConsole.MarkupLine("[yellow]  Init matchday detected - no database predictions exist[/]");
            AnsiConsole.MarkupLine("[red]Returning error to trigger initial prediction workflow[/]");
            return true; // Return error to trigger workflow
        }
        
        if (hasDiscrepancies)
        {
            AnsiConsole.MarkupLine($"[red]  Discrepancies found: {totalMatches - matchingPredictions}[/]");
            AnsiConsole.MarkupLine("[red]Verification failed - predictions do not match[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]  All predictions match - verification successful[/]");
        }
        
        return hasDiscrepancies;
    }
    
    private static bool ComparePredictions(BetPrediction? kicktippPrediction, Prediction? databasePrediction)
    {
        // Both null - match
        if (kicktippPrediction == null && databasePrediction == null)
        {
            return true;
        }
        
        // One null, other not - mismatch
        if (kicktippPrediction == null || databasePrediction == null)
        {
            return false;
        }
        
        // Both have values - compare
        return kicktippPrediction.HomeGoals == databasePrediction.HomeGoals &&
               kicktippPrediction.AwayGoals == databasePrediction.AwayGoals;
    }
    
    private static void ConfigureServices(IServiceCollection services, VerifySettings settings, ILogger logger)
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
    }
}
