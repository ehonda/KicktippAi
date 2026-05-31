using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Observability.Cost;

public class CostCommand : AsyncCommand<CostSettings>
{
    private const int FirestoreInFilterLimit = 30;

    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly ILogger<CostCommand> _logger;

    public CostCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        ILogger<CostCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, CostSettings settings, CancellationToken cancellationToken)
    {
        
        try
        {
            // Load configuration from file if specified
            if (!string.IsNullOrWhiteSpace(settings.ConfigFile))
            {
                var fileConfig = await LoadConfigurationFromFile(settings.ConfigFile);
                settings = MergeConfigurations(fileConfig, settings);
            }
            
            // Create Firebase services using factory (factory handles env var loading)
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
            var reasoningEfforts = ParseReasoningEfforts(settings);
            var communityContexts = ParseCommunityContexts(settings);
            
            // Get available model configurations and community contexts if not specified
            var availableModelConfigs = await ResolveModelConfigsAsync(predictionRepository, models, reasoningEfforts, cancellationToken);
            var availableCommunityContexts = communityContexts ?? await predictionRepository.GetAvailableCommunityContextsAsync();
            var queryMatchdays = matchdays;
            
            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Filters:[/]");
                _console.MarkupLine($"[dim]  Matchdays: {FormatMatchdayFilter(queryMatchdays)}[/]");
                _console.MarkupLine($"[dim]  Model Configs: {(models?.Any() == true || reasoningEfforts?.Any() == true ? string.Join(", ", availableModelConfigs.Select(config => config.DisplayName)) : $"all ({availableModelConfigs.Count} found)")}[/]");
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
            var costRows = new List<CostReportRow>();
            
            await _console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Calculating costs...", async ctx =>
                {
                    foreach (var modelConfig in availableModelConfigs)
                    {
                        foreach (var communityContext in availableCommunityContexts)
                        {
                            ctx.Status($"Processing {modelConfig.DisplayName} - {communityContext}...");
                            
                            if (settings.Verbose)
                            {
                                _console.MarkupLine($"[dim]  Processing model config: {modelConfig.DisplayName}, community context: {communityContext}[/]");
                            }
                            
                            // Get match prediction costs by reprediction index
                            var matchCostsByIndex = await GetMatchCostsByRepredictionIndexAsync(
                                predictionRepository,
                                modelConfig,
                                communityContext,
                                queryMatchdays,
                                cancellationToken);
                            
                            foreach (var kvp in matchCostsByIndex)
                            {
                                var repredictionIndex = kvp.Key;
                                var (cost, count) = kvp.Value;
                                
                                matchPredictionCost += cost;
                                matchPredictionCount += count;

                                // Store detailed data for breakdown
                                if (cost > 0 || count > 0)
                                {
                                    costRows.Add(new CostReportRow(communityContext, modelConfig.Model, modelConfig.ReasoningEffort, modelConfig.IdentityKey, modelConfig.DisplayName, "Match", repredictionIndex, count, cost));
                                }
                                
                                if (settings.Verbose && (cost > 0 || count > 0))
                                {
                                    _console.MarkupLine($"[dim]    Match predictions (reprediction {repredictionIndex}): {count} documents, ${cost.ToString("F4", CultureInfo.InvariantCulture)}[/]");
                                }
                            }
                            
                            // Get bonus prediction costs if requested or if all mode is enabled
                            if (settings.Bonus || settings.All)
                            {
                                var bonusCostsByIndex = await predictionRepository.GetBonusPredictionCostsByRepredictionIndexAsync(
                                    modelConfig,
                                    communityContext,
                                    cancellationToken);
                                
                                foreach (var kvp in bonusCostsByIndex)
                                {
                                    var repredictionIndex = kvp.Key;
                                    var (cost, count) = kvp.Value;
                                    
                                    bonusPredictionCost += cost;
                                    bonusPredictionCount += count;

                                    // Store detailed data for breakdown
                                    if (cost > 0 || count > 0)
                                    {
                                        costRows.Add(new CostReportRow(communityContext, modelConfig.Model, modelConfig.ReasoningEffort, modelConfig.IdentityKey, modelConfig.DisplayName, "Bonus", repredictionIndex, count, cost));
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
                var groupedData = costRows
                    .GroupBy(d => new { d.CommunityContext, d.Model, d.ReasoningEffort, d.Category })
                    .Select(g => new
                    {
                        g.Key.CommunityContext,
                        g.Key.Model,
                        ReasoningEffort = g.Key.ReasoningEffort ?? "model-default",
                        ModelConfigDisplayName = PredictionModelConfig.Create(g.Key.Model, g.Key.ReasoningEffort).DisplayName,
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
                    .ThenBy(g => g.ReasoningEffort)
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
                            data.ModelConfigDisplayName,
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
                            $"[blue]{data.ModelConfigDisplayName}[/]",
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
                if (costRows.Any())
                {
                    // Calculate totals by reprediction index
                    var totalIndex0Count = costRows.Where(x => x.RepredictionIndex == 0).Sum(x => x.Count);
                    var totalIndex0Cost = costRows.Where(x => x.RepredictionIndex == 0).Sum(x => x.Cost);
                    var totalIndex1Count = costRows.Where(x => x.RepredictionIndex == 1).Sum(x => x.Count);
                    var totalIndex1Cost = costRows.Where(x => x.RepredictionIndex == 1).Sum(x => x.Cost);
                    var totalIndex2PlusCount = costRows.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Count);
                    var totalIndex2PlusCost = costRows.Where(x => x.RepredictionIndex >= 2).Sum(x => x.Cost);
                    
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

            if (!string.IsNullOrWhiteSpace(settings.OutputJson))
            {
                var report = CreateReport(
                    settings,
                    queryMatchdays,
                    models,
                    reasoningEfforts,
                    communityContexts,
                    costRows,
                    matchPredictionCount,
                    matchPredictionCost,
                    bonusPredictionCount,
                    bonusPredictionCost,
                    totalCost);
                await WriteJsonReportAsync(settings.OutputJson, report, cancellationToken);
                _console.MarkupLine($"[green]✓ Cost JSON written to[/] [yellow]{Path.GetFullPath(settings.OutputJson)}[/]");
            }
            
            _console.MarkupLine($"[green]✓ Cost calculation completed[/]");
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate costs");
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

    private static string FormatMatchdayFilter(List<int>? matchdays)
    {
        if (matchdays is null)
        {
            return "all (unfiltered; no matchday discovery)";
        }

        return matchdays.Count == 0
            ? "none"
            : string.Join(", ", matchdays);
    }

    private static async Task<Dictionary<int, (double cost, int count)>> GetMatchCostsByRepredictionIndexAsync(
        IPredictionRepository predictionRepository,
        PredictionModelConfig modelConfig,
        string communityContext,
        List<int>? matchdays,
        CancellationToken cancellationToken)
    {
        if (matchdays is { Count: 0 })
        {
            return [];
        }

        if (matchdays is null || matchdays.Count <= FirestoreInFilterLimit)
        {
            return await predictionRepository.GetMatchPredictionCostsByRepredictionIndexAsync(
                modelConfig,
                communityContext,
                matchdays,
                cancellationToken);
        }

        var aggregate = new Dictionary<int, (double cost, int count)>();

        foreach (var chunk in matchdays.Chunk(FirestoreInFilterLimit))
        {
            var chunkCosts = await predictionRepository.GetMatchPredictionCostsByRepredictionIndexAsync(
                modelConfig,
                communityContext,
                chunk.ToList(),
                cancellationToken);

            foreach (var kvp in chunkCosts)
            {
                var (existingCost, existingCount) = aggregate.GetValueOrDefault(kvp.Key);
                var (chunkCost, chunkCount) = kvp.Value;
                aggregate[kvp.Key] = (existingCost + chunkCost, existingCount + chunkCount);
            }
        }

        return aggregate;
    }

    private static CostReport CreateReport(
        CostSettings settings,
        List<int>? matchdays,
        List<string>? models,
        List<string?>? reasoningEfforts,
        List<string>? communityContexts,
        IReadOnlyList<CostReportRow> rows,
        int matchPredictionCount,
        double matchPredictionCost,
        int bonusPredictionCount,
        double bonusPredictionCost,
        double totalCost)
    {
        var categoryTotals = rows
            .GroupBy(row => new { row.Category, row.RepredictionIndex })
            .Select(group => new CostReportCategoryTotal(
                group.Key.Category,
                group.Key.RepredictionIndex,
                group.Sum(row => row.Count),
                group.Sum(row => row.Cost)))
            .OrderBy(total => total.Category, StringComparer.Ordinal)
            .ThenBy(total => total.RepredictionIndex)
            .ToList();

        return new CostReport(
            new CostReportFilters(
                matchdays,
                models,
                reasoningEfforts,
                communityContexts,
                settings.Bonus || settings.All,
                settings.All),
            rows
                .OrderBy(row => row.CommunityContext, StringComparer.Ordinal)
                .ThenBy(row => row.Model, StringComparer.Ordinal)
                .ThenBy(row => row.ReasoningEffort ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(row => row.Category, StringComparer.Ordinal)
                .ThenBy(row => row.RepredictionIndex)
                .ToList(),
            categoryTotals,
            new CostReportTotal(
                matchPredictionCount,
                matchPredictionCost,
                bonusPredictionCount,
                bonusPredictionCost,
                matchPredictionCount + bonusPredictionCount,
                totalCost));
    }

    private static async Task WriteJsonReportAsync(
        string outputPath,
        CostReport report,
        CancellationToken cancellationToken)
    {
        var resolvedPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(resolvedPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            resolvedPath,
            JsonSerializer.Serialize(report, OutputJsonOptions),
            cancellationToken);
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

    private static List<string?>? ParseReasoningEfforts(CostSettings settings)
    {
        if (settings.All || string.IsNullOrWhiteSpace(settings.ReasoningEfforts))
        {
            return null;
        }

        if (settings.ReasoningEfforts.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var efforts = new List<string?>();
        foreach (var segment in settings.ReasoningEfforts.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = segment.Trim();
            if (trimmed.Equals("model-default", StringComparison.OrdinalIgnoreCase))
            {
                efforts.Add(null);
                continue;
            }

            if (!PredictionModelConfig.IsValidReasoningEffort(trimmed))
            {
                throw new ArgumentException("--reasoning-efforts must contain only: model-default, none, minimal, low, medium, high, xhigh, or all");
            }

            efforts.Add(PredictionModelConfig.NormalizeReasoningEffort(trimmed));
        }

        return efforts.Distinct().ToList();
    }

    private static async Task<List<PredictionModelConfig>> ResolveModelConfigsAsync(
        IPredictionRepository predictionRepository,
        List<string>? models,
        List<string?>? reasoningEfforts,
        CancellationToken cancellationToken)
    {
        var availableModelConfigs = await predictionRepository.GetAvailableModelConfigsAsync(cancellationToken);

        var filtered = availableModelConfigs.AsEnumerable();
        if (models is not null)
        {
            var requestedModels = models.ToHashSet(StringComparer.Ordinal);
            filtered = filtered.Where(config => requestedModels.Contains(config.Model));
        }

        if (reasoningEfforts is not null)
        {
            var requestedEfforts = reasoningEfforts.ToHashSet(StringComparer.Ordinal);
            filtered = filtered.Where(config => requestedEfforts.Contains(config.ReasoningEffort));
        }

        return filtered
            .OrderBy(config => config.Model, StringComparer.Ordinal)
            .ThenBy(config => config.ReasoningEffort ?? string.Empty, StringComparer.Ordinal)
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

    private async Task<CostConfiguration> LoadConfigurationFromFile(string configFilePath)
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

            _logger.LogInformation("Loading configuration from: {ConfigPath}", resolvedPath);

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

    private CostSettings MergeConfigurations(CostConfiguration fileConfig, CostSettings cliSettings)
    {
        _logger.LogInformation("Merging file configuration with command line options (CLI options take precedence)");

        // Create a new settings object with file config as base, CLI overrides
        var mergedSettings = new CostSettings();

        // Apply file config first (if values are not null/default)
        if (!string.IsNullOrWhiteSpace(fileConfig.Matchdays))
            mergedSettings.Matchdays = fileConfig.Matchdays;
        
        if (fileConfig.Bonus.HasValue)
            mergedSettings.Bonus = fileConfig.Bonus.Value;
        
        if (!string.IsNullOrWhiteSpace(fileConfig.Models))
            mergedSettings.Models = fileConfig.Models;

        if (!string.IsNullOrWhiteSpace(fileConfig.ReasoningEfforts))
            mergedSettings.ReasoningEfforts = fileConfig.ReasoningEfforts;
        
        if (!string.IsNullOrWhiteSpace(fileConfig.CommunityContexts))
            mergedSettings.CommunityContexts = fileConfig.CommunityContexts;
        
        if (fileConfig.All.HasValue)
            mergedSettings.All = fileConfig.All.Value;
        
        if (fileConfig.Verbose.HasValue)
            mergedSettings.Verbose = fileConfig.Verbose.Value;
        
        if (fileConfig.DetailedBreakdown.HasValue)
            mergedSettings.DetailedBreakdown = fileConfig.DetailedBreakdown.Value;

        if (!string.IsNullOrWhiteSpace(fileConfig.OutputJson))
            mergedSettings.OutputJson = fileConfig.OutputJson;

        // Override with CLI settings (non-default values)
        if (!string.IsNullOrWhiteSpace(cliSettings.Matchdays))
        {
            mergedSettings.Matchdays = cliSettings.Matchdays;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: Matchdays = {Value}", cliSettings.Matchdays);
        }
        
        if (cliSettings.Bonus) // Only override if explicitly set to true
        {
            mergedSettings.Bonus = cliSettings.Bonus;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: Bonus = {Value}", cliSettings.Bonus);
        }
        
        if (!string.IsNullOrWhiteSpace(cliSettings.Models))
        {
            mergedSettings.Models = cliSettings.Models;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: Models = {Value}", cliSettings.Models);
        }

        if (!string.IsNullOrWhiteSpace(cliSettings.ReasoningEfforts))
        {
            mergedSettings.ReasoningEfforts = cliSettings.ReasoningEfforts;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: ReasoningEfforts = {Value}", cliSettings.ReasoningEfforts);
        }
        
        if (!string.IsNullOrWhiteSpace(cliSettings.CommunityContexts))
        {
            mergedSettings.CommunityContexts = cliSettings.CommunityContexts;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: CommunityContexts = {Value}", cliSettings.CommunityContexts);
        }
        
        if (cliSettings.All) // Only override if explicitly set to true
        {
            mergedSettings.All = cliSettings.All;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: All = {Value}", cliSettings.All);
        }
        
        if (cliSettings.Verbose) // Only override if explicitly set to true
        {
            mergedSettings.Verbose = cliSettings.Verbose;
        }
        
        if (cliSettings.DetailedBreakdown) // Only override if explicitly set to true
        {
            mergedSettings.DetailedBreakdown = cliSettings.DetailedBreakdown;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: DetailedBreakdown = {Value}", cliSettings.DetailedBreakdown);
        }

        if (!string.IsNullOrWhiteSpace(cliSettings.OutputJson))
        {
            mergedSettings.OutputJson = cliSettings.OutputJson;
            if (mergedSettings.Verbose)
                _logger.LogInformation("CLI override: OutputJson = {Value}", cliSettings.OutputJson);
        }

        // Always preserve the ConfigFile setting
        mergedSettings.ConfigFile = cliSettings.ConfigFile;

        return mergedSettings;
    }

    private sealed record CostReport(
        CostReportFilters Filters,
        IReadOnlyList<CostReportRow> Rows,
        IReadOnlyList<CostReportCategoryTotal> CategoryTotals,
        CostReportTotal Total);

    private sealed record CostReportFilters(
        IReadOnlyList<int>? Matchdays,
        IReadOnlyList<string>? Models,
        IReadOnlyList<string?>? ReasoningEfforts,
        IReadOnlyList<string>? CommunityContexts,
        bool IncludeBonus,
        bool All);

    private sealed record CostReportRow(
        string CommunityContext,
        string Model,
        string? ReasoningEffort,
        string ModelConfigKey,
        string ModelConfigDisplayName,
        string Category,
        int RepredictionIndex,
        int Count,
        double Cost);

    private sealed record CostReportCategoryTotal(
        string Category,
        int RepredictionIndex,
        int Count,
        double Cost);

    private sealed record CostReportTotal(
        int MatchPredictionCount,
        double MatchPredictionCost,
        int BonusPredictionCount,
        double BonusPredictionCost,
        int Count,
        double Cost);
}
