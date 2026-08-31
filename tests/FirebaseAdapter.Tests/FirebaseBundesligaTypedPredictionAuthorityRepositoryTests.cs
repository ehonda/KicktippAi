using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.PredictionsParallelKey)]
public sealed class FirebaseBundesligaTypedPredictionAuthorityRepositoryTests(FirestoreFixture fixture)
{
    private static readonly string[] TypedCollections =
    [
        FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
        FirebaseBundesligaTypedPredictionCollections.BonusPredictions,
        FirebaseBundesligaTypedPredictionCollections.ItemSnapshots
    ];

    [Before(Test)]
    public async Task ClearAsync()
    {
        await fixture.ClearPredictionsAsync();
        foreach (var collection in TypedCollections)
        {
            await ClearCollectionAsync(collection);
        }
    }

    [Test]
    public async Task Construction_and_collections_are_bound_to_the_exact_frozen_epoch()
    {
        await Assert.That(FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch)
            .IsEqualTo("bundesliga-2026-27-typed-v1");
        await Assert.That(FirebaseBundesligaTypedPredictionCollections.MatchPredictions)
            .IsEqualTo("match-predictions-bundesliga-2026-27-typed-v1");
        await Assert.That(FirebaseBundesligaTypedPredictionCollections.BonusPredictions)
            .IsEqualTo("bonus-predictions-bundesliga-2026-27-typed-v1");
        await Assert.That(FirebaseBundesligaTypedPredictionCollections.ItemSnapshots)
            .IsEqualTo("matches-bundesliga-2026-27-typed-v1");
        await Assert.That(() => new FirebaseBundesligaTypedPredictionAuthorityRepository(
                null!, FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new FirebaseBundesligaTypedPredictionAuthorityRepository(
                fixture.Db, "bundesliga-2026-27-typed-v2"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Match_and_bonus_current_families_round_trip_only_in_exact_typed_collections()
    {
        var repository = CreateRepository();
        var match = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent();
        var bonus = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent();
        var matchPrediction = new Prediction(2, 1);
        var bonusPrediction = new BonusPrediction(["a"]);

        await repository.SaveCurrentTypedMatchPredictionAsync(
            match,
            matchPrediction,
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(match));
        await repository.SaveCurrentTypedBonusPredictionAsync(
            bonus,
            bonusPrediction,
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(bonus));

        var storedMatch = await repository.GetCurrentTypedMatchPredictionAsync(match);
        var storedBonus = await repository.GetCurrentTypedBonusPredictionAsync(bonus);
        var matchMetadata = await repository.GetCurrentTypedMatchPredictionMetadataAsync(match);
        var bonusMetadata = await repository.GetCurrentTypedBonusPredictionMetadataAsync(bonus);
        await Assert.That(PredictionContentEquality.Equals(storedMatch!.Prediction, matchPrediction)).IsTrue();
        await Assert.That(storedBonus!.SelectedOptionIds).IsEquivalentTo(["a"]);
        await Assert.That(matchMetadata!.RepredictionIndex).IsEqualTo(0);
        await Assert.That(bonusMetadata!.RepredictionIndex).IsEqualTo(0);
        await Assert.That(await repository.HasCurrentTypedMatchPredictionAsync(match)).IsTrue();
        await Assert.That(await repository.HasCurrentTypedBonusPredictionAsync(bonus)).IsTrue();
        await Assert.That(await repository.GetCurrentTypedMatchRepredictionIndexAsync(match)).IsEqualTo(0);
        await Assert.That(await repository.GetCurrentTypedBonusRepredictionIndexAsync(bonus)).IsEqualTo(0);

        await Assert.That((await fixture.Db.Collection("match-predictions").GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await fixture.Db.Collection("matches").GetSnapshotAsync()).Count).IsEqualTo(0);
        await Assert.That((await fixture.Db.Collection(
            FirebaseBundesligaTypedPredictionCollections.MatchPredictions).GetSnapshotAsync()).Count).IsEqualTo(2);
        await Assert.That((await fixture.Db.Collection(
            FirebaseBundesligaTypedPredictionCollections.BonusPredictions).GetSnapshotAsync()).Count).IsEqualTo(2);
        await Assert.That((await fixture.Db.Collection(
            FirebaseBundesligaTypedPredictionCollections.ItemSnapshots).GetSnapshotAsync()).Count).IsEqualTo(1);

        foreach (var collection in TypedCollections)
        {
            var documents = await fixture.Db.Collection(collection).GetSnapshotAsync();
            foreach (var document in documents.Documents)
            {
                var data = document.ToDictionary();
                await Assert.That(data["epoch"]).IsEqualTo(FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch);
                await Assert.That(data["authorityEpoch"]).IsEqualTo(FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch);
                await Assert.That(data.ContainsKey("postingCommunity")).IsTrue();
                await Assert.That(data.ContainsKey("predictionSourceCommunity")).IsTrue();
                await Assert.That(data.ContainsKey("communityContext")).IsTrue();
                await Assert.That(data.ContainsKey("kicktippItemId")).IsTrue();
                await Assert.That(data.ContainsKey("snapshotSha256")).IsTrue();
                await Assert.That(data.ContainsKey("snapshotCanonicalBase64")).IsTrue();
            }
        }
    }

    [Test]
    public async Task Save_rejects_provenance_for_another_physical_namespace()
    {
        var repository = CreateRepository();
        var match = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent();
        var bonus = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent();

        await Assert.That(async () => await repository.SaveCurrentTypedMatchPredictionAsync(
                match,
                new Prediction(1, 0),
                FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
                    match,
                    physicalNamespace: FirebaseBundesligaTypedPredictionCollections.BonusPredictions)))
            .Throws<InvalidDataException>();
        await Assert.That(async () => await repository.SaveCurrentTypedBonusPredictionAsync(
                bonus,
                new BonusPrediction(["a"]),
                FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(
                    bonus,
                    physicalNamespace: FirebaseBundesligaTypedPredictionCollections.MatchPredictions)))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Generic_initial_and_reprediction_saves_reject_copy_authority_without_writing_match_or_bonus_rows()
    {
        var repository = CreateRepository();
        var matchInput = FirebaseBundesligaTypedPredictionContractTestData.MatchCopyInput();
        var bonusInput = FirebaseBundesligaTypedPredictionContractTestData.BonusCopyInput();
        var initialMatchCopy = FirebaseBundesligaTypedPredictionContractTestData.MatchCopyProvenance(
            matchInput, "source-match-r0");
        var initialBonusCopy = FirebaseBundesligaTypedPredictionContractTestData.BonusCopyProvenance(
            bonusInput, "source-bonus-r0");
        var targetMatchFingerprint = repository.CurrentFingerprint(matchInput.TargetCurrent);
        var targetBonusFingerprint = repository.CurrentFingerprint(bonusInput.TargetCurrent);

        await Assert.That(async () => await repository.SaveCurrentTypedMatchPredictionAsync(
                matchInput.TargetCurrent, new Prediction(1, 0), initialMatchCopy))
            .Throws<InvalidDataException>();
        await Assert.That(async () => await repository.SaveCurrentTypedBonusPredictionAsync(
                bonusInput.TargetCurrent, new BonusPrediction(["a"]), initialBonusCopy))
            .Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions)
            .Document($"{targetMatchFingerprint}-head").GetSnapshotAsync()).Exists).IsFalse();
        await Assert.That((await fixture.Db.Collection(
                FirebaseBundesligaTypedPredictionCollections.BonusPredictions)
            .Document($"{targetBonusFingerprint}-head").GetSnapshotAsync()).Exists).IsFalse();

        var sourceMatchProvenance = FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
            matchInput.SourceCurrent);
        await repository.SaveCurrentTypedMatchPredictionAsync(
            matchInput.SourceCurrent, new Prediction(1, 0), sourceMatchProvenance);
        var matchCopy = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            matchInput, PredictionCopyCompatibilityV2.Evaluate(matchInput));
        var matchCandidate = await repository.GetTypedMatchCopyCandidateAsync(matchCopy);
        await repository.SaveCurrentTypedMatchCopyAsync(TypedMatchCopySaveRequest.Create(
            matchCopy, matchCandidate!, new Prediction(1, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchCopyProvenance(
                matchInput, sourceMatchProvenance.PredictionIdentity)));

        var sourceBonusProvenance = FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(
            bonusInput.SourceCurrent);
        await repository.SaveCurrentTypedBonusPredictionAsync(
            bonusInput.SourceCurrent, new BonusPrediction(["a"]), sourceBonusProvenance);
        var bonusCopy = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(
            bonusInput, PredictionCopyCompatibilityV2.Evaluate(bonusInput));
        var bonusCandidate = await repository.GetTypedBonusCopyCandidateAsync(bonusCopy);
        await repository.SaveCurrentTypedBonusCopyAsync(TypedBonusCopySaveRequest.Create(
            bonusCopy, bonusCandidate!, new BonusPrediction(["a"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusCopyProvenance(
                bonusInput, sourceBonusProvenance.PredictionIdentity)));

        await Assert.That(async () => await repository.SaveCurrentTypedMatchRepredictionAsync(
                matchInput.TargetCurrent,
                new Prediction(2, 0),
                FirebaseBundesligaTypedPredictionContractTestData.MatchCopyProvenance(
                    matchInput, sourceMatchProvenance.PredictionIdentity, 1),
                0,
                2))
            .Throws<InvalidDataException>();
        await Assert.That(async () => await repository.SaveCurrentTypedBonusRepredictionAsync(
                bonusInput.TargetCurrent,
                new BonusPrediction(["b"]),
                FirebaseBundesligaTypedPredictionContractTestData.BonusCopyProvenance(
                    bonusInput, sourceBonusProvenance.PredictionIdentity, 1),
                0,
                2))
            .Throws<InvalidDataException>();
        await Assert.That((await fixture.Db.Collection(
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions)
            .Document($"{targetMatchFingerprint}-r1").GetSnapshotAsync()).Exists).IsFalse();
        await Assert.That((await fixture.Db.Collection(
                FirebaseBundesligaTypedPredictionCollections.BonusPredictions)
            .Document($"{targetBonusFingerprint}-r1").GetSnapshotAsync()).Exists).IsFalse();
        await Assert.That(await repository.GetCurrentTypedMatchRepredictionIndexAsync(
            matchInput.TargetCurrent)).IsEqualTo(0);
        await Assert.That(await repository.GetCurrentTypedBonusRepredictionIndexAsync(
            bonusInput.TargetCurrent)).IsEqualTo(0);
    }

    [Test]
    [Arguments("head-epoch")]
    [Arguments("head-authority")]
    [Arguments("row-route")]
    [Arguments("snapshot-hash")]
    public async Task Current_read_rejects_document_epoch_or_complete_identity_drift(string mutation)
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent();
        await repository.SaveCurrentTypedMatchPredictionAsync(
            current,
            new Prediction(1, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current));
        var fingerprint = repository.CurrentFingerprint(current);
        var (collection, document, field, value) = mutation switch
        {
            "head-epoch" => (
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
                $"{fingerprint}-head", "epoch", "wrong-epoch"),
            "head-authority" => (
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
                $"{fingerprint}-head", "postingCommunity", "other-community"),
            "row-route" => (
                FirebaseBundesligaTypedPredictionCollections.MatchPredictions,
                $"{fingerprint}-r0", "routeId", "wrong-route"),
            _ => (
                FirebaseBundesligaTypedPredictionCollections.ItemSnapshots,
                $"{fingerprint}-snapshot", "snapshotSha256",
                FirebaseBundesligaTypedPredictionContractTestData.ShaB)
        };
        await fixture.Db.Collection(collection).Document(document).UpdateAsync(field, value);

        await Assert.That(async () => await repository.GetCurrentTypedMatchPredictionAsync(current))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Initial_save_is_duplicate_safe_and_exact_authorities_never_fallback()
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent("pes-squad");
        var provenance = FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current);
        await repository.SaveCurrentTypedMatchPredictionAsync(current, new Prediction(1, 1), provenance);

        await Assert.That(async () => await repository.SaveCurrentTypedMatchPredictionAsync(
                current, new Prediction(2, 2), provenance))
            .Throws<InvalidOperationException>();
        var otherAuthority = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent("other-community");
        await Assert.That(await repository.GetCurrentTypedMatchPredictionAsync(otherAuthority)).IsNull();
        await Assert.That(await repository.GetCurrentTypedMatchRepredictionIndexAsync(otherAuthority)).IsEqualTo(-1);

        await fixture.Db.Collection("match-predictions").Document("legacy-row").SetAsync(new Dictionary<string, object>
        {
            ["competition"] = CompetitionIds.Bundesliga2026_27,
            ["homeTeam"] = "FC Example",
            ["awayTeam"] = "SV Sample",
            ["cost"] = 1.0,
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["repredictionIndex"] = 0
        });
        await Assert.That(await repository.GetCurrentTypedMatchPredictionAsync(otherAuthority)).IsNull();
    }

