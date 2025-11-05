using TUnit.Core;

namespace OpenAiIntegration.Tests.RecursivePromptsDirectoryProviderTests;

/// <summary>
/// Tests for the RecursivePromptsDirectoryProvider GetPromptsDirectory method
/// These tests use the NotInParallel attribute because they change the working directory,
/// which is a shared global state that cannot be safely modified concurrently
/// </summary>
[NotInParallel]
public class RecursivePromptsDirectoryProvider_GetPromptsDirectory_Tests : RecursivePromptsDirectoryProviderTests_Base
{
    private string _tempSolutionDir = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _tempSolutionDir = CreateTestSolutionStructure();
        await Task.CompletedTask;
    }

    [After(Test)]
    public async Task Teardown()
    {
        CleanupTestSolutionStructure(_tempSolutionDir);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Getting_prompts_directory_from_solution_root_returns_correct_path()
    {
        // Arrange
        var expectedPromptsDir = Path.Combine(_tempSolutionDir, "prompts");

        // Act
        var actualPromptsDir = WithWorkingDirectory(_tempSolutionDir, () =>
        {
            var sut = new RecursivePromptsDirectoryProvider();
            return sut.GetPromptsDirectory();
        });

        // Assert
        await Assert.That(actualPromptsDir).IsEqualTo(expectedPromptsDir);
    }

    [Test]
    public async Task Getting_prompts_directory_from_nested_directory_returns_correct_path()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempSolutionDir, "src", "OpenAiIntegration");
        var expectedPromptsDir = Path.Combine(_tempSolutionDir, "prompts");

        // Act
        var actualPromptsDir = WithWorkingDirectory(nestedDir, () =>
        {
            var sut = new RecursivePromptsDirectoryProvider();
            return sut.GetPromptsDirectory();
        });

        // Assert
        await Assert.That(actualPromptsDir).IsEqualTo(expectedPromptsDir);
    }

    [Test]
    public async Task Getting_prompts_directory_from_deeply_nested_directory_returns_correct_path()
    {
        // Arrange
        var deeplyNestedDir = Path.Combine(_tempSolutionDir, "tests", "OpenAiIntegration.Tests");
        var expectedPromptsDir = Path.Combine(_tempSolutionDir, "prompts");

        // Act
        var actualPromptsDir = WithWorkingDirectory(deeplyNestedDir, () =>
        {
            var sut = new RecursivePromptsDirectoryProvider();
            return sut.GetPromptsDirectory();
        });

        // Assert
        await Assert.That(actualPromptsDir).IsEqualTo(expectedPromptsDir);
    }

    [Test]
    public async Task Getting_prompts_directory_caches_result_across_calls()
    {
        // Arrange
        var expectedPromptsDir = Path.Combine(_tempSolutionDir, "prompts");

        // Act
        var (firstResult, secondResult) = WithWorkingDirectory(_tempSolutionDir, () =>
        {
            var sut = new RecursivePromptsDirectoryProvider();
            
            // First call should search and cache
            var first = sut.GetPromptsDirectory();
            
            // Second call should use cached value
            var second = sut.GetPromptsDirectory();
            
            return (first, second);
        });

        // Assert
        await Assert.That(firstResult).IsEqualTo(expectedPromptsDir);
        await Assert.That(secondResult).IsEqualTo(expectedPromptsDir);
        await Assert.That(firstResult).IsEqualTo(secondResult);
    }

    [Test]
    public async Task Getting_prompts_directory_works_when_created_in_different_directory()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempSolutionDir, "src", "OpenAiIntegration");
        var expectedPromptsDir = Path.Combine(_tempSolutionDir, "prompts");

        // Act - Create provider in one directory
        var sut = WithWorkingDirectory(nestedDir, () => new RecursivePromptsDirectoryProvider());
        
        // Get directory in the same directory (should work because it's cached)
        var actualPromptsDir = WithWorkingDirectory(nestedDir, () => sut.GetPromptsDirectory());

        // Assert
        await Assert.That(actualPromptsDir).IsEqualTo(expectedPromptsDir);
    }
}
