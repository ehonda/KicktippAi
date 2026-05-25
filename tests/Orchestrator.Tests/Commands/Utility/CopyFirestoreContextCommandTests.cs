using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.CopyFirestoreContext;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

public class CopyFirestoreContextCommandTests
{
    private const string ExamplelandLineup =
        "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\n" +
        "Exampleland,2026-05-25,Player,Alex Example,24,Forward,1000000\n" +
        "Exampleland,2026-05-25,Coach,Casey Sample,51,Coach,";

    private const string SampleIslesLineup =
        "Team,Data_Collected_At,Role,Name,Age,Position,Market_Value_EUR\n" +
        "Sample Isles,2026-05-25,Player,Jordan Sample,26,Midfielder,800000\n" +
        "Sample Isles,2026-05-25,Coach,Riley Fiction,48,Coach,";

    private static (CommandApp App, TestConsole Console, Mock<IContextRepository> ContextRepository, Mock<IKpiRepository> KpiRepository)
        CreateCopyCommandApp(Mock<IContextRepository> contextRepository, Mock<IKpiRepository> kpiRepository)
    {
        var testConsole = new TestConsole();
        var mockFirebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: contextRepository,
            kpiRepository: kpiRepository);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton<IFirebaseServiceFactory>(mockFirebaseFactory.Object);
        services.AddSingleton<ILogger<CopyFirestoreContextCommand>>(new FakeLogger<CopyFirestoreContextCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<CopyFirestoreContextCommand>("copy-firestore-context");
        });

        return (app, testConsole, contextRepository, kpiRepository);
    }

    [Test]
    public async Task Copying_lineup_prefix_and_lineups_kpi_writes_target_documents()
    {
        var contextRepository = CreateLineupContextRepository();
        var kpiRepository = CreateLineupsKpiRepository();
        var (app, console, _, _) = CreateCopyCommandApp(contextRepository, kpiRepository);

        var exitCode = await RunLineupCopyAsync(app);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Copied 2 context document(s) and 1 KPI document(s)");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.Is<string>(name => name.StartsWith("lineup-", StringComparison.Ordinal)),
            It.IsAny<string>(),
            "target-community",
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        kpiRepository.Verify(r => r.SaveKpiDocumentAsync(
            "lineups",
            AggregateLineups(),
            "Fictional aggregate lineups",
            "target-community",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Dry_run_copies_nothing()
    {
        var contextRepository = CreateLineupContextRepository();
        var kpiRepository = CreateLineupsKpiRepository();
        var (app, console, _, _) = CreateCopyCommandApp(contextRepository, kpiRepository);

        var exitCode = await RunLineupCopyAsync(app, "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Dry run mode enabled");
        await Assert.That(console.Output).Contains("Would copy 2 context document(s) and 1 KPI document(s)");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        kpiRepository.Verify(r => r.SaveKpiDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Missing_source_documents_fail_without_writes()
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(r => r.GetContextDocumentNamesAsync("source-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["other.csv"]);

        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(r => r.GetKpiDocumentAsync("lineups", "source-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KpiDocument?)null);

        var (app, console, _, _) = CreateCopyCommandApp(contextRepository, kpiRepository);

        var exitCode = await RunLineupCopyAsync(app);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("No source context documents found");
        await Assert.That(console.Output).Contains("Missing source KPI document: lineups");
        contextRepository.Verify(r => r.SaveContextDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        kpiRepository.Verify(r => r.SaveKpiDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Task<int> RunLineupCopyAsync(CommandApp app, params string[] extraArgs)
    {
        var args = new List<string>
        {
            "copy-firestore-context",
            "--source-community-context",
            "source-community",
            "--target-community-context",
            "target-community",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--context-prefix",
            "lineup-",
            "--kpi-document",
            "lineups"
        };
        args.AddRange(extraArgs);
        return app.RunAsync(args);
    }

    private static Mock<IContextRepository> CreateLineupContextRepository()
    {
        return CreateContextRepository(new Dictionary<string, ContextDocument>
        {
            ["lineup-exampleland.csv"] = CreateContextDocument("lineup-exampleland.csv", ExamplelandLineup),
            ["lineup-sample-isles.csv"] = CreateContextDocument("lineup-sample-isles.csv", SampleIslesLineup)
        });
    }

    private static Mock<IContextRepository> CreateContextRepository(Dictionary<string, ContextDocument> documents)
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(r => r.GetContextDocumentNamesAsync("source-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents.Keys.Concat(["other.csv"]).ToList());
        contextRepository.Setup(r => r.GetLatestContextDocumentAsync(
                It.IsAny<string>(),
                "source-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string documentName, string _, CancellationToken _) => documents.GetValueOrDefault(documentName));
        contextRepository.Setup(r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "target-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return contextRepository;
    }

    private static Mock<IKpiRepository> CreateLineupsKpiRepository(string? content = null)
    {
        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(r => r.GetKpiDocumentAsync("lineups", "source-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KpiDocument(
                "lineups",
                content ?? AggregateLineups(),
                "Fictional aggregate lineups",
                0,
                DateTimeOffset.UtcNow));
        kpiRepository.Setup(r => r.SaveKpiDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                "target-community",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return kpiRepository;
    }

    private static string AggregateLineups()
    {
        var sampleRows = SampleIslesLineup[(SampleIslesLineup.IndexOf('\n') + 1)..];
        return ExamplelandLineup + "\n" + sampleRows;
    }
}
