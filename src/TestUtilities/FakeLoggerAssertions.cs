using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Assertions.Extensions;

namespace TestUtilities;

/// <summary>
/// Extension methods for asserting log messages captured by FakeLogger
/// </summary>
public static class FakeLoggerAssertions
{
    /// <summary>
    /// Asserts that a log message containing the specified text was logged at the specified level
    /// </summary>
    /// <typeparam name="T">The type parameter of the FakeLogger</typeparam>
    /// <param name="logger">The FakeLogger instance</param>
    /// <param name="logLevel">The expected log level</param>
    /// <param name="messageContent">The text that should appear in the log message</param>
    public static void AssertLogContains<T>(this FakeLogger<T> logger, LogLevel logLevel, string messageContent)
    {
        var logs = logger.Collector.GetSnapshot();
        var matchingLogs = logs.Where(l => l.Level == logLevel && l.Message.Contains(messageContent));
        
        if (!matchingLogs.Any())
        {
            var actualLogs = string.Join(", ", logs.Select(l => $"[{l.Level}] {l.Message}"));
            Assert.Fail(
                $"Expected to find a log at level {logLevel} containing '{messageContent}', but none was found. " +
                $"Actual logs: {actualLogs}");
        }
    }

    /// <summary>
    /// Asserts that no log message containing the specified text was logged at the specified level
    /// </summary>
    /// <typeparam name="T">The type parameter of the FakeLogger</typeparam>
    /// <param name="logger">The FakeLogger instance</param>
    /// <param name="logLevel">The log level to check</param>
    /// <param name="messageContent">The text that should NOT appear in the log message</param>
    public static void AssertLogDoesNotContain<T>(this FakeLogger<T> logger, LogLevel logLevel, string messageContent)
    {
        var logs = logger.Collector.GetSnapshot();
        var matchingLogs = logs.Where(l => l.Level == logLevel && l.Message.Contains(messageContent));
        
        if (matchingLogs.Any())
        {
            Assert.Fail(
                $"Expected NOT to find a log at level {logLevel} containing '{messageContent}', " +
                $"but {matchingLogs.Count()} were found.");
        }
    }
}
