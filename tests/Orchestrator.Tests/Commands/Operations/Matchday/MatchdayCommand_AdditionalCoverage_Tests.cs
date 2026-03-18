using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Additional tests to improve coverage of <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/>.
/// </summary>
public class MatchdayCommand_AdditionalCoverage_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Reprediction_skipped_when_prediction_metadata_is_null()
    {
        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PredictionMetadata?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments()));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Reprediction_skipped_when_prediction_metadata_has_empty_context_documents()
    {
        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), DateTimeOffset.UtcNow.AddHours(-1), new List<string>()));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments()));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Reprediction_triggered_when_context_document_updated_after_prediction()
    {
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var contextDocumentCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), predictionCreatedAt, new List<string> { "recent-history-fcb.csv" }));

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(documentName: "recent-history-fcb.csv", content: "Match,Result\n1,W", createdAt: contextDocumentCreatedAt)
        };
        var fullContextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextDocumentCreatedAt);
        foreach (var kvp in fullContextDocs)
        {
            contextDocs[kvp.Key] = kvp.Value;
        }

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(contextDocs));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("outdated");
    }

    [Test]
    public async Task Reprediction_verbose_mode_shows_context_document_checking_info()
    {
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), predictionCreatedAt, new List<string> { "recent-history-fcb.csv", "recent-history-bvb.csv" }));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1))));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Checking");
    }

    [Test]
    public async Task Reprediction_handles_exception_during_outdated_check_gracefully()
    {
        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments()));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Reprediction_verbose_shows_warning_when_context_document_not_found()
    {
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), predictionCreatedAt, new List<string> { "non-existent-document.csv" }));

        var mockContextRepository = new Mock<IContextRepository>();
        mockContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("not found");
    }

    [Test]
    public async Task Reprediction_skips_bundesliga_standings_from_outdated_check()
    {
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var standingsUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), predictionCreatedAt, new List<string> { "bundesliga-standings.csv" }));

        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(documentName: "bundesliga-standings.csv", content: "Position,Team,Points\n1,Bayern,50", createdAt: standingsUpdatedAt)
        };
        var otherDocs = CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1));
        foreach (var kvp in otherDocs.Where(k => k.Key != "bundesliga-standings.csv"))
        {
            contextDocs[kvp.Key] = kvp.Value;
        }

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(contextDocs));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipping");
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Verbose_mode_shows_database_context_count_when_all_required_present()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using");
        await Assert.That(output).Contains("context documents");
    }

    [Test]
    public async Task Verbose_mode_shows_merged_context_count_when_fallback_used()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(documentName: "bundesliga-standings.csv", content: "Position,Team,Points\n1,Bayern,50")
        };

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: CreateMockContextRepositoryWithDocuments(partialDocs));
        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("Falling back");
    }

    [Test]
    public async Task Verbose_mode_shows_document_retrieval_status()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context document");
    }

    [Test]
    public async Task Verbose_mode_shows_retrieved_documents()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context documents");
    }

    [Test]
    public async Task Command_handles_context_repository_exception_gracefully()
    {
        var mockContextRepository = new Mock<IContextRepository>();
        mockContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);
        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
    }

    [Test]
    public async Task Verbose_mode_shows_optional_transfers_document_info()
    {
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        contextDocs["fcb-transfers.csv"] = CreateContextDocument(documentName: "fcb-transfers.csv", content: "Player,Type\nMüller,Loan");
        contextDocs["bvb-transfers.csv"] = CreateContextDocument(documentName: "bvb-transfers.csv", content: "Player,Type\nHaller,Transfer");

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: CreateMockContextRepositoryWithDocuments(contextDocs));
        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("optional");
    }

    [Test]
    public async Task Verbose_mode_shows_missing_optional_documents()
    {
        var ctx = CreateMatchdayCommandApp(existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Missing optional");
    }

    [Test]
    public async Task Reprediction_handles_context_document_names_with_display_suffix()
    {
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(getPredictionResult: CreatePrediction(), getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(It.IsAny<Match>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(CreatePrediction(), predictionCreatedAt, new List<string> { "recent-history-fcb.csv (kpi-context)" }));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1))));

        var ctx = CreateMatchdayCommandApp(firebaseServiceFactory: mockFirebaseFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Fallback_adds_on_demand_context_documents_when_database_has_partial_docs()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(documentName: "bundesliga-standings.csv", content: "Position,Team,Points\n1,Bayern,50")
        };

        var onDemandDocs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Match,Result\n1,W"),
            new("recent-history-bvb.csv", "Match,Result\n1,L")
        };

        var mockContextProvider = CreateMockKicktippContextProvider(matchContextDocuments: onDemandDocs);
        var mockContextProviderFactory = CreateMockContextProviderFactory(contextProvider: mockContextProvider);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: CreateMockContextRepositoryWithDocuments(partialDocs));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            contextProviderFactory: mockContextProviderFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("Falling back");
        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(It.IsAny<Match>(), It.Is<IEnumerable<DocumentContext>>(docs => docs.Count() >= 3), It.IsAny<bool>(), It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Fallback_skips_duplicate_documents_from_on_demand_provider()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(documentName: "bundesliga-standings.csv", content: "Position,Team,Points\n1,Bayern,50")
        };

        var onDemandDocs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "Duplicate - should be skipped"),
            new("new-document.csv", "New content - should be added")
        };

        var mockContextProvider = CreateMockKicktippContextProvider(matchContextDocuments: onDemandDocs);
        var mockContextProviderFactory = CreateMockContextProviderFactory(contextProvider: mockContextProvider);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: CreateMockContextRepositoryWithDocuments(partialDocs));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            contextProviderFactory: mockContextProviderFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        ctx.PredictionService.Verify(
            s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.Is<IEnumerable<DocumentContext>>(docs =>
                    docs.Count() == 2 &&
                    docs.Any(d => d.Name == "bundesliga-standings.csv" && d.Content.Contains("Bayern")) &&
                    docs.Any(d => d.Name == "new-document.csv")),
                It.IsAny<bool>(),
                It.IsAny<OpenAiIntegration.PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Fallback_verbose_mode_shows_merged_context_document_count()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(documentName: "bundesliga-standings.csv", content: "Position,Team,Points\n1,Bayern,50")
        };

        var onDemandDocs = new List<DocumentContext>
        {
            new("recent-history-fcb.csv", "Match,Result\n1,W"),
            new("recent-history-bvb.csv", "Match,Result\n1,L")
        };

        var mockContextProvider = CreateMockKicktippContextProvider(matchContextDocuments: onDemandDocs);
        var mockContextProviderFactory = CreateMockContextProviderFactory(contextProvider: mockContextProvider);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: CreateMockContextRepositoryWithDocuments(partialDocs));

        var ctx = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            contextProviderFactory: mockContextProviderFactory);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("merged context documents");
    }

    [Test]
    public async Task Cancelled_match_processing_text_includes_cancelled_indicator()
    {
        // Arrange
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var matchesWithHistory = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: cancelledMatch)
        };

        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matchesWithHistory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Processing line should indicate cancelled status
        await Assert.That(output).Contains("Processing:").And.Contains("FC Bayern München vs Borussia Dortmund").And.Contains("(CANCELLED)");
    }

    [Test]
    public async Task Multiple_cancelled_matches_processing_text_includes_cancelled_indicator()
    {
        // Arrange
        var cancelledMatch1 = CreateMatch(
            homeTeam: "Team A",
            awayTeam: "Team B",
            matchday: 16,
            isCancelled: true);
        var cancelledMatch2 = CreateMatch(
            homeTeam: "Team C",
            awayTeam: "Team D",
            matchday: 16,
            isCancelled: true);
        var normalMatch = CreateMatch(
            homeTeam: "Team E",
            awayTeam: "Team F",
            matchday: 16,
            isCancelled: false);

        var matchesWithHistory = new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: cancelledMatch1),
            CreateMatchWithHistory(match: cancelledMatch2),
            CreateMatchWithHistory(match: normalMatch)
        };

        var ctx = CreateMatchdayCommandApp(matchesWithHistory: matchesWithHistory);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - both cancelled matches should show (CANCELLED) in processing text
        // Count occurrences of "(CANCELLED)" - should be exactly 2
        var cancelledCount = System.Text.RegularExpressions.Regex.Matches(output, @"\(CANCELLED\)").Count;
        await Assert.That(cancelledCount).IsEqualTo(2);
        
        // Normal match should NOT have cancelled indicator in processing
        await Assert.That(output).Contains("Processing:").And.Contains("Team E vs Team F");
        // Verify the normal match doesn't show (CANCELLED)
        // Check that "Team E vs Team F" line doesn't contain "CANCELLED"
        var lines = output.Split('\n');
        var teamEFLine = lines.FirstOrDefault(l => l.Contains("Team E vs Team F") && l.Contains("Processing:"));
        await Assert.That(teamEFLine).IsNotNull().And.DoesNotContain("CANCELLED");
    }

    [Test]
    public async Task Cancelled_match_repredict_shows_warning_when_metadata_lookup_fails()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var predictionRepo = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: CreatePrediction(),
            getCancelledMatchRepredictionIndexResult: 0);
        predictionRepo
            .Setup(r => r.GetCancelledMatchPredictionMetadataAsync(
                cancelledMatch.HomeTeam,
                cancelledMatch.AwayTeam,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Metadata lookup failed"));

        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments())));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-5",
            "-c",
            "test-community",
            "--repredict",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed to check outdated status");
    }

    [Test]
    public async Task Cancelled_match_repredict_creates_first_prediction_when_none_exists()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var predictionRepo = CreateMockPredictionRepository(
            getCancelledMatchRepredictionIndexResult: -1,
            getCancelledMatchPredictionResult: (Prediction?)null);

        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments())));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-5", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("creating first prediction");
        predictionRepo.Verify(
            r => r.GetCancelledMatchRepredictionIndexAsync(
                cancelledMatch.HomeTeam,
                cancelledMatch.AwayTeam,
                "gpt-5",
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task Cancelled_match_repredict_shows_latest_prediction_when_up_to_date()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var existingPrediction = CreatePrediction(homeGoals: 3, awayGoals: 1);
        var predictionTimestamp = DateTimeOffset.UtcNow;
        var olderContextTimestamp = predictionTimestamp.AddHours(-1);

        var predictionRepo = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: existingPrediction,
            getCancelledMatchPredictionMetadataResult: new PredictionMetadata(existingPrediction, predictionTimestamp, ["recent-history-fcb.csv"]),
            getCancelledMatchRepredictionIndexResult: 0);

        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(
                    CreateBayernVsDortmundContextDocuments(createdAt: olderContextTimestamp))));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-5", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Latest prediction:");
        await Assert.That(output).Contains("3:1");
        predictionRepo.Verify(
            r => r.GetCancelledMatchPredictionAsync(
                cancelledMatch.HomeTeam,
                cancelledMatch.AwayTeam,
                "gpt-5",
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Cancelled_match_repredict_at_max_uses_cancelled_prediction_lookup()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var existingPrediction = CreatePrediction(homeGoals: 2, awayGoals: 2);

        var predictionRepo = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: existingPrediction,
            getCancelledMatchRepredictionIndexResult: 2);

        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(CreateBayernVsDortmundContextDocuments())));

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-5",
            "-c",
            "test-community",
            "--max-repredictions",
            "2");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("already at max repredictions");
        predictionRepo.Verify(
            r => r.GetCancelledMatchPredictionAsync(
                cancelledMatch.HomeTeam,
                cancelledMatch.AwayTeam,
                "gpt-5",
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Cancelled_match_reprediction_save_uses_cancelled_index_lookup()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 16,
            isCancelled: true);
        var existingPrediction = CreatePrediction(homeGoals: 1, awayGoals: 0);
        var predictionTimestamp = DateTimeOffset.UtcNow.AddHours(-2);
        var newerContextTimestamp = DateTimeOffset.UtcNow.AddHours(-1);

        var predictionRepo = CreateMockPredictionRepository(
            getCancelledMatchPredictionResult: existingPrediction,
            getCancelledMatchPredictionMetadataResult: new PredictionMetadata(existingPrediction, predictionTimestamp, ["recent-history-fcb.csv"]),
            getCancelledMatchRepredictionIndexResult: 0);

        var ctx = CreateMatchdayCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(
                predictionRepository: predictionRepo,
                contextRepository: CreateMockContextRepositoryWithDocuments(
                    CreateBayernVsDortmundContextDocuments(createdAt: newerContextTimestamp))));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-5", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Creating reprediction");
        predictionRepo.Verify(
            r => r.GetCancelledMatchRepredictionIndexAsync(
                cancelledMatch.HomeTeam,
                cancelledMatch.AwayTeam,
                "gpt-5",
                "test-community",
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task Show_context_documents_reports_additional_lines_for_long_documents()
    {
        var longContent = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line {i}"));
        var docs = CreateBayernVsDortmundContextDocuments();
        docs["bundesliga-standings.csv"] = CreateContextDocument(
            documentName: "bundesliga-standings.csv",
            content: longContent);

        var ctx = CreateMatchdayCommandApp(contextDocuments: docs, existingPrediction: (Prediction?)null);

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "gpt-5",
            "-c",
            "test-community",
            "--show-context-documents");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("10 more lines");
    }

    [Test]
    public async Task Verbose_mode_reports_optional_document_lookup_failures()
    {
        var docs = CreateBayernVsDortmundContextDocuments();
        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string documentName, string _, CancellationToken _) =>
            {
                if (documentName.EndsWith("-transfers.csv", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Optional lookup failed");
                }

                return docs.GetValueOrDefault(documentName);
            });

        var ctx = CreateMatchdayCommandApp(
            existingPrediction: (Prediction?)null,
            firebaseServiceFactory: CreateMockFirebaseServiceFactoryFull(contextRepository: contextRepository));

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-5", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Failed optional");
        await Assert.That(output).Contains("Optional lookup failed");
    }
}
