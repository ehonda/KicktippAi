using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
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
            t => t.AddUsage("gpt-5", usage),
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
            c => c.LogCostBreakdown("gpt-5", usage),
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
}
