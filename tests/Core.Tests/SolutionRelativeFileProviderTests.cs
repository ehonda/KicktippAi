using EHonda.KicktippAi.Core;
using Microsoft.Extensions.FileProviders;

namespace Core.Tests;

public class SolutionRelativeFileProviderTests
{
    [Test]
    public async Task Creating_provider_for_existing_directory_returns_file_provider()
    {
        // Arrange & Act
        var provider = SolutionRelativeFileProvider.Create("prompts");

        // Assert
        await Assert.That(provider).IsNotNull().And.IsTypeOf<PhysicalFileProvider>();
    }

    [Test]
    public async Task Creating_provider_for_existing_directory_can_enumerate_files()
    {
        // Arrange
        var provider = SolutionRelativeFileProvider.Create("prompts");

        // Act
        var contents = provider.GetDirectoryContents("");

        // Assert
        await Assert.That(contents.Exists).IsTrue();
    }

    [Test]
    public async Task Creating_provider_for_nonexistent_directory_throws_directory_not_found_exception()
    {
        // Arrange & Act & Assert
        await Assert.That(() => SolutionRelativeFileProvider.Create("nonexistent-directory-12345"))
            .Throws<DirectoryNotFoundException>();
    }
}
