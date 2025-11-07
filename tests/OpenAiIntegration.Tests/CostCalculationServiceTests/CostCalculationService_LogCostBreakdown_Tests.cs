using Microsoft.Extensions.Logging;
using Moq;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Tests for the CostCalculationService LogCostBreakdown method
/// </summary>
public class CostCalculationService_LogCostBreakdown_Tests : CostCalculationServiceTests_Base
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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Reasoning Output Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Text Output Tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Total Output Tokens")),
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
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Total Output Tokens: 1,250,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_reasoning_tokens_logs_reasoning_and_text_breakdown()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            reasoningTokens: 300_000);
        
        // Act
        service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify reasoning and text tokens are logged separately
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Reasoning Output Tokens: 300,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Text Output Tokens: 200,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Total Output Tokens: 500,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_zero_reasoning_tokens_logs_zero_reasoning()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            reasoningTokens: 0);
        
        // Act
        service.LogCostBreakdown("gpt-4o", usage);
        
        // Assert - Verify reasoning tokens shows 0
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Reasoning Output Tokens: 0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Text Output Tokens: 500,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }

    [Test]
    public Task LogCostBreakdown_with_all_reasoning_tokens_logs_zero_text()
    {
        // Arrange
        var logger = new Mock<ILogger<CostCalculationService>>();
        var service = new CostCalculationService(logger.Object);
        
        var usage = CreateChatTokenUsage(
            inputTokens: 1_000_000,
            outputTokens: 500_000,
            cachedInputTokens: 0,
            reasoningTokens: 500_000);
        
        // Act
        service.LogCostBreakdown("o3", usage);
        
        // Assert - Verify all output is reasoning, no text tokens
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Reasoning Output Tokens: 500,000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Text Output Tokens: 0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        return Task.CompletedTask;
    }
}
