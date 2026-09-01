using EHonda.KicktippAi.Core;

namespace Core.Tests;

public sealed class BundesligaPredictionInventoryGateTests
{
    [Test]
    public async Task Match_and_bonus_inventories_require_exact_seed_entries_and_sort_by_Kicktipp_ID()
    {
        var routes = BundesligaPredictionContractTestData.Routes();
        var match2 = BundesligaPredictionContractTestData.Match(id: "2");
        var match1 = BundesligaPredictionContractTestData.Match(id: "1");
        var bonus2 = BundesligaPredictionContractTestData.Bonus(id: "b2");
        var bonus1 = BundesligaPredictionContractTestData.Bonus(id: "b1");
        var seed = BundesligaIdentitySeedGeneration.Create(
            "pes-squad", 1, null, "inventory-order",
            [
                BundesligaIdentitySeedEntry.ForMatch(
                    BundesligaPredictionContractTestData.MatchRoute, match2, routes),
                BundesligaIdentitySeedEntry.ForBonus(
                    BundesligaPredictionContractTestData.BonusRoute, bonus2, routes),
                BundesligaIdentitySeedEntry.ForMatch(
                    BundesligaPredictionContractTestData.MatchRoute, match1, routes),
                BundesligaIdentitySeedEntry.ForBonus(
                    BundesligaPredictionContractTestData.BonusRoute, bonus1, routes)
            ],
            routes);
        var authority = BundesligaPredictionContractTestData.DirectAuthority(seed);

        var matches = BundesligaPredictionInventoryGate.ValidateMatches(
            authority, seed, [match2.Key, match1.Key], [match2, match1], routes);
        var bonus = BundesligaPredictionInventoryGate.ValidateBonus(
            authority, seed, [bonus2.Key, bonus1.Key], [bonus2, bonus1], routes);

        await Assert.That(matches.Items.Select(item => item.Key.KicktippItemId)
            .SequenceEqual(["1", "2"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(bonus.Items.Select(item => item.Key.KicktippItemId)
            .SequenceEqual(["b1", "b2"], StringComparer.Ordinal)).IsTrue();
        await Assert.That(matches.Items.All(item => item.Authority == authority
            && item.PostingSeed == seed
            && item.SeedEntry.MatchSnapshot == item.Snapshot
            && item.Route.RouteId == BundesligaPredictionContractTestData.MatchRoute)).IsTrue();
        await Assert.That(bonus.Items.All(item => item.Authority == authority
            && item.PostingSeed == seed
            && item.SeedEntry.BonusSnapshot == item.Snapshot
            && item.Route.RouteId == BundesligaPredictionContractTestData.BonusRoute)).IsTrue();
    }

    [Test]
    public async Task Exactly_empty_expected_and_observed_scopes_are_valid_and_immutable()
    {
        var seed = BundesligaPredictionContractTestData.Seed();
        var authority = BundesligaPredictionContractTestData.DirectAuthority(seed);
        var expected = new List<StableLocalItemKey>();
        var observed = new List<TypedMatchSnapshot>();

        var inventory = BundesligaPredictionInventoryGate.ValidateMatches(
            authority, seed, expected, observed, BundesligaPredictionContractTestData.Routes());
        expected.Add(BundesligaPredictionContractTestData.MatchKey());
        observed.Add(BundesligaPredictionContractTestData.Match());

        await Assert.That(inventory.Items).Count().IsEqualTo(0);
        await Assert.That(inventory.GetType().GetConstructors()).Count().IsEqualTo(0);
        await Assert.That(typeof(BundesligaValidatedMatchItem).GetConstructors()).Count().IsEqualTo(0);
        await Assert.That(typeof(BundesligaValidatedBonusItem).GetConstructors()).Count().IsEqualTo(0);
        await Assert.That(inventory.Items is IList<BundesligaValidatedMatchItem>).IsTrue();
        await Assert.That(() => ((IList<BundesligaValidatedMatchItem>)inventory.Items).Clear())
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task Expected_and_observed_duplicates_are_rejected_before_scope_comparison()
    {
        var seed = BundesligaPredictionContractTestData.Seed();
        var authority = BundesligaPredictionContractTestData.DirectAuthority(seed);
        var snapshot = BundesligaPredictionContractTestData.Match();

        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority,
                seed,
                [snapshot.Key, snapshot.Key],
                [snapshot],
                BundesligaPredictionContractTestData.Routes()))
            .Throws<InvalidDataException>().WithMessageContaining("expected inventory duplicates");
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority,
                seed,
                [snapshot.Key],
                [snapshot, snapshot],
                BundesligaPredictionContractTestData.Routes()))
            .Throws<InvalidDataException>().WithMessageContaining("observed inventory duplicates");
    }

    [Test]
    public async Task Missing_extra_cross_community_and_wrong_kind_scopes_fail_closed()
    {
        var seed = BundesligaPredictionContractTestData.Seed();
        var authority = BundesligaPredictionContractTestData.DirectAuthority(seed);
        var snapshot = BundesligaPredictionContractTestData.Match();
        var routes = BundesligaPredictionContractTestData.Routes();

        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed, [snapshot.Key], [], routes))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed, [], [snapshot], routes))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed,
                [BundesligaPredictionContractTestData.MatchKey("relaxdays-tippt")],
                [snapshot], routes))
            .Throws<InvalidDataException>();
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed,
                [BundesligaPredictionContractTestData.BonusKey()],
                [snapshot], routes))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Seed_authority_snapshot_hash_canonical_bytes_and_route_drift_are_rejected()
    {
        var seed = BundesligaPredictionContractTestData.Seed();
        var authority = BundesligaPredictionContractTestData.DirectAuthority(seed);
        var original = BundesligaPredictionContractTestData.Match();
        var rescheduled = BundesligaPredictionContractTestData.Match(
            scheduledInstant: "2026-09-02T18:00:00Z");

        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed, [rescheduled.Key], [rescheduled],
                BundesligaPredictionContractTestData.Routes()))
            .Throws<InvalidDataException>().WithMessageContaining("pinned seed entry");

        var otherSeed = BundesligaPredictionContractTestData.Seed("relaxdays-tippt");
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, otherSeed, [], [], BundesligaPredictionContractTestData.Routes()))
            .Throws<InvalidDataException>().WithMessageContaining("exact Posting Community seed");

        var wrongRoutes = new BundesligaPredictionRouteCatalog(
        [
            new BundesligaPredictionRouteContract(
                "other-match-route",
                BundesligaPredictionItemKind.Match,
                BundesligaSeasonSubcompetition.Bundesliga)
        ]);
        await Assert.That(() => BundesligaPredictionInventoryGate.ValidateMatches(
                authority, seed, [original.Key], [original], wrongRoutes))
            .Throws<InvalidDataException>();
    }
}
