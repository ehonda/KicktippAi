using Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictBonusQuestionAsync method
/// </summary>
public class PredictionService_PredictBonusQuestionAsync_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task PredictBonusQuestionAsync_with_single_selection_returns_prediction()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt1"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNotNull();
            await Assert.That(prediction!.SelectedOptionIds.Count).IsEqualTo(1);
            await Assert.That(prediction.SelectedOptionIds[0]).IsEqualTo("opt1");
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_multiple_selections_returns_prediction()
    {
        // Arrange
            var usage = CreateChatTokenUsage(900, 40);
            var responseJson = """{"selectedOptionIds": ["opt1", "opt2"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNotNull();
            await Assert.That(prediction!.SelectedOptionIds.Count).IsEqualTo(2);
            await Assert.That(prediction.SelectedOptionIds).Contains("opt1");
            await Assert.That(prediction.SelectedOptionIds).Contains("opt2");
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_calls_token_tracker_with_correct_usage()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt2"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            tokenTracker.Verify(
                t => t.AddUsage("gpt-5", usage),
                Times.Once);
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_calls_cost_calculation_service()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt3"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            costCalc.Verify(
                c => c.LogCostBreakdown("gpt-5", usage),
                Times.Once);
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_empty_context_documents_succeeds()
    {
        // Arrange
            var usage = CreateChatTokenUsage(600, 25);
            var responseJson = """{"selectedOptionIds": ["opt1"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion();
            var emptyContextDocs = new List<DocumentContext>();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, emptyContextDocs);

            // Assert
            await Assert.That(prediction).IsNotNull();
            await Assert.That(prediction!.SelectedOptionIds.Count).IsEqualTo(1);
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_logs_information_message()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt1"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Generating prediction for bonus question")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_API_exception_returns_null()
    {
        // Arrange
            var mockChatClient = new Mock<ChatClient>();
            mockChatClient
                .Setup(c => c.CompleteChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("API error"));

            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient.Object,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_exception_logs_error()
    {
        // Arrange
            var mockChatClient = new Mock<ChatClient>();
            mockChatClient
                .Setup(c => c.CompleteChatAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatCompletionOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("API error"));

            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient.Object,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object, CreateMockTemplateProvider().Object,
                "gpt-5");

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error generating bonus prediction")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_invalid_option_id_returns_null()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["invalid-option"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion();
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_duplicate_selections_returns_null()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt1", "opt1"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
                
    }

    [Test]
    public async Task PredictBonusQuestionAsync_with_wrong_selection_count_returns_null()
    {
        // Arrange
            var usage = CreateChatTokenUsage(800, 30);
            var responseJson = """{"selectedOptionIds": ["opt1", "opt2"]}""";
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

            var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);
            var contextDocs = CreateTestContextDocuments();

            // Act
            var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

            // Assert
            await Assert.That(prediction).IsNull();
        
    }
}
