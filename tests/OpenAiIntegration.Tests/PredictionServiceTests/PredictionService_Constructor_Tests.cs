using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService constructor
/// </summary>
public class PredictionService_Constructor_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task Constructor_with_null_chatClient_throws_ArgumentNullException()
    {
        // Arrange
        var logger = CreateMockLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        const string model = "gpt-5";

        // Act & Assert
        await Assert.That(() => new PredictionService(
            null!,
            logger.Object,
            costCalc.Object,
            tokenTracker.Object,
            model))
        .Throws<ArgumentNullException>()
        .WithMessage("*chatClient*");
    }

    [Test]
    public async Task Constructor_with_null_logger_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        const string model = "gpt-5";

        // Act & Assert
        await Assert.That(() => new PredictionService(
            chatClient,
            null!,
            costCalc.Object,
            tokenTracker.Object,
            model))
        .Throws<ArgumentNullException>()
        .WithMessage("*logger*");
    }

    [Test]
    public async Task Constructor_with_null_costCalculationService_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateMockLogger();
        var tokenTracker = CreateMockTokenUsageTracker();
        const string model = "gpt-5";

        // Act & Assert
        await Assert.That(() => new PredictionService(
            chatClient,
            logger.Object,
            null!,
            tokenTracker.Object,
            model))
        .Throws<ArgumentNullException>()
        .WithMessage("*costCalculationService*");
    }

    [Test]
    public async Task Constructor_with_null_tokenUsageTracker_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateMockLogger();
        var costCalc = CreateMockCostCalculationService();
        const string model = "gpt-5";

        // Act & Assert
        await Assert.That(() => new PredictionService(
            chatClient,
            logger.Object,
            costCalc.Object,
            null!,
            model))
        .Throws<ArgumentNullException>()
        .WithMessage("*tokenUsageTracker*");
    }

    [Test]
    public async Task Constructor_with_null_model_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateMockLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();

        // Act & Assert
        await Assert.That(() => new PredictionService(
            chatClient,
            logger.Object,
            costCalc.Object,
            tokenTracker.Object,
            null!))
        .Throws<ArgumentNullException>()
        .WithMessage("*model*");
    }
}
