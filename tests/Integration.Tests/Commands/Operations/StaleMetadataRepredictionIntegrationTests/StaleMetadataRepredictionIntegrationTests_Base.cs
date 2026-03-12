using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using Integration.Tests.Infrastructure;
using KicktippIntegration;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using OpenAiIntegration;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console.Testing;
using TestUtilities;
using TUnit.Core;
using static TestUtilities.CoreTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Integration.Tests.Commands.Operations.StaleMetadataRepredictionIntegrationTests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.OrchestratorIntegrationParallelKey)]
public abstract class StaleMetadataRepredictionIntegrationTests_Base(FirestoreFixture fixture)
{
    protected const string Community = "ehonda-ai-arena";
    protected const string Model = "o4-mini";

    protected FirestoreFixture Fixture { get; } = fixture;

    [Before(Test)]
    public async Task ClearFirestoreAsync()
    {
        await Fixture.ClearOrchestratorIntegrationAsync();
    }

    protected static Match CreateRegressionMatch()
    {
        return CreateMatch(
            homeTeam: "Bayer 04 Leverkusen",
            awayTeam: "FC Bayern München",
            matchday: 26,
            startsAt: Instant.FromUtc(2026, 3, 14, 17, 30).InUtc());
    }

    protected static Mock<IKicktippClient> CreateKicktippClientMock(Match match, Prediction latestPrediction)
    {
        var kicktippClient = new Mock<IKicktippClient>();
        kicktippClient
            .Setup(client => client.GetPlacedPredictionsAsync(Community))
            .ReturnsAsync(new Dictionary<Match, BetPrediction?>
            {
                [match] = new BetPrediction(latestPrediction.HomeGoals, latestPrediction.AwayGoals)
            });
        kicktippClient
            .Setup(client => client.GetMatchesWithHistoryAsync(Community))
            .ReturnsAsync([
                CreateMatchWithHistory(match: match)
            ]);
        kicktippClient
            .Setup(client => client.PlaceBetsAsync(Community, It.IsAny<Dictionary<Match, BetPrediction>>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        return kicktippClient;
    }

    protected static Mock<IOpenAiServiceFactory> CreateOpenAiFactoryThatFailsOnPrediction(out Mock<IPredictionService> predictionService)
    {
        predictionService = new Mock<IPredictionService>();
        predictionService
            .Setup(service => service.GetMatchPromptPath(It.IsAny<bool>()))
            .Returns("prompts/match-prompt.md");
        predictionService
            .Setup(service => service.PredictMatchAsync(
                It.IsAny<Match>(),
                It.IsAny<IEnumerable<DocumentContext>>(),
                It.IsAny<bool>(),
                It.IsAny<PredictionTelemetryMetadata?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Prediction service must not run when the latest reprediction is up to date."));

        var tokenUsageTracker = new Mock<ITokenUsageTracker>();
        tokenUsageTracker.Setup(tracker => tracker.Reset());
        tokenUsageTracker.Setup(tracker => tracker.GetCompactSummary()).Returns("0/0/0/0/$0.00");
        tokenUsageTracker.Setup(tracker => tracker.GetCompactSummaryWithEstimatedCosts(It.IsAny<string>())).Returns("0/0/0/0/$0.00");
        tokenUsageTracker.Setup(tracker => tracker.GetLastUsageCompactSummary()).Returns("0/0/0/0/$0.00");
        tokenUsageTracker.Setup(tracker => tracker.GetLastUsageCompactSummaryWithEstimatedCosts(It.IsAny<string>())).Returns("0/0/0/0/$0.00");
        tokenUsageTracker.Setup(tracker => tracker.GetLastCost()).Returns(0.0m);
        tokenUsageTracker.Setup(tracker => tracker.GetLastUsageJson()).Returns("{}");
        tokenUsageTracker.Setup(tracker => tracker.GetTotalCost()).Returns(0.0m);

        var openAiFactory = new Mock<IOpenAiServiceFactory>();
        openAiFactory
            .Setup(factory => factory.CreatePredictionService(Model))
            .Returns(predictionService.Object);
        openAiFactory
            .Setup(factory => factory.GetTokenUsageTracker())
            .Returns(tokenUsageTracker.Object);

        return openAiFactory;
    }

    protected static Mock<IContextProviderFactory> CreateContextProviderFactory()
    {
        var kicktippContextProvider = new Mock<IKicktippContextProvider>();
        kicktippContextProvider
            .Setup(provider => provider.GetMatchContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<DocumentContext>());

        var kpiContextProvider = new Mock<IKpiContextProvider>();
        kpiContextProvider
            .Setup(provider => provider.GetContextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<DocumentContext>());
        kpiContextProvider
            .Setup(provider => provider.GetBonusQuestionContextAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<DocumentContext>());

        var contextProviderFactory = new Mock<IContextProviderFactory>();
        contextProviderFactory
            .Setup(factory => factory.CreateKicktippContextProvider(
                It.IsAny<IKicktippClient>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .Returns(kicktippContextProvider.Object);
        contextProviderFactory
            .Setup(factory => factory.CreateKpiContextProvider())
            .Returns(kpiContextProvider.Object);

        return contextProviderFactory;
    }

    protected OrchestratorIntegrationTestFactories.OrchestratorIntegrationTestContext CreateIntegrationContext(
        Mock<IKicktippClient> kicktippClient,
        Mock<IOpenAiServiceFactory> openAiServiceFactory,
        Mock<IContextProviderFactory> contextProviderFactory,
        TestConsole? console = null)
    {
        var firebaseServiceFactory = new TestFirebaseServiceFactory(Fixture.Db);

        var kicktippClientFactory = new Mock<IKicktippClientFactory>();
        kicktippClientFactory
            .Setup(factory => factory.CreateClient())
            .Returns(kicktippClient.Object);

        return OrchestratorIntegrationTestFactories.CreateCommandApp(
            firebaseServiceFactory,
            kicktippClientFactory,
            openAiServiceFactory,
            contextProviderFactory,
            console);
    }
}
