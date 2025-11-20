using TestUtilities;
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
    public async Task Creating_service_with_null_chatClient_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            null!,
            CreateFakeLogger(),
            CreateMockCostCalculationService().Object,
            CreateMockTokenUsageTracker().Object,
            CreateMockTemplateProvider().Object,
            "gpt-5"))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("chatClient");
    }

    [Test]
    public async Task Creating_service_with_null_logger_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            CreateMockChatClient("{}", OpenAITestHelpers.CreateChatTokenUsage(0, 0)),
            null!,
            CreateMockCostCalculationService().Object,
            CreateMockTokenUsageTracker().Object,
            CreateMockTemplateProvider().Object,
            "gpt-5"))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("logger");
    }

    [Test]
    public async Task Creating_service_with_null_costCalculationService_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            CreateMockChatClient("{}", OpenAITestHelpers.CreateChatTokenUsage(0, 0)),
            CreateFakeLogger(),
            null!,
            CreateMockTokenUsageTracker().Object,
            CreateMockTemplateProvider().Object,
            "gpt-5"))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("costCalculationService");
    }

    [Test]
    public async Task Creating_service_with_null_tokenUsageTracker_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            CreateMockChatClient("{}", OpenAITestHelpers.CreateChatTokenUsage(0, 0)),
            CreateFakeLogger(),
            CreateMockCostCalculationService().Object,
            null!,
            CreateMockTemplateProvider().Object,
            "gpt-5"))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("tokenUsageTracker");
    }

    [Test]
    public async Task Creating_service_with_null_templateProvider_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            CreateMockChatClient("{}", OpenAITestHelpers.CreateChatTokenUsage(0, 0)),
            CreateFakeLogger(),
            CreateMockCostCalculationService().Object,
            CreateMockTokenUsageTracker().Object,
            null!,
            "gpt-5"))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("templateProvider");
    }

    [Test]
    public async Task Creating_service_with_null_model_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => new PredictionService(
            CreateMockChatClient("{}", OpenAITestHelpers.CreateChatTokenUsage(0, 0)),
            CreateFakeLogger(),
            CreateMockCostCalculationService().Object,
            CreateMockTokenUsageTracker().Object,
            CreateMockTemplateProvider().Object,
            null!))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("model");
    }
}
