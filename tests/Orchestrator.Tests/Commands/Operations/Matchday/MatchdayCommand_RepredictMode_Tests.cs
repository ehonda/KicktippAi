using System.Diagnostics;
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
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateMatchContextDocuments())));

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
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(),
                new Dictionary<string, ContextDocument> { ["recent-history-fcb.csv"] = CreateContextDocument(documentName: "recent-history-fcb.csv", version: 0) }, predictionTimestamp));

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
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(), createdAt: predictionTimestamp));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--competition", CompetitionIds.Bundesliga2025_26, "--repredict");

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
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(), createdAt: predictionTimestamp));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--competition", CompetitionIds.Bundesliga2025_26, "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("3:2");
        await Assert.That(output).Contains("reprediction 0");
    }

    [Test]
    [Arguments("Friday")]
    [Arguments("Saturday")]
    [NotInParallel("Telemetry")]
    public async Task Weekend_standings_refresh_reuses_remaining_open_fixture_without_model_call_or_new_index(string completedFixtureDay)
    {
        const string communityContext = "pes-squad";
        var match = CreateBayernVsDortmundMatch();
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var recordedDocuments = CreateBayernVsDortmundContextDocuments(communityContext: communityContext);
        var currentDocuments = new Dictionary<string, ContextDocument>(recordedDocuments, StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new ContextDocument(
                "bundesliga-standings.csv",
                $"Position,Team,Points\n1,FC Bayern München,3 after {completedFixtureDay}",
                recordedDocuments["bundesliga-standings.csv"].Version + 1,
                recordedDocuments["bundesliga-standings.csv"].CreatedAt.AddHours(1))
        };
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: prediction,
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(
                prediction, match, recordedDocuments, communityContext: communityContext),
            getRepredictionIndexResult: 0);
        var predictionService = CreateMockPredictionService();
        var contextRepository = CreateMockContextRepositoryWithDocuments(currentDocuments);
        ConfigureRecordedStandings(contextRepository, recordedDocuments["bundesliga-standings.csv"], communityContext);
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: contextRepository),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-5.6-sol", "-c", communityContext,
            "--community-context", communityContext,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--repredict", "--max-repredictions", "2");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped reprediction").And.Contains("reprediction 0");
        predictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
        predictionRepository.As<IResolvedMatchContextPredictionRepository>().Verify(repository =>
            repository.SaveRepredictionWithResolvedContextAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<ResolvedMatchContextManifest>(), It.IsAny<CancellationToken>()), Times.Never);
        var rootActivity = activities.Last(activity => activity.OperationName == "matchday");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.repredictionIndices") as string).IsEqualTo("|0|");
        await Assert.That(rootActivity.GetTagItem("langfuse.trace.metadata.hasRepredictions") as string).IsEqualTo("false");
    }

    [Test]
    [Arguments("schadensfresse")]
    [Arguments("relaxdays-tippt")]
    [Arguments("ehonda-ai-arena")]
    public async Task Copy_posting_lane_reuses_pes_squad_context_prediction_after_standings_refresh(string targetCommunity)
    {
        const string communityContext = "pes-squad";
        var match = CreateBayernVsDortmundMatch();
        var prediction = CreatePrediction(homeGoals: 2, awayGoals: 1);
        var recordedDocuments = CreateBayernVsDortmundContextDocuments(communityContext: communityContext);
        var currentDocuments = new Dictionary<string, ContextDocument>(recordedDocuments, StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new ContextDocument(
                "bundesliga-standings.csv",
                "Position,Team,Points\n1,FC Bayern München,6",
                recordedDocuments["bundesliga-standings.csv"].Version + 1,
                recordedDocuments["bundesliga-standings.csv"].CreatedAt.AddHours(1))
        };
        var predictionRepository = CreateMockPredictionRepository(
            getPredictionResult: prediction,
            getPredictionMetadataResult: CreateCanonicalBundesligaPredictionMetadata(
                prediction, match, recordedDocuments, communityContext: communityContext),
            getRepredictionIndexResult: 0);
        var predictionService = CreateMockPredictionService();
        var contextRepository = CreateMockContextRepositoryWithDocuments(currentDocuments);
        ConfigureRecordedStandings(contextRepository, recordedDocuments["bundesliga-standings.csv"], communityContext);
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepository,
                contextRepository: contextRepository),
            openAiServiceFactory: CreateMockOpenAiServiceFactory(predictionService: predictionService));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-5.6-sol", "-c", targetCommunity,
            "--community-context", communityContext,
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--repredict", "--max-repredictions", "2");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipped reprediction").And.Contains("reprediction 0");
        predictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(), It.IsAny<IEnumerable<DocumentContext>>(), It.IsAny<bool>(),
            It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()), Times.Never);
        predictionRepository.As<IResolvedMatchContextPredictionRepository>().Verify(repository =>
            repository.SaveRepredictionWithResolvedContextAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<ResolvedMatchContextManifest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Running_command_with_max_repredictions_blocks_unsafe_cached_prediction_at_limit()
    {
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "2");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("cannot be reused");
    }

    private static void ConfigureRecordedStandings(
        Mock<IContextRepository> contextRepository,
        ContextDocument recordedStandings,
        string communityContext)
    {
        contextRepository.Setup(repository => repository.GetContextDocumentAsync(
                "bundesliga-standings.csv",
                recordedStandings.Version,
                communityContext,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordedStandings);
    }

    [Test]
    public async Task Running_command_with_int32_max_reprediction_index_reuses_the_current_legacy_prediction_without_overflow()
    {
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);
        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(int.MaxValue);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(existingPrediction, DateTimeOffset.UtcNow, []));
        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community",
            "--competition", CompetitionIds.Bundesliga2025_26, "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("already at max repredictions (2147483647/2147483647)");
        predictionRepo.Verify(r => r.SaveRepredictionAsync(
            It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_with_max_repredictions_allows_reprediction_when_under_limit()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(),
                new Dictionary<string, ContextDocument> { ["recent-history-fcb.csv"] = CreateContextDocument(documentName: "recent-history-fcb.csv", version: 0) }, predictionTimestamp));

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
    public async Task Running_command_with_zero_max_repredictions_blocks_unsafe_cached_prediction()
    {
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "0");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("cannot be reused");
    }

    [Test]
    public async Task Running_command_with_repredict_calls_save_reprediction_async()
    {
        var contextTimestamp = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var predictionTimestamp = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 1);

        var predictionRepo = CreateMockPredictionRepository(getPredictionResult: existingPrediction);
        predictionRepo
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(),
                new Dictionary<string, ContextDocument> { ["recent-history-fcb.csv"] = CreateContextDocument(documentName: "recent-history-fcb.csv", version: 0) }, predictionTimestamp));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextTimestamp);
        var contextRepo = new Mock<IContextRepository>();
        contextRepo
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string docName, string _, CancellationToken _) => contextDocs.GetValueOrDefault(docName));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(predictionRepository: predictionRepo, contextRepository: contextRepo));

        await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        predictionRepo.As<IResolvedMatchContextPredictionRepository>().Verify(
            r => r.SaveRepredictionWithResolvedContextAsync(
                It.IsAny<Match>(), It.IsAny<Prediction>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), 1, int.MaxValue,
                It.Is<ResolvedMatchContextManifest>(manifest => manifest.Documents.Length == 11), It.IsAny<CancellationToken>()),
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
            .Setup(r => r.GetMatchRepredictionIndexAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        predictionRepo
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<PredictionModelConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCanonicalBundesligaPredictionMetadata(existingPrediction, CreateBayernVsDortmundMatch(),
                new Dictionary<string, ContextDocument> { ["recent-history-fcb.csv"] = CreateContextDocument(documentName: "recent-history-fcb.csv", version: 0) }, predictionTimestamp));

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
