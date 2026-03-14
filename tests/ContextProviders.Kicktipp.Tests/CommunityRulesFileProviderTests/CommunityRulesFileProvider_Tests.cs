using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp.Tests.CommunityRulesFileProviderTests;

public class CommunityRulesFileProvider_Tests
{
    [Test]
    public async Task Creating_provider_returns_physical_file_provider()
    {
        var sut = CommunityRulesFileProvider.Create();

        await Assert.That(sut).IsTypeOf<PhysicalFileProvider>();
    }

    [Test]
    public async Task Getting_directory_contents_returns_markdown_files()
    {
        var contents = CommunityRulesFileProvider.Create().GetDirectoryContents("");

        await Assert.That(contents.Exists).IsTrue();
        await Assert.That(contents.Any(file => file.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }
}
