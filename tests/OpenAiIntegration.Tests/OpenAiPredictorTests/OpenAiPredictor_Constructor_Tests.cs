using EHonda.Optional.Core;
using Microsoft.Extensions.Logging.Testing;
using OpenAI.Chat;
using TUnit.Core;

namespace OpenAiIntegration.Tests.OpenAiPredictorTests;

public class OpenAiPredictor_Constructor_Tests : OpenAiPredictorTests_Base
{
    [Test]
    public async Task Creating_predictor_with_valid_dependencies_succeeds()
    {
        var predictor = CreatePredictor();

        await Assert.That(predictor).IsNotNull();
    }

    [Test]
    public async Task Creating_predictor_with_null_client_throws()
    {
        await Assert.That(() => CreatePredictor(chatClient: NullableOption.Some<ChatClient>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Test]
    public async Task Creating_predictor_with_null_logger_throws()
    {
        await Assert.That(() => CreatePredictor(logger: NullableOption.Some<FakeLogger<OpenAiPredictor>>(null)))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
