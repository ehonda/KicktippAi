using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterSeedTests
{
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
}
