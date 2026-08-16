using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaClubEloSeedTests
{
    private static readonly IReadOnlyDictionary<string, (int Rank, int Elo)> ExpectedRatings =
        new Dictionary<string, (int Rank, int Elo)>(StringComparer.Ordinal)
        {
            ["Augsburg"] = (118, 1676),
            ["Bayern"] = (1, 2019),
            ["Dortmund"] = (12, 1866),
            ["Elversberg"] = (205, 1632),
            ["Frankfurt"] = (72, 1723),
            ["Freiburg"] = (60, 1744),
            ["Gladbach"] = (110, 1681),
            ["Hamburg"] = (133, 1667),
            ["Hoffenheim"] = (64, 1734),
            ["Koeln"] = (193, 1638),
            ["Leverkusen"] = (16, 1847),
            ["Mainz"] = (76, 1714),
            ["Paderborn"] = (190, 1639),
            ["RBLeipzig"] = (26, 1811),
            ["Schalke"] = (233, 1615),
            ["Stuttgart"] = (30, 1801),
            ["UnionBerlin"] = (129, 1669),
            ["Werder"] = (144, 1662)
        };

    [Test]
    public async Task Checked_in_seed_locks_all_18_manifest_aliases_and_ratings()
    {
        var snapshot = BundesligaClubEloSeed.Default;
        var actual = snapshot.Entries.ToDictionary(
            entry => entry.Team.ClubEloName,
            entry => (entry.GlobalRank, entry.Elo),
            StringComparer.Ordinal);

        await Assert.That(snapshot.Entries.Count).IsEqualTo(BundesligaTeamManifest.ExpectedTeamCount);
        await Assert.That(actual).IsEquivalentTo(ExpectedRatings);
        await Assert.That(snapshot.RatedAt).IsEqualTo(new DateOnly(2026, 8, 14));
        await Assert.That(snapshot.CollectedAt).IsEqualTo(new DateTimeOffset(2026, 8, 16, 10, 44, 16, TimeSpan.Zero));
        await Assert.That(DateOnly.FromDateTime(snapshot.CollectedAt.UtcDateTime)).IsNotEqualTo(snapshot.RatedAt);
        await Assert.That(snapshot.SourceUrl.AbsoluteUri).IsEqualTo("https://clubelo.com/GER");
        await Assert.That(snapshot.Origin).IsEqualTo(BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var slugs = snapshot.Entries.Select(entry => entry.Team.TeamSlug).ToArray();
        await Assert.That(slugs.SequenceEqual(slugs.Order(StringComparer.Ordinal), StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Checked_in_and_embedded_seeds_are_identical_and_obey_byte_contract()
    {
        var bytes = File.ReadAllBytes(GetSeedPath());
        var text = Encoding.UTF8.GetString(bytes);
        var checkedIn = BundesligaClubEloSeed.Parse(bytes);
        var embedded = BundesligaClubEloSeed.Default;

        await Assert.That(checkedIn.Entries).IsEquivalentTo(embedded.Entries);
        await Assert.That(checkedIn.RatedAt).IsEqualTo(embedded.RatedAt);
        await Assert.That(checkedIn.CollectedAt).IsEqualTo(embedded.CollectedAt);
        await Assert.That(bytes[0]).IsEqualTo((byte)'T');
        await Assert.That(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)).IsFalse();
        await Assert.That(text).StartsWith(string.Join(',', BundesligaClubEloSeed.Headers) + "\r\n", StringComparison.Ordinal);
        await Assert.That(text).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(text.Replace("\r\n", string.Empty, StringComparison.Ordinal))
            .DoesNotContain("\r").And.DoesNotContain("\n");
    }

    [Test]
    public async Task Byte_parser_rejects_bom_lf_and_missing_final_terminator()
    {
        var valid = File.ReadAllBytes(GetSeedPath());
        var text = Encoding.UTF8.GetString(valid);
        var bom = Encoding.UTF8.Preamble.ToArray().Concat(valid).ToArray();
        var lf = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        var missingFinal = Encoding.UTF8.GetBytes(text[..^2]);

        await Assert.That(() => BundesligaClubEloSeed.Parse(bom)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaClubEloSeed.Parse(lf)).Throws<InvalidDataException>();
        await Assert.That(() => BundesligaClubEloSeed.Parse(missingFinal)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_malformed_nonpositive_and_duplicate_numeric_values()
    {
        var valid = ReadSeed();
        var malformedRank = valid.Replace("b04,Leverkusen,16,1847", "b04,Leverkusen,rank,1847", StringComparison.Ordinal);
        var zeroElo = valid.Replace("b04,Leverkusen,16,1847", "b04,Leverkusen,16,0", StringComparison.Ordinal);
        var duplicateRank = valid.Replace("bmg,Gladbach,110,1681", "bmg,Gladbach,16,1681", StringComparison.Ordinal);

        await Assert.That(() => Parse(malformedRank)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(zeroElo)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(duplicateRank)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_missing_duplicate_mismatched_and_unsorted_manifest_joins()
    {
        var valid = ReadSeed();
        var lines = valid.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var missing = string.Join("\r\n", lines[..^1]) + "\r\n";
        var duplicate = valid.Replace("bmg,Gladbach", "b04,Leverkusen", StringComparison.Ordinal);
        var mismatchedAlias = valid.Replace("s04,Schalke", "s04,Elversberg", StringComparison.Ordinal);
        (lines[1], lines[2]) = (lines[2], lines[1]);
        var unsorted = string.Join("\r\n", lines) + "\r\n";

        await Assert.That(() => Parse(missing)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(duplicate)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(mismatchedAlias)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(unsorted)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Parser_rejects_mixed_or_invalid_provenance()
    {
        var valid = ReadSeed();
        var mixedRatedAt = valid.Replace(
            "b04,Leverkusen,16,1847,2026-08-14",
            "b04,Leverkusen,16,1847,2026-08-13",
            StringComparison.Ordinal);
        var mixedCollectedAt = valid.Replace(
            "b04,Leverkusen,16,1847,2026-08-14,2026-08-16T10:44:16Z",
            "b04,Leverkusen,16,1847,2026-08-14,2026-08-16T10:44:17Z",
            StringComparison.Ordinal);
        var mixedSource = valid.Replace(
            "b04,Leverkusen,16,1847,2026-08-14,2026-08-16T10:44:16Z,https://clubelo.com/GER",
            "b04,Leverkusen,16,1847,2026-08-14,2026-08-16T10:44:16Z,https://clubelo.com/Leverkusen",
            StringComparison.Ordinal);
        var futureRatedAt = valid.Replace("2026-08-14", "2026-08-17", StringComparison.Ordinal);

        await Assert.That(() => Parse(mixedRatedAt)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(mixedCollectedAt)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(mixedSource)).Throws<InvalidDataException>();
        await Assert.That(() => Parse(futureRatedAt)).Throws<InvalidDataException>();
    }

    private static BundesligaClubEloSnapshot Parse(string content)
    {
        return BundesligaClubEloSeed.Parse(Encoding.UTF8.GetBytes(content));
    }

    private static string ReadSeed() => File.ReadAllText(GetSeedPath(), Encoding.UTF8);

    private static string GetSeedPath()
    {
        return Path.Combine(
            SolutionPathUtility.FindSolutionRoot(),
            "data",
            "bundesliga-2026-27",
            "club-elo-launch-seed.csv");
    }
}
