using Microsoft.Extensions.FileProviders;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PromptsFileProviderTests;

/// <summary>
/// Tests for the PromptsFileProvider
/// These tests use the NotInParallel attribute because they change the working directory,
/// which is a shared global state that cannot be safely modified concurrently
/// </summary>
[NotInParallel]
public class PromptsFileProvider_Tests : PromptsFileProviderTests_Base
{
    private string _tempSolutionDir = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempSolutionDir = CreateTestSolutionStructure();
    }

    [After(Test)]
    public void Teardown()
    {
        CleanupTestSolutionStructure(_tempSolutionDir);
    }

    [Test]
    public async Task Getting_file_from_solution_root_returns_existing_file()
    {
        // Arrange & Act
        var fileInfo = WithWorkingDirectory(_tempSolutionDir, () =>
        {
            var sut = PromptsFileProvider.Create();
            return sut.GetFileInfo("gpt-5/match.md");
        });

        // Assert
        await Assert.That(fileInfo.Exists).IsTrue();
    }

    [Test]
    public async Task Getting_file_from_nested_directory_returns_existing_file()
    {
        // Arrange
        var nestedDir = Path.Combine(_tempSolutionDir, "src", "OpenAiIntegration");

        // Act
        var fileInfo = WithWorkingDirectory(nestedDir, () =>
        {
            var sut = PromptsFileProvider.Create();
            return sut.GetFileInfo("gpt-5/match.md");
        });

        // Assert
        await Assert.That(fileInfo.Exists).IsTrue();
    }

    [Test]
    public async Task Getting_file_from_deeply_nested_directory_returns_existing_file()
    {
        // Arrange
        var deeplyNestedDir = Path.Combine(_tempSolutionDir, "tests", "OpenAiIntegration.Tests");

        // Act
        var fileInfo = WithWorkingDirectory(deeplyNestedDir, () =>
        {
            var sut = PromptsFileProvider.Create();
            return sut.GetFileInfo("gpt-5/match.md");
        });

        // Assert
        await Assert.That(fileInfo.Exists).IsTrue();
    }

    [Test]
    public async Task Getting_nonexistent_file_returns_nonexistent_file_info()
    {
        // Act
        var fileInfo = WithWorkingDirectory(_tempSolutionDir, () =>
        {
            var sut = PromptsFileProvider.Create();
            return sut.GetFileInfo("nonexistent/file.md");
        });

        // Assert
        await Assert.That(fileInfo.Exists).IsFalse();
    }

    [Test]
    public async Task Getting_file_content_returns_correct_content()
    {
        // Act
        var content = WithWorkingDirectory(_tempSolutionDir, () =>
        {
            var sut = PromptsFileProvider.Create();
            var fileInfo = sut.GetFileInfo("gpt-5/match.md");
            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        // Assert
        await Assert.That(content).IsEqualTo("GPT-5 Match Template");
    }
}
