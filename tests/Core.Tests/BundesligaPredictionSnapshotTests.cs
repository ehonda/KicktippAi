using System.Text;
using EHonda.KicktippAi.Core;
using NodaTime;
using NodaTime.Text;

namespace Core.Tests;

public class BundesligaPredictionSnapshotTests
{
    [Test]
    public async Task Match_and_bonus_canonical_bytes_hashes_and_round_trips_are_exact()
    {
        var match = BundesligaPredictionContractTestData.Match();
        var bonus = BundesligaPredictionContractTestData.Bonus();
        var expectedMatch = "{\"schemaVersion\":\"bundesliga-match-snapshot-v1\",\"key\":{\"seasonPartition\":\"bundesliga-2026-27\",\"postingCommunity\":\"pes-squad\",\"itemKind\":\"match\",\"kicktippItemId\":\"42\"},\"subcompetition\":\"bundesliga\",\"exactRound\":\"1. Spieltag\",\"resultBasis\":\"regularTime90Minutes\",\"homeTeam\":\"FC Example\",\"awayTeam\":\"SV Sample\",\"matchday\":1,\"scheduledInstant\":\"2026-09-01T18:00:00Z\"}";
        var expectedBonus = "{\"schemaVersion\":\"bundesliga-bonus-snapshot-v1\",\"key\":{\"seasonPartition\":\"bundesliga-2026-27\",\"postingCommunity\":\"pes-squad\",\"itemKind\":\"bonus\",\"kicktippItemId\":\"84\"},\"subcompetition\":\"bundesliga\",\"text\":\"Wer wird Meister?\",\"deadline\":\"2026-08-28T16:30:00Z\",\"maxSelections\":1,\"options\":[{\"id\":\"a\",\"text\":\"FC Example\"},{\"id\":\"b\",\"text\":\"SV Sample\"}]}";

        await Assert.That(Encoding.UTF8.GetString(match.SerializeCanonical())).IsEqualTo(expectedMatch);
        await Assert.That(Encoding.UTF8.GetString(bonus.SerializeCanonical())).IsEqualTo(expectedBonus);
        await Assert.That(TypedMatchSnapshot.DeserializeCanonical(match.SerializeCanonical())).IsEqualTo(match);
        await Assert.That(TypedBonusSnapshot.DeserializeCanonical(bonus.SerializeCanonical())).IsEqualTo(bonus);
        await Assert.That(match.SnapshotHash.Sha256).IsEqualTo(Sha256(match.SerializeCanonical()));
        await Assert.That(bonus.SnapshotHash.Sha256).IsEqualTo(Sha256(bonus.SerializeCanonical()));
    }

    [Test]
    public async Task Strict_snapshot_loaders_reject_unknown_missing_extra_reordered_and_wrong_type_values()
    {
        var canonical = Encoding.UTF8.GetString(
            BundesligaPredictionContractTestData.Match().SerializeCanonical());
        var mutations = new[]
        {
            canonical.Replace("\"schemaVersion\":\"bundesliga-match-snapshot-v1\",", "", StringComparison.Ordinal),
            canonical.Replace("{", "{\"extra\":true,", StringComparison.Ordinal),
            canonical.Replace("\"schemaVersion\":\"bundesliga-match-snapshot-v1\",\"key\"", "\"key\":{},\"schemaVersion\"", StringComparison.Ordinal),
            canonical.Replace("\"matchday\":1", "\"matchday\":\"1\"", StringComparison.Ordinal),
            canonical.Replace("\"subcompetition\":\"bundesliga\"", "\"subcompetition\":\"world-cup\"", StringComparison.Ordinal),
            canonical.Replace("\"itemKind\":\"match\"", "\"itemKind\":\"bonus\"", StringComparison.Ordinal),
            canonical + "\n"
        };

        foreach (var mutation in mutations)
        {
            await Assert.That(() => TypedMatchSnapshot.DeserializeCanonical(Encoding.UTF8.GetBytes(mutation)))
                .Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Invalid_scheduled_evidence_never_forms_a_snapshot()
    {
        var valid = new BundesligaFixtureDetailScheduleEvidence("42", BundesligaPredictionContractTestData.MatchTime);
        var rejected = new Action[]
        {
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", true, null), [valid]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", true, BundesligaPredictionContractTestData.MatchTime), [valid]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime, true), [valid]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, null), [valid]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime), []),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime), [valid, valid]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime), [new("42", "not-a-time")]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime), [new("43", BundesligaPredictionContractTestData.MatchTime)]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, BundesligaPredictionContractTestData.MatchTime), [new("42", "2026-09-01T19:00:00Z")]),
            () => Resolve(new BundesligaFixtureScheduleEvidence("42", false, InstantPattern.ExtendedIso.Format(Instant.MinValue)), [new("42", InstantPattern.ExtendedIso.Format(Instant.MinValue))])
        };

        _ = Resolve(
            new BundesligaFixtureScheduleEvidence("41", false, BundesligaPredictionContractTestData.MatchTime),
            [new BundesligaFixtureDetailScheduleEvidence("41", BundesligaPredictionContractTestData.MatchTime)]);
        foreach (var action in rejected)
        {
            await Assert.That(action).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Same_id_reschedule_preserves_key_and_rotates_snapshot_hash()
    {
        var before = BundesligaPredictionContractTestData.Match();
        var after = BundesligaPredictionContractTestData.Match(
            scheduledInstant: "2026-09-02T18:00:00Z");

        await Assert.That(after.Key).IsEqualTo(before.Key);
        await Assert.That(after.SnapshotHash).IsNotEqualTo(before.SnapshotHash);
        await Assert.That(after.SerializeCanonical().SequenceEqual(before.SerializeCanonical())).IsFalse();
    }

    [Test]
    public async Task Bonus_option_order_is_semantic_and_duplicate_options_are_rejected()
    {
        var original = BundesligaPredictionContractTestData.Bonus();
        var reordered = BundesligaPredictionContractTestData.Bonus(options:
        [
            new TypedBonusSnapshotOption("b", "SV Sample"),
            new TypedBonusSnapshotOption("a", "FC Example")
        ]);

        await Assert.That(reordered.SnapshotHash).IsNotEqualTo(original.SnapshotHash);
        await Assert.That(() => BundesligaPredictionContractTestData.Bonus(options:
        [
            new TypedBonusSnapshotOption("a", "FC Example"),
            new TypedBonusSnapshotOption("a", "SV Sample")
        ])).Throws<InvalidDataException>();
    }

    private static BundesligaResolvedScheduledInstant Resolve(
        BundesligaFixtureScheduleEvidence fixture,
        IEnumerable<BundesligaFixtureDetailScheduleEvidence> details) =>
        BundesligaScheduledInstantResolver.Resolve(fixture, details);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
}
