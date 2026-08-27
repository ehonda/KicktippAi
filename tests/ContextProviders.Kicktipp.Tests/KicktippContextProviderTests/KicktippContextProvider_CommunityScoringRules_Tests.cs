using ContextProviders.Kicktipp;
using EHonda.Optional.Core;
using Microsoft.Extensions.FileProviders;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_CommunityScoringRules_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_community_rules_returns_correct_document_name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.CommunityScoringRules();

        // Assert
        await Assert.That(context.Name).IsEqualTo($"community-rules-{TestCommunity}.md");
    }

    [Test]
    public async Task Getting_community_rules_returns_file_content()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.CommunityScoringRules();

        // Assert - verify content from actual ehonda-test-buli.md file
        await Assert.That(context.Content).Contains("# Kicktipp Community Scoring Rules");
        await Assert.That(context.Content).Contains("## Scoring System");
        await Assert.That(context.Content).Contains("| Result Type | Tendency | Goal Difference | Exact Result |");
    }

    [Test]
    public async Task Getting_community_rules_with_custom_context_uses_context_for_filename()
    {
        // Arrange
        var provider = CreateProvider(community: "some-other-community", communityContext: "ehonda-test-buli");

        // Act
        var context = await provider.CommunityScoringRules();

        // Assert - should use communityContext for both name and file lookup
        await Assert.That(context.Name).IsEqualTo("community-rules-ehonda-test-buli.md");
        await Assert.That(context.Content).Contains("# Kicktipp Community Scoring Rules");
    }

    [Test]
    public async Task Getting_community_rules_for_nonexistent_file_throws_FileNotFoundException()
    {
        // Arrange
        var provider = CreateProvider(communityContext: "nonexistent-community-rules");

        // Act & Assert
        await Assert.That(async () => await provider.CommunityScoringRules())
            .Throws<FileNotFoundException>()
            .WithMessageContaining("nonexistent-community-rules");
    }

    [Test]
    public async Task Relaxdays_rules_use_the_target_document_identity_and_match_pes_squad()
    {
        using var rulesFileProvider = (PhysicalFileProvider)CommunityRulesFileProvider.Create();
        var providerOption = Option.Some<IFileProvider>(rulesFileProvider);
        var relaxdaysProvider = CreateProvider(
            communityRulesFileProvider: providerOption,
            community: "relaxdays-tippt",
            communityContext: "relaxdays-tippt");
        var pesProvider = CreateProvider(
            communityRulesFileProvider: providerOption,
            community: "pes-squad",
            communityContext: "pes-squad");

        var relaxdaysRules = await relaxdaysProvider.CommunityScoringRules();
        var pesRules = await pesProvider.CommunityScoringRules();

        await Assert.That(relaxdaysRules.Name)
            .IsEqualTo("community-rules-relaxdays-tippt.md");
        await Assert.That(relaxdaysRules.Content.ReplaceLineEndings("\n"))
            .IsEqualTo(pesRules.Content.ReplaceLineEndings("\n"));
    }
}
