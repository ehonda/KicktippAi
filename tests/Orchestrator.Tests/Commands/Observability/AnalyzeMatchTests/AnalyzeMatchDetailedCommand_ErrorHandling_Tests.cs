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

public class AnalyzeMatchDetailedCommand_ErrorHandling_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Prediction_service_returns_null_marks_run_as_failed()
    {
        var context = CreateDetailedCommandApp(predictionResult: (Prediction?)null);
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Prediction failed");
    }

    [Test]
    public async Task All_predictions_null_shows_no_successful_message()
    {
        var context = CreateDetailedCommandApp(predictionResult: (Prediction?)null);
        var (_, output) = await RunDetailedAsync(context, "--runs", "2", "--no-live-estimates");

        await Assert.That(output).Contains("No successful predictions generated");
    }

    [Test]
    public async Task Kicktipp_client_exception_falls_back_to_provided_details()
    {
        var mockClient = new Mock<IKicktippClient>();
        mockClient.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Connection refused"));
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockClient);
        var context = CreateDetailedCommandApp(kicktippClientFactory: mockKicktippFactory);
        var (exitCode, _) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        // Command should still succeed with fallback match details
        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Match_not_found_in_kicktipp_falls_back_to_settings()
    {
        // Return matches that don't match the requested teams
        var otherMatch = CreateMatchWithHistory(
            match: CreateMatch(homeTeam: "Other Team", awayTeam: "Another Team", matchday: DefaultMatchday));
        var context = CreateDetailedCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { otherMatch });
        var (exitCode, _) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Context_repository_null_shows_warning_and_continues()
    {
        var mockFirebaseFactory = new Mock<IFirebaseServiceFactory>();
        mockFirebaseFactory.Setup(f => f.CreateContextRepository()).Returns((IContextRepository?)null!);
        var context = CreateDetailedCommandApp(firebaseServiceFactory: mockFirebaseFactory);
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

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
        var context = CreateDetailedCommandApp(openAiServiceFactory: mockOpenAiFactory);
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Service unavailable");
    }

    [Test]
    public async Task Verbose_shows_missing_required_documents()
    {
        // Empty context docs means all required documents are missing
        var context = CreateDetailedCommandApp(
            contextDocuments: new Dictionary<string, ContextDocument>());
        var (_, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(output).Contains("Missing");
    }

    [Test]
    public async Task Optional_document_exception_handled_gracefully_in_verbose()
    {
        // Set up a context repo that throws for transfer documents but returns others
        var mockContextRepo = new Mock<IContextRepository>();
        var docs = CreateDefaultContextDocuments();

        mockContextRepo.Setup(r => r.GetLatestContextDocumentAsync(
                It.Is<string>(name => !name.Contains("transfers")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                docs.TryGetValue(docName, out var doc) ? doc : null);

        mockContextRepo.Setup(r => r.GetLatestContextDocumentAsync(
                It.Is<string>(name => name.Contains("transfers")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Transfer doc error"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo);
        var context = CreateDetailedCommandApp(firebaseServiceFactory: mockFirebaseFactory);
        var (exitCode, output) = await RunDetailedAsync(
            context, "--runs", "1", "--no-live-estimates", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed optional");
        await Assert.That(output).Contains("Transfer doc error");
    }

    [Test]
    public async Task Null_kicktipp_client_still_succeeds()
    {
        var mockKicktippFactory = new Mock<IKicktippClientFactory>();
        mockKicktippFactory.Setup(f => f.CreateClient()).Returns((IKicktippClient?)null);
        var context = CreateDetailedCommandApp(kicktippClientFactory: mockKicktippFactory);
        var (exitCode, _) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
    }
}
