using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterCsvTests
{
    [Test]
    public async Task Renderers_match_deterministic_golden_fixtures()
    {
        var snapshot = CreateSnapshot("b04");
        var qualityRow = CreateQualityRow(snapshot);

        var roster = BundesligaRosterCsv.RenderTeamRoster(snapshot);
        var aggregate = BundesligaRosterCsv.RenderAggregate([snapshot], [snapshot.Team]);
        var summary = BundesligaRosterCsv.RenderSummary([snapshot], [snapshot.Team]);
        var quality = BundesligaRosterCsv.RenderQualityReport([qualityRow], [snapshot.Team]);

        await Assert.That(roster).IsEqualTo(ReadFixture("expected-roster-b04.csv"));
        await Assert.That(aggregate).IsEqualTo(roster);
        await Assert.That(summary).IsEqualTo(ReadFixture("expected-team-squad-summary.csv"));
        await Assert.That(quality).IsEqualTo(ReadFixture("expected-quality-report.csv"));
        await AssertCsvBytes(roster, 'T');
        await AssertCsvBytes(summary, 'T');
        await AssertCsvBytes(quality, 'T');
    }

    [Test]
    public async Task Aggregate_sorts_clubs_and_reuses_each_team_body()
    {
        var b04 = CreateSnapshot("b04");
        var bmg = CreateSnapshot("bmg", 2000);

        var aggregate = BundesligaRosterCsv.RenderAggregate([bmg, b04], [b04.Team, bmg.Team]);
        var b04Body = RemoveHeader(BundesligaRosterCsv.RenderTeamRoster(b04));
        var bmgBody = RemoveHeader(BundesligaRosterCsv.RenderTeamRoster(bmg));

        await Assert.That(aggregate).IsEqualTo(
            string.Join(',', BundesligaRosterCsv.RosterHeaders) + "\r\n" + b04Body + bmgBody);
    }

    [Test]
    public async Task Aggregate_and_report_default_to_the_complete_18_team_manifest()
    {
        var snapshot = CreateSnapshot("b04");

        await Assert.That(() => BundesligaRosterCsv.RenderAggregate([snapshot])).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterCsv.RenderSummary([snapshot])).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterCsv.RenderQualityReport([CreateQualityRow(snapshot)]))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Missing_supplemental_values_render_as_na_and_zero_is_rejected()
    {
        var snapshot = CreateSnapshot("b04");
        var zeroValueMembers = snapshot.Members
            .Select(member => member.Name == "Player 05" ? member with { MarketValueEur = 0 } : member)
            .ToArray();

        await Assert.That(BundesligaRosterCsv.RenderTeamRoster(snapshot)).Contains("Player 05,N/A,N/A,N/A\r\n");
        await Assert.That(() => BundesligaRosterCsv.RenderTeamRoster(snapshot with { Members = zeroValueMembers }))
            .Throws<InvalidDataException>();
    }

    private static BundesligaRosterClubSnapshot CreateSnapshot(string slug, int idOffset = 0)
    {
        var players = Enumerable.Range(1, 20).Select(index => new BundesligaRosterMember(
            BundesligaRosterRole.Player,
            $"Player {index:00}",
            idOffset + 1000 + index,
            index <= 4 ? 19 + index : null,
            index switch
            {
                1 => BundesligaRosterPosition.Goalkeeper,
                2 => BundesligaRosterPosition.Defender,
                3 => BundesligaRosterPosition.Midfield,
                4 => BundesligaRosterPosition.Attack,
                _ => null
            },
            index switch
            {
                1 => 1_000_000,
                2 => 2_000_000,
                3 => 3_000_000,
                4 => 4_000_001,
                _ => null
            }));
        var members = players
            .Append(new BundesligaRosterMember(BundesligaRosterRole.Coach, "Coach Alpha"))
            .OrderByDescending(member => member.Name, StringComparer.Ordinal)
            .ToArray();
        return new BundesligaRosterClubSnapshot(
            BundesligaTeamManifest.Default.GetByTeamSlug(slug),
            new DateOnly(2026, 8, 16),
            BundesligaRosterMembershipSource.FallbackSeed,
            members);
    }

    private static BundesligaRosterQualityReportRow CreateQualityRow(BundesligaRosterClubSnapshot snapshot)
    {
        return new BundesligaRosterQualityReportRow(
            snapshot.Team,
            BundesligaRosterMembershipSource.FallbackSeed,
            snapshot.MembershipAsOf,
            [snapshot.Team.OfficialRosterSourceUrl],
            null,
            null,
            null,
            20,
            1,
            20,
            4,
            4,
            4,
            BundesligaRosterDuckDbGateResult.NotEvaluated,
            "SEED_VALIDATED",
            [
                "POSITION_COVERAGE_BELOW_80_PERCENT",
                "AGE_COVERAGE_BELOW_80_PERCENT",
                "MARKET_VALUE_COVERAGE_BELOW_50_PERCENT"
            ]);
    }

    private static async Task AssertCsvBytes(string content, char firstHeaderCharacter)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        await Assert.That(bytes[0]).IsEqualTo((byte)firstHeaderCharacter);
        await Assert.That(hasBom).IsFalse();
        await Assert.That(content).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(content.Replace("\r\n", string.Empty, StringComparison.Ordinal)).DoesNotContain("\r").And.DoesNotContain("\n");
    }

    private static string RemoveHeader(string csv)
    {
        return csv[(csv.IndexOf("\r\n", StringComparison.Ordinal) + 2)..];
    }

    private static string ReadFixture(string name)
    {
        var path = Path.Combine(
            SolutionPathUtility.FindSolutionRoot(),
            "tests",
            "Core.Tests",
            "Fixtures",
            "BundesligaRosters",
            name);
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
