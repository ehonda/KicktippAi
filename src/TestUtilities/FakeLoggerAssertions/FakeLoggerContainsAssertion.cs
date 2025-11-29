using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Assertions.Core;

namespace TestUtilities.FakeLoggerAssertions;

/// <summary>
/// Asserts that a FakeLogger contains a log entry at the specified level with the specified content.
/// </summary>
public class FakeLoggerContainsAssertion<T> : Assertion<FakeLogger<T>>
{
    private readonly LogLevel _logLevel;
    private readonly string _messageContent;

    public FakeLoggerContainsAssertion(AssertionContext<FakeLogger<T>> context, LogLevel logLevel, string messageContent)
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
        var matchingLogs = logs.Where(l => l.Level == _logLevel && l.Message.Contains(_messageContent));

        if (matchingLogs.Any())
        {
            return Task.FromResult(AssertionResult.Passed);
        }

        var actualLogs = string.Join(", ", logs.Select(l => $"[{l.Level}] {l.Message}"));
        return Task.FromResult(AssertionResult.Failed(
            $"no matching log found. Actual logs: {actualLogs}"));
    }

    protected override string GetExpectation()
        => $"to contain a log at level {_logLevel} with message containing \"{_messageContent}\"";
}
