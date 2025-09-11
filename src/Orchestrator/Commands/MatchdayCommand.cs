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

            // Validate reprediction settings
            if (settings.OverrideDatabase && settings.IsRepredictMode)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] --override-database cannot be used with reprediction flags (--repredict or --max-repredictions)");
                return 1;
            }

            if (settings.MaxRepredictions.HasValue && settings.MaxRepredictions.Value < 0)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] --max-repredictions must be 0 or greater");
                return 1;
            }

            if (settings.IsRepredictMode)
            {
                var maxValue = settings.MaxRepredictions ?? int.MaxValue;
                AnsiConsole.MarkupLine($"[yellow]Reprediction mode enabled - max repredictions: {(settings.MaxRepredictions?.ToString() ?? "unlimited")}[/]");
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
        // Get the context repository (required)
        var contextRepository = serviceProvider.GetRequiredService<IContextRepository>();
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
                bool shouldPredict = false;
                
                // Check if we have an existing prediction in the database
                if (databaseEnabled && !settings.OverrideDatabase && !settings.IsRepredictMode)
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
                
                // Handle reprediction logic
                if (settings.IsRepredictMode && databaseEnabled)
                {
                    var currentRepredictionIndex = await predictionRepository!.GetMatchRepredictionIndexAsync(match, settings.Model, communityContext);
                    
                    if (currentRepredictionIndex == -1)
                    {
                        // No prediction exists yet - create first prediction
                        shouldPredict = true;
                        AnsiConsole.MarkupLine($"[yellow]  → No existing prediction found, creating first prediction...[/]");
                    }
                    else
                    {
                        // Check if we can create another reprediction
                        var maxAllowed = settings.MaxRepredictions ?? int.MaxValue;
                        var nextIndex = currentRepredictionIndex + 1;
                        
                        if (nextIndex <= maxAllowed)
                        {
                            // Before repredicting, check if the current prediction is actually outdated
                            var isOutdated = await CheckPredictionOutdated(predictionRepository!, contextRepository, match, settings.Model, communityContext, settings.Verbose);
                            
                            if (isOutdated)
                            {
                                shouldPredict = true;
                                AnsiConsole.MarkupLine($"[yellow]  → Creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed}) - prediction is outdated[/]");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine($"[green]  ✓ Skipped reprediction - current prediction is up-to-date[/]");
                                
                                // Get the latest prediction for display purposes
                                prediction = await predictionRepository!.GetPredictionAsync(match, settings.Model, communityContext);
                                if (prediction != null)
                                {
                                    fromDatabase = true;
                                    if (!settings.Agent)
                                    {
                                        AnsiConsole.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                    }
                                }
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]  ✗ Skipped - already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                            
                            // Get the latest prediction for display purposes
                            prediction = await predictionRepository!.GetPredictionAsync(match, settings.Model, communityContext);
                            if (prediction != null)
                            {
                                fromDatabase = true;
                                if (!settings.Agent)
                                {
                                    AnsiConsole.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                }
                            }
                        }
                    }
                }
                
                // If no existing prediction (normal mode) or we need to predict (reprediction mode), generate a new one
                if (prediction == null || shouldPredict)
                {
                    AnsiConsole.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
                    // Step 3: Get context using hybrid approach (database first, fallback to on-demand)
                    var contextDocuments = await GetHybridContextAsync(
                        contextRepository, 
                        contextProvider, 
                        match.HomeTeam, 
                        match.AwayTeam, 
                        communityContext, 
                        settings.Verbose);
                    
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
                                
                                if (settings.IsRepredictMode)
                                {
                                    // Save as reprediction with specific index
                                    var currentIndex = await predictionRepository!.GetMatchRepredictionIndexAsync(match, settings.Model, communityContext);
                                    var nextIndex = currentIndex == -1 ? 0 : currentIndex + 1;
                                    
                                    await predictionRepository!.SaveRepredictionAsync(
                                        match, 
                                        prediction, 
                                        settings.Model, 
                                        tokenUsageJson, 
                                        cost, 
                                        communityContext,
                                        contextDocuments.Select(d => d.Name),
                                        nextIndex);
                                        
                                    if (settings.Verbose)
                                    {
                                        AnsiConsole.MarkupLine($"[dim]    ✓ Saved as reprediction {nextIndex} to database[/]");
                                    }
                                }
                                else
                                {
                                    // Save normally (override or new prediction)
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
    
    /// <summary>
    /// Retrieves all available context documents from the database for the given community context.
    /// </summary>
    private static async Task<Dictionary<string, DocumentContext>> GetMatchContextDocumentsAsync(
        IContextRepository contextRepository, 
        string homeTeam,
        string awayTeam,
        string communityContext, 
        bool verbose = false)
    {
        var contextDocuments = new Dictionary<string, DocumentContext>();
        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);
        
        // Define the 7 specific document names needed for a match (required core set)
        var requiredDocuments = new[]
        {
            "bundesliga-standings.csv",
            $"community-rules-{communityContext}.md",
            $"recent-history-{homeAbbreviation}.csv",
            $"recent-history-{awayAbbreviation}.csv",
            $"home-history-{homeAbbreviation}.csv",
            $"away-history-{awayAbbreviation}.csv",
            $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv"
        };

        // Optional transfers documents (do not affect required count). Naming: <abbr>-transfers.csv
        var optionalDocuments = new[]
        {
            $"{homeAbbreviation}-transfers.csv",
            $"{awayAbbreviation}-transfers.csv"
        };
        
        if (verbose)
        {
            AnsiConsole.MarkupLine($"[dim]    Looking for {requiredDocuments.Length} specific context documents in database[/]");
        }
        
        try
        {
            // Retrieve each required document
            foreach (var documentName in requiredDocuments)
            {
                var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                if (contextDoc != null)
                {
                    contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);
                    
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]      ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                    }
                }
                else
                {
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]      ✗ Missing {documentName}[/]");
                    }
                }
            }

            // Retrieve optional transfers documents (best-effort)
            foreach (var documentName in optionalDocuments)
            {
                try
                {
                    var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                    if (contextDoc != null)
                    {
                        // Display name suffix to distinguish optional docs in prediction metadata (helps debug) 
                        contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);
                        if (verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]      ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                        }
                    }
                    else if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]      · Missing optional {documentName}[/]");
                    }
                }
                catch (Exception optEx)
                {
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]      · Failed optional {documentName}: {optEx.Message}[/]");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]    Warning: Failed to retrieve context from database: {ex.Message}[/]");
        }
        
        return contextDocuments;
    }
    
    /// <summary>
    /// Gets context documents using database first, falling back to on-demand context provider if needed.
    /// </summary>
    private static async Task<List<DocumentContext>> GetHybridContextAsync(
        IContextRepository contextRepository,
        KicktippContextProvider contextProvider,
        string homeTeam,
        string awayTeam,
        string communityContext,
        bool verbose = false)
    {
        var contextDocuments = new List<DocumentContext>();
        // Step 1: Retrieve any database documents (required + optional)
        var databaseContexts = await GetMatchContextDocumentsAsync(
            contextRepository,
            homeTeam,
            awayTeam,
            communityContext,
            verbose);

        // Reconstruct required document names (must match logic in GetMatchContextDocumentsAsync)
        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);
        var requiredDocuments = new[]
        {
            "bundesliga-standings.csv",
            $"community-rules-{communityContext}.md",
            $"recent-history-{homeAbbreviation}.csv",
            $"recent-history-{awayAbbreviation}.csv",
            $"home-history-{homeAbbreviation}.csv",
            $"away-history-{awayAbbreviation}.csv",
            $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv"
        };

        int requiredPresent = requiredDocuments.Count(d => databaseContexts.ContainsKey(d));
        int requiredTotal = requiredDocuments.Length;

        if (requiredPresent == requiredTotal)
        {
            // All required docs present; include every database doc (required + optional)
            if (verbose)
            {
                AnsiConsole.MarkupLine($"[green]    Using {databaseContexts.Count} context documents from database (all required present)[/]");
            }
            contextDocuments.AddRange(databaseContexts.Values);
        }
        else
        {
            // Fallback: use on-demand provider but still include any database docs we already have (including optional transfers)
            AnsiConsole.MarkupLine($"[yellow]    Warning: Only found {requiredPresent}/{requiredTotal} required context documents in database (have {databaseContexts.Count} total incl. optional). Falling back to on-demand context while preserving retrieved documents[/]");

            // Start with database docs
            contextDocuments.AddRange(databaseContexts.Values);

            // Add on-demand docs, skipping duplicates by name
            var existingNames = new HashSet<string>(contextDocuments.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            await foreach (var context in contextProvider.GetMatchContextAsync(homeTeam, awayTeam))
            {
                if (existingNames.Add(context.Name))
                {
                    contextDocuments.Add(context);
                }
            }

            if (verbose)
            {
                AnsiConsole.MarkupLine($"[yellow]    Using {contextDocuments.Count} merged context documents (database + on-demand) [/]");
            }
        }

        return contextDocuments;
    }
    
    /// <summary>
    /// Gets a team abbreviation for file naming.
    /// </summary>
    private static string GetTeamAbbreviation(string teamName)
    {
        // Current season team abbreviations (2025-26 Bundesliga participants)
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1. FC Heidenheim 1846", "fch" },
            { "1. FC Köln", "fck" },
            { "1. FC Union Berlin", "fcu" },
            { "1899 Hoffenheim", "tsg" },
            { "Bayer 04 Leverkusen", "b04" },
            { "Bor. Mönchengladbach", "bmg" },
            { "Borussia Dortmund", "bvb" },
            { "Eintracht Frankfurt", "sge" },
            { "FC Augsburg", "fca" },
            { "FC Bayern München", "fcb" },
            { "FC St. Pauli", "fcs" },
            { "FSV Mainz 05", "m05" },
            { "Hamburger SV", "hsv" },
            { "RB Leipzig", "rbl" },
            { "SC Freiburg", "scf" },
            { "VfB Stuttgart", "vfb" },
            { "VfL Wolfsburg", "wob" },
            { "Werder Bremen", "svw" }
        };
        
        if (abbreviations.TryGetValue(teamName, out var abbreviation))
        {
            return abbreviation;
        }
        
        // Fallback: create abbreviation from team name
        var words = teamName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var abbr = new System.Text.StringBuilder();
        
        foreach (var word in words.Take(3)) // Take up to 3 words
        {
            if (word.Length > 0 && char.IsLetter(word[0]))
            {
                abbr.Append(char.ToLowerInvariant(word[0]));
            }
        }
        
        return abbr.Length > 0 ? abbr.ToString() : "unknown";
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
        
        // Add Firebase database (required)
        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
        
        if (string.IsNullOrEmpty(firebaseProjectId) || string.IsNullOrEmpty(firebaseServiceAccountJson))
        {
            throw new InvalidOperationException("FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables are required");
        }
        
        services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, settings.Community);
        logger.LogInformation("Firebase database integration enabled for project: {ProjectId}, community: {Community}", firebaseProjectId, settings.Community);
        
        // Add context provider
        services.AddSingleton<KicktippContextProvider>(provider =>
        {
            var kicktippClient = provider.GetRequiredService<IKicktippClient>();
            string communityContext = settings.CommunityContext ?? settings.Community;
            return new KicktippContextProvider(kicktippClient, settings.Community, communityContext);
        });
    }
    
    private static async Task<bool> CheckPredictionOutdated(IPredictionRepository predictionRepository, IContextRepository contextRepository, Match match, string model, string communityContext, bool verbose)
    {
        try
        {
            // Get prediction metadata with context document names and timestamps
            var predictionMetadata = await predictionRepository.GetPredictionMetadataAsync(match, model, communityContext);
            
            if (predictionMetadata == null || !predictionMetadata.ContextDocumentNames.Any())
            {
                // If no context documents were used, prediction can't be outdated based on context changes
                return false;
            }
            
            if (verbose)
            {
                AnsiConsole.MarkupLine($"[dim]  Checking {predictionMetadata.ContextDocumentNames.Count} context documents for updates[/]");
            }
            
            // Check if any context document has been updated after the prediction was created
            foreach (var documentName in predictionMetadata.ContextDocumentNames)
            {
                // Strip any display suffix (e.g., " (kpi-context)") from the context document name
                // to get the actual document name stored in the repository
                var actualDocumentName = StripDisplaySuffix(documentName);
                
                // Skip bundesliga-standings.csv from outdated check to reduce unnecessary repredictions
                if (actualDocumentName.Equals("bundesliga-standings.csv", StringComparison.OrdinalIgnoreCase))
                {
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]  Skipping outdated check for '{actualDocumentName}' (excluded from cost optimization)[/]");
                    }
                    continue;
                }
                
                var latestContextDocument = await contextRepository.GetLatestContextDocumentAsync(actualDocumentName, communityContext);
                
                if (latestContextDocument != null && latestContextDocument.CreatedAt > predictionMetadata.CreatedAt)
                {
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]  Context document '{actualDocumentName}' (stored as '{documentName}') updated after prediction (document: {latestContextDocument.CreatedAt}, prediction: {predictionMetadata.CreatedAt})[/]");
                    }
                    return true; // Prediction is outdated
                }
                else if (verbose && latestContextDocument == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]  Warning: Context document '{actualDocumentName}' not found in repository[/]");
                }
            }
            
            return false; // Prediction is up-to-date
        }
        catch (Exception ex)
        {
            // Log error but don't fail verification due to outdated check issues
            if (verbose)
            {
                AnsiConsole.MarkupLine($"[yellow]  Warning: Failed to check outdated status: {ex.Message}[/]");
            }
            return false;
        }
    }
    
    /// <summary>
    /// Strips display suffixes like " (kpi-context)" from context document names
    /// to get the actual document name used in the repository.
    /// </summary>
    /// <param name="displayName">The display name that may contain a suffix</param>
    /// <returns>The actual document name without any display suffix</returns>
    private static string StripDisplaySuffix(string displayName)
    {
        // Look for patterns like " (some-text)" at the end and remove them
        var lastParenIndex = displayName.LastIndexOf(" (");
        if (lastParenIndex > 0 && displayName.EndsWith(")"))
        {
            return displayName.Substring(0, lastParenIndex);
        }
        return displayName;
    }
}
