using System.Collections.Immutable;

namespace EHonda.KicktippAi.Core;

public sealed class BundesligaValidatedMatchItem
{
    private BundesligaValidatedMatchItem(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedEntry seedEntry,
        BundesligaPredictionRouteContract route,
        TypedMatchSnapshot snapshot) =>
        (Authority, PostingSeed, SeedEntry, Route, Snapshot) =
        (authority, postingSeed, seedEntry, route, snapshot);

    public BundesligaPredictionAuthority Authority { get; }
    public BundesligaIdentitySeedGeneration PostingSeed { get; }
    public BundesligaIdentitySeedEntry SeedEntry { get; }
    public BundesligaPredictionRouteContract Route { get; }
    public TypedMatchSnapshot Snapshot { get; }
    public StableLocalItemKey Key => Snapshot.Key;

    internal static BundesligaValidatedMatchItem Create(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedEntry seedEntry,
        BundesligaPredictionRouteContract route,
        TypedMatchSnapshot snapshot) =>
        new(authority, postingSeed, seedEntry, route, snapshot);
}

public sealed class BundesligaValidatedBonusItem
{
    private BundesligaValidatedBonusItem(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedEntry seedEntry,
        BundesligaPredictionRouteContract route,
        TypedBonusSnapshot snapshot) =>
        (Authority, PostingSeed, SeedEntry, Route, Snapshot) =
        (authority, postingSeed, seedEntry, route, snapshot);

    public BundesligaPredictionAuthority Authority { get; }
    public BundesligaIdentitySeedGeneration PostingSeed { get; }
    public BundesligaIdentitySeedEntry SeedEntry { get; }
    public BundesligaPredictionRouteContract Route { get; }
    public TypedBonusSnapshot Snapshot { get; }
    public StableLocalItemKey Key => Snapshot.Key;

    internal static BundesligaValidatedBonusItem Create(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        BundesligaIdentitySeedEntry seedEntry,
        BundesligaPredictionRouteContract route,
        TypedBonusSnapshot snapshot) =>
        new(authority, postingSeed, seedEntry, route, snapshot);
}

public sealed class BundesligaValidatedMatchInventory
{
    private readonly ImmutableArray<BundesligaValidatedMatchItem> _items;

    private BundesligaValidatedMatchInventory(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<BundesligaValidatedMatchItem> items)
    {
        Authority = authority;
        PostingSeed = postingSeed;
        _items = items.ToImmutableArray();
    }

    public BundesligaPredictionAuthority Authority { get; }
    public BundesligaIdentitySeedGeneration PostingSeed { get; }
    public IReadOnlyList<BundesligaValidatedMatchItem> Items => _items;

    public BundesligaValidatedMatchItem Require(StableLocalItemKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.SingleOrDefault(item => item.Key == key)
            ?? throw new InvalidDataException($"Validated match inventory does not contain '{key.KicktippItemId}'.");
    }

    internal static BundesligaValidatedMatchInventory Create(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<BundesligaValidatedMatchItem> items) =>
        new(authority, postingSeed, items);
}

public sealed class BundesligaValidatedBonusInventory
{
    private readonly ImmutableArray<BundesligaValidatedBonusItem> _items;

    private BundesligaValidatedBonusInventory(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<BundesligaValidatedBonusItem> items)
    {
        Authority = authority;
        PostingSeed = postingSeed;
        _items = items.ToImmutableArray();
    }

    public BundesligaPredictionAuthority Authority { get; }
    public BundesligaIdentitySeedGeneration PostingSeed { get; }
    public IReadOnlyList<BundesligaValidatedBonusItem> Items => _items;

    public BundesligaValidatedBonusItem Require(StableLocalItemKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.SingleOrDefault(item => item.Key == key)
            ?? throw new InvalidDataException($"Validated bonus inventory does not contain '{key.KicktippItemId}'.");
    }

    internal static BundesligaValidatedBonusInventory Create(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<BundesligaValidatedBonusItem> items) =>
        new(authority, postingSeed, items);
}

public static class BundesligaPredictionInventoryGate
{
    public static BundesligaValidatedMatchInventory ValidateMatches(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<StableLocalItemKey> expectedKeys,
        IEnumerable<TypedMatchSnapshot> observedSnapshots,
        BundesligaPredictionRouteCatalog routes)
    {
        ValidateAuthorityAndSeed(authority, postingSeed);
        ArgumentNullException.ThrowIfNull(expectedKeys);
        ArgumentNullException.ThrowIfNull(observedSnapshots);
        ArgumentNullException.ThrowIfNull(routes);

        var expected = MaterializeExpected(
            expectedKeys, authority.PostingCommunity, BundesligaPredictionItemKind.Match);
        var observed = MaterializeObserved(observedSnapshots, snapshot => snapshot.Key, "match");
        RequireSameScope(expected, observed.Select(snapshot => snapshot.Key), "match");

        var items = observed.Select(snapshot =>
        {
            var entry = postingSeed.RequireEntry(snapshot.Key);
            if (entry.MatchSnapshot is not { } pinned
                || entry.BonusSnapshot is not null
                || entry.SnapshotHash != snapshot.SnapshotHash
                || pinned.SnapshotHash != snapshot.SnapshotHash
                || pinned.Subcompetition != snapshot.Subcompetition
                || !pinned.SerializeCanonical().SequenceEqual(snapshot.SerializeCanonical()))
            {
                throw new InvalidDataException(
                    $"Observed match '{snapshot.Key.KicktippItemId}' differs from its exact pinned seed entry.");
            }

            var route = routes.Require(
                entry.RouteId, BundesligaPredictionItemKind.Match, snapshot.Subcompetition);
            return BundesligaValidatedMatchItem.Create(authority, postingSeed, entry, route, snapshot);
        }).OrderBy(item => item.Key.KicktippItemId, StringComparer.Ordinal);

        return BundesligaValidatedMatchInventory.Create(authority, postingSeed, items);
    }

