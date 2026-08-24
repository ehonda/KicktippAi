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
        await Assert.That(json.RootElement.GetProperty("identityConflictCount").GetInt32()).IsEqualTo(0);
        await Assert.That(json.RootElement.GetProperty("expectedCsvCount").GetInt32()).IsEqualTo(400);
        await Assert.That(json.RootElement.GetProperty("validCsvCount").GetInt32()).IsEqualTo(0);
        await Assert.That(json.RootElement.GetProperty("invalidCsvCount").GetInt32()).IsEqualTo(0);
        var rows = json.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        var standings = rows.Single(row => row.GetProperty("name").GetString() == "bundesliga-standings.csv");
        var missingHistory = rows.Single(row => row.GetProperty("name").GetString() == "recent-history-fcb.csv");
        var missingRules = rows.Single(row =>
            row.GetProperty("name").GetString() == "community-rules-ehonda-dev-buli-2627.md");
        var legacy = rows.Single(row => row.GetProperty("name").GetString() == "team-data");
        await Assert.That(standings.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.NotChecked));
        await Assert.That(missingHistory.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.Missing));
        await Assert.That(missingRules.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.NotApplicable));
        await Assert.That(legacy.GetProperty("classification").GetString())
            .IsEqualTo(nameof(BundesligaContextHygieneClassification.DeprecatedTeamOrManager));
        await Assert.That(legacy.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.NotApplicable));
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
            "--validate-csv-bytes",
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
    public async Task Inventory_distinguishes_matching_generic_identities_from_context_and_kpi_head_conflicts()
    {
        var publicationRepository = CreateMockBundesligaDocumentPublicationRepository();
        var rosterPublication = await publicationRepository.Object.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.Rosters,
            "test-community");
        var eloPublication = await publicationRepository.Object.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.ClubElo,
            "test-community");
        var headedRoster = rosterPublication!.Documents.Single(document => document.Name == "roster-fcb");
        var headedSquadSummary = rosterPublication.Documents.Single(document => document.Name == "team-squad-summary");
        var headedElo = eloPublication!.Documents.Single(document => document.Name == "club-elo-fcb.csv");
        var headedEloRankings = eloPublication.Documents.Single(document => document.Name == "club-elo-rankings");

        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(repository => repository.GetContextDocumentNamesAsync(
                "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["roster-fcb", "club-elo-fcb.csv"]);
        var contextDocuments = new Dictionary<string, ContextDocument>(StringComparer.Ordinal)
        {
            ["roster-fcb"] = new(
                "roster-fcb",
                "SECRET_DIVERGENT_ROSTER_CONTENT",
                headedRoster.Version + 50,
                headedRoster.CreatedAt.AddMinutes(1)),
            ["club-elo-fcb.csv"] = new(
                "club-elo-fcb.csv",
                headedElo.Content,
                headedElo.Version,
                headedElo.CreatedAt)
        };
        contextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                It.IsAny<string>(), "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, CancellationToken _) => contextDocuments[name]);

        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(repository => repository.GetAllKpiDocumentsAsync(
                "test-community", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new KpiDocument(
                    "team-squad-summary",
                    "SECRET_DIVERGENT_KPI_CONTENT",
                    "divergent",
                    headedSquadSummary.Version + 50,
                    headedSquadSummary.CreatedAt.AddMinutes(1)),
                new KpiDocument(
                    "club-elo-rankings",
                    headedEloRankings.Content,
                    headedEloRankings.Description ?? string.Empty,
                    headedEloRankings.Version,
                    headedEloRankings.CreatedAt)
            ]);
        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "test-community",
            "--evaluation-date", "2026-08-21",
            "--validate-csv-bytes",
            "--json"
        ]);

        await Assert.That(exitCode).IsEqualTo(0);
        using var json = JsonDocument.Parse(console.Output);
        await Assert.That(json.RootElement.GetProperty("identityConflictCount").GetInt32()).IsEqualTo(2);
        await Assert.That(json.RootElement.GetProperty("expectedCsvCount").GetInt32()).IsEqualTo(400);
        await Assert.That(json.RootElement.GetProperty("validCsvCount").GetInt32()).IsEqualTo(39);
        await Assert.That(json.RootElement.GetProperty("invalidCsvCount").GetInt32()).IsEqualTo(0);
        var rows = json.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        foreach (var name in new[] { "roster-fcb", "team-squad-summary" })
        {
            var conflict = rows.Single(row => row.GetProperty("name").GetString() == name);
            await Assert.That(conflict.GetProperty("state").GetString()).IsEqualTo("HeadedGenericConflict");
            await Assert.That(conflict.GetProperty("genericVersion").GetInt32())
                .IsGreaterThan(conflict.GetProperty("headedVersion").GetInt32());
            await Assert.That(conflict.GetProperty("genericContentSha256").GetString())
                .IsNotEqualTo(conflict.GetProperty("headedContentSha256").GetString());
            await Assert.That(conflict.GetProperty("identityDiagnostic").GetString())
                .Contains("GENERIC_LATEST_DIVERGES_FROM_PUBLICATION_HEAD");
            await Assert.That(conflict.GetProperty("identityDiagnostic").GetString()).Contains("generic(version=");
            await Assert.That(conflict.GetProperty("identityDiagnostic").GetString()).Contains("headed(version=");
            await Assert.That(conflict.GetProperty("csvByteState").GetString())
                .IsEqualTo(nameof(BundesligaContextCsvValidationState.Valid));
            await Assert.That(conflict.GetProperty("csvByteDiagnostic").ValueKind).IsEqualTo(JsonValueKind.Null);
        }

        foreach (var name in new[] { "club-elo-fcb.csv", "club-elo-rankings" })
        {
            var matching = rows.Single(row => row.GetProperty("name").GetString() == name);
            await Assert.That(matching.GetProperty("state").GetString()).IsEqualTo("HeadedGenericMatch");
            await Assert.That(matching.GetProperty("genericVersion").GetInt32())
                .IsEqualTo(matching.GetProperty("headedVersion").GetInt32());
            await Assert.That(matching.GetProperty("genericContentSha256").GetString())
                .IsEqualTo(matching.GetProperty("headedContentSha256").GetString());
            await Assert.That(matching.GetProperty("identityDiagnostic").ValueKind).IsEqualTo(JsonValueKind.Null);
        }

        await Assert.That(console.Output).DoesNotContain("SECRET_DIVERGENT_ROSTER_CONTENT");
        await Assert.That(console.Output).DoesNotContain("SECRET_DIVERGENT_KPI_CONTENT");
    }

    [Test]
    public async Task Csv_byte_audit_reports_full_json_then_fails_without_writes_or_content_disclosure()
    {
        var (app, console, contextRepository, kpiRepository, publicationRepository) =
            CreateInvalidCsvAuditApp();

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "ehonda-dev-buli-2627",
            "--evaluation-date", "2026-08-21",
            "--validate-csv-bytes",
            "--json"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        using var json = JsonDocument.Parse(console.Output);
        await Assert.That(json.RootElement.GetProperty("expectedCount").GetInt32()).IsEqualTo(401);
        await Assert.That(json.RootElement.GetProperty("expectedCsvCount").GetInt32()).IsEqualTo(400);
        await Assert.That(json.RootElement.GetProperty("validCsvCount").GetInt32()).IsEqualTo(0);
        await Assert.That(json.RootElement.GetProperty("invalidCsvCount").GetInt32()).IsEqualTo(1);
        var rows = json.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        await Assert.That(rows.Length).IsEqualTo(402);
        var standings = rows.Single(row => row.GetProperty("name").GetString() == "bundesliga-standings.csv");
        var rules = rows.Single(row =>
            row.GetProperty("name").GetString() == "community-rules-ehonda-dev-buli-2627.md");
        var missing = rows.Single(row => row.GetProperty("name").GetString() == "recent-history-fcb.csv");
        var unexpected = rows.Single(row => row.GetProperty("name").GetString() == "operator-notes.csv");
        await Assert.That(standings.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.Invalid));
        await Assert.That(standings.GetProperty("csvByteDiagnostic").GetString())
            .IsEqualTo(BundesligaContextCsvFormatContract.CsvLineEndingNotCrLf);
        await Assert.That(rules.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.NotApplicable));
        await Assert.That(missing.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.Missing));
        await Assert.That(unexpected.GetProperty("csvByteState").GetString())
            .IsEqualTo(nameof(BundesligaContextCsvValidationState.NotApplicable));
        await AssertNoSecretContent(console.Output);
        VerifyNoWrites(contextRepository, kpiRepository, publicationRepository);
    }

    [Test]
    public async Task Csv_byte_audit_table_reports_states_and_counts_then_fails()
    {
        var (app, console, _, _, _) = CreateInvalidCsvAuditApp();
        console.Profile.Width = 500;

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "ehonda-dev-buli-2627",
            "--evaluation-date", "2026-08-21",
            "--validate-csv-bytes"
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("Expected CSV: 400");
        await Assert.That(console.Output).Contains("valid CSV: 0");
        await Assert.That(console.Output).Contains("invalid CSV: 1");
        await Assert.That(console.Output).Contains(nameof(BundesligaContextCsvValidationState.Invalid));
        await Assert.That(console.Output).Contains(nameof(BundesligaContextCsvValidationState.Missing));
        await Assert.That(console.Output).Contains(nameof(BundesligaContextCsvValidationState.NotApplicable));
        await Assert.That(console.Output).Contains(BundesligaContextCsvFormatContract.CsvLineEndingNotCrLf);
        await AssertNoSecretContent(console.Output);
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

    [Test]
    public async Task Known_community_conflict_is_rejected_before_repository_access()
    {
        var contextRepository = new Mock<IContextRepository>();
        var kpiRepository = new Mock<IKpiRepository>();
        var publicationRepository = new Mock<IDocumentPublicationRepository>();
        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);

        var exitCode = await app.RunAsync([
            "inventory",
            "--community-context", "ehonda-dev-wm26",
            "--competition", CompetitionIds.Bundesliga2026_27
        ]);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(console.Output).Contains("belongs to");
        contextRepository.Verify(repository => repository.GetContextDocumentNamesAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        kpiRepository.Verify(repository => repository.GetAllKpiDocumentsAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        publicationRepository.Verify(repository => repository.GetLastKnownGoodAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static (
        CommandApp App,
        TestConsole Console,
        Mock<IContextRepository> ContextRepository,
        Mock<IKpiRepository> KpiRepository,
        Mock<IDocumentPublicationRepository> PublicationRepository) CreateInvalidCsvAuditApp()
    {
        const string communityContext = "ehonda-dev-buli-2627";
        var contextRepository = new Mock<IContextRepository>();
        contextRepository.Setup(repository => repository.GetContextDocumentNamesAsync(
                communityContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                "bundesliga-standings.csv",
                $"community-rules-{communityContext}.md",
                "operator-notes.csv"
            ]);
        var documents = new Dictionary<string, ContextDocument>(StringComparer.Ordinal)
        {
            ["bundesliga-standings.csv"] = new(
                "bundesliga-standings.csv",
                BundesligaContextCsvFormatContract.StandingsHeader + "\nSECRET_INVALID_CSV_PAYLOAD\n",
                4,
                DateTimeOffset.UnixEpoch),
            [$"community-rules-{communityContext}.md"] = new(
                $"community-rules-{communityContext}.md",
                "SECRET_MARKDOWN_PAYLOAD",
                1,
                DateTimeOffset.UnixEpoch),
            ["operator-notes.csv"] = new(
                "operator-notes.csv",
                "SECRET_UNEXPECTED_CSV_PAYLOAD",
                2,
                DateTimeOffset.UnixEpoch)
        };
        contextRepository.Setup(repository => repository.GetLatestContextDocumentAsync(
                It.IsAny<string>(), communityContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, string _, CancellationToken _) => documents[name]);

        var kpiRepository = new Mock<IKpiRepository>();
        kpiRepository.Setup(repository => repository.GetAllKpiDocumentsAsync(
                communityContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var publicationRepository = new Mock<IDocumentPublicationRepository>();
        publicationRepository.Setup(repository => repository.GetLastKnownGoodAsync(
                It.IsAny<DocumentPublicationDefinition>(), communityContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);

        var (app, console) = CreateApp(contextRepository, kpiRepository, publicationRepository);
        return (app, console, contextRepository, kpiRepository, publicationRepository);
    }

    private static async Task AssertNoSecretContent(string output)
    {
        await Assert.That(output).DoesNotContain("SECRET_INVALID_CSV_PAYLOAD");
        await Assert.That(output).DoesNotContain("SECRET_MARKDOWN_PAYLOAD");
        await Assert.That(output).DoesNotContain("SECRET_UNEXPECTED_CSV_PAYLOAD");
    }

    private static void VerifyNoWrites(
        Mock<IContextRepository> contextRepository,
        Mock<IKpiRepository> kpiRepository,
        Mock<IDocumentPublicationRepository> publicationRepository)
    {
        contextRepository.Verify(repository => repository.SaveContextDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        contextRepository.Verify(repository => repository.SaveContextDocumentsAtomicallyAsync(
            It.IsAny<IReadOnlyList<ContextDocumentWrite>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        kpiRepository.Verify(repository => repository.SaveKpiDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        publicationRepository.Verify(repository => repository.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
