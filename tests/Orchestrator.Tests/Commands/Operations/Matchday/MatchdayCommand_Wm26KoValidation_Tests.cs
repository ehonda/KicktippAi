using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

public class MatchdayCommand_Wm26KoValidation_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Invalid_stored_ko_draw_under_cap_triggers_reprediction()
    {
        var match = CreateWm26KnockoutMatch();
        var predictionRepo = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(homeGoals: 1, awayGoals: 1),
            getRepredictionIndexResult: 0);
        var kicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: match) });
        var predictionService = CreateMockPredictionService(
            predictMatchResult: CreatePrediction(homeGoals: 2, awayGoals: 1));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateWorldCupContextDocuments(match))),
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktippClient),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-4o",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Current prediction 1:1 is invalid");
        predictionService.Verify(
            service => service.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        predictionRepo.Verify(
            repository => repository.SaveRepredictionAsync(
                match,
                It.IsAny<Prediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                "test-community",
                It.IsAny<IEnumerable<string>>(),
                1,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Invalid_stored_ko_draw_at_cap_is_excluded_while_other_matches_are_still_submitted()
    {
        var blockedMatch = CreateWm26KnockoutMatch(homeTeam: "Germany", awayTeam: "Brazil");
        var validMatch = CreateWm26KnockoutMatch(homeTeam: "Spain", awayTeam: "Argentina");
        var invalidPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var validPrediction = CreatePrediction(homeGoals: 2, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository();
        predictionRepo
            .Setup(repository => repository.GetPredictionAsync(
                blockedMatch,
                It.IsAny<PredictionModelConfig>(),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidPrediction);
        predictionRepo
            .Setup(repository => repository.GetPredictionAsync(
                validMatch,
                It.IsAny<PredictionModelConfig>(),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(validPrediction);
        predictionRepo
            .Setup(repository => repository.GetMatchRepredictionIndexAsync(
                blockedMatch,
                It.IsAny<PredictionModelConfig>(),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(repository => repository.GetMatchRepredictionIndexAsync(
                validMatch,
                It.IsAny<PredictionModelConfig>(),
                "test-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var kicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory>
            {
                CreateMatchWithHistory(match: blockedMatch),
                CreateMatchWithHistory(match: validMatch)
            });

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateWorldCupContextDocuments(validMatch))),
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktippClient));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-4o",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--max-repredictions",
            "0");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Blocked matches: 1");
        kicktippClient.Verify(
            client => client.PlaceBetsAsync(
                "test-community",
                It.Is<Dictionary<Match, BetPrediction>>(bets =>
                    bets.Count == 1 &&
                    bets.ContainsKey(validMatch) &&
                    !bets.ContainsKey(blockedMatch) &&
                    bets[validMatch].HomeGoals == 2 &&
                    bets[validMatch].AwayGoals == 1),
                false),
            Times.Once);
    }

    [Test]
    public async Task Invalid_generated_ko_draw_in_repredict_mode_is_saved_and_not_submitted()
    {
        var match = CreateWm26KnockoutMatch();
        var predictionRepo = CreateMockPredictionRepository(getRepredictionIndexResult: -1);
        var kicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: match) });
        var predictionService = CreateMockPredictionService(
            predictMatchResult: CreatePrediction(homeGoals: 1, awayGoals: 1));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateWorldCupContextDocuments(match))),
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktippClient),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-4o",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--repredict");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("generated prediction 1:1 is invalid");
        predictionRepo.Verify(
            repository => repository.SaveRepredictionAsync(
                match,
                It.IsAny<Prediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                "test-community",
                It.IsAny<IEnumerable<string>>(),
                0,
                It.IsAny<CancellationToken>()),
            Times.Once);
        kicktippClient.Verify(
            client => client.PlaceBetsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<Match, BetPrediction>>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Test]
    public async Task Invalid_generated_ko_draw_in_override_database_mode_is_not_persisted_or_submitted()
    {
        var match = CreateWm26KnockoutMatch();
        var predictionRepo = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(homeGoals: 2, awayGoals: 1));
        var kicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: match) });
        var predictionService = CreateMockPredictionService(
            predictMatchResult: CreatePrediction(homeGoals: 1, awayGoals: 1));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateWorldCupContextDocuments(match))),
            kicktippClientFactory: CreateMockKicktippClientFactory(kicktippClient),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-4o",
            "-c",
            "test-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--override-database");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("generated prediction 1:1 is invalid");
        predictionRepo.Verify(
            repository => repository.SavePredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        kicktippClient.Verify(
            client => client.PlaceBetsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<Match, BetPrediction>>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    private static Match CreateWm26KnockoutMatch(
        string homeTeam = "Germany",
        string awayTeam = "Brazil",
        int matchday = 1)
    {
        return CreateMatch(homeTeam: homeTeam, awayTeam: awayTeam, matchday: matchday) with
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };
    }

    private static Dictionary<string, ContextDocument> CreateWorldCupContextDocuments(
        Match match,
        string communityContext = "test-community")
    {
        return MatchContextDocumentCatalog
            .ForMatch(match, communityContext, CompetitionIds.FifaWorldCup2026)
            .RequiredDocumentNames
            .ToDictionary(
                documentName => documentName,
                documentName => CreateContextDocument(
                    documentName: documentName,
                    content: $"{documentName} content"));
    }
}
