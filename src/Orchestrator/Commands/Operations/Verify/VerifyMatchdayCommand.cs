using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.Verify;

public class VerifyMatchdayCommand : AsyncCommand<VerifySettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ILogger<VerifyMatchdayCommand> _logger;

    public VerifyMatchdayCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        ILogger<VerifyMatchdayCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, VerifySettings settings)
    {
        
        try
        {
            _console.MarkupLine($"[green]Verify matchday command initialized[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.Agent)
            {
                _console.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }
            
            if (settings.InitMatchday)
            {
                _console.MarkupLine("[cyan]Init matchday mode enabled - will return error if no predictions exist[/]");
            }
            
            if (settings.CheckOutdated)
            {
                _console.MarkupLine("[cyan]Outdated check enabled - predictions will be checked against latest context documents[/]");
            }
            
            // Execute the verification workflow
            var hasDiscrepancies = await ExecuteVerificationWorkflow(settings);
            
            return hasDiscrepancies ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing verify matchday command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private async Task<bool> ExecuteVerificationWorkflow(VerifySettings settings)
    {
        var kicktippClient = _kicktippClientFactory.CreateClient();
        
        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
        if (predictionRepository == null)
        {
            _console.MarkupLine("[red]Error: Database not configured. Cannot verify predictions without database access.[/]");
            _console.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        // Get context repository for outdated checks (may be null if Firebase is not configured)
        var contextRepository = _firebaseServiceFactory.CreateContextRepository();
        if (settings.CheckOutdated && contextRepository == null)
        {
            _console.MarkupLine("[red]Error: Database not configured. Cannot check outdated predictions without database access.[/]");
            _console.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        // Determine community context (use explicit setting or fall back to community name)
        string communityContext = settings.CommunityContext ?? settings.Community;
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine("[blue]Getting placed predictions from Kicktipp...[/]");
        
        // Step 1: Get placed predictions from Kicktipp
        var placedPredictions = await kicktippClient.GetPlacedPredictionsAsync(settings.Community);
        
        if (!placedPredictions.Any())
        {
            _console.MarkupLine("[yellow]No matches found on Kicktipp[/]");
            return false;
        }
        
        _console.MarkupLine($"[green]Found {placedPredictions.Count} matches on Kicktipp[/]");
        
        _console.MarkupLine("[blue]Retrieving predictions from database...[/]");
        
        var hasDiscrepancies = false;
        var totalMatches = 0;
        var matchesWithPlacedPredictions = 0;
        var matchesWithDatabasePredictions = 0;
        var matchingPredictions = 0;
        
        // Step 2: For each match, compare with database predictions
        foreach (var (match, kicktippPrediction) in placedPredictions)
        {
            totalMatches++;
            
            // Log warning for cancelled matches - they have inherited times which may affect database lookup reliability
            if (match.IsCancelled)
            {
                _console.MarkupLine($"[yellow]  ⚠ {match.HomeTeam} vs {match.AwayTeam} is cancelled (Abgesagt). " +
                    $"Database lookup uses inherited time which may not match original prediction time.[/]");
            }
            
            try
            {
                // Get prediction from database
                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]  Looking up: {match.HomeTeam} vs {match.AwayTeam} at {match.StartsAt}{(match.IsCancelled ? " (CANCELLED)" : "")}[/]");
                }
                
                var databasePrediction = await predictionRepository.GetPredictionAsync(match, settings.Model, communityContext);
                
                if (kicktippPrediction != null)
                {
                    matchesWithPlacedPredictions++;
                }
                
                if (databasePrediction != null)
                {
                    matchesWithDatabasePredictions++;
                    if (settings.Verbose && !settings.Agent)
                    {
                        _console.MarkupLine($"[dim]  Found database prediction: {databasePrediction.HomeGoals}:{databasePrediction.AwayGoals}[/]");
                    }
                }
                else if (settings.Verbose && !settings.Agent)
                {
                    _console.MarkupLine($"[dim]  No database prediction found[/]");
                }
                
                // Check if prediction is outdated (if enabled and context repository is available)
                var isOutdated = false;
                if (settings.CheckOutdated && contextRepository != null && databasePrediction != null)
                {
                    isOutdated = await CheckPredictionOutdated(predictionRepository, contextRepository, match, settings.Model, communityContext, settings.Verbose);
                }
                
                // Compare predictions
                var isMatchingPrediction = ComparePredictions(kicktippPrediction, databasePrediction);
                
                // Consider prediction invalid if it's outdated or mismatched
                var isValidPrediction = isMatchingPrediction && !isOutdated;
                
                if (isValidPrediction)
                {
                    matchingPredictions++;
                    
                    if (settings.Verbose)
                    {
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]✓ {match.HomeTeam} vs {match.AwayTeam}[/] [dim](valid)[/]");
                        }
                        else
                        {
                            var predictionText = kicktippPrediction?.ToString() ?? "no prediction";
                            _console.MarkupLine($"[green]✓ {match.HomeTeam} vs {match.AwayTeam}:[/] {predictionText} [dim](valid)[/]");
                        }
                    }
                }
                else
                {
                    hasDiscrepancies = true;
                    
                    if (settings.Agent)
                    {
                        var reason = isOutdated ? "outdated" : "mismatch";
                        _console.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}[/] [dim]({reason})[/]");
                    }
                    else
                    {
                        var kicktippText = kicktippPrediction?.ToString() ?? "no prediction";
                        var databaseText = databasePrediction != null ? $"{databasePrediction.HomeGoals}:{databasePrediction.AwayGoals}" : "no prediction";
                        
                        _console.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}:[/]");
                        _console.MarkupLine($"  [yellow]Kicktipp:[/] {kicktippText}");
                        _console.MarkupLine($"  [yellow]Database:[/] {databaseText}");
                        
                        if (isOutdated)
                        {
                            _console.MarkupLine($"  [yellow]Status:[/] Outdated (context updated after prediction)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                hasDiscrepancies = true;
                _logger.LogError(ex, "Error verifying prediction for {Match}", $"{match.HomeTeam} vs {match.AwayTeam}");
                
                if (settings.Agent)
                {
                    _console.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}[/] [dim](error)[/]");
                }
                else
                {
                    _console.MarkupLine($"[red]✗ {match.HomeTeam} vs {match.AwayTeam}:[/] Error during verification");
                }
            }
        }
        
        // Step 3: Display summary
        _console.WriteLine();
        _console.MarkupLine("[bold]Verification Summary:[/]");
        _console.MarkupLine($"  Total matches: {totalMatches}");
        _console.MarkupLine($"  Matches with Kicktipp predictions: {matchesWithPlacedPredictions}");
        _console.MarkupLine($"  Matches with database predictions: {matchesWithDatabasePredictions}");
        _console.MarkupLine($"  Matching predictions: {matchingPredictions}");
        
        // Check for init-matchday mode first
        if (settings.InitMatchday && matchesWithDatabasePredictions == 0)
        {
            _console.MarkupLine("[yellow]  Init matchday detected - no database predictions exist[/]");
            _console.MarkupLine("[red]Returning error to trigger initial prediction workflow[/]");
            return true; // Return error to trigger workflow
        }
        
        if (hasDiscrepancies)
        {
            _console.MarkupLine($"[red]  Discrepancies found: {totalMatches - matchingPredictions}[/]");
            _console.MarkupLine("[red]Verification failed - predictions do not match[/]");
        }
        else
        {
            _console.MarkupLine("[green]  All predictions match - verification successful[/]");
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
