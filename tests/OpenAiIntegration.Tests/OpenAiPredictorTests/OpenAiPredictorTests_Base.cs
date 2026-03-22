using System.ClientModel;
using System.ClientModel.Primitives;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAI.Chat;
using Match = EHonda.KicktippAi.Core.Match;

namespace OpenAiIntegration.Tests.OpenAiPredictorTests;

public abstract class OpenAiPredictorTests_Base
{
    protected static OpenAiPredictor CreatePredictor(
        NullableOption<ChatClient> chatClient = default,
        NullableOption<FakeLogger<OpenAiPredictor>> logger = default)
    {
        var actualChatClient = chatClient.Or(() => CreateMockChatClient());
        var actualLogger = logger.Or(() => new FakeLogger<OpenAiPredictor>());

        return new OpenAiPredictor(actualChatClient!, actualLogger!);
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

    protected static ChatClient CreateMockChatClient(
        Option<string> responseText = default,
        Option<ClientResult<ChatCompletion>> response = default)
    {
        var actualResponse = response.Or(() =>
        {
            var mockResult = new Mock<ClientResult<ChatCompletion>>(null!, Mock.Of<PipelineResponse>());
            var completion = OpenAIChatModelFactory.ChatCompletion(
                id: "test-completion-id",
                model: "gpt-5",
                createdAt: DateTimeOffset.UtcNow,
                finishReason: ChatFinishReason.Stop,
                role: ChatMessageRole.Assistant,
                content: [ChatMessageContentPart.CreateTextPart(responseText.Or("2-1"))]);

            mockResult.SetupGet(result => result.Value).Returns(completion);
            return mockResult.Object;
        });

        var mockClient = new Mock<ChatClient>();
        mockClient.Setup(client => client.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualResponse);

        return mockClient.Object;
    }

    protected static ChatClient CreateMockChatClientWithCapture(
        Action<IReadOnlyList<ChatMessage>> captureMessages,
        Option<string> responseText = default)
    {
        var mockResult = new Mock<ClientResult<ChatCompletion>>(null!, Mock.Of<PipelineResponse>());
        var completion = OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-5",
            createdAt: DateTimeOffset.UtcNow,
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(responseText.Or("2-1"))]);

        mockResult.SetupGet(result => result.Value).Returns(completion);

        var mockClient = new Mock<ChatClient>();
        mockClient.Setup(client => client.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatCompletionOptions, CancellationToken>((messages, _, _) =>
                captureMessages(messages.ToList()))
            .ReturnsAsync(mockResult.Object);

        return mockClient.Object;
    }
}
