using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Utility.ContextHygiene;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Utility;

public class ContextHygieneInventoryCommandTests
{
    [Test]
    public async Task Json_inventory_reports_identity_hash_and_classification_without_content_or_writes()
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(repository => repository.GetContextDocumentNamesAsync(
                "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["bundesliga-standings.csv", "operator-notes.md", "team-data"]);
        var documents = new Dictionary<string, ContextDocument>(StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new("bundesliga-standings.csv", "SECRET_STANDINGS_BODY", 4, DateTimeOffset.UnixEpoch),
            ["operator-notes.md"] = new("operator-notes.md", "SECRET_OPERATOR_BODY", 2, DateTimeOffset.UnixEpoch),
            ["team-data"] = new("team-data", "SECRET_LEGACY_BODY", 9, DateTimeOffset.UnixEpoch)
        };
        contextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                It.IsAny<string>(), "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, CancellationToken _) => documents[name]);
        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(repository => repository.GetAllKpiDocumentsAsync(
                "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new KpiDocument("manager-data", "SECRET_MANAGER_BODY", "legacy", 7, DateTimeOffset.UnixEpoch)]);
        var publicationRepository = new Mock<IDocumentPublicationRepository>();
        publicationRepository.Setup(repository => repository.GetLastKnownGoodAsync(
                It.IsAny<DocumentPublicationDefinition>(), "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);
        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "ehonda-dev-buli-2627",
            "--json"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        using var json = JsonDocument.Parse(console.Output);
        var rows = json.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        var legacy = rows.Single(row => row.GetProperty("name").GetString() == "team-data");
        await Assert.That(legacy.GetProperty("classification").GetString())
            .IsEqualTo(nameof(BundesligaContextHygieneClassification.DeprecatedTeamOrManager));
        await Assert.That(legacy.GetProperty("contentSha256").GetString())
            .IsEqualTo(DocumentPublicationContract.ComputeContentSha256("SECRET_LEGACY_BODY"));
        await Assert.That(console.Output).DoesNotContain("SECRET_STANDINGS_BODY");
        await Assert.That(console.Output).DoesNotContain("SECRET_OPERATOR_BODY");
        await Assert.That(console.Output).DoesNotContain("SECRET_LEGACY_BODY");
        await Assert.That(console.Output).DoesNotContain("SECRET_MANAGER_BODY");
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        kpiRepository.Verify(repository => repository.SaveKpiDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        publicationRepository.Verify(repository => repository.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Inventory_uses_exact_publication_heads_and_reports_source_dates()
    {
        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(repository => repository.GetContextDocumentNamesAsync(
                "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(repository => repository.GetAllKpiDocumentsAsync(
                "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var publicationRepository = CreateMockBundesligaDocumentPublicationRepository();
        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "test-community",
            "--evaluation-date", "2026-08-21",
            "--json"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        using var json = JsonDocument.Parse(console.Output);
        await Assert.That(json.RootElement.GetProperty("evaluationDate").GetString()).IsEqualTo("2026-08-21");
        var rows = json.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        var roster = rows.Single(row => row.GetProperty("name").GetString() == "roster-fcb");
        var elo = rows.Single(row => row.GetProperty("name").GetString() == "club-elo-fcb.csv");
        await Assert.That(roster.GetProperty("state").GetString()).IsEqualTo("Headed");
        await Assert.That(roster.GetProperty("publicationSet").GetString())
            .IsEqualTo(BundesligaDocumentPublication.RosterPublicationSet);
        await Assert.That(roster.GetProperty("sourceAsOf").GetString()).IsNotNull();
        await Assert.That(elo.GetProperty("state").GetString()).IsEqualTo("Headed");
        await Assert.That(elo.GetProperty("publicationSet").GetString())
            .IsEqualTo(BundesligaDocumentPublication.ClubEloPublicationSet);
        await Assert.That(elo.GetProperty("sourceAsOf").GetString()).IsNotNull();
        await Assert.That(roster.GetProperty("publicationSnapshotId").GetString()!.Length).IsEqualTo(64);
    }

    [Test]
    public async Task Historical_competition_is_rejected_before_repository_access()
    {
        var contextRepository = new Mock<IContextRepository>();
        var kpiRepository = new Mock<IKpiRepository>();
        var publicationRepository = new Mock<IDocumentPublicationRepository>();
        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "historical-community",
            "--competition", CompetitionIds.Bundesliga2025_26
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("supports only");
        contextRepository.Verify(repository => repository.GetContextDocumentNamesAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (CommandApp App, TestConsole Console) CreateApp(
        Mock<IContextRepository> contextRepository,
        Mock<IKpiRepository> kpiRepository,
        Mock<IDocumentPublicationRepository> publicationRepository)
    {
        var console = new TestConsole();
        var firebaseFactory = CreateMockFirebaseServiceFactoryFull(
            contextRepository: contextRepository,
            kpiRepository: kpiRepository,
            documentPublicationRepository: publicationRepository);
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton<IFirebaseServiceFactory>(firebaseFactory.Object);
        services.AddSingleton<ILogger<ContextHygieneInventoryCommand>>(
            new FakeLogger<ContextHygieneInventoryCommand>());
        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(configuration =>
        {
            configuration.Settings.Console = console;
            configuration.AddCommand<ContextHygieneInventoryCommand>("inventory");
        });
        return (app, console);
    }
}
