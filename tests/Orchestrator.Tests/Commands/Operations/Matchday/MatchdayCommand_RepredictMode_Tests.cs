using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> reprediction mode workflow.
/// </summary>
public class MatchdayCommand_RepredictMode_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Running_command_with_repredict_creates_first_prediction_when_none_exists()
    {
        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: (Prediction?)null);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No existing prediction found");
        await Assert.That(output).Contains("creating first prediction");
    }

    [Test]
    public async Task Running_command_with_repredict_saves_as_reprediction_when_outdated()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["bundesliga-standings.csv", "recent-history-fcb.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction");
        await Assert.That(output).Contains("outdated");
    }

    [Test]
    public async Task Running_command_with_repredict_skips_when_prediction_is_up_to_date()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["bundesliga-standings.csv", "recent-history-fcb.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped reprediction");
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Running_command_with_repredict_shows_latest_prediction_when_skipped()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 2);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["bundesliga-standings.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("3:2");
        await Assert.That(output).Contains("reprediction 0");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_skips_when_at_limit()
    {
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "2");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped");
        await Assert.That(output).Contains("already at max repredictions");
        await Assert.That(output).Contains("2/2");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_allows_reprediction_when_under_limit()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["recent-history-fcb.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "3");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction 2");
        await Assert.That(output).Contains("current: 1");
        await Assert.That(output).Contains("max: 3");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_zero_allows_only_first_prediction()
    {
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "0");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("already at max repredictions");
        await Assert.That(output).Contains("0/0");
    }

    [Test]
    public async Task Running_command_with_repredict_calls_save_reprediction_async()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["recent-history-fcb.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        predictionRepo.Verify(
            r => r.SaveRepredictionAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_repredict_verbose_shows_reprediction_index_saved()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, predictionTimestamp, ["recent-history-fcb.csv"]));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved as reprediction 1");
    }
}
