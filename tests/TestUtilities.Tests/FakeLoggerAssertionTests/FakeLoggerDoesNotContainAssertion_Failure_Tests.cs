using Microsoft.Extensions.Logging;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Assertions.Exceptions;
using TUnit.Core;

namespace TestUtilities.Tests.FakeLoggerAssertionTests;

/// <summary>
/// Tests for FakeLoggerDoesNotContainAssertion - verifying failure scenarios
/// </summary>
public class FakeLoggerDoesNotContainAssertion_Failure_Tests : FakeLoggerAssertionTests_Base
{
    [Test]
    public async Task DoesNotContainLog_with_exact_match_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert
        await Assert.That(async () =>
            await Assert.That(logger).DoesNotContainLog(LogLevel.Information, "Test message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task DoesNotContainLog_with_partial_match_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "This is a longer test message");

        // Act & Assert - partial message match should fail the assertion
        await Assert.That(async () =>
            await Assert.That(logger).DoesNotContainLog(LogLevel.Information, "longer test")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task DoesNotContainLog_with_multiple_entries_one_matching_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Debug, "Debug message");
        LogMessage(logger, LogLevel.Information, "Info message");
        LogMessage(logger, LogLevel.Warning, "Warning message");

        // Act & Assert - should fail because one entry matches
        await Assert.That(async () =>
            await Assert.That(logger).DoesNotContainLog(LogLevel.Information, "Info message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task DoesNotContainLog_in_chain_fails_when_second_assertion_fails()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Info message");
        LogMessage(logger, LogLevel.Error, "Error message");

        // Act & Assert - first assertion passes, second fails
        await Assert.That(async () =>
            await Assert.That(logger)
                .DoesNotContainLog(LogLevel.Warning, "Warning message").And
                .DoesNotContainLog(LogLevel.Error, "Error message")
        ).Throws<AssertionException>();
    }
}
