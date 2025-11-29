using Microsoft.Extensions.Logging;
using TestUtilities.FakeLoggerAssertions;
using TUnit.Core;

namespace TestUtilities.Tests.FakeLoggerAssertionTests;

/// <summary>
/// Tests for FakeLoggerContainsAssertion - verifying successful scenarios
/// </summary>
public class FakeLoggerContainsAssertion_Success_Tests : FakeLoggerAssertionTests_Base
{
    [Test]
    public async Task ContainsLog_with_matching_message_and_level_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "Test message");

        // Act & Assert - should not throw
        await Assert.That(logger)
            .ContainsLog(LogLevel.Information, "Test message");
    }

    [Test]
    public async Task ContainsLog_with_partial_message_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "This is a longer test message with content");

        // Act & Assert - should not throw
        await Assert.That(logger)
            .ContainsLog(LogLevel.Information, "longer test message");
    }

    [Test]
    public async Task ContainsLog_with_multiple_log_entries_finds_matching()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Debug, "Debug message");
        LogMessage(logger, LogLevel.Information, "Info message");
        LogMessage(logger, LogLevel.Warning, "Warning message");

        // Act & Assert - should find the matching one
        await Assert.That(logger)
            .ContainsLog(LogLevel.Information, "Info message");
    }

    [Test]
    public async Task ContainsLog_with_chaining_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Information, "First message");
        LogMessage(logger, LogLevel.Information, "Second message");

        // Act & Assert - should successfully chain assertions
        await Assert.That(logger)
            .ContainsLog(LogLevel.Information, "First message").And
            .ContainsLog(LogLevel.Information, "Second message");
    }

    [Test]
    public async Task ContainsLog_with_Error_level_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Error, "An error occurred");

        // Act & Assert
        await Assert.That(logger)
            .ContainsLog(LogLevel.Error, "An error occurred");
    }

    [Test]
    public async Task ContainsLog_with_Warning_level_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Warning, "Warning about something");

        // Act & Assert
        await Assert.That(logger)
            .ContainsLog(LogLevel.Warning, "Warning about something");
    }

    [Test]
    public async Task ContainsLog_with_Debug_level_succeeds()
    {
        // Arrange
        var logger = CreateFakeLogger();
        LogMessage(logger, LogLevel.Debug, "Debug information");

        // Act & Assert
        await Assert.That(logger)
            .ContainsLog(LogLevel.Debug, "Debug information");
    }
}
