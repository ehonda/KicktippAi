using Microsoft.Extensions.Logging;
using Moq;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker constructor
/// </summary>
public class TokenUsageTracker_Constructor_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task Constructor_with_valid_parameters_creates_instance()
    {
        // Arrange
        var logger = new Mock<ILogger<TokenUsageTracker>>();
        var costService = new Mock<ICostCalculationService>();

        // Act
        var tracker = new TokenUsageTracker(logger.Object, costService.Object);

        // Assert
        await Assert.That(tracker).IsNotNull();
    }

    [Test]
    public async Task Constructor_with_null_logger_throws_ArgumentNullException()
    {
        // Arrange
        var costService = new Mock<ICostCalculationService>();

        // Act & Assert
        await Assert.That(() => new TokenUsageTracker(null!, costService.Object))
            .Throws<ArgumentNullException>()
            .And.HasProperty(x => x.ParamName, "logger");
    }

    [Test]
    public async Task Constructor_with_null_cost_service_throws_ArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<TokenUsageTracker>>();

        // Act & Assert
        await Assert.That(() => new TokenUsageTracker(logger.Object, null!))
            .Throws<ArgumentNullException>()
            .And.HasProperty(x => x.ParamName, "costCalculationService");
    }

    [Test]
    public async Task Constructor_initializes_with_zero_usage()
    {
        // Arrange
        var logger = new Mock<ILogger<TokenUsageTracker>>();
        var costService = new Mock<ICostCalculationService>();

        // Act
        var tracker = new TokenUsageTracker(logger.Object, costService.Object);

        // Assert
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetLastCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetCompactSummary()).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
        await Assert.That(tracker.GetLastUsageJson()).IsNull();
    }
}
