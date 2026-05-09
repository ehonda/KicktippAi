using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAI.Chat;
using OpenAI.Responses;
using TestUtilities;
using TUnit.Core;
using Match = EHonda.KicktippAi.Core.Match;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Base class for PredictionService tests providing shared helper functionality
/// </summary>
public abstract class PredictionServiceTests_Base
{
    /// <summary>
    /// Factory method to create a PredictionService instance using the configured dependencies
    /// </summary>
    protected static PredictionService CreateService(
        NullableOption<ResponsesClient> chatClient = default,
        NullableOption<FakeLogger<PredictionService>> logger = default,
        NullableOption<ICostCalculationService> costCalculationService = default,
        NullableOption<ITokenUsageTracker> tokenUsageTracker = default,
        NullableOption<IInstructionsTemplateProvider> templateProvider = default,
        NullableOption<string> model = default,
        NullableOption<PredictionServiceOptions> options = default)
    {
        var actualResponsesClient = chatClient.Or(() =>
        {
            return CreateMockChatClient();
        });

        var actualLogger = logger.Or(CreateFakeLogger);
        var actualCostService = costCalculationService.Or(() => CreateMockCostCalculationService().Object);
        var actualTokenTracker = tokenUsageTracker.Or(() => CreateMockTokenUsageTracker().Object);
        var actualTemplateProvider = templateProvider.Or(() => CreateMockTemplateProvider().Object);
        var actualModel = model.Or("gpt-5");
        var actualOptions = options.Or(PredictionServiceOptions.Default);

        return new PredictionService(
            actualResponsesClient!,
            actualLogger!,
            actualCostService!,
            actualTokenTracker!,
            actualTemplateProvider!,
            actualModel!,
            actualOptions!);
    }

    /// <summary>
    /// Helper method to create a test Match instance
    /// </summary>
    protected static Match CreateTestMatch(
        string homeTeam = "Bayern Munich",
        string awayTeam = "Borussia Dortmund",
        int matchday = 1)
    {
        var instant = Instant.FromUtc(2025, 10, 30, 15, 30);
        var zonedDateTime = instant.InUtc();
        return new Match(homeTeam, awayTeam, zonedDateTime, matchday);
    }

    /// <summary>
    /// Helper method to create test context documents
    /// </summary>
    protected static List<DocumentContext> CreateTestContextDocuments()
    {
        return new List<DocumentContext>
        {
            new DocumentContext("Team Stats", "Bayern Munich has won 5 of their last 6 games."),
            new DocumentContext("Head to Head", "Bayern Munich won the last 3 matches against Dortmund.")
        };
    }

    /// <summary>
    /// Helper method to create a test BonusQuestion instance
    /// </summary>
    protected static BonusQuestion CreateTestBonusQuestion(
        string text = "Who will be top scorer?",
        int maxSelections = 1)
    {
        var instant = Instant.FromUtc(2025, 11, 1, 12, 0);
        var zonedDateTime = instant.InUtc();
        var options = new List<BonusQuestionOption>
        {
            new BonusQuestionOption("opt1", "Robert Lewandowski"),
            new BonusQuestionOption("opt2", "Erling Haaland"),
            new BonusQuestionOption("opt3", "Harry Kane")
        };
        return new BonusQuestion(text, zonedDateTime, options, maxSelections);
    }

    /// <summary>
    /// Creates a FakeLogger for PredictionService
    /// </summary>
    protected static FakeLogger<PredictionService> CreateFakeLogger()
    {
        return new FakeLogger<PredictionService>();
    }

    /// <summary>
    /// Creates a mock ICostCalculationService
    /// </summary>
    protected static Mock<ICostCalculationService> CreateMockCostCalculationService()
    {
        return new Mock<ICostCalculationService>();
    }

    /// <summary>
    /// Creates a mock ITokenUsageTracker
    /// </summary>
    protected static Mock<ITokenUsageTracker> CreateMockTokenUsageTracker()
    {
        return new Mock<ITokenUsageTracker>();
    }

