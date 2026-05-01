using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TestUtilities;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Core;
using EHonda.Optional.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictBonusQuestionAsync method
/// </summary>
public class PredictionService_PredictBonusQuestionAsync_Tests : PredictionServiceTests_Base
{
    /// <summary>
    /// Helper method to call PredictBonusQuestionAsync with optional parameters that default to test helpers
    /// </summary>
    private Task<BonusPrediction?> PredictBonusQuestionAsync(
        Option<PredictionService> service = default,
        Option<BonusQuestion> bonusQuestion = default,
        Option<IEnumerable<DocumentContext>> contextDocuments = default,
        CancellationToken cancellationToken = default)
    {
        var actualService = service.Or(() => CreateService());
        var actualBonusQuestion = bonusQuestion.Or(() => CreateTestBonusQuestion());
        var actualContextDocs = contextDocuments.Or(CreateTestContextDocuments);
        
        return actualService.PredictBonusQuestionAsync(
            actualBonusQuestion,
            actualContextDocs,
            cancellationToken: cancellationToken);
    }

    private static ActivityListener CreateActivityListener(List<Activity> capturedActivities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "KicktippAi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = capturedActivities.Add
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ChatClient CreateProtocolBonusChatClient(
        List<string?> requestedServiceTiers,
        Exception? firstException = null,
        string responseServiceTier = "flex")
    {
        var mockClient = new Mock<ChatClient>();
        var callCount = 0;

        mockClient
            .Setup(client => client.CompleteChatAsync(
                It.IsAny<BinaryContent>(),
                It.IsAny<RequestOptions>()))
            .Returns<BinaryContent, RequestOptions>((content, _) =>
            {
                callCount += 1;
                var payloadJson = ReadPayloadJson(content);
                requestedServiceTiers.Add(ExtractStringProperty(payloadJson, "service_tier"));
                if (callCount == 1 && firstException is not null)
                {
                    return Task.FromException<ClientResult>(firstException);
                }

                return Task.FromResult(CreateProtocolClientResult(responseServiceTier));
            });

        return mockClient.Object;
    }

    private static string ReadPayloadJson(BinaryContent content)
    {
        using var stream = new MemoryStream();
        content.WriteTo(stream, CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? ExtractStringProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static ClientResult CreateProtocolClientResult(string serviceTier)
    {
        var response = new Mock<PipelineResponse>();
        response.SetupGet(candidate => candidate.Status).Returns(200);
        response.SetupGet(candidate => candidate.Content).Returns(BinaryData.FromString($$"""
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1760000000,
              "model": "gpt-5",
              "service_tier": "{{serviceTier}}",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "{\"selectedOptionIds\":[\"opt1\"]}"
                  },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 800,
                "completion_tokens": 30,
                "total_tokens": 830,
                "prompt_tokens_details": {
                  "cached_tokens": 125
                },
                "completion_tokens_details": {
                  "reasoning_tokens": 4
                }
              }
            }
            """));
        return ClientResult.FromResponse(response.Object);
    }

    private static ClientResultException CreateClientResultException(
        int status,
        string reasonPhrase = "Resource Unavailable",
        string body = """{"error":{"code":"resource_unavailable","message":"capacity unavailable"}}""")
    {
        var response = new Mock<PipelineResponse>();
        response.SetupGet(candidate => candidate.Status).Returns(status);
        response.SetupGet(candidate => candidate.ReasonPhrase).Returns(reasonPhrase);
        response.SetupGet(candidate => candidate.Content).Returns(BinaryData.FromString(body));
        return new ClientResultException("OpenAI request failed", response.Object, null);
    }

    [Test]
    public async Task Predicting_bonus_question_with_single_selection_returns_prediction()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1"]}""");
        var service = CreateService(chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);

        // Act
        var prediction = await PredictBonusQuestionAsync(service, bonusQuestion);

        // Assert
        var expected = new BonusPrediction(["opt1"]);
        await Assert.That(prediction).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Predicting_bonus_question_with_multiple_selections_returns_prediction()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt2"]}""");
        var service = CreateService(chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);

        // Act
        var prediction = await PredictBonusQuestionAsync(service, bonusQuestion);

        // Assert
        var expected = new BonusPrediction(["opt1", "opt2"]);
        await Assert.That(prediction).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Predicting_bonus_question_calls_token_tracker_with_correct_usage()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt2"]}""", usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(chatClient, tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object));

        // Act
        await PredictBonusQuestionAsync(service);

