using Microsoft.Extensions.Logging;
using Moq;
using TestUtilities;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Tests for the CostCalculationService CalculateCost method
/// </summary>
public class CostCalculationService_CalculateCost_Tests : CostCalculationServiceTests_Base
{
    [Test]
    public async Task CalculateCost_with_known_model_and_no_cached_tokens_returns_correct_cost()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        var cost = Service.CalculateCost("gpt-4o", usage);
        
        // Assert
        // gpt-4o: $2.50/1M input, $10.00/1M output
        // Expected: (1M * 2.50) + (0.5M * 10.00) = 2.50 + 5.00 = 7.50
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(7.50m);
    }

    [Test]
    public async Task CalculateCost_with_cached_input_tokens_returns_correct_cost()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act
        var cost = Service.CalculateCost("gpt-4o", usage);
        
        // Assert
        // gpt-4o: $2.50/1M input, $10.00/1M output, $1.25/1M cached
        // Uncached: 400,000 tokens
        // Cached: 600,000 tokens
        // Expected: (0.4M * 2.50) + (0.6M * 1.25) + (0.5M * 10.00) = 1.00 + 0.75 + 5.00 = 6.75
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(6.75m);
    }

    [Test]
    public async Task CalculateCost_with_model_without_cached_pricing_ignores_cached_tokens()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act - o1-pro doesn't have cached pricing
        var cost = Service.CalculateCost("o1-pro", usage);
        
        // Assert
        // o1-pro: $150/1M input, $600/1M output, no cached pricing
        // Uncached: 400,000 tokens (1M - 600K cached)
        // Cached tokens are ignored (cost $0)
        // Expected: (0.4M * 150) + (0.5M * 600) = 60 + 300 = 360
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(360m);
    }

    [Test]
    public async Task CalculateCost_with_unknown_model_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        var cost = Service.CalculateCost("unknown-model", usage);
        
        // Assert
        await Assert.That(cost).IsNull();
    }

    [Test]
    public async Task CalculateCost_with_zero_tokens_returns_zero_cost()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 0,
            outputTokens: 0,
            cachedInputTokens: 0);
        
        // Act
        var cost = Service.CalculateCost("gpt-4o", usage);
        
        // Assert
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(0m);
    }

    [Test]
    public async Task CalculateCost_with_reasoning_model_o3_calculates_correctly()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 2_000_000,
            outputTokens: 1_000_000,
            cachedInputTokens: 500_000);
        
        // Act
        var cost = Service.CalculateCost("o3", usage);
        
        // Assert
        // o3: $2.00/1M input, $8.00/1M output, $0.50/1M cached
        // Uncached: 1,500,000 tokens
        // Cached: 500,000 tokens
        // Expected: (1.5M * 2.00) + (0.5M * 0.50) + (1M * 8.00) = 3.00 + 0.25 + 8.00 = 11.25
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(11.25m);
    }

    [Test]
    [Arguments("gpt-5-nano", 0.05, 0.40, 0.005)]
    [Arguments("o4-mini", 1.10, 4.40, 0.275)]
    [Arguments("gpt-4.1", 2.00, 8.00, 0.50)]
    public async Task CalculateCost_with_various_models_calculates_correctly(
        string model, 
        decimal inputPrice, 
        decimal outputPrice, 
        decimal cachedPrice)
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 1_000_000,
            cachedInputTokens: 0);
        
        // Act
        var cost = Service.CalculateCost(model, usage);
        
        // Assert
        var expectedCost = inputPrice + outputPrice;
        await Assert.That(cost).IsNotNull();
        await Assert.That(cost!.Value).IsEqualTo(expectedCost);
    }
}
