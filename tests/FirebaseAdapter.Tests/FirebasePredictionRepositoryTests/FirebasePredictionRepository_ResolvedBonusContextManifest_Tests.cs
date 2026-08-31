using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using NodaTime;
using TestUtilities;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public sealed class FirebasePredictionRepository_ResolvedBonusContextManifest_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Provenance_capable_bonus_save_round_trips_exact_canonical_manifest()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion(text: "Wer wird Deutscher Meister?");
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("test-community");

        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question,
            CreateBonusPrediction(),
            config,
            "{}",
            0.01,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            manifest);

        var metadata = await repository.GetCurrentBonusPredictionMetadataAsync(
            question, config, manifest.CommunityContext);
        var stored = (await Fixture.Db.Collection("bonus-predictions")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .GetSnapshotAsync()).Documents.Single().ConvertTo<FirestoreBonusPrediction>();

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNotNull();
        await Assert.That(metadata.QuestionCompatibilityManifest).IsNotNull();
        await Assert.That(metadata.QuestionCompatibilityManifest!.CompatibilitySha256)
            .IsEqualTo(BonusQuestionCompatibilityManifest.Create(question).CompatibilitySha256);
        await Assert.That(metadata.ResolvedContextManifest!.Documents.Select(document => document.Name))
            .IsEquivalentTo(manifest.Documents.Select(document => document.Name));
        await Assert.That(stored.ResolvedBonusContextManifest).StartsWith(
            "{\"competition\":\"bundesliga-2026-27\",\"communityContext\":\"test-community\",\"documents\":[{\"kind\":\"Kpi\",\"name\":\"club-elo-rankings\"");
        await Assert.That(stored.ResolvedBonusContextManifest).DoesNotContain("elo-content");
        await Assert.That(stored.ResolvedBonusContextManifest).DoesNotContain("summary-content");
    }

    [Test]
    public async Task Copy_candidate_round_trips_complete_source_options_and_normalized_lookup()
    {
        var repository = CreateBundesligaRepository();
        var sourceQuestion = CreateBonusQuestion(
            text: "  Wer\t wird Meister? ",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("source-fcb", "ＦＣ Bayern  München"),
                new BonusQuestionOption("source-bvb", "Borussia Dortmund")
            }) with { KicktippQuestionId = null, BundesligaSeasonSubcompetition = null };
        var targetQuestion = CreateBonusQuestion(
            text: "Wer wird Meister?",
            options: new List<BonusQuestionOption>
            {
                new BonusQuestionOption("target-bvb", "Borussia   Dortmund"),
                new BonusQuestionOption("target-fcb", "FC Bayern München")
            }) with { KicktippQuestionId = null, BundesligaSeasonSubcompetition = null };
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("test-community");
        var serializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        await SeedLegacyAsync(
            sourceQuestion,
            config,
            System.Text.Json.JsonSerializer.Serialize(manifest, serializerOptions),
            System.Text.Json.JsonSerializer.Serialize(
                BonusQuestionCompatibilityManifest.Create(sourceQuestion), serializerOptions));

        var candidate = await ((IBonusPredictionCopyRepository)repository)
            .GetBonusPredictionCopyCandidateAsync(targetQuestion, config, manifest.CommunityContext);

        await Assert.That(candidate).IsNotNull();
        await Assert.That(candidate!.PredictionIdentity).IsNotNull().And.IsNotEmpty();
        await Assert.That(candidate!.QuestionCompatibilityManifest).IsNotNull();
        await Assert.That(candidate.QuestionCompatibilityManifest!.Options.Select(option => option.SourceOptionId))
            .IsEquivalentTo(["source-bvb", "source-fcb"]);
        var compatibility = candidate.QuestionCompatibilityManifest.TryMapPrediction(
            targetQuestion,
            candidate.BonusPrediction,
            out var mapped,
            out _);
        await Assert.That(compatibility).IsEqualTo(BonusPredictionCopyCompatibility.Compatible);
        await Assert.That(mapped!.SelectedOptionIds).IsEquivalentTo(["target-fcb"]);
    }

    [Test]
    public async Task Copy_candidate_selects_only_the_exact_competing_model_configuration()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion(text: "Which club wins the league?");
        var version1 = PredictionModelConfig.Create(
            "gpt-5.6-luna",
            "none",
            10_000,
            "kicktippai/bundesliga-2026-27/predict-bonus",
            1);
        var version2 = PredictionModelConfig.Create(
            "gpt-5.6-luna",
            "none",
            10_000,
            "kicktippai/bundesliga-2026-27/predict-bonus",
            2);
        var manifest = CreateManifest("pes-squad");

        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question,
            new BonusPrediction([question.Options[0].Id]),
            version1,
            "{}",
            0.01,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            manifest);
        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question,
            new BonusPrediction([question.Options[1].Id]),
            version2,
            "{}",
            0.02,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            manifest);

        var version1Candidate = await ((IBonusPredictionCopyRepository)repository)
            .GetBonusPredictionCopyCandidateAsync(question, version1, manifest.CommunityContext);
        var version2Candidate = await ((IBonusPredictionCopyRepository)repository)
            .GetBonusPredictionCopyCandidateAsync(question, version2, manifest.CommunityContext);

        await Assert.That(version1Candidate!.BonusPrediction.SelectedOptionIds)
            .IsEquivalentTo([question.Options[0].Id]);
        await Assert.That(version2Candidate!.BonusPrediction.SelectedOptionIds)
            .IsEquivalentTo([question.Options[1].Id]);
    }

    [Test]
    public async Task Provenance_capable_bonus_reprediction_round_trips_the_manifest()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion(text: "Wer wird Torschützenkönig?");
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("test-community");

        await repository.SaveBonusPredictionWithResolvedContextAsync(
            question,
            CreateBonusPrediction(),
            config,
            "{}",
            0.01,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            manifest);

        await repository.SaveBonusRepredictionWithResolvedContextAsync(
            question,
            CreateBonusPrediction(),
            config,
            "{}",
            0.01,
            manifest.CommunityContext,
            manifest.Documents.Select(document => document.Name),
            1,
            manifest);

        var metadata = await repository.GetCurrentBonusPredictionMetadataAsync(
            question, config, manifest.CommunityContext);

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNotNull();
        await Assert.That(metadata.ResolvedContextManifest!.Documents
            .Select(document => document.Name)
            .SequenceEqual(manifest.Documents.Select(document => document.Name))).IsTrue();
    }

    [Test]
    public async Task Ordinary_Bundesliga_bonus_save_entrypoints_reject_manifestless_new_writes()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion();
        var prediction = CreateBonusPrediction();
        var config = PredictionModelConfig.Create("gpt-5");

        await Assert.That(() => repository.SaveBonusPredictionAsync(
                question, prediction, config, "{}", 0.01, "test-community", []))
            .Throws<InvalidOperationException>();
        await Assert.That(() => repository.SaveBonusRepredictionAsync(
                question, prediction, config, "{}", 0.01, "test-community", [], 1))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Provenance_capable_bonus_saves_reject_scope_and_name_mismatches()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion();
        var prediction = CreateBonusPrediction();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("different-community");

        await Assert.That(() => repository.SaveBonusPredictionWithResolvedContextAsync(
                question, prediction, config, "{}", 0.01, "test-community",
                manifest.Documents.Select(document => document.Name), manifest))
            .Throws<InvalidDataException>();
        await Assert.That(() => repository.SaveBonusRepredictionWithResolvedContextAsync(
                question, prediction, config, "{}", 0.01, manifest.CommunityContext,
                ["team-squad-summary", "club-elo-rankings"], 1, manifest))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Provenance_manifest_is_rejected_outside_the_Bundesliga_scope()
    {
        var repository = CreateRepository(
            competition: EHonda.Optional.Core.Option.Some(CompetitionIds.FifaWorldCup2026));
        var question = CreateBonusQuestion();
        var prediction = CreateBonusPrediction();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("test-community");

        await Assert.That(() => repository.SaveBonusPredictionWithResolvedContextAsync(
                question, prediction, config, "{}", 0.01, manifest.CommunityContext,
                manifest.Documents.Select(document => document.Name), manifest))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Manifest_rejects_noncanonical_baseline_hash_and_roster_ordering()
    {
        var elo = new ResolvedBonusContextDocument(
            "Kpi", "club-elo-rankings", 1, DocumentPublicationContract.ComputeContentSha256("elo"));
        var summary = new ResolvedBonusContextDocument(
            "Kpi", "team-squad-summary", 1, DocumentPublicationContract.ComputeContentSha256("summary"));
        var bvb = new ResolvedBonusContextDocument(
            "Context", "roster-bvb", 1, DocumentPublicationContract.ComputeContentSha256("bvb"));
        var fcb = new ResolvedBonusContextDocument(
            "Context", "roster-fcb", 1, DocumentPublicationContract.ComputeContentSha256("fcb"));

        await Assert.That(() => CreateManifest([summary, elo])).Throws<ArgumentException>();
        await Assert.That(() => CreateManifest([
                new ResolvedBonusContextDocument("Kpi", elo.Name, elo.Version, elo.ContentSha256.ToUpperInvariant()),
                summary]))
            .Throws<ArgumentException>();
        await Assert.That(() => CreateManifest([elo, summary, fcb, bvb])).Throws<ArgumentException>();
        await Assert.That(() => CreateManifest([elo, summary, bvb, bvb])).Throws<ArgumentException>();
    }

    [Test]
    public async Task Legacy_bonus_metadata_remains_readable_without_a_manifest()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var question = CreateBonusQuestion(text: "Legacy question?");
        await SeedLegacyAsync(question, config, null);

        var metadata = await repository.GetBonusPredictionMetadataByTextAsync(
            question.Text, config, "test-community");
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNull();
        await Assert.That(metadata.QuestionCompatibilityManifest).IsNull();
        await Assert.That(await repository.GetAllBonusPredictionsAsync(config, "test-community")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Legacy_or_malformed_option_provenance_is_returned_as_noncopyable()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = System.Text.Json.JsonSerializer.Serialize(
            CreateManifest("test-community"),
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        var legacy = CreateBonusQuestion(text: "Legacy options?") with
        {
            KicktippQuestionId = null,
            BundesligaSeasonSubcompetition = null
        };
        var malformed = CreateBonusQuestion(text: "Malformed options?") with
        {
            KicktippQuestionId = null,
            BundesligaSeasonSubcompetition = null
        };
        await SeedLegacyAsync(legacy, config, manifest);
        await SeedLegacyAsync(malformed, config, manifest, "{invalid-json");

        var legacyCandidate = await ((IBonusPredictionCopyRepository)repository)
            .GetBonusPredictionCopyCandidateAsync(legacy, config, "test-community");
        var malformedCandidate = await ((IBonusPredictionCopyRepository)repository)
            .GetBonusPredictionCopyCandidateAsync(malformed, config, "test-community");

        await Assert.That(legacyCandidate).IsNotNull();
        await Assert.That(legacyCandidate!.QuestionCompatibilityManifest).IsNull();
        await Assert.That(malformedCandidate).IsNotNull();
        await Assert.That(malformedCandidate!.QuestionCompatibilityManifest).IsNull();
    }

    [Test]
    public async Task Noncanonical_stored_bonus_manifest_fails_closed()
    {
        var repository = CreateBundesligaRepository();
        var config = PredictionModelConfig.Create("gpt-5");
        var question = CreateBonusQuestion(text: "Corrupt question?");
        var canonical = System.Text.Json.JsonSerializer.Serialize(CreateManifest("test-community"));
        await SeedLegacyAsync(question, config, canonical.Replace(
            "{\"competition\"",
            "{ \"competition\"",
            StringComparison.Ordinal));

        await Assert.That(() => repository.GetBonusPredictionMetadataByTextAsync(
                question.Text, config, "test-community"))
            .Throws<InvalidDataException>();
    }

    private static BonusQuestion CreateBonusQuestion(
        Option<string> text = default,
        Option<ZonedDateTime> deadline = default,
        Option<List<BonusQuestionOption>> options = default,
        Option<int> maxSelections = default,
        NullableOption<string> formFieldName = default)
    {
        var question = CoreTestFactories.CreateBonusQuestion(
            text, deadline, options, maxSelections, formFieldName);
        return question with
        {
            KicktippQuestionId = $"test-question-{Math.Abs(question.Text.GetHashCode(StringComparison.Ordinal))}",
            BundesligaSeasonSubcompetition = BundesligaSeasonSubcompetition.Bundesliga
        };
    }

    private FirebasePredictionRepository CreateBundesligaRepository() =>
        CreateRepository(competition: EHonda.Optional.Core.Option.Some(CompetitionIds.Bundesliga2026_27));

    private static ResolvedBonusContextManifest CreateManifest(string communityContext) =>
        ResolvedBonusContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            communityContext,
            [
                new ResolvedBonusContextDocument(
                    "Kpi",
                    BundesligaDocumentPublication.ClubEloRankingsDocumentName,
                    3,
                    DocumentPublicationContract.ComputeContentSha256("elo-content")),
                new ResolvedBonusContextDocument(
                    "Kpi",
                    BundesligaRosterPublicationContract.SquadSummaryDocumentName,
                    5,
                    DocumentPublicationContract.ComputeContentSha256("summary-content"))
            ],
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private static ResolvedBonusContextManifest CreateManifest(
        IEnumerable<ResolvedBonusContextDocument> documents) =>
        ResolvedBonusContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            "test-community",
            documents,
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private async Task SeedLegacyAsync(
        BonusQuestion question,
        PredictionModelConfig config,
        string? manifest,
        string? questionCompatibilityManifest = null)
    {
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("bonus-predictions").Document(Guid.NewGuid().ToString()).SetAsync(
            new FirestoreBonusPrediction
            {
                QuestionText = question.Text,
                SelectedOptionIds = [question.Options[0].Id],
                SelectedOptionTexts = [question.Options[0].Text],
                CreatedAt = now,
                UpdatedAt = now,
                Competition = CompetitionIds.Bundesliga2026_27,
                Model = config.Model,
                ModelConfigKey = config.IdentityKey,
                CommunityContext = "test-community",
                ContextDocumentNames = manifest is null
                    ? []
                    : ["club-elo-rankings", "team-squad-summary"],
                ResolvedBonusContextManifest = manifest,
                BonusQuestionCompatibilityManifest = questionCompatibilityManifest,
                RepredictionIndex = 0
            });
    }
}
