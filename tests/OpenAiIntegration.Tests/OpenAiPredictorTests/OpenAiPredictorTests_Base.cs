using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAI.Responses;
using Match = EHonda.KicktippAi.Core.Match;

namespace OpenAiIntegration.Tests.OpenAiPredictorTests;

public abstract class OpenAiPredictorTests_Base
{
    protected static OpenAiPredictor CreatePredictor(
        NullableOption<ResponsesClient> chatClient = default,
        NullableOption<FakeLogger<OpenAiPredictor>> logger = default)
    {
        var actualResponsesClient = chatClient.Or(() => CreateMockChatClient());
        var actualLogger = logger.Or(() => new FakeLogger<OpenAiPredictor>());

        return new OpenAiPredictor(actualResponsesClient!, actualLogger!, "gpt-5");
    }

    protected static Match CreateTestMatch(
        string homeTeam = "FC Bayern München",
        string awayTeam = "Borussia Dortmund",
        int matchday = 25)
    {
        return new Match(
            homeTeam,
            awayTeam,
            Instant.FromUtc(2025, 3, 15, 14, 30).InUtc(),
            matchday);
    }

    protected static PredictorContext CreateTestContext()
    {
        return PredictorContext.CreateBasic();
    }

    protected static ResponsesClient CreateMockChatClient(
        Option<string> responseText = default,
        Option<ClientResult<ResponseResult>> response = default)
    {
        var actualResponse = response.Or(() =>
        {
            var mockResult = new Mock<ClientResult<ResponseResult>>(null!, Mock.Of<PipelineResponse>());
            var result = CreateResponseResult(responseText.Or("2-1"));

            mockResult.SetupGet(candidate => candidate.Value).Returns(result);
            return mockResult.Object;
        });

        var mockClient = new Mock<ResponsesClient>("test-api-key");
        mockClient.Setup(client => client.CreateResponseAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualResponse);

        return mockClient.Object;
    }

    protected static ResponsesClient CreateMockChatClientWithCapture(
        Action<string> capturePrompt,
        Option<string> responseText = default)
    {
        var mockResult = new Mock<ClientResult<ResponseResult>>(null!, Mock.Of<PipelineResponse>());
        var result = CreateResponseResult(responseText.Or("2-1"));

        mockResult.SetupGet(candidate => candidate.Value).Returns(result);

        var mockClient = new Mock<ResponsesClient>("test-api-key");
        mockClient.Setup(client => client.CreateResponseAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((_, prompt, _, _) =>
                capturePrompt(prompt))
            .ReturnsAsync(mockResult.Object);

        return mockClient.Object;
    }

    private static ResponseResult CreateResponseResult(string responseText)
    {
        var responseTextJson = JsonSerializer.Serialize(responseText);
        var responseContent = BinaryData.FromString($$"""
            {
              "id": "resp-test",
              "object": "response",
              "created_at": 1760000000,
              "status": "completed",
              "model": "gpt-5",
              "output": [
                {
                  "id": "msg-test",
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    {
                      "type": "output_text",
                      "text": {{responseTextJson}},
                      "annotations": []
                    }
                  ]
                }
              ]
            }
            """);

        return ModelReaderWriter.Read<ResponseResult>(responseContent, ModelReaderWriterOptions.Json)!;
    }
}
