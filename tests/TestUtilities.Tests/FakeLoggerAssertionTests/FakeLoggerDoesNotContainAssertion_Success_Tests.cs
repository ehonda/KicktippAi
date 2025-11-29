using Microsoft.Extensions.Logging;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Core;

namespace TestUtilities.Tests.FakeLoggerAssertionTests;

/// <summary>
/// Tests for FakeLoggerDoesNotContainAssertion - verifying successful scenarios
/// </summary>
public class FakeLoggerDoesNotContainAssertion_Success_Tests : FakeLoggerAssertionTests_Base
{
    [Test]
    public async Task DoesNotContainLog_with_empty_logger_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();

        // Act & Assert - should not throw
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Information, "Any message");
    }

    [Test]
    public async Task DoesNotContainLog_with_different_level_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert - should not throw because level doesn't match
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Error, "Test message");
    }

    [Test]
    public async Task DoesNotContainLog_with_different_message_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert - should not throw because message doesn't match
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Information, "Different message");
    }

    [Test]
    public async Task DoesNotContainLog_with_both_different_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert - should not throw
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Error, "Different message");
    }

    [Test]
    public async Task DoesNotContainLog_with_multiple_entries_none_matching_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Debug, "Debug message");
        LogMessage(logger, LogLevel.Information, "Info message");
        LogMessage(logger, LogLevel.Warning, "Warning message");

        // Act & Assert - should not throw
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Error, "Error message");
    }

    [Test]
    public async Task DoesNotContainLog_with_chaining_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Info message");

        // Act & Assert - should successfully chain assertions
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Error, "Error message").And
            .DoesNotContainLog(LogLevel.Warning, "Warning message");
    }

    [Test]
    public async Task DoesNotContainLog_message_partial_mismatch_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert - "Completely different" is not contained in "Test message"
        await Assert.That(logger)
            .DoesNotContainLog(LogLevel.Information, "Completely different");
    }
}
