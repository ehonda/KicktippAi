using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Core;
using KicktippIntegration;
using OpenAiIntegration;
using ContextProviders.Kicktipp;
using FirebaseAdapter;

namespace Orchestrator.Commands;

public class MatchdayCommand : AsyncCommand<BaseSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<MatchdayCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Matchday command initialized with model:[/] [yellow]{settings.Model}[/]");
            
            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.OverrideKicktipp)
            {
                AnsiConsole.MarkupLine("[yellow]Override mode enabled - will override existing Kicktipp predictions[/]");
            }
            
            if (settings.OverrideDatabase)
            {
                AnsiConsole.MarkupLine("[yellow]Override database mode enabled - will override existing database predictions[/]");
            }
            
            if (settings.Agent)
            {
                AnsiConsole.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }
            
            if (settings.DryRun)
            {
                AnsiConsole.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database or Kicktipp[/]");
            }

            if (!string.IsNullOrEmpty(settings.EstimatedCostsModel))
            {
                AnsiConsole.MarkupLine($"[cyan]Estimated costs will be calculated for model:[/] [yellow]{settings.EstimatedCostsModel}[/]");
            }
            
            // Execute the matchday workflow
            await ExecuteMatchdayWorkflow(serviceProvider, settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing matchday command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private static async Task ExecuteMatchdayWorkflow(IServiceProvider serviceProvider, BaseSettings settings, ILogger logger)
    {
        var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
        var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
        var contextProvider = serviceProvider.GetRequiredService<KicktippContextProvider>();
        var tokenUsageTracker = serviceProvider.GetService<ITokenUsageTracker>();
        
        // Log the prompt paths being used
        if (settings.Verbose)
        {
            AnsiConsole.MarkupLine($"[dim]Match prompt:[/] [blue]{predictionService.GetMatchPromptPath()}[/]");
        }
        
        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = serviceProvider.GetService<IPredictionRepository>();
        var databaseEnabled = predictionRepository != null;
        
        // Reset token usage tracker for this workflow
        tokenUsageTracker?.Reset();
        
        // Determine community context (use explicit setting or fall back to community name)
        string communityContext = settings.CommunityContext ?? settings.Community;
        
        AnsiConsole.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        AnsiConsole.MarkupLine("[blue]Getting current matchday matches...[/]");
        
        // Step 1: Get current matchday via GetMatchesWithHistoryAsync
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.Community);
        
        if (!matchesWithHistory.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No matches found for current matchday[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");
        
        if (databaseEnabled)
        {
            AnsiConsole.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }
        
        var predictions = new Dictionary<Match, BetPrediction>();
        
        // Step 2: For each match, check database first, then predict if needed
        foreach (var matchWithHistory in matchesWithHistory)
        {
            var match = matchWithHistory.Match;
            AnsiConsole.MarkupLine($"[cyan]Processing:[/] {match.HomeTeam} vs {match.AwayTeam}");
            
            try
            {
                Prediction? prediction = null;
                bool fromDatabase = false;
                
                // Check if we have an existing prediction in the database
                if (databaseEnabled && !settings.OverrideDatabase)
                {
                    prediction = await predictionRepository!.GetPredictionAsync(match, settings.Model, communityContext);
                    if (prediction != null)
                    {
                        fromDatabase = true;
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Found existing prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](from database)[/]");
                        }
                    }
                }
                
                // If no existing prediction, generate a new one
                if (prediction == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
                    // Step 3: Get context using GetMatchContextAsync
                    var contextDocuments = new List<DocumentContext>();
                    await foreach (var context in contextProvider.GetMatchContextAsync(match.HomeTeam, match.AwayTeam))
                    {
                        contextDocuments.Add(context);
                    }
                    
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]    Using {contextDocuments.Count} context documents[/]");
                    }
                    
                    // Show context documents if requested
                    if (settings.ShowContextDocuments)
                    {
                        AnsiConsole.MarkupLine($"[cyan]    Context documents for {match.HomeTeam} vs {match.AwayTeam}:[/]");
                        foreach (var doc in contextDocuments)
                        {
                            AnsiConsole.MarkupLine($"[dim]    📄 {doc.Name}[/]");
                            
                            // Show first few lines and total line count for readability
                            var lines = doc.Content.Split('\n');
                            var previewLines = lines.Take(10).ToArray();
                            var hasMore = lines.Length > 10;
                            
                            foreach (var line in previewLines)
                            {
                                AnsiConsole.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }
                            
                            if (hasMore)
                            {
                                AnsiConsole.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }
                            
                            AnsiConsole.MarkupLine($"[dim]      (Total: {lines.Length} lines, {doc.Content.Length} characters)[/]");
                            AnsiConsole.WriteLine();
                        }
                    }
                    
                    // Predict the match
                    prediction = await predictionService.PredictMatchAsync(match, contextDocuments);
                    
                    if (prediction != null)
                    {
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Generated prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals}");
                        }
                        
                        // Save to database immediately if enabled
                        if (databaseEnabled && !settings.DryRun)
                        {
                            try
                            {
                                // Get token usage and cost information
                                string tokenUsageJson = "{}"; // Default empty JSON
                                double cost = 0.0;
                                
                                if (tokenUsageTracker != null)
                                {
                                    cost = (double)tokenUsageTracker.GetLastCost(); // Get the cost for this individual match
                                    // Use the new GetLastUsageJson method to get full JSON
                                    tokenUsageJson = tokenUsageTracker.GetLastUsageJson() ?? "{}";
                                }
                                
                                await predictionRepository!.SavePredictionAsync(
                                    match, 
                                    prediction, 
                                    settings.Model, 
                                    tokenUsageJson, 
                                    cost, 
                                    communityContext,
                                    contextDocuments.Select(d => d.Name));
                                    
                                if (settings.Verbose)
                                {
                                    AnsiConsole.MarkupLine($"[dim]    ✓ Saved to database[/]");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to save prediction for match {Match}", match);
                                AnsiConsole.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]    (Dry run - skipped database save)[/]");
                        }
                        
                        // Show individual match token usage in verbose mode
                        if (settings.Verbose && tokenUsageTracker != null)
                        {
                            var matchUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            AnsiConsole.MarkupLine($"[dim]    Token usage: {matchUsage}[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }
                
                // Convert to BetPrediction for Kicktipp
                var betPrediction = new BetPrediction(prediction.HomeGoals, prediction.AwayGoals);
                predictions[match] = betPrediction;
                
                if (!fromDatabase && settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]    Already saved to database[/]");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing match {Match}", match);
                AnsiConsole.MarkupLine($"[red]  ✗ Error processing match: {ex.Message}[/]");
            }
        }
        
        if (!predictions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            return;
        }
        
        // Step 4: Place all predictions using PlaceBetsAsync
        AnsiConsole.MarkupLine($"[blue]Placing {predictions.Count} predictions to Kicktipp...[/]");
        
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} predictions (no actual changes made)[/]");
        }
        else
        {
            var success = await kicktippClient.PlaceBetsAsync(settings.Community, predictions, overrideBets: settings.OverrideKicktipp);
            
            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} predictions![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Failed to place some or all predictions[/]");
            }
        }
        
        // Display token usage summary
        if (tokenUsageTracker != null)
        {
            var summary = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                ? tokenUsageTracker.GetCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                : tokenUsageTracker.GetCompactSummary();
            AnsiConsole.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");
        }
    }
    
    private static void ConfigureServices(IServiceCollection services, BaseSettings settings, ILogger logger)
    {
        // Add logging
        services.AddSingleton(logger);
        
        // Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");
        }
        
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
        
        // Add OpenAI integration
        services.AddOpenAiPredictor(apiKey, settings.Model);
        
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
