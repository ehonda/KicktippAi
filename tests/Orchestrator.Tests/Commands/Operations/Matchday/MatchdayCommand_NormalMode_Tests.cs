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
    [Test]
    public async Task Running_command_with_no_matches_shows_no_matches_message()
    {
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
    }

    [Test]
    public async Task Running_command_with_no_matches_does_not_call_prediction_service()
    {
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_with_matches_shows_match_count()
    {
        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(),
            CreateMatchWithHistory(match: CreateMatch(homeTeam: "RB Leipzig", awayTeam: "VfB Stuttgart"))
        };
        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matches);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found 2 matches");
    }

    [Test]
    public async Task Running_command_with_matches_displays_match_processing()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Processing:");
        await Assert.That(output).Contains("FC Bayern München");
        await Assert.That(output).Contains("Borussia Dortmund");
    }

    [Test]
    public async Task Running_command_uses_cached_prediction_when_available()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).Contains("3:0");
        await Assert.That(output).Contains("from database");
    }

    [Test]
    public async Task Running_command_with_cached_prediction_does_not_call_prediction_service()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_in_agent_mode_with_cached_prediction_hides_score()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Found existing prediction");
        await Assert.That(output).DoesNotContain("3:0");
    }

    [Test]
    public async Task Running_command_generates_new_prediction_when_no_cached_prediction_exists()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        await Assert.That(output).Contains("Generated prediction");
    }

    [Test]
    public async Task Running_command_calls_prediction_service_when_no_cached_prediction_exists()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_saves_new_prediction_to_database()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionRepository.Verify(
            r => r.SavePredictionAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_displays_generated_prediction_score()
    {
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction:");
        await Assert.That(output).Contains("2:1");
    }

    [Test]
    public async Task Running_command_in_agent_mode_hides_generated_prediction_score()
    {
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction");
        await Assert.That(output).DoesNotContain("2:1");
    }

    [Test]
    public async Task Running_command_with_override_database_generates_new_prediction_even_when_cached_exists()
    {
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var newPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);
        var ctx = CreateMatchdayCommandApp(existingPrediction: existingPrediction, predictionResult: newPrediction);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-database");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generating new prediction");
        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_places_bets_to_kicktipp()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Placing");
        await Assert.That(output).Contains("predictions to Kicktipp");
        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_shows_success_when_bets_placed_successfully()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, placeBetsResult: true);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Successfully placed all");
    }

    [Test]
    public async Task Running_command_shows_failure_when_bets_fail_to_place()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, placeBetsResult: false);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to place");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_passes_override_flag_to_kicktipp_client()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-kicktipp");

        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), true),
            Times.Once);
    }

    [Test]
    public async Task Running_command_displays_token_usage_summary()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }

    [Test]
    public async Task Running_command_shows_failure_when_prediction_service_returns_null()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to generate prediction");
    }

    [Test]
    public async Task Running_command_with_all_predictions_failed_shows_no_predictions_message()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No predictions available");
    }
}
