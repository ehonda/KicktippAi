using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TUnit.Assertions.Core;

namespace TestUtilities.FakeLoggerAssertions;

/// <summary>
/// Extension methods for FakeLogger assertions.
/// </summary>
public static class FakeLoggerAssertionExtensions
{
    /// <summary>
    /// Asserts that the logger contains a log entry at the specified level with the specified content.
    /// </summary>
    public static FakeLoggerContainsAssertion<T> ContainsLog<T>(
        this IAssertionSource<FakeLogger<T>> source,
        LogLevel logLevel,
        string messageContent,
        [CallerArgumentExpression(nameof(logLevel))] string? logLevelExpression = null,
        [CallerArgumentExpression(nameof(messageContent))] string? messageExpression = null)
    {
        source.Context.ExpressionBuilder.Append($".ContainsLog({logLevelExpression}, {messageExpression})");
        return new FakeLoggerContainsAssertion<T>(source.Context, logLevel, messageContent);
    }

    /// <summary>
    /// Asserts that the logger does NOT contain a log entry at the specified level with the specified content.
    /// </summary>
    public static FakeLoggerDoesNotContainAssertion<T> DoesNotContainLog<T>(
        this IAssertionSource<FakeLogger<T>> source,
        LogLevel logLevel,
        string messageContent,
        [CallerArgumentExpression(nameof(logLevel))] string? logLevelExpression = null,
        [CallerArgumentExpression(nameof(messageContent))] string? messageExpression = null)
    {
        source.Context.ExpressionBuilder.Append($".DoesNotContainLog({logLevelExpression}, {messageExpression})");
        return new FakeLoggerDoesNotContainAssertion<T>(source.Context, logLevel, messageContent);
    }
}
