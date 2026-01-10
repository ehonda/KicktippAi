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
/// Additional tests to improve coverage of <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/>.
/// </summary>
public class MatchdayCommand_AdditionalCoverage_Tests : MatchdayCommandTests_Base
{
    #region CheckPredictionOutdated Coverage Tests

    [Test]
    public async Task Reprediction_skipped_when_prediction_metadata_is_null()
    {
        // Arrange - Set up prediction repo to return null metadata
        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);  // Has prediction at index 0
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PredictionMetadata?)null);  // No metadata

        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert - Should show up-to-date message because metadata is null
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Reprediction_skipped_when_prediction_metadata_has_empty_context_documents()
    {
        // Arrange - Set up prediction repo to return metadata with no context documents
        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);  // Has prediction at index 0
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                DateTimeOffset.UtcNow.AddHours(-1),
                new List<string>()));  // Empty context documents

        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert - Should show up-to-date message because no context documents were used
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    [Test]
    public async Task Reprediction_triggered_when_context_document_updated_after_prediction()
    {
        // Arrange - Set up prediction that is older than the context document
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-2);  // 2 hours ago
        var contextDocumentCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);  // 1 hour ago (newer)

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);  // Has prediction at index 0
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                predictionCreatedAt,
                new List<string> { "recent-history-fcb.csv" }));  // Context documents used

        // Create context doc that is newer than the prediction
        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["recent-history-fcb.csv"] = CreateContextDocument(
                documentName: "recent-history-fcb.csv",
                content: "Match,Result\n1,W",
                createdAt: contextDocumentCreatedAt)  // Newer than prediction
        };

        // Add all other required docs
        var fullContextDocs = CreateBayernVsDortmundContextDocuments(createdAt: contextDocumentCreatedAt);
        foreach (var kvp in fullContextDocs)
        {
            contextDocs[kvp.Key] = kvp.Value;
        }

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert - Should trigger reprediction because context is newer
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("outdated");
    }

    [Test]
    public async Task Reprediction_verbose_mode_shows_context_document_checking_info()
    {
        // Arrange - Set up prediction with context documents
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                predictionCreatedAt,
                new List<string> { "recent-history-fcb.csv", "recent-history-bvb.csv" }));

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1));  // Older than prediction
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        // Assert - Should show verbose checking info
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Checking");
    }

    [Test]
    public async Task Reprediction_handles_exception_during_outdated_check_gracefully()
    {
        // Arrange - Set up prediction repo to throw on metadata call
        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);  // Has prediction at index 0
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var contextDocs = CreateBayernVsDortmundContextDocuments();
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert - Should complete gracefully despite error
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");  // Falls back to not outdated on error
    }

    [Test]
    public async Task Reprediction_verbose_shows_warning_when_context_document_not_found()
    {
        // Arrange - Set up prediction with context documents that don't exist in repo
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                predictionCreatedAt,
                new List<string> { "non-existent-document.csv" }));

        // Context repository returns null for non-existent documents
        var mockContextRepository = new Mock<IContextRepository>();
        mockContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        // Assert - Should show warning about missing context document
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("not found");
    }

    [Test]
    public async Task Reprediction_skips_bundesliga_standings_from_outdated_check()
    {
        // Arrange - Set up prediction with bundesliga-standings in context documents
        // Even if standings are newer, it should skip checking them
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var standingsUpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);  // Newer, but should be skipped

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                predictionCreatedAt,
                new List<string> { "bundesliga-standings.csv" }));  // Only standings in context

        // Set up context repository with newer standings
        var contextDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50",
                createdAt: standingsUpdatedAt)  // Newer than prediction
        };

        // Add other required docs (older than prediction)
        var otherDocs = CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1));
        foreach (var kvp in otherDocs.Where(k => k.Key != "bundesliga-standings.csv"))
        {
            contextDocs[kvp.Key] = kvp.Value;
        }

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act - Use verbose mode to see skipping message
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict", "-v");

        // Assert - Should show up-to-date because standings check is skipped
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Skipping");
        await Assert.That(output).Contains("up-to-date");
    }

    #endregion

    #region GetHybridContextAsync Coverage Tests

    [Test]
    public async Task Verbose_mode_shows_database_context_count_when_all_required_present()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show database context count
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using");
        await Assert.That(output).Contains("context documents");
    }

    [Test]
    public async Task Verbose_mode_shows_merged_context_count_when_fallback_used()
    {
        // Arrange - Context repository returns incomplete documents
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50")
        };

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(partialDocs);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show fallback warning and merged count
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
        await Assert.That(output).Contains("Falling back");
    }

    #endregion

    #region GetMatchContextDocumentsAsync Coverage Tests

    [Test]
    public async Task Verbose_mode_shows_document_retrieval_status()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show document retrieval info
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context document");
    }

    [Test]
    public async Task Verbose_mode_shows_retrieved_documents()
    {
        // Arrange
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show document info
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("context documents");
    }

    [Test]
    public async Task Command_handles_context_repository_exception_gracefully()
    {
        // Arrange - Context repository throws exception
        var mockContextRepository = new Mock<IContextRepository>();
        mockContextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community");

        // Assert - Should show warning about database failure
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Warning:");
    }

    #endregion

    #region Optional Transfers Document Tests

    [Test]
    public async Task Verbose_mode_shows_optional_transfers_document_info()
    {
        // Arrange - Include optional transfers documents
        var contextDocs = CreateBayernVsDortmundContextDocuments();
        
        // Add optional transfers documents
        contextDocs["fcb-transfers.csv"] = CreateContextDocument(
            documentName: "fcb-transfers.csv",
            content: "Player,Type\nMüller,Loan");
        contextDocs["bvb-transfers.csv"] = CreateContextDocument(
            documentName: "bvb-transfers.csv",
            content: "Player,Type\nHaller,Transfer");

        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show optional document info
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("optional");
    }

    [Test]
    public async Task Verbose_mode_shows_missing_optional_documents()
    {
        // Arrange - Standard mocks without optional transfers
        var mocks = CreateStandardMocks(existingPrediction: (Prediction?)null);

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mocks.FirebaseServiceFactory,
            kicktippClientFactory: mocks.KicktippClientFactory,
            openAiServiceFactory: mocks.OpenAiServiceFactory,
            contextProviderFactory: mocks.ContextProviderFactory);

        // Act - Use verbose mode
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "-v");

        // Assert - Should show info about missing optional docs
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Missing optional");
    }

    #endregion

    #region StripDisplaySuffix Coverage Tests

    [Test]
    public async Task Reprediction_handles_context_document_names_with_display_suffix()
    {
        // Arrange - Set up prediction with context documents that have display suffixes
        var predictionCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);

        var mockPredictionRepository = CreateMockPredictionRepository(
            getPredictionResult: CreatePrediction(),
            getRepredictionIndexResult: 0);
        mockPredictionRepository
            .Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PredictionMetadata(
                CreatePrediction(),
                predictionCreatedAt,
                new List<string> { "recent-history-fcb.csv (kpi-context)" }));  // Has display suffix

        var contextDocs = CreateBayernVsDortmundContextDocuments(createdAt: predictionCreatedAt.AddHours(-1));  // Older than prediction
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(contextDocs);

        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            predictionRepository: mockPredictionRepository,
            contextRepository: mockContextRepository);

        var mockKicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateBayernVsDortmundMatchWithHistory() });
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory();
        var mockContextProviderFactory = CreateMockContextProviderFactory();

        var (app, console) = CreateMatchdayCommandApp(
            firebaseServiceFactory: mockFirebaseFactory,
            kicktippClientFactory: mockKicktippFactory,
            openAiServiceFactory: mockOpenAiFactory,
            contextProviderFactory: mockContextProviderFactory);

        // Act
        var (exitCode, output) = await RunCommandAsync(app, console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        // Assert - Should process correctly despite suffix
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("up-to-date");
    }

    #endregion
}
