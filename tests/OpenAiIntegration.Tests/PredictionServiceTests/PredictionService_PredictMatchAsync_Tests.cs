using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TestUtilities;
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
            cancellationToken);
    }

    [Test]
    public async Task Predicting_match_with_valid_input_returns_prediction()
    {
        // Act
        var prediction = await PredictMatchAsync();

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.HomeGoals).IsEqualTo(2);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(prediction.Justification).IsNull();
    }

    [Test]
    public async Task Predicting_match_with_includeJustification_returns_prediction_with_justification()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 150);
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
        var chatClient = CreateMockChatClient(responseJson: responseJson, usage: usage);
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictMatchAsync(service: service, includeJustification: true);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.HomeGoals).IsEqualTo(3);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(prediction.Justification).IsNotNull();
        await Assert.That(prediction.Justification!.KeyReasoning).IsEqualTo("Bayern Munich has strong home form");
        await Assert.That(prediction.Justification.ContextSources.MostValuable.Count).IsEqualTo(1);
        await Assert.That(prediction.Justification.ContextSources.MostValuable[0].DocumentName).IsEqualTo("Team Stats");
        await Assert.That(prediction.Justification.Uncertainties.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Predicting_match_calls_token_tracker_with_correct_usage()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        var chatClient = CreateMockChatClient(responseJson: """{"home": 2, "away": 1}""", usage: usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(chatClient: chatClient, tokenUsageTracker: Option.Some(tokenUsageTracker.Object));

        // Act
        await PredictMatchAsync(service: service);

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
        var chatClient = CreateMockChatClient(responseJson: """{"home": 2, "away": 1}""", usage: usage);
        var costCalculationService = CreateMockCostCalculationService();
        var service = CreateService(chatClient: chatClient, costCalculationService: Option.Some(costCalculationService.Object));

        // Act
        await PredictMatchAsync(service: service);

        // Assert
        costCalculationService.Verify(
            c => c.LogCostBreakdown("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_with_empty_context_documents_succeeds()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(500, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"home": 1, "away": 1}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var emptyContextDocs = new List<DocumentContext>();

        // Act
        var prediction = await PredictMatchAsync(service: service, contextDocuments: emptyContextDocs);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
    }

    [Test]
    public async Task Predicting_match_logs_information_message()
    {
        // Arrange
        var logger = CreateFakeLogger();
        var service = CreateService(logger: logger);

        // Act
        await PredictMatchAsync(service: service);

        // Assert
        logger.AssertLogContains(LogLevel.Information, "Generating prediction for match");
    }

    [Test]
    public async Task Predicting_match_with_API_exception_returns_null()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictMatchAsync(service: service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_match_with_exception_logs_error()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var logger = CreateFakeLogger();
        var service = CreateService(chatClient: chatClient, logger: logger);

        // Act
        await PredictMatchAsync(service: service);

        // Assert
        logger.AssertLogContains(LogLevel.Error, "Error generating prediction");
    }

    [Test]
    public async Task Predicting_match_with_invalid_JSON_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        // Use malformed JSON that will cause JsonException during deserialization
        var invalidJson = """not valid json at all""";
        var chatClient = CreateMockChatClient(responseJson: invalidJson, usage: usage);
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictMatchAsync(service: service);

        // Assert
        await Assert.That(prediction).IsNull();
    }
}
