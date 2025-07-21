namespace PromptSampleTests.Models;

/// <summary>
/// Represents pricing information for an OpenAI model
/// </summary>
public class ModelPricing
{
    /// <summary>
    /// Input price per 1M tokens in USD
    /// </summary>
    public decimal InputPrice { get; set; }

    /// <summary>
    /// Cached input price per 1M tokens in USD (optional)
    /// </summary>
    public decimal? CachedInputPrice { get; set; }

    /// <summary>
    /// Output price per 1M tokens in USD
    /// </summary>
    public decimal OutputPrice { get; set; }
}

/// <summary>
/// Static dictionary containing pricing information for OpenAI models
/// Based on the pricing table from price-estimate.md
/// </summary>
public static class ModelPricingData
{
    public static readonly Dictionary<string, ModelPricing> Pricing = new()
    {
        ["gpt-4.1"] = new() { InputPrice = 2.00m, CachedInputPrice = 0.50m, OutputPrice = 8.00m },
        ["gpt-4.1-mini"] = new() { InputPrice = 0.40m, CachedInputPrice = 0.10m, OutputPrice = 1.60m },
        ["gpt-4.1-nano"] = new() { InputPrice = 0.10m, CachedInputPrice = 0.025m, OutputPrice = 0.40m },
        ["gpt-4.5-preview"] = new() { InputPrice = 75.00m, CachedInputPrice = 37.50m, OutputPrice = 150.00m },
        ["gpt-4o"] = new() { InputPrice = 2.50m, CachedInputPrice = 1.25m, OutputPrice = 10.00m },
        ["gpt-4o-mini"] = new() { InputPrice = 0.15m, CachedInputPrice = 0.075m, OutputPrice = 0.60m },
        ["o1"] = new() { InputPrice = 15.00m, CachedInputPrice = 7.50m, OutputPrice = 60.00m },
        ["o1-pro"] = new() { InputPrice = 150.00m, CachedInputPrice = null, OutputPrice = 600.00m },
        ["o3"] = new() { InputPrice = 2.00m, CachedInputPrice = 0.50m, OutputPrice = 8.00m },
        ["o4-mini"] = new() { InputPrice = 1.10m, CachedInputPrice = 0.275m, OutputPrice = 4.40m },
        ["o3-mini"] = new() { InputPrice = 1.10m, CachedInputPrice = 0.55m, OutputPrice = 4.40m },
        ["o1-mini"] = new() { InputPrice = 1.10m, CachedInputPrice = 0.55m, OutputPrice = 4.40m }
    };

    /// <summary>
    /// Calculate the cost for a given model and token usage
    /// </summary>
    /// <param name="model">The model name</param>
    /// <param name="inputTokens">Number of input tokens</param>
    /// <param name="outputTokens">Number of output tokens</param>
    /// <param name="useCachedPrice">Whether to use cached input pricing (if available)</param>
    /// <returns>Total cost in USD</returns>
    public static decimal CalculateCost(string model, int inputTokens, int outputTokens, bool useCachedPrice = false)
    {
        if (!Pricing.TryGetValue(model, out var pricing))
        {
            throw new ArgumentException($"Pricing information not available for model: {model}");
        }

        var inputPrice = useCachedPrice && pricing.CachedInputPrice.HasValue 
            ? pricing.CachedInputPrice.Value 
            : pricing.InputPrice;

        var inputCost = (inputTokens / 1_000_000m) * inputPrice;
        var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPrice;

        return inputCost + outputCost;
    }
}
