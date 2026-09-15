using EHonda.KicktippAi.Core;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using TestUtilities;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextClubEloCommandTests
{
    [Test]
    [Arguments("Eligible", false, "Published")]
    [Arguments("Eligible", true, "NotAttempted")]
    [Arguments("TransportRejected", false, "Published")]
    [Arguments("TransportRejected", true, "NotAttempted")]
    [Arguments("SizeRejected", true, "NotAttempted")]
    [Arguments("ResponseRejected", true, "NotAttempted")]
    [Arguments("DomRejected", true, "NotAttempted")]
    [Arguments("LexerRejected", true, "NotAttempted")]
    [Arguments("FragmentRejected", true, "NotAttempted")]
    [Arguments("DateRejected", true, "NotAttempted")]
    [Arguments("MappingRejected", true, "NotAttempted")]
    [Arguments("CoverageRejected", true, "NotAttempted")]
    [Arguments("StaleRejected", false, "Published")]
    [Arguments("StaleRejected", true, "NotAttempted")]
    [Arguments("NotNewer", true, "NotAttempted")]
    public async Task Every_prepared_outcome_completes_the_exact_receipt_without_reacquisition(string evaluation, bool hasHead, string expected)
    {
        var scenario = new PreparedScenario(evaluation, hasHead);
        using var activation = scenario.Preparation.Activate();
        var result = await scenario.Command.ExecuteWithSettingsAsync(Settings());
        await Assert.That(result).IsEqualTo(0);
        await Assert.That(scenario.Completed.Count).IsEqualTo(1);
        var receipt = scenario.Completed.Single();
        await Assert.That(receipt.PublicationDisposition.ToString()).IsEqualTo(expected);
        await Assert.That(receipt.SelectedOrigin).IsEqualTo(hasHead ? BundesligaContextSourceSelectedOrigin.LastKnownGood
            : evaluation == "Eligible" ? BundesligaContextSourceSelectedOrigin.NetworkCandidate : BundesligaContextSourceSelectedOrigin.LaunchSeed);
        await Assert.That(receipt.SourceDates.RatedAt).IsEqualTo(hasHead || evaluation == "Eligible" ? new DateOnly(2026, 9, 6) : BundesligaClubEloSeed.Default.RatedAt);
        await Assert.That(receipt.ObservationDigest).IsEqualTo(scenario.Observation.ObservationDigest);
        await Assert.That(scenario.Published.Count).IsEqualTo(hasHead ? 0 : 1);
        if (!hasHead)
        {
            var request = scenario.Published.Single();
            await Assert.That(request.SourcePublicationCommit).IsNotNull();
            await Assert.That(request.MetadataJson.Contains("sourceDescriptor", StringComparison.Ordinal)).IsEqualTo(evaluation == "Eligible");
        }
    }

    [Test]
    public async Task Prepared_dry_run_validates_candidate_and_does_no_receipt_or_publication_writes()
    {
        var scenario = new PreparedScenario("Eligible", false, dryRun: true);
        using var activation = scenario.Preparation.Activate();
        await Assert.That(await scenario.Command.ExecuteWithSettingsAsync(Settings(dryRun: true))).IsEqualTo(0);
        await Assert.That(scenario.Published.Count).IsEqualTo(0);
        await Assert.That(scenario.Completed.Count).IsEqualTo(0);
        scenario.Cycles.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Prepared_replay_is_receipt_first_and_uses_completion_after_the_guarded_commit()
    {
        var scenario = new PreparedScenario("Eligible", false);
        using (scenario.Preparation.Activate())
            await Assert.That(await scenario.Command.ExecuteWithSettingsAsync(Settings())).IsEqualTo(0);
        var request = scenario.Completed.Single();
        var persisted = new BundesligaContextSourceReceipt(request, BundesligaClubEloRefreshSourceTests.Now);
        var replay = new ContextSourceCyclePreparation(scenario.Preparation.Files, false, scenario.Preparation.CurrentLaneId,
            scenario.Preparation.PersistedCycle, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt> { [BundesligaContextSource.ClubElo] = persisted });
        scenario.Publications.Invocations.Clear();
        scenario.Seed.Invocations.Clear();
        using (replay.Activate())
            await Assert.That(await scenario.Command.ExecuteWithSettingsAsync(Settings())).IsEqualTo(0);
        await Assert.That(scenario.Completed.Count).IsEqualTo(2);
        scenario.Publications.VerifyNoOtherCalls();
        scenario.Seed.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Contradictory_not_newer_and_cross_lane_preparation_fail_without_mutation()
    {
        foreach (var crossed in new[] { false, true })
        {
            var scenario = new PreparedScenario("NotNewer", false);
            using var activation = scenario.Preparation.Activate();
            var settings = Settings(); if (crossed) settings.CommunityContext = "pes-squad";
            await Assert.That(await scenario.Command.ExecuteWithSettingsAsync(settings)).IsEqualTo(1);
            await Assert.That(scenario.Published.Count).IsEqualTo(0);
            await Assert.That(scenario.Completed.Count).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Disabled_legacy_command_never_resolves_source_cycle_http_artifact_or_receipt_services()
    {
        var repository = CreatePublicationRepository();
        var factory = CreateMockFirebaseServiceFactoryFull(documentPublicationRepository: repository);
        var services = new Mock<IServiceProvider>(MockBehavior.Strict);
        var command = new CollectContextClubEloCommand(new TestConsole(), factory.Object, SeedSource().Object,
            new FakeLogger<CollectContextClubEloCommand>(), services.Object);
        await Assert.That(await command.ExecuteWithSettingsAsync(Settings())).IsEqualTo(0);
        services.VerifyNoOtherCalls();
        repository.Verify(value => value.PublishAsync(BundesligaDocumentPublication.ClubElo,
            It.Is<DocumentPublicationRequest>(request => request.SourcePublicationCommit == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    internal static BundesligaContextSourceObservationResult PreparedObservation(BundesligaContextSourceCycleIdentity cycle, string evaluation)
    {
        var bytes = Encoding.UTF8.GetBytes(BundesligaClubEloRefreshSourceTests.Fixture());
        var response = new BundesligaClubEloRefresh.Response(evaluation == "ResponseRejected" ? 404 : 200,
            BundesligaClubEloRefresh.SourceUrl, 0, null, "text/html", "utf-8", [],
            evaluation == "SizeRejected" ? BundesligaClubEloRefresh.MaximumBytes + 1 : bytes.Length);
        var rows = Encoding.UTF8.GetString(BundesligaClubEloRefresh.CanonicalMappingBytes()).Split("\r\n").Skip(1)
            .Select((line, index) => { var fields = line.Split(','); return (fields[0], new BundesligaClubEloRefresh.Row(fields[1], fields[2], 18 - index, 1500)); }).ToArray();
        var hasDate = evaluation is "MappingRejected" or "CoverageRejected" or "StaleRejected" or "NotNewer" or "Eligible";
        var hasRows = evaluation is "CoverageRejected" or "StaleRejected" or "NotNewer" or "Eligible";
        return BundesligaClubEloRefresh.CreateObservation(cycle, BundesligaClubEloRefreshSourceTests.Now, evaluation,
            evaluation == "TransportRejected" ? null : response,
            evaluation is "TransportRejected" or "SizeRejected" ? null : bytes,
            hasDate ? evaluation == "StaleRejected" ? "2026-08-29" : "2026-09-06" : null,
            hasRows ? evaluation == "CoverageRejected" ? rows[..1] : rows : null);
    }

    private static CollectContextClubEloSettings Settings(bool dryRun = false) => new()
    {
        Competition = CompetitionIds.Bundesliga2026_27, CommunityContext = BundesligaContextSourceContract.DevelopmentCommunity, DryRun = dryRun
    };

    private sealed class PreparedScenario
    {
        public Mock<IDocumentPublicationRepository> Publications { get; } = new(MockBehavior.Strict);
        public Mock<IBundesligaContextSourceCycleRepository> Cycles { get; } = new(MockBehavior.Strict);
        public Mock<IBundesligaClubEloSource> Seed { get; } = SeedSource();
        public List<DocumentPublicationRequest> Published { get; } = [];
        public List<BundesligaContextSourceReceiptRequest> Completed { get; } = [];
        public ContextSourceCyclePreparation Preparation { get; }
        public BundesligaContextSourceObservation Observation { get; }
        public CollectContextClubEloCommand Command { get; }

        public PreparedScenario(string evaluation, bool hasHead, bool dryRun = false)
        {
            var now = BundesligaClubEloRefreshSourceTests.Now;
            var cycle = BundesligaContextSourceCycleIdentity.Development(CompetitionIds.Bundesliga2026_27, "0198f865-1467-7000-8000-000000000007");
            var result = PreparedObservation(cycle, evaluation); Observation = result.Observation;
            var bundle = new BundesligaContextSourceBundle(cycle, now, now, BundesligaContextSourceContract.DevelopmentLane,
                BundesligaContextSourceContract.DevelopmentConsumers, [Observation]);
            var payloads = new Dictionary<string, byte[]>(); if (result.PayloadBytes is not null) payloads.Add(Observation.Payload!.Path, result.PayloadBytes);
            var files = new ContextSourceBundleFiles(bundle, payloads);
            var persistedCycle = new BundesligaContextSourceOuterCycle(cycle, now, now, bundle.ProducerLaneId, bundle.ExpectedConsumers,
                [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.HandoffReady, files.Digest);
            Preparation = new(files, false, bundle.ProducerLaneId, dryRun ? null : persistedCycle, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>());
            var finalized = new BundesligaContextSourceCycleClaim(cycle, BundesligaContextSource.ClubElo, Observation.AttemptId,
                BundesligaContextSourceSourceStatus.Finalized, BundesligaContextSourceContract.NewClaimToken(), now, now.AddMinutes(10),
                now, Observation.ObservationDigest, Observation, null, [], null);
            BundesligaContextSourceReceipt? receipt = null;
            Cycles.Setup(value => value.GetReceiptAsync(cycle, BundesligaContextSource.ClubElo, bundle.ProducerLaneId, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(receipt));
            Cycles.Setup(value => value.GetCycleAsync(cycle, It.IsAny<CancellationToken>())).ReturnsAsync(persistedCycle);
            Cycles.Setup(value => value.GetSourceCycleAsync(cycle, BundesligaContextSource.ClubElo, It.IsAny<CancellationToken>())).ReturnsAsync(finalized);
            Cycles.Setup(value => value.GetHealthAsync(cycle.Competition, cycle.Scope, BundesligaContextSource.ClubElo, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BundesligaContextSourceHealth?)null);
            Cycles.Setup(value => value.RecordReceiptAsync(It.IsAny<BundesligaContextSourceReceiptRequest>(), It.IsAny<CancellationToken>()))
                .Returns((BundesligaContextSourceReceiptRequest request, CancellationToken _) =>
                {
                    BundesligaContextSourceReceiptContract.ValidateAgainstObservation(request, Observation);
                    Completed.Add(request); receipt = new(request, now); return Task.FromResult(receipt);
                });
            Publications.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,
                BundesligaContextSourceContract.DevelopmentCommunity, It.IsAny<CancellationToken>()))
                .ReturnsAsync(hasHead ? BundesligaClubEloRefreshSourceTests.Loaded(BundesligaContextSourceContract.DevelopmentCommunity, new DateOnly(2026, 9, 6)) : null);
            Publications.Setup(value => value.PublishAsync(BundesligaDocumentPublication.ClubElo, It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()))
                .Returns((DocumentPublicationDefinition _, DocumentPublicationRequest request, CancellationToken _) =>
                {
                    Published.Add(request);
                    var id = DocumentPublicationContract.ComputeSnapshotId(request.Documents);
                    var commit = request.SourcePublicationCommit!; var guard = commit.Guard; var template = commit.ReceiptTemplate;
                    receipt = new(new(guard.Identity, guard.Source, guard.ConsumerLaneId, guard.CommunityContext, guard.ObservationDigest,
                        guard.BundleDigest, template.SelectionDisposition, id, template.SelectedOrigin, BundesligaContextSourcePublicationDisposition.Published,
                        template.SourceDates, template.RosterRevision, template.CarriedFields, template.ActiveConditions), now);
                    return Task.FromResult(new DocumentPublicationResult(DocumentPublicationDisposition.Published,
                        new DocumentPublicationSnapshot(cycle.Competition, request.CommunityContext, "club-elo", id, null, now, request.MetadataJson, [])));
                });
            var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
            factory.Setup(value => value.CreateDocumentPublicationRepository(cycle.Competition)).Returns(Publications.Object);
            var services = new ServiceCollection().AddSingleton(new ContextSourceCycleCoordinator(Cycles.Object, [])).BuildServiceProvider();
            Command = new(new TestConsole(), factory.Object, Seed.Object, new FakeLogger<CollectContextClubEloCommand>(), services);
        }
    }

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
