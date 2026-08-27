using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp.Tests.CommunityRulesFileProviderTests;

public class CommunityRulesFileProvider_Tests
{
    private static readonly string[] CurrentBundesligaProductionCommunities =
    [
        "pes-squad",
        "schadensfresse",
        "relaxdays-tippt",
        "ehonda-ai-arena"
    ];

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

    [Test]
    public async Task Every_current_Bundesliga_production_community_has_an_exact_rules_source()
    {
        using var provider = (PhysicalFileProvider)CommunityRulesFileProvider.Create();

        foreach (var community in CurrentBundesligaProductionCommunities)
        {
            var rules = provider.GetFileInfo($"{community}.md");

            await Assert.That(rules.Exists).IsTrue();
            await Assert.That(rules.Name).IsEqualTo($"{community}.md");
            await Assert.That(rules.Length).IsGreaterThan(0);
        }
    }
}
