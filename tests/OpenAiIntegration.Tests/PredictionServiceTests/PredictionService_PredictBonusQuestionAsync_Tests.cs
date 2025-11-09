using Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TestUtilities;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService PredictBonusQuestionAsync method
/// </summary>
public class PredictionService_PredictBonusQuestionAsync_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task Predicting_bonus_question_with_single_selection_returns_prediction()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1"]}""", usage);
        
        var service = CreateService();
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
    public async Task Predicting_bonus_question_with_multiple_selections_returns_prediction()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(900, 40);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt2"]}""", usage);
        
        var service = CreateService();
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
    public async Task Predicting_bonus_question_calls_token_tracker_with_correct_usage()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt2"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        TokenUsageTracker.Verify(
            t => t.AddUsage("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt3"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        CostCalculationService.Verify(
            c => c.LogCostBreakdown("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_with_empty_context_documents_succeeds()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(600, 25);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var emptyContextDocs = new List<DocumentContext>();

        // Act
        var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, emptyContextDocs);

        // Assert
        await Assert.That(prediction).IsNotNull();
        await Assert.That(prediction!.SelectedOptionIds.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Predicting_bonus_question_logs_information_message()
    {
        // Arrange
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        Logger.AssertLogContains(LogLevel.Information, "Generating prediction for bonus question");
    }

    [Test]
    public async Task Predicting_bonus_question_with_API_exception_returns_null()
    {
        // Arrange
        var mockChatClient = new Mock<ChatClient>();
        mockChatClient
            .Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));
        ChatClient = mockChatClient.Object;
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_exception_logs_error()
    {
        // Arrange
        var mockChatClient = new Mock<ChatClient>();
        mockChatClient
            .Setup(c => c.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));
        ChatClient = mockChatClient.Object;
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        Logger.AssertLogContains(LogLevel.Error, "Error generating bonus prediction");
    }

    [Test]
    public async Task Predicting_bonus_question_with_invalid_option_id_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["invalid-option"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion();
        var contextDocs = CreateTestContextDocuments();

        // Act
        var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_duplicate_selections_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt1"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);
        var contextDocs = CreateTestContextDocuments();

        // Act
        var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_wrong_selection_count_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        ChatClient = CreateMockChatClient("""{"selectedOptionIds": ["opt1", "opt2"]}""", usage);
        
        var service = CreateService();
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);
        var contextDocs = CreateTestContextDocuments();

        // Act
        var prediction = await service.PredictBonusQuestionAsync(bonusQuestion, contextDocs);

        // Assert
        await Assert.That(prediction).IsNull();
    }
}
