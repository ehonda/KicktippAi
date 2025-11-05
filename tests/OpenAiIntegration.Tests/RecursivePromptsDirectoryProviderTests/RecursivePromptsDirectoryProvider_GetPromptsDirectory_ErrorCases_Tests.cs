using TUnit.Core;

namespace OpenAiIntegration.Tests.RecursivePromptsDirectoryProviderTests;

/// <summary>
/// Tests for error cases in RecursivePromptsDirectoryProvider GetPromptsDirectory method
/// These tests use the NotInParallel attribute because they change the working directory,
/// which is a shared global state that cannot be safely modified concurrently
/// </summary>
[NotInParallel]
public class RecursivePromptsDirectoryProvider_GetPromptsDirectory_ErrorCases_Tests : RecursivePromptsDirectoryProviderTests_Base
{
    private string _tempDir = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"KicktippAi_NoSolution_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Teardown()
    {
        CleanupTestSolutionStructure(_tempDir);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Getting_prompts_directory_throws_when_solution_file_not_found()
    {
        // Arrange & Act & Assert
        await Assert.That(() => WithWorkingDirectory(_tempDir, () =>
        {
            var sut = new RecursivePromptsDirectoryProvider();
            return sut.GetPromptsDirectory();
        })).Throws<DirectoryNotFoundException>();
    }
}
