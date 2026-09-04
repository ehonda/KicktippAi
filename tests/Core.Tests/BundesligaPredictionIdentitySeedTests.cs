using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaPredictionIdentitySeedTests
{
    [Test]
    public async Task Canonical_seed_round_trips_and_is_independent_of_input_order()
    {
        var seed = BundesligaPredictionContractTestData.Seed();
        var reversed = BundesligaIdentitySeedGeneration.Create(
            seed.PostingCommunity, seed.Generation, seed.Predecessor, seed.SourceEvidenceIdentity,
            seed.Entries.Reverse(), BundesligaPredictionContractTestData.Routes());
        var restored = BundesligaIdentitySeedGeneration.DeserializeCanonical(seed.SerializeCanonical(), BundesligaPredictionContractTestData.Routes());

        await Assert.That(restored.SerializeCanonical()).IsEquivalentTo(seed.SerializeCanonical());
        await Assert.That(restored.CanonicalSha256).IsEqualTo(seed.CanonicalSha256);
        await Assert.That(reversed.SerializeCanonical()).IsEquivalentTo(seed.SerializeCanonical());
        await Assert.That(reversed.CanonicalSha256).IsEqualTo(seed.CanonicalSha256);
        await Assert.That(seed.CanonicalSha256).IsEqualTo("78850c80996acccb10d7ccde1da750ea5ad939595b960174fc67eadc2fb6fc66");
        await Assert.That(seed.Entries.Select(entry => entry.Key.ItemKind).ToArray())
            .IsEquivalentTo([BundesligaPredictionItemKind.Bonus, BundesligaPredictionItemKind.Match]);
    }

    [Test]
    public async Task Seed_rejects_duplicate_scope_and_noncanonical_or_drifting_content()
    {
        var routes = BundesligaPredictionContractTestData.Routes();
        var match = BundesligaPredictionContractTestData.Match();
        var entry = BundesligaIdentitySeedEntry.ForMatch(BundesligaPredictionContractTestData.MatchRoute, match, routes);
        await Assert.That(() => BundesligaIdentitySeedGeneration.Create("pes-squad", 1, null, "evidence", [entry, entry], routes))
            .Throws<InvalidDataException>();

        var foreign = BundesligaIdentitySeedEntry.ForMatch(
            BundesligaPredictionContractTestData.MatchRoute,
            BundesligaPredictionContractTestData.Match("other-community"), routes);
        await Assert.That(() => BundesligaIdentitySeedGeneration.Create("pes-squad", 1, null, "evidence", [foreign], routes))
            .Throws<InvalidDataException>();

        var seed = BundesligaPredictionContractTestData.Seed();
        var json = Encoding.UTF8.GetString(seed.SerializeCanonical());
        foreach (var mutation in new[]
        {
            json.Replace("\"schemaVersion\":\"bundesliga-identity-seed-v1\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
            json.Replace("\"seasonPartition\":\"bundesliga-2026-27\"", "\"seasonPartition\":\"wm26\"", StringComparison.Ordinal),
            json.Replace("\"snapshotHash\":{", "\"extra\":true,\"snapshotHash\":{", StringComparison.Ordinal),
            " " + json
        })
        {
            await Assert.That(() => BundesligaIdentitySeedGeneration.DeserializeCanonical(Encoding.UTF8.GetBytes(mutation), routes))
                .Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Additive_reschedule_preserves_key_rotates_snapshot_and_pins_predecessor()
    {
        var first = BundesligaPredictionContractTestData.Seed();
        var second = BundesligaPredictionContractTestData.Seed(
            generation: 2,
            predecessor: BundesligaGenerationPredecessor.Create(1, first.CanonicalSha256),
            matchTime: "2026-09-01T19:00:00Z");
        var oldMatch = first.RequireEntry(BundesligaPredictionContractTestData.MatchKey());
        var newMatch = second.RequireEntry(BundesligaPredictionContractTestData.MatchKey());

        await Assert.That(newMatch.Key).IsEqualTo(oldMatch.Key);
        await Assert.That(newMatch.SnapshotHash).IsNotEqualTo(oldMatch.SnapshotHash);
        await Assert.That(second.Predecessor!.Sha256).IsEqualTo(first.CanonicalSha256);
        await Assert.That(() => BundesligaPredictionContractTestData.Seed(generation: 2, predecessor: null))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionContractTestData.Seed(
            generation: 3,
            predecessor: BundesligaGenerationPredecessor.Create(1, first.CanonicalSha256)))
            .Throws<InvalidDataException>();
    }
}
