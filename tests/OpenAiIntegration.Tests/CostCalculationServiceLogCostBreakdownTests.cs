using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests;

/// <summary>
/// Tests for the CostCalculationService LogCostBreakdown method
/// </summary>
public class CostCalculationServiceLogCostBreakdownTests
{
    [Test]
    public Task LogCostBreakdown_with_known_model_logs_cost_breakdown()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify all log entries are created
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Uncached Input Tokens")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cached Input Tokens")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Output Tokens")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Total Cost")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_cached_tokens_logs_cached_cost_line()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify cached tokens are logged
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cached Input Tokens: 600,000")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_model_without_cached_pricing_does_not_log_cached_line()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act - o1-pro doesn't have cached pricing
        service.LogCostBreakdown("o1-pro", usage);
        
        // Assert - Verify cached tokens line is NOT logged
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cached Input Tokens")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_unknown_model_logs_warning()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("unknown-model", usage);
        
        // Assert - Verify warning is logged
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Pricing information not found for model 'unknown-model'")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_zero_cached_tokens_does_not_log_zero_cached_cost()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - When cachedInputTokens is 0, cached line should still be logged (showing 0 tokens)
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cached Input Tokens: 0")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_logs_correct_token_counts_and_prices()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 2_500_000,
            outputTokens: 1_250_000,
            cachedInputTokens: 1_000_000);
        
        // Act
        service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify correct token counts are logged
        // o3: $2.00/1M input, $8.00/1M output, $0.50/1M cached
        // Uncached: 1,500,000 tokens
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Uncached Input Tokens: 1,500,000")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Cached Input Tokens: 1,000,000")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Output Tokens: 1,250,000")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        return Task.CompletedTask;
    }

    private static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens)
    {
        var usage = Substitute.For<ChatTokenUsage>();
        usage.InputTokenCount.Returns(inputTokens);
        usage.OutputTokenCount.Returns(outputTokens);
        
        if (cachedInputTokens > 0)
        {
            var inputDetails = Substitute.For<ChatInputTokenUsageDetails>();
            inputDetails.CachedTokenCount.Returns(cachedInputTokens);
            usage.InputTokenDetails.Returns(inputDetails);
        }
        
        return usage;
    }
}
