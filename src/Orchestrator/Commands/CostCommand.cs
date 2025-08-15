using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using FirebaseAdapter;
using Core;
using Google.Cloud.Firestore;
using System.Globalization;

namespace Orchestrator.Commands;

public class CostCommand : AsyncCommand<CostSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CostSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<CostCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Cost command initialized[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.All)
            {
                AnsiConsole.MarkupLine("[blue]All mode enabled - aggregating over all available data[/]");
            }
            
            var predictionRepository = serviceProvider.GetRequiredService<IPredictionRepository>();
            var firestoreDb = serviceProvider.GetRequiredService<FirestoreDb>();
            
            // Parse filter parameters
            var matchdays = ParseMatchdays(settings);
            var models = ParseModels(settings);
            var communityContexts = ParseCommunityContexts(settings);
            
            // Get available models and community contexts if not specified
            var availableModels = models ?? await GetAvailableModels(firestoreDb);
            var availableCommunityContexts = communityContexts ?? await GetAvailableCommunityContexts(firestoreDb);
            var availableMatchdays = matchdays ?? await GetAvailableMatchdays(firestoreDb);
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Filters:[/]");
                AnsiConsole.MarkupLine($"[dim]  Matchdays: {(matchdays?.Any() == true ? string.Join(", ", matchdays) : $"all ({availableMatchdays.Count} found)")}[/]");
                AnsiConsole.MarkupLine($"[dim]  Models: {(models?.Any() == true ? string.Join(", ", models) : $"all ({availableModels.Count} found)")}[/]");
                AnsiConsole.MarkupLine($"[dim]  Community Contexts: {(communityContexts?.Any() == true ? string.Join(", ", communityContexts) : $"all ({availableCommunityContexts.Count} found)")}[/]");
                AnsiConsole.MarkupLine($"[dim]  Include Bonus: {settings.Bonus}[/]");
            }
            
            // Calculate costs
            var totalCost = 0.0;
            var matchPredictionCost = 0.0;
            var bonusPredictionCost = 0.0;
            var matchPredictionCount = 0;
            var bonusPredictionCount = 0;
            
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("Calculating costs...", ctx =>
                {
                    foreach (var model in availableModels)
                    {
                        foreach (var communityContext in availableCommunityContexts)
                        {
                            ctx.Status($"Processing {model} - {communityContext}...");
                            
                            if (settings.Verbose)
                            {
                                AnsiConsole.MarkupLine($"[dim]  Processing model: {model}, community context: {communityContext}[/]");
                            }
                            
                            // Get match prediction costs
                            var (matchCost, matchCount) = GetMatchPredictionCosts(firestoreDb, model, communityContext, availableMatchdays).Result;
                            matchPredictionCost += matchCost;
                            matchPredictionCount += matchCount;
                            
                            if (settings.Verbose && (matchCost > 0 || matchCount > 0))
                            {
                                AnsiConsole.MarkupLine($"[dim]    Match predictions: {matchCount} documents, ${matchCost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
                            }
                            
                            // Get bonus prediction costs if requested
                            if (settings.Bonus)
                            {
                                var (bonusCost, bonusCount) = GetBonusPredictionCosts(firestoreDb, model, communityContext).Result;
                                bonusPredictionCost += bonusCost;
                                bonusPredictionCount += bonusCount;
                                
                                if (settings.Verbose && (bonusCost > 0 || bonusCount > 0))
                                {
                                    AnsiConsole.MarkupLine($"[dim]    Bonus predictions: {bonusCount} documents, ${bonusCost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
                                }
                            }
                        }
                    }
                });
            
            totalCost = matchPredictionCost + bonusPredictionCost;
            
            // Display results
            var table = new Table();
            table.AddColumn("Category");
            table.AddColumn("Count", col => col.RightAligned());
            table.AddColumn("Cost (USD)", col => col.RightAligned());
            
            table.AddRow("Match Predictions", matchPredictionCount.ToString(CultureInfo.InvariantCulture), $"${matchPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}");
            
            if (settings.Bonus)
            {
                table.AddRow("Bonus Predictions", bonusPredictionCount.ToString(CultureInfo.InvariantCulture), $"${bonusPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}");
            }
            
            table.AddEmptyRow();
            table.AddRow("[bold]Total[/]", $"[bold]{(matchPredictionCount + bonusPredictionCount).ToString(CultureInfo.InvariantCulture)}[/]", $"[bold]${totalCost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
            
            AnsiConsole.Write(table);
            
            AnsiConsole.MarkupLine($"[green]✓ Cost calculation completed[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate costs");
            AnsiConsole.MarkupLine($"[red]✗ Failed to calculate costs: {ex.Message}[/]");
            return 1;
        }
    }
    
    private static void ConfigureServices(ServiceCollection services, CostSettings settings, ILogger logger)
    {
        // Add logging
        services.AddSingleton(logger);
        services.AddLogging(builder => 
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add Firebase database if credentials are available
        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
        
        if (!string.IsNullOrEmpty(firebaseProjectId) && !string.IsNullOrEmpty(firebaseServiceAccountJson))
        {
            services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, "default"); // Use a default community since it's not used anyway
            logger.LogInformation("Firebase database integration enabled for project: {ProjectId}", firebaseProjectId);
        }
        else
        {
            throw new InvalidOperationException("Firebase credentials are required for cost analysis. Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables.");
        }
    }
    
    private List<int>? ParseMatchdays(CostSettings settings)
    {
        if (settings.All || string.IsNullOrWhiteSpace(settings.Matchdays))
            return null; // null means all matchdays
            
        if (settings.Matchdays.Trim().ToLowerInvariant() == "all")
            return null;
            
        try
        {
            return settings.Matchdays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(md => int.Parse(md.Trim()))
                .ToList();
        }
        catch (FormatException)
        {
            throw new ArgumentException($"Invalid matchday format: {settings.Matchdays}. Use comma-separated numbers (e.g., '1,2,3') or 'all'.");
        }
    }
    
    private async Task<List<int>> GetAvailableMatchdays(FirestoreDb firestoreDb)
    {
        var matchdays = new HashSet<int>();
        var competition = "bundesliga-2025-26";

        // Query match predictions for unique matchdays
        var query = firestoreDb.Collection("match-predictions")
            .WhereEqualTo("competition", competition);
        var snapshot = await query.GetSnapshotAsync();
        
        foreach (var doc in snapshot.Documents)
        {
            if (doc.TryGetValue<int>("matchday", out var matchday) && matchday > 0)
            {
                matchdays.Add(matchday);
            }
        }

        return matchdays.OrderBy(m => m).ToList();
    }
    
    private List<string>? ParseModels(CostSettings settings)
    {
        if (settings.All || string.IsNullOrWhiteSpace(settings.Models))
            return null; // null means all models
            
        if (settings.Models.Trim().ToLowerInvariant() == "all")
            return null;
            
        return settings.Models
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Trim())
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();
    }
    
    private List<string>? ParseCommunityContexts(CostSettings settings)
    {
        if (settings.All || string.IsNullOrWhiteSpace(settings.CommunityContexts))
            return null; // null means all community contexts
            
        if (settings.CommunityContexts.Trim().ToLowerInvariant() == "all")
            return null;
            
        return settings.CommunityContexts
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(cc => cc.Trim())
            .Where(cc => !string.IsNullOrWhiteSpace(cc))
            .ToList();
    }
    
    private async Task<List<string>> GetAvailableModels(FirestoreDb firestoreDb)
    {
        var models = new HashSet<string>();
        var competition = "bundesliga-2025-26";

        // Query match predictions for unique models
        var matchQuery = firestoreDb.Collection("match-predictions")
            .WhereEqualTo("competition", competition);
        var matchSnapshot = await matchQuery.GetSnapshotAsync();
        
        foreach (var doc in matchSnapshot.Documents)
        {
            if (doc.TryGetValue<string>("model", out var model) && !string.IsNullOrWhiteSpace(model))
            {
                models.Add(model);
            }
        }

        // Query bonus predictions for unique models
        var bonusQuery = firestoreDb.Collection("bonus-predictions")
            .WhereEqualTo("competition", competition);
        var bonusSnapshot = await bonusQuery.GetSnapshotAsync();
        
        foreach (var doc in bonusSnapshot.Documents)
        {
            if (doc.TryGetValue<string>("model", out var model) && !string.IsNullOrWhiteSpace(model))
            {
                models.Add(model);
            }
        }

        return models.ToList();
    }
    
    private async Task<List<string>> GetAvailableCommunityContexts(FirestoreDb firestoreDb)
    {
        var communityContexts = new HashSet<string>();
        var competition = "bundesliga-2025-26";

        // Query match predictions for unique community contexts
        var matchQuery = firestoreDb.Collection("match-predictions")
            .WhereEqualTo("competition", competition);
        var matchSnapshot = await matchQuery.GetSnapshotAsync();
        
        foreach (var doc in matchSnapshot.Documents)
        {
            if (doc.TryGetValue<string>("communityContext", out var context) && !string.IsNullOrWhiteSpace(context))
            {
                communityContexts.Add(context);
            }
        }

        // Query bonus predictions for unique community contexts
        var bonusQuery = firestoreDb.Collection("bonus-predictions")
            .WhereEqualTo("competition", competition);
        var bonusSnapshot = await bonusQuery.GetSnapshotAsync();
        
        foreach (var doc in bonusSnapshot.Documents)
        {
            if (doc.TryGetValue<string>("communityContext", out var context) && !string.IsNullOrWhiteSpace(context))
            {
                communityContexts.Add(context);
            }
        }

        return communityContexts.ToList();
    }
    
    private async Task<(double cost, int count)> GetMatchPredictionCosts(
        FirestoreDb firestoreDb,
        string model, 
        string communityContext, 
        List<int> matchdays)
    {
        var totalCost = 0.0;
        var competition = "bundesliga-2025-26"; // This matches the default in FirestoreModels
        
        // Query for match predictions with cost data
        var query = firestoreDb.Collection("match-predictions")
            .WhereEqualTo("competition", competition)
            .WhereEqualTo("model", model)
            .WhereEqualTo("communityContext", communityContext);
            
        // Add matchday filter if specified
        if (matchdays?.Count > 0)
        {
            query = query.WhereIn("matchday", matchdays.Cast<object>().ToArray());
        }
        
        var snapshot = await query.GetSnapshotAsync();
        
        foreach (var doc in snapshot.Documents)
        {
            if (doc.Exists && doc.TryGetValue<object>("cost", out var costValue))
            {
                if (costValue is double cost)
                {
                    totalCost += cost;
                }
                else if (double.TryParse(costValue.ToString(), out var parsedCost))
                {
                    totalCost += parsedCost;
                }
            }
        }
        
        return (totalCost, snapshot.Documents.Count);
    }
    
    private async Task<(double cost, int count)> GetBonusPredictionCosts(
        FirestoreDb firestoreDb,
        string model, 
        string communityContext)
    {
        var totalCost = 0.0;
        var competition = "bundesliga-2025-26"; // This matches the default in FirestoreModels
        
        // Query for bonus predictions with cost data
        var query = firestoreDb.Collection("bonus-predictions")
            .WhereEqualTo("competition", competition)
            .WhereEqualTo("model", model)
            .WhereEqualTo("communityContext", communityContext);
        
        var snapshot = await query.GetSnapshotAsync();
        
        foreach (var doc in snapshot.Documents)
        {
            if (doc.Exists && doc.TryGetValue<object>("cost", out var costValue))
            {
                if (costValue is double cost)
                {
                    totalCost += cost;
                }
                else if (double.TryParse(costValue.ToString(), out var parsedCost))
                {
                    totalCost += parsedCost;
                }
            }
        }
        
        return (totalCost, snapshot.Documents.Count);
    }
}
