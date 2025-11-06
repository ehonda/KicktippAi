using TUnit.Core;

namespace OpenAiIntegration.Tests.PromptsFileProviderTests;

/// <summary>
/// Tests for error cases in PromptsFileProvider
/// These tests use the NotInParallel attribute because they change the working directory,
/// which is a shared global state that cannot be safely modified concurrently
/// </summary>
[NotInParallel]
public class PromptsFileProvider_ErrorCases_Tests : PromptsFileProviderTests_Base
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
    public async Task Creating_provider_throws_when_solution_file_not_found()
    {
        // Arrange & Act & Assert
        await Assert.That(() => WithWorkingDirectory(_tempDir, () =>
        {
            return PromptsFileProvider.Create();
        })).Throws<DirectoryNotFoundException>();
    }
}
