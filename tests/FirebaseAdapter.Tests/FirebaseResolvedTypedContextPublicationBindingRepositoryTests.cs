using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
public sealed class FirebaseResolvedTypedContextPublicationBindingRepositoryTests(FirestoreFixture fixture)
{
    private const string Collection = "resolved-typed-context-publication-bindings";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Test]
    public async Task Exact_binding_round_trips_canonical_bytes_and_only_advances_a_newer_observation()
    {
        var repository = CreateRepository();
        var seed = NewSeed();
        var initial = Binding(seed, Now.AddMinutes(-10));
        var created = await repository.UpsertExactAsync(initial);
        var older = await repository.UpsertExactAsync(Binding(seed, Now.AddMinutes(-11)));
        var newer = await repository.UpsertExactAsync(Binding(seed, Now.AddMinutes(-1)));

        await Assert.That(created.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Created);
        await Assert.That(older.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.NoOp);
        await Assert.That(older.EffectiveBinding).IsEqualTo(initial);
        await Assert.That(newer.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Updated);
        await Assert.That(await repository.GetExactAsync(initial.Key)).IsEqualTo(newer.EffectiveBinding);

        var snapshot = await fixture.Db.Collection(Collection).Document(initial.Key.PhysicalId).GetSnapshotAsync();
        var carrier = snapshot.ConvertTo<FirestoreResolvedTypedContextPublicationBinding>();
        await Assert.That(carrier.CanonicalJsonUtf8.ToByteArray()).IsEquivalentTo(newer.EffectiveBinding.SerializeCanonical());
    }

    [Test]
    public async Task Exact_binding_rejects_cross_key_carriers_and_future_or_drifted_candidates_without_replacement()
    {
        var repository = CreateRepository();
        var seed = NewSeed();
        var initial = Binding(seed, Now.AddMinutes(-5));
        await repository.UpsertExactAsync(initial);
        var other = Binding(seed, Now.AddMinutes(-4), community: "Schadensfresse");
        await fixture.Db.Collection(Collection).Document(other.Key.PhysicalId).SetAsync(new FirestoreResolvedTypedContextPublicationBinding
        {
            Id = other.Key.PhysicalId,
            CanonicalJsonUtf8 = ByteString.CopyFrom(initial.SerializeCanonical())
        });

        await Assert.That(() => repository.GetExactAsync(other.Key)).Throws<InvalidDataException>();
        await Assert.That(() => repository.UpsertExactAsync(Binding(seed, Now.AddHours(1)))).Throws<InvalidDataException>();

        var drift = new ResolvedTypedContextPublicationBinding(initial.SeasonPartition, initial.CommunityContext,
            initial.ProfileId, initial.RoutingSeedSha256, initial.BundesligaSeasonSubcompetition,
            Now.AddMinutes(-2), initial.RulesSchemaVersion, initial.CanonicalRulesSha256,
            new ResolvedTypedContextDocument("Context", initial.Document.Name, initial.Document.Version + 1, initial.Document.ContentSha256));
        var result = await repository.UpsertExactAsync(drift);
        await Assert.That(result.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.IdentityDrift);
        await Assert.That(await repository.GetExactAsync(initial.Key)).IsEqualTo(initial);
    }

    [Test]
    public async Task Concurrent_identity_equal_candidates_converge_on_the_greatest_observation()
    {
        var repository = CreateRepository();
        var seed = NewSeed();
        var older = Binding(seed, Now.AddMinutes(-12));
        var newer = Binding(seed, Now.AddMinutes(-2));
        var results = await Task.WhenAll(repository.UpsertExactAsync(older), repository.UpsertExactAsync(newer));

        await Assert.That(await repository.GetExactAsync(older.Key)).IsEqualTo(newer);
        await Assert.That(results.All(result => result.EffectiveBinding.RulesObservedAt <= newer.RulesObservedAt)).IsTrue();
    }

    [Test]
    public async Task Stale_stored_binding_is_refreshable_but_not_current_until_a_fresh_candidate_commits()
    {
        var repository = CreateRepository();
        var seed = NewSeed();
        var stale = Binding(seed, Now.AddHours(-25));
        await fixture.Db.Collection(Collection).Document(stale.Key.PhysicalId).SetAsync(new FirestoreResolvedTypedContextPublicationBinding
        {
            Id = stale.Key.PhysicalId,
            CanonicalJsonUtf8 = ByteString.CopyFrom(stale.SerializeCanonical())
        });
        await Assert.That(() => repository.GetExactAsync(stale.Key)).Throws<InvalidDataException>();

        var fresh = Binding(seed, Now.AddMinutes(-1));
        var result = await repository.UpsertExactAsync(fresh);

        await Assert.That(result.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Updated);
        await Assert.That(result.EffectiveBinding).IsEqualTo(fresh);
        await Assert.That(await repository.GetExactAsync(stale.Key)).IsEqualTo(fresh);
    }

    [Test]
    public async Task Exact_binding_rejects_malformed_canonical_carriers_and_future_stored_state()
    {
        var repository = CreateRepository();
        var seed = NewSeed();
        var binding = Binding(seed, Now.AddMinutes(-1));
        var reference = fixture.Db.Collection(Collection).Document(binding.Key.PhysicalId);

        await reference.SetAsync(new FirestoreResolvedTypedContextPublicationBinding
        {
            Id = binding.Key.PhysicalId, CanonicalJsonUtf8 = ByteString.CopyFromUtf8("{}")
        });
        await Assert.That(() => repository.GetExactAsync(binding.Key)).Throws<InvalidDataException>();

        var future = Binding(seed, Now.AddHours(1));
        await reference.SetAsync(new FirestoreResolvedTypedContextPublicationBinding
        {
            Id = binding.Key.PhysicalId, CanonicalJsonUtf8 = ByteString.CopyFrom(future.SerializeCanonical())
        });
        await Assert.That(() => repository.UpsertExactAsync(binding)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Binding_dispositions_have_exact_effective_values_and_only_create_or_update_select_writes()
    {
        var selections = new ConcurrentQueue<TypedContextPublicationBindingUpsertResult>();
        var repository = CreateRepository(selections.Enqueue);
        var seed = NewSeed();
        var initial = Binding(seed, Now.AddMinutes(-10));
        var equal = Binding(seed, Now.AddMinutes(-10));
        var older = Binding(seed, Now.AddMinutes(-11));
        var newer = Binding(seed, Now.AddMinutes(-1));
        var drift = new ResolvedTypedContextPublicationBinding(initial.SeasonPartition, initial.CommunityContext,
            initial.ProfileId, initial.RoutingSeedSha256, initial.BundesligaSeasonSubcompetition, Now.AddMinutes(-1),
            initial.RulesSchemaVersion, initial.CanonicalRulesSha256,
            new ResolvedTypedContextDocument("Context", initial.Document.Name, initial.Document.Version + 1, initial.Document.ContentSha256));

        var created = await repository.UpsertExactAsync(initial);
        var equalResult = await repository.UpsertExactAsync(equal);
        var olderResult = await repository.UpsertExactAsync(older);
        var updated = await repository.UpsertExactAsync(newer);
        var driftResult = await repository.UpsertExactAsync(drift);

        await Assert.That(created.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Created);
        await Assert.That(equalResult).IsEqualTo(TypedContextPublicationBindingUpsertResult.NoOp(initial));
        await Assert.That(olderResult).IsEqualTo(TypedContextPublicationBindingUpsertResult.NoOp(initial));
        await Assert.That(updated).IsEqualTo(TypedContextPublicationBindingUpsertResult.Updated(newer));
        await Assert.That(driftResult).IsEqualTo(TypedContextPublicationBindingUpsertResult.Drift(newer));
        await Assert.That(selections.Count(result => result.Disposition is TypedContextPublicationBindingUpsertDisposition.Created or TypedContextPublicationBindingUpsertDisposition.Updated)).IsEqualTo(2);
        await Assert.That(await repository.GetExactAsync(initial.Key)).IsEqualTo(newer);
    }

    [Test]
    public async Task Equal_creators_and_both_observation_orders_converge_to_the_maximum_including_an_initial_binding()
    {
        var equalRepository = CreateRepository();
        var equalSeed = NewSeed();
        var equal = Binding(equalSeed, Now.AddMinutes(-2));
        var equalResults = await Task.WhenAll(equalRepository.UpsertExactAsync(equal), equalRepository.UpsertExactAsync(equal));
        await Assert.That(equalResults.All(x => x.EffectiveBinding.Equals(equal))).IsTrue();
        await Assert.That(await equalRepository.GetExactAsync(equal.Key)).IsEqualTo(equal);

        foreach (var newerStartsFirst in new[] { false, true })
        {
            var repository = CreateRepository();
            var seed = NewSeed();
            var initial = Binding(seed, Now.AddMinutes(-12));
            var older = Binding(seed, Now.AddMinutes(-8));
            var newer = Binding(seed, Now.AddMinutes(-1));
            await repository.UpsertExactAsync(initial);
            var first = newerStartsFirst ? newer : older;
            var second = newerStartsFirst ? older : newer;
            var firstResult = await repository.UpsertExactAsync(first);
            var secondResult = await repository.UpsertExactAsync(second);
            await Assert.That(firstResult.EffectiveBinding.RulesObservedAt).IsLessThanOrEqualTo(newer.RulesObservedAt);
            await Assert.That(secondResult.EffectiveBinding.RulesObservedAt).IsLessThanOrEqualTo(newer.RulesObservedAt);
            await Assert.That(await repository.GetExactAsync(initial.Key)).IsEqualTo(newer);
        }
    }

    [Test]
    public async Task Concurrent_losing_candidates_return_only_a_committed_effective_binding_and_observe_retry_callbacks()
    {
        var selections = new ConcurrentQueue<TypedContextPublicationBindingUpsertResult>();
        var repository = CreateRepository(selections.Enqueue);
        var seed = NewSeed();
        var candidates = Enumerable.Range(1, 12).Select(i => Binding(seed, Now.AddMinutes(-20 + i))).ToArray();
        var results = await Task.WhenAll(candidates.Select(candidate => repository.UpsertExactAsync(candidate)));
        var committed = await repository.GetExactAsync(candidates[0].Key);

        await Assert.That(committed).IsEqualTo(candidates[^1]);
        await Assert.That(results.All(result => result.EffectiveBinding.RulesObservedAt <= committed!.RulesObservedAt)).IsTrue();
        // The observer is invoked inside the actual Firestore transaction callback, so any
        // SDK retry is observable as another selection and cannot leak an uncommitted candidate.
        await Assert.That(selections.Count).IsGreaterThanOrEqualTo(candidates.Length);
    }

    [Test]
    public async Task Deterministic_post_transaction_readback_returns_a_competing_committed_winner_never_the_losing_candidate()
    {
        var seed = NewSeed();
        var candidate = Binding(seed, Now.AddMinutes(-10));
        var winner = Binding(seed, Now.AddMinutes(-1));
        var selected = ResolvedTypedContextPublicationBindingContract.SelectEffective(null, candidate, Now);

        var completed = FirebaseResolvedTypedContextPublicationBindingRepository.CompleteSelectedTransactionReadback(
            candidate.Key, selected, winner, Now);

        await Assert.That(completed.Disposition).IsEqualTo(TypedContextPublicationBindingUpsertDisposition.Created);
        await Assert.That(completed.EffectiveBinding).IsEqualTo(winner);
        await Assert.That(completed.EffectiveBinding).IsNotEqualTo(candidate);
    }

    private FirebaseResolvedTypedContextPublicationBindingRepository CreateRepository(
        Action<TypedContextPublicationBindingUpsertResult>? selectionObserver = null) =>
        new(fixture.Db, NullLogger<FirebaseResolvedTypedContextPublicationBindingRepository>.Instance, selectionObserver);

    private static string NewSeed() => Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant().PadRight(64, 'a')[..64];

    private static ResolvedTypedContextPublicationBinding Binding(string seed, DateTimeOffset observed, string community = "schadensfresse") => new(
        CompetitionIds.Bundesliga2026_27,
        community,
        "schadensfresse-dfb-pokal-rules-only-v1",
        seed,
        BundesligaSeasonSubcompetition.DfbPokal,
        observed,
        SchadensfresseRulesCanonicalJson.SchemaVersion,
        SchadensfresseRulesCanonicalJson.CanonicalSha256,
        new ResolvedTypedContextDocument("Context", "community-rules-schadensfresse.md", 7, new string('b', 64)));
}
