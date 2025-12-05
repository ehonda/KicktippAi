namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_GetMatchContextAsync_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_match_context_returns_all_context_documents()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetMatchContextAsync(TestHomeTeam, TestAwayTeam).ToListAsync();

        // Assert - should return 7 documents:
        // 1. standings, 2. community rules, 3. home recent history, 4. away recent history,
        // 5. home history, 6. away history, 7. head-to-head history
        await Assert.That(contexts).HasCount().EqualTo(7);
    }

    [Test]
    public async Task Getting_match_context_returns_documents_in_correct_order()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetMatchContextAsync(TestHomeTeam, TestAwayTeam).ToListAsync();

        // Assert - verify order
        var expectedNames = new[]
        {
            "bundesliga-standings.csv",
            $"community-rules-{TestCommunity}.md",
            "recent-history-fcb.csv",
            "recent-history-bvb.csv",
            "home-history-fcb.csv",
            "away-history-bvb.csv",
            "head-to-head-fcb-vs-bvb.csv"
        };

        await Assert.That(contexts.Select(c => c.Name).SequenceEqual(expectedNames)).IsTrue();
    }

    [Test]
    public async Task Getting_match_context_returns_recent_history_with_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetMatchContextAsync(TestHomeTeam, TestAwayTeam).ToListAsync();
        var recentHistory = contexts[2]; // Home team recent history

        // Assert - verify CSV format
        var expectedCsv = """
            Competition,Home_Team,Away_Team,Score,Annotation
            1.BL,FC Bayern München,VfB Stuttgart,3:1,
            1.BL,RB Leipzig,FC Bayern München,1:1,
            DFB,FC Bayern München,1. FC Köln,5:0,

            """;
        await Assert.That(NormalizeLineEndings(recentHistory.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }

    [Test]
    public async Task Getting_match_context_returns_head_to_head_with_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var contexts = await provider.GetMatchContextAsync(TestHomeTeam, TestAwayTeam).ToListAsync();
        var h2hContext = contexts[6];

        // Assert - verify CSV format with all H2H columns
        var expectedCsv = """
            Competition,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation
            1.BL 2024/25,5. Spieltag,2024-09-28,Borussia Dortmund,FC Bayern München,1:5,
            1.BL 2023/24,27. Spieltag,2024-03-30,FC Bayern München,Borussia Dortmund,0:2,
            DFB 2022/23,Achtelfinale,2023-02-01,FC Bayern München,Borussia Dortmund,2:1,nach Verlängerung

            """;
        await Assert.That(NormalizeLineEndings(h2hContext.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }
}