        // Assert
        tokenUsageTracker.Verify(
            t => t.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 800 && actual.OutputTokenCount == 30),
                "flex"),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt3"]}""", usage);
        var costCalculationService = CreateMockCostCalculationService();
        var service = CreateService(chatClient, costCalculationService: NullableOption.Some(costCalculationService.Object));

        // Act
        await PredictBonusQuestionAsync(service);

        // Assert
        costCalculationService.Verify(
            c => c.LogCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 800 && actual.OutputTokenCount == 30),
                "flex"),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_with_empty_context_documents_succeeds()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1"]}""");
        var service = CreateService(chatClient);
        var emptyContextDocs = new List<DocumentContext>();

        // Act
        var prediction = await PredictBonusQuestionAsync(service, contextDocuments: emptyContextDocs);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.SelectedOptionIds.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Predicting_bonus_question_logs_information_message()
    {
        // Arrange
        var logger = CreateFakeLogger();
        var service = CreateService(logger: logger);

        // Act
        await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(logger).ContainsLog(LogLevel.Information, "Generating prediction for bonus question");
    }

    [Test]
    public async Task Predicting_bonus_question_with_default_options_uses_flex_processing()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(requestedServiceTiers);
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new BonusPrediction(["opt1"]));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
    }

    [Test]
    public async Task Predicting_bonus_question_with_flex_processing_retries_standard_after_capacity_failure()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(429),
            responseServiceTier: "standard");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new BonusPrediction(["opt1"]));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(2);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedServiceTiers[1]).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_flex_processing_does_not_retry_plain_rate_limit()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(
                429,
                reasonPhrase: "Too Many Requests",
                body: """{"error":{"code":"rate_limit_exceeded","message":"rate limit exceeded"}}"""));
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
    }

    [Test]
    public async Task Predicting_bonus_question_with_standard_processing_option_disables_flex_processing()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1"]}""", usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(
            chatClient,
            tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object),
            options: NullableOption.Some(PredictionServiceOptions.StandardProcessing));

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new BonusPrediction(["opt1"]));
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage("gpt-5", usage),
            Times.Once);
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage("gpt-5", It.IsAny<ChatTokenUsage>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_bonus_question_with_flex_processing_records_usage_cost_and_langfuse_tier_metadata()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(requestedServiceTiers);
        var costCalculationService = CreateMockCostCalculationService();
        costCalculationService
            .Setup(service => service.CalculateCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage =>
                    usage.InputTokenCount == 800 &&
                    usage.OutputTokenCount == 30 &&
                    usage.InputTokenDetails!.CachedTokenCount == 125),
                "flex"))
            .Returns(new CostBreakdown(0.001m, 0.0001m, 0.002m, 0.0031m));
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(
            chatClient,
            costCalculationService: NullableOption.Some(costCalculationService.Object),
            tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object));
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new BonusPrediction(["opt1"]));
        var activity = capturedActivities.Single(candidate => candidate.OperationName == "predict-bonus");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiExecutionStrategy"))
            .IsEqualTo("flex-first-standard-fallback");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiRequestedServiceTier"))
            .IsEqualTo("flex");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiFinalServiceTier"))
            .IsEqualTo("flex");
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")?.ToString())
            .Contains("\"total\":0.0031");
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage => usage.InputTokenCount == 800 && usage.OutputTokenCount == 30),
                "flex"),
            Times.Once);
        costCalculationService.Verify(
            service => service.LogCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage => usage.InputTokenCount == 800 && usage.OutputTokenCount == 30),
                "flex"),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_with_API_exception_returns_null()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_exception_logs_error()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var logger = CreateFakeLogger();
        var service = CreateService(chatClient, logger: logger);

        // Act
        await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(logger).ContainsLog(LogLevel.Error, "Error generating bonus prediction");
    }

    [Test]
    public async Task Predicting_bonus_question_with_invalid_option_id_returns_null()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["invalid-option"]}""");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_duplicate_selections_returns_null()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt1"]}""");
        var service = CreateService(chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);

        // Act
        var prediction = await PredictBonusQuestionAsync(service, bonusQuestion);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_wrong_selection_count_returns_null()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt2"]}""");
        var service = CreateService(chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);

        // Act
        var prediction = await PredictBonusQuestionAsync(service, bonusQuestion);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_invalid_JSON_returns_null()
    {
        // Arrange
        var invalidJson = """not valid json""";
        var chatClient = CreateMockChatClient(invalidJson);
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_empty_selection_array_returns_null()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"selectedOptionIds": []}""");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_null_json_object_returns_null()
    {
        // Arrange
        var chatClient = CreateMockChatClient("null");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }
}
