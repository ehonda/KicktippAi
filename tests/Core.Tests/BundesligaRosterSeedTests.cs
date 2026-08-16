using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterSeedTests
{
    private static readonly IReadOnlyDictionary<string, (string Coach, int Players, int StableIds)> ExpectedTeams =
        new Dictionary<string, (string Coach, int Players, int StableIds)>(StringComparer.Ordinal)
        {
            ["b04"] = ("Carles Martínez", 31, 30),
            ["bmg"] = ("Eugen Polanski", 31, 27),
            ["bvb"] = ("Niko Kovac", 27, 26),
            ["fca"] = ("Manuel Baum", 29, 27),
            ["fcb"] = ("Vincent Kompany", 25, 25),
            ["fck"] = ("René Wagner", 26, 25),
            ["fcu"] = ("Mauro Lustrinelli", 30, 27),
            ["hsv"] = ("Merlin Polzin", 30, 30),
            ["m05"] = ("Urs Fischer", 29, 27),
            ["rbl"] = ("Martín Demichelis", 34, 34),
            ["s04"] = ("Miron Muslić", 29, 18),
            ["scf"] = ("Julian Schuster", 30, 30),
            ["scp"] = ("Ralf Kettemann", 30, 10),
            ["sge"] = ("Adi Hütter", 31, 30),
            ["sve"] = ("Vincent Wagner", 28, 11),
            ["svw"] = ("Daniel Thioune", 31, 28),
            ["tsg"] = ("Christian Ilzer", 29, 26),
            ["vfb"] = ("Sebastian Hoeneß", 34, 33)
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Uri>> AdditionalQualityReferences =
        new Dictionary<string, IReadOnlyList<Uri>>(StringComparer.Ordinal)
        {
            ["hsv"] =
            [
                new("https://www.hsv.de/news/ransford-koenigsdoerffer-verlaesst-den-hsv")
            ],
            ["m05"] =
            [
                new("https://www.mainz05.de/news/trainingsauftakt-sommervorbereitung-profis-2627"),
                new("https://www.mainz05.de/news/ransford-konigsdorffer-wird-mainzer")
            ]
        };

    [Test]
    public async Task Checked_in_seed_contains_the_complete_source_dated_fallback()
    {
        var seed = BundesligaRosterSeed.Default;
        var actualTeams = seed.Entries
            .GroupBy(entry => entry.TeamSlug, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var players = group.Where(entry => entry.Role == BundesligaRosterRole.Player).ToArray();
                    return (
                        group.Single(entry => entry.Role == BundesligaRosterRole.Coach).Name,
                        players.Length,
                        players.Count(player => player.TransfermarktPlayerId is not null));
                },
                StringComparer.Ordinal);

        await Assert.That(seed.Entries.Count).IsEqualTo(552);
        await Assert.That(seed.Entries.Count(entry => entry.Role == BundesligaRosterRole.Player)).IsEqualTo(534);
        await Assert.That(seed.Entries.Count(entry => entry.Role == BundesligaRosterRole.Coach)).IsEqualTo(18);
        await Assert.That(seed.Entries.Count(entry => entry.TransfermarktPlayerId is not null)).IsEqualTo(464);
        await Assert.That(seed.Entries.Count(entry => entry.Role == BundesligaRosterRole.Player && entry.TransfermarktPlayerId is null))
            .IsEqualTo(70);
        await Assert.That(actualTeams).IsEquivalentTo(ExpectedTeams);
        await Assert.That(seed.Entries.Select(entry => entry.MembershipAsOf).Distinct())
            .IsEquivalentTo([new DateOnly(2026, 8, 16)]);
        await Assert.That(seed.Entries.All(entry => entry.MembershipSourceUrl.Scheme == Uri.UriSchemeHttps)).IsTrue();
        await Assert.That(seed.Entries.All(entry =>
            entry.TransfermarktClubId == BundesligaTeamManifest.Default.GetByTeamSlug(entry.TeamSlug).TransfermarktClubId)).IsTrue();
        await Assert.That(seed.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Checked_in_and_embedded_seeds_are_identical_and_obey_byte_contract()
    {
        var bytes = File.ReadAllBytes(GetCheckedInSeedPath());
        var content = Encoding.UTF8.GetString(bytes);
        var checkedIn = BundesligaRosterSeed.Parse(bytes);

        await Assert.That(checkedIn.Entries).IsEquivalentTo(BundesligaRosterSeed.Default.Entries);
        await Assert.That(bytes[0]).IsEqualTo((byte)'T');
        await Assert.That(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)).IsFalse();
        await Assert.That(content).StartsWith(string.Join(',', BundesligaRosterSeed.Headers) + "\r\n", StringComparison.Ordinal);
        await Assert.That(content).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(content.Replace("\r\n", string.Empty, StringComparison.Ordinal))
            .DoesNotContain("\r").And.DoesNotContain("\n");
    }

    [Test]
    public async Task Checked_in_seed_resolves_stale_cross_club_cards_with_dated_official_evidence()
    {
        var seed = BundesligaRosterSeed.Default;
        var hsvPlayers = PlayerNames(seed, "hsv");
        var mainzPlayers = PlayerNames(seed, "m05");
        var schalkePlayers = PlayerNames(seed, "s04");

        await Assert.That(hsvPlayers).DoesNotContain("Ransford Königsdörffer");
        await Assert.That(mainzPlayers).Contains("Ransford Königsdörffer");
        await Assert.That(schalkePlayers).DoesNotContain("Junior Dina Ebimbe");
        await Assert.That(schalkePlayers).DoesNotContain("Edin Džeko");
    }

    [Test]
    public async Task Checked_in_quality_report_is_reproducible_from_the_seed()
    {
        var seed = BundesligaRosterSeed.Default;
        var rows = seed.Entries
            .GroupBy(entry => entry.TeamSlug, StringComparer.Ordinal)
            .Select(group =>
            {
                var team = BundesligaTeamManifest.Default.GetByTeamSlug(group.Key);
                var players = group.Where(entry => entry.Role == BundesligaRosterRole.Player).ToArray();
                var stableIdCount = players.Count(player => player.TransfermarktPlayerId is not null);
                var references = group.Select(entry => entry.MembershipSourceUrl)
                    .Concat(AdditionalQualityReferences.GetValueOrDefault(group.Key) ?? [])
                    .ToArray();
                var missingIdCount = players.Length - stableIdCount;
                return new BundesligaRosterQualityReportRow(
                    team,
                    BundesligaRosterMembershipSource.FallbackSeed,
                    new DateOnly(2026, 8, 16),
                    references,
                    SourceRevision: null,
                    LastKnownGoodSnapshotId: null,
                    DuckDbSnapshotAsOf: null,
                    PlayerCount: players.Length,
                    CoachCount: 1,
                    StablePlayerIdCount: stableIdCount,
                    KnownAgeCount: 0,
                    KnownPositionCount: 0,
                    ValuedPlayerCount: 0,
                    BundesligaRosterDuckDbGateResult.NotEvaluated,
                    SelectionReason: "FALLBACK_SEED_BASELINE",
                    Diagnostics: missingIdCount == 0 ? [] : [$"MISSING_STABLE_PLAYER_IDS:{missingIdCount}"]);
            })
            .ToArray();
        var expected = BundesligaRosterCsv.RenderQualityReport(rows);
        var bytes = File.ReadAllBytes(GetQualityReportPath());
        var actual = Encoding.UTF8.GetString(bytes);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(bytes[0]).IsEqualTo((byte)'T');
        await Assert.That(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)).IsFalse();
        await Assert.That(actual).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(actual.Replace("\r\n", string.Empty, StringComparison.Ordinal))
            .DoesNotContain("\r").And.DoesNotContain("\n");
    }

    [Test]
    public async Task Valid_fixture_locks_seed_schema_order_and_byte_contract()
    {
        var path = GetFixturePath("valid-seed.csv");
        var bytes = File.ReadAllBytes(path);
        var content = Encoding.UTF8.GetString(bytes);
        var seed = BundesligaRosterSeed.Parse(bytes, [BayerLeverkusen], path);

        await Assert.That(seed.Entries.Count).IsEqualTo(21);
        await Assert.That(seed.Entries[0].Role).IsEqualTo(BundesligaRosterRole.Coach);
        await Assert.That(seed.Entries.Count(entry => entry.Role == BundesligaRosterRole.Player)).IsEqualTo(20);
        await Assert.That(seed.Diagnostics).IsEmpty();
        await Assert.That(bytes[0]).IsEqualTo((byte)'T');
        await Assert.That(content).StartsWith(string.Join(',', BundesligaRosterSeed.Headers) + "\r\n", StringComparison.Ordinal);
        await Assert.That(content).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(content.Replace("\r\n", string.Empty, StringComparison.Ordinal)).DoesNotContain("\r").And.DoesNotContain("\n");
    }

    [Test]
    public async Task Byte_parser_rejects_bom_lf_and_missing_final_terminator()
    {
        var valid = File.ReadAllBytes(GetFixturePath("valid-seed.csv"));
        var content = Encoding.UTF8.GetString(valid);
        var bom = Encoding.UTF8.Preamble.ToArray().Concat(valid).ToArray();
        var lf = Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n", StringComparison.Ordinal));
        var missingFinal = Encoding.UTF8.GetBytes(content[..^2]);

        await Assert.That(() => BundesligaRosterSeed.Parse(bom, [BayerLeverkusen], "bom"))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterSeed.Parse(lf, [BayerLeverkusen], "lf"))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaRosterSeed.Parse(missingFinal, [BayerLeverkusen], "missing final"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_schema_count_order_and_identity_violations()
    {
        var valid = ReadFixture("valid-seed.csv");
        var badHeader = valid.Replace("Team_Slug,Role", "Team_Slug,Member_Role", StringComparison.Ordinal);
        var tooFewPlayers = valid.Replace(
            "b04,Player,Player 20,15,1020,https://www.bundesliga.com/de/bundesliga/clubs/bayer-04-leverkusen,2026-08-16\r\n",
            string.Empty,
            StringComparison.Ordinal);
        var duplicateId = valid.Replace(
            "b04,Player,Player 02,15,1002,",
            "b04,Player,Player 02,15,1001,",
            StringComparison.Ordinal);
        var rows = valid.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        (rows[2], rows[3]) = (rows[3], rows[2]);
        var unsorted = string.Join("\r\n", rows) + "\r\n";

        await Assert.That(() => Parse(badHeader)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(tooFewPlayers)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(duplicateId)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(unsorted)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_keeps_unknown_player_ids_empty_and_never_guesses()
    {
        var withUnknownId = ReadFixture("valid-seed.csv").Replace(
            "b04,Player,Player 20,15,1020,",
            "b04,Player,Player 20,15,,",
            StringComparison.Ordinal);

        var seed = Parse(withUnknownId);

        await Assert.That(seed.Entries.Single(entry => entry.Name == "Player 20").TransfermarktPlayerId).IsNull();
    }

    private static BundesligaRosterSeed Parse(string content)
    {
        using var reader = new StringReader(content);
        return BundesligaRosterSeed.Parse(reader, [BayerLeverkusen], "test seed");
    }

    private static BundesligaTeamManifestEntry BayerLeverkusen => BundesligaTeamManifest.Default.GetByTeamSlug("b04");

    private static string[] PlayerNames(BundesligaRosterSeed seed, string teamSlug) => seed.Entries
        .Where(entry => entry.TeamSlug == teamSlug && entry.Role == BundesligaRosterRole.Player)
        .Select(entry => entry.Name)
        .ToArray();

    private static string ReadFixture(string name) => File.ReadAllText(GetFixturePath(name), Encoding.UTF8);

    private static string GetFixturePath(string name)
    {
        return Path.Combine(
            SolutionPathUtility.FindSolutionRoot(),
            "tests",
            "Core.Tests",
            "Fixtures",
            "BundesligaRosters",
            name);
    }

    private static string GetCheckedInSeedPath() => Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        BundesligaRosterSeed.RelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string GetQualityReportPath() => Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        BundesligaRosterSeed.QualityReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
}
