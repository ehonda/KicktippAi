using System.Globalization;
using CsvHelper;
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

    [Test]
    public async Task Dry_run_audits_all_matchdays_without_writing_and_uses_the_exact_competition_partition()
    {
        var contextRepository = CreateRepository();
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
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        outcomeRepository.Verify(repository => repository.GetMatchdayOutcomesAsync(
            It.Is<int>(matchday => matchday >= 1 && matchday <= 34), Community, It.IsAny<CancellationToken>()), Times.Exactly(34));
        test.FirebaseFactory.Verify(factory => factory.CreateContextRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
        test.FirebaseFactory.Verify(factory => factory.CreateMatchOutcomeRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
    }

    [Test]
    public async Task Apply_saves_only_the_changed_canonical_document_after_the_complete_gate_passes()
    {
        var contextRepository = CreateRepository();
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "apply", "--community-context", Community, "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(test.Console.Output).Contains("atomically saved 1 document");
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.Is<IReadOnlyList<ContextDocumentWrite>>(documents =>
                documents.Count == 54
                && documents.Any(document => document.DocumentName == DocumentName
                    && document.Content.StartsWith("Competition,Played_At,", StringComparison.Ordinal))),
            Community,
            It.IsAny<CancellationToken>()), Times.Once);
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Failed_apply_gate_retains_last_known_good_document_and_makes_no_partial_write()
    {
        var contextRepository = CreateRepository(corruptTargetScore: true);
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "apply", "--community-context", Community, "--input", CreateMap()
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(test.Console.Output).Contains("no documents were written");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Audit_is_strict_and_never_uses_the_repository_save_seam()
    {
        var contextRepository = CreateRepository();
        var test = CreateApp(contextRepository, CreateMockMatchOutcomeRepository());

        var exitCode = await test.App.RunAsync([
            "audit", "--community-context", Community, "--input", CreateMap(), "--verbose"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(test.Console.Output).Contains("Strict audit passed").And.Contains("transfermarkt-datasets@");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Kicktipp_inventory_loads_matching_credentials_before_creating_a_client_or_output()
    {
        var contextRepository = CreateRepository();
        var kicktippFactory = new Mock<IKicktippClientFactory>();
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        credentialLoader.Setup(loader => loader.Load("pes-squad"))
            .Throws(new InvalidOperationException("credential load failed"));
        var output = Path.Combine(Path.GetTempPath(), $"kicktippai-inventory-{Guid.NewGuid():N}.csv");
        var test = CreateApp(
            contextRepository,
            CreateMockMatchOutcomeRepository(),
            kicktippFactory,
            credentialLoader);

        try
        {
            var exitCode = await test.App.RunAsync([
                "export-inventory", "--community-context", "pes-squad", "--from-kicktipp", "--matchdays", "3,4,2", "--output", output
            ]);

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(test.Console.Output).Contains("credential load failed");
            credentialLoader.Verify(loader => loader.Load("pes-squad"), Times.Once);
            kicktippFactory.Verify(factory => factory.CreateClient(), Times.Never);
            await Assert.That(File.Exists(output)).IsFalse();
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Test]
    public async Task Stored_inventory_never_loads_kicktipp_credentials()
    {
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        var output = Path.Combine(Path.GetTempPath(), $"kicktippai-stored-inventory-{Guid.NewGuid():N}.csv");
        var test = CreateApp(
            CreateRepository(),
            CreateMockMatchOutcomeRepository(),
            credentialLoader: credentialLoader);

        try
        {
            var exitCode = await test.App.RunAsync([
                "export-inventory", "--community-context", Community, "--output", output
            ]);

            await Assert.That(exitCode).IsEqualTo(0);
            credentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
            credentialLoader.Verify(loader => loader.Load(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            await Assert.That(File.Exists(output)).IsTrue();
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static TestContext CreateApp(
        Mock<IContextRepository> contextRepository,
        Mock<IMatchOutcomeRepository> matchOutcomeRepository,
        Mock<IKicktippClientFactory>? kicktippFactory = null,
        Mock<ICommunityKicktippCredentialLoader>? credentialLoader = null)
    {
        var console = new TestConsole();
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: contextRepository,
            matchOutcomeRepository: matchOutcomeRepository);
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(firebaseFactory.Object);
        services.AddSingleton((kicktippFactory ?? new Mock<IKicktippClientFactory>()).Object);
        services.AddSingleton((credentialLoader ?? new Mock<ICommunityKicktippCredentialLoader>()).Object);
        services.AddSingleton(new Mock<IContextProviderFactory>().Object);
        services.AddSingleton<IBundesligaHistoryPlayedDateCollector, BundesligaHistoryPlayedDateCollector>();
        services.AddSingleton<ILogger<BundesligaHistoryApplyCommand>>(new FakeLogger<BundesligaHistoryApplyCommand>());
        services.AddSingleton<ILogger<BundesligaHistoryAuditCommand>>(new FakeLogger<BundesligaHistoryAuditCommand>());
        services.AddSingleton<ILogger<BundesligaHistoryExportInventoryCommand>>(new FakeLogger<BundesligaHistoryExportInventoryCommand>());

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(configuration =>
        {
            configuration.Settings.Console = console;
            configuration.AddCommand<BundesligaHistoryApplyCommand>("apply");
            configuration.AddCommand<BundesligaHistoryAuditCommand>("audit");
            configuration.AddCommand<BundesligaHistoryExportInventoryCommand>("export-inventory");
        });
        return new(app, console, firebaseFactory);
    }

    private static Mock<IContextRepository> CreateRepository(bool corruptTargetScore = false)
    {
        var map = BundesligaHistoryPlayedDateMap.Default.Entries;
        var documents = map.GroupBy(entry => entry.DocumentName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => CreateContextDocument(
                    documentName: group.Key,
                    content: RenderHistory(group, includePlayedAt: group.Key != DocumentName,
                        corruptFirstScore: corruptTargetScore)),
                StringComparer.Ordinal);
        var repository = new Mock<IContextRepository>();
        repository.Setup(value => value.GetContextDocumentNamesAsync(Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documents.Keys.Append("head-to-head-b04-vs-vfb.csv").ToArray());
        repository.Setup(value => value.GetLatestContextDocumentAsync(
                It.IsAny<string>(), Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, CancellationToken _) => documents.GetValueOrDefault(name));
        repository.Setup(value => value.SaveContextDocumentsAtomicallyAsync(
                It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ContextDocumentWrite> writes, string _, CancellationToken _) => writes
                .Select(write => new ContextDocumentSaveResult(write.DocumentName,
                    write.DocumentName == DocumentName ? 2 : null))
                .ToArray());
        return repository;
    }

    private static string CreateMap()
    {
        return Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "history-played-dates.csv");
    }

    private static string RenderHistory(
        IEnumerable<BundesligaHistoryPlayedDateMapEntry> entries,
        bool includePlayedAt,
        bool corruptFirstScore)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\r\n" };
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var header in includePlayedAt
                     ? new[] { "Competition", "Played_At", "Home_Team", "Away_Team", "Score", "Annotation" }
                     : new[] { "Competition", "Home_Team", "Away_Team", "Score", "Annotation" })
        {
            csv.WriteField(header);
        }
        csv.NextRecord();

        foreach (var entry in entries.OrderBy(entry => entry.RowOrdinal))
        {
            csv.WriteField(entry.HistoryCompetition);
            if (includePlayedAt) csv.WriteField(entry.PlayedAt);
            csv.WriteField(entry.HomeTeam);
            csv.WriteField(entry.AwayTeam);
            csv.WriteField(corruptFirstScore && entry.RowOrdinal == 1 ? "99:99" : entry.Score);
            csv.WriteField(entry.Annotation);
            csv.NextRecord();
        }
        return writer.ToString();
    }

    private sealed record TestContext(
        CommandApp App,
        TestConsole Console,
        Mock<IFirebaseServiceFactory> FirebaseFactory);
}
