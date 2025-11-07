using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using OpenAI.Chat;

namespace OpenAiIntegration.Tests.CostCalculationServiceTests;

/// <summary>
/// Base class for CostCalculationService tests providing shared helper functionality
/// </summary>
public abstract class CostCalculationServiceTests_Base
{
    protected FakeLogger<CostCalculationService> Logger = null!;
    protected CostCalculationService Service = null!;

    [Before(Test)]
    public void SetupServiceAndLogger()
    {
        Logger = new FakeLogger<CostCalculationService>();
        Service = new CostCalculationService(Logger);
    }

    /// <summary>
    /// Verifies that a log message containing the specified text was logged at the specified level
    /// </summary>
    protected void AssertLogContains(LogLevel logLevel, string messageContent)
    {
        var logs = Logger.Collector.GetSnapshot();
        var matchingLogs = logs.Where(l => l.Level == logLevel && l.Message.Contains(messageContent));
        
        if (!matchingLogs.Any())
        {
            throw new Exception(
                $"Expected to find a log at level {logLevel} containing '{messageContent}', but none was found. " +
                $"Actual logs: {string.Join(", ", logs.Select(l => $"[{l.Level}] {l.Message}"))}");
        }
    }

    /// <summary>
    /// Verifies that no log message containing the specified text was logged at the specified level
    /// </summary>
    protected void AssertLogDoesNotContain(LogLevel logLevel, string messageContent)
    {
        var logs = Logger.Collector.GetSnapshot();
        var matchingLogs = logs.Where(l => l.Level == logLevel && l.Message.Contains(messageContent));
        
        if (matchingLogs.Any())
        {
            throw new Exception(
                $"Expected NOT to find a log at level {logLevel} containing '{messageContent}', but {matchingLogs.Count()} were found.");
        }
    }

    /// <summary>
    /// Helper method to create ChatTokenUsage instances for testing
    /// </summary>
    protected static ChatTokenUsage CreateChatTokenUsage(
        int inputTokens, 
        int outputTokens, 
        int cachedInputTokens,
        int reasoningTokens = 0)
    {
        ChatInputTokenUsageDetails? inputDetails = cachedInputTokens > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedInputTokens)
            : null;
        
        ChatOutputTokenUsageDetails? outputDetails = reasoningTokens > 0
            ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: reasoningTokens)
            : null;
        
        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: inputTokens,
            outputTokenCount: outputTokens,
            inputTokenDetails: inputDetails,
            outputTokenDetails: outputDetails);
    }
}