    /// <summary>
    /// Creates a mock IInstructionsTemplateProvider with default test templates
    /// </summary>
    protected static Mock<IInstructionsTemplateProvider> CreateMockTemplateProvider(string model = "gpt-5")
    {
        var mock = new Mock<IInstructionsTemplateProvider>();

        var matchTemplate = "You are a football prediction expert. Predict the match outcome.";
        var matchJustificationTemplate = "You are a football prediction expert. Predict the match outcome and provide justification.";
        var bonusTemplate = "You are a football prediction expert. Answer the bonus question.";

        mock.Setup(p => p.LoadMatchTemplate(It.IsAny<string>(), false))
            .Returns((string m, bool _) =>
            {
                var pm = GetPromptModelForTest(m);
                return (matchTemplate, $"/prompts/{pm}/match.md");
            });

        mock.Setup(p => p.LoadMatchTemplate(It.IsAny<string>(), true))
            .Returns((string m, bool _) =>
            {
                var pm = GetPromptModelForTest(m);
                return (matchJustificationTemplate, $"/prompts/{pm}/match.justification.md");
            });

        mock.Setup(p => p.LoadBonusTemplate(It.IsAny<string>()))
            .Returns((string m) =>
            {
                var pm = GetPromptModelForTest(m);
                return (bonusTemplate, $"/prompts/{pm}/bonus.md");
            });

        return mock;
    }

