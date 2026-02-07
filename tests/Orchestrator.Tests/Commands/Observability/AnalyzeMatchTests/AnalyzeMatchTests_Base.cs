using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Commands.Observability.AnalyzeMatch;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

/// <summary>
/// Contains the CommandApp, TestConsole, and all mocks for an analyze-match command test.
/// </summary>
public record AnalyzeMatchTestContext(
    CommandApp App,
    TestConsole Console,
    Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
    Mock<IKicktippClientFactory> KicktippClientFactory,
    Mock<IOpenAiServiceFactory> OpenAiServiceFactory,
    Mock<IKicktippClient> KicktippClient,
    Mock<IContextRepository> ContextRepository,
    Mock<IPredictionService> PredictionService,
    Mock<ITokenUsageTracker> TokenUsageTracker);

/// <summary>
/// Base class for AnalyzeMatch command tests providing shared test infrastructure.
/// </summary>
public abstract class AnalyzeMatchTests_Base
{
    // Well-known team names that map to abbreviations in AnalyzeMatchCommandHelpers
    protected const string HomeTeam = "FC Bayern München";
    protected const string AwayTeam = "Borussia Dortmund";
    protected const string HomeAbbreviation = "fcb";
    protected const string AwayAbbreviation = "bvb";
    protected const string DefaultCommunityContext = "test-community";
    protected const string DefaultModel = "gpt-5-nano";
    protected const int DefaultMatchday = 25;

    /// <summary>
    /// Creates a configured command app for testing an analyze-match command.
    /// </summary>
    /// <typeparam name="TCommand">The command type (detailed or comparison).</typeparam>
    /// <param name="commandName">The command name to register.</param>
    /// <param name="contextDocuments">Documents returned by the context repository. Defaults to standard set.</param>
    /// <param name="matchesWithHistory">Matches returned by the Kicktipp client. Defaults to standard match.</param>
    /// <param name="predictionResult">Prediction returned by PredictMatchAsync. Defaults to 2:1.</param>
    /// <param name="firebaseServiceFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="kicktippClientFactory">Pre-configured mock (overrides domain params).</param>
    /// <param name="openAiServiceFactory">Pre-configured mock (overrides domain params).</param>
    protected static AnalyzeMatchTestContext CreateAnalyzeMatchCommandApp<TCommand>(
        string commandName,
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        NullableOption<Prediction> predictionResult = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default)
        where TCommand : class, ICommand
    {
        var testConsole = new TestConsole();

        var docs = contextDocuments.Or(CreateDefaultContextDocuments);
        var matches = matchesWithHistory.Or(() => new List<MatchWithHistory> { CreateDefaultMatchWithHistory() });

        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: matches);
        var mockContextRepository = CreateMockContextRepositoryWithDocuments(docs);
        var mockPredictionService = CreateMockPredictionService(
            predictMatchResult: predictionResult.Or(() => CreatePrediction()));
        var mockTokenUsageTracker = CreateMockTokenUsageTracker();

