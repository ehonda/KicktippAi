using TUnit.Core;
using TUnit.Assertions.Enums;
using TestUtilities.StringAssertions;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_GetContextAsync_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_context_returns_standings_and_community_rules()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetContextAsync().ToListAsync();

        // Assert
        var expectedNames = new[] { "bundesliga-standings.csv", $"community-rules-{TestCommunity}.md" };
        await Assert.That(contexts.Select(c => c.Name)).IsEquivalentTo(expectedNames, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Getting_context_returns_standings_with_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetContextAsync().ToListAsync();
        var standingsContext = contexts[0];

        // Assert - verify CSV header and structure
        var expectedCsv = """
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses
            1,FC Bayern München,10,25,30:10,30,10,8,1,1
            2,Borussia Dortmund,10,22,28:12,28,12,7,1,2
            3,RB Leipzig,10,20,22:14,22,14,6,2,2

            """;
        await Assert.That(standingsContext.Content).IsEqualToWithNormalizedLineEndings(expectedCsv);
    }

    [Test]
    public async Task Getting_context_returns_community_rules_content()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetContextAsync().ToListAsync();
        var rulesContext = contexts[1];

        // Assert - verify it contains expected content from the actual file
        await Assert.That(rulesContext.Content).Contains("# Kicktipp Community Scoring Rules");
        await Assert.That(rulesContext.Content).Contains("## Scoring System");
    }
}
