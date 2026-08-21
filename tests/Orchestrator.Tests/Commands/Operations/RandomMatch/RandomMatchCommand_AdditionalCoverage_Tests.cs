using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using FirebaseAdapter;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.RandomMatch;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.RandomMatch;

public class RandomMatchCommand_AdditionalCoverage_Tests
{
    [Test]
    public async Task With_justification_outputs_reasoning_details()
    {
        var prediction = CreatePrediction(
            homeGoals: 2,
            awayGoals: 1,
            justification: CreatePredictionJustification(
                keyReasoning: "Strong home form",
                contextSources: CreatePredictionJustificationContextSources(
                    mostValuable: new List<PredictionJustificationContextSource>
                    {
                        CreatePredictionJustificationContextSource(
                            documentName: "home-history-fcb.csv",
                            details: "Strong home record")
                    }),
                uncertainties: new List<string> { "Late injury concerns" }));

        var ctx = CreateRandomMatchCommandApp(predictionResult: prediction);

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "random-match",
            "gpt-5",
            "-c",
            "test-community",
            "--with-justification");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Justification output enabled");
        await Assert.That(output).Contains("Key reasoning:");
        await Assert.That(output).Contains("Strong home form");
    }

    [Test]
    public async Task World_cup_hosted_match_prompt_with_justification_remains_fail_closed()
    {
        var ctx = CreateRandomMatchCommandApp();

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "random-match",
            "gpt-5-nano",
            "-c",
            "ehonda-dev-wm26",
            "--with-justification");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("WM 2026 hosted match prompt").And.Contains("justification");
    }

    [Test]
    public async Task Prediction_service_exception_returns_error()
    {
        var mockPredictionService = new Mock<IPredictionService>();
        mockPredictionService
            .Setup(service => service.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/gpt-5/match.md");
        mockPredictionService
            .Setup(service => service.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Prediction failed"));

        var ctx = CreateRandomMatchCommandApp(predictionService: mockPredictionService);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Error:");
        await Assert.That(output).Contains("Prediction failed");
    }

    [Test]
    public async Task No_matches_found_exits_early_with_message()
    {
        var ctx = CreateRandomMatchCommandApp(matchesWithHistory: new List<MatchWithHistory>());

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No matches found for current matchday");
    }

    [Test]
    public async Task Cancelled_match_with_failed_prediction_shows_warning()
    {
        var cancelledMatch = CreateMatch(
            homeTeam: "FC Bayern München",
            awayTeam: "Borussia Dortmund",
            matchday: 25,
            isCancelled: true);

        var ctx = CreateRandomMatchCommandApp(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: cancelledMatch) },
            predictionResult: null);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("is cancelled");
        await Assert.That(output).Contains("Failed to generate prediction");
    }

    [Test]
    public async Task All_required_documents_in_database_use_database_only_context()
    {
        var docs = CreateMatchContextDocuments();

        var ctx = CreateRandomMatchCommandApp(contextDocuments: docs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using 11 context documents");
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(),
            It.Is<IEnumerable<DocumentContext>>(documents =>
                documents.Any(document => document.Name == "roster-fcb") &&
                documents.Any(document => document.Name == "club-elo-bvb.csv")),
            It.IsAny<bool>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Incomplete_on_demand_context_fails_closed()
    {
        var partialDocs = new Dictionary<string, ContextDocument>
        {
            ["bundesliga-standings.csv"] = CreateContextDocument(
                documentName: "bundesliga-standings.csv",
                content: "Position,Team,Points\n1,Bayern,50")
        };

        var onDemandDocs = new List<DocumentContext>
        {
            new("bundesliga-standings.csv", "duplicate"),
            new("recent-history-fcb.csv", "new content")
        };

        var ctx = CreateRandomMatchCommandApp(
            contextDocuments: partialDocs,
            onDemandContextDocuments: onDemandDocs);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Missing required Bundesliga context document");
    }

    [Test]
    public async Task Required_document_repository_failure_fails_closed()
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .SetupSequence(repo => repo.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var ctx = CreateRandomMatchCommandApp(contextRepository: contextRepository);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Database unavailable");
    }

    [Test]
    public async Task Required_document_lookup_exceptions_are_reported()
    {
        var docs = CreateMatchContextDocuments();
        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(repo => repo.GetLatestContextDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string documentName, string _, CancellationToken _) =>
            {
                if (documentName == "bundesliga-standings.csv")
                {
                    throw new InvalidOperationException("Required lookup failed");
                }

                return docs.GetValueOrDefault(documentName);
            });

        var ctx = CreateRandomMatchCommandApp(contextRepository: contextRepository);

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "random-match", "gpt-5", "-c", "test-community");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("Required lookup failed");
        ctx.PredictionService.Verify(service => service.PredictMatchAsync(
            It.IsAny<Match>(),
            It.IsAny<IEnumerable<DocumentContext>>(),
            It.IsAny<bool>(),
            It.IsAny<PredictionTelemetryMetadata?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RandomMatchCommandTestContext CreateRandomMatchCommandApp(
        Option<TestConsole> console = default,
        Option<List<MatchWithHistory>> matchesWithHistory = default,
        Option<Dictionary<string, ContextDocument>> contextDocuments = default,
        NullableOption<Prediction> predictionResult = default,
        Option<List<DocumentContext>> onDemandContextDocuments = default,
        Option<Mock<IContextRepository>> contextRepository = default,
        Option<Mock<IPredictionService>> predictionService = default)
    {
        var testConsole = console.Or(() => new TestConsole());
        var matches = matchesWithHistory.Or(() => new List<MatchWithHistory>
        {
            CreateMatchWithHistory(match: CreateMatch(
                homeTeam: "FC Bayern München",
                awayTeam: "Borussia Dortmund",
                matchday: 25))
        });
        var docs = contextDocuments.Or(() => CreateMatchContextDocuments());
        var onDemandDocs = onDemandContextDocuments.Or(() => []);

        var mockKicktippClient = CreateMockKicktippClient(matchesWithHistory: matches);
        var mockKicktippFactory = CreateMockKicktippClientFactory(mockKicktippClient);

        var mockContextRepository = contextRepository.Or(() => CreateMockContextRepositoryWithDocuments(docs));
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: mockContextRepository);

        var mockPredictionService = predictionService.Or(() => CreateMockPredictionService(
            predictMatchResult: predictionResult.Or(() => CreatePrediction())));
        var mockTokenUsageTracker = CreateMockTokenUsageTracker();
        var mockOpenAiFactory = CreateMockOpenAiServiceFactory(
            predictionService: mockPredictionService,
            tokenUsageTracker: mockTokenUsageTracker);

        var mockContextProvider = CreateMockKicktippContextProvider(matchContextDocuments: onDemandDocs);
        var mockContextProviderFactory = CreateMockContextProviderFactory(contextProvider: mockContextProvider);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(mockFirebaseFactory.Object);
        services.AddSingleton(mockKicktippFactory.Object);
        services.AddSingleton(mockOpenAiFactory.Object);
        services.AddSingleton(mockContextProviderFactory.Object);
        services.AddSingleton(Mock.Of<ILangfusePublicApiClient>());
        services.AddSingleton<ILogger<RandomMatchCommand>>(new FakeLogger<RandomMatchCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<RandomMatchCommand>("random-match");
        });

        return new RandomMatchCommandTestContext(app, testConsole, mockContextRepository, mockPredictionService);
    }

    private sealed record RandomMatchCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IContextRepository> ContextRepository,
        Mock<IPredictionService> PredictionService);
}
