using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> error handling scenarios.
/// </summary>
public class MatchdayCommand_ErrorHandling_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Running_command_handles_kicktipp_client_exception()
    {
        var mockKicktippClient = new Mock<IKicktippClient>();
        mockKicktippClient
            .Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var ctx = CreateMatchdayCommandApp(kicktippClientFactory: CreateMockKicktippClientFactory(mockKicktippClient));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Network error");
    }

    [Test]
    public async Task Running_command_handles_place_bets_failure_gracefully()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null, placeBetsResult: false);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to place");
    }

    [Test]
    public async Task Running_command_handles_prediction_service_exception_for_single_match()
    {
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService
            .Setup(s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API error"));
        mockPredictionService
            .Setup(s => s.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/test.md");

        var ctx = CreateMatchdayCommandApp(openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: mockPredictionService));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error processing match:");
        await Assert.That(output).Contains("API error");
        await Assert.That(output).Contains("match_processing_error");
    }

    [Test]
    public async Task Running_command_continues_processing_other_matches_after_single_match_error()
    {
        var callCount = 0;
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService
            .Setup(s => s.PredictMatchAsync(It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First match failed");
                return Task.FromResult<Prediction?>(CreatePrediction());
            });
        mockPredictionService
            .Setup(s => s.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/test.md");

        var matches = new List<MatchWithHistory>
        {
            CreateBayernVsDortmundMatchWithHistory(matchday: 25),
            CreateBayernVsDortmundMatchWithHistory(matchday: 26)
        };
        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: matches,
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: mockPredictionService));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error processing match:");
        await Assert.That(output).Contains("First match failed");
        await Assert.That(output).Contains("Successfully placed all 1 predictions");
        await Assert.That(output).Contains("Matchday completed with blocked matches");
    }

    [Test]
    public async Task Running_command_handles_database_save_error_gracefully()
    {
        var mockPredictionRepo = CreateMockPredictionRepository();
        mockPredictionRepo.As<IResolvedMatchContextPredictionRepository>()
            .Setup(r => r.SavePredictionWithResolvedContextAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<ResolvedMatchContextManifest>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: mockPredictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments())),
            kicktippClientFactory: CreateMockKicktippClientFactory(mockKicktippClient));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Failed to save to database");
        mockKicktippClient.Verify(
            c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_fails_closed_when_context_repository_errors()
    {
        var mockContextRepo = new Mock<IContextRepository>();
        mockContextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Context fetch failed"));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Context fetch failed");
    }

    [Test]
    public async Task Running_command_passes_model_name_to_prediction_service()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "custom-model-name", "-c", "test-community");

        ctx.OpenAiServiceFactory.Verify(f => f.CreatePredictionService(
            "custom-model-name",
            It.IsAny<PredictionServiceOptions>(),
            It.IsAny<IInstructionsTemplateProvider>()), Times.Once);
    }
}