    public static BundesligaValidatedBonusInventory ValidateBonus(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed,
        IEnumerable<StableLocalItemKey> expectedKeys,
        IEnumerable<TypedBonusSnapshot> observedSnapshots,
        BundesligaPredictionRouteCatalog routes)
    {
        ValidateAuthorityAndSeed(authority, postingSeed);
        ArgumentNullException.ThrowIfNull(expectedKeys);
        ArgumentNullException.ThrowIfNull(observedSnapshots);
        ArgumentNullException.ThrowIfNull(routes);

        var expected = MaterializeExpected(
            expectedKeys, authority.PostingCommunity, BundesligaPredictionItemKind.Bonus);
        var observed = MaterializeObserved(observedSnapshots, snapshot => snapshot.Key, "bonus");
        RequireSameScope(expected, observed.Select(snapshot => snapshot.Key), "bonus");

        var items = observed.Select(snapshot =>
        {
            var entry = postingSeed.RequireEntry(snapshot.Key);
            if (entry.BonusSnapshot is not { } pinned
                || entry.MatchSnapshot is not null
                || entry.SnapshotHash != snapshot.SnapshotHash
                || pinned.SnapshotHash != snapshot.SnapshotHash
                || pinned.Subcompetition != snapshot.Subcompetition
                || !pinned.SerializeCanonical().SequenceEqual(snapshot.SerializeCanonical()))
            {
                throw new InvalidDataException(
                    $"Observed bonus '{snapshot.Key.KicktippItemId}' differs from its exact pinned seed entry.");
            }

            var route = routes.Require(
                entry.RouteId, BundesligaPredictionItemKind.Bonus, snapshot.Subcompetition);
            return BundesligaValidatedBonusItem.Create(authority, postingSeed, entry, route, snapshot);
        }).OrderBy(item => item.Key.KicktippItemId, StringComparer.Ordinal);

        return BundesligaValidatedBonusInventory.Create(authority, postingSeed, items);
    }

    private static void ValidateAuthorityAndSeed(
        BundesligaPredictionAuthority authority,
        BundesligaIdentitySeedGeneration postingSeed)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(postingSeed);
        if (!string.Equals(
                authority.PostingCommunity, postingSeed.PostingCommunity, StringComparison.Ordinal)
            || authority.PostingSeed != postingSeed.Reference)
        {
            throw new InvalidDataException(
                "Inventory authority does not pin the exact Posting Community seed generation.");
        }
    }

    private static ImmutableArray<StableLocalItemKey> MaterializeExpected(
        IEnumerable<StableLocalItemKey> keys,
        string postingCommunity,
        BundesligaPredictionItemKind itemKind)
    {
        var materialized = keys.ToArray();
        if (materialized.Any(key => key is null))
        {
            throw new InvalidDataException("Expected inventory keys cannot contain null.");
        }

        RequireNoDuplicateKeys(materialized, "expected");
        if (materialized.Any(key => key.ItemKind != itemKind
                || !string.Equals(key.SeasonPartition,
                    BundesligaPredictionAuthority.SeasonPartitionValue, StringComparison.Ordinal)
                || !string.Equals(key.PostingCommunity, postingCommunity, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Expected inventory key is outside the exact authority scope.");
        }

        return materialized.ToImmutableArray();
    }

    private static ImmutableArray<TSnapshot> MaterializeObserved<TSnapshot>(
        IEnumerable<TSnapshot> snapshots,
        Func<TSnapshot, StableLocalItemKey> key,
        string kind) where TSnapshot : class
    {
        var materialized = snapshots.ToArray();
        if (materialized.Any(snapshot => snapshot is null))
        {
            throw new InvalidDataException($"Observed {kind} inventory cannot contain null.");
        }

        RequireNoDuplicateKeys(materialized.Select(key), "observed");
        return materialized.ToImmutableArray();
    }

    private static void RequireNoDuplicateKeys(
        IEnumerable<StableLocalItemKey> keys,
        string origin)
    {
        var duplicate = keys.GroupBy(key => key).FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"The {origin} inventory duplicates local item '{duplicate.Key.KicktippItemId}'.");
        }
    }

    private static void RequireSameScope(
        IReadOnlyCollection<StableLocalItemKey> expected,
        IEnumerable<StableLocalItemKey> observedKeys,
        string kind)
    {
        var observed = observedKeys.ToArray();
        if (expected.Count != observed.Length
            || expected.Except(observed).Any()
            || observed.Except(expected).Any())
        {
            throw new InvalidDataException(
                $"Expected and observed {kind} inventories are not one exact scope.");
        }
    }
}
