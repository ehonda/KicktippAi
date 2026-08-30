using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using NodaTime;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public sealed class FirebasePredictionRepository_BundesligaSeasonIdentity_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Typed_Bundesliga_match_serializes_exact_identity_while_Dfb_and_Champions_League_fail_closed()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("fixture-bundesliga", BundesligaSeasonSubcompetition.Bundesliga, ResultBasis.RegularTime90Minutes, 1);
        var manifest = MatchManifest(match, "typed-community");
        await repository.SavePredictionWithResolvedContextAsync(match, new Prediction(1, 0), config, "{}", 0.01,
            "typed-community", manifest.Documents.Select(document => document.Name), manifest);
        await Assert.That(await repository.GetPredictionMetadataAsync(match, config, "typed-community")).IsNotNull();

        foreach (var unsupported in new[]
                 {
                     Match("fixture-dfb", BundesligaSeasonSubcompetition.DfbPokal, ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 2),
                     Match("fixture-cl", BundesligaSeasonSubcompetition.ChampionsLeague, ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 3)
                 })
        {
            var unsupportedManifest = MatchManifest(unsupported, "typed-community");
            await Assert.That(() => repository.SavePredictionWithResolvedContextAsync(unsupported, new Prediction(1, 0), config, "{}", 0.01,
                    "typed-community", unsupportedManifest.Documents.Select(document => document.Name), unsupportedManifest))
                .Throws<InvalidOperationException>();
            await Assert.That(() => repository.GetPredictionMetadataAsync(unsupported, config, "typed-community"))
                .Throws<InvalidOperationException>();
            await Assert.That(() => repository.HasPredictionAsync(unsupported, config, "typed-community"))
                .Throws<InvalidOperationException>();
            await Assert.That(() => repository.GetMatchRepredictionIndexAsync(unsupported, config, "typed-community"))
                .Throws<InvalidOperationException>();
        }

        var rows = (await Fixture.Db.Collection("match-predictions").GetSnapshotAsync()).Documents
            .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
            .OrderBy(row => row.KicktippFixtureId, StringComparer.Ordinal)
            .ToArray();
        await Assert.That(rows.Select(row => (row.KicktippFixtureId, row.KicktippRoundName, row.ResultBasis, row.BundesligaSeasonSubcompetition)))
            .IsEquivalentTo([
                ("fixture-bundesliga", "Bundesliga exact round", "regularTime90Minutes", "bundesliga")]);
    }

    [Test]
    public async Task Untyped_legacy_Bundesliga_rows_remain_auditable_but_cannot_be_current_or_reused()
    {
        var repository = CreateBundesligaRepository();
        var match = Match("fixture-current", BundesligaSeasonSubcompetition.Bundesliga, ResultBasis.RegularTime90Minutes, 1);
        var config = PredictionModelConfig.Create("gpt-5");
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("match-predictions").Document("legacy-untyped").SetAsync(new FirestoreMatchPrediction
        {
            Id = "legacy-untyped", HomeTeam = match.HomeTeam, AwayTeam = match.AwayTeam,
            StartsAt = Timestamp.FromDateTimeOffset(match.StartsAt.ToInstant().ToDateTimeOffset()), Matchday = match.Matchday,
            HomeGoals = 2, AwayGoals = 1, CreatedAt = now, UpdatedAt = now,
            Competition = CompetitionIds.Bundesliga2026_27, Model = config.Model,
            ModelConfigKey = config.IdentityKey, ReasoningEffort = config.ReasoningEffort,
            TokenUsage = "{}", CommunityContext = "typed-community"
        });

        await Assert.That(await repository.GetPredictionMetadataAsync(match, config, "typed-community")).IsNull();
        await Assert.That(await repository.HasPredictionAsync(match, config, "typed-community")).IsFalse();
        await Assert.That(await repository.GetMatchRepredictionIndexAsync(match, config, "typed-community")).IsEqualTo(-1);
        await Assert.That(await repository.GetAllPredictionsAsync(config, "typed-community")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Typed_bonus_rows_bind_question_id_subcompetition_and_full_question_identity()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var question = new BonusQuestion("Exact Champions League question", Instant.FromUtc(2026, 9, 8, 16, 45).InUtc(),
            [new BonusQuestionOption("option-1", "One"), new BonusQuestionOption("option-2", "Two")], 1)
        {
            KicktippQuestionId = "question-bundesliga",
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga
        };
        var manifest = BonusManifest("typed-community");
        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question, new BonusPrediction(["option-1"]), config, "{}", 0.01, "typed-community",
            manifest.Documents.Select(document => document.Name), manifest);

        await Assert.That(await repository.GetCurrentBonusPredictionAsync(question, config, "typed-community")).IsNotNull();
        foreach (var drifted in new[]
                 {
                     question with { Text = "Mutated" },
                     question with { Deadline = question.Deadline.PlusHours(1) },
                     question with { MaxSelections = 2 },
                     question with { Options = [question.Options[1], question.Options[0]] },
                     question with { Options = [question.Options[0] with { Id = "mutated-id" }, question.Options[1]] },
                     question with { Options = [question.Options[0] with { Text = "Mutated option" }, question.Options[1]] }
                 })
        {
            await Assert.That(await repository.GetCurrentBonusPredictionAsync(
                drifted, config, "typed-community")).IsNull();
        }

        await Assert.That(await repository.GetBonusPredictionAsync(
            question.KicktippQuestionId!, config, "typed-community")).IsNull();
        await Assert.That(await repository.GetBonusPredictionByTextAsync(
            question.Text, config, "typed-community")).IsNull();
        await Assert.That(await repository.GetBonusPredictionMetadataByTextAsync(
            question.Text, config, "typed-community")).IsNull();
        await Assert.That(await repository.HasBonusPredictionAsync(
            question.KicktippQuestionId!, config, "typed-community")).IsFalse();
        await Assert.That(await repository.GetBonusRepredictionIndexAsync(
            question.Text, config, "typed-community")).IsEqualTo(-1);
        var stored = (await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Documents.Single();
        await Assert.That(stored.GetValue<string>("kicktippQuestionId")).IsEqualTo("question-bundesliga");
        await Assert.That(stored.GetValue<string>("bundesligaSeasonSubcompetition")).IsEqualTo("bundesliga");
        await Assert.That(stored.GetValue<string>("bundesligaSeasonBonusIdentitySha256"))
            .IsEqualTo(BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(question));

        var unsupported = question with { KicktippQuestionId = "question-cl", BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague };
        await Assert.That(() => repository.SaveBonusPredictionWithResolvedContextAsync(unsupported, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "typed-community", manifest.Documents.Select(document => document.Name), manifest))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Typed_Dfb_match_and_champions_bonus_rows_round_trip_their_row_bound_canonical_generation_manifests()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("typed-dfb-roundtrip", BundesligaSeasonSubcompetition.DfbPokal,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 5);
        var championsLeagueMatch = Match("typed-cl-roundtrip", BundesligaSeasonSubcompetition.ChampionsLeague,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 10);
        var question = Question("typed-cl-roundtrip") with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };
        var matchManifest = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false);
        var championsLeagueMatchManifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: false);
        var bonusManifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true);

        await typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", matchManifest.Documents.Select(x => x.Name), matchManifest);
        await typed.SavePredictionWithResolvedTypedContextAsync(championsLeagueMatch, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", championsLeagueMatchManifest.Documents.Select(x => x.Name), championsLeagueMatchManifest);
        await typed.SaveBonusPredictionWithResolvedTypedContextAsync(question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
            "schadensfresse", bonusManifest.Documents.Select(x => x.Name), bonusManifest);

        var matchMetadata = await typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse");
        var championsLeagueMatchMetadata = await typed.GetCurrentTypedPredictionMetadataAsync(championsLeagueMatch, config, "schadensfresse");
        var bonusMetadata = await typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse");
        await Assert.That(matchMetadata!.ResolvedTypedContextManifest).IsEqualTo(matchManifest);
        await Assert.That(matchMetadata.ResolvedContextManifest).IsNull();
        await Assert.That(championsLeagueMatchMetadata!.ResolvedTypedContextManifest).IsEqualTo(championsLeagueMatchManifest);
        await Assert.That(bonusMetadata!.ResolvedTypedContextManifest).IsEqualTo(bonusManifest);
        await Assert.That(bonusMetadata.ResolvedContextManifest).IsNull();

        var matchRow = (await Fixture.Db.Collection("match-predictions").WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId).GetSnapshotAsync()).Documents.Single();
        var championsLeagueMatchRow = (await Fixture.Db.Collection("match-predictions").WhereEqualTo("kicktippFixtureId", championsLeagueMatch.KicktippFixtureId).GetSnapshotAsync()).Documents.Single();
        var bonusRow = (await Fixture.Db.Collection("bonus-predictions").WhereEqualTo("kicktippQuestionId", question.KicktippQuestionId).GetSnapshotAsync()).Documents.Single();
        await Assert.That(matchRow.GetValue<string>("resolvedTypedContextManifest"))
            .IsEqualTo(System.Text.Encoding.UTF8.GetString(matchManifest.SerializeCanonical()));
        await Assert.That(championsLeagueMatchRow.GetValue<string>("resolvedTypedContextManifest"))
            .IsEqualTo(System.Text.Encoding.UTF8.GetString(championsLeagueMatchManifest.SerializeCanonical()));
        await Assert.That(bonusRow.GetValue<string>("resolvedTypedContextManifest"))
            .IsEqualTo(System.Text.Encoding.UTF8.GetString(bonusManifest.SerializeCanonical()));
        await Assert.That(matchRow.ConvertTo<FirestoreMatchPrediction>().ResolvedContextManifest).IsNull();
        await Assert.That(bonusRow.ConvertTo<FirestoreBonusPrediction>().ResolvedBonusContextManifest).IsNull();

        var changedBonusManifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true,
            observedAt: DateTimeOffset.UtcNow.AddMinutes(-2), documentVersion: bonusManifest.Documents[0].Version + 1);
        await Assert.That(() => typed.SaveBonusPredictionWithResolvedTypedContextAsync(question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "schadensfresse", changedBonusManifest.Documents.Select(x => x.Name), changedBonusManifest)).Throws<InvalidDataException>();
        var staleBonusManifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true,
            observedAt: DateTimeOffset.UtcNow.AddHours(-25));
        await bonusRow.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(staleBonusManifest.SerializeCanonical()));
        await Assert.That((await typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse"))!.ResolvedTypedContextManifest)
            .IsEqualTo(staleBonusManifest);
    }

    [Test]
    public async Task Typed_generation_manifests_are_immutable_and_stale_historical_bytes_remain_row_bound_audit_data()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("typed-dfb-immutable", BundesligaSeasonSubcompetition.DfbPokal,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 6);
        var manifest = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false);
        await typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", manifest.Documents.Select(x => x.Name), manifest);

        var changed = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false,
            observedAt: DateTimeOffset.UtcNow.AddMinutes(-2), documentVersion: manifest.Documents[0].Version + 1);
        await Assert.That(() => typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
                "schadensfresse", changed.Documents.Select(x => x.Name), changed)).Throws<InvalidDataException>();

        var stale = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false,
            observedAt: DateTimeOffset.UtcNow.AddHours(-25));
        var row = (await Fixture.Db.Collection("match-predictions").WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId).GetSnapshotAsync()).Documents.Single();
        await row.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(stale.SerializeCanonical()));
        var auditMetadata = await typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse");
        await Assert.That(auditMetadata!.ResolvedTypedContextManifest).IsEqualTo(stale);
    }

    [Test]
    public async Task Typed_current_rows_fail_closed_for_missing_noncanonical_wrong_profile_and_legacy_hash_manifests()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("typed-cl-corrupt", BundesligaSeasonSubcompetition.ChampionsLeague,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 7);
        var manifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: false);
        await typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", manifest.Documents.Select(x => x.Name), manifest);
        var row = (await Fixture.Db.Collection("match-predictions").WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId).GetSnapshotAsync()).Documents.Single();

        foreach (var corrupt in new[]
                 {
                     "{}",
                     "{\"normalizedRulesSha256\":\"" + new string('a', 64) + "\"}",
                     "{\"canonicalRulesSha256\":123}",
                     System.Text.Encoding.UTF8.GetString(TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true).SerializeCanonical())
                 })
        {
            await row.Reference.UpdateAsync("resolvedTypedContextManifest", corrupt);
            await Assert.That(() => typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse"))
                .Throws<InvalidDataException>();
        }

        await row.Reference.UpdateAsync("resolvedTypedContextManifest", FieldValue.Delete);
        await Assert.That(() => typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_save_materializes_match_context_names_once_and_rejects_wrong_row_profile()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("typed-dfb-stateful", BundesligaSeasonSubcompetition.DfbPokal,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 8);
        var manifest = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false);
        var enumerations = 0;
        IEnumerable<string> Names()
        {
            enumerations++;
            yield return enumerations == 1 ? manifest.Documents[0].Name : "changed-on-second-enumeration";
        }

        await typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", Names(), manifest);
        await Assert.That(enumerations).IsEqualTo(1);

        var clMatch = Match("typed-cl-wrong-profile", BundesligaSeasonSubcompetition.ChampionsLeague,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 9);
        var bonusProfile = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true);
        await Assert.That(() => typed.SavePredictionWithResolvedTypedContextAsync(clMatch, new Prediction(2, 1), config, "{}", 0.01,
                "schadensfresse", bonusProfile.Documents.Select(x => x.Name), bonusProfile)).Throws<InvalidDataException>();

        var clQuestion = Question("typed-cl-bonus-wrong-profile") with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };
        var matchProfile = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: false);
        await Assert.That(() => typed.SaveBonusPredictionWithResolvedTypedContextAsync(clQuestion, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "schadensfresse", matchProfile.Documents.Select(x => x.Name), matchProfile)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_bonus_current_rows_fail_closed_for_missing_malformed_and_cross_profile_generation_manifests()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var question = Question("typed-cl-bonus-corrupt") with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };
        var manifest = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true);
        await typed.SaveBonusPredictionWithResolvedTypedContextAsync(question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
            "schadensfresse", manifest.Documents.Select(x => x.Name), manifest);
        var row = (await Fixture.Db.Collection("bonus-predictions").WhereEqualTo("kicktippQuestionId", question.KicktippQuestionId).GetSnapshotAsync()).Documents.Single();

        foreach (var corrupt in new[]
                 {
                     "{\"tableHash\":\"" + new string('a', 64) + "\"}",
                     "{\"rulesObservedAt\":123}",
                     System.Text.Encoding.UTF8.GetString(TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: false).SerializeCanonical())
                 })
        {
            await row.Reference.UpdateAsync("resolvedTypedContextManifest", corrupt);
            await Assert.That(() => typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse"))
                .Throws<InvalidDataException>();
        }

        await row.Reference.UpdateAsync("resolvedTypedContextManifest", FieldValue.Delete);
        await Assert.That(() => typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Typed_Dfb_bonus_save_and_current_read_are_not_supported()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var question = Question("typed-dfb-bonus-unsupported") with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.DfbPokal
        };
        var manifest = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false);

        await Assert.That(() => typed.SaveBonusPredictionWithResolvedTypedContextAsync(question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "schadensfresse", manifest.Documents.Select(x => x.Name), manifest)).Throws<InvalidOperationException>();
        await Assert.That(() => typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Typed_current_generation_observation_accepts_exact_and_stale_but_rejects_future_by_one_tick_for_match_and_bonus()
    {
        var repository = CreateBundesligaRepository();
        var typed = (IResolvedTypedContextPredictionRepository)repository;
        var config = PredictionModelConfig.Create("gpt-5");
        var evaluationInstant = DateTimeOffset.UtcNow.AddMinutes(-1);
        var match = Match("typed-dfb-observation-boundary", BundesligaSeasonSubcompetition.DfbPokal,
            ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout, 11);
        var question = Question("typed-cl-bonus-observation-boundary") with
        {
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.ChampionsLeague
        };
        var exactMatch = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false, observedAt: evaluationInstant);
        var exactBonus = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true, observedAt: evaluationInstant);
        await typed.SavePredictionWithResolvedTypedContextAsync(match, new Prediction(2, 1), config, "{}", 0.01,
            "schadensfresse", exactMatch.Documents.Select(x => x.Name), exactMatch);
        await typed.SaveBonusPredictionWithResolvedTypedContextAsync(question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
            "schadensfresse", exactBonus.Documents.Select(x => x.Name), exactBonus);

        await Assert.That((await typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse", evaluationInstant: evaluationInstant))!.ResolvedTypedContextManifest)
            .IsEqualTo(exactMatch);
        await Assert.That((await typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse", evaluationInstant: evaluationInstant))!.ResolvedTypedContextManifest)
            .IsEqualTo(exactBonus);

        var staleMatch = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false, observedAt: evaluationInstant.AddHours(-48));
        var staleBonus = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true, observedAt: evaluationInstant.AddHours(-48));
        var matchRow = (await Fixture.Db.Collection("match-predictions").WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId).GetSnapshotAsync()).Documents.Single();
        var bonusRow = (await Fixture.Db.Collection("bonus-predictions").WhereEqualTo("kicktippQuestionId", question.KicktippQuestionId).GetSnapshotAsync()).Documents.Single();
        await matchRow.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(staleMatch.SerializeCanonical()));
        await bonusRow.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(staleBonus.SerializeCanonical()));
        await Assert.That((await typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse", evaluationInstant: evaluationInstant))!.ResolvedTypedContextManifest)
            .IsEqualTo(staleMatch);
        await Assert.That((await typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse", evaluationInstant: evaluationInstant))!.ResolvedTypedContextManifest)
            .IsEqualTo(staleBonus);

        var futureMatch = TypedManifest(BundesligaSeasonSubcompetition.DfbPokal, isBonus: false, observedAt: evaluationInstant.AddTicks(1));
        var futureBonus = TypedManifest(BundesligaSeasonSubcompetition.ChampionsLeague, isBonus: true, observedAt: evaluationInstant.AddTicks(1));
        await matchRow.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(futureMatch.SerializeCanonical()));
        await bonusRow.Reference.UpdateAsync("resolvedTypedContextManifest", System.Text.Encoding.UTF8.GetString(futureBonus.SerializeCanonical()));
        await Assert.That(() => typed.GetCurrentTypedPredictionMetadataAsync(match, config, "schadensfresse", evaluationInstant: evaluationInstant))
            .Throws<InvalidDataException>();
        await Assert.That(() => typed.GetCurrentTypedBonusPredictionMetadataAsync(question, config, "schadensfresse", evaluationInstant: evaluationInstant))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Concurrent_typed_initial_match_saves_share_one_semantic_index_zero_row()
    {
        var firstRepository = CreateBundesligaRepository();
        var secondRepository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("fixture-concurrent", BundesligaSeasonSubcompetition.Bundesliga,
            ResultBasis.RegularTime90Minutes, 1);
        var manifest = MatchManifest(match, "typed-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();

        await Task.WhenAll(
            firstRepository.SavePredictionWithResolvedContextAsync(
                match, new Prediction(1, 0), config, "{}", 0.01,
                "typed-community", names, manifest),
            secondRepository.SavePredictionWithResolvedContextAsync(
                match, new Prediction(2, 1), config, "{}", 0.02,
                "typed-community", names, manifest));

        var rows = (await Fixture.Db.Collection("match-predictions").GetSnapshotAsync()).Documents
            .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
            .Where(row => row.KicktippFixtureId == match.KicktippFixtureId)
            .ToArray();
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows.Single().RepredictionIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Concurrent_typed_bonus_initial_and_reprediction_saves_are_atomic_and_limit_safe()
    {
        var firstRepository = CreateBundesligaRepository();
        var secondRepository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var question = Question("question-concurrent");
        var manifest = BonusManifest("typed-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();

        await Task.WhenAll(
            firstRepository.SaveBonusPredictionWithResolvedContextAsync(
                question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "typed-community", names, manifest),
            secondRepository.SaveBonusPredictionWithResolvedContextAsync(
                question, new BonusPrediction(["option-2"]), config, "{}", 0.02,
                "typed-community", names, manifest));

        var firstReprediction = firstRepository.SaveBonusRepredictionWithResolvedContextAsync(
            question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
            "typed-community", names, 1, manifest);
        var secondReprediction = secondRepository.SaveBonusRepredictionWithResolvedContextAsync(
            question, new BonusPrediction(["option-2"]), config, "{}", 0.02,
            "typed-community", names, 1, manifest);
        try
        {
            await Task.WhenAll(firstReprediction, secondReprediction);
        }
        catch (InvalidOperationException)
        {
            // The stale contender must fail after index 1 consumes the caller-approved maximum.
        }

        await Assert.That(new[] { firstReprediction, secondReprediction }
            .Count(task => task.Status == TaskStatus.RanToCompletion)).IsEqualTo(1);
        await Assert.That(() => firstRepository.SaveBonusRepredictionWithResolvedContextAsync(
                question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
                "typed-community", names, 1, manifest))
            .Throws<InvalidOperationException>();

        var rows = (await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Documents
            .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
            .Where(row => row.KicktippQuestionId == question.KicktippQuestionId)
            .OrderBy(row => row.RepredictionIndex)
            .ToArray();
        await Assert.That(rows.Select(row => row.RepredictionIndex)).IsEquivalentTo([0, 1]);
        await Assert.That(await firstRepository.GetCurrentBonusRepredictionIndexAsync(
            question, config, "typed-community")).IsEqualTo(1);
    }

    [Test]
    public async Task Typed_semantic_document_identities_isolate_stable_ids_and_model_configs()
    {
        var repository = CreateBundesligaRepository();
        var firstConfig = PredictionModelConfig.Create(
            "gpt-5", "high", 10_000, "typed-prompt", 1);
        var secondConfig = PredictionModelConfig.Create(
            "gpt-5", "high", 10_000, "typed-prompt", 2);
        var firstQuestion = Question("question-isolated-1");
        var secondQuestion = Question("question-isolated-2") with { Text = firstQuestion.Text };
        var manifest = BonusManifest("typed-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();

        await Task.WhenAll(
            repository.SaveBonusPredictionWithResolvedContextAsync(
                firstQuestion, new BonusPrediction(["option-1"]), firstConfig, "{}", 0.01,
                "typed-community", names, manifest),
            repository.SaveBonusPredictionWithResolvedContextAsync(
                secondQuestion, new BonusPrediction(["option-1"]), firstConfig, "{}", 0.01,
                "typed-community", names, manifest),
            repository.SaveBonusPredictionWithResolvedContextAsync(
                firstQuestion, new BonusPrediction(["option-1"]), secondConfig, "{}", 0.01,
                "typed-community", names, manifest));

        var rows = (await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Documents
            .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
            .ToArray();
        await Assert.That(rows.Length).IsEqualTo(3);
        await Assert.That(rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count()).IsEqualTo(3);
        await Assert.That(rows.Select(row => row.RepredictionIndex).Distinct().Single()).IsEqualTo(0);
    }

    [Test]
    public async Task Typed_current_reads_require_complete_provenance_for_match_and_bonus()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("fixture-manifestless", BundesligaSeasonSubcompetition.Bundesliga,
            ResultBasis.RegularTime90Minutes, 1);
        var question = Question("question-manifestless");
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("match-predictions").Document("typed-manifestless-match").SetAsync(
            new FirestoreMatchPrediction
            {
                Id = "typed-manifestless-match", HomeTeam = match.HomeTeam, AwayTeam = match.AwayTeam,
                StartsAt = Timestamp.FromDateTimeOffset(match.StartsAt.ToInstant().ToDateTimeOffset()), Matchday = match.Matchday,
                KicktippFixtureId = match.KicktippFixtureId, KicktippRoundName = match.KicktippRoundName,
                ResultBasis = match.ResultBasis!.Value.ToSerializedValue(),
                BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                HomeGoals = 1, AwayGoals = 0, CreatedAt = now, UpdatedAt = now,
                Competition = CompetitionIds.Bundesliga2026_27, Model = config.Model,
                ModelConfigKey = config.IdentityKey, CommunityContext = "typed-community", RepredictionIndex = 0
            });
        await Fixture.Db.Collection("bonus-predictions").Document("typed-manifestless-bonus").SetAsync(
            new FirestoreBonusPrediction
            {
                Id = "typed-manifestless-bonus", QuestionText = question.Text,
                KicktippQuestionId = question.KicktippQuestionId,
                BundesligaSeasonSubcompetition = question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                BundesligaSeasonBonusIdentitySha256 = BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(question),
                SelectedOptionIds = ["option-1"], SelectedOptionTexts = ["One"],
                CreatedAt = now, UpdatedAt = now, Competition = CompetitionIds.Bundesliga2026_27,
                Model = config.Model, ModelConfigKey = config.IdentityKey,
                CommunityContext = "typed-community", RepredictionIndex = 0
            });

        await Assert.That(await repository.GetPredictionMetadataAsync(match, config, "typed-community")).IsNull();
        await Assert.That(await repository.HasPredictionAsync(match, config, "typed-community")).IsFalse();
        await Assert.That(await repository.GetMatchRepredictionIndexAsync(match, config, "typed-community")).IsEqualTo(-1);
        await Assert.That(await repository.GetCurrentBonusPredictionAsync(question, config, "typed-community")).IsNull();
        await Assert.That(await repository.HasCurrentBonusPredictionAsync(question, config, "typed-community")).IsFalse();
        await Assert.That(await repository.GetCurrentBonusRepredictionIndexAsync(question, config, "typed-community")).IsEqualTo(-1);
    }

    [Test]
    public async Task Duplicate_full_provenance_typed_match_indices_fail_closed_for_current_cancelled_and_reprediction_paths()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var match = Match("fixture-duplicate", BundesligaSeasonSubcompetition.Bundesliga,
            ResultBasis.RegularTime90Minutes, 1);
        var manifest = MatchManifest(match, "typed-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();
        await repository.SavePredictionWithResolvedContextAsync(
            match, new Prediction(1, 0), config, "{}", 0.01,
            "typed-community", names, manifest);
        await DuplicateTypedMatchRowAsync(match.KicktippFixtureId!, "duplicate-typed-match");

        await Assert.That(() => repository.GetPredictionAsync(match, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetPredictionMetadataAsync(match, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.HasPredictionAsync(match, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetMatchRepredictionIndexAsync(match, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.SaveRepredictionWithResolvedContextAsync(
                match, new Prediction(2, 1), config, "{}", 0.01,
                "typed-community", names, 0, 1, manifest))
            .Throws<InvalidOperationException>();

        var cancelled = Match("fixture-duplicate-cancelled", BundesligaSeasonSubcompetition.Bundesliga,
            ResultBasis.RegularTime90Minutes, 2) with { IsCancelled = true };
        var cancelledManifest = MatchManifest(cancelled, "typed-community");
        var cancelledNames = cancelledManifest.Documents.Select(document => document.Name).ToArray();
        await repository.SavePredictionWithResolvedContextAsync(
            cancelled, new Prediction(0, 0), config, "{}", 0.01,
            "typed-community", cancelledNames, cancelledManifest);
        await DuplicateTypedMatchRowAsync(cancelled.KicktippFixtureId!, "duplicate-typed-cancelled-match");

        await Assert.That(() => repository.GetCurrentCancelledMatchPredictionAsync(
                cancelled, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetCurrentCancelledMatchPredictionMetadataAsync(
                cancelled, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetCurrentCancelledMatchRepredictionIndexAsync(
                cancelled, config, "typed-community"))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Duplicate_full_provenance_typed_bonus_indices_fail_closed_for_current_has_index_copy_and_reprediction_paths()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var question = Question("question-duplicate");
        var manifest = BonusManifest("typed-community");
        var names = manifest.Documents.Select(document => document.Name).ToArray();
        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question, new BonusPrediction(["option-1"]), config, "{}", 0.01,
            "typed-community", names, manifest);
        await DuplicateTypedBonusRowAsync(question.KicktippQuestionId!, "duplicate-typed-bonus");

        await Assert.That(() => repository.GetCurrentBonusPredictionAsync(
                question, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetCurrentBonusPredictionMetadataAsync(
                question, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.HasCurrentBonusPredictionAsync(
                question, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetCurrentBonusRepredictionIndexAsync(
                question, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.GetBonusPredictionCopyCandidateAsync(
                question, config, "typed-community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.SaveBonusRepredictionWithResolvedContextAsync(
                question, new BonusPrediction(["option-2"]), config, "{}", 0.01,
                "typed-community", names, 1, manifest))
            .Throws<InvalidOperationException>();
    }

    private async Task DuplicateTypedMatchRowAsync(string fixtureId, string duplicateId)
    {
        var original = (await Fixture.Db.Collection("match-predictions")
                .WhereEqualTo("kicktippFixtureId", fixtureId)
                .GetSnapshotAsync())
            .Documents.Single()
            .ConvertTo<FirestoreMatchPrediction>();
        original.Id = duplicateId;
        await Fixture.Db.Collection("match-predictions").Document(duplicateId).SetAsync(original);
    }

    private async Task DuplicateTypedBonusRowAsync(string questionId, string duplicateId)
    {
        var original = (await Fixture.Db.Collection("bonus-predictions")
                .WhereEqualTo("kicktippQuestionId", questionId)
                .GetSnapshotAsync())
            .Documents.Single()
            .ConvertTo<FirestoreBonusPrediction>();
        original.Id = duplicateId;
        await Fixture.Db.Collection("bonus-predictions").Document(duplicateId).SetAsync(original);
    }

    private FirebasePredictionRepository CreateBundesligaRepository() =>
        CreateRepository(competition: NullableOption.Some(CompetitionIds.Bundesliga2026_27));

    private static Match Match(string fixtureId, BundesligaSeasonSubcompetition subcompetition, ResultBasis resultBasis, int matchday) =>
        new("FC Bayern München", "Borussia Dortmund", Instant.FromUtc(2026, 9, 1, 18, 0).Plus(Duration.FromHours(matchday)).InUtc(), matchday)
        {
            KicktippFixtureId = fixtureId,
            KicktippRoundName = $"{subcompetition} exact round",
            BundesligaSeasonSubcompetition = subcompetition,
            ResultBasis = resultBasis
        };

    private static ResolvedMatchContextManifest MatchManifest(Match match, string community) =>
        ResolvedMatchContextManifest.Create(CompetitionIds.Bundesliga2026_27, community,
            MatchContextDocumentCatalog.ForMatch(match, community, CompetitionIds.Bundesliga2026_27).RequiredDocumentNames
                .Select((name, index) => new ResolvedMatchContextDocument(name, index, "Context", DocumentPublicationContract.ComputeContentSha256(name))),
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private static ResolvedBonusContextManifest BonusManifest(string community) =>
        ResolvedBonusContextManifest.Create(CompetitionIds.Bundesliga2026_27, community,
            [new ResolvedBonusContextDocument("Kpi", "club-elo-rankings", 1, DocumentPublicationContract.ComputeContentSha256("elo")),
             new ResolvedBonusContextDocument("Kpi", "team-squad-summary", 1, DocumentPublicationContract.ComputeContentSha256("summary"))],
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private static ResolvedTypedContextManifest TypedManifest(
        BundesligaSeasonSubcompetition subcompetition,
        bool isBonus,
        DateTimeOffset? observedAt = null,
        int documentVersion = 7) =>
        new(CompetitionIds.Bundesliga2026_27, "schadensfresse",
            subcompetition,
            subcompetition == BundesligaSeasonSubcompetition.DfbPokal
                ? "schadensfresse-dfb-pokal-rules-only-v1"
                : isBonus
                    ? "schadensfresse-champions-league-bonus-rules-only-v1"
                    : "schadensfresse-champions-league-match-rules-only-v1",
            new string('a', 64), observedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            SchadensfresseRulesCanonicalJson.SchemaVersion,
            SchadensfresseRulesCanonicalJson.CanonicalSha256,
            [new ResolvedTypedContextDocument("Context", "community-rules-schadensfresse.md", documentVersion, new string('b', 64))]);

    private static BonusQuestion Question(string id) =>
        new("Exact typed question", Instant.FromUtc(2026, 9, 8, 16, 45).InUtc(),
            [new BonusQuestionOption("option-1", "One"), new BonusQuestionOption("option-2", "Two")], 1)
        {
            KicktippQuestionId = id,
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga
        };
}
