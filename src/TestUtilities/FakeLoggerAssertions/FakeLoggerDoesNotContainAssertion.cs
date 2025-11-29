using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Assertions.Core;

namespace TestUtilities.FakeLoggerAssertions;

/// <summary>
/// Asserts that a FakeLogger does NOT contain a log entry at the specified level with the specified content.
/// </summary>
public class FakeLoggerDoesNotContainAssertion<T> : Assertion<FakeLogger<T>>
{
    private readonly LogLevel _logLevel;
    private readonly string _messageContent;

    public FakeLoggerDoesNotContainAssertion(AssertionContext<FakeLogger<T>> context, LogLevel logLevel, string messageContent)
        : base(context)
    {
        _logLevel = logLevel;
        _messageContent = messageContent;
    }

    protected override Task<AssertionResult> CheckAsync(EvaluationMetadata<FakeLogger<T>> metadata)
    {
        var actualValue = metadata.Value;

        if (actualValue is null)
        {
            return Task.FromResult(AssertionResult.Failed("logger was null"));
        }

        var logs = actualValue.Collector.GetSnapshot();
        var matchingLogs = logs.Where(l => l.Level == _logLevel && l.Message.Contains(_messageContent)).ToList();

        if (!matchingLogs.Any())
        {
            return Task.FromResult(AssertionResult.Passed);
        }

        return Task.FromResult(AssertionResult.Failed(
            $"{matchingLogs.Count} matching log(s) were found"));
    }

    protected override string GetExpectation()
        => $"to NOT contain a log at level {_logLevel} with message containing \"{_messageContent}\"";
}
