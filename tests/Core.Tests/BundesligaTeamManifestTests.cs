using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaTeamManifestTests
{
    [Test]
    public async Task Checked_in_manifest_contains_the_exact_18_kicktipp_teams()
    {
        var kicktippNames = BundesligaTeamManifest.Default.Entries.Select(entry => entry.KicktippName);

        await Assert.That(kicktippNames).IsEquivalentTo(
        [
            "1. FC Köln",
            "1. FC Union Berlin",
            "1899 Hoffenheim",
            "Bayer 04 Leverkusen",
            "Bor. Mönchengladbach",
            "Borussia Dortmund",
            "Eintracht Frankfurt",
            "FC Augsburg",
            "FC Bayern München",
            "FC Schalke 04",
            "FSV Mainz 05",
            "Hamburger SV",
            "RB Leipzig",
            "SC Freiburg",
            "SC Paderborn 07",
            "SV Elversberg",
            "VfB Stuttgart",
            "Werder Bremen"
        ]);
    }

    [Test]
    public async Task Checked_in_manifest_exposes_promoted_team_joins()
    {
        var manifest = BundesligaTeamManifest.Default;

        await Assert.That(manifest.GetByKicktippName("SV Elversberg").TeamSlug).IsEqualTo("sve");
        await Assert.That(manifest.GetByKicktippName("FC Schalke 04").ClubEloName).IsEqualTo("Schalke");
        await Assert.That(manifest.GetByKicktippName("SC Paderborn 07").OfficialName).IsEqualTo("SC Paderborn 07");
        await Assert.That(manifest.GetByKicktippName("FC Bayern München").ClubEloName).IsEqualTo("Bayern");
        await Assert.That(manifest.GetByKicktippName("1. FC Köln").ClubEloName).IsEqualTo("Koeln");
        await Assert.That(manifest.GetByKicktippName("RB Leipzig").ClubEloName).IsEqualTo("RBLeipzig");
        await Assert.That(manifest.GetByKicktippName("1. FC Union Berlin").ClubEloName).IsEqualTo("UnionBerlin");
        await Assert.That(manifest.GetByClubEloName("Elversberg").KicktippName).IsEqualTo("SV Elversberg");
        await Assert.That(manifest.GetByTeamSlug("scp").TransfermarktClubId).IsEqualTo(127);
    }

    [Test]
    public async Task Checked_in_and_embedded_manifests_are_identical()
    {
        using var reader = new StringReader(ReadCheckedInManifest());
        var checkedInManifest = BundesligaTeamManifest.Parse(reader, BundesligaTeamManifest.RelativePath);

        await Assert.That(checkedInManifest.Entries).IsEquivalentTo(BundesligaTeamManifest.Default.Entries);
    }

    [Test]
    public async Task Checked_in_manifest_has_the_required_byte_and_line_ending_contract()
    {
        var bytes = File.ReadAllBytes(GetManifestPath());
        var content = Encoding.UTF8.GetString(bytes);
        var contentWithoutCrLf = content.Replace("\r\n", string.Empty, StringComparison.Ordinal);

        await Assert.That(bytes[0]).IsEqualTo((byte)'K');
        await Assert.That(content).StartsWith("Kicktipp_Name,", StringComparison.Ordinal);
        await Assert.That(content).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(contentWithoutCrLf).DoesNotContain("\r").And.DoesNotContain("\n");
        await Assert.That(content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length)
            .IsEqualTo(BundesligaTeamManifest.ExpectedTeamCount + 1);
    }

    [Test]
    public async Task Parser_rejects_a_manifest_without_18_teams()
    {
        var lines = ReadCheckedInManifest()
            .ReplaceLineEndings("\r\n")
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var incomplete = string.Join("\r\n", lines[..^1]) + "\r\n";

        await Assert.That(() => Parse(incomplete)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_duplicate_kicktipp_names()
    {
        var duplicate = ReadCheckedInManifest().Replace(
            "Bor. Mönchengladbach,bmg",
            "Bayer 04 Leverkusen,bmg",
            StringComparison.Ordinal);

        await Assert.That(() => Parse(duplicate)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_duplicate_team_slugs()
    {
        var duplicate = ReadCheckedInManifest().Replace(
            "Bor. Mönchengladbach,bmg",
            "Bor. Mönchengladbach,b04",
            StringComparison.Ordinal);

        await Assert.That(() => Parse(duplicate)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_missing_official_roster_source()
    {
        var missingSource = ReadCheckedInManifest().Replace(
            "https://www.bundesliga.com/de/bundesliga/clubs/bayer-04-leverkusen,Leverkusen",
            ",Leverkusen",
            StringComparison.Ordinal);

        await Assert.That(() => Parse(missingSource)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_empty_club_elo_mapping()
    {
        var missingAlias = ReadCheckedInManifest().Replace(
            ",Leverkusen,15",
            ",,15",
            StringComparison.Ordinal);

        await Assert.That(() => Parse(missingAlias)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_non_deterministic_row_order()
    {
        var lines = ReadCheckedInManifest()
            .ReplaceLineEndings("\r\n")
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        (lines[1], lines[2]) = (lines[2], lines[1]);
        var unsorted = string.Join("\r\n", lines) + "\r\n";

        await Assert.That(() => Parse(unsorted)).Throws<InvalidDataException>();
    }

    private static BundesligaTeamManifest Parse(string content)
    {
        using var reader = new StringReader(content);
        return BundesligaTeamManifest.Parse(reader, "test manifest");
    }

    private static string ReadCheckedInManifest()
    {
        return File.ReadAllText(GetManifestPath(), Encoding.UTF8);
    }

    private static string GetManifestPath()
    {
        return Path.Combine(
            SolutionPathUtility.FindSolutionRoot(),
            "data",
            "bundesliga-2026-27",
            "team-manifest.csv");
    }
}
