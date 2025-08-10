using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Core;
using KicktippIntegration;
using OpenAiIntegration;
using FirebaseAdapter;

namespace Orchestrator.Commands;

public class BonusCommand : AsyncCommand<BaseSettings>
{
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
            
            AnsiConsole.MarkupLine($"[green]Bonus command initialized with model:[/] [yellow]{settings.Model}[/]");
            
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
            
            // Execute the bonus prediction workflow
            await ExecuteBonusWorkflow(serviceProvider, settings, logger);
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing bonus command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private static async Task ExecuteBonusWorkflow(IServiceProvider serviceProvider, BaseSettings settings, ILogger logger)
    {
        var kicktippClient = serviceProvider.GetRequiredService<IKicktippClient>();
        var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
        
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
        
        AnsiConsole.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        AnsiConsole.MarkupLine("[blue]Getting open bonus questions from Kicktipp...[/]");
        
        // Step 1: Get open bonus questions from Kicktipp
        var bonusQuestions = await kicktippClient.GetOpenBonusQuestionsAsync(settings.Community);
        
        if (!bonusQuestions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No open bonus questions found[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]Found {bonusQuestions.Count} open bonus questions[/]");
        
        if (databaseEnabled)
        {
            AnsiConsole.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }
        
        var predictions = new Dictionary<string, BonusPrediction>();
        
        // Step 2: For each question, check database first, then predict if needed
        foreach (var question in bonusQuestions)
        {
            AnsiConsole.MarkupLine($"[cyan]Processing:[/] {Markup.Escape(question.Text)}");
            
            try
            {
                BonusPrediction? prediction = null;
                bool fromDatabase = false;
                
                // Check if we have an existing prediction in the database
                if (databaseEnabled && !settings.OverrideDatabase)
                {
                    // Look for prediction by question text, model, and community context
                    prediction = await predictionRepository!.GetBonusPredictionByTextAsync(question.Text, settings.Model, communityContext);
                    if (prediction != null)
                    {
                        fromDatabase = true;
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            AnsiConsole.MarkupLine($"[green]  ✓ Found existing prediction:[/] {string.Join(", ", optionTexts)} [dim](from database)[/]");
                        }
                    }
                }
                
                // If no existing prediction, generate a new one
                if (prediction == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
                    // Step 3: Get KPI context for bonus predictions
                    var contextDocuments = new List<DocumentContext>();
                    
                    // Use KPI documents as context for bonus predictions (targeted by question content)
                    await foreach (var context in kpiContextProvider.GetBonusQuestionContextAsync(question.Text, communityContext))
                    {
                        contextDocuments.Add(context);
                    }
                    
                    if (settings.Verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]    Using {contextDocuments.Count} KPI context documents[/]");
                    }
                    
                    // Predict the bonus question
                    prediction = await predictionService.PredictBonusQuestionAsync(question, contextDocuments);
                    
                    if (prediction != null)
                    {
                        if (settings.Agent)
                        {
                            AnsiConsole.MarkupLine($"[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            AnsiConsole.MarkupLine($"[green]  ✓ Generated prediction:[/] {string.Join(", ", optionTexts)}");
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
                                
                                await predictionRepository!.SaveBonusPredictionAsync(
                                    question, 
                                    prediction, 
                                    settings.Model, 
                                    tokenUsageJson, 
                                    cost, 
                                    communityContext);
                                    
                                if (settings.Verbose)
                                {
                                    AnsiConsole.MarkupLine($"[dim]    ✓ Saved to database[/]");
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to save bonus prediction for question '{QuestionText}'", question.Text);
                                AnsiConsole.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]    (Dry run - skipped database save)[/]");
                        }
                        
                        // Show individual question token usage in verbose mode
                        if (settings.Verbose && tokenUsageTracker != null)
                        {
                            var questionUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            AnsiConsole.MarkupLine($"[dim]    Token usage: {questionUsage}[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }
                
                predictions[question.FormFieldName ?? question.Text] = prediction;
                
                if (!fromDatabase && settings.Verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]    Ready for Kicktipp placement[/]");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing bonus question '{QuestionText}'", question.Text);
                AnsiConsole.MarkupLine($"[red]  ✗ Error processing question: {ex.Message}[/]");
            }
        }
        
        if (!predictions.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            return;
        }
        
        // Step 4: Place all predictions using PlaceBonusPredictionsAsync
        AnsiConsole.MarkupLine($"[blue]Placing {predictions.Count} bonus predictions to Kicktipp...[/]");
        
        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} bonus predictions (no actual changes made)[/]");
        }
        else
        {
            var success = await kicktippClient.PlaceBonusPredictionsAsync(settings.Community, predictions, overridePredictions: settings.OverrideKicktipp);
            
            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} bonus predictions![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Failed to place some or all bonus predictions[/]");
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
