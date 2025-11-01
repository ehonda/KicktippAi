using Core;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

#pragma warning disable OPENAI001 // Suppress warnings for using OpenAI model factories for testing

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Base class for PredictionService tests providing shared helper functionality
/// </summary>
public abstract class PredictionServiceTests_Base
{
    /// <summary>
    /// Helper method to create ChatTokenUsage instances for testing
    /// </summary>
    protected static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens = 0,
        int outputReasoningTokens = 0)
    {
        ChatInputTokenUsageDetails? inputDetails = cachedInputTokens > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedInputTokens)
            : null;
        
        ChatOutputTokenUsageDetails? outputDetails = outputReasoningTokens > 0
            ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: outputReasoningTokens)
            : null;
        
        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: inputTokens,
            outputTokenCount: outputTokens,
            inputTokenDetails: inputDetails,
            outputTokenDetails: outputDetails);
    }

    /// <summary>
    /// Helper method to create a test Match instance
    /// </summary>
    protected static Core.Match CreateTestMatch(
        string homeTeam = "Bayern Munich",
        string awayTeam = "Borussia Dortmund",
        int matchday = 1)
    {
        var instant = Instant.FromUtc(2025, 10, 30, 15, 30);
        var zonedDateTime = instant.InUtc();
        return new Core.Match(homeTeam, awayTeam, zonedDateTime, matchday);
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
    /// Creates a mock logger for PredictionService
    /// </summary>
    protected static Mock<ILogger<PredictionService>> CreateMockLogger()
    {
        return new Mock<ILogger<PredictionService>>();
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
    /// Creates temporary test prompt files in a temp directory for testing
    /// Returns the temp directory path that should be cleaned up after the test
    /// </summary>
    protected static string CreateTestPromptFiles(string model = "gpt-5")
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        // Create the prompts directory structure
        var promptsDir = Path.Combine(tempDir, "prompts", model);
        Directory.CreateDirectory(promptsDir);
        
        // Create solution file
        File.WriteAllText(Path.Combine(tempDir, "KicktippAi.slnx"), "<solution />");
        
        // Create match.md
        File.WriteAllText(
            Path.Combine(promptsDir, "match.md"),
            "You are a football prediction expert. Predict the match outcome.");
        
        // Create match.justification.md
        File.WriteAllText(
            Path.Combine(promptsDir, "match.justification.md"),
            "You are a football prediction expert. Predict the match outcome and provide justification.");
        
        // Create bonus.md
        File.WriteAllText(
            Path.Combine(promptsDir, "bonus.md"),
            "You are a football prediction expert. Answer the bonus question.");
        
        return tempDir;
    }

    /// <summary>
    /// Cleans up temporary test directory
    /// </summary>
    protected static void CleanupTestDirectory(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Creates a mock ChatClient with a configured response
    /// </summary>
    protected static ChatClient CreateMockChatClient(string responseJson, ChatTokenUsage usage)
    {
        // Create the mock ChatClient and mock ClientResult
        var mockClient = new Mock<ChatClient>();
        var mockResult = new Mock<ClientResult<ChatCompletion>>(null, Mock.Of<PipelineResponse>());
        
        // Create the ChatCompletion using the model factory
        var completion = OpenAIChatModelFactory.ChatCompletion(
            id: "test-completion-id",
            model: "gpt-5",
            createdAt: DateTimeOffset.UtcNow,
            finishReason: ChatFinishReason.Stop,
            role: ChatMessageRole.Assistant,
            content: [ChatMessageContentPart.CreateTextPart(responseJson)],
            usage: usage);
        
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
