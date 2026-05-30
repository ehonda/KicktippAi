using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextLineupsCommandTests
{
    [Test]
    public async Task Running_command_uploads_per_team_lineup_context_documents_and_kpi_document()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var lineupSource = CreateMockLineupSource();
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(lineupSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("WM26 lineup context collection completed");
        await Assert.That(output).Contains("Header-only lineup context payloads");

        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "lineup-exampleland.csv",
                It.Is<string>(content => content.Contains("Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR") &&
                                         content.Contains("Exampleland,2026-05-25,Player,Player One,25,Forward,15.000.000")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "lineup-missingland.csv",
                "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n",
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
        kpiRepository.Verify(
            r => r.SaveKpiDocumentAsync(
                "lineups",
                It.Is<string>(content => content.Contains("Player One")),
                It.Is<string>(description => description.Contains("WM26 lineups")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_with_dry_run_collects_source_but_performs_no_firestore_writes()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var lineupSource = CreateMockLineupSource();
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(lineupSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
            "--community-context",
            "ehonda-dev-wm26",
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed");

        lineupSource.Verify(
            source => source.CollectAsync(It.IsAny<Wm26LineupSourceRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoWrites(contextRepository, kpiRepository);
    }

    [Test]
    public async Task Running_command_uses_fifa_world_cup_repository_scoping_for_wm26_context()
    {
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: CreateMockKpiRepositoryForUpload(savedVersion: 0),
            contextRepository: CreateMockContextRepositoryForUpload(savedVersion: 1));
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(CreateMockLineupSource().Object)));

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
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
    public async Task Running_command_fails_before_writing_when_source_validation_fails()
    {
        var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var lineupSource = new Mock<IWm26LineupSource>();
        lineupSource
            .Setup(source => source.CollectAsync(It.IsAny<Wm26LineupSourceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Lineup enrichment failed."));
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(lineupSource.Object)));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Lineup enrichment failed");
        VerifyNoWrites(contextRepository, kpiRepository);
    }

    private static Mock<IWm26LineupSource> CreateMockLineupSource()
    {
        var mock = new Mock<IWm26LineupSource>();
        mock
            .Setup(source => source.CollectAsync(It.IsAny<Wm26LineupSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLineupCollection());
        return mock;
    }

    private static Wm26LineupCollection CreateLineupCollection()
    {
        const string examplelandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
            "Exampleland,2026-05-25,Coach,Coach One,,Coach,\r\n" +
            "Exampleland,2026-05-25,Player,Player One,25,Forward,15.000.000\r\n";
        const string missinglandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n";

        return new Wm26LineupCollection(
            "seed.csv",
            "teams.csv",
            "transfermarkt-datasets.duckdb",
            2,
            2,
            [
                new("lineup-exampleland.csv", examplelandContent, "Exampleland", 1, false),
                new("lineup-missingland.csv", missinglandContent, "Missingland", 0, true)
            ],
            examplelandContent,
            [new("missingland", "Missingland")],
            []);
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
