using EHonda.KicktippAi.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using TestUtilities;
using TestUtilities.FakeLoggerAssertions;
using EHonda.Optional.Core;
using TUnit.Core;
using Match = EHonda.KicktippAi.Core.Match;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictMatchAsync method
/// </summary>
public class PredictionService_PredictMatchAsync_Tests : PredictionServiceTests_Base
{
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

    private static bool IsMatchingPredictMatchActivity(Activity candidate, PredictionTelemetryMetadata telemetryMetadata)
    {
        return candidate.OperationName == "predict-match" &&
               candidate.GetTagItem("langfuse.observation.type") is not null &&
               candidate.GetTagItem("langfuse.observation.metadata.homeTeam") is string homeTeam &&
               homeTeam == telemetryMetadata.HomeTeam &&
               candidate.GetTagItem("langfuse.observation.metadata.awayTeam") is string awayTeam &&
               awayTeam == telemetryMetadata.AwayTeam &&
               candidate.GetTagItem("langfuse.observation.metadata.repredictionIndex") is string repredictionIndex &&
               repredictionIndex == "3";
    }

    private static ResponsesClient CreateProtocolChatClient(
        List<string?> requestedServiceTiers,
        List<string?>? requestedReasoningEfforts = null,
        List<string>? requestPayloads = null,
        Exception? firstException = null,
        string responseServiceTier = "standard")
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
                requestPayloads?.Add(payloadJson);
                requestedServiceTiers.Add(ExtractStringProperty(payloadJson, "service_tier"));
                requestedReasoningEfforts?.Add(ExtractReasoningEffort(payloadJson));
                if (callCount == 1 && firstException is not null)
                {
                    return Task.FromException<ClientResult<ResponseResult>>(firstException);
                }