        var mockFirebaseFactory = firebaseServiceFactory.Or(() =>
            CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository));
        var mockKicktippFactory = kicktippClientFactory.Or(() =>
            CreateMockKicktippClientFactory(mockKicktippClient));
        var mockOpenAiFactory = openAiServiceFactory.Or(() =>
            CreateMockOpenAiServiceFactory(
                predictionService: mockPredictionService,
                tokenUsageTracker: mockTokenUsageTracker));

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockOpenAiFactory.Object);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<TCommand>(commandName);
        });

        return new AnalyzeMatchTestContext(
            app,
            testConsole,
            mockFirebaseFactory,
            mockKicktippFactory,
            mockOpenAiFactory,
            mockKicktippClient,
            mockContextRepository,
            mockPredictionService,
            mockTokenUsageTracker);
    }

    /// <summary>
    /// Creates a configured command app for testing AnalyzeMatchDetailedCommand.
    /// </summary>
    protected static AnalyzeMatchTestContext CreateDetailedCommandApp(
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        NullableOption<Prediction> predictionResult = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default)
    {
        return CreateAnalyzeMatchCommandApp<AnalyzeMatchDetailedCommand>(
            "detailed",
            contextDocuments,
            matchesWithHistory,
            predictionResult,
            firebaseServiceFactory,
            kicktippClientFactory,
            openAiServiceFactory);
    }

    /// <summary>
    /// Creates a configured command app for testing AnalyzeMatchComparisonCommand.
    /// </summary>
    protected static AnalyzeMatchTestContext CreateComparisonCommandApp(
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        NullableOption<Prediction> predictionResult = default,
        Option<Mock<IFirebaseServiceFactory>> firebaseServiceFactory = default,
        Option<Mock<IKicktippClientFactory>> kicktippClientFactory = default,
        Option<Mock<IOpenAiServiceFactory>> openAiServiceFactory = default)
    {
        return CreateAnalyzeMatchCommandApp<AnalyzeMatchComparisonCommand>(
            "comparison",
            contextDocuments,
            matchesWithHistory,
            predictionResult,
            firebaseServiceFactory,
            kicktippClientFactory,
            openAiServiceFactory);
    }

    /// <summary>
    /// Runs the detailed command with default args plus any extra args.
    /// </summary>
    protected static async Task<(int ExitCode, string Output)> RunDetailedAsync(
        AnalyzeMatchTestContext context,
        params string[] extraArgs)
    {
        var args = BuildArgs("detailed", extraArgs);
        return await RunCommandAsync(context.App, context.Console, args);
    }

    /// <summary>
    /// Runs the comparison command with default args plus any extra args.
    /// </summary>
    protected static async Task<(int ExitCode, string Output)> RunComparisonAsync(
        AnalyzeMatchTestContext context,
        params string[] extraArgs)
    {
        var args = BuildArgs("comparison", extraArgs);
        return await RunCommandAsync(context.App, context.Console, args);
    }

    /// <summary>
    /// Builds args for a command: commandName, model, then required options, then extra args.
    /// </summary>
    private static string[] BuildArgs(string commandName, string[] extraArgs)
    {
        var args = new List<string>
        {
            commandName,
            DefaultModel,
            "--community-context", DefaultCommunityContext,
            "--home", HomeTeam,
            "--away", AwayTeam,
            "--matchday", DefaultMatchday.ToString()
        };
        args.AddRange(extraArgs);
        return args.ToArray();
    }

    /// <summary>
    /// Creates the default match used in tests (Bayern vs Dortmund).
    /// </summary>
    protected static Match CreateDefaultMatch()
    {
        return CreateMatch(
            homeTeam: HomeTeam,
            awayTeam: AwayTeam,
            matchday: DefaultMatchday,
            startsAt: Instant.FromUtc(2025, 3, 15, 15, 30).InUtc());
    }

    /// <summary>
    /// Creates a MatchWithHistory for the default test match.
    /// </summary>
    protected static MatchWithHistory CreateDefaultMatchWithHistory()
    {
        return CreateMatchWithHistory(match: CreateDefaultMatch());
    }

    /// <summary>
    /// Creates the standard set of context documents for Bayern vs Dortmund.
    /// </summary>
    protected static Dictionary<string, ContextDocument> CreateDefaultContextDocuments()
    {
        return CreateMatchContextDocuments(
            homeAbbreviation: HomeAbbreviation,
            awayAbbreviation: AwayAbbreviation,
            communityContext: DefaultCommunityContext);
    }

    /// <summary>
    /// Creates a prediction with a justification for testing justification display.
    /// </summary>
    protected static Prediction CreatePredictionWithJustification()
    {
        return CreatePrediction(
            homeGoals: 2,
            awayGoals: 1,
            justification: CreatePredictionJustification(
                keyReasoning: "Bayern has strong home form",
                contextSources: CreatePredictionJustificationContextSources(
                    mostValuable: new List<PredictionJustificationContextSource>
                    {
                        CreatePredictionJustificationContextSource(
                            documentName: "home-history-fcb.csv",
                            details: "Strong home record")
                    },
                    leastValuable: new List<PredictionJustificationContextSource>
                    {
                        CreatePredictionJustificationContextSource(
                            documentName: "head-to-head-fcb-vs-bvb.csv",
                            details: "Limited data")
                    }),
                uncertainties: new List<string> { "Injury concerns for key players" }));
    }
}
