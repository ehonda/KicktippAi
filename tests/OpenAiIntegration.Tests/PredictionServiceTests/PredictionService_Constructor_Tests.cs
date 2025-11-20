using TestUtilities;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Core;
using EHonda.Optional.Core;
using OpenAI.Chat;
using EHonda.KicktippAi.Core;

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
        var exception = await Assert.That(() => CreateService(chatClient: Option.Some<ChatClient>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("chatClient");
    }

    [Test]
    public async Task Creating_service_with_null_logger_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(logger: Option.Some<Microsoft.Extensions.Logging.Testing.FakeLogger<PredictionService>>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("logger");
    }

    [Test]
    public async Task Creating_service_with_null_costCalculationService_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(costCalculationService: Option.Some<ICostCalculationService>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("costCalculationService");
    }

    [Test]
    public async Task Creating_service_with_null_tokenUsageTracker_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(tokenUsageTracker: Option.Some<ITokenUsageTracker>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("tokenUsageTracker");
    }

    [Test]
    public async Task Creating_service_with_null_templateProvider_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(templateProvider: Option.Some<IInstructionsTemplateProvider>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("templateProvider");
    }

    [Test]
    public async Task Creating_service_with_null_model_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(model: Option.Some<string>(null!)))
        .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("model");
    }
}
