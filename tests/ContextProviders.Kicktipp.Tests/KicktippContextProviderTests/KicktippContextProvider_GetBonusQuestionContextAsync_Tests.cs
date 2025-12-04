namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_GetBonusQuestionContextAsync_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_bonus_context_returns_standings_and_community_rules()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetBonusQuestionContextAsync().ToListAsync();

        // Assert
        await Assert.That(contexts).HasCount().EqualTo(2);
        await Assert.That(contexts[0].Name).IsEqualTo("bundesliga-standings.csv");
        await Assert.That(contexts[1].Name).IsEqualTo($"community-rules-{TestCommunity}.md");
    }

    [Test]
    public async Task Getting_bonus_context_returns_standings_with_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetBonusQuestionContextAsync().ToListAsync();
        var standingsContext = contexts[0];

        // Assert - verify CSV structure is consistent with GetContextAsync
        var expectedCsv = """
            Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses
            1,FC Bayern München,10,25,30:10,30,10,8,1,1
            2,Borussia Dortmund,10,22,28:12,28,12,7,1,2
            3,RB Leipzig,10,20,22:14,22,14,6,2,2

            """;
        await Assert.That(standingsContext.Content).IsEqualTo(expectedCsv);
    }
}
