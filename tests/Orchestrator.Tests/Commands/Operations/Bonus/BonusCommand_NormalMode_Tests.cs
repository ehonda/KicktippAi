using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
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
        var prediction = CreateBonusPrediction();
        var context = CreateBonusCommandApp(
            existingBonusPrediction: Option.None<BonusPrediction>(),
            predictionResult: prediction);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        context.PredictionRepository.Verify(r => r.SaveBonusPredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            "test-model",
            It.IsAny<string>(),
            It.IsAny<double>(),
            "test",
            It.IsAny<IEnumerable<string>>(),
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
        await Assert.That(output).Contains("Using 2 KPI context documents");
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
        context.PredictionRepository.Verify(r => r.SaveBonusPredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(),
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPrediction?)null);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BonusPrediction?)null);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateBonusCommandApp(
            existingBonusPrediction: NullableOption.Some<BonusPrediction>(null),
            openAiServiceFactory: mockOpenAiFactory);

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
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
        context.PredictionRepository.Verify(r => r.SaveBonusPredictionAsync(
            It.IsAny<BonusQuestion>(),
            It.IsAny<BonusPrediction>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<double>(),
            It.IsAny<string>(),
            It.Is<IEnumerable<string>>(names => names.Any()), // Verify context document names are passed
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
