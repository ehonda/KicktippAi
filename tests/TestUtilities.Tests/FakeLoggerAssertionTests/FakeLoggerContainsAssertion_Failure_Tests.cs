using Microsoft.Extensions.Logging;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Assertions.Exceptions;
using TUnit.Core;

namespace TestUtilities.Tests.FakeLoggerAssertionTests;

/// <summary>
/// Tests for FakeLoggerContainsAssertion - verifying failure scenarios
/// </summary>
public class FakeLoggerContainsAssertion_Failure_Tests : FakeLoggerAssertionTests_Base
{
    [Test]
    public async Task ContainsLog_with_wrong_level_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert
        await Assert.That(async () =>
            await Assert.That(logger).ContainsLog(LogLevel.Error, "Test message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task ContainsLog_with_wrong_message_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert
        await Assert.That(async () =>
            await Assert.That(logger).ContainsLog(LogLevel.Information, "Different message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task ContainsLog_with_empty_logger_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();

        // Act & Assert
        await Assert.That(async () =>
            await Assert.That(logger).ContainsLog(LogLevel.Information, "Any message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task ContainsLog_with_both_wrong_level_and_message_throws()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert
        await Assert.That(async () =>
            await Assert.That(logger).ContainsLog(LogLevel.Error, "Different message")
        ).Throws<AssertionException>();
    }

    [Test]
    public async Task ContainsLog_in_chain_fails_when_second_assertion_fails()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "First message");

        // Act & Assert - first assertion passes, second fails
        await Assert.That(async () =>
            await Assert.That(logger)
                .ContainsLog(LogLevel.Information, "First message").And
                .ContainsLog(LogLevel.Information, "Non-existent message")
        ).Throws<AssertionException>();
    }
}
