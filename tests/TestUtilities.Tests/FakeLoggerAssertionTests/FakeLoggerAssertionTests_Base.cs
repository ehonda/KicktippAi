using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace TestUtilities.Tests.FakeLoggerAssertionTests;

/// <summary>
/// Base class for FakeLogger assertion tests
/// </summary>
public abstract class FakeLoggerAssertionTests_Base
{
    protected static FakeLogger<FakeLoggerAssertionTests_Base> CreateFakeLogger()
    {
        return new FakeLogger<FakeLoggerAssertionTests_Base>();
    }

    protected static void LogMessage(
        FakeLogger<FakeLoggerAssertionTests_Base> logger,
        LogLevel level,
        string message)
    {
        logger.Log(level, message);
    }
}
