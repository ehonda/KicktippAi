using System.Security.Cryptography;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using TestUtilities;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextRostersCommandTests
{
    [Test]
    public async Task Explicit_scope_and_dry_run_build_all_documents_without_a_write()
    {
        var repository = Repository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = Source();
        var (app, console) = App(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console, "collect-context-rosters",
            "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed").And.Contains("roster-b04").And.Contains("team-squad-summary");
        repository.Verify(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Valid_scope_publishes_exactly_the_canonical_twenty_document_set()
    {
        var repository = Repository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = Source();
        var (app, console) = App(factory, source);

        var (exitCode, _) = await RunCommandAsync(app, console, "collect-context-rosters",
            "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(0);
        repository.Verify(value => value.PublishAsync(BundesligaDocumentPublication.Rosters,
            It.Is<DocumentPublicationRequest>(request => request.CommunityContext == "ehonda-dev-buli-2627"
                && request.ExpectedPreviousSnapshotId == null && request.Documents.Length == 20
                && request.Documents.Count(document => document.Kind == DocumentPublicationKind.Context) == 19
                && request.Documents.Single(document => document.Kind == DocumentPublicationKind.Kpi).Name == "team-squad-summary"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Missing_scope_or_partial_duckdb_provenance_fails_before_source_or_write()
    {
        var repository = Repository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = Source();
        var (app, console) = App(factory, source);
        var (missingScope, _) = await RunCommandAsync(app, console, "collect-context-rosters", "--community-context", "ehonda-dev-buli-2627");
        var (duckDbApp, duckDbConsole) = App(factory, source);
        var (badDuckDb, output) = await RunCommandAsync(duckDbApp, duckDbConsole, "collect-context-rosters",
            "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627", "--duckdb-path", "fake.duckdb");

        await Assert.That(missingScope).IsEqualTo(1);
        await Assert.That(badDuckDb).IsEqualTo(1);
        await Assert.That(output).Contains("requires --duckdb-revision and --duckdb-snapshot-date");
        source.Verify(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Launch_coverage_requires_a_pinned_database_and_rejects_a_hash_mismatch_before_source()
    {
        var path = Path.GetTempFileName();
        try
        {
            var repository = Repository();
            var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
            var source = Source();
            var (missingApp, missingConsole) = App(factory, source);
            var (missingExit, missingOutput) = await RunCommandAsync(missingApp, missingConsole, "collect-context-rosters",
                "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627",
                "--require-launch-coverage", "--dry-run");
            var (mismatchApp, mismatchConsole) = App(factory, source);
            var (mismatchExit, mismatchOutput) = await RunCommandAsync(mismatchApp, mismatchConsole, "collect-context-rosters",
                "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627",
                "--duckdb-path", path, "--duckdb-revision", "fixture@1", "--duckdb-snapshot-date", "2026-08-13",
                "--duckdb-sha256", new string('a', 64), "--require-launch-coverage", "--dry-run");

            await Assert.That(missingExit).IsEqualTo(1);
            await Assert.That(missingOutput).Contains("--require-launch-coverage requires --duckdb-path")
                .And.Contains("--duckdb-sha256");
            await Assert.That(mismatchExit).IsEqualTo(1);
            await Assert.That(mismatchOutput).Contains("DuckDB SHA-256 mismatch");
            source.Verify(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
            repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Launch_coverage_gate_reports_audited_counts_and_allows_a_dry_run_without_write()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            var repository = Repository();
            var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
            var source = LaunchSource();
            var (app, console) = App(factory, source);

            var (exitCode, output) = await RunCommandAsync(app, console, "collect-context-rosters",
                "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627",
                "--duckdb-path", path, "--duckdb-revision", "fixture@1", "--duckdb-snapshot-date", "2026-08-13",
                "--duckdb-sha256", sha256, "--require-launch-coverage", "--dry-run");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Launch roster coverage passed: ages 464/464, positions 464/464, valued 450/450");
            repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Launch_coverage_regression_fails_before_publication()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            var repository = Repository();
            var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
            var source = Source();
            var (app, console) = App(factory, source);

            var (exitCode, output) = await RunCommandAsync(app, console, "collect-context-rosters",
                "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627",
                "--duckdb-path", path, "--duckdb-revision", "fixture@1", "--duckdb-snapshot-date", "2026-08-13",
                "--duckdb-sha256", sha256, "--require-launch-coverage");

            await Assert.That(exitCode).IsEqualTo(1);
            await Assert.That(output).Contains("Bundesliga launch roster enrichment regressed");
            repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Retained_last_known_good_disposition_never_calls_publish()
    {
        var repository = Repository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = Source();
        source.Setup(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaRosterSourceRequest request, BundesligaRosterLastKnownGood? _, DateOnly _, CancellationToken _) =>
            {
                var root = SolutionPathUtility.FindSolutionRoot();
                var baseline = new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
                    Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath), null, null, null), null, new DateOnly(2026, 8, 18)).GetAwaiter().GetResult();
                return baseline with { RetainLastKnownGood = true, Diagnostics = ["ENRICHMENT_UNAVAILABLE"] };
            });
        var (app, console) = App(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console, "collect-context-rosters",
            "--competition", CompetitionIds.Bundesliga2026_27, "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Retained the exact headed last-known-good");
        repository.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IBundesligaRosterSource> Source()
    {
        var result = BaselineCollection();
        var source = new Mock<IBundesligaRosterSource>();
        source.Setup(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return source;
    }

    private static BundesligaRosterCollection BaselineCollection()
    {
        var root = SolutionPathUtility.FindSolutionRoot();
        return new BundesligaRosterSource().CollectAsync(new BundesligaRosterSourceRequest(
            Path.Combine(root, BundesligaRosterSeed.RelativePath), Path.Combine(root, BundesligaTeamManifest.RelativePath),
            null, null, null), null, new DateOnly(2026, 8, 18)).GetAwaiter().GetResult();
    }

    private static Mock<IBundesligaRosterSource> LaunchSource()
    {
        var baseline = BaselineCollection();
        var valuedRemaining = BundesligaRosterLaunchCoverage.RequiredValuedPlayerCount;
        var snapshots = baseline.Snapshots.Select(snapshot => snapshot with
        {
            Members = snapshot.Members.Select(member =>
            {
                if (member.Role != BundesligaRosterRole.Player || member.TransfermarktPlayerId is null)
                {
                    return member;
                }
                var value = valuedRemaining > 0 ? 1_000_000L : (long?)null;
                if (value is not null) valuedRemaining--;
                return member with { Age = 25, Position = BundesligaRosterPosition.Midfield, MarketValueEur = value };
            }).ToArray()
        }).ToArray();
        var rows = baseline.QualityRows.Select(row =>
        {
            var snapshot = snapshots.Single(value => value.Team.TeamSlug == row.Team.TeamSlug);
            var players = snapshot.Members.Where(member => member.Role == BundesligaRosterRole.Player).ToArray();
            return row with
            {
                SourceRevision = "fixture@1",
                DuckDbSnapshotAsOf = new DateOnly(2026, 8, 13),
                KnownAgeCount = players.Count(player => player.Age is not null),
                KnownPositionCount = players.Count(player => player.Position is not null),
                ValuedPlayerCount = players.Count(player => player.MarketValueEur is not null),
                DuckDbGateResult = BundesligaRosterDuckDbGateResult.Rejected,
                SelectionReason = "DUCKDB_REJECTED_USE_FALLBACK_SEED"
            };
        }).ToArray();
        var source = new Mock<IBundesligaRosterSource>();
        source.Setup(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline with { Snapshots = snapshots, QualityRows = rows });
        return source;
    }

    private static Mock<IDocumentPublicationRepository> Repository()
    {
        var repository = new Mock<IDocumentPublicationRepository>();
        repository.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);
        repository.Setup(value => value.PublishAsync(BundesligaDocumentPublication.Rosters, It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentPublicationResult(DocumentPublicationDisposition.Published,
                new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "ehonda-dev-buli-2627", "rosters", new string('a', 64), null, DateTimeOffset.UtcNow, "{}", [])));
        return repository;
    }

    private static (Spectre.Console.Cli.CommandApp App, Spectre.Console.Testing.TestConsole Console) App(Mock<IFirebaseServiceFactory> factory, Mock<IBundesligaRosterSource> source) =>
        CreateCommandApp<CollectContextRostersCommand>("collect-context-rosters", firebaseServiceFactory: factory,
            configureServices: new Action<IServiceCollection>(services => { services.AddSingleton(source.Object); services.AddSingleton(TimeProvider.System); }));
}
