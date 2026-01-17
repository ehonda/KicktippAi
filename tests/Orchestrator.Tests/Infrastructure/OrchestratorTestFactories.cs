using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Utility.ListKpi;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using TestUtilities;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Infrastructure;

/// <summary>
/// Factory methods for creating test infrastructure for Orchestrator command tests.
/// Use these methods to create test instances with sensible defaults,
/// allowing tests to override only the dependencies relevant to their scenario.
/// </summary>
public static class OrchestratorTestFactories
{
    /// <summary>
    /// Creates a configured <see cref="CommandApp"/> for testing commands.
    /// </summary>
    /// <typeparam name="TCommand">The command type to configure.</typeparam>
    /// <param name="commandName">The command name to register (e.g., "list-kpi").</param>
    /// <param name="console">Optional TestConsole instance. Defaults to a new TestConsole.</param>
    /// <param name="firebaseServiceFactory">Optional mock for IFirebaseServiceFactory. Defaults to a new mock.</param>
    /// <param name="configureServices">Optional action to configure additional services.</param>
    /// <returns>A tuple containing the CommandApp and the TestConsole for assertions.</returns>
    public static (CommandApp App, TestConsole Console) CreateCommandApp<TCommand>(
        string commandName,
        Option<TestConsole> console = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Action<IServiceCollection>> configureServices = default)
        where TCommand : class, ICommand
    {
        var testConsole = console.Or(() => new TestConsole());
        var mockFactory = firebaseServiceFactory.Or(() => new Mock<IFirebaseServiceFactory>());

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFactory.Object);
        services.AddSingleton<ILogger<TCommand>>(new FakeLogger<TCommand>());

