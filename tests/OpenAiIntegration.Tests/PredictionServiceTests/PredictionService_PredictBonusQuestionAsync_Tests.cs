using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
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

    private static ResponsesClient CreateProtocolBonusChatClient(
        List<string?> requestedServiceTiers,
        Exception? firstException = null,
        string responseServiceTier = "flex")
    {
        var mockClient = new Mock<ResponsesClient>("test-api-key");
        var callCount = 0;

        mockClient
            .Setup(client => client.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateResponseOptions, CancellationToken>((options, _) =>
            {
                callCount += 1;
                var payloadJson = ReadPayloadJson(options);
                requestedServiceTiers.Add(ExtractStringProperty(payloadJson, "service_tier"));
                if (callCount == 1 && firstException is not null)
                {
                    return Task.FromException<ClientResult<ResponseResult>>(firstException);
                }

                return Task.FromResult(CreateResponseClientResult(
                    """{"selectedOptionIds":["opt1"]}""",
                    OpenAITestHelpers.CreateChatTokenUsage(800, 30, cachedInputTokens: 125, outputReasoningTokens: 4),
                    responseServiceTier));
            });

        return mockClient.Object;
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
        await Assert.That(requestedServiceTiers[1]).IsEqualTo("default");
    }

    [Test]
    public async Task Predicting_bonus_question_retries_plain_rate_limit_with_default_processing_after_flex_failure()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(
            requestedServiceTiers,
            firstException: CreateRateLimitExceededException(),
            responseServiceTier: "standard");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new BonusPrediction(["opt1"]));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(2);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedServiceTiers[1]).IsEqualTo("default");
    }

    [Test]
    public async Task Predicting_bonus_question_does_not_retry_insufficient_quota_rate_limit()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolBonusChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(
                429,
                reasonPhrase: "Too Many Requests",
                body: """{"error":{"code":"insufficient_quota","message":"You exceeded your current quota, please check your plan and billing details."}}"""));
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
            tracker => tracker.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 800 && actual.OutputTokenCount == 30)),
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
        var activity = capturedActivities.Last(candidate =>
            candidate.OperationName == "predict-bonus" &&
            candidate.GetTagItem("langfuse.observation.metadata.openaiExecutionStrategy") is not null &&
            candidate.GetTagItem("langfuse.observation.cost_details") is not null);
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
