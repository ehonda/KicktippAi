using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchDetailedCommand_HappyPath_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Happy_path_with_single_run_returns_success()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Analyze match initialized with model:");
        await Assert.That(output).Contains(DefaultModel);
    }

    [Test]
    public async Task Happy_path_displays_community_context()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains(DefaultCommunityContext);
    }

    [Test]
    public async Task Single_run_displays_prediction_score()
    {
        var prediction = CreatePrediction(homeGoals: 3, awayGoals: 0);
        var context = CreateDetailedCommandApp(predictionResult: prediction);
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("3:0");
    }

    [Test]
    public async Task Multiple_runs_execute_all_predictions()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "3", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Run 1/3");
        await Assert.That(output).Contains("Run 2/3");
        await Assert.That(output).Contains("Run 3/3");
    }

    [Test]
    public async Task Calls_prediction_service_with_justification_enabled()
    {
        var context = CreateDetailedCommandApp();
        await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        context.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<EHonda.KicktippAi.Core.Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Test]
    public async Task Displays_total_run_count_after_completion()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "2", "--no-live-estimates");

        await Assert.That(output).Contains("Total runs with predictions:");
        await Assert.That(output).Contains("2/2");
    }

    [Test]
    public async Task Displays_total_cost_after_completion()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Total cost:");
    }

    [Test]
    public async Task Resets_token_tracker_before_runs()
    {
        var context = CreateDetailedCommandApp();
        await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        context.TokenUsageTracker.Verify(t => t.Reset(), Times.Once());
    }

    [Test]
    public async Task No_live_estimates_shows_summary_table()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Live Estimates");
    }

    [Test]
    public async Task Live_estimates_enabled_by_default_shows_summary()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Live Estimates");
    }

    [Test]
    public async Task Displays_cost_per_run()
    {
        var mockTracker = CreateMockTokenUsageTracker(lastCost: 0.05m);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(tokenUsageTracker: mockTracker);
        var context = CreateDetailedCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("$0.0500");
    }

    [Test]
    public async Task Justification_content_is_displayed_when_present()
    {
        var prediction = CreatePredictionWithJustification();
        var context = CreateDetailedCommandApp(predictionResult: prediction);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Bayern has strong home form");
    }

    [Test]
    public async Task No_justification_displays_fallback_message()
    {
        var prediction = CreatePrediction(homeGoals: 1, awayGoals: 0);
        var context = CreateDetailedCommandApp(predictionResult: prediction);
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("no explanation returned by model");
    }

    [Test]
    public async Task Loads_context_documents_and_lists_them()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(output).Contains("Loaded context documents:");
        await Assert.That(output).Contains("bundesliga-standings.csv");
    }

    [Test]
    public async Task Debug_flag_displays_debug_logging_message()
    {
        var context = CreateDetailedCommandApp();
        var (_, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates", "--debug");

        await Assert.That(output).Contains("Debug logging enabled");
    }
}
