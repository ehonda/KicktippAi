using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> error handling scenarios.
/// </summary>
public class BonusCommand_ErrorHandling_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Bundesliga_command_requires_a_resolved_bonus_context_provider()
    {
        var legacyProvider = new Mock<IKpiContextProvider>();
        var context = CreateBonusCommandApp(
            contextProviderFactory: CreateMockContextProviderFactory(kpiContextProvider: legacyProvider));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("requires a resolved bonus-context provider");
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_command_requires_a_provenance_capable_prediction_repository()
    {
        var legacyRepository = new Mock<IPredictionRepository>();
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: legacyRepository));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("provenance-capable prediction");
        context.PredictionService.Verify(service => service.PredictBonusQuestionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_normal_mode_does_not_place_a_legacy_prediction_without_provenance()
    {
        var legacyPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var repository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: legacyPrediction,
            getBonusPredictionMetadataByTextResult: (BonusPredictionMetadata?)null);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: repository));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("lacks current").And.Contains("immutable provenance");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_normal_mode_fails_closed_when_cached_value_and_metadata_disagree()
    {
        var cachedPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var metadataPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bvb" });
        var metadata = CreateCanonicalBundesligaBonusPredictionMetadata(
            CreateLeagueWinnerBonusQuestion(),
            metadataPrediction,
            communityContext: "test");
        var repository = CreateMockPredictionRepository(
            getBonusPredictionByTextResult: cachedPrediction,
            getBonusPredictionMetadataByTextResult: metadata);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: repository));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("immutable provenance metadata").And.Contains("same cached value");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_context_provenance_failure_returns_nonzero_and_places_nothing()
    {
        var provider = CreateMockKpiContextProvider();
        provider.As<IResolvedBonusContextProvider>()
            .Setup(candidate => candidate.ResolveBonusQuestionContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("publication head is corrupt"));
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            contextProviderFactory: CreateMockContextProviderFactory(kpiContextProvider: provider));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("Failed to resolve immutable Bundesliga bonus provenance");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Bundesliga_later_question_provenance_failure_blocks_placement_of_all_selected_answers()
    {
        var firstQuestion = CreateLeagueWinnerBonusQuestion(formFieldName: "q1");
        var secondQuestion = CreateTrainerChangeBonusQuestion(formFieldName: "q2");
        var provider = CreateMockKpiContextProvider();
        var calls = 0;
        provider.As<IResolvedBonusContextProvider>()
            .Setup(candidate => candidate.ResolveBonusQuestionContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusQuestion question, string communityContext, CancellationToken _) =>
            {
                calls++;
                return calls == 1
                    ? CreateCanonicalBundesligaResolvedBonusContext(question, communityContext)
                    : throw new InvalidOperationException("second publication head is corrupt");
            });
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { firstQuestion, secondQuestion },
            existingBonusPrediction: Option.None<BonusPrediction>(),
            contextProviderFactory: CreateMockContextProviderFactory(kpiContextProvider: provider));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(calls).IsEqualTo(2);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Running_command_handles_kicktipp_client_exception()
    {
        // Arrange
        var mockKicktippClient = CreateMockKicktippClient();
        mockKicktippClient.Setup(c => c.GetOpenBonusQuestionsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var context = CreateBonusCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Connection failed");
    }

    [Test]
    public async Task Running_world_cup_command_fails_before_prediction_when_fifa_rankings_kpi_is_missing()
    {
        var context = CreateBonusCommandApp(kpiContextDocuments: new List<DocumentContext>());

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "ehonda-dev-wm26"]);
        var output = context.Console.Output;

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Missing required WM26 KPI context document");
        context.PredictionService.Verify(
            service => service.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_handles_prediction_service_exception()
    {
        // Arrange
        var mockPredictionService = CreateMockPredictionService();
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));

        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0); // Should continue processing
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("API error");
    }

    [Test]
    public async Task Running_command_continues_processing_after_individual_question_error()
    {
        // Arrange
        var questions = new List<BonusQuestion>
        {
            CreateLeagueWinnerBonusQuestion(formFieldName: "q1"),
            CreateTrainerChangeBonusQuestion(formFieldName: "q2")
        };

        var mockPredictionService = CreateMockPredictionService();
        var callCount = 0;
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First question error");
                return CreateBonusPrediction();
            });

        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            openBonusQuestions: questions,
            existingBonusPrediction: Option.None<BonusPrediction>(),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("Placing 1 bonus predictions"); // Only second question succeeded
    }

    [Test]
    public async Task Bundesliga_database_save_failure_does_not_place_the_unsaved_prediction()
    {
        // Arrange
        var mockPredictionRepository = CreateMockPredictionRepository();
        mockPredictionRepository.As<IResolvedBonusContextPredictionRepository>().Setup(r =>
            r.SaveBonusPredictionWithResolvedContextAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<ResolvedBonusContextManifest>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database write failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            firebaseServiceFactory: mockFirebaseFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to persist immutable Bundesliga bonus provenance");
        await Assert.That(output).Contains("Database write failed");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task Running_command_handles_placement_exception()
    {
        // Arrange - need to set up bonus questions so we reach the placement step
        var mockKicktippClient = CreateMockKicktippClient(
            openBonusQuestions: new List<BonusQuestion> { CreateLeagueWinnerBonusQuestion() });
        mockKicktippClient.Setup(c => c.PlaceBonusPredictionsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, BonusPrediction>>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("Placement failed"));

        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var context = CreateBonusCommandApp(kicktippClientFactory: mockKicktippFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Placement failed");
    }

    [Test]
    public async Task World_cup_command_continues_after_database_lookup_exception()
    {
        // Arrange
        var mockPredictionRepository = CreateMockPredictionRepository();
        mockPredictionRepository.Setup(r => r.GetBonusPredictionByTextAsync(
                It.IsAny<string>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database read failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(predictionRepository: mockPredictionRepository);
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kpiContextDocuments: new List<DocumentContext>
            {
                new("fifa-rankings", "Rank,Team,ELO\r\n1,Test,1000\r\n")
            });

        // Act
        var exitCode = await context.App.RunAsync([
            "bonus",
            "test-model",
            "--community",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Error processing question");
        await Assert.That(output).Contains("Database read failed");
    }

    [Test]
    public async Task Bundesliga_database_lookup_failure_returns_nonzero_and_places_nothing()
    {
        var repository = CreateMockPredictionRepository();
        repository.Setup(candidate => candidate.GetBonusPredictionByTextAsync(
                It.IsAny<string>(),
                It.IsAny<PredictionModelConfig>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database read failed"));
        var context = CreateBonusCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: repository));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(context.Console.Output).Contains("Failed to read a coherent cached Bundesliga bonus prediction");
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            It.IsAny<bool>()), Times.Never);
    }
}
