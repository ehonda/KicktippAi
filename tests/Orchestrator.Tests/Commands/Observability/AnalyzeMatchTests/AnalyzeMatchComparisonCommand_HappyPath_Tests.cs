using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchComparisonCommand_HappyPath_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Happy_path_with_single_run_returns_success()
    {
        var context = CreateComparisonCommandApp();
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Analyze match comparison initialized with model:");
        await Assert.That(output).Contains(DefaultModel);
    }

    [Test]
    public async Task Displays_community_context()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains(DefaultCommunityContext);
    }

    [Test]
    public async Task Single_run_executes_two_predictions()
    {
        var context = CreateComparisonCommandApp();
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
        // Should show both modes
        await Assert.That(output).Contains("with justification");
        await Assert.That(output).Contains("without justification");
    }

    [Test]
    public async Task Calls_prediction_service_with_and_without_justification()
    {
        var context = CreateComparisonCommandApp();
        await RunComparisonAsync(context, "--runs", "1");

        context.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                true,
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Once());

        context.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                false,
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Test]
    public async Task Multiple_runs_execute_correctly()
    {
        var context = CreateComparisonCommandApp();
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "3");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Run 1/3");
        await Assert.That(output).Contains("Run 2/3");
        await Assert.That(output).Contains("Run 3/3");
    }

    [Test]
    public async Task Multiple_runs_call_prediction_service_correct_number_of_times()
    {
        var context = CreateComparisonCommandApp();
        await RunComparisonAsync(context, "--runs", "3");

        // 3 runs × 2 modes = 6 total predictions
        context.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(6));
    }

    [Test]
    public async Task Resets_token_tracker_before_runs()
    {
        var context = CreateComparisonCommandApp();
        await RunComparisonAsync(context, "--runs", "1");

        context.TokenUsageTracker.Verify(t => t.Reset(), Times.Once());
    }

    [Test]
    public async Task Prediction_scores_are_displayed()
    {
        var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var context = CreateComparisonCommandApp(predictionResult: prediction);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("3:0");
    }

    [Test]
    public async Task Debug_flag_displays_debug_logging_message()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1", "--debug");

        await Assert.That(output).Contains("Debug logging enabled");
    }

    [Test]
    public async Task Loads_context_documents()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("Loaded context documents:");
        await Assert.That(output).Contains("bundesliga-standings.csv");
    }

    [Test]
    public async Task Runs_per_mode_count_is_displayed()
    {
        var context = CreateComparisonCommandApp();
        var (_, output) = await RunComparisonAsync(context, "--runs", "5");

        await Assert.That(output).Contains("Runs per mode:");
        await Assert.That(output).Contains("5");
    }
}