        var configureAction = configureServices.Or(() => _ => { });
        configureAction(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<TCommand>(commandName);
        });

        return (app, testConsole);
    }

    /// <summary>
    /// Runs a command and returns the result including exit code and console output.
    /// </summary>
    /// <param name="app">The CommandApp to run.</param>
    /// <param name="console">The TestConsole to capture output from.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A tuple containing the exit code and the console output.</returns>
    public static async Task<(int ExitCode, string Output)> RunCommandAsync(
        CommandApp app,
        TestConsole console,
        params string[] args)
    {
        var exitCode = await app.RunAsync(args);
        var output = console.Output;
        return (exitCode, output);
    }

    /// <summary>
    /// Creates a mock <see cref="IKpiRepository"/> configured to return the specified documents.
    /// </summary>
    /// <param name="documents">The documents to return from GetAllKpiDocumentsAsync. Defaults to empty list.</param>
    /// <param name="communityContext">The community context to match. If not provided, matches any.</param>
    public static Mock<IKpiRepository> CreateMockKpiRepository(
        Option<IReadOnlyList<KpiDocument>> documents = default,
        Option<string> communityContext = default)
    {
        var mock = new Mock<IKpiRepository>();
        var docs = documents.Or(() => new List<KpiDocument>());

        // Use deconstruction to check if communityContext was explicitly provided
        var (hasContext, contextValue) = communityContext;

        if (hasContext)
        {
            mock.Setup(r => r.GetAllKpiDocumentsAsync(contextValue, It.IsAny<CancellationToken>()))
                .ReturnsAsync(docs);
        }
        else
        {
            mock.Setup(r => r.GetAllKpiDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(docs);
        }

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IFirebaseServiceFactory"/> that returns the specified KPI repository.
    /// </summary>
    /// <param name="kpiRepository">The KPI repository to return. Defaults to a new mock.</param>
    public static Mock<IFirebaseServiceFactory> CreateMockFirebaseServiceFactory(
        Option<Mock<IKpiRepository>> kpiRepository = default)
    {
        var mockFactory = new Mock<IFirebaseServiceFactory>();
        var mockRepo = kpiRepository.Or(() => new Mock<IKpiRepository>());

        mockFactory.Setup(f => f.CreateKpiRepository()).Returns(mockRepo.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a test <see cref="KpiDocument"/> with default values.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-document".</param>
    /// <param name="content">Document content. Defaults to "test content".</param>
    /// <param name="description">Document description. Defaults to "test description".</param>
    /// <param name="version">Document version. Defaults to 1.</param>
    /// <param name="createdAt">Creation timestamp. Defaults to 2025-01-10 12:00 UTC.</param>
    public static KpiDocument CreateKpiDocument(
        Option<string> documentName = default,
        Option<string> content = default,
        Option<string> description = default,
        Option<int> version = default,
        Option<DateTimeOffset> createdAt = default)
    {
        return new KpiDocument(
            documentName.Or("test-document"),
            content.Or("test content"),
            description.Or("test description"),
            version.Or(1),
            createdAt.Or(() => new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Creates a mock <see cref="IKicktippClient"/> with configurable behavior.
    /// </summary>
    /// <param name="matchesWithHistory">Matches returned by GetMatchesWithHistoryAsync. Defaults to empty list.</param>
    /// <param name="placeBetsResult">Result of PlaceBetsAsync. Defaults to true.</param>
    /// <param name="placedPredictions">Predictions returned by GetPlacedPredictionsAsync. Defaults to empty dictionary.</param>
    /// <param name="openBonusQuestions">Bonus questions returned by GetOpenBonusQuestionsAsync. Defaults to empty list.</param>
    /// <param name="placeBonusPredictionsResult">Result of PlaceBonusPredictionsAsync. Defaults to true.</param>
    /// <param name="placedBonusPredictions">Predictions returned by GetPlacedBonusPredictionsAsync. Defaults to empty dictionary.</param>
    public static Mock<IKicktippClient> CreateMockKicktippClient(
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<bool> placeBetsResult = default,
        Option<Dictionary<Match, BetPrediction?>> placedPredictions = default,
        Option<List<BonusQuestion>> openBonusQuestions = default,
        Option<bool> placeBonusPredictionsResult = default,
        Option<Dictionary<string, BonusPrediction?>> placedBonusPredictions = default)
    {
        var mock = new Mock<IKicktippClient>();
        var matches = matchesWithHistory.Or(() => []);
        var betsResult = placeBetsResult.Or(true);
        var predictions = placedPredictions.Or(() => new Dictionary<Match, BetPrediction?>());
        var bonusQuestions = openBonusQuestions.Or(() => []);
        var bonusResult = placeBonusPredictionsResult.Or(true);
        var bonusPredictions = placedBonusPredictions.Or(() => new Dictionary<string, BonusPrediction?>());

        mock.Setup(c => c.GetMatchesWithHistoryAsync(It.IsAny<string>()))
            .ReturnsAsync(matches);

        mock.Setup(c => c.PlaceBetsAsync(It.IsAny<string>(), It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()))
            .ReturnsAsync(betsResult);

        mock.Setup(c => c.GetPlacedPredictionsAsync(It.IsAny<string>()))
            .ReturnsAsync(predictions);

        mock.Setup(c => c.GetOpenBonusQuestionsAsync(It.IsAny<string>()))
            .ReturnsAsync(bonusQuestions);

        mock.Setup(c => c.PlaceBonusPredictionsAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, BonusPrediction>>(),
                It.IsAny<bool>()))
            .ReturnsAsync(bonusResult);

        mock.Setup(c => c.GetPlacedBonusPredictionsAsync(It.IsAny<string>()))
            .ReturnsAsync(bonusPredictions);

        return mock;
    }

    /// <summary>
    /// Creates a test <see cref="BetPrediction"/> with default values.
    /// </summary>
    /// <param name="homeGoals">Home team goals. Defaults to 2.</param>
    /// <param name="awayGoals">Away team goals. Defaults to 1.</param>
    public static BetPrediction CreateBetPrediction(
        Option<int> homeGoals = default,
        Option<int> awayGoals = default)
    {
        return new BetPrediction(
            homeGoals.Or(2),
            awayGoals.Or(1));
    }

    /// <summary>
    /// Creates a mock <see cref="IKicktippClientFactory"/> that returns the specified client.
    /// </summary>
    /// <param name="kicktippClient">The Kicktipp client to return. Defaults to a new mock.</param>
    public static Mock<IKicktippClientFactory> CreateMockKicktippClientFactory(
        Option<Mock<IKicktippClient>> kicktippClient = default)
    {
        var mockFactory = new Mock<IKicktippClientFactory>();
        var mockClient = kicktippClient.Or(() => CreateMockKicktippClient());

        mockFactory.Setup(f => f.CreateClient()).Returns(mockClient.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a mock <see cref="IPredictionService"/> with configurable behavior.
    /// </summary>
    /// <param name="predictMatchResult">Result of PredictMatchAsync. Defaults to a new test prediction.</param>
    /// <param name="matchPromptPath">Result of GetMatchPromptPath. Defaults to "prompts/match-prompt.md".</param>
    /// <param name="predictBonusResult">Result of PredictBonusQuestionAsync. Defaults to a new test bonus prediction.</param>
    /// <param name="bonusPromptPath">Result of GetBonusPromptPath. Defaults to "prompts/bonus-prompt.md".</param>
    public static Mock<IPredictionService> CreateMockPredictionService(
        NullableOption<Prediction> predictMatchResult = default,
        Option<string> matchPromptPath = default,
        NullableOption<BonusPrediction> predictBonusResult = default,
        Option<string> bonusPromptPath = default)
    {
        var mock = new Mock<IPredictionService>();

        var prediction = predictMatchResult.Or(() => new Prediction(2, 1, null));
        mock.Setup(s => s.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(prediction);

        mock.Setup(s => s.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns(matchPromptPath.Or("prompts/match-prompt.md"));

        var bonusPrediction = predictBonusResult.Or(() => new BonusPrediction(["opt-1"]));
        mock.Setup(s => s.PredictBonusQuestionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(bonusPrediction);

        mock.Setup(s => s.GetBonusPromptPath())
            .Returns(bonusPromptPath.Or("prompts/bonus-prompt.md"));

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="ITokenUsageTracker"/> with configurable behavior.
    /// </summary>
    /// <param name="compactSummary">Result of GetCompactSummary. Defaults to "0/0/0/0/$0.00".</param>
    /// <param name="lastCost">Result of GetLastCost. Defaults to 0.01m.</param>
    /// <param name="lastUsageJson">Result of GetLastUsageJson. Defaults to "{}".</param>
    public static Mock<ITokenUsageTracker> CreateMockTokenUsageTracker(
        Option<string> compactSummary = default,
        Option<decimal> lastCost = default,
        NullableOption<string> lastUsageJson = default)
    {
        var mock = new Mock<ITokenUsageTracker>();
        var summary = compactSummary.Or("0/0/0/0/$0.00");

        mock.Setup(t => t.GetCompactSummary()).Returns(summary);
        mock.Setup(t => t.GetCompactSummaryWithEstimatedCosts(It.IsAny<string>())).Returns(summary);
        mock.Setup(t => t.GetLastUsageCompactSummary()).Returns(summary);
        mock.Setup(t => t.GetLastUsageCompactSummaryWithEstimatedCosts(It.IsAny<string>())).Returns(summary);
        mock.Setup(t => t.GetLastCost()).Returns(lastCost.Or(0.01m));
        mock.Setup(t => t.GetLastUsageJson()).Returns(lastUsageJson.Or("{}"));
        mock.Setup(t => t.GetTotalCost()).Returns(lastCost.Or(0.01m));

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IOpenAiServiceFactory"/> that returns the specified services.
    /// </summary>
    /// <param name="predictionService">The prediction service to return. Defaults to a new mock.</param>
    /// <param name="tokenUsageTracker">The token usage tracker to return. Defaults to a new mock.</param>
    public static Mock<IOpenAiServiceFactory> CreateMockOpenAiServiceFactory(
        Option<Mock<IPredictionService>> predictionService = default,
        Option<Mock<ITokenUsageTracker>> tokenUsageTracker = default)
    {
        var mockFactory = new Mock<IOpenAiServiceFactory>();
        var mockPredictionService = predictionService.Or(() => CreateMockPredictionService());
        var mockTracker = tokenUsageTracker.Or(() => CreateMockTokenUsageTracker());

        mockFactory.Setup(f => f.CreatePredictionService(It.IsAny<string>()))
            .Returns(mockPredictionService.Object);
        mockFactory.Setup(f => f.GetTokenUsageTracker())
            .Returns(mockTracker.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a mock <see cref="IContextProviderFactory"/> with configurable context provider behavior.
    /// </summary>
    /// <param name="contextProvider">Optional mock Kicktipp context provider. If not provided, creates an empty one.</param>
    /// <param name="kpiContextProvider">Optional mock KPI context provider. If not provided, creates an empty one.</param>
    public static Mock<IContextProviderFactory> CreateMockContextProviderFactory(
        Option<Mock<IKicktippContextProvider>> contextProvider = default,
        Option<Mock<IKpiContextProvider>> kpiContextProvider = default)
    {
        var mockFactory = new Mock<IContextProviderFactory>();
        var mockProvider = contextProvider.Or(() => CreateMockKicktippContextProvider());
        var mockKpiProvider = kpiContextProvider.Or(() => CreateMockKpiContextProvider());

        mockFactory.Setup(f => f.CreateKicktippContextProvider(
                It.IsAny<IKicktippClient>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(mockProvider.Object);

        mockFactory.Setup(f => f.CreateKpiContextProvider())
            .Returns(mockKpiProvider.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a mock <see cref="IKicktippContextProvider"/> with configurable behavior.
    /// </summary>
    /// <param name="matchContextDocuments">Documents to return from GetMatchContextAsync. Defaults to empty.</param>
    public static Mock<IKicktippContextProvider> CreateMockKicktippContextProvider(
        Option<List<DocumentContext>> matchContextDocuments = default)
    {
        var mock = new Mock<IKicktippContextProvider>();
        var docs = matchContextDocuments.Or(() => new List<DocumentContext>());

        mock.Setup(p => p.GetMatchContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(docs.ToAsyncEnumerable());

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IKpiContextProvider"/> with configurable behavior.
    /// </summary>
    /// <param name="bonusQuestionContextDocuments">Documents to return from GetBonusQuestionContextAsync. Defaults to empty.</param>
    /// <param name="contextDocuments">Documents to return from GetContextAsync. Defaults to empty.</param>
    public static Mock<IKpiContextProvider> CreateMockKpiContextProvider(
        Option<List<DocumentContext>> bonusQuestionContextDocuments = default,
        Option<List<DocumentContext>> contextDocuments = default)
    {
        var mock = new Mock<IKpiContextProvider>();
        var bonusDocs = bonusQuestionContextDocuments.Or(() => []);
        var docs = contextDocuments.Or(() => []);

        mock.Setup(p => p.GetBonusQuestionContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(bonusDocs.ToAsyncEnumerable());

        mock.Setup(p => p.GetContextAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(docs.ToAsyncEnumerable());

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IPredictionRepository"/> with configurable behavior.
    /// </summary>
    /// <param name="getPredictionResult">Result of GetPredictionAsync. Defaults to null.</param>
    /// <param name="getPredictionMetadataResult">Result of GetPredictionMetadataAsync. Defaults to null.</param>
    /// <param name="getRepredictionIndexResult">Result of GetMatchRepredictionIndexAsync. Defaults to -1.</param>
    /// <param name="getBonusPredictionByTextResult">Result of GetBonusPredictionByTextAsync. Defaults to null.</param>
    /// <param name="getBonusRepredictionIndexResult">Result of GetBonusRepredictionIndexAsync. Defaults to -1.</param>
    /// <param name="getCancelledMatchPredictionResult">Result of GetCancelledMatchPredictionAsync. Defaults to null.</param>
    /// <param name="getCancelledMatchPredictionMetadataResult">Result of GetCancelledMatchPredictionMetadataAsync. Defaults to null.</param>
    /// <param name="getCancelledMatchRepredictionIndexResult">Result of GetCancelledMatchRepredictionIndexAsync. Defaults to -1.</param>
    /// <param name="getBonusPredictionMetadataByTextResult">Result of GetBonusPredictionMetadataByTextAsync. Defaults to null.</param>
    public static Mock<IPredictionRepository> CreateMockPredictionRepository(
        NullableOption<Prediction> getPredictionResult = default,
        NullableOption<PredictionMetadata> getPredictionMetadataResult = default,
        Option<int> getRepredictionIndexResult = default,
        NullableOption<BonusPrediction> getBonusPredictionByTextResult = default,
        Option<int> getBonusRepredictionIndexResult = default,
        NullableOption<Prediction> getCancelledMatchPredictionResult = default,
        NullableOption<PredictionMetadata> getCancelledMatchPredictionMetadataResult = default,
        Option<int> getCancelledMatchRepredictionIndexResult = default,
        NullableOption<BonusPredictionMetadata> getBonusPredictionMetadataByTextResult = default)
    {
        var mock = new Mock<IPredictionRepository>();

        mock.Setup(r => r.GetPredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getPredictionResult.Or((Prediction?)null));

        mock.Setup(r => r.GetPredictionMetadataAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getPredictionMetadataResult.Or((PredictionMetadata?)null));

        mock.Setup(r => r.GetMatchRepredictionIndexAsync(
                It.IsAny<Match>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getRepredictionIndexResult.Or(-1));

        mock.Setup(r => r.SavePredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mock.Setup(r => r.SaveRepredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Bonus prediction methods
        mock.Setup(r => r.GetBonusPredictionByTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getBonusPredictionByTextResult.Or((BonusPrediction?)null));

        mock.Setup(r => r.GetBonusPredictionMetadataByTextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getBonusPredictionMetadataByTextResult.Or((BonusPredictionMetadata?)null));

        mock.Setup(r => r.GetBonusRepredictionIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getBonusRepredictionIndexResult.Or(-1));

        // Cancelled match methods - must be explicitly configured when testing cancelled matches
        mock.Setup(r => r.GetCancelledMatchPredictionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getCancelledMatchPredictionResult.Or((Prediction?)null));

        mock.Setup(r => r.GetCancelledMatchPredictionMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getCancelledMatchPredictionMetadataResult.Or((PredictionMetadata?)null));

        mock.Setup(r => r.GetCancelledMatchRepredictionIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getCancelledMatchRepredictionIndexResult.Or(-1));

        mock.Setup(r => r.SaveBonusPredictionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback<BonusQuestion, BonusPrediction, string, string, double, string, IEnumerable<string>, bool, CancellationToken>(
                (_, _, _, _, _, _, contextDocumentNames, _, _) =>
                {
                    // Force enumeration of the context document names to exercise the Select lambda
                    _ = contextDocumentNames.ToList();
                })
            .Returns(Task.CompletedTask);

        mock.Setup(r => r.SaveBonusRepredictionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<BonusQuestion, BonusPrediction, string, string, double, string, IEnumerable<string>, int, CancellationToken>(
                (_, _, _, _, _, _, contextDocumentNames, _, _) =>
                {
                    // Force enumeration of the context document names to exercise the Select lambda
                    _ = contextDocumentNames.ToList();
                })
            .Returns(Task.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IContextRepository"/> with configurable behavior.
    /// </summary>
    /// <param name="getLatestContextDocumentResult">Result of GetLatestContextDocumentAsync. Defaults to null.</param>
    /// <param name="saveContextDocumentResult">Result of SaveContextDocumentAsync (version number or null if unchanged). Defaults to 1.</param>
    public static Mock<IContextRepository> CreateMockContextRepository(
        NullableOption<ContextDocument> getLatestContextDocumentResult = default,
        NullableOption<int?> saveContextDocumentResult = default)
    {
        var mock = new Mock<IContextRepository>();

        mock.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(getLatestContextDocumentResult.Or((ContextDocument?)null));

        mock.Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(saveContextDocumentResult.Or(1));

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IContextRepository"/> configured for upload operations.
    /// </summary>
    /// <param name="existingDocument">Document returned by GetLatestContextDocumentAsync. Defaults to null (no existing document).</param>
    /// <param name="savedVersion">Version returned by SaveContextDocumentAsync. Defaults to 0. Pass null to simulate unchanged content.</param>
    public static Mock<IContextRepository> CreateMockContextRepositoryForUpload(
        NullableOption<ContextDocument> existingDocument = default,
        NullableOption<int?> savedVersion = default)
    {
        var mock = new Mock<IContextRepository>();

        mock.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument.Or((ContextDocument?)null));

        mock.Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedVersion.Or((int?)0));

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IContextRepository"/> that returns documents based on document name.
    /// </summary>
    /// <param name="documentsByName">Dictionary mapping document names to their content.</param>
    /// <param name="communityContext">Community context to match. If not provided, matches any.</param>
    public static Mock<IContextRepository> CreateMockContextRepositoryWithDocuments(
        Dictionary<string, ContextDocument> documentsByName,
        Option<string> communityContext = default)
    {
        var mock = new Mock<IContextRepository>();
        var (hasContext, contextValue) = communityContext;

        if (hasContext)
        {
            mock.Setup(r => r.GetLatestContextDocumentAsync(
                    It.IsAny<string>(),
                    contextValue,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string docName, string _, CancellationToken _) =>
                    documentsByName.TryGetValue(docName, out var doc) ? doc : null);
        }
        else
        {
            mock.Setup(r => r.GetLatestContextDocumentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string docName, string _, CancellationToken _) =>
                    documentsByName.TryGetValue(docName, out var doc) ? doc : null);
        }

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IFirebaseServiceFactory"/> with all repositories configured.
    /// </summary>
    /// <param name="kpiRepository">The KPI repository to return. Defaults to a new mock.</param>
    /// <param name="predictionRepository">The prediction repository to return. Defaults to a new mock.</param>
    /// <param name="contextRepository">The context repository to return. Defaults to a new mock.</param>
    public static Mock<IFirebaseServiceFactory> CreateMockFirebaseServiceFactoryFull(
        Option<Mock<IKpiRepository>> kpiRepository = default,
        Option<Mock<IPredictionRepository>> predictionRepository = default,
        Option<Mock<IContextRepository>> contextRepository = default)
    {
        var mockFactory = new Mock<IFirebaseServiceFactory>();
        var mockKpiRepo = kpiRepository.Or(() => new Mock<IKpiRepository>());
        var mockPredictionRepo = predictionRepository.Or(() => CreateMockPredictionRepository());
        var mockContextRepo = contextRepository.Or(() => CreateMockContextRepository());

        mockFactory.Setup(f => f.CreateKpiRepository()).Returns(mockKpiRepo.Object);
        mockFactory.Setup(f => f.CreatePredictionRepository()).Returns(mockPredictionRepo.Object);
        mockFactory.Setup(f => f.CreateContextRepository()).Returns(mockContextRepo.Object);

        return mockFactory;
    }

    /// <summary>
    /// Creates a test <see cref="ContextDocument"/> with default values.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-document".</param>
    /// <param name="content">Document content. Defaults to "test content".</param>
    /// <param name="version">Document version. Defaults to 1.</param>
    /// <param name="createdAt">Creation timestamp. Defaults to 2025-01-10 12:00 UTC.</param>
    public static ContextDocument CreateContextDocument(
        Option<string> documentName = default,
        Option<string> content = default,
        Option<int> version = default,
        Option<DateTimeOffset> createdAt = default)
    {
        return new ContextDocument(
            documentName.Or("test-document"),
            content.Or("test content"),
            version.Or(1),
            createdAt.Or(() => new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Creates a set of context documents required for a match prediction.
    /// </summary>
    /// <param name="homeAbbreviation">Home team abbreviation. Defaults to "fcb".</param>
    /// <param name="awayAbbreviation">Away team abbreviation. Defaults to "bvb".</param>
    /// <param name="communityContext">Community context. Defaults to "test-community".</param>
    /// <param name="createdAt">Creation timestamp for all documents. Defaults to 2025-01-10 12:00 UTC.</param>
    public static Dictionary<string, ContextDocument> CreateMatchContextDocuments(
        Option<string> homeAbbreviation = default,
        Option<string> awayAbbreviation = default,
        Option<string> communityContext = default,
        Option<DateTimeOffset> createdAt = default)
    {
        var home = homeAbbreviation.Or("fcb");
        var away = awayAbbreviation.Or("bvb");
        var context = communityContext.Or("test-community");
        var timestamp = createdAt.Or(() => new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero));

        return new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50",
                createdAt: timestamp),
            [$"community-rules-{context}.md"] = CreateContextDocument(
                documentName: $"community-rules-{context}.md",
                content: "# Community Rules",
                createdAt: timestamp),
            [$"recent-history-{home}.csv"] = CreateContextDocument(
                documentName: $"recent-history-{home}.csv",
                content: "Match,Result\n1,W",
                createdAt: timestamp),
            [$"recent-history-{away}.csv"] = CreateContextDocument(
                documentName: $"recent-history-{away}.csv",
                content: "Match,Result\n1,L",
                createdAt: timestamp),
            [$"home-history-{home}.csv"] = CreateContextDocument(
                documentName: $"home-history-{home}.csv",
                content: "Match,Result\n1,W",
                createdAt: timestamp),
            [$"away-history-{away}.csv"] = CreateContextDocument(
                documentName: $"away-history-{away}.csv",
                content: "Match,Result\n1,L",
                createdAt: timestamp),
            [$"head-to-head-{home}-vs-{away}.csv"] = CreateContextDocument(
                documentName: $"head-to-head-{home}-vs-{away}.csv",
                content: "Match,Score\n1,2-1",
                createdAt: timestamp)
        };
    }

    /// <summary>
    /// Creates a mock <see cref="IKpiRepository"/> configured for upload operations.
    /// </summary>
    /// <param name="existingDocument">Document returned by GetKpiDocumentAsync. Defaults to null (no existing document).</param>
    /// <param name="savedVersion">Version returned by SaveKpiDocumentAsync. Defaults to 0.</param>
    public static Mock<IKpiRepository> CreateMockKpiRepositoryForUpload(
        NullableOption<KpiDocument> existingDocument = default,
        Option<int> savedVersion = default)
    {
        var mock = new Mock<IKpiRepository>();

        mock.Setup(r => r.GetKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument.Or((KpiDocument?)null));

        mock.Setup(r => r.SaveKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedVersion.Or(0));

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IFileProvider"/> configured with the specified KPI document JSON files.
    /// </summary>
    /// <param name="files">Dictionary mapping relative file paths (e.g., "output/community/doc.json") to file contents.</param>
    /// <returns>A configured mock IFileProvider.</returns>
    public static Mock<IFileProvider> CreateMockKpiFileProvider(Dictionary<string, string> files)
    {
        return MockFileProviderHelpers.CreateMockFileProvider(files);
    }

    /// <summary>
    /// Creates a JSON string representing a KPI document file.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-document".</param>
    /// <param name="content">Document content. Defaults to "test content".</param>
    /// <param name="description">Document description. Defaults to "test description".</param>
    /// <param name="communityContext">Community context. Defaults to "test-community".</param>
    public static string CreateKpiDocumentJson(
        Option<string> documentName = default,
        Option<string> content = default,
        Option<string> description = default,
        Option<string> communityContext = default)
    {
        var name = documentName.Or("test-document");
        var contentValue = content.Or("test content");
        var desc = description.Or("test description");
        var context = communityContext.Or("test-community");

        return $$"""
            {
                "documentName": "{{name}}",
                "content": "{{contentValue}}",
                "description": "{{desc}}",
                "communityContext": "{{context}}"
            }
            """;
    }

    /// <summary>
    /// Creates a JSON string representing a transfers document file.
    /// </summary>
    /// <param name="documentName">Document name. Defaults to "test-transfers.csv".</param>
    /// <param name="content">Document content. Defaults to "test transfers content".</param>
    /// <param name="description">Document description. Defaults to "test transfers description".</param>
    /// <param name="communityContext">Community context. Defaults to "test-community".</param>
    public static string CreateTransfersDocumentJson(
        Option<string> documentName = default,
        Option<string> content = default,
        Option<string> description = default,
        Option<string> communityContext = default)
    {
        var name = documentName.Or("test-transfers.csv");
        var contentValue = content.Or("test transfers content");
        var desc = description.Or("test transfers description");
        var context = communityContext.Or("test-community");

        return $$"""
            {
                "documentName": "{{name}}",
                "content": "{{contentValue}}",
                "description": "{{desc}}",
                "communityContext": "{{context}}"
            }
            """;
    }

    /// <summary>
    /// Creates a mock <see cref="IFileProvider"/> configured with the specified transfers document JSON files.
    /// </summary>
    /// <param name="files">Dictionary mapping relative file paths (e.g., "output/community/doc.json") to file contents.</param>
    /// <returns>A configured mock IFileProvider.</returns>
    public static Mock<IFileProvider> CreateMockTransfersFileProvider(Dictionary<string, string> files)
    {
        return MockFileProviderHelpers.CreateMockFileProvider(files);
    }
}
