using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using TestUtilities;
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
        var actualContextDocs = contextDocuments.Or(() => CreateTestContextDocuments());
        
        return actualService.PredictBonusQuestionAsync(
            actualBonusQuestion,
            actualContextDocs,
            cancellationToken);
    }

    [Test]
    public async Task Predicting_bonus_question_with_single_selection_returns_prediction()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt1"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service, bonusQuestion: bonusQuestion);

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
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt1", "opt2"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service, bonusQuestion: bonusQuestion);

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
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt2"]}""", usage: usage);
        var tokenUsageTracker = CreateMockTokenUsageTracker();
        var service = CreateService(chatClient: chatClient, tokenUsageTracker: Option.Some(tokenUsageTracker.Object));

        // Act
        await PredictBonusQuestionAsync(service: service);

        // Assert
        tokenUsageTracker.Verify(
            t => t.AddUsage("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_calls_cost_calculation_service()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt3"]}""", usage: usage);
        var costCalculationService = CreateMockCostCalculationService();
        var service = CreateService(chatClient: chatClient, costCalculationService: Option.Some(costCalculationService.Object));

        // Act
        await PredictBonusQuestionAsync(service: service);

        // Assert
        costCalculationService.Verify(
            c => c.LogCostBreakdown("gpt-5", usage),
            Times.Once);
    }

    [Test]
    public async Task Predicting_bonus_question_with_empty_context_documents_succeeds()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(600, 25);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt1"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var emptyContextDocs = new List<DocumentContext>();

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service, contextDocuments: emptyContextDocs);

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
        await PredictBonusQuestionAsync(service: service);

        // Assert
        logger.AssertLogContains(LogLevel.Information, "Generating prediction for bonus question");
    }

    [Test]
    public async Task Predicting_bonus_question_with_API_exception_returns_null()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_exception_logs_error()
    {
        // Arrange
        var chatClient = CreateThrowingMockChatClient(new InvalidOperationException("API error"));
        var logger = CreateFakeLogger();
        var service = CreateService(chatClient: chatClient, logger: logger);

        // Act
        await PredictBonusQuestionAsync(service: service);

        // Assert
        logger.AssertLogContains(LogLevel.Error, "Error generating bonus prediction");
    }

    [Test]
    public async Task Predicting_bonus_question_with_invalid_option_id_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["invalid-option"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_duplicate_selections_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt1", "opt1"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 2);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service, bonusQuestion: bonusQuestion);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_wrong_selection_count_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var chatClient = CreateMockChatClient(responseJson: """{"selectedOptionIds": ["opt1", "opt2"]}""", usage: usage);
        var service = CreateService(chatClient: chatClient);
        var bonusQuestion = CreateTestBonusQuestion(maxSelections: 1);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service, bonusQuestion: bonusQuestion);

        // Assert
        await Assert.That(prediction).IsNull();
    }

    [Test]
    public async Task Predicting_bonus_question_with_invalid_JSON_returns_null()
    {
        // Arrange
        var usage = OpenAITestHelpers.CreateChatTokenUsage(800, 30);
        var invalidJson = """not valid json""";
        var chatClient = CreateMockChatClient(responseJson: invalidJson, usage: usage);
        var service = CreateService(chatClient: chatClient);

        // Act
        var prediction = await PredictBonusQuestionAsync(service: service);

        // Assert
        await Assert.That(prediction).IsNull();
    }
}
