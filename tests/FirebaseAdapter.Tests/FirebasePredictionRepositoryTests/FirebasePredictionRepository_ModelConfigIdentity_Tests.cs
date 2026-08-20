using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using TestUtilities;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;

namespace FirebaseAdapter.Tests.FirebasePredictionRepositoryTests;

public class FirebasePredictionRepository_ModelConfigIdentity_Tests(FirestoreFixture fixture)
    : FirebasePredictionRepositoryTests_Base(fixture)
{
    [Test]
    public async Task Match_predictions_with_same_model_are_isolated_by_reasoning_effort()
    {
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var minimalConfig = PredictionModelConfig.Create("gpt-5-nano", "minimal");
        var highConfig = PredictionModelConfig.Create("gpt-5-nano", "high");
        var minimalPrediction = CreatePrediction(homeGoals: 1, awayGoals: 0);
        var highPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var updatedMinimalPrediction = CreatePrediction(homeGoals: 3, awayGoals: 1);

        await repository.SavePredictionAsync(match, minimalPrediction, minimalConfig, "100", 0.01, "test-community", ["minimal-doc"]);
        await repository.SavePredictionAsync(match, highPrediction, highConfig, "200", 0.02, "test-community", ["high-doc"]);

        await repository.SavePredictionAsync(match, updatedMinimalPrediction, minimalConfig, "150", 0.03, "test-community", ["minimal-updated-doc"]);

        var minimalResult = await repository.GetPredictionAsync(match, minimalConfig, "test-community");
        var highResult = await repository.GetPredictionAsync(match, highConfig, "test-community");
        var minimalMetadata = await repository.GetPredictionMetadataAsync(match, minimalConfig, "test-community");
        var highMetadata = await repository.GetPredictionMetadataAsync(match, highConfig, "test-community");

        await Assert.That(minimalResult).IsEqualTo(updatedMinimalPrediction);
        await Assert.That(highResult).IsEqualTo(highPrediction);
        await Assert.That(minimalMetadata!.ContextDocumentNames).IsEquivalentTo(["minimal-updated-doc"]);
        await Assert.That(highMetadata!.ContextDocumentNames).IsEquivalentTo(["high-doc"]);
    }

    [Test]
    public async Task Bonus_predictions_with_same_model_are_isolated_by_reasoning_effort()
    {
        var repository = CreateRepository();
        var question = CreateBonusQuestion(text: "Who will win?");
        var minimalConfig = PredictionModelConfig.Create("gpt-5-nano", "minimal");
        var highConfig = PredictionModelConfig.Create("gpt-5-nano", "high");
        var minimalPrediction = new BonusPrediction(["opt-1"]);
        var highPrediction = new BonusPrediction(["opt-2"]);
        var updatedHighPrediction = new BonusPrediction(["opt-3"]);

        await repository.SaveBonusPredictionAsync(question, minimalPrediction, minimalConfig, "100", 0.01, "test-community", ["minimal-doc"]);
        await repository.SaveBonusPredictionAsync(question, highPrediction, highConfig, "200", 0.02, "test-community", ["high-doc"]);

        await repository.SaveBonusPredictionAsync(question, updatedHighPrediction, highConfig, "250", 0.03, "test-community", ["high-updated-doc"]);

        var minimalResult = await repository.GetBonusPredictionByTextAsync("Who will win?", minimalConfig, "test-community");
        var highResult = await repository.GetBonusPredictionByTextAsync("Who will win?", highConfig, "test-community");
        var minimalMetadata = await repository.GetBonusPredictionMetadataByTextAsync("Who will win?", minimalConfig, "test-community");
        var highMetadata = await repository.GetBonusPredictionMetadataByTextAsync("Who will win?", highConfig, "test-community");

        await Assert.That(minimalResult!.SelectedOptionIds).IsEquivalentTo(minimalPrediction.SelectedOptionIds);
        await Assert.That(highResult!.SelectedOptionIds).IsEquivalentTo(updatedHighPrediction.SelectedOptionIds);
        await Assert.That(minimalMetadata!.ContextDocumentNames).IsEquivalentTo(["minimal-doc"]);
        await Assert.That(highMetadata!.ContextDocumentNames).IsEquivalentTo(["high-updated-doc"]);
    }

    [Test]
    public async Task Reprediction_paths_are_isolated_by_reasoning_effort()
    {
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var question = CreateBonusQuestion(text: "Who will win?");
        var minimalConfig = PredictionModelConfig.Create("gpt-5-nano", "minimal");
        var highConfig = PredictionModelConfig.Create("gpt-5-nano", "high");

        await repository.SavePredictionAsync(match, CreatePrediction(homeGoals: 1, awayGoals: 0), minimalConfig, "100", 0.01, "test-community", []);
        await repository.SaveRepredictionAsync(match, CreatePrediction(homeGoals: 2, awayGoals: 1), minimalConfig, "120", 0.02, "test-community", [], 1);
        await repository.SavePredictionAsync(match, CreatePrediction(homeGoals: 0, awayGoals: 0), highConfig, "100", 0.01, "test-community", []);

        await repository.SaveBonusPredictionAsync(question, new BonusPrediction(["opt-1"]), minimalConfig, "100", 0.01, "test-community", []);
        await repository.SaveBonusRepredictionAsync(question, new BonusPrediction(["opt-2"]), minimalConfig, "120", 0.02, "test-community", [], 2);
        await repository.SaveBonusPredictionAsync(question, new BonusPrediction(["opt-3"]), highConfig, "100", 0.01, "test-community", []);

        var minimalMatchIndex = await repository.GetMatchRepredictionIndexAsync(match, minimalConfig, "test-community");
        var highMatchIndex = await repository.GetMatchRepredictionIndexAsync(match, highConfig, "test-community");
        var minimalCancelledIndex = await repository.GetCancelledMatchRepredictionIndexAsync("Team A", "Team B", minimalConfig, "test-community");
        var highCancelledIndex = await repository.GetCancelledMatchRepredictionIndexAsync("Team A", "Team B", highConfig, "test-community");
        var minimalCancelledPrediction = await repository.GetCancelledMatchPredictionAsync("Team A", "Team B", minimalConfig, "test-community");
        var highCancelledPrediction = await repository.GetCancelledMatchPredictionAsync("Team A", "Team B", highConfig, "test-community");
        var minimalBonusIndex = await repository.GetBonusRepredictionIndexAsync("Who will win?", minimalConfig, "test-community");
        var highBonusIndex = await repository.GetBonusRepredictionIndexAsync("Who will win?", highConfig, "test-community");

        await Assert.That(minimalMatchIndex).IsEqualTo(1);
        await Assert.That(highMatchIndex).IsEqualTo(0);
        await Assert.That(minimalCancelledIndex).IsEqualTo(1);
        await Assert.That(highCancelledIndex).IsEqualTo(0);
        await Assert.That(minimalCancelledPrediction).IsEqualTo(CreatePrediction(homeGoals: 2, awayGoals: 1));
        await Assert.That(highCancelledPrediction).IsEqualTo(CreatePrediction(homeGoals: 0, awayGoals: 0));
        await Assert.That(minimalBonusIndex).IsEqualTo(2);
        await Assert.That(highBonusIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Legacy_match_rows_are_read_only_when_reasoning_effort_is_omitted_and_exact_rows_win()
    {
        var repository = CreateRepository();
        var legacyOnlyMatch = CreateMatch(homeTeam: "Legacy Home", awayTeam: "Legacy Away");
        var exactWinsMatch = CreateMatch(homeTeam: "Exact Home", awayTeam: "Exact Away");
        var modelDefaultConfig = PredictionModelConfig.Create("gpt-5-nano");
        var explicitMinimalConfig = PredictionModelConfig.Create("gpt-5-nano", "minimal");

        await InsertLegacyMatchPredictionAsync(legacyOnlyMatch, "gpt-5-nano", "test-community", CreatePrediction(homeGoals: 1, awayGoals: 0), repredictionIndex: 0);
        await repository.SavePredictionAsync(exactWinsMatch, CreatePrediction(homeGoals: 2, awayGoals: 1), modelDefaultConfig, "100", 0.01, "test-community", ["exact-doc"]);
        await InsertLegacyMatchPredictionAsync(exactWinsMatch, "gpt-5-nano", "test-community", CreatePrediction(homeGoals: 9, awayGoals: 9), repredictionIndex: 9);

        var omittedReadsLegacy = await repository.GetPredictionAsync(legacyOnlyMatch, modelDefaultConfig, "test-community");
        var explicitDoesNotReadLegacy = await repository.GetPredictionAsync(legacyOnlyMatch, explicitMinimalConfig, "test-community");
        var exactWinsOverLegacy = await repository.GetPredictionAsync(exactWinsMatch, modelDefaultConfig, "test-community");

        await Assert.That(omittedReadsLegacy).IsEqualTo(CreatePrediction(homeGoals: 1, awayGoals: 0));
        await Assert.That(explicitDoesNotReadLegacy).IsNull();
        await Assert.That(exactWinsOverLegacy).IsEqualTo(CreatePrediction(homeGoals: 2, awayGoals: 1));
    }

    [Test]
    public async Task Legacy_bonus_rows_are_read_only_when_reasoning_effort_is_omitted_and_exact_rows_win()
    {
        var repository = CreateRepository();
        var modelDefaultConfig = PredictionModelConfig.Create("gpt-5-nano");
        var explicitHighConfig = PredictionModelConfig.Create("gpt-5-nano", "high");

        await InsertLegacyBonusPredictionAsync("Legacy question?", "gpt-5-nano", "test-community", new BonusPrediction(["opt-1"]), repredictionIndex: 0);
        await repository.SaveBonusPredictionAsync(CreateBonusQuestion(text: "Exact question?"), new BonusPrediction(["opt-2"]), modelDefaultConfig, "100", 0.01, "test-community", ["exact-doc"]);
        await InsertLegacyBonusPredictionAsync("Exact question?", "gpt-5-nano", "test-community", new BonusPrediction(["opt-3"]), repredictionIndex: 9);

        var omittedReadsLegacy = await repository.GetBonusPredictionByTextAsync("Legacy question?", modelDefaultConfig, "test-community");
        var explicitDoesNotReadLegacy = await repository.GetBonusPredictionByTextAsync("Legacy question?", explicitHighConfig, "test-community");
        var exactWinsOverLegacy = await repository.GetBonusPredictionByTextAsync("Exact question?", modelDefaultConfig, "test-community");

        await Assert.That(omittedReadsLegacy!.SelectedOptionIds).IsEquivalentTo(["opt-1"]);
        await Assert.That(explicitDoesNotReadLegacy).IsNull();
        await Assert.That(exactWinsOverLegacy!.SelectedOptionIds).IsEquivalentTo(["opt-2"]);
    }

    [Test]
    public async Task Cost_queries_are_isolated_by_reasoning_effort()
    {
        var repository = CreateRepository();
        var match = CreateMatch(homeTeam: "Team A", awayTeam: "Team B");
        var question = CreateBonusQuestion(text: "Who will win?");
        var minimalConfig = PredictionModelConfig.Create("gpt-5-nano", "minimal");
        var highConfig = PredictionModelConfig.Create("gpt-5-nano", "high");

        await repository.SavePredictionAsync(match, CreatePrediction(), minimalConfig, "100", 0.10, "test-community", []);
        await repository.SavePredictionAsync(match, CreatePrediction(), highConfig, "100", 0.90, "test-community", []);
        await repository.SaveBonusPredictionAsync(question, new BonusPrediction(["opt-1"]), minimalConfig, "100", 0.20, "test-community", []);
        await repository.SaveBonusPredictionAsync(question, new BonusPrediction(["opt-2"]), highConfig, "100", 0.80, "test-community", []);

        var minimalMatchCosts = await repository.GetMatchPredictionCostsByRepredictionIndexAsync(minimalConfig, "test-community");
        var highMatchCosts = await repository.GetMatchPredictionCostsByRepredictionIndexAsync(highConfig, "test-community");
        var minimalBonusCosts = await repository.GetBonusPredictionCostsByRepredictionIndexAsync(minimalConfig, "test-community");
        var highBonusCosts = await repository.GetBonusPredictionCostsByRepredictionIndexAsync(highConfig, "test-community");

        await Assert.That(minimalMatchCosts[0]).IsEqualTo((cost: 0.10, count: 1));
        await Assert.That(highMatchCosts[0]).IsEqualTo((cost: 0.90, count: 1));
        await Assert.That(minimalBonusCosts[0]).IsEqualTo((cost: 0.20, count: 1));
        await Assert.That(highBonusCosts[0]).IsEqualTo((cost: 0.80, count: 1));
    }

    private async Task InsertLegacyMatchPredictionAsync(
        Match match,
        string model,
        string communityContext,
        Prediction prediction,
        int repredictionIndex)
    {
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("match-predictions")
            .Document(Guid.NewGuid().ToString())
            .SetAsync(new Dictionary<string, object?>
            {
                ["homeTeam"] = match.HomeTeam,
                ["awayTeam"] = match.AwayTeam,
                ["startsAt"] = Timestamp.FromDateTime(match.StartsAt.ToInstant().ToDateTimeUtc()),
                ["matchday"] = match.Matchday,
                ["homeGoals"] = prediction.HomeGoals,
                ["awayGoals"] = prediction.AwayGoals,
                ["createdAt"] = now,
                ["updatedAt"] = now,
                ["competition"] = CompetitionIds.Bundesliga2025_26,
                ["model"] = model,
                ["tokenUsage"] = "legacy",
                ["cost"] = 0.01,
                ["communityContext"] = communityContext,
                ["contextDocumentNames"] = Array.Empty<string>(),
                ["repredictionIndex"] = repredictionIndex
            });
    }

    private async Task InsertLegacyBonusPredictionAsync(
        string questionText,
        string model,
        string communityContext,
        BonusPrediction prediction,
        int repredictionIndex)
    {
        var now = Timestamp.GetCurrentTimestamp();
        await Fixture.Db.Collection("bonus-predictions")
            .Document(Guid.NewGuid().ToString())
            .SetAsync(new Dictionary<string, object?>
            {
                ["questionText"] = questionText,
                ["selectedOptionIds"] = prediction.SelectedOptionIds.ToArray(),
                ["selectedOptionTexts"] = prediction.SelectedOptionIds.ToArray(),
                ["createdAt"] = now,
                ["updatedAt"] = now,
                ["competition"] = CompetitionIds.Bundesliga2025_26,
                ["model"] = model,
                ["tokenUsage"] = "legacy",
                ["cost"] = 0.01,
                ["communityContext"] = communityContext,
                ["contextDocumentNames"] = Array.Empty<string>(),
                ["repredictionIndex"] = repredictionIndex
            });
    }
}
