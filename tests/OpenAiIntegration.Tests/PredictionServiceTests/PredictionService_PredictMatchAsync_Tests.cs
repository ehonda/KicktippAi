using Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictMatchAsync method
/// </summary>
public class PredictionService_PredictMatchAsync_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task PredictMatchAsync_with_valid_input_returns_prediction()
    {
        // Arrange
        var usage = CreateChatTokenUsage(1000, 50);
        var responseJson = """{"home": 2, "away": 1}""";
        var mockChatClient = CreateMockChatClient(responseJson, usage);
        var logger = CreateMockLogger();
        var costCalc = CreateMockCostCalculationService();
        var tokenTracker = CreateMockTokenUsageTracker();

        var service = new PredictionService(
            mockChatClient,
            logger.Object,
            costCalc.Object,
            tokenTracker.Object,
            CreateMockTemplateProvider().Object,
            "gpt-5");

        var match = CreateTestMatch();
        var contextDocs = CreateTestContextDocuments();

        // Act
        var prediction = await service.PredictMatchAsync(match, contextDocs);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.HomeGoals).IsEqualTo(2);
        await Assert.That(prediction.AwayGoals).IsEqualTo(1);
        await Assert.That(prediction.Justification).IsNull();
    }

    [Test]
    public async Task PredictMatchAsync_with_includeJustification_returns_prediction_with_justification()
    {
        // Arrange
            var usage = CreateChatTokenUsage(1000, 150);
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
            var mockChatClient = CreateMockChatClient(responseJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictMatchAsync(match, contextDocs, includeJustification: true);

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
    public async Task PredictMatchAsync_calls_token_tracker_with_correct_usage()
    {
        // Arrange
            var usage = CreateChatTokenUsage(1000, 50);
            var responseJson = """{"home": 2, "away": 1}""";
            var mockChatClient = CreateMockChatClient(responseJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictMatchAsync(match, contextDocs);

            // Assert
            tokenTracker.Verify(
                t => t.AddUsage("gpt-5", usage),
                Times.Once);
                
    }

    [Test]
    public async Task PredictMatchAsync_calls_cost_calculation_service()
    {
        // Arrange
            var usage = CreateChatTokenUsage(1000, 50);
            var responseJson = """{"home": 2, "away": 1}""";
            var mockChatClient = CreateMockChatClient(responseJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictMatchAsync(match, contextDocs);

            // Assert
            costCalc.Verify(
                c => c.LogCostBreakdown("gpt-5", usage),
                Times.Once);
                
    }

    [Test]
    public async Task PredictMatchAsync_with_empty_context_documents_succeeds()
    {
        // Arrange
            var usage = CreateChatTokenUsage(500, 30);
            var responseJson = """{"home": 1, "away": 1}""";
            var mockChatClient = CreateMockChatClient(responseJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var emptyContextDocs = new List<DocumentContext>();

            // Act
            var prediction = await service.PredictMatchAsync(match, emptyContextDocs);

            // Assert
            await Assert.That(prediction).IsNotNull();
            await Assert.That(prediction!.HomeGoals).IsEqualTo(1);
            await Assert.That(prediction.AwayGoals).IsEqualTo(1);
                
    }

    [Test]
    public async Task PredictMatchAsync_logs_information_message()
    {
        // Arrange
            var usage = CreateChatTokenUsage(1000, 50);
            var responseJson = """{"home": 2, "away": 1}""";
            var mockChatClient = CreateMockChatClient(responseJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictMatchAsync(match, contextDocs);

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Generating prediction for match")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
                
    }

    [Test]
    public async Task PredictMatchAsync_with_API_exception_returns_null()
    {
        // Arrange
            var mockChatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictMatchAsync(match, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
                
    }

    [Test]
    public async Task PredictMatchAsync_with_exception_logs_error()
    {
        // Arrange
            var mockChatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictMatchAsync(match, contextDocs);

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error generating prediction")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
                
    }

    [Test]
    public async Task PredictMatchAsync_with_invalid_JSON_returns_null()
    {
        // Arrange
            var usage = CreateChatTokenUsage(1000, 50);
            var invalidJson = """{"invalid": "response"}""";
            var mockChatClient = CreateMockChatClient(invalidJson, usage);
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var match = CreateTestMatch();
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictMatchAsync(match, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
        
    }
}
