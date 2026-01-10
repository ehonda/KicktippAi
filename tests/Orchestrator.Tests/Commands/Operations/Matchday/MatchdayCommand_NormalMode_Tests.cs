using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> normal mode workflow.
/// </summary>
public class MatchdayCommand_NormalMode_Tests : MatchdayCommandTests_Base
{
    #region Empty Matches Tests

    [Test]
    public async Task Running_command_with_no_matches_shows_no_matches_message()
    {
        // Arrange
        var mocks = CreateStandardMocks(matchesWithHistory: new List<MatchWithHistory>());
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
    }

    [Test]
    public async Task Running_command_with_no_matches_does_not_call_prediction_service()
    {
        // Arrange
        var mocks = CreateStandardMocks(matchesWithHistory: new List<MatchWithHistory>());
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Match Found Display Tests

    [Test]
    public async Task Running_command_with_matches_shows_match_count()
    {
        // Arrange
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory(match: CreateMatch(homeTeam: "RB Leipzig", awayTeam: "VfB Stuttgart"))
        };
        var mocks = CreateStandardMocks(matchesWithHistory: matches);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 matches");
    }

    [Test]
    public async Task Running_command_with_matches_displays_match_processing()
    {
        // Arrange
        var mocks = CreateStandardMocks();
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing:");
        await Assert.That(output).Contains("FC Bayern München");
        await Assert.That(output).Contains("Borussia Dortmund");
    }

    #endregion

    #region Database Cache Hit Tests

    [Test]
    public async Task Running_command_uses_cached_prediction_when_available()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("3:0");
        await Assert.That(output).Contains("from database");
    }

    [Test]
    public async Task Running_command_with_cached_prediction_does_not_call_prediction_service()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_in_agent_mode_with_cached_prediction_hides_score()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).DoesNotContain("3:0");
    }

    #endregion

    #region New Prediction Tests

    [Test]
    public async Task Running_command_generates_new_prediction_when_no_cached_prediction_exists()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        await Assert.That(output).Contains("Generated prediction");
    }

    [Test]
    public async Task Running_command_calls_prediction_service_when_no_cached_prediction_exists()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_saves_new_prediction_to_database()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        mocks.PredictionRepository.Verify(
            r => r.SavePredictionAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_displays_generated_prediction_score()
    {
        // Arrange
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, predictionResult: predictionResult);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction:");
        await Assert.That(output).Contains("2:1");
    }

    [Test]
    public async Task Running_command_in_agent_mode_hides_generated_prediction_score()
    {
        // Arrange
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, predictionResult: predictionResult);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction");
        await Assert.That(output).DoesNotContain("2:1");
    }

    #endregion

    #region Override Database Tests

    [Test]
    public async Task Running_command_with_override_database_generates_new_prediction_even_when_cached_exists()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var newPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var mocks = CreateStandardMocks(existingPrediction: existingPrediction, predictionResult: newPrediction);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-database");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        mocks.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Bet Placement Tests

    [Test]
    public async Task Running_command_places_bets_to_kicktipp()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Placing");
        await Assert.That(output).Contains("predictions to Kicktipp");
        mocks.KicktippClient.Verify(
            c => c.PlaceBetsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<EHonda.KicktippAi.Core.Match, BetPrediction>>(),
                It.IsAny<bool>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_shows_success_when_bets_placed_successfully()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, placeBetsResult: true);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Successfully placed all");
    }

    [Test]
    public async Task Running_command_shows_failure_when_bets_fail_to_place()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, placeBetsResult: false);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to place");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_passes_override_flag_to_kicktipp_client()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--override-kicktipp");

        // Assert
        mocks.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<EHonda.KicktippAi.Core.Match, BetPrediction>>(), true),
            Times.Once);
    }

    #endregion

    #region Token Usage Display Tests

    [Test]
    public async Task Running_command_displays_token_usage_summary()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }

    #endregion

    #region Prediction Failed Tests

    [Test]
    public async Task Running_command_shows_failure_when_prediction_service_returns_null()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to generate prediction");
    }

    [Test]
    public async Task Running_command_with_all_predictions_failed_shows_no_predictions_message()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);
        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No predictions available");
    }

    #endregion
}
