using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiIntegration;

/// <summary>
/// Service for tracking token usage and costs across multiple API calls
/// </summary>
public class TokenUsageTracker : ITokenUsageTracker
{
    private readonly ILogger<TokenUsageTracker> _logger;
    private readonly ICostCalculationService _costCalculationService;
    private readonly object _lock = new();
    
    private int _totalUncachedInputTokens = 0;
    private int _totalCachedInputTokens = 0;
    private int _totalOutputReasoningTokens = 0;
    private int _totalOutputTokens = 0;
    private decimal _totalCost = 0m;
    
    // Track last usage for individual match reporting
    private int _lastUncachedInputTokens = 0;
    private int _lastCachedInputTokens = 0;
    private int _lastOutputReasoningTokens = 0;
    private int _lastOutputTokens = 0;
    private decimal _lastCost = 0m;
    private ChatTokenUsage? _lastUsage = null;

    public TokenUsageTracker(ILogger<TokenUsageTracker> logger, ICostCalculationService costCalculationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _costCalculationService = costCalculationService ?? throw new ArgumentNullException(nameof(costCalculationService));
    }

    public void AddUsage(string model, ChatTokenUsage usage)
    {
        lock (_lock)
        {
            // Store last usage for individual reporting
            _lastUsage = usage;

            // Get token counts
            var cachedInputTokens = usage.InputTokenDetails?.CachedTokenCount ?? 0;
            var uncachedInputTokens = usage.InputTokenCount - cachedInputTokens;
            var outputReasoningTokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0;
            var regularOutputTokens = usage.OutputTokenCount - outputReasoningTokens;

            // Store last usage for individual reporting
            _lastUncachedInputTokens = uncachedInputTokens;
            _lastCachedInputTokens = cachedInputTokens;
            _lastOutputReasoningTokens = outputReasoningTokens;
            _lastOutputTokens = regularOutputTokens;

            // Add to totals
            _totalUncachedInputTokens += uncachedInputTokens;
            _totalCachedInputTokens += cachedInputTokens;
            _totalOutputReasoningTokens += outputReasoningTokens;
            _totalOutputTokens += regularOutputTokens;

            // Calculate cost for this usage
            var costForThisUsage = _costCalculationService.CalculateCost(model, usage) ?? 0m;
            _lastCost = costForThisUsage;
            _totalCost += costForThisUsage;
            
            _logger.LogDebug("Added usage for model {Model}: {UncachedInput} uncached + {CachedInput} cached + {OutputReasoning} reasoning + {OutputRegular} output = ${Cost:F6}",
                model, uncachedInputTokens, cachedInputTokens, outputReasoningTokens, regularOutputTokens, costForThisUsage);
        }
    }

    public string GetCompactSummary()
    {
        lock (_lock)
        {
            return $"{_totalUncachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalCachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalOutputReasoningTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalOutputTokens.ToString("N0", CultureInfo.InvariantCulture)} / ${_totalCost.ToString("F4", CultureInfo.InvariantCulture)}";
        }
    }

    public string GetCompactSummaryWithEstimatedCosts(string estimatedCostsModel)
    {
        lock (_lock)
        {
            var baseSummary = $"{_totalUncachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalCachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalOutputReasoningTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_totalOutputTokens.ToString("N0", CultureInfo.InvariantCulture)} / ${_totalCost.ToString("F4", CultureInfo.InvariantCulture)}";
            
            // Calculate estimated costs for the alternative model
            decimal totalEstimatedCost = CalculateTotalEstimatedCost(estimatedCostsModel);
            
            return $"{baseSummary} (est {estimatedCostsModel}: ${totalEstimatedCost.ToString("F4", CultureInfo.InvariantCulture)})";
        }
    }

    private decimal CalculateTotalEstimatedCost(string estimatedCostsModel)
    {
        // Manually calculate estimated cost based on our tracked totals
        if (!ModelPricingData.Pricing.TryGetValue(estimatedCostsModel, out var pricing))
        {
            return 0m; // Can't calculate if we don't have pricing info
        }
        
        // Calculate costs for each component
        var uncachedInputCost = (_totalUncachedInputTokens / 1_000_000m) * pricing.InputPrice;
        var cachedInputCost = pricing.CachedInputPrice.HasValue 
            ? (_totalCachedInputTokens / 1_000_000m) * pricing.CachedInputPrice.Value 
            : 0m;
        var totalOutputTokenCount = _totalOutputReasoningTokens + _totalOutputTokens;
        var outputCost = (totalOutputTokenCount / 1_000_000m) * pricing.OutputPrice;
        
        return uncachedInputCost + cachedInputCost + outputCost;
    }

    public string GetLastUsageCompactSummary()
    {
        lock (_lock)
        {
            return $"{_lastUncachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastCachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastOutputReasoningTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastOutputTokens.ToString("N0", CultureInfo.InvariantCulture)} / ${_lastCost.ToString("F4", CultureInfo.InvariantCulture)}";
        }
    }

    public string GetLastUsageCompactSummaryWithEstimatedCosts(string estimatedCostsModel)
    {
        lock (_lock)
        {
            var baseSummary = $"{_lastUncachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastCachedInputTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastOutputReasoningTokens.ToString("N0", CultureInfo.InvariantCulture)} / {_lastOutputTokens.ToString("N0", CultureInfo.InvariantCulture)} / ${_lastCost.ToString("F4", CultureInfo.InvariantCulture)}";
            
            // Calculate estimated cost for last usage
            if (_lastUsage != null)
            {
                var estimatedCost = _costCalculationService.CalculateCost(estimatedCostsModel, _lastUsage) ?? 0m;
                return $"{baseSummary} (est {estimatedCostsModel}: ${estimatedCost.ToString("F4", CultureInfo.InvariantCulture)})";
            }
            
            return baseSummary;
        }
    }

    public decimal GetTotalCost()
    {
        lock (_lock)
        {
            return _totalCost;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _totalUncachedInputTokens = 0;
            _totalCachedInputTokens = 0;
            _totalOutputReasoningTokens = 0;
            _totalOutputTokens = 0;
            _totalCost = 0m;
            
            _lastUncachedInputTokens = 0;
            _lastCachedInputTokens = 0;
            _lastOutputReasoningTokens = 0;
            _lastOutputTokens = 0;
            _lastCost = 0m;
            _lastUsage = null;
            
            _logger.LogDebug("Token usage tracker reset");
        }
    }
}
