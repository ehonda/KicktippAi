using EHonda.KicktippAi.Core;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using TestUtilities;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextClubEloCommandTests
{
    [Test]
    public async Task Complete_seed_publishes_the_canonical_atomic_definition_in_explicit_scope()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = SeedSource();
        var (app, console) = CreateApp(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console,
            "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
            "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("NetworkDisabled").And.Contains("Mapped manifest teams: 18/18");
        factory.Verify(factory => factory.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27), Times.Once);
        repository.Verify(repository => repository.PublishAsync(
            BundesligaDocumentPublication.ClubElo,
            It.Is<DocumentPublicationRequest>(request => request.CommunityContext == "ehonda-dev-buli-2627"
                && request.ExpectedPreviousSnapshotId == null
                && request.Documents.Length == 19
                && request.Documents.Count(document => document.Kind == DocumentPublicationKind.Context) == 18
                && request.Documents.Single(document => document.Kind == DocumentPublicationKind.Kpi).Name == "club-elo-rankings"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Dry_run_reads_lkg_for_diagnostics_but_never_publishes()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = SeedSource();
        var (app, console) = CreateApp(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console,
            "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
            "--community-context", "ehonda-dev-buli-2627", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run completed");
        repository.Verify(repository => repository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.ClubElo, "ehonda-dev-buli-2627", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repository => repository.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Missing_explicit_competition_fails_before_source_or_write()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = SeedSource();
        var (app, console) = CreateApp(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console,
            "collect-context-club-elo", "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Explicit --community-context and --competition are required");
        source.Verify(source => source.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repository => repository.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Blank_community_or_wrong_competition_fails_before_source_or_write()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = SeedSource();
        var (blankApp, blankConsole) = CreateApp(factory, source);

        var (blankExitCode, _) = await RunCommandAsync(blankApp, blankConsole,
            "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27);
        var (wrongApp, wrongConsole) = CreateApp(factory, source);
        var (wrongExitCode, output) = await RunCommandAsync(wrongApp, wrongConsole,
            "collect-context-club-elo", "--competition", CompetitionIds.FifaWorldCup2026,
            "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(blankExitCode).IsEqualTo(1);
        await Assert.That(wrongExitCode).IsEqualTo(1);
        await Assert.That(output).Contains("only supports bundesliga-2026-27");
        source.Verify(value => value.GetLatestAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.GetLastKnownGoodAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(value => value.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Rejected_seed_fails_closed_before_read_or_publish()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = new Mock<IBundesligaClubEloSource>();
        source.Setup(value => value.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BundesligaClubEloSourceResult.Rejected("MISSING_ALIAS:Schalke"));
        var (app, console) = CreateApp(factory, source);

        var (exitCode, output) = await RunCommandAsync(app, console,
            "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
            "--community-context", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("MISSING_ALIAS:Schalke");
        repository.Verify(repository => repository.GetLastKnownGoodAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repository => repository.PublishAsync(
            It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    [NotInParallel("Telemetry")]
    public async Task Dry_run_emits_complete_nonsecret_collection_activity_tags()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var source = SeedSource();
        var (app, console) = CreateApp(factory, source);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);

        var (exitCode, _) = await RunCommandAsync(app, console,
            "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
            "--community-context", "ehonda-dev-buli-2627", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        var activity = activities.Last(activity => activity.OperationName == "collect-context-club-elo"
            && activity.Tags.Any(tag => tag.Key == "club_elo.target_snapshot_id"));
        var tags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
        await Assert.That(tags["club_elo.origin"]).IsEqualTo("LaunchSeed");
        await Assert.That(tags["club_elo.selection_disposition"]).IsEqualTo("NetworkDisabled");
        await Assert.That(tags["club_elo.rated_at"]).IsEqualTo("2026-08-14");
        await Assert.That(tags["club_elo.mapping_coverage"]).IsEqualTo(18);
        await Assert.That((bool)tags["club_elo.dry_run"]!).IsTrue();
        await Assert.That(tags["club_elo.publication_disposition"]).IsEqualTo("DryRun");
        await Assert.That(tags.Keys).Contains("club_elo.collected_at").And.Contains("club_elo.source_url")
            .And.Contains("club_elo.age_days").And.Contains("club_elo.previous_snapshot_id")
            .And.Contains("club_elo.target_snapshot_id").And.Contains("club_elo.diagnostics");
    }

    [Test]
    public async Task Invalid_custom_seed_dry_runs_fail_closed_before_publication()
    {
        var valid = File.ReadAllText(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaClubEloSeed.RelativePath), Encoding.UTF8);
        var invalidSeeds = new[]
        {
            valid.Replace("s04,Schalke", "b04,Leverkusen", StringComparison.Ordinal),
            valid.Replace("b04,Leverkusen,16,1847", "b04,Leverkusen,rank,1847", StringComparison.Ordinal),
            string.Join("\r\n", valid.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[..^1]) + "\r\n"
        };

        foreach (var invalid in invalidSeeds)
        {
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(path, new UTF8Encoding(false).GetBytes(invalid));
                var repository = CreatePublicationRepository();
                var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
                var (app, console) = CreateApp(factory, SeedSource());

                var (exitCode, _) = await RunCommandAsync(app, console,
                    "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
                    "--community-context", "ehonda-dev-buli-2627", "--dry-run", "--seed", path);

                await Assert.That(exitCode).IsEqualTo(1);
                repository.Verify(value => value.GetLastKnownGoodAsync(
                    It.IsAny<DocumentPublicationDefinition>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
                repository.Verify(value => value.PublishAsync(
                    It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task Old_complete_custom_launch_seed_remains_usable_and_reports_age()
    {
        var valid = File.ReadAllText(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaClubEloSeed.RelativePath), Encoding.UTF8);
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, new UTF8Encoding(false).GetBytes(valid.Replace("2026-08-14", "2020-08-14", StringComparison.Ordinal)));
            var repository = CreatePublicationRepository();
            var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
            var (app, console) = CreateApp(factory, SeedSource());

            var (exitCode, output) = await RunCommandAsync(app, console,
                "collect-context-club-elo", "--competition", CompetitionIds.Bundesliga2026_27,
                "--community-context", "ehonda-dev-buli-2627", "--dry-run", "--seed", path);

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output).Contains("Rated at: 2020-08-14").And.Contains("days old");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (Spectre.Console.Cli.CommandApp App, Spectre.Console.Testing.TestConsole Console) CreateApp(
        Mock<IFirebaseServiceFactory> firebaseFactory,
        Mock<IBundesligaClubEloSource> source) =>
        CreateCommandApp<CollectContextClubEloCommand>("collect-context-club-elo", firebaseServiceFactory: firebaseFactory,
            configureServices: new Action<IServiceCollection>(services => services.AddSingleton(source.Object)));

    private static Mock<IBundesligaClubEloSource> SeedSource()
    {
        var source = new Mock<IBundesligaClubEloSource>();
        source.Setup(value => value.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Default));
        return source;
    }

    private static Mock<IDocumentPublicationRepository> CreatePublicationRepository()
    {
        var repository = new Mock<IDocumentPublicationRepository>();
        repository.SetupGet(value => value.Competition).Returns(CompetitionIds.Bundesliga2026_27);
        repository.Setup(value => value.GetLastKnownGoodAsync(
                BundesligaDocumentPublication.ClubElo, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoadedDocumentPublication?)null);
        repository.Setup(value => value.PublishAsync(
                BundesligaDocumentPublication.ClubElo, It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentPublicationResult(DocumentPublicationDisposition.Published,
                new DocumentPublicationSnapshot(CompetitionIds.Bundesliga2026_27, "ehonda-dev-buli-2627", "club-elo",
                    new string('a', 64), null, DateTimeOffset.UtcNow, "{}", [])));
        return repository;
    }
}
