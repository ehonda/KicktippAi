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
        var logger = CreateFakeLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        var templateProvider = CreateMockTemplateProvider();
        const string model = "gpt-5";

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            null!,
            logger,
            costCalc.Object,
            tokenTracker.Object,
            templateProvider.Object,
            model))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("chatClient");
    }

    [Test]
    public async Task Constructor_with_null_logger_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        var templateProvider = CreateMockTemplateProvider();
        const string model = "gpt-5";

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            chatClient,
            null!,
            costCalc.Object,
            tokenTracker.Object,
            templateProvider.Object,
            model))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("logger");
    }

    [Test]
    public async Task Constructor_with_null_costCalculationService_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateFakeLogger();
        var tokenTracker = CreateMockTokenUsageTracker();
        var templateProvider = CreateMockTemplateProvider();
        const string model = "gpt-5";

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            chatClient,
            logger,
            null!,
            tokenTracker.Object,
            templateProvider.Object,
            model))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("costCalculationService");
    }

    [Test]
    public async Task Constructor_with_null_tokenUsageTracker_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateFakeLogger();
        var costCalc = CreateMockCostCalculationService();
        var templateProvider = CreateMockTemplateProvider();
        const string model = "gpt-5";

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            chatClient,
            logger,
            costCalc.Object,
            null!,
            templateProvider.Object,
            model))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("tokenUsageTracker");
    }

    [Test]
    public async Task Constructor_with_null_templateProvider_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateFakeLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        const string model = "gpt-5";

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            chatClient,
            logger,
            costCalc.Object,
            tokenTracker.Object,
            null!,
            model))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("templateProvider");
    }

    [Test]
    public async Task Constructor_with_null_model_throws_ArgumentNullException()
    {
        // Arrange
        var chatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
        var logger = CreateFakeLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();
        var templateProvider = CreateMockTemplateProvider();

        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            chatClient,
            logger,
            costCalc.Object,
            tokenTracker.Object,
            templateProvider.Object,
            null!))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("model");
    }
}