    [Test]
    public async Task Match_reprediction_is_transactional_concurrent_and_max_bounded()
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent();
        await repository.SaveCurrentTypedMatchPredictionAsync(
            current,
            new Prediction(1, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current));

        async Task<bool> TrySaveAsync(int homeGoals)
        {
            try
            {
                await repository.SaveCurrentTypedMatchRepredictionAsync(
                    current,
                    new Prediction(homeGoals, 1),
                    FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
                        current, 1, $"match-r1-{homeGoals}"),
                    0,
                    2);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TrySaveAsync(2), TrySaveAsync(3));
        await Assert.That(results.Count(success => success)).IsEqualTo(1);
        await Assert.That(await repository.GetCurrentTypedMatchRepredictionIndexAsync(current)).IsEqualTo(1);
        await Assert.That(async () => await repository.SaveCurrentTypedMatchRepredictionAsync(
                current,
                new Prediction(4, 1),
                FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
                    current, 1, "stale-match-r1"),
                0,
                3))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await repository.SaveCurrentTypedMatchRepredictionAsync(
                current,
                new Prediction(4, 1),
                FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current, 2),
                1,
                1))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Bonus_reprediction_enforces_expected_current_and_maximum()
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent();
        await repository.SaveCurrentTypedBonusPredictionAsync(
            current,
            new BonusPrediction(["a"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current));
        await repository.SaveCurrentTypedBonusRepredictionAsync(
            current,
            new BonusPrediction(["b"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current, 1),
            0,
            2);
        await Assert.That((await repository.GetCurrentTypedBonusPredictionAsync(current))!
            .SelectedOptionIds).IsEquivalentTo(["b"]);
        await Assert.That(async () => await repository.SaveCurrentTypedBonusRepredictionAsync(
                current,
                new BonusPrediction(["a"]),
                FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(
                    current, 1, "stale-bonus-r1"),
                0,
                2))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await repository.SaveCurrentTypedBonusRepredictionAsync(
                current,
                new BonusPrediction(["a"]),
                FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current, 2),
                1,
                1))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Reprediction_validates_the_complete_existing_match_and_bonus_rows_before_any_advance()
    {
        var repository = CreateRepository();

        async Task AssertMatchRejectedAsync(string community, string mutation)
        {
            var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent(community);
            await repository.SaveCurrentTypedMatchPredictionAsync(
                current, new Prediction(1, 0),
                FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current));
            var fingerprint = repository.CurrentFingerprint(current);
            var collection = fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.MatchPredictions);
            if (mutation == "missing-row")
            {
                await collection.Document($"{fingerprint}-r0").DeleteAsync();
            }
            else if (mutation == "malformed-row")
            {
                await collection.Document($"{fingerprint}-r0").UpdateAsync("predictionJson", "{");
            }
            else
            {
                await collection.Document($"{fingerprint}-head")
                    .UpdateAsync("currentPredictionIdentity", "another-valid-identity");
            }
            await Assert.That(async () => await repository.SaveCurrentTypedMatchRepredictionAsync(
                    current, new Prediction(2, 0),
                    FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current, 1),
                    0, 2))
                .Throws<InvalidDataException>();
            await Assert.That((await collection.Document($"{fingerprint}-r1").GetSnapshotAsync()).Exists)
                .IsFalse();
        }

        async Task AssertBonusRejectedAsync(string community, string mutation)
        {
            var current = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent(community);
            await repository.SaveCurrentTypedBonusPredictionAsync(
                current, new BonusPrediction(["a"]),
                FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current));
            var fingerprint = repository.CurrentFingerprint(current);
            var collection = fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.BonusPredictions);
            if (mutation == "missing-row")
            {
                await collection.Document($"{fingerprint}-r0").DeleteAsync();
            }
            else if (mutation == "malformed-row")
            {
                await collection.Document($"{fingerprint}-r0")
                    .UpdateAsync("selectedOptionIds", new object[] { 7L });
            }
            else
            {
                await collection.Document($"{fingerprint}-head")
                    .UpdateAsync("currentPredictionIdentity", "another-valid-identity");
            }
            await Assert.That(async () => await repository.SaveCurrentTypedBonusRepredictionAsync(
                    current, new BonusPrediction(["b"]),
                    FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current, 1),
                    0, 2))
                .Throws<InvalidDataException>();
            await Assert.That((await collection.Document($"{fingerprint}-r1").GetSnapshotAsync()).Exists)
                .IsFalse();
        }

        foreach (var mutation in new[] { "missing-row", "malformed-row", "head-identity-drift" })
        {
            await AssertMatchRejectedAsync($"match-{mutation}", mutation);
            await AssertBonusRejectedAsync($"bonus-{mutation}", mutation);
        }
    }

    [Test]
    public async Task Initial_and_reprediction_persist_factory_defensive_match_and_bonus_payloads()
    {
        var repository = CreateRepository();
        var match = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent("payload-match");
        var most = new List<PredictionJustificationContextSource>
        {
            new("rules.md", "initial evidence")
        };
        var least = new List<PredictionJustificationContextSource>
        {
            new("history.csv", "initial uncertainty")
        };
        var uncertainties = new List<string> { "initial uncertainty" };
        var initialMatch = new Prediction(1, 0, new PredictionJustification(
            "initial reasoning",
            new PredictionJustificationContextSources(most, least),
            uncertainties));
        var initialMatchSave = repository.SaveCurrentTypedMatchPredictionAsync(
            match, initialMatch,
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(match));
        most[0] = new PredictionJustificationContextSource("tampered.md", "tampered");
        least.Clear();
        uncertainties[0] = "tampered";
        await initialMatchSave;
        var storedInitialMatch = (await repository.GetCurrentTypedMatchPredictionAsync(match))!.Prediction;
        await Assert.That(storedInitialMatch.Justification!.ContextSources.MostValuable[0].DocumentName)
            .IsEqualTo("rules.md");
        await Assert.That(storedInitialMatch.Justification.ContextSources.LeastValuable).HasCount().EqualTo(1);
        await Assert.That(storedInitialMatch.Justification.Uncertainties[0]).IsEqualTo("initial uncertainty");

        var laterUncertainties = new List<string> { "later uncertainty" };
        var laterMatch = new Prediction(2, 0, new PredictionJustification(
            "later reasoning",
            new PredictionJustificationContextSources(
                new List<PredictionJustificationContextSource> { new("elo.csv", "later evidence") },
                new List<PredictionJustificationContextSource>()),
            laterUncertainties));
        var laterMatchSave = repository.SaveCurrentTypedMatchRepredictionAsync(
            match, laterMatch,
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(match, 1),
            0, 2);
        laterUncertainties[0] = "tampered later";
        await laterMatchSave;
        await Assert.That((await repository.GetCurrentTypedMatchPredictionAsync(match))!
            .Prediction.Justification!.Uncertainties[0]).IsEqualTo("later uncertainty");

        var bonus = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent("payload-bonus");
        var initialSelections = new List<string> { "a" };
        var initialBonusSave = repository.SaveCurrentTypedBonusPredictionAsync(
            bonus, new BonusPrediction(initialSelections),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(bonus));
        initialSelections[0] = "b";
        await initialBonusSave;
        await Assert.That((await repository.GetCurrentTypedBonusPredictionAsync(bonus))!.SelectedOptionIds)
            .IsEquivalentTo(["a"]);

        var laterSelections = new List<string> { "b" };
        var laterBonusSave = repository.SaveCurrentTypedBonusRepredictionAsync(
            bonus, new BonusPrediction(laterSelections),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(bonus, 1),
            0, 2);
        laterSelections[0] = "a";
        await laterBonusSave;
        await Assert.That((await repository.GetCurrentTypedBonusPredictionAsync(bonus))!.SelectedOptionIds)
            .IsEquivalentTo(["b"]);
    }

    [Test]
    public async Task Match_and_bonus_copy_require_the_exact_typed_source_and_create_new_target_rows()
    {
        var repository = CreateRepository();
        var matchInput = FirebaseBundesligaTypedPredictionContractTestData.MatchCopyInput();
        var sourceMatchProvenance = FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
            matchInput.SourceCurrent);
        var sourceMatch = new Prediction(2, 1);
        await repository.SaveCurrentTypedMatchPredictionAsync(
            matchInput.SourceCurrent, sourceMatch, sourceMatchProvenance);
        var matchCopy = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            matchInput, PredictionCopyCompatibilityV2.Evaluate(matchInput));
        var matchCandidate = await repository.GetTypedMatchCopyCandidateAsync(matchCopy);
        var targetMatchProvenance = FirebaseBundesligaTypedPredictionContractTestData.MatchCopyProvenance(
            matchInput, sourceMatchProvenance.PredictionIdentity);
        await repository.SaveCurrentTypedMatchCopyAsync(TypedMatchCopySaveRequest.Create(
            matchCopy, matchCandidate!, sourceMatch, targetMatchProvenance));
        await Assert.That(PredictionContentEquality.Equals(
            (await repository.GetCurrentTypedMatchPredictionAsync(matchInput.TargetCurrent))!.Prediction,
            sourceMatch)).IsTrue();

        var bonusInput = FirebaseBundesligaTypedPredictionContractTestData.BonusCopyInput();
        var sourceBonusProvenance = FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(
            bonusInput.SourceCurrent);
        await repository.SaveCurrentTypedBonusPredictionAsync(
            bonusInput.SourceCurrent, new BonusPrediction(["a"]), sourceBonusProvenance);
        var bonusCopy = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(
            bonusInput, PredictionCopyCompatibilityV2.Evaluate(bonusInput));
        var bonusCandidate = await repository.GetTypedBonusCopyCandidateAsync(bonusCopy);
        var targetBonusProvenance = FirebaseBundesligaTypedPredictionContractTestData.BonusCopyProvenance(
            bonusInput, sourceBonusProvenance.PredictionIdentity);
        await repository.SaveCurrentTypedBonusCopyAsync(TypedBonusCopySaveRequest.Create(
            bonusCopy, bonusCandidate!, new BonusPrediction(["a"]), targetBonusProvenance));
        await Assert.That((await repository.GetCurrentTypedBonusPredictionAsync(bonusInput.TargetCurrent))!
            .SelectedOptionIds).IsEquivalentTo(["a"]);

        await Assert.That(async () => await repository.SaveCurrentTypedMatchCopyAsync(
                TypedMatchCopySaveRequest.Create(
                    matchCopy, matchCandidate!, sourceMatch, targetMatchProvenance)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Copy_save_rejects_a_source_that_changed_after_candidate_selection()
    {
        var repository = CreateRepository();
        var matchInput = FirebaseBundesligaTypedPredictionContractTestData.MatchCopyInput();
        var sourceMatchProvenance = FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(
            matchInput.SourceCurrent);
        await repository.SaveCurrentTypedMatchPredictionAsync(
            matchInput.SourceCurrent, new Prediction(1, 0), sourceMatchProvenance);
        var matchCopy = BundesligaTypedCopyRequest<TypedMatchSnapshot>.Create(
            matchInput, PredictionCopyCompatibilityV2.Evaluate(matchInput));
        var matchCandidate = await repository.GetTypedMatchCopyCandidateAsync(matchCopy);
        await repository.SaveCurrentTypedMatchRepredictionAsync(
            matchInput.SourceCurrent,
            new Prediction(2, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(matchInput.SourceCurrent, 1),
            0,
            2);

        await Assert.That(async () => await repository.SaveCurrentTypedMatchCopyAsync(
                TypedMatchCopySaveRequest.Create(
                    matchCopy,
                    matchCandidate!,
                    new Prediction(1, 0),
                    FirebaseBundesligaTypedPredictionContractTestData.MatchCopyProvenance(
                        matchInput,
                        sourceMatchProvenance.PredictionIdentity))))
            .Throws<InvalidDataException>();

        var bonusInput = FirebaseBundesligaTypedPredictionContractTestData.BonusCopyInput();
        var sourceBonusProvenance = FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(
            bonusInput.SourceCurrent);
        await repository.SaveCurrentTypedBonusPredictionAsync(
            bonusInput.SourceCurrent, new BonusPrediction(["a"]), sourceBonusProvenance);
        var bonusCopy = BundesligaTypedCopyRequest<TypedBonusSnapshot>.Create(
            bonusInput, PredictionCopyCompatibilityV2.Evaluate(bonusInput));
        var bonusCandidate = await repository.GetTypedBonusCopyCandidateAsync(bonusCopy);
        await repository.SaveCurrentTypedBonusRepredictionAsync(
            bonusInput.SourceCurrent,
            new BonusPrediction(["b"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(bonusInput.SourceCurrent, 1),
            0,
            2);

        await Assert.That(async () => await repository.SaveCurrentTypedBonusCopyAsync(
                TypedBonusCopySaveRequest.Create(
                    bonusCopy,
                    bonusCandidate!,
                    new BonusPrediction(["a"]),
                    FirebaseBundesligaTypedPredictionContractTestData.BonusCopyProvenance(
                        bonusInput,
                        sourceBonusProvenance.PredictionIdentity))))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Audit_cost_reader_capabilities_are_independent_labelled_non_current_and_one_collection_each()
    {
        await fixture.Db.Collection("match-predictions").Document("legacy-match").SetAsync(
            new Dictionary<string, object>
            {
                ["competition"] = CompetitionIds.Bundesliga2026_27,
                ["createdAt"] = Timestamp.FromDateTimeOffset(
                    new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero)),
                ["repredictionIndex"] = 0,
                ["cost"] = 0.25
            });
        await fixture.Db.Collection("match-predictions").Document("wm26-match").SetAsync(
            new Dictionary<string, object>
            {
                ["competition"] = "wm26",
                ["createdAt"] = Timestamp.GetCurrentTimestamp(),
                ["repredictionIndex"] = 0,
                ["cost"] = 99.0
            });
        await fixture.Db.Collection("bonus-predictions").Document("legacy-bonus").SetAsync(
            new Dictionary<string, object>
            {
                ["competition"] = CompetitionIds.Bundesliga2026_27,
                ["createdAt"] = Timestamp.FromDateTimeOffset(
                    new DateTimeOffset(2026, 8, 31, 11, 0, 0, TimeSpan.Zero)),
                ["repredictionIndex"] = 0,
                ["cost"] = 0.10
            });
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent();
        var bonus = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent();
        await repository.SaveCurrentTypedMatchPredictionAsync(
            current,
            new Prediction(1, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current));
        await repository.SaveCurrentTypedBonusPredictionAsync(
            bonus,
            new BonusPrediction(["a"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(bonus));

        IFirebasePredictionAuditCostReader[] readers =
        [
            new FirebaseLegacyMatchPredictionAuditCostReader(fixture.Db),
            new FirebaseLegacyBonusPredictionAuditCostReader(fixture.Db),
            new FirebaseTypedMatchPredictionAuditCostReader(fixture.Db),
            new FirebaseTypedBonusPredictionAuditCostReader(fixture.Db)
        ];
        var results = new List<FirebasePredictionAuditCostRow>();
        foreach (var reader in readers)
        {
            var rows = await reader.ReadAsync();
            await Assert.That(rows).HasCount().EqualTo(1);
            await Assert.That(rows[0].PhysicalCollection).IsEqualTo(reader.PhysicalCollection);
            await Assert.That(rows[0].ItemKind).IsEqualTo(reader.ItemKind);
            await Assert.That(rows[0].AuthorityLabel).IsEqualTo(reader.AuthorityLabel);
            results.Add(rows[0]);
        }

        await Assert.That(results.Single(row => row.PhysicalCollection == "match-predictions").CostUsd)
            .IsEqualTo(0.25m);
        await Assert.That(results.Single(row => row.PhysicalCollection ==
            FirebaseBundesligaTypedPredictionCollections.MatchPredictions).InputTokens).IsEqualTo(100);
        await Assert.That(results.All(row => !row.IsCurrentAuthoritative)).IsTrue();
        await Assert.That(readers.All(reader => reader is not IBundesligaTypedPredictionAuthorityRepository))
            .IsTrue();
        await Assert.That(readers.Select(reader => reader.PhysicalCollection).Distinct(StringComparer.Ordinal))
            .HasCount().EqualTo(4);
    }

    [Test]
    public async Task One_collection_audit_capability_never_queries_its_sibling_collection()
    {
        await fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.BonusPredictions)
            .Document("foreign")
            .SetAsync(new Dictionary<string, object>
            {
                ["epoch"] = "bundesliga-2026-27-typed-v2",
                ["authorityEpoch"] = "bundesliga-2026-27-typed-v2",
                ["documentKind"] = "prediction"
            });
        var matchReader = new FirebaseTypedMatchPredictionAuditCostReader(fixture.Db);
        var bonusReader = new FirebaseTypedBonusPredictionAuditCostReader(fixture.Db);

        await Assert.That(await matchReader.ReadAsync()).HasCount().EqualTo(0);
        await Assert.That(async () => await bonusReader.ReadAsync()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_match_audit_validates_every_repeated_identity_field_and_exact_row_address()
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.MatchCurrent("audit-match");
        await repository.SaveCurrentTypedMatchPredictionAsync(
            current, new Prediction(1, 0),
            FirebaseBundesligaTypedPredictionContractTestData.MatchProvenance(current));
        var fingerprint = repository.CurrentFingerprint(current);
        var collection = fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.MatchPredictions);
        var rowReference = collection.Document($"{fingerprint}-r0");
        var original = (await rowReference.GetSnapshotAsync()).ToDictionary();
        var reader = new FirebaseTypedMatchPredictionAuditCostReader(fixture.Db);
        var repeatedIdentityFields = new[]
        {
            "authorityEpoch", "authorityMode", "seasonPartition", "postingCommunity",
            "predictionSourceCommunity", "communityContext", "postingSeedGeneration",
            "postingSeedSha256", "sourceSeedGeneration", "sourceSeedSha256",
            "copyBindingGeneration", "copyBindingSha256", "itemKind", "keySeasonPartition",
            "keyPostingCommunity", "keyItemKind", "kicktippItemId", "snapshotSchemaVersion",
            "snapshotSha256", "snapshotCanonicalBase64", "routeId", "profileId",
            "generationInputContractId", "generationInputContractSha256", "model",
            "reasoningEffort", "maxOutputTokenCount", "promptName", "promptVersion",
            "currentFingerprint"
        };
        foreach (var field in repeatedIdentityFields)
        {
            await rowReference.UpdateAsync(field, "independently-tampered");
            await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
            await rowReference.UpdateAsync(field, original[field]);
        }

        await rowReference.UpdateAsync("predictionIdentity", "another-prediction");
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
        await rowReference.UpdateAsync("predictionIdentity", original["predictionIdentity"]);
        await rowReference.UpdateAsync("repredictionIndex", 7L);
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
        await rowReference.UpdateAsync("repredictionIndex", original["repredictionIndex"]);
        await rowReference.UpdateAsync("createdAt", Timestamp.FromDateTimeOffset(
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
        await rowReference.UpdateAsync("createdAt", original["createdAt"]);

        await collection.Document($"{fingerprint}-r9").SetAsync(original);
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_bonus_audit_rejects_independent_snapshot_mutation_and_duplicate_wrong_address()
    {
        var repository = CreateRepository();
        var current = FirebaseBundesligaTypedPredictionContractTestData.BonusCurrent("audit-bonus");
        await repository.SaveCurrentTypedBonusPredictionAsync(
            current, new BonusPrediction(["a"]),
            FirebaseBundesligaTypedPredictionContractTestData.BonusProvenance(current));
        var fingerprint = repository.CurrentFingerprint(current);
        var collection = fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.BonusPredictions);
        var rowReference = collection.Document($"{fingerprint}-r0");
        var original = (await rowReference.GetSnapshotAsync()).ToDictionary();
        var reader = new FirebaseTypedBonusPredictionAuditCostReader(fixture.Db);

        await rowReference.UpdateAsync(
            "snapshotCanonicalBase64",
            Convert.ToBase64String(FirebaseBundesligaTypedPredictionContractTestData.Match("audit-bonus").SerializeCanonical()));
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
        await rowReference.UpdateAsync("snapshotCanonicalBase64", original["snapshotCanonicalBase64"]);
        await collection.Document($"{fingerprint}-r1").SetAsync(original);
        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_audit_reader_rejects_a_cross_epoch_document_in_its_physical_namespace()
    {
        await fixture.Db.Collection(FirebaseBundesligaTypedPredictionCollections.MatchPredictions)
            .Document("foreign")
            .SetAsync(new Dictionary<string, object>
            {
                ["epoch"] = "bundesliga-2026-27-typed-v2",
                ["authorityEpoch"] = "bundesliga-2026-27-typed-v2",
                ["documentKind"] = "prediction"
            });
        var reader = new FirebaseTypedMatchPredictionAuditCostReader(fixture.Db);

        await Assert.That(async () => await reader.ReadAsync()).Throws<InvalidDataException>();
    }

    private FirebaseBundesligaTypedPredictionAuthorityRepository CreateRepository() =>
        new(fixture.Db, FirebaseBundesligaTypedPredictionCollections.AuthorityEpoch);

    private async Task ClearCollectionAsync(string collection)
    {
        var snapshot = await fixture.Db.Collection(collection).GetSnapshotAsync();
        if (snapshot.Count == 0)
        {
            return;
        }
        var batch = fixture.Db.StartBatch();
        foreach (var document in snapshot.Documents)
        {
            batch.Delete(document.Reference);
        }
        await batch.CommitAsync();
    }
}
