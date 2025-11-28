using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using EHonda.Optional.Core;

namespace OpenAiIntegration.Tests.TokenUsageTrackerTests;

/// <summary>
/// Tests for the TokenUsageTracker constructor
/// </summary>
public class TokenUsageTracker_Constructor_Tests : TokenUsageTrackerTests_Base
{
    [Test]
    public async Task Creating_tracker_with_valid_parameters_creates_instance()
    {
        // Act
        var tracker = CreateTracker();

        // Assert
        await Assert.That(tracker).IsNotNull();
    }

    [Test]
    public async Task Creating_tracker_with_null_logger_throws_ArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => CreateTracker(NullableOption.Some<FakeLogger<TokenUsageTracker>>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public async Task Creating_tracker_with_null_cost_service_throws_ArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => CreateTracker(costCalculationService: NullableOption.Some<ICostCalculationService>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("costCalculationService");
    }

    [Test]
    public async Task Creating_tracker_initializes_with_zero_usage()
    {
        // Act
        var tracker = CreateTracker();

        // Assert
        await Assert.That(tracker.GetTotalCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetLastCost()).IsEqualTo(0m);
        await Assert.That(tracker.GetCompactSummary()).IsEqualTo("0 / 0 / 0 / 0 / $0.0000");
        await Assert.That(tracker.GetLastUsageJson()).IsNull();
    }
}