    /// <summary>
    /// Creates a mock ResponsesClient with a configured response.
    /// </summary>
    protected static ResponsesClient CreateMockChatClient(
        Option<string> responseJson = default,
        Option<ChatTokenUsage> usage = default)
    {
        var actualResponseJson = responseJson.Or("""{"home": 2, "away": 1}""");
        var actualUsage = usage.Or(() => OpenAITestHelpers.CreateChatTokenUsage(1000, 50));

        var mockClient = new Mock<ResponsesClient>("test-api-key");
        var mockResult = CreateResponseClientResult(actualResponseJson, actualUsage, serviceTier: "flex");

        mockClient.Setup(client => client.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        return mockClient.Object;
    }

    /// <summary>
    /// Creates a mock ResponsesClient and captures the response input messages passed to the async API.
    /// </summary>
    protected static ResponsesClient CreateMockChatClientWithCapture(
        Action<IReadOnlyList<ChatMessage>> captureMessages,
        Option<string> responseJson = default,
        Option<ChatTokenUsage> usage = default)
    {
        var actualResponseJson = responseJson.Or("""{"home": 2, "away": 1}""");
        var actualUsage = usage.Or(() => OpenAITestHelpers.CreateChatTokenUsage(1000, 50));

        var mockClient = new Mock<ResponsesClient>("test-api-key");
        var mockResult = CreateResponseClientResult(actualResponseJson, actualUsage, serviceTier: "flex");

        mockClient.Setup(client => client.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateResponseOptions, CancellationToken>((options, _) =>
                captureMessages(ReadResponseMessages(options)))
            .ReturnsAsync(mockResult);

        return mockClient.Object;
    }

    /// <summary>
    /// Creates a mock ResponsesClient that throws an exception when called.
    /// </summary>
    protected static ResponsesClient CreateThrowingMockChatClient(Exception exception)
    {
        var mockClient = new Mock<ResponsesClient>("test-api-key");

        mockClient.Setup(client => client.CreateResponseAsync(
                It.IsAny<CreateResponseOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return mockClient.Object;
    }

    protected static ClientResultException CreateClientResultException(
        int status,
        string reasonPhrase = "Resource Unavailable",
        string body = """{"error":{"code":"resource_unavailable","message":"capacity unavailable"}}""",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var response = new TestPipelineResponse(status, reasonPhrase, body, headers);
        return new ClientResultException("OpenAI request failed", response, null);
    }

    protected static ClientResultException CreateRateLimitExceededException()
    {
        return CreateClientResultException(
            429,
            reasonPhrase: "Too Many Requests",
            body: """{"error":{"code":"rate_limit_exceeded","message":"rate limit exceeded"}}""",
            headers: new Dictionary<string, string>
            {
                ["x-ratelimit-remaining-requests"] = "0",
                ["x-ratelimit-reset-requests"] = "0ms"
            });
    }

    protected static ClientResult<ResponseResult> CreateResponseClientResult(
        string responseJson,
        ChatTokenUsage usage,
        string? serviceTier = "flex")
    {
        var response = new Mock<PipelineResponse>();
        response.SetupGet(candidate => candidate.Status).Returns(200);

        var mockResult = new Mock<ClientResult<ResponseResult>>(null!, response.Object);
        var result = CreateResponseResult(responseJson, usage, serviceTier);

        mockResult.SetupGet(candidate => candidate.Value).Returns(result);
        return mockResult.Object;
    }

    private static ResponseResult CreateResponseResult(
        string responseJson,
        ChatTokenUsage usage,
        string? serviceTier)
    {
        var responseText = JsonSerializer.Serialize(responseJson);
        var serviceTierJson = string.IsNullOrWhiteSpace(serviceTier)
            ? string.Empty
            : $",{Environment.NewLine}  \"service_tier\": {JsonSerializer.Serialize(serviceTier)}";
        var responseContent = BinaryData.FromString($$"""
            {
              "id": "resp-test",
              "object": "response",
              "created_at": 1760000000,
              "status": "completed",
              "model": "gpt-5"{{serviceTierJson}},
              "output": [
                {
                  "id": "msg-test",
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    {
                      "type": "output_text",
                      "text": {{responseText}},
                      "annotations": []
                    }
                  ]
                }
              ],
              "usage": {
                "input_tokens": {{usage.InputTokenCount}},
                "input_tokens_details": {
                  "cached_tokens": {{usage.InputTokenDetails?.CachedTokenCount ?? 0}}
                },
                "output_tokens": {{usage.OutputTokenCount}},
                "output_tokens_details": {
                  "reasoning_tokens": {{usage.OutputTokenDetails?.ReasoningTokenCount ?? 0}}
                },
                "total_tokens": {{usage.TotalTokenCount}}
              }
            }
            """);

        return ModelReaderWriter.Read<ResponseResult>(responseContent, ModelReaderWriterOptions.Json)!;
    }

    protected static string ReadPayloadJson(CreateResponseOptions options)
    {
        using var content = (BinaryContent)options;
        using var stream = new MemoryStream();
        content.WriteTo(stream, CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    protected static string? ExtractStringProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    protected static IReadOnlyList<ChatMessage> ReadResponseMessages(CreateResponseOptions options)
    {
        var payloadJson = ReadPayloadJson(options);
        using var document = JsonDocument.Parse(payloadJson);
        var messages = new List<ChatMessage>();

        foreach (var message in document.RootElement.GetProperty("input").EnumerateArray())
        {
            var role = message.GetProperty("role").GetString();
            var text = ReadResponseMessageText(message);
            messages.Add(role switch
            {
                "system" => new SystemChatMessage(text),
                "user" => new UserChatMessage(text),
                _ => throw new InvalidOperationException($"Unsupported response message role '{role}'.")
            });
        }

        return messages;
    }

    private static string GetPromptModelForTest(string model)
    {
        return model switch
        {
            "o3" => "o3",
            "gpt-5" => "gpt-5",
            "o4-mini" => "o3",
            "gpt-5-mini" => "gpt-5",
            "gpt-5-nano" => "gpt-5",
            _ => model
        };
    }

    private static string ReadResponseMessageText(JsonElement message)
    {
        var content = message.GetProperty("content");
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textProperty))
            {
                return textProperty.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private sealed class TestPipelineResponse : PipelineResponse
    {
        private readonly PipelineResponseHeaders _headers;

        public TestPipelineResponse(
            int status,
            string reasonPhrase,
            string body,
            IReadOnlyDictionary<string, string>? headers)
        {
            Status = status;
            ReasonPhrase = reasonPhrase;
            Content = BinaryData.FromString(body);
            ContentStream = new MemoryStream(Content.ToArray());
            _headers = new TestPipelineResponseHeaders(headers ?? new Dictionary<string, string>());
        }

        public override int Status { get; }

        public override string ReasonPhrase { get; }

        public override BinaryData Content { get; }

        public override Stream? ContentStream { get; set; }

        protected override PipelineResponseHeaders HeadersCore => _headers;

        public override BinaryData BufferContent(CancellationToken cancellationToken)
        {
            return Content;
        }

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Content);
        }

        public override void Dispose()
        {
            ContentStream?.Dispose();
        }
    }

    private sealed class TestPipelineResponseHeaders : PipelineResponseHeaders
    {
        private readonly Dictionary<string, string> _headers;

        public TestPipelineResponseHeaders(IReadOnlyDictionary<string, string> headers)
        {
            _headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        }

        public override bool TryGetValue(string name, out string value)
        {
            return _headers.TryGetValue(name, out value!);
        }

        public override bool TryGetValues(string name, out IEnumerable<string> values)
        {
            if (_headers.TryGetValue(name, out var value))
            {
                values = [value];
                return true;
            }

            values = [];
            return false;
        }

        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _headers.GetEnumerator();
        }
    }
}
