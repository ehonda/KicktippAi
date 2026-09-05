using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public sealed class FirebasePredictionRepository_ChampionsLeagueBonus_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Exact_specialized_write_round_trips_and_is_excluded_from_generic_cache_reads()
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        var prediction = CreatePrediction(scope);

        await repository.SaveAsync(scope, prediction, "langfuse", "{}", 0.01, overrideExisting: false);

        var current = await repository.GetCurrentAsync(scope);
        await Assert.That(current).IsNotNull();
        await Assert.That(current!.BonusPrediction.SelectedOptionIds.SequenceEqual(prediction.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();
        await Assert.That(current.ContextDocumentNames).IsEmpty();
        await Assert.That(current.SchadensfresseChampionsLeagueBonusManifest).IsNotNull();
        current.SchadensfresseChampionsLeagueBonusManifest!.Validate(scope);
        await Assert.That(await repository.GetCurrentRepredictionIndexAsync(scope)).IsEqualTo(0);

        var reprediction = new BonusPrediction([scope.Question.Options[1].Id]);
        await repository.SaveRepredictionAsync(
            scope, reprediction, "langfuse", "{}", 0.02,
            expectedCurrentRepredictionIndex: 0, maxRepredictions: 1);
        await Assert.That(await repository.GetCurrentRepredictionIndexAsync(scope)).IsEqualTo(1);
        await Assert.That((await repository.GetCurrentAsync(scope))!.BonusPrediction.SelectedOptionIds
            .SequenceEqual(reprediction.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();

        var forced = new BonusPrediction([scope.Question.Options[2].Id]);
        await repository.SaveAsync(scope, forced, "dedicated-cl-mirror", "{}", 0.03, overrideExisting: true);
        await Assert.That((await repository.GetCurrentAsync(scope))!.BonusPrediction.SelectedOptionIds
            .SequenceEqual(forced.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();
        await Assert.That((await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Documents.Count).IsEqualTo(2);

        var generic = await repository.GetBonusPredictionByTextAsync(
            scope.Question.Text, scope.ModelConfig, SchadensfresseChampionsLeagueBonusProfile.Community);
        await Assert.That(generic).IsNull();
        await Assert.That(await repository.HasBonusPredictionAsync(
            scope.SeedQuestion.KicktippQuestionId,
            scope.ModelConfig,
            SchadensfresseChampionsLeagueBonusProfile.Community)).IsFalse();
        await Assert.That(await repository.GetAllBonusPredictionsAsync(
            scope.ModelConfig,
            SchadensfresseChampionsLeagueBonusProfile.Community)).IsEmpty();
        await Assert.That(await repository.GetAvailableModelsAsync()).IsEmpty();
        await Assert.That(await repository.GetAvailableModelConfigsAsync()).IsEmpty();
        await Assert.That(await repository.GetAvailableCommunityContextsAsync()).IsEmpty();
    }

    [Test]
    public async Task Selection_filters_every_collision_before_choosing_the_latest_exact_lineage()
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        var prediction = CreatePrediction(scope);
        await repository.SaveAsync(scope, prediction, "langfuse", "{}", 0.01, overrideExisting: false);
        var stored = await ReadOnlyStoredRowAsync();

        await WriteCollisionAsync(stored, "ordinary", row =>
        {
            row.SchadensfresseChampionsLeagueBonusManifest = null;
            row.RepredictionIndex = 90;
        });
        await WriteCollisionAsync(stored, "wrong-manifest", row =>
        {
            row.SchadensfresseChampionsLeagueBonusManifest = row.SchadensfresseChampionsLeagueBonusManifest!
                .Replace("1662326752", "1662326753", StringComparison.Ordinal);
            row.RepredictionIndex = 91;
        });
        await WriteCollisionAsync(stored, "both-manifests", row =>
        {
            row.ResolvedBonusContextManifest = "{}";
            row.RepredictionIndex = 92;
        });
        await WriteCollisionAsync(stored, "empty-ordinary-manifest", row =>
        {
            row.ResolvedBonusContextManifest = "";
            row.RepredictionIndex = 93;
        });
        await WriteCollisionAsync(stored, "whitespace-ordinary-manifest", row =>
        {
            row.ResolvedBonusContextManifest = " ";
            row.RepredictionIndex = 94;
        });
        await WriteCollisionAsync(stored, "wrong-config", row =>
        {
            row.ModelConfigKey += "-wrong";
            row.RepredictionIndex = 95;
        });

        var current = await repository.GetCurrentAsync(scope);
        await Assert.That(current).IsNotNull();
        await Assert.That(await repository.GetCurrentRepredictionIndexAsync(scope)).IsEqualTo(0);
        await Assert.That(current!.BonusPrediction.SelectedOptionIds.SequenceEqual(prediction.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    public async Task No_matching_lineage_creates_reprediction_index_zero_despite_a_wrong_collision()
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        var prediction = CreatePrediction(scope);
        await repository.SaveAsync(scope, prediction, "langfuse", "{}", 0.01, overrideExisting: false);
        var stored = await ReadOnlyStoredRowAsync();
        stored.ModelConfigKey += "-wrong";
        stored.RepredictionIndex = 7;
        await Fixture.Db.Collection("bonus-predictions").Document("wrong-only").SetAsync(stored);
        foreach (var document in (await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync()).Documents
                     .Where(document => document.Id != "wrong-only"))
        {
            await document.Reference.DeleteAsync();
        }

        await repository.SaveRepredictionAsync(
            scope, prediction, "dedicated-cl-mirror", "{}", 0.02,
            expectedCurrentRepredictionIndex: -1, maxRepredictions: 0);

        await Assert.That(await repository.GetCurrentRepredictionIndexAsync(scope)).IsEqualTo(0);
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Present_but_blank_specialized_manifest_is_neither_cl_lineage_nor_ordinary_data(string malformedManifest)
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        await repository.SaveAsync(scope, CreatePrediction(scope), "langfuse", "{}", 0.01, overrideExisting: false);
        var stored = await ReadOnlyStoredRowAsync();
        stored.SchadensfresseChampionsLeagueBonusManifest = malformedManifest;
        await Fixture.Db.Collection("bonus-predictions").Document(stored.Id!).SetAsync(stored);

        await Assert.That(await repository.GetCurrentAsync(scope)).IsNull();
        await Assert.That(await repository.GetBonusPredictionByTextAsync(
            scope.Question.Text, scope.ModelConfig, SchadensfresseChampionsLeagueBonusProfile.Community)).IsNull();
        await Assert.That(await repository.GetAllBonusPredictionsAsync(
            scope.ModelConfig, SchadensfresseChampionsLeagueBonusProfile.Community)).IsEmpty();
        await Assert.That(await repository.GetAvailableModelsAsync()).IsEmpty();
        await Assert.That(await repository.GetAvailableModelConfigsAsync()).IsEmpty();
        await Assert.That(await repository.GetAvailableCommunityContextsAsync()).IsEmpty();
    }

    [Test]
    public async Task Ordinary_save_does_not_update_or_replace_a_specialized_row()
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        var clPrediction = CreatePrediction(scope);
        await repository.SaveAsync(scope, clPrediction, "langfuse", "{}", 0.01, overrideExisting: false);
        var ordinaryPrediction = new BonusPrediction([scope.Question.Options[1].Id]);
        var manifest = CreateOrdinaryManifest();

        await repository.SaveBonusPredictionWithResolvedContextAsync(
            scope.Question,
            ordinaryPrediction,
            scope.ModelConfig,
            "{}",
            0.02,
            SchadensfresseChampionsLeagueBonusProfile.Community,
            manifest.Documents.Select(document => document.Name),
            manifest,
            overrideCreatedAt: true);

        var documents = await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync();
        await Assert.That(documents.Documents.Count).IsEqualTo(2);
        await Assert.That((await repository.GetCurrentAsync(scope))!.BonusPrediction.SelectedOptionIds
            .SequenceEqual(clPrediction.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();
        await Assert.That((await repository.GetBonusPredictionByTextAsync(
            scope.Question.Text,
            scope.ModelConfig,
            SchadensfresseChampionsLeagueBonusProfile.Community))!.SelectedOptionIds
            .SequenceEqual(ordinaryPrediction.SelectedOptionIds, StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Duplicate_exact_index_and_stale_reprediction_expectation_fail_closed()
    {
        var repository = CreateRepository(competition: CompetitionIds.Bundesliga2026_27);
        var scope = CreateScope(0);
        var prediction = CreatePrediction(scope);
        await repository.SaveAsync(scope, prediction, "langfuse", "{}", 0.01, overrideExisting: false);

        await Assert.That(() => repository.SaveRepredictionAsync(
                scope, prediction, "langfuse", "{}", 0.01,
                expectedCurrentRepredictionIndex: -1, maxRepredictions: 2))
            .Throws<InvalidOperationException>();

        var stored = await ReadOnlyStoredRowAsync();
        await Fixture.Db.Collection("bonus-predictions").Document("duplicate-exact").SetAsync(stored);
        await Assert.That(() => repository.GetCurrentAsync(scope)).Throws<InvalidDataException>();
    }

    private static SchadensfresseChampionsLeagueBonusPredictionScope CreateScope(int questionIndex)
    {
        var seed = SchadensfresseChampionsLeagueBonusSeed.Default.Questions[questionIndex];
        var question = new BonusQuestion(
            seed.Text,
            NodaTime.Text.InstantPattern.ExtendedIso.Parse(seed.Deadline).Value.InUtc(),
            seed.Options.Select(option => new BonusQuestionOption(option.Id, option.Text)).ToList(),
            seed.MaxSelections,
            seed.FormKeys[0]);
        return SchadensfresseChampionsLeagueBonusPredictionScope.Create(
            question, SchadensfresseChampionsLeagueBonusProfile.CreateModelConfig());
    }

    private static BonusPrediction CreatePrediction(SchadensfresseChampionsLeagueBonusPredictionScope scope) =>
        new(scope.Question.Options.Take(scope.Question.MaxSelections).Select(option => option.Id).ToList());

    private static ResolvedBonusContextManifest CreateOrdinaryManifest() =>
        ResolvedBonusContextManifest.Create(
            CompetitionIds.Bundesliga2026_27,
            SchadensfresseChampionsLeagueBonusProfile.Community,
            [
                new ResolvedBonusContextDocument(
                    "Kpi", "club-elo-rankings", 1,
                    DocumentPublicationContract.ComputeContentSha256("elo")),
                new ResolvedBonusContextDocument(
                    "Kpi", "team-squad-summary", 1,
                    DocumentPublicationContract.ComputeContentSha256("summary"))
            ],
            new string('a', DocumentPublicationContract.Sha256HexLength),
            new string('b', DocumentPublicationContract.Sha256HexLength));

    private async Task<FirestoreBonusPrediction> ReadOnlyStoredRowAsync()
    {
        var snapshot = await Fixture.Db.Collection("bonus-predictions").GetSnapshotAsync();
        return snapshot.Documents.Single().ConvertTo<FirestoreBonusPrediction>();
    }

    private async Task WriteCollisionAsync(
        FirestoreBonusPrediction source,
        string id,
        Action<FirestoreBonusPrediction> mutate)
    {
        var clone = Clone(source);
        clone.Id = id;
        mutate(clone);
        await Fixture.Db.Collection("bonus-predictions").Document(id).SetAsync(clone);
    }

    private static FirestoreBonusPrediction Clone(FirestoreBonusPrediction source) => new()
    {
        Id = source.Id,
        QuestionId = source.QuestionId,
        QuestionText = source.QuestionText,
        QuestionDeadline = source.QuestionDeadline,
        SelectedOptionIds = source.SelectedOptionIds.ToArray(),
        SelectedOptionTexts = source.SelectedOptionTexts.ToArray(),
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        Competition = source.Competition,
        Model = source.Model,
        ModelConfigKey = source.ModelConfigKey,
        ReasoningEffort = source.ReasoningEffort,
        MaxOutputTokenCount = source.MaxOutputTokenCount,
        PromptName = source.PromptName,
        PromptVersion = source.PromptVersion,
        TokenUsage = source.TokenUsage,
        Cost = source.Cost,
        CommunityContext = source.CommunityContext,
        ContextDocumentNames = source.ContextDocumentNames.ToArray(),
        ResolvedBonusContextManifest = source.ResolvedBonusContextManifest,
        SchadensfresseChampionsLeagueBonusManifest = source.SchadensfresseChampionsLeagueBonusManifest,
        BonusQuestionCompatibilityManifest = source.BonusQuestionCompatibilityManifest,
        RepredictionIndex = source.RepredictionIndex
    };
}
