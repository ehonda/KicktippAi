using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
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

        var metadata = await repository.GetBonusPredictionMetadataByTextAsync(
            question.Text,
            config,
            manifest.CommunityContext);
        var stored = (await Fixture.Db.Collection("bonus-predictions")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .GetSnapshotAsync()).Documents.Single().ConvertTo<FirestoreBonusPrediction>();

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNotNull();
        await Assert.That(metadata.ResolvedContextManifest!.Documents.Select(document => document.Name))
            .IsEquivalentTo(manifest.Documents.Select(document => document.Name));
        await Assert.That(stored.ResolvedBonusContextManifest).StartsWith(
            "{\"competition\":\"bundesliga-2026-27\",\"communityContext\":\"test-community\",\"documents\":[{\"kind\":\"Kpi\",\"name\":\"club-elo-rankings\"");
        await Assert.That(stored.ResolvedBonusContextManifest).DoesNotContain("elo-content");
        await Assert.That(stored.ResolvedBonusContextManifest).DoesNotContain("summary-content");
    }

    [Test]
    public async Task Provenance_capable_bonus_reprediction_round_trips_the_manifest()
    {
        var repository = CreateBundesligaRepository();
        var question = CreateBonusQuestion(text: "Wer wird Torschützenkönig?");
        var config = PredictionModelConfig.Create("gpt-5");
        var manifest = CreateManifest("test-community");

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

        var metadata = await repository.GetBonusPredictionMetadataByTextAsync(
            question.Text,
            config,
            manifest.CommunityContext);

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
            question.Text,
            config,
            "test-community");

        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ResolvedContextManifest).IsNull();
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
                question.Text,
                config,
                "test-community"))
            .Throws<InvalidDataException>();
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
        string? manifest)
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
                RepredictionIndex = 0
            });
    }
}
