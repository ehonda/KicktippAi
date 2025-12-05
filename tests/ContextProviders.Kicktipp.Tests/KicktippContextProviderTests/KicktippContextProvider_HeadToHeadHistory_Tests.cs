using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;

using Match = EHonda.KicktippAi.Core.Match;

namespace ContextProviders.Kicktipp.Tests.KicktippContextProviderTests;

public class KicktippContextProvider_HeadToHeadHistory_Tests : KicktippContextProviderTests_Base
{
    [Test]
    public async Task Getting_head_to_head_history_returns_correct_document_name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.HeadToHeadHistory(TestHomeTeam, TestAwayTeam);

        // Assert - uses both team abbreviations
        await Assert.That(context.Name).IsEqualTo("head-to-head-fcb-vs-bvb.csv");
    }

    [Test]
    public async Task Getting_head_to_head_history_returns_correct_csv_format()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var context = await provider.HeadToHeadHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation
            1.BL 2024/25,5. Spieltag,2024-09-28,Borussia Dortmund,FC Bayern München,1:5,
            1.BL 2023/24,27. Spieltag,2024-03-30,FC Bayern München,Borussia Dortmund,0:2,
            DFB 2022/23,Achtelfinale,2023-02-01,FC Bayern München,Borussia Dortmund,2:1,nach Verlängerung

            """;
        await Assert.That(NormalizeLineEndings(context.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }

    [Test]
    public async Task Getting_head_to_head_history_with_empty_data_returns_headers_only()
    {
        // Arrange
        var mockClient = CreateMockKicktippClient(headToHeadDetailedHistory: new List<HeadToHeadResult>());
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HeadToHeadHistory(TestHomeTeam, TestAwayTeam);

        // Assert
        var expectedCsv = """
            Competition,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation

            """;
        await Assert.That(NormalizeLineEndings(context.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }

    [Test]
    public async Task Getting_head_to_head_history_includes_all_columns()
    {
        // Arrange
        var h2hResults = new List<HeadToHeadResult>
        {
            new(
                "1.BL 2024/25",
                "10. Spieltag",
                "2024-11-09",
                "FC Bayern München",
                "Borussia Dortmund",
                "3:0",
                null)
        };

        var mockClient = CreateMockKicktippClient(headToHeadDetailedHistory: h2hResults);
        var provider = CreateProvider(Option.Some(mockClient.Object));

        // Act
        var context = await provider.HeadToHeadHistory(TestHomeTeam, TestAwayTeam);

        // Assert - verify all expected columns are present
        var expectedCsv = """
            Competition,Matchday,Played_At,Home_Team,Away_Team,Score,Annotation
            1.BL 2024/25,10. Spieltag,2024-11-09,FC Bayern München,Borussia Dortmund,3:0,

            """;
        await Assert.That(NormalizeLineEndings(context.Content)).IsEqualTo(NormalizeLineEndings(expectedCsv));
    }
}
