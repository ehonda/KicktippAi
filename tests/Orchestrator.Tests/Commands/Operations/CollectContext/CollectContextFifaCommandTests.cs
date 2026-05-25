using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextFifaCommandTests
{
    [Test]
    public async Task Running_command_uploads_per_team_ranking_context_documents_and_kpi_document()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var fifaRankingSource = CreateMockFifaRankingSource();
        var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
            "collect-context-fifa",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(fifaRankingSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-fifa",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("FIFA ranking context collection completed");
        await Assert.That(output).Contains("FRS_Male_Football_20260119");
        await Assert.That(output).Contains("Mapped WM26 teams");

        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "fifa-ranking-mexiko.csv",
                It.Is<string>(content => content.Contains("Rank,Team,ELO,Data_Collected_At") &&
                                         content.Contains("15,Mexiko,1681.03,2026-05-25")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "fifa-ranking-sudafrika.csv",
                It.Is<string>(content => content.Contains("60,Südafrika,1429.73,2026-05-25")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        kpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                "fifa-rankings",
                It.Is<string>(content => content.Contains("Rank,Team,ELO,Data_Collected_At") &&
                                         content.Contains("15,Mexiko,1681.03,2026-05-25")),
                It.Is<string>(description => description.Contains("WM26 FIFA rankings")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_dry_run_fetches_live_source_but_performs_no_firestore_writes()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var fifaRankingSource = CreateMockFifaRankingSource();
        var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
            "collect-context-fifa",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(fifaRankingSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-fifa",
            "--community-context",
            "ehonda-dev-wm26",
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed");

        fifaRankingSource.Verify(
            source => source.CollectLatestAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Once);
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        kpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Running_command_uses_fifa_world_cup_repository_scoping_for_wm26_context()
    {
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: CreateMockKpiRepositoryForUpload(savedVersion: 0),
            contextRepository: CreateMockContextRepositoryForUpload(savedVersion: 1));
        var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
            "collect-context-fifa",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(CreateMockFifaRankingSource().Object)));

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-fifa",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        firebaseFactory.Verify(
            f => f.CreateContextRepository(CompetitionIds.FifaWorldCup2026),
            Times.Once);
        firebaseFactory.Verify(
            f => f.CreateKpiRepository(CompetitionIds.FifaWorldCup2026),
            Times.Once);
    }

    [Test]
    public async Task Running_command_fails_before_writing_when_live_source_validation_fails()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var fifaRankingSource = new Mock<IFifaRankingSource>();
        fifaRankingSource
            .Setup(source => source.CollectLatestAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FIFA ranking response is missing WM26 teams: MEX (Mexiko)."));
        var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
            "collect-context-fifa",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(fifaRankingSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-fifa",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("missing WM26 teams");
        VerifyNoWrites(contextRepository, kpiRepository);
    }

    private static Mock<IFifaRankingSource> CreateMockFifaRankingSource()
    {
        var mock = new Mock<IFifaRankingSource>();
        mock
            .Setup(source => source.CollectLatestAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFifaRankingCollection());
        return mock;
    }

    private static FifaRankingCollection CreateFifaRankingCollection()
    {
        return new FifaRankingCollection(
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
                    1681.03m),
                new(
                    "fifa-ranking-sudafrika.csv",
                    "Rank,Team,ELO,Data_Collected_At\n60,Südafrika,1429.73,2026-05-25\n",
                    "Südafrika",
                    60,
                    1429.73m)
            ],
            "Rank,Team,ELO,Data_Collected_At\n15,Mexiko,1681.03,2026-05-25\n60,Südafrika,1429.73,2026-05-25\n");
    }

    private static void VerifyNoWrites(
        Mock<IContextRepository> contextRepository,
        Mock<IKpiRepository> kpiRepository)
    {
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        kpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
