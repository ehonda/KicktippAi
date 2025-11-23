using TestUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
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
        var exception = await Assert.That(() => CreateService(NullableOption.Some<ChatClient>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("chatClient");
    }

    [Test]
    public async Task Creating_service_with_null_logger_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(logger: NullableOption.Some<FakeLogger<PredictionService>>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("logger");
    }

    [Test]
    public async Task Creating_service_with_null_costCalculationService_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(costCalculationService: NullableOption.Some<ICostCalculationService>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("costCalculationService");
    }

    [Test]
    public async Task Creating_service_with_null_tokenUsageTracker_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(tokenUsageTracker: NullableOption.Some<ITokenUsageTracker>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("tokenUsageTracker");
    }

    [Test]
    public async Task Creating_service_with_null_templateProvider_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(templateProvider: NullableOption.Some<IInstructionsTemplateProvider>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("templateProvider");
    }

    [Test]
    public async Task Creating_service_with_null_model_throws_ArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.That(() => CreateService(model: NullableOption.Some<string>(null)))
            .Throws<ArgumentNullException>();
        
        await Assert.That(exception!.ParamName).IsEqualTo("model");
    }
}
