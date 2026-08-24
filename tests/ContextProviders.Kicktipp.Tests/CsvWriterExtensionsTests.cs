using ContextProviders.Kicktipp.Csv;
using EHonda.KicktippAi.Core;

namespace ContextProviders.Kicktipp.Tests;

public class CsvWriterExtensionsTests
{
    [Test]
    public async Task Shared_writer_uses_exact_crlf_and_a_final_terminator()
    {
        var content = new[]
        {
            new TeamStanding(1, "FC Bayern München", 1, 3, 2, 0, 2, 1, 0, 0, null)
        }.WriteToCsv<TeamStanding, TeamStandingCsvMap>();

        const string expected =
            "Position,Team,Games,Points,Goal_Ratio,Goals_For,Goals_Against,Wins,Draws,Losses,Group\r\n" +
            "1,FC Bayern München,1,3,2:0,2,0,1,0,0,\r\n";
        await Assert.That(content).IsEqualTo(expected);
        await Assert.That(content.EndsWith("\r\n", StringComparison.Ordinal)).IsTrue();
        await Assert.That(content.Replace("\r\n", string.Empty, StringComparison.Ordinal))
            .DoesNotContain("\r")
            .And.DoesNotContain("\n");
    }
}