                return Task.FromResult(CreateResponseClientResult(
                    """{"home": 2, "away": 1}""",
                    OpenAITestHelpers.CreateChatTokenUsage(1000, 50, cachedInputTokens: 250, outputReasoningTokens: 7),
                    responseServiceTier));
            });

        return mockClient.Object;
    }

    private static string? ExtractReasoningEffort(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("reasoning", out var reasoning)
               && reasoning.TryGetProperty("effort", out var effort)
               && effort.ValueKind == JsonValueKind.String
            ? effort.GetString()
            : null;
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

    /// <summary>
    /// Helper method to call PredictMatchAsync with optional parameters that default to test helpers
    /// </summary>
    private Task<Prediction?> PredictMatchAsync(
        Option<PredictionService> service = default,
        Option<Match> match = default,
        Option<IEnumerable<DocumentContext>> contextDocuments = default,
        bool includeJustification = false,
        CancellationToken cancellationToken = default)
    {
        var actualService = service.Or(() => CreateService());
        var actualMatch = match.Or(() => CreateTestMatch());
        var actualContextDocs = contextDocuments.Or(() => CreateTestContextDocuments());
        
        return actualService.PredictMatchAsync(
            actualMatch,
            actualContextDocs,
            includeJustification,
            cancellationToken: cancellationToken);
    }

    [Test]
    public async Task Predicting_match_with_valid_input_returns_prediction()
    {
        // Act
        var prediction = await PredictMatchAsync();

        // Assert
        var expected = new Prediction(2, 1, null);
        await Assert.That(prediction).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Predicting_match_with_includeJustification_returns_prediction_with_justification()
    {
        // Arrange
        var responseJson = """
            {
                "home": 3,
                "away": 1,
                "justification": {
                    "keyReasoning": "Bayern Munich has strong home form",
                    "contextSources": {
                        "mostValuable": [
                            {
                                "documentName": "Team Stats",
                                "details": "Bayern's recent winning streak"
                            }
                        ],
                        "leastValuable": []
                    },
                    "uncertainties": ["Weather conditions unclear"]
                }
            }
            """;
        var chatClient = CreateMockChatClient(responseJson);
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictMatchAsync(service, includeJustification: true);

        // Assert
        var expected = new Prediction(3, 1, new PredictionJustification(
            "Bayern Munich has strong home form",
            new PredictionJustificationContextSources(
                [new("Team Stats", "Bayern's recent winning streak")],
                []
            ),
            ["Weather conditions unclear"]
        ));

        await Assert.That(prediction).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Predicting_match_calls_token_tracker_with_correct_usage()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var chatClient = CreateMockChatClient("""{"home": 2, "away": 1}""", usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(chatClient, tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object));

        // Act
        await PredictMatchAsync(service);

        // Assert
        tokenUsageTracker.Verify(
            t => t.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 1000 && actual.OutputTokenCount == 50),
                "flex"),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var chatClient = CreateMockChatClient("""{"home": 2, "away": 1}""", usage);
        var costCalculationService = CreateMockCostCalculationService();
        var service = CreateService(chatClient, costCalculationService: NullableOption.Some(costCalculationService.Object));

        // Act
        await PredictMatchAsync(service);

        // Assert
        costCalculationService.Verify(
            c => c.LogCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 1000 && actual.OutputTokenCount == 50),
                "flex"),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_with_empty_context_documents_succeeds()
    {
        // Arrange
        var chatClient = CreateMockChatClient("""{"home": 2, "away": 1}""");
        var service = CreateService(chatClient);
        List<DocumentContext> emptyContextDocs = [];

        // Act
        var prediction = await PredictMatchAsync(service, contextDocuments: emptyContextDocs);

        // Assert
        var expected = new Prediction(2, 1, null);
        await Assert.That(prediction).IsEquivalentTo(expected);
    }

    [Test]
    public async Task Predicting_match_logs_information_message()
    {
        // Arrange
        var logger = CreateFakeLogger();
        var service = CreateService(logger: logger);

        // Act
        await PredictMatchAsync(service);

        // Assert
        await Assert.That(logger).ContainsLog(LogLevel.Information, "Generating prediction for match");
    }

    [Test]
    public async Task Predicting_match_with_API_exception_returns_null()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_match_with_exception_logs_error()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var logger = CreateFakeLogger();
        var service = CreateService(chatClient, logger: logger);

        // Act
        await PredictMatchAsync(service);

        // Assert
        await Assert.That(logger).ContainsLog(LogLevel.Error, "Error generating prediction for match");
    }

    [Test]
    public async Task Predicting_match_with_invalid_JSON_returns_null()
    {
        // Arrange
        var invalidJson = """not valid json""";
        var chatClient = CreateMockChatClient(invalidJson);
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_with_default_options_uses_flex_processing()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolChatClient(requestedServiceTiers, responseServiceTier: "flex");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
    }

    [Test]
    public async Task Predicting_match_sends_responses_request_with_strict_structured_output()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var requestPayloads = new List<string>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            requestPayloads: requestPayloads,
            responseServiceTier: "flex");
        var service = CreateService(chatClient);

        // Act
        var prediction = await PredictMatchAsync(service, includeJustification: true);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestPayloads).HasSingleItem();

        using var document = JsonDocument.Parse(requestPayloads.Single());
        var root = document.RootElement;
        await Assert.That(root.GetProperty("model").GetString()).IsEqualTo("gpt-5");
        await Assert.That(root.GetProperty("max_output_tokens").GetInt32()).IsEqualTo(10_000);
        await Assert.That(root.GetProperty("service_tier").GetString()).IsEqualTo("flex");

        var input = root.GetProperty("input").EnumerateArray().ToArray();
        await Assert.That(input.Length).IsEqualTo(2);
        await Assert.That(input[0].GetProperty("role").GetString()).IsEqualTo("system");
        await Assert.That(input[1].GetProperty("role").GetString()).IsEqualTo("user");

        var format = root.GetProperty("text").GetProperty("format");
        await Assert.That(format.GetProperty("type").GetString()).IsEqualTo("json_schema");
        await Assert.That(format.GetProperty("name").GetString()).IsEqualTo("match_prediction");
        await Assert.That(format.GetProperty("strict").GetBoolean()).IsTrue();
        await Assert.That(format.GetProperty("schema").GetProperty("required").EnumerateArray().Select(item => item.GetString()))
            .Contains("justification");
    }

    [Test]
    public async Task Predicting_match_with_custom_max_output_token_count_sends_request_limit()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var requestPayloads = new List<string>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            requestPayloads: requestPayloads);
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback with
            {
                MaxOutputTokenCount = 20_000
            }));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestPayloads).HasSingleItem();

        using var document = JsonDocument.Parse(requestPayloads.Single());
        await Assert.That(document.RootElement.GetProperty("max_output_tokens").GetInt32()).IsEqualTo(20_000);
    }

    [Test]
    public async Task Predicting_match_with_standard_processing_option_disables_flex_processing()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var chatClient = CreateMockChatClient("""{"home": 2, "away": 1}""", usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(
            chatClient,
            tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object),
            options: NullableOption.Some(PredictionServiceOptions.StandardProcessing));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(actual => actual.InputTokenCount == 1000 && actual.OutputTokenCount == 50)),
            Times.Once);
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage("gpt-5", It.IsAny<ChatTokenUsage>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_with_flex_processing_retries_standard_after_capacity_failure()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var requestedReasoningEfforts = new List<string?>();
        var requestPayloads = new List<string>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            requestedReasoningEfforts,
            requestPayloads,
            firstException: CreateClientResultException(429));
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback));
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(2);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedServiceTiers[1]).IsNull();
        await Assert.That(requestedReasoningEfforts.Count).IsEqualTo(2);
        await Assert.That(requestedReasoningEfforts[0]).IsNull();
        await Assert.That(requestedReasoningEfforts[1]).IsNull();
        await Assert.That(requestPayloads.All(payload => !payload.Contains("\"reasoning\""))).IsTrue();

        var activity = capturedActivities.Single(candidate => candidate.OperationName == "predict-match");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiExecutionStrategy"))
            .IsEqualTo("flex-first-standard-fallback");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiRequestedServiceTier"))
            .IsEqualTo("standard");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiFinalServiceTier"))
            .IsEqualTo("standard");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiServiceTierFallbackUsed"))
            .IsEqualTo("True");
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")?.ToString())
            .Contains("\"cache_read_input_tokens\":250");
    }

    [Test]
    public async Task Predicting_match_retries_transient_openai_server_error_without_switching_service_tier()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(
                500,
                reasonPhrase: "server_error",
                body: """{"error":{"code":"server_error","message":"An error occurred while processing your request."}}"""),
            responseServiceTier: "flex");
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(2);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedServiceTiers[1]).IsEqualTo("flex");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_with_flex_processing_records_explicit_langfuse_cost_details()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            responseServiceTier: "flex");
        var costCalculationService = CreateMockCostCalculationService();
        costCalculationService
            .Setup(service => service.CalculateCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage =>
                    usage.InputTokenCount == 1000 &&
                    usage.OutputTokenCount == 50 &&
                    usage.InputTokenDetails!.CachedTokenCount == 250),
                "flex"))
            .Returns(new CostBreakdown(0.001m, 0.0001m, 0.002m, 0.0031m));
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(
            chatClient,
            costCalculationService: NullableOption.Some(costCalculationService.Object),
            tokenUsageTracker: NullableOption.Some(tokenUsageTracker.Object),
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback));
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        var activity = capturedActivities.Single(candidate => candidate.OperationName == "predict-match");
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")?.ToString())
            .Contains("\"total\":1050");
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")?.ToString())
            .Contains("\"total\":0.0031");
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")?.ToString())
            .Contains("\"cache_read_input_tokens\":0.0001");
        tokenUsageTracker.Verify(
            tracker => tracker.AddUsage(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage => usage.InputTokenCount == 1000 && usage.OutputTokenCount == 50),
                "flex"),
            Times.Once);
        costCalculationService.Verify(
            service => service.LogCostBreakdown(
                "gpt-5",
                It.Is<ChatTokenUsage>(usage => usage.InputTokenCount == 1000 && usage.OutputTokenCount == 50),
                "flex"),
            Times.Once);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_with_reasoning_effort_sends_effort_and_records_langfuse_metadata()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var requestedReasoningEfforts = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            requestedReasoningEfforts);
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback with
            {
                ReasoningEffort = "xhigh"
            }));
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedReasoningEfforts.Count).IsEqualTo(1);
        await Assert.That(requestedReasoningEfforts[0]).IsEqualTo("xhigh");

        var activity = capturedActivities.Single(candidate => candidate.OperationName == "predict-match");
        await Assert.That(activity.GetTagItem("gen_ai.request.reasoning_effort")).IsEqualTo("xhigh");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.openaiReasoningEffort")).IsEqualTo("xhigh");
    }

    [Test]
    public async Task Predicting_match_with_reasoning_effort_preserves_effort_on_standard_fallback()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var requestedReasoningEfforts = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            requestedReasoningEfforts,
            firstException: CreateClientResultException(429));
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback with
            {
                ReasoningEffort = "none"
            }));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsEquivalentTo(new Prediction(2, 1, null));
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(2);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
        await Assert.That(requestedServiceTiers[1]).IsNull();
        await Assert.That(requestedReasoningEfforts.Count).IsEqualTo(2);
        await Assert.That(requestedReasoningEfforts[0]).IsEqualTo("none");
        await Assert.That(requestedReasoningEfforts[1]).IsEqualTo("none");
    }

    [Test]
    public async Task Predicting_match_with_flex_processing_does_not_retry_non_capacity_failure()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(400));
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
    }

    [Test]
    public async Task Predicting_match_with_flex_processing_does_not_retry_plain_rate_limit()
    {
        // Arrange
        var requestedServiceTiers = new List<string?>();
        var chatClient = CreateProtocolChatClient(
            requestedServiceTiers,
            firstException: CreateClientResultException(
                429,
                reasonPhrase: "Too Many Requests",
                body: """{"error":{"code":"rate_limit_exceeded","message":"rate limit exceeded"}}"""));
        var service = CreateService(
            chatClient,
            options: NullableOption.Some(PredictionServiceOptions.FlexProcessingWithStandardFallback));

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
        await Assert.That(requestedServiceTiers.Count).IsEqualTo(1);
        await Assert.That(requestedServiceTiers[0]).IsEqualTo("flex");
    }

    [Test]
    public async Task Predicting_match_with_null_JSON_object_returns_null()
    {
        // Arrange
        var logger = CreateFakeLogger();
        var chatClient = CreateMockChatClient("null");
        var service = CreateService(chatClient, logger: logger);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
        await Assert.That(logger).ContainsLog(LogLevel.Error, "Raw model response from OpenAI: null");
    }

    [Test]
    public async Task Predicting_match_with_whitespace_response_logs_empty_raw_response_message()
    {
        // Arrange
        var logger = CreateFakeLogger();
        var chatClient = CreateMockChatClient("   ");
        var service = CreateService(chatClient, logger: logger);

        // Act
        var prediction = await PredictMatchAsync(service);

        // Assert
        await Assert.That(prediction).IsNull();
        await Assert.That(logger).ContainsLog(LogLevel.Error, "Raw model response from OpenAI was empty or whitespace.");
    }

    [Test]
    public async Task Predicting_match_uses_shared_prompt_composer_for_system_prompt_and_match_json()
    {
        // Arrange
        IReadOnlyList<ChatMessage>? capturedMessages = null;
        var contextDocuments = CreateTestContextDocuments();
        var chatClient = CreateMockChatClientWithCapture(messages => capturedMessages = messages);
        var service = CreateService(chatClient);
        var match = CreateTestMatch();

        // Act
        await PredictMatchAsync(service, match, contextDocuments);

        // Assert
        await Assert.That(capturedMessages).IsNotNull();
        await Assert.That(capturedMessages!.Count).IsEqualTo(2);

        var systemMessage = (SystemChatMessage)capturedMessages[0];
        var userMessage = (UserChatMessage)capturedMessages[1];

        var expectedSystemPrompt = PredictionPromptComposer.BuildSystemPrompt(
            "You are a football prediction expert. Predict the match outcome.",
            contextDocuments);

        await Assert.That(systemMessage.Content[0].Text).IsEqualTo(expectedSystemPrompt);
        await Assert.That(userMessage.Content[0].Text).IsEqualTo(PredictionPromptComposer.CreateMatchJson(match));
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_records_langfuse_generation_activity_tags()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var service = CreateService(CreateMockChatClient("""{"home": 2, "away": 1}"""));
        var telemetryMetadata = new PredictionTelemetryMetadata("Telemetry Home Team", "Telemetry Away Team", 3);
        var match = CreateTestMatch();
        var contextDocuments = CreateTestContextDocuments();

        var prediction = await service.PredictMatchAsync(match, contextDocuments, telemetryMetadata: telemetryMetadata);

        await Assert.That(prediction).IsNotNull();
        var activity = capturedActivities
            .FirstOrDefault(candidate => IsMatchingPredictMatchActivity(candidate, telemetryMetadata));
        await Assert.That(activity).IsNotNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.type")).IsEqualTo("generation");
        await Assert.That(activity.GetTagItem("gen_ai.request.model")).IsEqualTo("gpt-5");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")?.ToString()).Contains("\"role\":\"system\"");
        await Assert.That(activity.GetTagItem("langfuse.observation.input")?.ToString()).Contains("\"role\":\"user\"");
        await Assert.That(activity.GetTagItem("langfuse.observation.output")).IsEqualTo("""{"home": 2, "away": 1}""");
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")?.ToString()).Contains("\"input\":1000");
        await Assert.That(activity.GetTagItem("langfuse.observation.usage_details")?.ToString()).Contains("\"total\":1050");
        await Assert.That(activity.GetTagItem("langfuse.observation.cost_details")).IsNull();
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.homeTeam")).IsEqualTo("Telemetry Home Team");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.awayTeam")).IsEqualTo("Telemetry Away Team");
        await Assert.That(activity.GetTagItem("langfuse.observation.metadata.repredictionIndex")).IsEqualTo("3");
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Predicting_match_records_langfuse_prompt_link_tags_when_prompt_metadata_is_configured()
    {
        var capturedActivities = new List<Activity>();
        using var listener = CreateActivityListener(capturedActivities);
        var service = CreateService(
            CreateMockChatClient("""{"home": 2, "away": 1}"""),
            options: NullableOption.Some(new PredictionServiceOptions(
                LangfusePromptTraceMetadata: new LangfusePromptTraceMetadata(
                    "kicktippai/predict-one-match-o3-poc",
                    7))));

        var prediction = await PredictMatchAsync(service);

        await Assert.That(prediction).IsNotNull();
        var activity = capturedActivities.Single(candidate => candidate.OperationName == "predict-match");
        await Assert.That(activity.GetTagItem("langfuse.observation.prompt.name"))
            .IsEqualTo("kicktippai/predict-one-match-o3-poc");
        await Assert.That(activity.GetTagItem("langfuse.observation.prompt.version")).IsEqualTo(7);
    }
}
