using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using Orchestrator.Infrastructure;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> normal mode workflow.
/// </summary>
public class BonusCommand_NormalMode_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Running_command_with_no_open_questions_returns_early()
    {
        // Arrange
        var context = CreateBonusCommandApp(openBonusQuestions: new List<BonusQuestion>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No open bonus questions found");
    }

    [Test]
    public async Task Running_command_displays_found_questions_count()
    {
        // Arrange
        var questions = new List<BonusQuestion>
        {
            CreateLeagueWinnerBonusQuestion(),
            CreateTrainerChangeBonusQuestion()
        };
        var context = CreateBonusCommandApp(openBonusQuestions: questions);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 open bonus questions");
    }

    [Test]
    public async Task Running_command_uses_existing_prediction_from_database()
    {
        // Arrange
        var existingPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var context = CreateBonusCommandApp(existingBonusPrediction: existingPrediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("from database");
        await Assert.That(output).Contains("FC Bayern München");
    }

    [Test]
    public async Task Bundesliga_accepts_separately_materialized_cached_value_and_metadata_with_exact_content()
    {
        var cachedPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var metadataPrediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
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

        await Assert.That(exitCode).IsEqualTo(0);
        context.KicktippClient.Verify(client => client.PlaceBonusPredictionsAsync(
            "test",
            It.Is<Dictionary<string, BonusPrediction>>(predictions =>
                BonusPredictionContentEquality.Equals(predictions["bonus_q1"], cachedPrediction)),
            false), Times.Once);
    }

    [Test]
    public async Task Running_command_generates_new_prediction_when_none_exists()
    {
        // Arrange
        var prediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bvb" });
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            predictionResult: prediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        await Assert.That(output).Contains("Generated prediction:");
        await Assert.That(output).Contains("Borussia Dortmund");
    }

    [Test]
    public async Task Running_command_saves_prediction_to_database()
    {
        // Arrange
        var prediction = CreateBonusPrediction(selectedOptionIds: new List<string> { "bayern" });
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            predictionResult: prediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(r =>
            r.SaveBonusPredictionWithResolvedContextAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.Is<PredictionModelConfig>(config =>
                config.Model == "test-model" &&
                config.ReasoningEffort == null),
            It.IsAny<string>(),
            It.IsAny<double>(),
            "test",
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<ResolvedBonusContextManifest>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Bundesliga_validation_run_saves_exact_bonus_runtime_identity()
    {
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>());

        var exitCode = await context.App.RunAsync([
            "bonus",
            "gpt-5.6-luna",
            "--community",
            "test",
            "--competition",
            CompetitionIds.Bundesliga2026_27,
            "--reasoning-effort",
            "none",
            "--max-output-tokens",
            "10000"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(repository =>
            repository.SaveBonusPredictionWithResolvedContextAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.Is<PredictionModelConfig>(config =>
                config.Model == "gpt-5.6-luna" &&
                config.ReasoningEffort == "none" &&
                config.MaxOutputTokenCount == 10_000 &&
                config.PromptName == CompetitionResolver.BundesligaBonusPromptName &&
                config.PromptVersion == CompetitionResolver.BundesligaBonusPromptVersion),
            It.IsAny<string>(),
            It.IsAny<double>(),
            "test",
            It.IsAny<IEnumerable<string>>(),
            It.Is<ResolvedBonusContextManifest>(manifest =>
                manifest.Documents.Select(document => document.Name).SequenceEqual(
                new[] { "club-elo-rankings", "team-squad-summary" })),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_places_predictions_to_kicktipp()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Placing");
        await Assert.That(output).Contains("bonus predictions to Kicktipp");
        await Assert.That(output).Contains("Successfully placed");
        context.KicktippClient.Verify(c => c.PlaceBonusPredictionsAsync(
            "test",
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            false), Times.Once);
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_passes_override_flag()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-kicktipp"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.KicktippClient.Verify(c => c.PlaceBonusPredictionsAsync(
            "test",
            It.IsAny<Dictionary<string, BonusPrediction>>(),
            true), Times.Once);
    }

    [Test]
    public async Task Running_command_reports_placement_failure()
    {
        // Arrange
        var context = CreateBonusCommandApp(placeBonusPredictionsResult: false);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to place");
    }

    [Test]
    public async Task Running_command_displays_token_usage_summary()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_kpi_context_count()
    {
        // Arrange
        var kpiDocs = CreateBonusQuestionKpiDocuments();
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            kpiContextDocuments: kpiDocs);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using 2 bonus context documents");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_individual_token_usage()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage:");
    }

    [Test]
    public async Task Running_command_with_override_database_saves_with_override_flag()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-database"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(r =>
            r.SaveBonusPredictionWithResolvedContextAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<PredictionModelConfig>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
            It.IsAny<ResolvedBonusContextManifest>(),
            true, // overrideCreatedAt should be true
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_processes_multiple_questions()
    {
        // Arrange
        var questions = new List<BonusQuestion>
        {
            CreateLeagueWinnerBonusQuestion(formFieldName: "q1"),
            CreateTrainerChangeBonusQuestion(formFieldName: "q2")
        };
        var context = CreateBonusCommandApp(
            openBonusQuestions: questions,
            existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Who will win the league?");
        await Assert.That(output).Contains("Trainerwechsel");
        await Assert.That(output).Contains("Placing 2 bonus predictions");
    }

    [Test]
    public async Task Running_command_shows_database_enabled_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Database enabled");
    }

    [Test]
    public async Task Running_command_with_no_predictions_available_returns_early()
    {
        // Arrange - All predictions fail (return null)
        var mockPredictionService = CreateMockPredictionService();
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPrediction?)null);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync([
            "bonus", "test-model", "--community", "test",
            "--competition", CompetitionIds.Bundesliga2025_26
        ]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No predictions available");
    }

    [Test]
    public async Task Running_command_shows_failed_prediction_message()
    {
        // Arrange - Prediction service returns null
        var mockPredictionService = CreateMockPredictionService();
        mockPredictionService.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPrediction?)null);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync([
            "bonus", "test-model", "--community", "test",
            "--competition", CompetitionIds.Bundesliga2025_26
        ]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to generate prediction");
    }

    [Test]
    public async Task Running_command_with_verbose_and_database_save_shows_saved_message()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved to database");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_ready_for_placement_message()
    {
        // Arrange
        var context = CreateBonusCommandApp(existingBonusPrediction: Option.None<BonusPrediction>());

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Ready for Kicktipp placement");
    }

    [Test]
    public async Task Running_command_saves_context_document_names_to_database()
    {
        // Arrange - provide KPI context documents
        var kpiDocs = CreateBonusQuestionKpiDocuments();
        var context = CreateBonusCommandApp(
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null),
            kpiContextDocuments: kpiDocs);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.As<IResolvedBonusContextPredictionRepository>().Verify(r =>
            r.SaveBonusPredictionWithResolvedContextAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<PredictionModelConfig>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<string>>(names => names.Any()), // Verify context document names are passed
            It.IsAny<ResolvedBonusContextManifest>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Running_command_passes_the_complete_question_to_context_selection()
    {
        var question = CreateLeagueWinnerBonusQuestion();
        var context = CreateBonusCommandApp(
            openBonusQuestions: new List<BonusQuestion> { question },
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null));

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        await Assert.That(exitCode).IsEqualTo(0);
        context.KpiContextProvider.As<IResolvedBonusContextProvider>().Verify(provider =>
            provider.ResolveBonusQuestionContextAsync(
            question,
            "test",
            It.IsAny<CancellationToken>(),
            It.Is<BonusContextBudget>(budget =>
                budget.MaximumDocuments == BonusContextBudget.DefaultMaximumDocuments
                && budget.MaximumEstimatedTokens == BonusContextBudget.DefaultMaximumEstimatedTokens)), Times.Once);
        context.KpiContextProvider.Verify(provider => provider.GetBonusQuestionContextAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
