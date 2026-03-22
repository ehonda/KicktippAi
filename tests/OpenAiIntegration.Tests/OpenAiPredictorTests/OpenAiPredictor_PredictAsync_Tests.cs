using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAI.Chat;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Core;

namespace OpenAiIntegration.Tests.OpenAiPredictorTests;

public class OpenAiPredictor_PredictAsync_Tests : OpenAiPredictorTests_Base
{
    [Test]
    public async Task Predicting_with_valid_score_returns_parsed_prediction()
    {
        var logger = new FakeLogger<OpenAiPredictor>();
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClient(responseText: "2-1"),
            logger: logger);

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(2);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(logger).ContainsLog(LogLevel.Information, "Prediction generated");
    }

    [Test]
    public async Task Predicting_with_embedded_score_parses_first_match()
    {
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClient(responseText: "Expected result: 3-2 after a close match."));

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(3);
        await Assert.That(prediction.AwayGoals).IsEqualTo(2);
    }

    [Test]
    public async Task Predicting_with_out_of_range_score_returns_fallback_prediction()
    {
        var logger = new FakeLogger<OpenAiPredictor>();
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClient(responseText: "12-0"),
            logger: logger);

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(logger).ContainsLog(LogLevel.Warning, "out of reasonable range");
    }

    [Test]
    public async Task Predicting_with_unparseable_score_returns_fallback_prediction()
    {
        var logger = new FakeLogger<OpenAiPredictor>();
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClient(responseText: "Bayern should win comfortably."),
            logger: logger);

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(logger).ContainsLog(LogLevel.Warning, "Could not parse score");
    }

    [Test]
    public async Task Predicting_with_empty_response_returns_fallback_prediction()
    {
        var logger = new FakeLogger<OpenAiPredictor>();
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClient(responseText: " "),
            logger: logger);

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(logger).ContainsLog(LogLevel.Warning, "Empty content");
    }

    [Test]
    public async Task Predicting_with_client_exception_returns_fallback_prediction_and_logs_error()
    {
        var logger = new FakeLogger<OpenAiPredictor>();
        var mockClient = new Mock<ChatClient>();
        mockClient.Setup(client => client.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var predictor = CreatePredictor(
            chatClient: mockClient.Object,
            logger: logger);

        var prediction = await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(prediction.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(logger).ContainsLog(LogLevel.Error, "Error generating prediction");
        await Assert.That(logger).ContainsLog(LogLevel.Warning, "Returning fallback prediction");
    }

    [Test]
    public async Task Predicting_builds_prompt_with_match_details()
    {
        IReadOnlyList<ChatMessage> capturedMessages = [];
        var predictor = CreatePredictor(
            chatClient: CreateMockChatClientWithCapture(messages => capturedMessages = messages));

        await predictor.PredictAsync(CreateTestMatch(), CreateTestContext());

        await Assert.That(capturedMessages.Count).IsEqualTo(1);
        var prompt = ((UserChatMessage)capturedMessages[0]).Content[0].Text;
        await Assert.That(prompt).Contains("FC Bayern München vs Borussia Dortmund");
        await Assert.That(prompt).Contains("2025-03-15 14:30");
        await Assert.That(prompt).Contains("HOME_GOALS-AWAY_GOALS");
    }
}
