using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> special modes
/// (dry-run, verbose, agent, etc.).
/// </summary>
public class MatchdayCommand_Modes_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Running_command_in_dry_run_mode_does_not_save_to_database()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run");

        ctx.PredictionRepository.Verify(
            r => r.SavePredictionAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_in_dry_run_mode_does_not_place_bets_to_kicktipp()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run");

        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_in_dry_run_mode_shows_dry_run_message_at_bet_placement()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode");
        await Assert.That(output).Contains("would have placed");
    }

    [Test]
    public async Task Running_command_in_dry_run_verbose_mode_shows_skipped_database_save()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run - skipped database save");
    }

    [Test]
    public async Task Running_command_in_verbose_mode_shows_match_prompt_path()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Match prompt:");
    }

    [Test]
    public async Task Running_command_in_verbose_mode_shows_context_document_count()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context documents");
    }

    [Test]
    public async Task Running_command_in_verbose_mode_shows_individual_token_usage()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Token usage");
    }

    [Test]
    public async Task Running_command_in_verbose_mode_shows_database_save_confirmation()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved to database");
    }

    [Test]
    public async Task Running_command_in_agent_mode_generates_predictions_but_hides_details()
    {
        var predictionResult = CreatePrediction(homeGoals: 4, awayGoals: 2);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Generated prediction");
        await Assert.That(output).DoesNotContain("4:2");
    }

    [Test]
    public async Task Running_command_in_agent_mode_still_saves_predictions_to_database()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        ctx.PredictionRepository.Verify(
            r => r.SavePredictionAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_in_agent_mode_still_places_bets_to_kicktipp()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        ctx.KicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_show_context_documents_displays_document_names()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--show-context-documents");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Context documents for");
    }

    [Test]
    public async Task Running_command_with_justification_passes_flag_to_prediction_service()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_without_justification_passes_false_to_prediction_service()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_justification_displays_justification_when_available()
    {
        var justification = CreatePredictionJustification(keyReasoning: "Bayern is strong at home");
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1, justification: justification);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Bayern is strong at home");
    }

    [Test]
    public async Task Running_command_with_justification_shows_no_justification_message_when_unavailable()
    {
        var predictionResult = CreatePrediction(homeGoals: 2, awayGoals: 1, justification: (PredictionJustification?)null);
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, predictionResult: predictionResult);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No justification available");
    }
}
