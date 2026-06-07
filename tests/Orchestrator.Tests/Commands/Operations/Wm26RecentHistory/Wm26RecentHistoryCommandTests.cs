using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.Wm26RecentHistory;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Wm26RecentHistory;

public class Wm26RecentHistoryCommandTests
{
    [Test]
    public async Task Export_date_map_writes_recent_history_rows()
    {
        var outputPath = CreateTempCsvPath();
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,"),
                ["fifa-world-cup-2026-standings.csv"] = CreateContextDocument(
                    documentName: "fifa-world-cup-2026-standings.csv",
                    content: "Position,Team,Points\n1,Germany,3")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, _) = await RunCommandAsync(
            ctx,
            "export-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--output",
            outputPath);

        await Assert.That(exitCode).IsEqualTo(0);
        var content = await File.ReadAllTextAsync(outputPath);
        await Assert.That(content).Contains("DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At");
        await Assert.That(content).Contains("recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0");
    }

    [Test]
    public async Task Export_date_map_preserves_duplicate_existing_entries_in_row_order()
    {
        var outputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-canada.csv,CopAm,Argentina,Canada,2:0,,2024-07-09,CONMEBOL,https://example.test/semifinal,2026-05-24,
            recent-history-canada.csv,CopAm,Argentina,Canada,2:0,,2024-06-20,CONMEBOL,https://example.test/group,2026-05-24,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-canada.csv"] = CreateContextDocument(
                    documentName: "recent-history-canada.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nCopAm,Argentina,Canada,2:0,\nCopAm,Argentina,Canada,2:0,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, _) = await RunCommandAsync(
            ctx,
            "export-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--output",
            outputPath);

        await Assert.That(exitCode).IsEqualTo(0);
        var entries = HistoryCsvUtility.ReadDateMapEntries(await File.ReadAllTextAsync(outputPath));
        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].PlayedAt).IsEqualTo("2024-07-09");
        await Assert.That(entries[1].PlayedAt).IsEqualTo("2024-06-20");
    }

    [Test]
    public async Task Apply_date_map_dry_run_does_not_save_documents()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,2025-11-17,DFB,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run - would save");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Apply_date_map_saves_updated_recent_history_documents()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,2025-11-17,DFB,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, _) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath);

        await Assert.That(exitCode).IsEqualTo(0);
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-germany.csv",
                It.Is<string>(content =>
                    content.Contains("Played_At") &&
                    !content.Contains("Data_Collected_At") &&
                    content.Contains("2025-11-17")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Apply_date_map_fails_when_target_rows_have_no_exact_played_at()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("missing exact Played_At");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Apply_date_map_known_only_updates_mapped_rows_and_preserves_unmapped_rows()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,2025-11-17,DFB,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content:
                        "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\n" +
                        "KL-WM,2026-06-03,Germany,Slovakia,6:0,\n" +
                        "WM,2026-06-03,Germany,Unmapped Team,1:1,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--apply-known-only",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("1 updated").And.Contains("skipped");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-germany.csv",
                It.Is<string>(content =>
                    content.Contains("Played_At") &&
                    !content.Contains("Data_Collected_At") &&
                    content.Contains("KL-WM,2025-11-17,Germany,Slovakia,6:0,") &&
                    content.Contains("WM,2026-06-03,Germany,Unmapped Team,1:1,")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Apply_date_map_known_only_preserves_invalid_map_dates()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content:
                        "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\n" +
                        "KL-WM,2026-06-03,Germany,Slovakia,6:0,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--apply-known-only",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("0 updated").And.Contains("skipped");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-germany.csv",
                It.Is<string>(content =>
                    content.Contains("Played_At") &&
                    !content.Contains("Data_Collected_At") &&
                    content.Contains("KL-WM,2026-06-03,Germany,Slovakia,6:0,")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Apply_date_map_known_only_succeeds_when_no_recent_history_documents_exist()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,2025-11-17,DFB,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["fifa-world-cup-2026-standings.csv"] = CreateContextDocument(
                    documentName: "fifa-world-cup-2026-standings.csv",
                    content: "Position,Team,Points\n1,Germany,3")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--apply-known-only");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("No recent-history documents found");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Apply_date_map_known_only_preserves_cutoff_rows_without_consuming_map_entries()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-mexiko.csv,WM,Mexiko,Südafrika,1:1,,2010-06-11,FIFA,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-mexiko.csv"] = CreateContextDocument(
                    documentName: "recent-history-mexiko.csv",
                    content:
                        "Competition,Data_Collected_At,Home_Team,Away_Team,Score,Annotation\n" +
                        "WM,2026-06-11,Mexiko,Südafrika,1:1,\n" +
                        "WM,2026-06-03,Mexiko,Südafrika,1:1,")
            });
        var ctx = CreateApp(contextRepository);

        var (exitCode, output) = await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--apply-known-only",
            "--preserve-collected-on-or-after",
            "2026-06-11",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("1 updated").And.Contains("1 preserved");
        contextRepository.Verify(
            r => r.SaveContextDocumentAsync(
                "recent-history-mexiko.csv",
                It.Is<string>(content =>
                    content.Contains("Played_At") &&
                    !content.Contains("Data_Collected_At") &&
                    content.Contains("WM,2026-06-11,Mexiko,Südafrika,1:1,") &&
                    content.Contains("WM,2010-06-11,Mexiko,Südafrika,1:1,")),
                "ehonda-dev-wm26",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Commands_scope_context_repository_to_world_cup_competition()
    {
        var inputPath = CreateTempDateMap("""
            DocumentName,Competition,Home_Team,Away_Team,Score,Annotation,Played_At,Source_Name,Source_Url,Verified_At,Notes
            recent-history-germany.csv,KL-WM,Germany,Slovakia,6:0,,2025-11-17,DFB,https://example.test,2026-05-23,
            """);
        var contextRepository = CreateRepository(
            new Dictionary<string, ContextDocument>
            {
                ["recent-history-germany.csv"] = CreateContextDocument(
                    documentName: "recent-history-germany.csv",
                    content: "Competition,Home_Team,Away_Team,Score,Annotation\nKL-WM,Germany,Slovakia,6:0,")
            });
        var ctx = CreateApp(contextRepository);

        await RunCommandAsync(
            ctx,
            "apply-date-map",
            "--community-context",
            "ehonda-dev-wm26",
            "--competition",
            CompetitionIds.FifaWorldCup2026,
            "--input",
            inputPath,
            "--dry-run");

        ctx.FirebaseServiceFactory.Verify(
            f => f.CreateContextRepository(CompetitionIds.FifaWorldCup2026),
            Times.Once);
    }

    private static Wm26RecentHistoryCommandTestContext CreateApp(Mock<IContextRepository> contextRepository)
    {
        var testConsole = new TestConsole();
        var firebaseServiceFactory = CreateMockFirebaseServiceFactoryFull(contextRepository: contextRepository);

        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(testConsole);
        services.AddSingleton(firebaseServiceFactory.Object);
        services.AddSingleton<ILogger<Wm26RecentHistoryExportDateMapCommand>>(new FakeLogger<Wm26RecentHistoryExportDateMapCommand>());
        services.AddSingleton<ILogger<Wm26RecentHistoryApplyDateMapCommand>>(new FakeLogger<Wm26RecentHistoryApplyDateMapCommand>());

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.Settings.Console = testConsole;
            config.AddCommand<Wm26RecentHistoryExportDateMapCommand>("export-date-map");
            config.AddCommand<Wm26RecentHistoryApplyDateMapCommand>("apply-date-map");
        });

        return new Wm26RecentHistoryCommandTestContext(app, testConsole, firebaseServiceFactory);
    }

    private static Mock<IContextRepository> CreateRepository(Dictionary<string, ContextDocument> documents)
    {
        var mock = new Mock<IContextRepository>();
        mock.Setup(r => r.GetContextDocumentNamesAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents.Keys.ToList());
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
            .ReturnsAsync(2);

        return mock;
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(
        Wm26RecentHistoryCommandTestContext context,
        params string[] args)
    {
        var exitCode = await context.App.RunAsync(args);
        return (exitCode, context.Console.Output);
    }

    private static string CreateTempDateMap(string content)
    {
        var path = CreateTempCsvPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
    }

    private static string CreateTempCsvPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "KicktippAi",
            "wm26-recent-history-tests",
            $"{Guid.NewGuid():N}.csv");
    }

    private sealed record Wm26RecentHistoryCommandTestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseServiceFactory);
}
