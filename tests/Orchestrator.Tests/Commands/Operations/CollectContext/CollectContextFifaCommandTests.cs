using EHonda.KicktippAi.Core;
using EHonda.Optional.Core;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextFifaCommandTests
{
    [Test]
    public async Task Running_command_uploads_per_team_ranking_context_documents_and_kpi_document()
    {
        var sourceRoot = CreateTempFifaSourceRoot();
        try
        {
            var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
            var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                kpiRepository: kpiRepository,
                contextRepository: contextRepository);
            var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
                "collect-context-fifa",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-fifa",
                "--community-context",
                "ehonda-dev-wm26",
                "--source-root",
                sourceRoot);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("FIFA ranking context collection completed");

            contextRepository.Verify(
                r => r.SaveContextDocumentAsync(
                    "fifa-ranking-mexiko.csv",
                    It.Is<string>(content => content.Contains("Mexiko,2026-05-24,15,1681.03")),
                    "ehonda-dev-wm26",
                    It.IsAny<CancellationToken>()),
                Times.Once);
            contextRepository.Verify(
                r => r.SaveContextDocumentAsync(
                    "fifa-ranking-sudafrika.csv",
                    It.Is<string>(content => content.Contains("Sudafrika,2026-05-24,56,1412.69")),
                    "ehonda-dev-wm26",
                    It.IsAny<CancellationToken>()),
                Times.Once);
            kpiRepository.Verify(
                r => r.SaveKpiDocumentAsync(
                    "fifa-rankings",
                    It.Is<string>(content => content.Contains("Mexiko,2026-05-24,15,1681.03")),
                    It.Is<string>(description => description.Contains("WM26 FIFA rankings")),
                    "ehonda-dev-wm26",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            DeleteTempDirectory(sourceRoot);
        }
    }

    [Test]
    public async Task Running_command_with_dry_run_performs_no_firestore_writes()
    {
        var sourceRoot = CreateTempFifaSourceRoot();
        try
        {
            var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
            var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                kpiRepository: kpiRepository,
                contextRepository: contextRepository);
            var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
                "collect-context-fifa",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-fifa",
                "--community-context",
                "ehonda-dev-wm26",
                "--source-root",
                sourceRoot,
                "--dry-run");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Dry run completed");

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
        finally
        {
            DeleteTempDirectory(sourceRoot);
        }
    }

    [Test]
    public async Task Running_command_uses_fifa_world_cup_repository_scoping_for_wm26_context()
    {
        var sourceRoot = CreateTempFifaSourceRoot();
        try
        {
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                kpiRepository: CreateMockKpiRepositoryForUpload(savedVersion: 0),
                contextRepository: CreateMockContextRepositoryForUpload(savedVersion: 1));
            var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
                "collect-context-fifa",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, _) = await RunCommandAsync(
                app,
                console,
                "collect-context-fifa",
                "--community-context",
                "ehonda-dev-wm26",
                "--source-root",
                sourceRoot);

            await Assert.That(exitCode).IsEqualTo(0);
            firebaseFactory.Verify(
                f => f.CreateContextRepository(CompetitionIds.FifaWorldCup2026),
                Times.Once);
            firebaseFactory.Verify(
                f => f.CreateKpiRepository(CompetitionIds.FifaWorldCup2026),
                Times.Once);
        }
        finally
        {
            DeleteTempDirectory(sourceRoot);
        }
    }

    [Test]
    public async Task Running_command_fails_before_writing_when_data_collected_at_header_is_missing()
    {
        var sourceRoot = CreateTempFifaSourceRoot(
            contextContent: "team,rank,ELO\nMexiko,15,1681.03\n");
        try
        {
            var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
            var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                kpiRepository: kpiRepository,
                contextRepository: contextRepository);
            var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
                "collect-context-fifa",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-fifa",
                "--community-context",
                "ehonda-dev-wm26",
                "--source-root",
                sourceRoot);

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(output).Contains("Data_Collected_At");
            VerifyNoWrites(contextRepository, kpiRepository);
        }
        finally
        {
            DeleteTempDirectory(sourceRoot);
        }
    }

    [Test]
    public async Task Running_command_fails_before_writing_when_data_collected_at_is_empty()
    {
        var sourceRoot = CreateTempFifaSourceRoot(
            contextContent: "team,Data_Collected_At,rank,ELO\nMexiko,,15,1681.03\n");
        try
        {
            var contextRepository = CreateMockContextRepositoryForUpload(savedVersion: 1);
            var kpiRepository = CreateMockKpiRepositoryForUpload(savedVersion: 0);
            var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
                kpiRepository: kpiRepository,
                contextRepository: contextRepository);
            var (app, console) = CreateCommandApp<CollectContextFifaCommand>(
                "collect-context-fifa",
                firebaseServiceFactory: firebaseFactory);

            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-fifa",
                "--community-context",
                "ehonda-dev-wm26",
                "--source-root",
                sourceRoot);

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(output).Contains("empty Data_Collected_At");
            VerifyNoWrites(contextRepository, kpiRepository);
        }
        finally
        {
            DeleteTempDirectory(sourceRoot);
        }
    }

    private static string CreateTempFifaSourceRoot(
        Option<string> contextContent = default,
        Option<string> kpiContent = default)
    {
        var root = Path.Combine(Path.GetTempPath(), "KicktippAiTests", Guid.NewGuid().ToString("N"));
        var contextDirectory = Path.Combine(root, "context-documents");
        var kpiDirectory = Path.Combine(root, "kpi-documents");
        Directory.CreateDirectory(contextDirectory);
        Directory.CreateDirectory(kpiDirectory);

        var actualContextContent = contextContent.Or("team,Data_Collected_At,rank,ELO\nMexiko,2026-05-24,15,1681.03\n");
        var actualKpiContent = kpiContent.Or(
            "team,Data_Collected_At,rank,ELO\nMexiko,2026-05-24,15,1681.03\nSudafrika,2026-05-24,56,1412.69\n");

        File.WriteAllText(Path.Combine(contextDirectory, "fifa-ranking-mexiko.csv"), actualContextContent);
        File.WriteAllText(
            Path.Combine(contextDirectory, "fifa-ranking-sudafrika.csv"),
            "team,Data_Collected_At,rank,ELO\nSudafrika,2026-05-24,56,1412.69\n");
        File.WriteAllText(Path.Combine(kpiDirectory, "fifa-rankings.csv"), actualKpiContent);

        return root;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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
