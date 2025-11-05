using Moq;
using TUnit.Core;

namespace OpenAiIntegration.Tests.InstructionsTemplateProviderTests;

/// <summary>
/// Base class for InstructionsTemplateProvider tests providing shared helper functionality
/// </summary>
public abstract class InstructionsTemplateProviderTests_Base
{
    protected string TempDir { get; private set; } = null!;

    [Before(Test)]
    public async Task Setup()
    {
        TempDir = CreateTestPromptsDirectory();
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Teardown()
    {
        CleanupTestPromptsDirectory(TempDir);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a mock IPromptsDirectoryProvider that returns the specified directory
    /// </summary>
    protected static Mock<IPromptsDirectoryProvider> CreateMockPromptsDirectoryProvider(string promptsDirectory)
    {
        var mock = new Mock<IPromptsDirectoryProvider>();
        mock.Setup(x => x.GetPromptsDirectory()).Returns(promptsDirectory);
        return mock;
    }

    /// <summary>
    /// Creates a temporary directory structure with test prompt files
    /// </summary>
    protected static string CreateTestPromptsDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"KicktippAi_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        // Create directory structure for different models
        var gpt5Dir = Path.Combine(tempDir, "gpt-5");
        var o3Dir = Path.Combine(tempDir, "o3");
        Directory.CreateDirectory(gpt5Dir);
        Directory.CreateDirectory(o3Dir);
        
        // Create test prompt files
        File.WriteAllText(Path.Combine(gpt5Dir, "match.md"), "GPT-5 Match Template");
        File.WriteAllText(Path.Combine(gpt5Dir, "match.justification.md"), "GPT-5 Match Template with Justification");
        File.WriteAllText(Path.Combine(gpt5Dir, "bonus.md"), "GPT-5 Bonus Template");
        
        File.WriteAllText(Path.Combine(o3Dir, "match.md"), "O3 Match Template");
        File.WriteAllText(Path.Combine(o3Dir, "bonus.md"), "O3 Bonus Template");
        
        return tempDir;
    }

    /// <summary>
    /// Cleans up a test prompts directory
    /// </summary>
    protected static void CleanupTestPromptsDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
