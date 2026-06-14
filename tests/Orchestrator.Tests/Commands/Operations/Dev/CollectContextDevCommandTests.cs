using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NodaTime;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Commands.Operations.Wm26RecentHistory;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Services;
using Spectre.Console;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Dev;

public class CollectContextDevCommandTests
{
    [Test]
    public async Task Running_collect_context_dev_rejects_non_dev_communities()
    {
        var testContext = CreateCollectContextDevCommandApp();

        var (exitCode, output) = await RunCommandAsync(
            testContext.App,
            testContext.Console,
            "collect-context-dev",
            "--community",
            "ehonda-test-buli");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("only available");
        testContext.KicktippClientFactory.Verify(f => f.CreateClient(), Times.Never);
        testContext.FirebaseServiceFactory.Verify(f => f.CreateContextRepository(It.IsAny<string?>()), Times.Never);
        testContext.FirebaseServiceFactory.Verify(f => f.CreateKpiRepository(It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task Running_collect_context_dev_for_wm26_calls_kicktipp_date_map_fifa_and_lineup_collection_paths()
    {
        var testContext = CreateCollectContextDevCommandApp();
        var dateMapPath = CreateTempDateMap();

        var (exitCode, output) = await RunCommandAsync(
            testContext.App,
            testContext.Console,
            "collect-context-dev",
            "--community",
            "ehonda-dev-wm26",
            "--recent-history-date-map",
            dateMapPath,
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Collect-context kicktipp command initialized");
        await Assert.That(output).Contains("Applying WM26 recent-history date map");
        await Assert.That(output).Contains("Collect-context fifa command initialized");
        await Assert.That(output).Contains("Collect-context lineups command initialized");

        testContext.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-kanada.csv",
                It.Is<string>(content =>
                    content.Contains("Data_Collected_At") &&
                    content.Contains("CopAm,202") &&
                    content.Contains("Kanada,Chile,0:0")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-kanada.csv",
                It.Is<string>(content =>
                    content.Contains("Competition,Played_At,Home_Team,Away_Team,Score,Annotation") &&
                    !content.Contains("Data_Collected_At") &&
                    content.Contains("CopAm,2024-06-29T20:00:00+02:00,Kanada,Chile,0:0,")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "fifa-ranking-mexiko.csv",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.KpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                "fifa-rankings",
                It.Is<string>(content => content.Contains("Data_Collected_At")),
                It.IsAny<string>(),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "lineup-mexiko.csv",
                It.Is<string>(content => content.Contains("Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.KpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                "lineups",
                It.Is<string>(content => content.Contains("Player One")),
                It.IsAny<string>(),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        testContext.FirebaseServiceFactory.Verify(
            f => f.CreateContextRepository(CompetitionIds.FifaWorldCup2026),
            Times.AtLeast(4));
        testContext.FirebaseServiceFactory.Verify(
            f => f.CreateKpiRepository(CompetitionIds.FifaWorldCup2026),
            Times.Exactly(2));
    }

    [Test]
    public async Task Running_collect_context_dev_for_wm26_passes_matchdays_and_dry_run_to_kicktipp_collection()
    {
        var testContext = CreateCollectContextDevCommandApp();
        var dateMapPath = CreateTempDateMap();

        var (exitCode, output) = await RunCommandAsync(
            testContext.App,
            testContext.Console,
            "collect-context-dev",
            "--community",
            "ehonda-dev-wm26",
            "--matchdays",
            "2,3",
            "--recent-history-date-map",
            dateMapPath,
            "--dry-run",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode enabled");
        await Assert.That(output).Contains("Getting matchday 2 matches");
        await Assert.That(output).Contains("Getting matchday 3 matches");

        testContext.KicktippClient.Verify(
            c => c.GetMatchesWithHistoryAsync("ehonda-dev-wm26", 2),
            Times.Once);
        testContext.KicktippClient.Verify(
            c => c.GetMatchesWithHistoryAsync("ehonda-dev-wm26", 3),
            Times.Once);
        testContext.ContextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        testContext.KpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CollectContextDevCommandTestContext CreateCollectContextDevCommandApp()
    {
        var contextRepository = CreateInMemoryContextRepository();
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var matchOutcomeRepository = CreateMockMatchOutcomeRepository();
        var predictionRepository = CreateMockPredictionRepository(
            getLatestPredictedMatchByTeamsResult: CreateMatch(
                homeTeam: "Kanada",
                awayTeam: "Chile",
                startsAt: Instant.FromUtc(2024, 6, 29, 18, 0).InUtc()));
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            predictionRepository: predictionRepository,
            contextRepository: contextRepository,
            matchOutcomeRepository: matchOutcomeRepository);

        var match = CreateMatch(homeTeam: "Mexiko", awayTeam: "Sudafrika", matchday: 1);
        var kicktippClient = CreateMockKicktippClient(
            matchesWithHistory: new List<MatchWithHistory> { CreateMatchWithHistory(match: match) });
        var kicktippClientFactory = CreateMockKicktippClientFactory(kicktippClient);
        var contextProvider = CreateMockKicktippContextProvider(
            matchContextDocuments: new List<DocumentContext>
            {
                new(
                    "recent-history-kanada.csv",
                    "Competition,Home_Team,Away_Team,Score,Annotation\nCopAm,Kanada,Chile,0:0,")
            });
        var contextProviderFactory = CreateMockContextProviderFactory(contextProvider);
        var fifaRankingSource = new Mock<IFifaRankingSource>();
        fifaRankingSource
            .Setup(source => source.CollectLatestAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FifaRankingCollection(
                "FRS_Male_Football_20260119",
                new DateTimeOffset(2026, 4, 1, 11, 55, 29, TimeSpan.Zero),
                new DateOnly(2026, 5, 25),
                211,
                [
                    new(
                        "fifa-ranking-mexiko.csv",
                        "Rank,Team,ELO,Data_Collected_At\n15,Mexiko,1681.03,2026-05-25\n",
                        "Mexiko",
                        15,
                        1681.03m)
                ],
                "Rank,Team,ELO,Data_Collected_At\n15,Mexiko,1681.03,2026-05-25\n"));
        var lineupSource = new Mock<IWm26LineupSource>();
        lineupSource
            .Setup(source => source.CollectAsync(It.IsAny<Wm26LineupSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wm26LineupCollection(
                "seed.csv",
                "teams.csv",
                "transfermarkt-datasets.duckdb",
                2,
                2,
                [
                    new(
                        "lineup-mexiko.csv",
                        "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\nMexiko,2026-05-25,Player,Player One,25,Forward,1.000.000\r\n",
                        "Mexiko",
                        1,
                        false)
                ],
                "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\nMexiko,2026-05-25,Player,Player One,25,Forward,1.000.000\r\n",
                [],
                []));

        var (app, console) = CreateCommandApp<CollectContextDevCommand>(
            "collect-context-dev",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services =>
            {
                services.AddSingleton(kicktippClientFactory.Object);
                services.AddSingleton(contextProviderFactory.Object);
                services.AddSingleton(fifaRankingSource.Object);
                services.AddSingleton(lineupSource.Object);
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<ILogger<MatchOutcomeCollectionService>>(new FakeLogger<MatchOutcomeCollectionService>());
                services.AddSingleton<MatchOutcomeCollectionService>();
                services.AddSingleton<ILogger<CollectContextKicktippCommand>>(new FakeLogger<CollectContextKicktippCommand>());
                services.AddSingleton<ILogger<CollectContextFifaCommand>>(new FakeLogger<CollectContextFifaCommand>());
                services.AddSingleton<ILogger<CollectContextLineupsCommand>>(new FakeLogger<CollectContextLineupsCommand>());
                services.AddSingleton<ILogger<Wm26RecentHistoryApplyDateMapCommand>>(new FakeLogger<Wm26RecentHistoryApplyDateMapCommand>());
            }));

        return new CollectContextDevCommandTestContext(
            app,
            console,
            firebaseFactory,
            kicktippClientFactory,
            kicktippClient,
            contextRepository,
            kpiRepository);
    }

    private static Mock<IContextRepository> CreateInMemoryContextRepository()
    {
        var documents = new Dictionary<string, ContextDocument>(StringComparer.OrdinalIgnoreCase);
        var versions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var mock = new Mock<IContextRepository>();

        mock.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => documents.Keys.ToList());

        mock.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string documentName, string _, CancellationToken _) =>
                documents.TryGetValue(documentName, out var document) ? document : null);

        mock.Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string documentName, string content, string _, CancellationToken _) =>
            {
                var version = versions.TryGetValue(documentName, out var currentVersion)
                    ? currentVersion + 1
                    : 1;
                versions[documentName] = version;
                documents[documentName] = new ContextDocument(
                    documentName,
                    content,
                    version,
                    DateTimeOffset.UtcNow);
                return version;
            });

        return mock;
    }

    private static string CreateTempDateMap()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "KicktippAi",
            "collect-context-dev-tests",
            $"{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-kanada.csv,CopAm,Kanada,Chile,0:0,,2024-06-29,Test,https://example.test,2026-06-07,
            """.Replace("\r\n", "\n"));
        return path;
    }

    private sealed record CollectContextDevCommandTestContext(
        Spectre.Console.Cli.CommandApp App,
        Spectre.Console.Testing.TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory,
        Mock<IKicktippClientFactory> KicktippClientFactory,
        Mock<IKicktippClient> KicktippClient,
        Mock<IContextRepository> ContextRepository,
        Mock<IKpiRepository> KpiRepository);
}
