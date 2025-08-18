using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Core;
using KicktippIntegration;
using FirebaseAdapter;

namespace Orchestrator.Commands;

public class VerifyBonusCommand : AsyncCommand<VerifySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VerifySettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<VerifyBonusCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            AnsiConsole.MarkupLine($"[green]Verify bonus command initialized[/]");
            
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
                AnsiConsole.MarkupLine("[cyan]Init bonus mode enabled - will return error if no predictions exist[/]");
            }
            
            if (settings.CheckOutdated)
            {
                AnsiConsole.MarkupLine("[cyan]Outdated check enabled - predictions will be checked against latest context documents[/]");
            }
            
            // Execute the verification workflow
            var hasDiscrepancies = await ExecuteVerificationWorkflow(serviceProvider, settings, logger);
            
            return hasDiscrepancies ? 1 : 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing verify bonus command");
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
        
        // Get KPI repository for outdated checks (required for bonus predictions)
        var kpiRepository = serviceProvider.GetRequiredService<IKpiRepository>();
        
        // Determine community context (use explicit setting or fall back to community name)
        string communityContext = settings.CommunityContext ?? settings.Community;
        
        AnsiConsole.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        AnsiConsole.MarkupLine("[blue]Getting open bonus questions from Kicktipp...[/]");
        
        // Step 1: Get open bonus questions from Kicktipp
        var bonusQuestions = await kicktippClient.GetOpenBonusQuestionsAsync(settings.Community);
        
        if (!bonusQuestions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No bonus questions found on Kicktipp[/]");
            return false;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {bonusQuestions.Count} bonus questions on Kicktipp[/]");
        
        AnsiConsole.MarkupLine("[blue]Getting placed bonus predictions from Kicktipp...[/]");
        
        // Step 1.5: Get currently placed predictions from Kicktipp
        var placedPredictions = await kicktippClient.GetPlacedBonusPredictionsAsync(settings.Community);
        
        AnsiConsole.MarkupLine("[blue]Retrieving predictions from database...[/]");
        
        var hasDiscrepancies = false;
        var totalQuestions = 0;
        var questionsWithDatabasePredictions = 0;
        var validPredictions = 0;
        
        // Step 2: For each bonus question, check if we have a prediction in database
        foreach (var question in bonusQuestions)
        {
            totalQuestions++;
            
            try
            {
                // Get prediction from database
                if (settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]  Looking up: {Markup.Escape(question.Text)}[/]");
                }
                
                var databasePrediction = await predictionRepository.GetBonusPredictionByTextAsync(question.Text, settings.Model, communityContext);
                var kicktippPrediction = placedPredictions.GetValueOrDefault(question.FormFieldName ?? question.Text);
                
                if (databasePrediction != null)
                {
                    questionsWithDatabasePredictions++;
                    
                    // Validate the prediction against the question
                    var isValidPrediction = ValidateBonusPrediction(question, databasePrediction);
                    
                    // Compare database prediction with Kicktipp placed prediction
                    var predictionsMatch = CompareBonusPredictions(databasePrediction, kicktippPrediction);
                    
                    // Check if prediction is outdated (if enabled)
                    var isOutdated = false;
                    if (settings.CheckOutdated)
                    {
                        isOutdated = await CheckBonusPredictionOutdated(predictionRepository, kpiRepository, question.Text, settings.Model, settings.Community, settings.Verbose);
                    }
                    
                    // Consider prediction valid if it passes validation, matches Kicktipp, and is not outdated
                    var isPredictionValid = isValidPrediction && predictionsMatch && !isOutdated;
                    
                    if (isPredictionValid)
                    {
                        validPredictions++;
                        
                        if (settings.Verbose)
                        {
                            if (settings.Agent)
                            {
                                AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(question.Text)}[/] [dim](valid)[/]");
                            }
                            else
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                AnsiConsole.MarkupLine($"[green]✓ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](valid)[/]");
                            }
                        }
                    }
                    else
                    {
                        hasDiscrepancies = true;
                        
                        if (settings.Agent)
                        {
                            var status = !isValidPrediction ? "invalid prediction" : 
                                        !predictionsMatch ? "mismatch with Kicktipp" : "outdated";
                            AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}[/] [dim]({status})[/]");
                        }
                        else
                        {
                            if (!isValidPrediction)
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](invalid prediction)[/]");
                            }
                            else if (!predictionsMatch)
                            {
                                // Show mismatch details
                                var databaseTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                var kicktippTexts = kicktippPrediction != null 
                                    ? question.Options
                                        .Where(o => kicktippPrediction.SelectedOptionIds.Contains(o.Id))
                                        .Select(o => o.Text)
                                    : new List<string>();
                                
                                AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/]");
                                AnsiConsole.MarkupLine($"  [yellow]Database:[/] {string.Join(", ", databaseTexts)}");
                                AnsiConsole.MarkupLine($"  [yellow]Kicktipp:[/] {(kicktippTexts.Any() ? string.Join(", ", kicktippTexts) : "no prediction")}");
                            }
                            else if (isOutdated)
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](outdated)[/]");
                                AnsiConsole.MarkupLine($"  [yellow]Status:[/] Outdated (context updated after prediction)");
                            }
                        }
                    }
                }
                else
                {
                    hasDiscrepancies = true;
                    
                    if (settings.Verbose)
                    {
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[yellow]○ {Markup.Escape(question.Text)}[/] [dim](no prediction)[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]○ {Markup.Escape(question.Text)}:[/] [dim](no prediction)[/]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                hasDiscrepancies = true;
                logger.LogError(ex, "Error verifying bonus prediction for question '{QuestionText}'", question.Text);
                
                if (settings.Agent)
                {
                    AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}[/] [dim](error)[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] Error during verification");
                }
            }
        }
        
        // Step 3: Display summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Verification Summary:[/]");
        AnsiConsole.MarkupLine($"  Total bonus questions: {totalQuestions}");
        AnsiConsole.MarkupLine($"  Questions with database predictions: {questionsWithDatabasePredictions}");
        AnsiConsole.MarkupLine($"  Valid predictions: {validPredictions}");
        
        // Check for init-bonus mode first
        if (settings.InitMatchday && questionsWithDatabasePredictions == 0)
        {
            AnsiConsole.MarkupLine("[yellow]  Init bonus detected - no database predictions exist[/]");
            AnsiConsole.MarkupLine("[red]Returning error to trigger initial prediction workflow[/]");
            return true; // Return error to trigger workflow
        }
        
        if (hasDiscrepancies)
        {
            AnsiConsole.MarkupLine($"[red]  Missing or invalid predictions: {totalQuestions - validPredictions}[/]");
            AnsiConsole.MarkupLine("[red]Verification failed - some predictions are missing or invalid[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]  All predictions are valid - verification successful[/]");
        }
        
        return hasDiscrepancies;
    }
    
    private static bool ValidateBonusPrediction(BonusQuestion question, BonusPrediction prediction)
    {
        // Check if all selected option IDs exist in the question
        var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
        var allOptionsValid = prediction.SelectedOptionIds.All(id => validOptionIds.Contains(id));
        
        if (!allOptionsValid)
        {
            return false;
        }
        
        // Check if the number of selections is valid
        var selectionCount = prediction.SelectedOptionIds.Count;
        if (selectionCount < 1 || selectionCount > question.MaxSelections)
        {
            return false;
        }
        
        // Check for duplicates
        var uniqueSelections = prediction.SelectedOptionIds.Distinct().Count();
        if (uniqueSelections != selectionCount)
        {
            return false;
        }
        
        return true;
    }
    
    private static async Task<bool> CheckBonusPredictionOutdated(
        IPredictionRepository predictionRepository,
        IKpiRepository kpiRepository,
        string questionText,
        string model,
        string communityContext,
        bool verbose)
    {
        try
        {
            // Get prediction metadata (includes creation timestamp and context document names)
            var predictionMetadata = await predictionRepository.GetBonusPredictionMetadataByTextAsync(
                questionText, model, communityContext);
            
            if (predictionMetadata == null)
            {
                // No metadata found, assume not outdated
                return false;
            }
            
            // Check if any KPI document has been updated after the prediction was created
            foreach (var contextDocumentName in predictionMetadata.ContextDocumentNames)
            {
                var kpiDocument = await kpiRepository.GetKpiDocumentAsync(contextDocumentName, communityContext);
                if (kpiDocument != null)
                {
                    // Compare the creation timestamps
                    // Note: We need to be careful about timezone handling here
                    // Both timestamps should be in UTC for proper comparison
                    if (kpiDocument.CreatedAt > predictionMetadata.CreatedAt)
                    {
                        if (verbose)
                        {
                            AnsiConsole.MarkupLine($"[yellow]KPI document '{contextDocumentName}' updated after prediction was created[/]");
                            AnsiConsole.MarkupLine($"  [dim]Prediction created:[/] {predictionMetadata.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                            AnsiConsole.MarkupLine($"  [dim]KPI document created:[/] {kpiDocument.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        return true; // Prediction is outdated
                    }
                    
                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]KPI document '{contextDocumentName}' found, version {kpiDocument.Version} is latest[/]");
                    }
                }
                else if (verbose)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: KPI document '{contextDocumentName}' not found[/]");
                }
            }
            
            return false; // No KPI documents are newer than the prediction
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not check if prediction is outdated: {ex.Message}[/]");
            }
            return false; // Assume not outdated if we can't determine
        }
    }
    
    private static bool CompareBonusPredictions(BonusPrediction? databasePrediction, BonusPrediction? kicktippPrediction)
    {
        // Both null - match
        if (databasePrediction == null && kicktippPrediction == null)
        {
            return true;
        }
        
        // One null, other not - mismatch
        if (databasePrediction == null || kicktippPrediction == null)
        {
            return false;
        }
        
        // Both have values - compare selected option IDs
        var databaseOptions = databasePrediction.SelectedOptionIds.OrderBy(x => x).ToList();
        var kicktippOptions = kicktippPrediction.SelectedOptionIds.OrderBy(x => x).ToList();
        
        return databaseOptions.SequenceEqual(kicktippOptions);
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
