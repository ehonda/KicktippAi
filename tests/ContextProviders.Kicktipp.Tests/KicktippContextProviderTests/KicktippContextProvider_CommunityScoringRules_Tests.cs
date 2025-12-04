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
}
