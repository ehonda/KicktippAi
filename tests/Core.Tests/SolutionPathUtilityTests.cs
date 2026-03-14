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
