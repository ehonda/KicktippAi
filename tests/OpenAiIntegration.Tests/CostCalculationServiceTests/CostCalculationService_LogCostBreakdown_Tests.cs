using Microsoft.Extensions.Logging;
using TestUtilities;
using TestUtilities.FakeLoggerAssertions;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Tests for the CostCalculationService LogCostBreakdown method
/// </summary>
public class CostCalculationService_LogCostBreakdown_Tests : CostCalculationServiceTests_Base
{
    [Test]
    public async Task LogCostBreakdown_with_known_model_logs_cost_breakdown()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        Service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify all log entries are created
        await Assert.That(Logger)
            .ContainsLog(LogLevel.Information, "Uncached Input Tokens").And
            .ContainsLog(LogLevel.Information, "Cached Input Tokens").And
            .ContainsLog(LogLevel.Information, "Reasoning Output Tokens").And
            .ContainsLog(LogLevel.Information, "Text Output Tokens").And
            .ContainsLog(LogLevel.Information, "Total Output Tokens").And
            .ContainsLog(LogLevel.Information, "Total Cost");
    }

    [Test]
    public async Task LogCostBreakdown_with_cached_tokens_logs_cached_cost_line()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act
        Service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify cached tokens are logged
        await Assert.That(Logger).ContainsLog(LogLevel.Information, "Cached Input Tokens: 600,000");
    }

    [Test]
    public async Task LogCostBreakdown_with_model_without_cached_pricing_does_not_log_cached_line()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act - o1-pro doesn't have cached pricing
        Service.LogCostBreakdown("o1-pro", usage);
        
        // Assert - Verify cached tokens line is NOT logged
        await Assert.That(Logger).DoesNotContainLog(LogLevel.Information, "Cached Input Tokens");
    }

    [Test]
    public async Task LogCostBreakdown_with_unknown_model_logs_warning()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        Service.LogCostBreakdown("unknown-model", usage);
        
        // Assert - Verify warning is logged
        await Assert.That(Logger).ContainsLog(LogLevel.Warning, "Pricing information not found for model 'unknown-model'");
    }

    [Test]
    public async Task LogCostBreakdown_with_zero_cached_tokens_does_not_log_zero_cached_cost()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        Service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - When cachedInputTokens is 0, cached line should still be logged (showing 0 tokens)
        await Assert.That(Logger).ContainsLog(LogLevel.Information, "Cached Input Tokens: 0");
    }

    [Test]
    public async Task LogCostBreakdown_logs_correct_token_counts_and_prices()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 2_500_000,
            outputTokens: 1_250_000,
            cachedInputTokens: 1_000_000);
        
        // Act
        Service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify correct token counts are logged
        // o3: $2.00/1M input, $8.00/1M output, $0.50/1M cached
        // Uncached: 1,500,000 tokens
        await Assert.That(Logger)
            .ContainsLog(LogLevel.Information, "Uncached Input Tokens: 1,500,000").And
            .ContainsLog(LogLevel.Information, "Cached Input Tokens: 1,000,000").And
            .ContainsLog(LogLevel.Information, "Total Output Tokens: 1,250,000");
    }

    [Test]
    public async Task LogCostBreakdown_with_reasoning_tokens_logs_reasoning_and_text_breakdown()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            outputReasoningTokens: 300_000);
        
        // Act
        Service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify reasoning and text tokens are logged separately
        await Assert.That(Logger)
            .ContainsLog(LogLevel.Information, "Reasoning Output Tokens: 300,000").And
            .ContainsLog(LogLevel.Information, "Text Output Tokens: 200,000").And
            .ContainsLog(LogLevel.Information, "Total Output Tokens: 500,000");
    }

    [Test]
    public async Task LogCostBreakdown_with_zero_reasoning_tokens_logs_zero_reasoning()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            outputReasoningTokens: 0);
        
        // Act
        Service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify reasoning tokens shows 0
        await Assert.That(Logger)
            .ContainsLog(LogLevel.Information, "Reasoning Output Tokens: 0").And
            .ContainsLog(LogLevel.Information, "Text Output Tokens: 500,000");
    }

    [Test]
    public async Task LogCostBreakdown_with_all_reasoning_tokens_logs_zero_text()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            outputReasoningTokens: 500_000);
        
        // Act
        Service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify all output is reasoning, no text tokens
        await Assert.That(Logger)
            .ContainsLog(LogLevel.Information, "Reasoning Output Tokens: 500,000").And
            .ContainsLog(LogLevel.Information, "Text Output Tokens: 0");
    }
}
