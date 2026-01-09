using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using OpenAiIntegration;
using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.Matchday;

public class MatchdayCommand : AsyncCommand<BaseSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly IContextProviderFactory _contextProviderFactory;

    public MatchdayCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _contextProviderFactory = contextProviderFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<MatchdayCommand>();
        
        try
        {
            _console.MarkupLine($"[green]Matchday command initialized with model:[/] [yellow]{settings.Model}[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.OverrideKicktipp)
            {
                _console.MarkupLine("[yellow]Override mode enabled - will override existing Kicktipp predictions[/]");
            }
            
            if (settings.OverrideDatabase)
            {
                _console.MarkupLine("[yellow]Override database mode enabled - will override existing database predictions[/]");
            }
            
            if (settings.Agent)
            {
                _console.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }
            
            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database or Kicktipp[/]");
            }

            if (!string.IsNullOrEmpty(settings.EstimatedCostsModel))
            {
                _console.MarkupLine($"[cyan]Estimated costs will be calculated for model:[/] [yellow]{settings.EstimatedCostsModel}[/]");
            }

            if (settings.WithJustification)
            {
                if (settings.Agent)
                {
                    _console.MarkupLine("[red]Error:[/] --with-justification cannot be used with --agent");
                    return 1;
                }

                _console.MarkupLine("[green]Justification output enabled - model reasoning will be captured[/]");
            }

            // Validate reprediction settings
            if (settings.OverrideDatabase && settings.IsRepredictMode)
            {
                _console.MarkupLine($"[red]Error:[/] --override-database cannot be used with reprediction flags (--repredict or --max-repredictions)");
                return 1;
            }

            if (settings.MaxRepredictions.HasValue && settings.MaxRepredictions.Value < 0)
            {
                _console.MarkupLine($"[red]Error:[/] --max-repredictions must be 0 or greater");
                return 1;
            }

            if (settings.IsRepredictMode)
            {
                var maxValue = settings.MaxRepredictions ?? int.MaxValue;
                _console.MarkupLine($"[yellow]Reprediction mode enabled - max repredictions: {(settings.MaxRepredictions?.ToString() ?? "unlimited")}[/]");
            }
            
            // Execute the matchday workflow
            await ExecuteMatchdayWorkflow(settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing matchday command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private async Task ExecuteMatchdayWorkflow(BaseSettings settings, ILogger logger)
    {
        // Create services using factories
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var predictionService = _openAiServiceFactory.CreatePredictionService(settings.Model);
        
        // Create context provider using factory
        string communityContext = settings.CommunityContext ?? settings.Community;
        var contextProvider = _contextProviderFactory.CreateKicktippContextProvider(
            kicktippClient, settings.Community, communityContext);
        
        var tokenUsageTracker = _openAiServiceFactory.GetTokenUsageTracker();
        
        // Log the prompt paths being used
        if (settings.Verbose)
        {
            _console.MarkupLine($"[dim]Match prompt:[/] [blue]{predictionService.GetMatchPromptPath(settings.WithJustification)}[/]");
        }
        
        // Create repositories
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
        var contextRepository = _firebaseServiceFactory.CreateContextRepository();
        var databaseEnabled = true;
        
        // Reset token usage tracker for this workflow
        tokenUsageTracker.Reset();
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine("[blue]Getting current matchday matches...[/]");
        
        // Step 1: Get current matchday via GetMatchesWithHistoryAsync
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.Community);
        
        if (!matchesWithHistory.Any())
        {
            _console.MarkupLine("[yellow]No matches found for current matchday[/]");
            return;
        }
        
        _console.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");
        
        if (databaseEnabled)
        {
            _console.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }
        
        var predictions = new Dictionary<Match, BetPrediction>();
        
        // Step 2: For each match, check database first, then predict if needed
        foreach (var matchWithHistory in matchesWithHistory)
        {
            var match = matchWithHistory.Match;
            _console.MarkupLine($"[cyan]Processing:[/] {match.HomeTeam} vs {match.AwayTeam}");
            
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
                            _console.MarkupLine($"[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[green]  ✓ Found existing prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](from database)[/]");
                            WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
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
                        _console.MarkupLine($"[yellow]  → No existing prediction found, creating first prediction...[/]");
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
                                _console.MarkupLine($"[yellow]  → Creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed}) - prediction is outdated[/]");
                            }
                            else
                            {
                                _console.MarkupLine($"[green]  ✓ Skipped reprediction - current prediction is up-to-date[/]");
                                
                                // Get the latest prediction for display purposes
                                prediction = await predictionRepository!.GetPredictionAsync(match, settings.Model, communityContext);
                                if (prediction != null)
                                {
                                    fromDatabase = true;
                                    if (!settings.Agent)
                                    {
                                        _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                        WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _console.MarkupLine($"[yellow]  ✗ Skipped - already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                            
                            // Get the latest prediction for display purposes
                            prediction = await predictionRepository!.GetPredictionAsync(match, settings.Model, communityContext);
                            if (prediction != null)
                            {
                                fromDatabase = true;
                                if (!settings.Agent)
                                {
                                    _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                    WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
                                }
                            }
                        }
                    }
                }
                
                // If no existing prediction (normal mode) or we need to predict (reprediction mode), generate a new one
                if (prediction == null || shouldPredict)
                {
                    _console.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
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
                        _console.MarkupLine($"[dim]    Using {contextDocuments.Count} context documents[/]");
                    }
                    
                    // Show context documents if requested
                    if (settings.ShowContextDocuments)
                    {
                        _console.MarkupLine($"[cyan]    Context documents for {match.HomeTeam} vs {match.AwayTeam}:[/]");
                        foreach (var doc in contextDocuments)
                        {
                            _console.MarkupLine($"[dim]    📄 {doc.Name}[/]");
                            
                            // Show first few lines and total line count for readability
                            var lines = doc.Content.Split('\n');
                            var previewLines = lines.Take(10).ToArray();
                            var hasMore = lines.Length > 10;
                            
                            foreach (var line in previewLines)
                            {
                                _console.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }
                            
                            if (hasMore)
                            {
                                _console.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }
                            
                            _console.MarkupLine($"[dim]      (Total: {lines.Length} lines, {doc.Content.Length} characters)[/]");
                            _console.WriteLine();
                        }
                    }
                    
                    // Predict the match
                    prediction = await predictionService.PredictMatchAsync(match, contextDocuments, settings.WithJustification);
                    
                    if (prediction != null)
                    {
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[green]  ✓ Generated prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals}");
                            WriteJustificationIfNeeded(prediction, settings.WithJustification);
                        }
                        
                        // Save to database immediately if enabled
                        if (databaseEnabled && !settings.DryRun)
                        {
                            try
                            {
                                // Get token usage and cost information
                                var cost = (double)tokenUsageTracker.GetLastCost(); // Get the cost for this individual match
                                // Use the new GetLastUsageJson method to get full JSON
                                var tokenUsageJson = tokenUsageTracker.GetLastUsageJson() ?? "{}";
                                
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
                                        _console.MarkupLine($"[dim]    ✓ Saved as reprediction {nextIndex} to database[/]");
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
                                        contextDocuments.Select(d => d.Name),
                                        overrideCreatedAt: settings.OverrideDatabase);
                                        
                                    if (settings.Verbose)
                                    {
                                        _console.MarkupLine($"[dim]    ✓ Saved to database[/]");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to save prediction for match {Match}", match);
                                _console.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            _console.MarkupLine($"[dim]    (Dry run - skipped database save)[/]");
                        }
                        
                        // Show individual match token usage in verbose mode
                        if (settings.Verbose)
                        {
                            var matchUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            _console.MarkupLine($"[dim]    Token usage: {matchUsage}[/]");
                        }
                    }
                    else
                    {
                        _console.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }
                
                // Convert to BetPrediction for Kicktipp
                var betPrediction = new BetPrediction(prediction.HomeGoals, prediction.AwayGoals);
                predictions[match] = betPrediction;
                
                if (!fromDatabase && settings.Verbose)
                {
                    _console.MarkupLine($"[dim]    Already saved to database[/]");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing match {Match}", match);
                _console.MarkupLine($"[red]  ✗ Error processing match: {ex.Message}[/]");
            }
        }
        
        if (!predictions.Any())
        {
            _console.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            return;
        }
        
        // Step 4: Place all predictions using PlaceBetsAsync
        _console.MarkupLine($"[blue]Placing {predictions.Count} predictions to Kicktipp...[/]");
        
        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} predictions (no actual changes made)[/]");
        }
        else
        {
            var success = await kicktippClient.PlaceBetsAsync(settings.Community, predictions, overrideBets: settings.OverrideKicktipp);
            
            if (success)
            {
                _console.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} predictions![/]");
            }
            else
            {
                _console.MarkupLine("[red]✗ Failed to place some or all predictions[/]");
            }
        }
        
        // Display token usage summary
        var summary = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
            ? tokenUsageTracker.GetCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
            : tokenUsageTracker.GetCompactSummary();
        _console.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");
    }
    
    /// <summary>
    /// Retrieves all available context documents from the database for the given community context.
    /// </summary>
    private async Task<Dictionary<string, DocumentContext>> GetMatchContextDocumentsAsync(
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
            _console.MarkupLine($"[dim]    Looking for {requiredDocuments.Length} specific context documents in database[/]");
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
                        _console.MarkupLine($"[dim]      ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                    }
                }
                else
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]      ✗ Missing {documentName}[/]");
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
                            _console.MarkupLine($"[dim]      ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                        }
                    }
                    else if (verbose)
                    {
                        _console.MarkupLine($"[dim]      · Missing optional {documentName}[/]");
                    }
                }
                catch (Exception optEx)
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]      · Failed optional {documentName}: {optEx.Message}[/]");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]    Warning: Failed to retrieve context from database: {ex.Message}[/]");
        }
        
        return contextDocuments;
    }
    
    /// <summary>
    /// Gets context documents using database first, falling back to on-demand context provider if needed.
    /// </summary>
    private async Task<List<DocumentContext>> GetHybridContextAsync(
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
                _console.MarkupLine($"[green]    Using {databaseContexts.Count} context documents from database (all required present)[/]");
            }
            contextDocuments.AddRange(databaseContexts.Values);
        }
        else
        {
            // Fallback: use on-demand provider but still include any database docs we already have (including optional transfers)
            _console.MarkupLine($"[yellow]    Warning: Only found {requiredPresent}/{requiredTotal} required context documents in database (have {databaseContexts.Count} total incl. optional). Falling back to on-demand context while preserving retrieved documents[/]");

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
                _console.MarkupLine($"[yellow]    Using {contextDocuments.Count} merged context documents (database + on-demand) [/]");
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
    
    private async Task<bool> CheckPredictionOutdated(IPredictionRepository predictionRepository, IContextRepository contextRepository, Match match, string model, string communityContext, bool verbose)
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
                _console.MarkupLine($"[dim]  Checking {predictionMetadata.ContextDocumentNames.Count} context documents for updates[/]");
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
                        _console.MarkupLine($"[dim]  Skipping outdated check for '{actualDocumentName}' (excluded from cost optimization)[/]");
                    }
                    continue;
                }
                
                var latestContextDocument = await contextRepository.GetLatestContextDocumentAsync(actualDocumentName, communityContext);
                
                if (latestContextDocument != null && latestContextDocument.CreatedAt > predictionMetadata.CreatedAt)
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]  Context document '{actualDocumentName}' (stored as '{documentName}') updated after prediction (document: {latestContextDocument.CreatedAt}, prediction: {predictionMetadata.CreatedAt})[/]");
                    }
                    return true; // Prediction is outdated
                }
                else if (verbose && latestContextDocument == null)
                {
                    _console.MarkupLine($"[yellow]  Warning: Context document '{actualDocumentName}' not found in repository[/]");
                }
            }
            
            return false; // Prediction is up-to-date
        }
        catch (Exception ex)
        {
            // Log error but don't fail verification due to outdated check issues
            if (verbose)
            {
                _console.MarkupLine($"[yellow]  Warning: Failed to check outdated status: {ex.Message}[/]");
            }
            return false;
        }
    }
    
    private void WriteJustificationIfNeeded(Prediction? prediction, bool includeJustification, bool fromDatabase = false)
    {
        if (!includeJustification || prediction == null)
        {
            return;
        }

        var sourceLabel = fromDatabase ? "stored prediction" : "model response";

        var justificationWriter = new JustificationConsoleWriter(_console);
        justificationWriter.WriteJustification(
            prediction.Justification,
            "[dim]    ↳ Justification:[/]",
            "        ",
            $"[yellow]    ↳ No justification available for this {sourceLabel}[/]");
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
