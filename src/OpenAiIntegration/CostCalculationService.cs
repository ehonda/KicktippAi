using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for calculating and logging OpenAI API costs
/// </summary>
public class CostCalculationService : ICostCalculationService
{
    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(ILogger<CostCalculationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogCostBreakdown(string model, ChatTokenUsage usage)
    {
        if (ModelPricingData.Pricing.TryGetValue(model, out var pricing))
        {
            // Get exact token counts from usage details
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens;
            var outputTokens = usage.OutputTokenCount;
            
            // Calculate costs for each component
            var uncachedInputCost = (uncachedInputTokens / 1_000_000m) * pricing.InputPrice;
            var cachedInputCost = pricing.CachedInputPrice.HasValue 
                ? (cachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value 
                : 0m;
            var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPrice;
            var totalCost = uncachedInputCost + cachedInputCost + outputCost;
            
            // Log the cost breakdown
            _logger.LogInformation("Uncached Input Tokens: {UncachedInputTokens:N0} × ${InputPrice:F2}/1M = ${UncachedInputCost:F6}",
                uncachedInputTokens, pricing.InputPrice, uncachedInputCost);
                
            if (pricing.CachedInputPrice.HasValue)
            {
                _logger.LogInformation("Cached Input Tokens: {CachedInputTokens:N0} × ${CachedInputPrice:F3}/1M = ${CachedInputCost:F6}",
                    cachedInputTokens, pricing.CachedInputPrice.Value, cachedInputCost);
            }
            
            _logger.LogInformation("Output Tokens: {OutputTokens:N0} × ${OutputPrice:F2}/1M = ${OutputCost:F6}",
                outputTokens, pricing.OutputPrice, outputCost);
                
            _logger.LogInformation("Total Cost: ${TotalCost:F6}", totalCost);
        }
        else
        {
            _logger.LogWarning("Cost calculation not available: Pricing information not found for model '{Model}'", model);
        }
    }

    public decimal? CalculateCost(string model, ChatTokenUsage usage)
    {
        if (ModelPricingData.Pricing.TryGetValue(model, out var pricing))
        {
            // Get exact token counts from usage details
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens;
            var outputTokens = usage.OutputTokenCount;
            
            // Calculate costs for each component
            var uncachedInputCost = (uncachedInputTokens / 1_000_000m) * pricing.InputPrice;
            var cachedInputCost = pricing.CachedInputPrice.HasValue 
                ? (cachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value 
                : 0m;
            var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPrice;
            
            return uncachedInputCost + cachedInputCost + outputCost;
        }
        
        return null;
    }
}

/// <summary>
/// Static pricing data for OpenAI models - matches the structure from PromptSampleTests
/// </summary>
internal static class ModelPricingData
{
    public static readonly Dictionary<string, ModelPricing> Pricing = new()
    {
        ["gpt-4.1"] = new(2.00m, 8.00m, 0.50m),
        ["gpt-4.1-mini"] = new(0.40m, 1.60m, 0.10m),
        ["gpt-4.1-nano"] = new(0.10m, 0.40m, 0.025m),
        ["gpt-4.5-preview"] = new(75.00m, 150.00m, 37.50m),
        ["gpt-4o"] = new(2.50m, 10.00m, 1.25m),
        ["gpt-4o-mini"] = new(0.15m, 0.60m, 0.075m),
        ["gpt-5"] = new(1.25m, 10.00m, 0.125m),
        ["gpt-5-mini"] = new(0.25m, 2.00m, 0.025m),
        ["gpt-5-nano"] = new(0.05m, 0.40m, 0.005m),
        ["o1"] = new(15.00m, 60.00m, 7.50m),
        ["o1-pro"] = new(150.00m, 600.00m),
        ["o3"] = new(2.00m, 8.00m, 0.50m),
        ["o3-pro"] = new(20.00m, 80.00m),
        ["o4-mini"] = new(1.10m, 4.40m, 0.275m),
        ["o3-mini"] = new(1.10m, 4.40m, 0.55m),
        ["o1-mini"] = new(1.10m, 4.40m, 0.55m),
    };
}

/// <summary>
/// Pricing information for an OpenAI model
/// </summary>
/// <param name="InputPrice">Price per 1M input tokens</param>
/// <param name="OutputPrice">Price per 1M output tokens</param>
/// <param name="CachedInputPrice">Price per 1M cached input tokens (if supported)</param>
internal record ModelPricing(decimal InputPrice, decimal OutputPrice, decimal? CachedInputPrice = null);
