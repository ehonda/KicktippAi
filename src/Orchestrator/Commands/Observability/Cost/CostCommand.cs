using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using Google.Cloud.Firestore;
using System.Globalization;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Observability.Cost;

public class CostCommand : AsyncCommand<CostSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;

    public CostCommand(IAnsiConsole console, IFirebaseServiceFactory firebaseServiceFactory)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CostSettings settings)
    {
        var logger = LoggingConfiguration.CreateLogger<CostCommand>();
        
        try
        {
            // Load configuration from file if specified
            if (!string.IsNullOrWhiteSpace(settings.ConfigFile))
            {
                var fileConfig = await LoadConfigurationFromFile(settings.ConfigFile, logger);
                settings = MergeConfigurations(fileConfig, settings, logger);
            }
            
            // Create Firebase services using factory (factory handles env var loading)
            var firestoreDb = _firebaseServiceFactory.FirestoreDb;
            var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository();
            
            _console.MarkupLine($"[green]Cost command initialized[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.All)
            {
                _console.MarkupLine("[blue]All mode enabled - aggregating over all available data[/]");
            }
            
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
                _console.MarkupLine($"[dim]Filters:[/]");
                _console.MarkupLine($"[dim]  Matchdays: {(matchdays?.Any() == true ? string.Join(", ", matchdays) : $"all ({availableMatchdays.Count} found)")}[/]");
                _console.MarkupLine($"[dim]  Models: {(models?.Any() == true ? string.Join(", ", models) : $"all ({availableModels.Count} found)")}[/]");
                _console.MarkupLine($"[dim]  Community Contexts: {(communityContexts?.Any() == true ? string.Join(", ", communityContexts) : $"all ({availableCommunityContexts.Count} found)")}[/]");
                _console.MarkupLine($"[dim]  Include Bonus: {settings.Bonus || settings.All}[/]");
            }
            
            // Calculate costs
            var totalCost = 0.0;
            var matchPredictionCost = 0.0;
            var bonusPredictionCost = 0.0;
            var matchPredictionCount = 0;
            var bonusPredictionCount = 0;

            // Structure to store detailed breakdown data with reprediction index support
            var detailedData = new List<(string CommunityContext, string Model, string Category, int RepredictionIndex, int Count, double Cost)>();
            
            _console.Status()
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
                                _console.MarkupLine($"[dim]  Processing model: {model}, community context: {communityContext}[/]");
                            }
                            
                            // Get match prediction costs by reprediction index
                            var matchCostsByIndex = predictionRepository.GetMatchPredictionCostsByRepredictionIndexAsync(model, communityContext, availableMatchdays).Result;
                            
                            foreach (var kvp in matchCostsByIndex)
                            {
                                var repredictionIndex = kvp.Key;
                                var (cost, count) = kvp.Value;
                                
                                matchPredictionCost += cost;
                                matchPredictionCount += count;

                                // Store detailed data for breakdown
                                if (settings.DetailedBreakdown && (cost > 0 || count > 0))
                                {
                                    detailedData.Add((communityContext, model, "Match", repredictionIndex, count, cost));
                                }
                                
                                if (settings.Verbose && (cost > 0 || count > 0))
                                {
                                    _console.MarkupLine($"[dim]    Match predictions (reprediction {repredictionIndex}): {count} documents, ${cost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
                                }
                            }
                            
                            // Get bonus prediction costs if requested or if all mode is enabled
                            if (settings.Bonus || settings.All)
                            {
                                var bonusCostsByIndex = predictionRepository.GetBonusPredictionCostsByRepredictionIndexAsync(model, communityContext).Result;
                                
                                foreach (var kvp in bonusCostsByIndex)
                                {
                                    var repredictionIndex = kvp.Key;
                                    var (cost, count) = kvp.Value;
                                    
                                    bonusPredictionCost += cost;
                                    bonusPredictionCount += count;

                                    // Store detailed data for breakdown
                                    if (settings.DetailedBreakdown && (cost > 0 || count > 0))
                                    {
                                        detailedData.Add((communityContext, model, "Bonus", repredictionIndex, count, cost));
                                    }
                                    
                                    if (settings.Verbose && (cost > 0 || count > 0))
                                    {
                                        _console.MarkupLine($"[dim]    Bonus predictions (reprediction {repredictionIndex}): {count} documents, ${cost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
                                    }
                                }
                            }
                        }
                    }
                });
            
            totalCost = matchPredictionCost + bonusPredictionCost;
            
            // Display results
            var table = new Table();
            table.Border(TableBorder.Rounded);
            
            if (settings.DetailedBreakdown)
            {
                // Add columns for detailed breakdown with reprediction support
                table.AddColumn("Community Context");
                table.AddColumn("Model");
                table.AddColumn("Category");
                table.AddColumn("Index 0", col => col.RightAligned());
                table.AddColumn("Index 1", col => col.RightAligned());
                table.AddColumn("Index 2+", col => col.RightAligned());
                table.AddColumn("Total Count", col => col.RightAligned());
                table.AddColumn("Total Cost (USD)", col => col.RightAligned());
                
                // Group data by community context, model, and category to aggregate reprediction indices
                var groupedData = detailedData
                    .GroupBy(d => new { d.CommunityContext, d.Model, d.Category })
                    .Select(g => new
                    {
                        g.Key.CommunityContext,
                        g.Key.Model,
                        g.Key.Category,
                        Index0Count = g.Where(x => x.RepredictionIndex == 0).Sum(x => x.Count),
                        Index0Cost = g.Where(x => x.RepredictionIndex == 0).Sum(x => x.Cost),
                        Index1Count = g.Where(x => x.RepredictionIndex == 1).Sum(x => x.Count),
                        Index1Cost = g.Where(x => x.RepredictionIndex == 1).Sum(x => x.Cost),
                        Index2PlusCount = g.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Count),
                        Index2PlusCost = g.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Cost),
                        TotalCount = g.Sum(x => x.Count),
                        TotalCost = g.Sum(x => x.Cost)
                    })
                    .OrderBy(g => g.CommunityContext)
                    .ThenBy(g => g.Model)
                    .ThenBy(g => g.Category)
                    .ToList();
                
                // Add rows for detailed breakdown with alternating styling
                for (int i = 0; i < groupedData.Count; i++)
                {
                    var data = groupedData[i];
                    var isEvenRow = i % 2 == 0;
                    
                    var index0Text = data.Index0Count > 0 ? $"{data.Index0Count} (${data.Index0Cost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    var index1Text = data.Index1Count > 0 ? $"{data.Index1Count} (${data.Index1Cost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    var index2PlusText = data.Index2PlusCount > 0 ? $"{data.Index2PlusCount} (${data.Index2PlusCost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    
                    if (isEvenRow)
                    {
                        // Even rows - normal styling
                        table.AddRow(
                            data.CommunityContext,
                            data.Model,
                            data.Category,
                            index0Text,
                            index1Text,
                            index2PlusText,
                            data.TotalCount.ToString(CultureInfo.InvariantCulture),
                            $"${data.TotalCost.ToString("F4", CultureInfo.InvariantCulture)}"
                        );
                    }
                    else
                    {
                        // Odd rows - subtle blue tint for visual differentiation
                        table.AddRow(
                            $"[blue]{data.CommunityContext}[/]",
                            $"[blue]{data.Model}[/]",
                            $"[blue]{data.Category}[/]",
                            $"[blue]{index0Text}[/]",
                            $"[blue]{index1Text}[/]",
                            $"[blue]{index2PlusText}[/]",
                            $"[blue]{data.TotalCount.ToString(CultureInfo.InvariantCulture)}[/]",
                            $"[blue]${data.TotalCost.ToString("F4", CultureInfo.InvariantCulture)}[/]"
                        );
                    }
                }
                
                // Add total row
                if (detailedData.Any())
                {
                    // Calculate totals by reprediction index
                    var totalIndex0Count = detailedData.Where(x => x.RepredictionIndex == 0).Sum(x => x.Count);
                    var totalIndex0Cost = detailedData.Where(x => x.RepredictionIndex == 0).Sum(x => x.Cost);
                    var totalIndex1Count = detailedData.Where(x => x.RepredictionIndex == 1).Sum(x => x.Count);
                    var totalIndex1Cost = detailedData.Where(x => x.RepredictionIndex == 1).Sum(x => x.Cost);
                    var totalIndex2PlusCount = detailedData.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Count);
                    var totalIndex2PlusCost = detailedData.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Cost);
                    
                    var totalIndex0Text = totalIndex0Count > 0 ? $"{totalIndex0Count} (${totalIndex0Cost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    var totalIndex1Text = totalIndex1Count > 0 ? $"{totalIndex1Count} (${totalIndex1Cost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    var totalIndex2PlusText = totalIndex2PlusCount > 0 ? $"{totalIndex2PlusCount} (${totalIndex2PlusCost.ToString("F2", CultureInfo.InvariantCulture)})" : "-";
                    
                    table.AddEmptyRow();
                    table.AddRow(
                        "[bold]Total[/]",
                        "",
                        "",
                        $"[bold]{totalIndex0Text}[/]",
                        $"[bold]{totalIndex1Text}[/]",
                        $"[bold]{totalIndex2PlusText}[/]",
                        $"[bold]{(matchPredictionCount + bonusPredictionCount).ToString(CultureInfo.InvariantCulture)}[/]",
                        $"[bold]${totalCost.ToString("F4", CultureInfo.InvariantCulture)}[/]"
                    );
                }
            }
            else
            {
                // Standard summary table
                table.AddColumn("Category");
                table.AddColumn("Count", col => col.RightAligned());
                table.AddColumn("Cost (USD)", col => col.RightAligned());
                
                var rowIndex = 0;
                
                // Add Match row
                var isEvenRow = rowIndex % 2 == 0;
                if (isEvenRow)
                {
                    table.AddRow("Match", matchPredictionCount.ToString(CultureInfo.InvariantCulture), $"${matchPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}");
                }
                else
                {
                    table.AddRow(
                        "[blue]Match[/]", 
                        $"[blue]{matchPredictionCount.ToString(CultureInfo.InvariantCulture)}[/]", 
                        $"[blue]${matchPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}[/]"
                    );
                }
                rowIndex++;
                
                // Add Bonus row if applicable
                if (settings.Bonus || settings.All)
                {
                    isEvenRow = rowIndex % 2 == 0;
                    if (isEvenRow)
                    {
                        table.AddRow("Bonus", bonusPredictionCount.ToString(CultureInfo.InvariantCulture), $"${bonusPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        table.AddRow(
                            "[blue]Bonus[/]", 
                            $"[blue]{bonusPredictionCount.ToString(CultureInfo.InvariantCulture)}[/]", 
                            $"[blue]${bonusPredictionCost.ToString("F4", CultureInfo.InvariantCulture)}[/]"
                        );
                    }
                }
                
                table.AddEmptyRow();
                table.AddRow("[bold]Total[/]", $"[bold]{(matchPredictionCount + bonusPredictionCount).ToString(CultureInfo.InvariantCulture)}[/]", $"[bold]${totalCost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
            }
            
            _console.Write(table);
            
            _console.MarkupLine($"[green]✓ Cost calculation completed[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate costs");
            _console.MarkupLine($"[red]✗ Failed to calculate costs: {ex.Message}[/]");
            return 1;
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

    private async Task<CostConfiguration> LoadConfigurationFromFile(string configFilePath, ILogger logger)
    {
        try
        {
            // Resolve relative paths
            var resolvedPath = Path.IsPathRooted(configFilePath) 
                ? configFilePath 
                : Path.Combine(Directory.GetCurrentDirectory(), configFilePath);

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {resolvedPath}");
            }

            logger.LogInformation("Loading configuration from: {ConfigPath}", resolvedPath);

            var jsonContent = await File.ReadAllTextAsync(resolvedPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var config = JsonSerializer.Deserialize<CostConfiguration>(jsonContent, options);
            if (config == null)
            {
                throw new InvalidOperationException($"Failed to deserialize configuration from: {resolvedPath}");
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in configuration file: {configFilePath}. {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration from file: {configFilePath}. {ex.Message}", ex);
        }
    }

    private CostSettings MergeConfigurations(CostConfiguration fileConfig, CostSettings cliSettings, ILogger logger)
    {
        logger.LogInformation("Merging file configuration with command line options (CLI options take precedence)");

        // Create a new settings object with file config as base, CLI overrides
        var mergedSettings = new CostSettings();

        // Apply file config first (if values are not null/default)
        if (!string.IsNullOrWhiteSpace(fileConfig.Matchdays))
            mergedSettings.Matchdays = fileConfig.Matchdays;
        
        if (fileConfig.Bonus.HasValue)
            mergedSettings.Bonus = fileConfig.Bonus.Value;
        
        if (!string.IsNullOrWhiteSpace(fileConfig.Models))
            mergedSettings.Models = fileConfig.Models;
        
        if (!string.IsNullOrWhiteSpace(fileConfig.CommunityContexts))
            mergedSettings.CommunityContexts = fileConfig.CommunityContexts;
        
        if (fileConfig.All.HasValue)
            mergedSettings.All = fileConfig.All.Value;
        
        if (fileConfig.Verbose.HasValue)
            mergedSettings.Verbose = fileConfig.Verbose.Value;
        
        if (fileConfig.DetailedBreakdown.HasValue)
            mergedSettings.DetailedBreakdown = fileConfig.DetailedBreakdown.Value;

        // Override with CLI settings (non-default values)
        if (!string.IsNullOrWhiteSpace(cliSettings.Matchdays))
        {
            mergedSettings.Matchdays = cliSettings.Matchdays;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: Matchdays = {Value}", cliSettings.Matchdays);
        }
        
        if (cliSettings.Bonus) // Only override if explicitly set to true
        {
            mergedSettings.Bonus = cliSettings.Bonus;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: Bonus = {Value}", cliSettings.Bonus);
        }
        
        if (!string.IsNullOrWhiteSpace(cliSettings.Models))
        {
            mergedSettings.Models = cliSettings.Models;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: Models = {Value}", cliSettings.Models);
        }
        
        if (!string.IsNullOrWhiteSpace(cliSettings.CommunityContexts))
        {
            mergedSettings.CommunityContexts = cliSettings.CommunityContexts;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: CommunityContexts = {Value}", cliSettings.CommunityContexts);
        }
        
        if (cliSettings.All) // Only override if explicitly set to true
        {
            mergedSettings.All = cliSettings.All;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: All = {Value}", cliSettings.All);
        }
        
        if (cliSettings.Verbose) // Only override if explicitly set to true
        {
            mergedSettings.Verbose = cliSettings.Verbose;
        }
        
        if (cliSettings.DetailedBreakdown) // Only override if explicitly set to true
        {
            mergedSettings.DetailedBreakdown = cliSettings.DetailedBreakdown;
            if (mergedSettings.Verbose)
                logger.LogInformation("CLI override: DetailedBreakdown = {Value}", cliSettings.DetailedBreakdown);
        }

        // Always preserve the ConfigFile setting
        mergedSettings.ConfigFile = cliSettings.ConfigFile;

        return mergedSettings;
    }
}
