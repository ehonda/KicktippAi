using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for calculating and logging OpenAI API costs
/// </summary>
public class CostCalculationService : ICostCalculationService
{
    private const decimal FlexPriceMultiplier = 0.5m;

    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(ILogger<CostCalculationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogCostBreakdown(string model, ChatTokenUsage usage)
    {
        LogCostBreakdown(model, usage, serviceTier: null);
    }

    public void LogCostBreakdown(string model, ChatTokenUsage usage, string? serviceTier)
    {
        if (ModelPricingData.Pricing.TryGetValue(model, out var pricing))
        {
            pricing = ApplyPricingAdjustments(model, usage.InputTokenCount, pricing, serviceTier);

            // Get exact token counts from usage details
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var (write30m, write12h) = CacheWriteUsage.Get(usage);
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens - write30m - write12h;
            var reasoningOutputTokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0;
            var textOutputTokens = usage.OutputTokenCount - reasoningOutputTokens;
            
            // Calculate costs for each component
            var uncachedInputCost = (uncachedInputTokens / 1_000_000m) * pricing.InputPrice;
            var cachedInputCost = pricing.CachedInputPrice.HasValue 
                ? (cachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value 
                : 0m;
            var outputCost = (usage.OutputTokenCount / 1_000_000m) * pricing.OutputPrice;
            var cacheWriteCost = CalculateCostBreakdown(model, usage, serviceTier)!.CacheWrite;
            var totalCost = uncachedInputCost + cachedInputCost + cacheWriteCost + outputCost;
            _logger.LogInformation("Cache Write Tokens: {CacheWriteTokens:N0} = ${CacheWriteCost:F6}", write30m + write12h, cacheWriteCost);
            
            // Log the cost breakdown
            _logger.LogInformation("Uncached Input Tokens: {UncachedInputTokens:N0} × ${InputPrice:F2}/1M = ${UncachedInputCost:F6}",
                uncachedInputTokens, pricing.InputPrice, uncachedInputCost);
                
            if (pricing.CachedInputPrice.HasValue)
            {
                _logger.LogInformation("Cached Input Tokens: {CachedInputTokens:N0} × ${CachedInputPrice:F3}/1M = ${CachedInputCost:F6}",
                    cachedInputTokens, pricing.CachedInputPrice.Value, cachedInputCost);
            }
            
            _logger.LogInformation("Reasoning Output Tokens: {ReasoningOutputTokens:N0}",
                reasoningOutputTokens);
            
            _logger.LogInformation("Text Output Tokens: {TextOutputTokens:N0}",
                textOutputTokens);
            
            _logger.LogInformation("Total Output Tokens: {TotalOutputTokens:N0} × ${OutputPrice:F2}/1M = ${OutputCost:F6}",
                usage.OutputTokenCount, pricing.OutputPrice, outputCost);
                
            _logger.LogInformation("Total Cost: ${TotalCost:F6}", totalCost);
        }
        else
        {
            _logger.LogWarning("Cost calculation not available: Pricing information not found for model '{Model}'", model);
        }
    }

    public decimal? CalculateCost(string model, ChatTokenUsage usage)
    {
        return CalculateCost(model, usage, serviceTier: null);
    }

    public decimal? CalculateCost(string model, ChatTokenUsage usage, string? serviceTier)
    {
        return CalculateCostBreakdown(model, usage, serviceTier)?.Total;
    }

    public CostBreakdown? CalculateCostBreakdown(string model, ChatTokenUsage usage)
    {
        return CalculateCostBreakdown(model, usage, serviceTier: null);
    }

    public CostBreakdown? CalculateCostBreakdown(string model, ChatTokenUsage usage, string? serviceTier)
    {
        if (ModelPricingData.Pricing.TryGetValue(model, out var pricing))
        {
            pricing = ApplyPricingAdjustments(model, usage.InputTokenCount, pricing, serviceTier);

            // Get exact token counts from usage details
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var (write30m, write12h) = CacheWriteUsage.Get(usage);
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens - write30m - write12h;
            var outputTokens = usage.OutputTokenCount;

            // Calculate costs for each component
            var uncachedInputCost = (uncachedInputTokens / 1_000_000m) * pricing.InputPrice;
            var cachedInputCost = pricing.CachedInputPrice.HasValue
                ? (cachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value
                : 0m;
            var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPrice;

            var cacheWriteCost = model is "gpt-5.6" or "gpt-5.6-sol" or "gpt-5.6-terra" or "gpt-5.6-luna" or "gpt-6.1-sol" or "gpt-6.2-sol"
                ? (write30m * 1.25m + write12h * 2.5m) / 1_000_000m * pricing.InputPrice
                : (write30m + write12h) / 1_000_000m * pricing.InputPrice;
            return new CostBreakdown(uncachedInputCost, cachedInputCost, outputCost, uncachedInputCost + cachedInputCost + cacheWriteCost + outputCost)
            {
                CacheWrite = cacheWriteCost
            };
        }

        return null;
    }

    private static ModelPricing ApplyPricingAdjustments(string model, int inputTokens, ModelPricing pricing, string? serviceTier)
    {
        pricing = ApplyServiceTier(pricing, serviceTier);
        if (model == "gpt-6.2-sol" && inputTokens > 272_000)
        {
            pricing = new ModelPricing(pricing.InputPrice * 2m, pricing.OutputPrice * 1.5m, pricing.CachedInputPrice * 2m);
        }
        return pricing;
    }

    private static ModelPricing ApplyServiceTier(ModelPricing pricing, string? serviceTier)
    {
        if (!string.Equals(serviceTier?.Trim(), "flex", StringComparison.OrdinalIgnoreCase))
        {
            return pricing;
        }

        return new ModelPricing(
            pricing.InputPrice * FlexPriceMultiplier,
            pricing.OutputPrice * FlexPriceMultiplier,
            pricing.CachedInputPrice * FlexPriceMultiplier);
    }
}

/// <summary>
/// Static short-context standard pricing data for OpenAI models.
/// </summary>
/// <remarks>
/// Source: <see href="https://developers.openai.com/api/docs/pricing.md">OpenAI API pricing</see>.
/// Flex processing applies the same short-context rates as Batch pricing for supported models.
/// </remarks>
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
        ["gpt-5.5"] = new(5.00m, 30.00m, 0.50m),
        ["gpt-5.4"] = new(2.50m, 15.00m, 0.25m),
        ["gpt-5.4-mini"] = new(0.75m, 4.50m, 0.075m),
        ["gpt-5.4-nano"] = new(0.20m, 1.25m, 0.02m),
        ["gpt-5.6"] = new(4.00m, 20.00m, 0.40m),
        ["gpt-5.6-luna"] = new(0.20m, 1.20m, 0.02m),
        ["gpt-5.6-sol"] = new(4.00m, 20.00m, 0.40m),
        ["gpt-6.1-sol"] = new(2.00m, 10.00m, 0.10m),
        ["gpt-6.2-sol"] = new(2.00m, 10.00m, 0.10m),
        ["gpt-5.6-terra"] = new(2.00m, 12.00m, 0.20m),
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
