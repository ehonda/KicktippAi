using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp.Tests.CommunityRulesFileProviderTests;

public class CommunityRulesFileProvider_Tests
{
    [Test]
    public async Task Getting_file_from_community_rules_directory_returns_existing_file()
    {
        var sut = CommunityRulesFileProvider.Create();

        await Assert.That(sut).IsTypeOf<PhysicalFileProvider>();
        await Assert.That(sut.GetFileInfo("ehonda-test-buli.md").Exists).IsTrue();
    }
}
