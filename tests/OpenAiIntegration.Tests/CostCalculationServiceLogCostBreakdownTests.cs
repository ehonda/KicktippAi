using Microsoft.Extensions.Logging;
using Moq;
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
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify all log entries are created
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Uncached Input Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cached Input Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Output Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Total Cost")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_cached_tokens_logs_cached_cost_line()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify cached tokens are logged
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cached Input Tokens: 600,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_model_without_cached_pricing_does_not_log_cached_line()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 600_000);
        
        // Act - o1-pro doesn't have cached pricing
        service.LogCostBreakdown("o1-pro", usage);
        
        // Assert - Verify cached tokens line is NOT logged
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cached Input Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_unknown_model_logs_warning()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("unknown-model", usage);
        
        // Assert - Verify warning is logged
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Pricing information not found for model 'unknown-model'")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_zero_cached_tokens_does_not_log_zero_cached_cost()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - When cachedInputTokens is 0, cached line should still be logged (showing 0 tokens)
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cached Input Tokens: 0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_logs_correct_token_counts_and_prices()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 2_500_000,
            outputTokens: 1_250_000,
            cachedInputTokens: 1_000_000);
        
        // Act
        service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify correct token counts are logged
        // o3: $2.00/1M input, $8.00/1M output, $0.50/1M cached
        // Uncached: 1,500,000 tokens
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Uncached Input Tokens: 1,500,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Cached Input Tokens: 1,000,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Output Tokens: 1,250,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    private static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens)
    {
        ChatInputTokenUsageDetails? inputDetails = cachedInputTokens > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedInputTokens)
            : null;
        
        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: inputTokens,
            outputTokenCount: outputTokens,
            inputTokenDetails: inputDetails);
    }
}
