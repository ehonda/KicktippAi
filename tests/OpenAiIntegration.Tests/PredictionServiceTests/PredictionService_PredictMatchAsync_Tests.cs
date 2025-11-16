using Core;
using Microsoft.Extensions.Logging;
using Moq;
using OneOf.Types;
using OpenAI.Chat;
using TestUtilities;
using TestUtilities.Options;
using TUnit.Core;

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
        Option<PredictionService> service = default!,
        Option<Core.Match> match = default!,
        Option<IEnumerable<DocumentContext>> contextDocuments = default!,
        bool includeJustification = false,
        CancellationToken cancellationToken = default)
    {
        // Convert default! (null) to None for uninitialized parameters
        service ??= new None();
        match ??= new None();
        contextDocuments ??= new None();
        
        var actualService = service.GetValueOrCreate(() => CreateService());
        var actualMatch = match.GetValueOrCreate(() => CreateTestMatch());
        var actualContextDocs = contextDocuments.GetValueOrCreate(() => CreateTestContextDocuments());
        
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
        ChatClient = CreateMockChatClient(responseJson, usage);

        // Act
        var prediction = await PredictMatchAsync(includeJustification: true);

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
        ChatClient = CreateMockChatClient("""{"home": 2, "away": 1}""", usage);

        // Act
        await PredictMatchAsync();

        // Assert
        TokenUsageTracker.Verify(
            t => t.AddUsage("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        ChatClient = CreateMockChatClient("""{"home": 2, "away": 1}""", usage);

        // Act
        await PredictMatchAsync();

        // Assert
        CostCalculationService.Verify(
            c => c.LogCostBreakdown("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_match_with_empty_context_documents_succeeds()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(500, 30);
        ChatClient = CreateMockChatClient("""{"home": 1, "away": 1}""", usage);
        var emptyContextDocs = new List<DocumentContext>();

        // Act
        var prediction = await PredictMatchAsync(contextDocuments: emptyContextDocs);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.HomeGoals).IsEqualTo(1);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
    }

    [Test]
    public async Task Predicting_match_logs_information_message()
    {
        // Act
        await PredictMatchAsync();

        // Assert
        Logger.AssertLogContains(LogLevel.Information, "Generating prediction for match");
    }

    [Test]
    public async Task Predicting_match_with_API_exception_returns_null()
    {
        // Arrange
        ChatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));

        // Act
        var prediction = await PredictMatchAsync();

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_match_with_exception_logs_error()
    {
        // Arrange
        ChatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));

        // Act
        await PredictMatchAsync();

        // Assert
        Logger.AssertLogContains(LogLevel.Error, "Error generating prediction");
    }

    [Test]
    public async Task Predicting_match_with_invalid_JSON_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(1000, 50);
        // Use malformed JSON that will cause JsonException during deserialization
        var invalidJson = """not valid json at all""";
        ChatClient = CreateMockChatClient(invalidJson, usage);

        // Act
        var prediction = await PredictMatchAsync();

        // Assert
        await Assert.That(prediction).IsNull();
    }
}
