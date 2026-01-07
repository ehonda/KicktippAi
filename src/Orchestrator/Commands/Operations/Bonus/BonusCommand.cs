using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using OpenAiIntegration;
using FirebaseAdapter;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Shared;

namespace Orchestrator.Commands.Operations.Bonus;

public class BonusCommand : AsyncCommand<BaseSettings>
{
    private readonly IAnsiConsole _console;

    public BonusCommand(IAnsiConsole console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<BonusCommand>();
        
        try
        {
            // Load environment variables
            EnvironmentHelper.LoadEnvironmentVariables(logger);
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services, settings, logger);
            var serviceProvider = services.BuildServiceProvider();
            
            _console.MarkupLine($"[green]Bonus command initialized with model:[/] [yellow]{settings.Model}[/]");
            
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
            
            // Execute the bonus prediction workflow
            await ExecuteBonusWorkflow(serviceProvider, settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing bonus command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private async Task ExecuteBonusWorkflow(IServiceProvider serviceProvider, BaseSettings settings, ILogger logger)
    {
        var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
        var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
        
        // Log the prompt paths being used
        if (settings.Verbose)
        {
            _console.MarkupLine($"[dim]Bonus prompt:[/] [blue]{predictionService.GetBonusPromptPath()}[/]");
        }
        
        // Use Firebase KPI Context Provider for bonus predictions
        var kpiContextProvider = serviceProvider.GetRequiredService<FirebaseKpiContextProvider>();
        
        var tokenUsageTracker = serviceProvider.GetService<ITokenUsageTracker>();
        
        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = serviceProvider.GetService<IPredictionRepository>();
        var databaseEnabled = predictionRepository != null;
        
        // Reset token usage tracker for this workflow
        tokenUsageTracker?.Reset();
        
        // Determine community context (use explicit setting or fall back to community name)
        string communityContext = settings.CommunityContext ?? settings.Community;
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine("[blue]Getting open bonus questions from Kicktipp...[/]");
        
        // Step 1: Get open bonus questions from Kicktipp
        var bonusQuestions = await kicktippClient.GetOpenBonusQuestionsAsync(settings.Community);
        
        if (!bonusQuestions.Any())
        {
            _console.MarkupLine("[yellow]No open bonus questions found[/]");
            return;
        }
        
        _console.MarkupLine($"[green]Found {bonusQuestions.Count} open bonus questions[/]");
        
        if (databaseEnabled)
        {
            _console.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }
        
        var predictions = new Dictionary<string, BonusPrediction>();
        
        // Step 2: For each question, check database first, then predict if needed
        foreach (var question in bonusQuestions)
        {
            _console.MarkupLine($"[cyan]Processing:[/] {Markup.Escape(question.Text)}");
            
            try
            {
                BonusPrediction? prediction = null;
                bool fromDatabase = false;
                bool shouldPredict = false;
                
                // Check if we have an existing prediction in the database
                if (databaseEnabled && !settings.OverrideDatabase && !settings.IsRepredictMode)
                {
                    // Look for prediction by question text, model, and community context
                    prediction = await predictionRepository!.GetBonusPredictionByTextAsync(question.Text, settings.Model, communityContext);
                    if (prediction != null)
                    {
                        fromDatabase = true;
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            _console.MarkupLine($"[green]  ✓ Found existing prediction:[/] {string.Join(", ", optionTexts)} [dim](from database)[/]");
                        }
                    }
                }
                
                // Handle reprediction logic
                if (settings.IsRepredictMode && databaseEnabled)
                {
                    var currentRepredictionIndex = await predictionRepository!.GetBonusRepredictionIndexAsync(question.Text, settings.Model, communityContext);
                    
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
                            shouldPredict = true;
                            _console.MarkupLine($"[yellow]  → Creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed})...[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[yellow]  ✗ Skipped - already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                            
                            // Get the latest prediction for display purposes
                            prediction = await predictionRepository!.GetBonusPredictionByTextAsync(question.Text, settings.Model, communityContext);
                            if (prediction != null)
                            {
                                fromDatabase = true;
                                if (!settings.Agent)
                                {
                                    var optionTexts = question.Options
                                        .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                        .Select(o => o.Text);
                                    _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {string.Join(", ", optionTexts)} [dim](reprediction {currentRepredictionIndex})[/]");
                                }
                            }
                        }
                    }
                }
                
                // If no existing prediction (normal mode) or we need to predict (reprediction mode), generate a new one
                if (prediction == null || shouldPredict)
                {
                    _console.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
                    // Step 3: Get KPI context for bonus predictions
                    var contextDocuments = new List<DocumentContext>();
                    
                    // Use KPI documents as context for bonus predictions (targeted by question content)
                    await foreach (var context in kpiContextProvider.GetBonusQuestionContextAsync(question.Text, communityContext))
                    {
                        contextDocuments.Add(context);
                    }
                    
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]    Using {contextDocuments.Count} KPI context documents[/]");
                    }
                    
                    // Predict the bonus question
                    prediction = await predictionService.PredictBonusQuestionAsync(question, contextDocuments);
                    
                    if (prediction != null)
                    {
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            _console.MarkupLine($"[green]  ✓ Generated prediction:[/] {string.Join(", ", optionTexts)}");
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
                                    cost = (double)tokenUsageTracker.GetLastCost(); // Get the cost for this individual question
                                    // Use the new GetLastUsageJson method to get full JSON
                                    tokenUsageJson = tokenUsageTracker.GetLastUsageJson() ?? "{}";
                                }
                                
                                if (settings.IsRepredictMode)
                                {
                                    // Save as reprediction with specific index
                                    var currentIndex = await predictionRepository!.GetBonusRepredictionIndexAsync(question.Text, settings.Model, communityContext);
                                    var nextIndex = currentIndex == -1 ? 0 : currentIndex + 1;
                                    
                                    await predictionRepository!.SaveBonusRepredictionAsync(
                                        question, 
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
                                    await predictionRepository!.SaveBonusPredictionAsync(
                                        question, 
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
                                logger.LogError(ex, "Failed to save bonus prediction for question '{QuestionText}'", question.Text);
                                _console.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            _console.MarkupLine($"[dim]    (Dry run - skipped database save)[/]");
                        }
                        
                        // Show individual question token usage in verbose mode
                        if (settings.Verbose && tokenUsageTracker != null)
                        {
                            var questionUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            _console.MarkupLine($"[dim]    Token usage: {questionUsage}[/]");
                        }
                    }
                    else
                    {
                        _console.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }
                
                predictions[question.FormFieldName ?? question.Text] = prediction;
                
                if (!fromDatabase && settings.Verbose)
                {
                    _console.MarkupLine($"[dim]    Ready for Kicktipp placement[/]");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing bonus question '{QuestionText}'", question.Text);
                _console.MarkupLine($"[red]  ✗ Error processing question: {ex.Message}[/]");
            }
        }
        
        if (!predictions.Any())
        {
            _console.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            return;
        }
        
        // Step 4: Place all predictions using PlaceBonusPredictionsAsync
        _console.MarkupLine($"[blue]Placing {predictions.Count} bonus predictions to Kicktipp...[/]");
        
        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} bonus predictions (no actual changes made)[/]");
        }
        else
        {
            var success = await kicktippClient.PlaceBonusPredictionsAsync(settings.Community, predictions, overridePredictions: settings.OverrideKicktipp);
            
            if (success)
            {
                _console.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} bonus predictions![/]");
            }
            else
            {
                _console.MarkupLine("[red]✗ Failed to place some or all bonus predictions[/]");
            }
        }
        
        // Display token usage summary
        if (tokenUsageTracker != null)
        {
            var summary = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                ? tokenUsageTracker.GetCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                : tokenUsageTracker.GetCompactSummary();
            _console.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");
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
        
        // Add Firebase database (required for KPI context provider)
        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
        
        if (string.IsNullOrEmpty(firebaseProjectId) || string.IsNullOrEmpty(firebaseServiceAccountJson))
        {
            throw new InvalidOperationException("Firebase credentials are required for bonus predictions. Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables.");
        }
        
        services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, settings.Community);
        logger.LogInformation("Firebase database integration enabled for project: {ProjectId}, community: {Community}", firebaseProjectId, settings.Community);
        logger.LogInformation("KPI Context Provider enabled for bonus predictions");
    }
}
