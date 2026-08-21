using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.BundesligaHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.BundesligaHistory;

public class BundesligaHistoryCommandTests
{
    private const string Community = "ehonda-dev-buli-2627";
    private const string DocumentName = "recent-history-b04.csv";
    private const string UndatedContent = "Competition,Home_Team,Away_Team,Score,Annotation\n" +
                                            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,3:1,";

    [Test]
    public async Task Dry_run_audits_all_matchdays_without_writing_and_uses_the_exact_competition_partition()
    {
        var contextRepository = CreateRepository(UndatedContent);
        var outcomeRepository = CreateMockMatchOutcomeRepository();
        var test = CreateApp(contextRepository, outcomeRepository);
        var mapPath = CreateMap();

        var exitCode = await test.App.RunAsync([
            "apply", "--community-context", Community, "--input", mapPath, "--dry-run"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(test.Console.Output).Contains("Strict dry-run passed").And.Contains("no writes were made");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        outcomeRepository.Verify(repository => repository.GetMatchdayOutcomesAsync(
            It.Is<int>(matchday => matchday >= 1 && matchday <= 34), Community, It.IsAny<CancellationToken>()), Times.Exactly(34));
        test.FirebaseFactory.Verify(factory => factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
        test.FirebaseFactory.Verify(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
    }

    [Test]
    public async Task Apply_saves_only_the_changed_canonical_document_after_the_complete_gate_passes()
    {
        var contextRepository = CreateRepository(UndatedContent);
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "apply", "--community-context", Community, "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(test.Console.Output).Contains("saved 1 document");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            DocumentName,
            "Competition,Played_At,Home_Team,Away_Team,Score,Annotation\r\n" +
            "1.BL,2026-05-09,Bayer 04 Leverkusen,VfB Stuttgart,3:1,\r\n",
            Community,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Failed_apply_gate_retains_last_known_good_document_and_makes_no_partial_write()
    {
        var contextRepository = CreateRepository(
            "Competition,Home_Team,Away_Team,Score,Annotation\n" +
            "1.BL,Bayer 04 Leverkusen,VfB Stuttgart,4:1,");
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "apply", "--community-context", Community, "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(test.Console.Output).Contains("no documents were written");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Audit_is_strict_and_never_uses_the_repository_save_seam()
    {
        var contextRepository = CreateRepository(UndatedContent);
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "audit", "--community-context", Community, "--input", CreateMap(), "--verbose"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(test.Console.Output).Contains("Strict audit passed").And.Contains("transfermarkt-datasets@");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TestContext CreateApp(
        Mock<IContextRepository> contextRepository,
        Mock<IMatchOutcomeRepository> matchOutcomeRepository)
    {
        var console = new TestConsole();
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: contextRepository,
            matchOutcomeRepository: matchOutcomeRepository);
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(firebaseFactory.Object);
        services.AddSingleton<IBundesligaHistoryPlayedDateCollector, BundesligaHistoryPlayedDateCollector>();
        services.AddSingleton<ILogger<BundesligaHistoryApplyCommand>>(new FakeLogger<BundesligaHistoryApplyCommand>());
        services.AddSingleton<ILogger<BundesligaHistoryAuditCommand>>(new FakeLogger<BundesligaHistoryAuditCommand>());

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(configuration =>
        {
            configuration.Settings.Console = console;
            configuration.AddCommand<BundesligaHistoryApplyCommand>("apply");
            configuration.AddCommand<BundesligaHistoryAuditCommand>("audit");
        });
        return new(app, console, firebaseFactory);
    }

    private static Mock<IContextRepository> CreateRepository(string content)
    {
        var document = CreateContextDocument(documentName: DocumentName, content: content);
        var repository = new Mock<IContextRepository>();
        repository.Setup(value => value.GetContextDocumentNamesAsync(Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DocumentName, "head-to-head-b04-vs-vfb.csv"]);
        repository.Setup(value => value.GetLatestContextDocumentAsync(
                It.IsAny<string>(), Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, CancellationToken _) => name == DocumentName ? document : null);
        repository.Setup(value => value.SaveContextDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        return repository;
    }

    private static string CreateMap()
    {
        var path = Path.Combine(Path.GetTempPath(), "KicktippAi", "bundesliga-history-tests", $"{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, BundesligaHistoryPlayedDateMap.Write([
            new BundesligaHistoryPlayedDateMapEntry(
                DocumentName, 1, "1.BL", "Bayer 04 Leverkusen", "VfB Stuttgart", "3:1", string.Empty,
                "2026-05-09", BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceClass,
                BundesligaHistoryPlayedDateMap.TransfermarktDatasetSourceName,
                "https://www.transfermarkt.co.uk/example/index/spielbericht/4634534",
                BundesligaHistoryPlayedDateMap.TransfermarktDatasetRevision,
                "4634534", "2026-08-21T12:00:00+02:00")
        ]));
        return path;
    }

    private sealed record TestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseFactory);
}
