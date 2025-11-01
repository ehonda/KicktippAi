using Core;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ClientModel.Primitives;

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
        // Create the response JSON that includes usage information
        var fullResponse = $$"""
        {
            "id": "test-completion-id",
            "object": "chat.completion",
            "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
            "model": "gpt-5",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": {{System.Text.Json.JsonSerializer.Serialize(responseJson)}}
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": {{usage.InputTokenCount}},
                "completion_tokens": {{usage.OutputTokenCount}},
                "total_tokens": {{usage.TotalTokenCount}}
            }
        }
        """;
        
        MockPipelineResponse mockResponse = new MockPipelineResponse(200)
            .WithContent(BinaryContent.Create(BinaryData.FromString(fullResponse)));
        
        OpenAIClientOptions options = new()
        {
            Transport = new MockPipelineTransport(_ => mockResponse)
        };
        
        return new ChatClient("gpt-5", new ApiKeyCredential("test-key"), options);
    }

    /// <summary>
    /// Creates a mock ChatClient that throws an exception when called
    /// </summary>
    protected static ChatClient CreateThrowingMockChatClient(Exception exception)
    {
        var mockTransport = new Mock<MockPipelineTransport>(MockBehavior.Strict, (Func<PipelineRequest, PipelineResponse>)(_ => throw exception));
        
        OpenAIClientOptions options = new()
        {
            Transport = mockTransport.Object
        };
        
        return new ChatClient("gpt-5", new ApiKeyCredential("test-key"), options);
    }
}
