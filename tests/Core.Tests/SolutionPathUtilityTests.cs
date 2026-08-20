using EHonda.KicktippAi.Core;
using TUnit.Core;

namespace Core.Tests;

[NotInParallel]
public class SolutionPathUtilityTests
{
    private string _tempDirectory = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"KicktippAi_NoSolution_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [After(Test)]
    public void Teardown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Finding_solution_root_throws_when_solution_file_is_not_found()
    {
        await Assert.That(() => WithWorkingDirectory(_tempDirectory, SolutionPathUtility.FindSolutionRoot))
            .Throws<DirectoryNotFoundException>();
    }

    [Test]
    public async Task Finding_original_repository_root_returns_current_solution_root_when_locator_is_absent()
    {
        var solutionRoot = CreateSolutionDirectory("current");

        var result = WithWorkingDirectory(solutionRoot, SolutionPathUtility.FindOriginalRepositoryRoot);

        await Assert.That(result).IsEqualTo(solutionRoot);
    }

    [Test]
    public async Task Finding_original_repository_root_returns_valid_locator_target()
    {
        var worktreeRoot = CreateSolutionDirectory("worktree");
        var originalRoot = CreateSolutionDirectory("original");
        WriteLocator(worktreeRoot, $"  {originalRoot}  {Environment.NewLine}");

        var result = WithWorkingDirectory(worktreeRoot, SolutionPathUtility.FindOriginalRepositoryRoot);

        await Assert.That(result).IsEqualTo(originalRoot);
    }

    [Test]
    public async Task Finding_original_repository_root_throws_for_blank_locator()
    {
        var solutionRoot = CreateSolutionDirectory("current");
        WriteLocator(solutionRoot, "  ");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WithWorkingDirectory(solutionRoot, SolutionPathUtility.FindOriginalRepositoryRoot));

        await Assert.That(exception!.Message).Contains("blank");
    }

    [Test]
    public async Task Finding_original_repository_root_throws_for_relative_locator()
    {
        var solutionRoot = CreateSolutionDirectory("current");
        WriteLocator(solutionRoot, "relative-repository");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WithWorkingDirectory(solutionRoot, SolutionPathUtility.FindOriginalRepositoryRoot));

        await Assert.That(exception!.Message).Contains("absolute path");
    }

    [Test]
    public async Task Finding_original_repository_root_throws_for_nonexistent_locator_target()
    {
        var solutionRoot = CreateSolutionDirectory("current");
        var missingRoot = Path.Combine(_tempDirectory, "missing");
        WriteLocator(solutionRoot, missingRoot);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WithWorkingDirectory(solutionRoot, SolutionPathUtility.FindOriginalRepositoryRoot));

        await Assert.That(exception!.Message).Contains("does not exist");
    }

    [Test]
    public async Task Finding_original_repository_root_throws_when_locator_target_does_not_contain_solution_file()
    {
        var solutionRoot = CreateSolutionDirectory("current");
        var invalidRoot = Path.Combine(_tempDirectory, "invalid");
        Directory.CreateDirectory(invalidRoot);
        WriteLocator(solutionRoot, invalidRoot);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WithWorkingDirectory(solutionRoot, SolutionPathUtility.FindOriginalRepositoryRoot));

        await Assert.That(exception!.Message).Contains("KicktippAi.slnx");
    }

    private string CreateSolutionDirectory(string name)
    {
        var solutionRoot = Path.Combine(_tempDirectory, name);
        Directory.CreateDirectory(solutionRoot);
        File.WriteAllText(Path.Combine(solutionRoot, "KicktippAi.slnx"), "test");
        return solutionRoot;
    }

    private static void WriteLocator(string solutionRoot, string content)
    {
        var locatorDirectory = Path.Combine(solutionRoot, ".codex-local");
        Directory.CreateDirectory(locatorDirectory);
        File.WriteAllText(Path.Combine(locatorDirectory, "original-repository-path"), content);
    }

    private static T WithWorkingDirectory<T>(string workingDirectory, Func<T> action)
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
}
