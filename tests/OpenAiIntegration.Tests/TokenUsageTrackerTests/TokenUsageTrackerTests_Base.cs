using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAI.Chat;
using TestUtilities;
using EHonda.Optional.Core;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Base class for TokenUsageTracker tests providing shared helper functionality
/// </summary>
public abstract class TokenUsageTrackerTests_Base
{
    /// <summary>
    /// Factory method to create a TokenUsageTracker instance using the configured dependencies
    /// </summary>
    protected static TokenUsageTracker CreateTracker(
        NullableOption<FakeLogger<TokenUsageTracker>> logger = default,
        NullableOption<ICostCalculationService> costCalculationService = default)
    {
        var actualLogger = logger.Or(CreateFakeLogger);
        var actualCostService = costCalculationService.Or(() => CreateMockCostCalculationService().Object);

        return new TokenUsageTracker(actualLogger!, actualCostService!);
    }

    /// <summary>
    /// Creates a FakeLogger for TokenUsageTracker
    /// </summary>
    protected static FakeLogger<TokenUsageTracker> CreateFakeLogger()
    {
        return new FakeLogger<TokenUsageTracker>();
    }

    /// <summary>
    /// Creates a mock ICostCalculationService with optional default return value
    /// </summary>
    protected static Mock<ICostCalculationService> CreateMockCostCalculationService(
        NullableOption<decimal?> costToReturn = default)
    {
        var actualCost = costToReturn.Or(1.23m);
        var mock = new Mock<ICostCalculationService>();
        mock.Setup(x => x.CalculateCost(It.IsAny<string>(), It.IsAny<ChatTokenUsage>()))
            .Returns(actualCost);
        return mock;
    }
}
