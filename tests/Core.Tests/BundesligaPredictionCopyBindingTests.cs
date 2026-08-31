using System.Text;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaPredictionCopyBindingTests
{
    [Test]
    public async Task Binding_round_trips_with_exact_pinned_seeds_and_total_option_projection()
    {
        var posting = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var source = BundesligaPredictionContractTestData.Seed("pes-squad");
        var binding = BundesligaPredictionContractTestData.Binding(posting, source);
        var reversed = BundesligaCopyBindingGeneration.Create(
            binding.PostingCommunity, binding.SourceCommunity, binding.Generation, binding.Predecessor,
            binding.SourceEvidenceIdentity, posting, source, binding.Entries.Reverse());
        var restored = BundesligaCopyBindingGeneration.DeserializeCanonical(
            binding.SerializeCanonical(), posting, source, BundesligaPredictionContractTestData.Routes());

        await Assert.That(restored.SerializeCanonical()).IsEquivalentTo(binding.SerializeCanonical());
        await Assert.That(binding.CanonicalSha256).IsEqualTo("da84af867d286ccdfbbae005c484c529c8f85f342d599563773dd1c5c2ab7397");
        await Assert.That(reversed.SerializeCanonical()).IsEquivalentTo(binding.SerializeCanonical());
        await Assert.That(reversed.CanonicalSha256).IsEqualTo(binding.CanonicalSha256);
        await Assert.That(restored.PostingSeed).IsEqualTo(posting.Reference);
        await Assert.That(restored.SourceSeed).IsEqualTo(source.Reference);
        await Assert.That(restored.RequirePostingItem(BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt")).OptionProjection.Count)
            .IsEqualTo(2);
    }

    [Test]
    public async Task Bonus_binding_rejects_partial_many_to_one_and_unknown_route()
    {
        var posting = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var source = BundesligaPredictionContractTestData.Seed("pes-squad");
        var routes = BundesligaPredictionContractTestData.Routes();
        async Task Reject(IEnumerable<BundesligaBonusOptionProjection> projection) =>
            await Assert.That(() => BundesligaCopyBindingEntry.CreateBonus(
                BundesligaPredictionContractTestData.BonusRoute,
                posting, BundesligaPredictionContractTestData.BonusKey("relaxdays-tippt"),
                source, BundesligaPredictionContractTestData.BonusKey("pes-squad"), projection, routes))
                .Throws<InvalidDataException>();

        await Reject([new("a", "a")]);
        await Reject([new("a", "a"), new("b", "a")]);
        await Assert.That(() => BundesligaCopyBindingEntry.CreateMatch(
            "unknown-route", posting, BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt"),
            source, BundesligaPredictionContractTestData.MatchKey("pes-squad"), routes))
            .Throws<InvalidDataException>();

        var aliasRoutes = new BundesligaPredictionRouteCatalog(
        [
            new BundesligaPredictionRouteContract(
                "alias-match-route", BundesligaPredictionItemKind.Match,
                BundesligaSeasonSubcompetition.Bundesliga)
        ]);
        await Assert.That(() => BundesligaCopyBindingEntry.CreateMatch(
            "alias-match-route", posting, BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt"),
            source, BundesligaPredictionContractTestData.MatchKey("pes-squad"), aliasRoutes))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Binding_rejects_drift_noncanonical_json_and_duplicate_endpoints()
    {
        var posting = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        var source = BundesligaPredictionContractTestData.Seed("pes-squad");
        var routes = BundesligaPredictionContractTestData.Routes();
        var binding = BundesligaPredictionContractTestData.Binding(posting, source);
        var json = Encoding.UTF8.GetString(binding.SerializeCanonical());
        foreach (var mutation in new[]
        {
            json.Replace(posting.CanonicalSha256, BundesligaPredictionContractTestData.ShaA, StringComparison.Ordinal),
            json.Replace("\"schemaVersion\":\"bundesliga-copy-binding-v1\"", "\"schemaVersion\":\"other\"", StringComparison.Ordinal),
            json.Replace("\"entries\":[", "\"unexpected\":0,\"entries\":[", StringComparison.Ordinal),
            " " + json
        })
        {
            await Assert.That(() => BundesligaCopyBindingGeneration.DeserializeCanonical(
                Encoding.UTF8.GetBytes(mutation), posting, source, routes)).Throws<InvalidDataException>();
        }

        var entry = binding.Entries[0];
        await Assert.That(() => BundesligaCopyBindingGeneration.Create(
            posting.PostingCommunity, source.PostingCommunity, 1, null, "evidence",
            posting, source, [entry, entry])).Throws<InvalidDataException>();
    }
}
