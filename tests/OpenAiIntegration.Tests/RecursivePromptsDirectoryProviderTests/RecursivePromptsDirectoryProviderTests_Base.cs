namespace OpenAiIntegration.Tests.RecursivePromptsDirectoryProviderTests;

/// <summary>
/// Base class for RecursivePromptsDirectoryProvider tests providing shared helper functionality
/// </summary>
public abstract class RecursivePromptsDirectoryProviderTests_Base
{
    /// <summary>
    /// Creates a temporary directory structure that mimics the solution structure
    /// </summary>
    protected static string CreateTestSolutionStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"KicktippAi_RecursiveTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        // Create the solution file
        File.WriteAllText(Path.Combine(tempDir, "KicktippAi.slnx"), "<solution />");
        
        // Create the prompts directory
        var promptsDir = Path.Combine(tempDir, "prompts");
        Directory.CreateDirectory(promptsDir);
        
        // Create some nested directories to simulate working from different locations
        var srcDir = Path.Combine(tempDir, "src");
        var projectDir = Path.Combine(srcDir, "OpenAiIntegration");
        Directory.CreateDirectory(projectDir);
        
        var testsDir = Path.Combine(tempDir, "tests");
        var testProjectDir = Path.Combine(testsDir, "OpenAiIntegration.Tests");
        Directory.CreateDirectory(testProjectDir);
        
        return tempDir;
    }

    /// <summary>
    /// Cleans up a test solution directory
    /// </summary>
    protected static void CleanupTestSolutionStructure(string directory)
    {
        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Executes an action with a specific working directory and restores the original afterward
    /// </summary>
    protected static T WithWorkingDirectory<T>(string workingDirectory, Func<T> action)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            return action();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    /// <summary>
    /// Executes an action with a specific working directory and restores the original afterward
    /// </summary>
    protected static void WithWorkingDirectory(string workingDirectory, Action action)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            action();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }
}
