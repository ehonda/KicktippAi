using System.Reflection;
using TUnit.Core;

namespace KicktippIntegration.Tests.Infrastructure;

[NotInParallel("ProcessState")]
public class TestEnvironmentHelperTests
{
    private string _originalCurrentDirectory = null!;
    private string _tempDirectory = null!;

    [Before(Test)]
    public void Setup()
    {
        _originalCurrentDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"KicktippAi_TestEnvironmentHelper_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [After(Test)]
    public void Teardown()
    {
        Directory.SetCurrentDirectory(_originalCurrentDirectory);

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Fixture_environment_path_uses_original_repository_locator()
    {
        var worktreeRoot = CreateSolutionDirectory("worktree", "KicktippAi");
        var originalRoot = CreateSolutionDirectory("original", "KicktippAi");
        var expectedPath = Path.Combine(
            _tempDirectory,
            "original",
            "KicktippAi.Secrets",
            "tests",
            "KicktippIntegration.Tests",
            ".env");
        WriteOriginalRepositoryLocator(worktreeRoot, originalRoot);
        Directory.SetCurrentDirectory(worktreeRoot);

        var getEnvFilePath = typeof(TestEnvironmentHelper).GetMethod(
            "GetEnvFilePath",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (string?)getEnvFilePath?.Invoke(null, null);

        await Assert.That(Path.GetFullPath(result!)).IsEqualTo(expectedPath);
    }

    private string CreateSolutionDirectory(string workspaceName, string solutionDirectoryName)
    {
        var solutionRoot = Path.Combine(_tempDirectory, workspaceName, solutionDirectoryName);
        Directory.CreateDirectory(solutionRoot);
        File.WriteAllText(Path.Combine(solutionRoot, "KicktippAi.slnx"), "test");
        return solutionRoot;
    }

    private static void WriteOriginalRepositoryLocator(string solutionRoot, string originalRoot)
    {
        var locatorDirectory = Path.Combine(solutionRoot, ".codex-local");
        Directory.CreateDirectory(locatorDirectory);
        File.WriteAllText(Path.Combine(locatorDirectory, "original-repository-path"), originalRoot);
    }
}
