namespace Orchestrator.Tests.Infrastructure;

/// <summary>
/// Base class providing environment variable management for tests that use KICKTIPP_FIXTURE_KEY.
/// Handles saving and restoring the original value to avoid test pollution.
/// </summary>
/// <remarks>
/// <para>
/// Tests using this base class should be marked with <c>[NotInParallel("KICKTIPP_FIXTURE_KEY")]</c>
/// to prevent parallel execution that could cause race conditions on the environment variable.
/// </para>
/// <para>
/// This class can be combined with <see cref="TempDirectoryTestBase"/> by inheriting from
/// <see cref="TempDirectoryWithEncryptionKeyTestBase"/> instead.
/// </para>
/// </remarks>
public abstract class EncryptionKeyTestBase
{
    private const string EncryptionKeyEnvVar = "KICKTIPP_FIXTURE_KEY";
    private string? _originalEncryptionKey;

    /// <summary>
    /// Saves the current encryption key before the test runs.
    /// </summary>
    [Before(Test)]
    public void SaveEncryptionKey()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable(EncryptionKeyEnvVar);
    }

    /// <summary>
    /// Restores the original encryption key after the test completes.
    /// </summary>
    [After(Test)]
    public void RestoreEncryptionKey()
    {
        Environment.SetEnvironmentVariable(EncryptionKeyEnvVar, _originalEncryptionKey);
    }

    /// <summary>
    /// Sets the encryption key environment variable for the current test.
    /// </summary>
    /// <param name="key">The encryption key to set, or null to clear it.</param>
    protected static void SetEncryptionKey(string? key)
    {
        Environment.SetEnvironmentVariable(EncryptionKeyEnvVar, key);
    }
}

/// <summary>
/// Combined base class providing both temporary directory management and encryption key handling.
/// Use this for tests that need both features (most snapshot command tests).
/// </summary>
/// <remarks>
/// Tests using this base class should be marked with <c>[NotInParallel("KICKTIPP_FIXTURE_KEY")]</c>
/// to prevent parallel execution that could cause race conditions on the environment variable.
/// </remarks>
public abstract class TempDirectoryWithEncryptionKeyTestBase : TempDirectoryTestBase
{
    private const string EncryptionKeyEnvVar = "KICKTIPP_FIXTURE_KEY";
    private string? _originalEncryptionKey;

    /// <summary>
    /// Saves the current encryption key before the test runs.
    /// </summary>
    [Before(Test)]
    public void SaveEncryptionKey()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable(EncryptionKeyEnvVar);
    }

    /// <summary>
    /// Restores the original encryption key after the test completes.
    /// </summary>
    [After(Test)]
    public void RestoreEncryptionKey()
    {
        Environment.SetEnvironmentVariable(EncryptionKeyEnvVar, _originalEncryptionKey);
    }

    /// <summary>
    /// Sets the encryption key environment variable for the current test.
    /// </summary>
    /// <param name="key">The encryption key to set, or null to clear it.</param>
    protected static void SetEncryptionKey(string? key)
    {
        Environment.SetEnvironmentVariable(EncryptionKeyEnvVar, key);
    }
}
