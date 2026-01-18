namespace Orchestrator.Tests.Infrastructure;

/// <summary>
/// Base class providing temporary directory management for tests.
/// Handles creation of unique test directories and cleanup of stale directories.
/// </summary>
/// <remarks>
/// <para>
/// Uses a two-level directory structure: a fixed parent directory (e.g., "SnapshotsEncryptTests")
/// containing GUID-named subdirectories for each test run. This allows cleanup of stale directories
/// from previous test runs that may have been interrupted.
/// </para>
/// <para>
/// Subclasses must override <see cref="TestDirectoryName"/> to specify their parent directory name.
/// </para>
/// </remarks>
public abstract class TempDirectoryTestBase
{
    /// <summary>
    /// The unique directory for the current test instance.
    /// </summary>
    protected string TestDirectory { get; private set; } = null!;

    /// <summary>
    /// The name of the parent directory under the temp folder (e.g., "SnapshotsEncryptTests").
    /// </summary>
    protected abstract string TestDirectoryName { get; }

    /// <summary>
    /// Gets the full path to the parent directory containing all test subdirectories.
    /// </summary>
    private string ParentDirectory => Path.Combine(Path.GetTempPath(), TestDirectoryName);

    /// <summary>
    /// Cleans up stale test directories from previous runs.
    /// Called once before any tests in the class run.
    /// </summary>
    [Before(Class)]
    public static void CleanupStaleDirectories(ClassHookContext context)
    {
        // Get TestDirectoryName via a temporary instance - not ideal but works
        // The actual instance will be created later for each test
        var testClass = context.ClassType;
        if (testClass.IsAbstract) return;

        var instance = (TempDirectoryTestBase?)Activator.CreateInstance(testClass);
        if (instance == null) return;

        var parentDir = Path.Combine(Path.GetTempPath(), instance.TestDirectoryName);
        if (Directory.Exists(parentDir))
        {
            foreach (var subDir in Directory.GetDirectories(parentDir))
            {
                try
                {
                    Directory.Delete(subDir, recursive: true);
                }
                catch
                {
                    // Ignore errors - directory may be in use by another process
                }
            }
        }
    }

    /// <summary>
    /// Creates a unique temp directory for the current test.
    /// </summary>
    [Before(Test)]
    public void SetUpTempDirectory()
    {
        TestDirectory = Path.Combine(ParentDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(TestDirectory);
    }

    /// <summary>
    /// Cleans up the temp directory after the test completes.
    /// </summary>
    [After(Test)]
    public void TearDownTempDirectory()
    {
        if (Directory.Exists(TestDirectory))
        {
            Directory.Delete(TestDirectory, recursive: true);
        }
    }
}
