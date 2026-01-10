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
    #region Reprediction Index Tests

    [Test]
    public async Task Running_command_with_repredict_creates_first_prediction_when_none_exists()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);
        // Setup reprediction index to return -1 (no existing prediction)
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No existing prediction found");
        await Assert.That(output).Contains("creating first prediction");
    }

    [Test]
    public async Task Running_command_with_repredict_saves_as_reprediction_when_outdated()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        // Setup reprediction index to return 0 (one existing prediction)
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Setup prediction metadata with older timestamp
        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["bundesliga-standings.csv", "recent-history-fcb.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Setup context repository to return documents with newer timestamp
        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        // Need to override the mock to return newer context documents
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction");
        await Assert.That(output).Contains("outdated");
    }

    [Test]
    public async Task Running_command_with_repredict_skips_when_prediction_is_up_to_date()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        // Setup reprediction index to return 0
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Setup prediction metadata with newer timestamp than context docs
        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["bundesliga-standings.csv", "recent-history-fcb.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Setup context repository to return documents with older timestamp
        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped reprediction");
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Running_command_with_repredict_shows_latest_prediction_when_skipped()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 2);
        var contextTimestamp = new DateTimeOffset(2025, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["bundesliga-standings.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("3:2");
        await Assert.That(output).Contains("reprediction 0");
    }

    #endregion

    #region Max Repredictions Tests

    [Test]
    public async Task Running_command_with_max_repredictions_skips_when_at_limit()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        // Setup reprediction index to return 2 (already at max of 2)
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "2");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped");
        await Assert.That(output).Contains("already at max repredictions");
        await Assert.That(output).Contains("2/2");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_allows_reprediction_when_under_limit()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        // Setup reprediction index to return 1 (under max of 3)
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Setup prediction metadata with older timestamp
        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["recent-history-fcb.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "3");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction 2");
        await Assert.That(output).Contains("current: 1");
        await Assert.That(output).Contains("max: 3");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_zero_allows_only_first_prediction()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        // Setup reprediction index to return 0 (already at max of 0)
        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "0");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("already at max repredictions");
        await Assert.That(output).Contains("0/0");
    }

    #endregion

    #region Save Reprediction Tests

    [Test]
    public async Task Running_command_with_repredict_calls_save_reprediction_async()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["recent-history-fcb.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert
        mocks.PredictionRepository.Verify(
            r => r.SaveRepredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                2, // Next index should be 2 (current is 1)
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_repredict_verbose_shows_reprediction_index_saved()
    {
        // Arrange
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);

        var mocks = CreateStandardMocks(existingPrediction: existingPrediction);

        mocks.PredictionRepository
            .Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var metadata = new PredictionMetadata(
            existingPrediction,
            predictionTimestamp,
            ["recent-history-fcb.csv"]);
        mocks.PredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        mocks.ContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) =>
                contextDocs.TryGetValue(docName, out var doc) ? doc : null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "--verbose");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Saved as reprediction 1");
    }

    #endregion
}
