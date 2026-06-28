using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
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

    protected override async Task<int> ExecuteAsync(CommandContext context, VerifySettings settings, CancellationToken cancellationToken)
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
        string communityContext = settings.CommunityContext ?? settings.Community;
        var competition = CompetitionResolver.ResolveCompetition(settings.Competition, settings.Community, communityContext);
        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(settings.Model, settings.ReasoningEffort);
        var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);

        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository(repositoryCompetition);
        if (predictionRepository == null)
        {
            _console.MarkupLine("[red]Error: Database not configured. Cannot verify predictions without database access.[/]");
            _console.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        // Get context repository for outdated checks (may be null if Firebase is not configured)
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
        if (settings.CheckOutdated && contextRepository == null)
        {
            _console.MarkupLine("[red]Error: Database not configured. Cannot check outdated predictions without database access.[/]");
            _console.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
        _console.MarkupLine($"[blue]Using model config:[/] [yellow]{modelConfig.DisplayName}[/]");
        _console.MarkupLine("[blue]Getting placed predictions from Kicktipp...[/]");
        
        // Step 1: Get placed predictions from Kicktipp
        var placedPredictions = await kicktippClient.GetPlacedPredictionsAsync(settings.Community, competition);
        
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
            
            try
            {
                Prediction? databasePrediction;
                
                // For cancelled matches, use team-names-only lookup to handle startsAt inconsistencies
                // See IPredictionRepository.cs for detailed documentation on this edge case
                if (match.IsCancelled)
                {
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Looking up (cancelled match, team-names-only): {match.HomeTeam} vs {match.AwayTeam}[/]");
                    }
                    databasePrediction = await predictionRepository.GetCancelledMatchPredictionAsync(
                        match.HomeTeam, match.AwayTeam, modelConfig, communityContext);
                }
                else
                {
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]  Looking up: {match.HomeTeam} vs {match.AwayTeam} at {match.StartsAt}[/]");
                    }
                    databasePrediction = await predictionRepository.GetPredictionAsync(match, modelConfig, communityContext);
                }
                
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
                    isOutdated = await CheckPredictionOutdated(predictionRepository, contextRepository, match, modelConfig, communityContext, competition, settings.Verbose);
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
    
    private async Task<bool> CheckPredictionOutdated(IPredictionRepository predictionRepository, IContextRepository contextRepository, Match match, PredictionModelConfig modelConfig, string communityContext, string competition, bool verbose)
    {
        try
        {
            // Get prediction metadata with context document names and timestamps
            // For cancelled matches, use team-names-only lookup to handle startsAt inconsistencies
            PredictionMetadata? predictionMetadata;
            if (match.IsCancelled)
            {
                predictionMetadata = await predictionRepository.GetCancelledMatchPredictionMetadataAsync(
                    match.HomeTeam, match.AwayTeam, modelConfig, communityContext);
            }
            else
            {
                predictionMetadata = await predictionRepository.GetPredictionMetadataAsync(match, modelConfig, communityContext);
            }
            
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
                
                var standingsDocumentName = MatchContextDocumentCatalog.GetStandingsDocumentName(competition);
                if (actualDocumentName.Equals(standingsDocumentName, StringComparison.OrdinalIgnoreCase))
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
                    var predictionTimeContextDocument = await contextRepository.GetContextDocumentByTimestampAsync(
                        actualDocumentName,
                        predictionMetadata.CreatedAt,
                        communityContext);

                    if (predictionTimeContextDocument != null &&
                        string.Equals(
                            predictionTimeContextDocument.Content,
                            latestContextDocument.Content,
                            StringComparison.Ordinal))
                    {
                        if (verbose)
                        {
                            _console.MarkupLine(
                                $"[dim]  Context document '{actualDocumentName}' has newer versions after the prediction, but the latest content matches the prediction-time version[/]");
                        }

                        continue;
                    }

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
