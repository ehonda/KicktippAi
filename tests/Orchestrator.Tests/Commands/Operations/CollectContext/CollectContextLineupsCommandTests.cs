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
            configureServices: ConfigureLineupServices(lineupSource));

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
            configureServices: ConfigureLineupServices(lineupSource));

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
            configureServices: ConfigureLineupServices(CreateMockLineupSource()));

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
            configureServices: ConfigureLineupServices(lineupSource));

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

    [Test]
    public async Task Running_command_updates_data_collected_at_only_for_changed_lineup_rows()
    {
        const string currentExamplelandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
            "Exampleland,2026-05-25,Coach,Coach One,,Coach,\r\n" +
            "Exampleland,2026-05-25,Player,Player One,25,Forward,15.000.000\r\n" +
            "Exampleland,2026-05-25,Player,New Player,22,Midfield,N/A\r\n";
        const string existingExamplelandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
            "Exampleland,2026-06-01,Coach,Coach One,,Coach,\r\n" +
            "Exampleland,2026-06-01,Player,Player One,25,Forward,14.000.000\r\n";
        const string expectedExamplelandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
            "Exampleland,2026-06-01,Coach,Coach One,,Coach,\r\n" +
            "Exampleland,2026-06-14,Player,Player One,25,Forward,15.000.000\r\n" +
            "Exampleland,2026-05-25,Player,New Player,22,Midfield,N/A\r\n";

        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                "lineup-exampleland.csv",
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                "lineup-exampleland.csv",
                existingExamplelandContent,
                3,
                new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)));
        contextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                "lineup-missingland.csv",
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);
        contextRepository
            .Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 2);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var lineupSource = CreateMockLineupSource(currentExamplelandContent);
        var fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 6, 14, 7, 0, 0, TimeSpan.Zero));
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: ConfigureLineupServices(lineupSource, fixedTimeProvider));

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "lineup-exampleland.csv",
                expectedExamplelandContent,
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
                expectedExamplelandContent,
                It.Is<string>(description => description.Contains("WM26 lineups")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Running_command_fails_before_writing_when_existing_lineup_csv_is_malformed()
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository
            .Setup(r => r.GetLatestContextDocumentAsync(
                "lineup-exampleland.csv",
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                "lineup-exampleland.csv",
                "Team,Role,Name\r\nExampleland,Player,Player One\r\n",
                3,
                new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)));
        var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 2);
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            kpiRepository: kpiRepository,
            contextRepository: contextRepository);
        var lineupSource = CreateMockLineupSource();
        var (app, console) = CreateCommandApp<CollectContextLineupsCommand>(
            "collect-context-lineups",
            firebaseServiceFactory: firebaseFactory,
            configureServices: ConfigureLineupServices(lineupSource));

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-lineups",
            "--community-context",
            "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Existing lineup context document lineup-exampleland.csv is malformed");
        VerifyNoWrites(contextRepository, kpiRepository);
    }

    private static Action<IServiceCollection> ConfigureLineupServices(
        Mock<IWm26LineupSource> lineupSource,
        TimeProvider? timeProvider = null)
    {
        return services =>
        {
            services.AddSingleton(lineupSource.Object);
            services.AddSingleton(timeProvider ?? TimeProvider.System);
        };
    }

    private static Mock<IWm26LineupSource> CreateMockLineupSource(string? examplelandContent = null)
    {
        var mock = new Mock<IWm26LineupSource>();
        mock
            .Setup(source => source.CollectAsync(It.IsAny<Wm26LineupSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLineupCollection(examplelandContent));
        return mock;
    }

    private static Wm26LineupCollection CreateLineupCollection(string? examplelandContent = null)
    {
        const string defaultExamplelandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n" +
            "Exampleland,2026-05-25,Coach,Coach One,,Coach,\r\n" +
            "Exampleland,2026-05-25,Player,Player One,25,Forward,15.000.000\r\n";
        const string missinglandContent =
            "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\r\n";
        var resolvedExamplelandContent = examplelandContent ?? defaultExamplelandContent;

        return new Wm26LineupCollection(
            "seed.csv",
            "teams.csv",
            "transfermarkt-datasets.duckdb",
            2,
            2,
            [
                new("lineup-exampleland.csv", resolvedExamplelandContent, "Exampleland", 1, false),
                new("lineup-missingland.csv", missinglandContent, "Missingland", 0, true)
            ],
            resolvedExamplelandContent,
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
