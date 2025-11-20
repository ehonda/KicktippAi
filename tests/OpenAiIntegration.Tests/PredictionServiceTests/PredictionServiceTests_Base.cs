using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;
using EHonda.KicktippAi.Core;
using TestUtilities;
using TUnit.Core;
using EHonda.Optional.Core;
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
        Option<ChatClient> chatClient = default,
        Option<FakeLogger<PredictionService>> logger = default,
        Option<ICostCalculationService> costCalculationService = default,
        Option<ITokenUsageTracker> tokenUsageTracker = default,
        Option<IInstructionsTemplateProvider> templateProvider = default,
        Option<string> model = default)
    {
        var actualChatClient = chatClient.Or(() =>
        {
            return CreateMockChatClient();
        });

        var actualLogger = logger.Or(CreateFakeLogger);
        var actualCostService = costCalculationService.Or(() => CreateMockCostCalculationService().Object);
        var actualTokenTracker = tokenUsageTracker.Or(() => CreateMockTokenUsageTracker().Object);
        var actualTemplateProvider = templateProvider.Or(() => CreateMockTemplateProvider().Object);
        var actualModel = model.Or("gpt-5");

        return new PredictionService(
            actualChatClient,
            actualLogger,
            actualCostService,
            actualTokenTracker,
            actualTemplateProvider,
            actualModel);
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
        
        // Map the model to the prompt model (same logic as GetPromptModelForModel)
        var promptModel = model switch
        {
            "o3" => "o3",
            "gpt-5" => "gpt-5",
            "o4-mini" => "o3",
            "gpt-5-mini" => "gpt-5",
            "gpt-5-nano" => "gpt-5",
            _ => model
        };
        
        var matchTemplate = "You are a football prediction expert. Predict the match outcome.";
        var matchPath = $"/prompts/{promptModel}/match.md";
        var matchJustificationTemplate = "You are a football prediction expert. Predict the match outcome and provide justification.";
        var matchJustificationPath = $"/prompts/{promptModel}/match.justification.md";
        var bonusTemplate = "You are a football prediction expert. Answer the bonus question.";
        var bonusPath = $"/prompts/{promptModel}/bonus.md";
        
        // Set up the mock to handle any model by applying the mapping
        mock.Setup(p => p.LoadMatchTemplate(It.IsAny<string>(), false))
            .Returns((string m, bool _) =>
            {
                var pm = m switch
                {
                    "o3" => "o3",
                    "gpt-5" => "gpt-5",
                    "o4-mini" => "o3",
                    "gpt-5-mini" => "gpt-5",
                    "gpt-5-nano" => "gpt-5",
                    _ => m
                };
                return (matchTemplate, $"/prompts/{pm}/match.md");
            });
        
        mock.Setup(p => p.LoadMatchTemplate(It.IsAny<string>(), true))
            .Returns((string m, bool _) =>
            {
                var pm = m switch
                {
                    "o3" => "o3",
                    "gpt-5" => "gpt-5",
                    "o4-mini" => "o3",
                    "gpt-5-mini" => "gpt-5",
                    "gpt-5-nano" => "gpt-5",
                    _ => m
                };
                return (matchJustificationTemplate, $"/prompts/{pm}/match.justification.md");
            });
        
        mock.Setup(p => p.LoadBonusTemplate(It.IsAny<string>()))
            .Returns((string m) =>
            {
                var pm = m switch
                {
                    "o3" => "o3",
                    "gpt-5" => "gpt-5",
                    "o4-mini" => "o3",
                    "gpt-5-mini" => "gpt-5",
                    "gpt-5-nano" => "gpt-5",
                    _ => m
                };
                return (bonusTemplate, $"/prompts/{pm}/bonus.md");
            });
        
        return mock;
    }

    /// <summary>
    /// Creates a mock ChatClient with a configured response
    /// </summary>
    protected static ChatClient CreateMockChatClient(
        Option<string> responseJson = default,
        Option<ChatTokenUsage> usage = default)
    {
        var actualResponseJson = responseJson.Or("""{"home": 2, "away": 1}""");
        var actualUsage = usage.Or(() => OpenAITestHelpers.CreateChatTokenUsage(1000, 50));

        // Create the mock ChatClient and mock ClientResult
        var mockClient = new Mock<ChatClient>();
        var mockResult = new Mock<ClientResult<ChatCompletion>>(null!, Mock.Of<PipelineResponse>());
        
        // Create the ChatCompletion using the model factory
        var completion = OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-5",
            createdAt: DateTimeOffset.UtcNow,
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(actualResponseJson)],
            usage: actualUsage);
        
        // Set up the mock result to return the completion
        mockResult
            .SetupGet(result => result.Value)
            .Returns(completion);
        
        // Set up both sync and async methods
        mockClient.Setup(client => client.CompleteChat(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockResult.Object);
        
        mockClient.Setup(client => client.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);
        
        return mockClient.Object;
    }

    /// <summary>
    /// Creates a mock ChatClient that throws an exception when called
    /// </summary>
    protected static ChatClient CreateThrowingMockChatClient(Exception exception)
    {
        var mockClient = new Mock<ChatClient>();
        
        mockClient.Setup(client => client.CompleteChat(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .Throws(exception);
        
        mockClient.Setup(client => client.CompleteChatAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatCompletionOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        
        return mockClient.Object;
    }
}
