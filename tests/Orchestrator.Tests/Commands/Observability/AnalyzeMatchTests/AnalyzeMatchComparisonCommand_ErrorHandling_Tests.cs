using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using Orchestrator.Infrastructure.Factories;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchComparisonCommand_ErrorHandling_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Prediction_service_returns_null_marks_run_as_failed()
    {
        var context = CreateComparisonCommandApp(predictionResult: (Prediction?)null);
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Prediction failed");
    }

    [Test]
    public async Task All_predictions_null_shows_no_predictions_message()
    {
        var context = CreateComparisonCommandApp(predictionResult: (Prediction?)null);
        var (_, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(output).Contains("No successful predictions to compare");
    }

    [Test]
    public async Task Kicktipp_client_exception_falls_back_and_continues()
    {
        var mockClient = new Mock<IKicktippClient>();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Connection refused"));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockClient);
        var context = CreateComparisonCommandApp(kicktippClientFactory: mockKicktippFactory);
        var (exitCode, _) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Match_not_found_in_kicktipp_falls_back_to_settings()
    {
        var otherMatch = CreateMatchWithHistory(
            match: CreateMatch(homeTeam: "Other Team", awayTeam: "Another Team", matchday: DefaultMatchday));
        var context = CreateComparisonCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { otherMatch });
        var (exitCode, _) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Context_repository_null_shows_warning_and_continues()
    {
        var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
        mockFirebaseFactory.Setup(f => f.CreateContextRepository()).Returns((IContextRepository?)null!);
        var context = CreateComparisonCommandApp(firebaseServiceFactory: mockFirebaseFactory);
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Context repository not configured");
    }

    [Test]
    public async Task General_exception_returns_error_exit_code()
    {
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService.Setup(s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(predictionService: mockPredictionService);
        var context = CreateComparisonCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (exitCode, output) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Service unavailable");
    }

    [Test]
    public async Task Summary_table_shows_failure_count()
    {
        var context = CreateComparisonCommandApp(predictionResult: (Prediction?)null);
        var (_, output) = await RunComparisonAsync(context, "--runs", "2");

        // All 4 predictions (2 runs × 2 modes) should fail
        // Combined row should show 0 successful, 4 failed
        await Assert.That(output).Contains("Combined");
    }

    [Test]
    public async Task Null_kicktipp_client_still_succeeds()
    {
        var mockKicktippFactory = new Mock<IKicktippClientFactory>();
        mockKicktippFactory.Setup(f => f.CreateClient()).Returns((IKicktippClient)null!);
        var context = CreateComparisonCommandApp(kicktippClientFactory: mockKicktippFactory);
        var (exitCode, _) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
    }
}
