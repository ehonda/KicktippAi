using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Tests.Commands.Operations.CollectContext;
using Spectre.Console;
using Spectre.Console.Testing;
using TestUtilities;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Dev;

[NotInParallel("Telemetry")]
public class CompetitionProfileCollectionRunnerTests
{
    [Test]
    [Arguments("current-pending")]
    [Arguments("current-synchronized")]
    [Arguments("newer-watermark")]
    [Arguments("earlier-lane")]
    public async Task Production_completion_requires_current_synchronization_and_preserves_newer_replay(string state)
    {
        var identity = BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27, 123, 456);
        var now = BundesligaClubEloRefreshSourceTests.Now;
        var lanes = BundesligaContextSourceContract.ProductionConsumers;
        var lane = state == "earlier-lane" ? lanes[0] : lanes[^1];
        var community = state == "earlier-lane" ? "pes-squad" : "ehonda-ai-arena";
        var observation = CollectContextClubEloCommandTests.PreparedObservation(identity,"Eligible");
        var files = new ContextSourceBundleFiles(new(identity,now,now,lanes[0],lanes,[observation.Observation]),
            new Dictionary<string,byte[]> { [observation.Observation.Payload!.Path] = observation.PayloadBytes! });
        var receipt = new BundesligaContextSourceReceipt(new(identity, BundesligaContextSource.ClubElo, lane, community,
            observation.Observation.ObservationDigest, files.Digest, BundesligaContextSourceSelectionDisposition.NetworkAccepted,
            new string('c',64), BundesligaContextSourceSelectedOrigin.NetworkCandidate, BundesligaContextSourcePublicationDisposition.Published,
            new(new DateOnly(2026,9,6),null,null,null),null,new(0,0,0,null),[]),now);
        var cycle = new BundesligaContextSourceOuterCycle(identity,now,now,lanes[0],lanes,[BundesligaContextSource.ClubElo],
            state == "earlier-lane" ? BundesligaContextSourceCycleStatus.HandoffReady : BundesligaContextSourceCycleStatus.Complete,
            files.Digest, $"bundesliga-context-source-bundle-{identity.StorageId}", CompletedAtUtc: state == "earlier-lane" ? null : now);
        var healthIdentity = state == "newer-watermark" ? BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27,123,457) : identity;
        var watermark = new BundesligaContextSourceWatermark(healthIdentity.Sequence, healthIdentity.CycleId);
        var marker = "<!-- kicktippai:context-source-health:bundesliga-2026-27:club-elo -->";
        var hash = BundesligaContextSourceHealth.HashIssueBody(BundesligaContextSourceHealth.CreateIssueBody(marker, identity.Competition, BundesligaContextSource.ClubElo, watermark, []));
        var selections = lanes.Order(StringComparer.Ordinal).Select(value => new BundesligaContextSourceCommunitySelection(value,
            value == lanes[0] ? "pes-squad" : value == lanes[1] ? "schadensfresse" : value == lanes[2] ? "relaxdays-tippt" : "ehonda-ai-arena",
            new string('c',64),BundesligaContextSourceSelectedOrigin.NetworkCandidate,new DateOnly(2026,9,6),null,null,null,[])).ToArray();
        var health = new BundesligaContextSourceHealth(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo,watermark,healthIdentity.CycleId,
            new(0,0,0,0),new(new DateOnly(2026,9,6),null,null),null,selections,[],new(marker,
            "[KicktippAi] Bundesliga 2026/27 club-elo context-source health",hash,BundesligaContextSourceIssueState.Closed,
            state == "current-synchronized" ? hash : null,state == "current-synchronized" ? BundesligaContextSourceIssueSynchronization.Synchronized : BundesligaContextSourceIssueSynchronization.Pending,
            state == "current-synchronized" ? now : null,null));
        var repository = new Mock<IBundesligaContextSourceCycleRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetReceiptAsync(identity,BundesligaContextSource.ClubElo,lane,It.IsAny<CancellationToken>())).ReturnsAsync(receipt);
        repository.Setup(value => value.GetCycleAsync(identity,It.IsAny<CancellationToken>())).ReturnsAsync(cycle);
        repository.Setup(value => value.GetHealthAsync(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo,It.IsAny<CancellationToken>())).ReturnsAsync(health);
        using var services = new ServiceCollection().AddSingleton(repository.Object).BuildServiceProvider();
        var invocation = new CompetitionContextSourceCycleInvocation(identity,lane,lanes[0],lanes);
        var profile = new CompetitionCollectionProfileResolver().ResolveCompetition(identity.Competition) with { ContextSourceFeatures = new(true,false) };
        var request = new CompetitionProfileCollectionRequest(profile,community,community,null,false,"",false,false,ContextSourceCycle:invocation,ContextSourceOnly:true);
        var executor = new Mock<ICompetitionProfileCollectorExecutor>(MockBehavior.Strict);
        executor.Setup(value => value.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextSourceCyclePreparation(files,false,lane,cycle,new Dictionary<BundesligaContextSource,BundesligaContextSourceReceipt> { [BundesligaContextSource.ClubElo] = receipt }));
        await Assert.That(await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(),executor.Object,request,default,services))
            .IsEqualTo(state == "current-pending" ? 1 : 0);
        executor.Verify(value => value.ExecuteAsync(It.IsAny<CompetitionCollector>(),It.IsAny<CompetitionCollectorExecutionContext>(),It.IsAny<CancellationToken>()),Times.Never);
        if (state == "earlier-lane") repository.Verify(value => value.GetHealthAsync(It.IsAny<string>(),It.IsAny<BundesligaContextSourceScope>(),It.IsAny<BundesligaContextSource>(),It.IsAny<CancellationToken>()),Times.Never);
        repository.Verify(value => value.RecordReceiptAsync(It.IsAny<BundesligaContextSourceReceiptRequest>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    internal static ContextSourceCyclePreparation Preparation()
    {
        var identity = BundesligaContextSourceCycleIdentity.Development(CompetitionIds.Bundesliga2026_27, Guid.CreateVersion7().ToString("D"));
        var observed = CollectContextClubEloCommandTests.PreparedObservation(identity, "Eligible");
        var now = BundesligaClubEloRefreshSourceTests.Now;
        return new(new ContextSourceBundleFiles(new(identity, now, now, BundesligaContextSourceContract.DevelopmentLane,
            BundesligaContextSourceContract.DevelopmentConsumers, [observed.Observation]),
            new Dictionary<string, byte[]> { [observed.Observation.Payload!.Path] = observed.PayloadBytes! }), false);
    }

    [Test]
    [Arguments("preparation-timeout")]
    [Arguments("preparation-io")]
    public async Task Preparation_failure_returns_nonzero_and_executable_ordinary_collection_retains_head(string failure)
    {
        var head = BundesligaClubEloRefreshSourceTests.Loaded(BundesligaContextSourceContract.DevelopmentCommunity, new DateOnly(2026, 9, 6));
        var publications = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        publications.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,
            BundesligaContextSourceContract.DevelopmentCommunity, It.IsAny<CancellationToken>())).ReturnsAsync(head);
        var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(publications.Object);
        var seed = new Mock<IBundesligaClubEloSource>(MockBehavior.Strict);
        var command = new CollectContextClubEloCommand(new TestConsole(), factory.Object, seed.Object,
            new FakeLogger<CollectContextClubEloCommand>(), new Mock<IServiceProvider>(MockBehavior.Strict).Object);
        var executor = new Mock<ICompetitionProfileCollectorExecutor>(MockBehavior.Strict);
        executor.Setup(value => value.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure == "preparation-timeout" ? new OperationCanceledException() : new IOException(failure));
        var selected = new List<CompetitionCollector>();
        executor.Setup(value => value.ExecuteAsync(It.IsAny<CompetitionCollector>(), It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns((CompetitionCollector collector, CompetitionCollectorExecutionContext _, CancellationToken token) => {
                selected.Add(collector);
                return collector == CompetitionCollector.ClubElo ? command.ExecuteWithSettingsAsync(new() {
                    CommunityContext = BundesligaContextSourceContract.DevelopmentCommunity, Competition = CompetitionIds.Bundesliga2026_27 }, token) : Task.FromResult(0);
            });
        var profile = new CompetitionCollectionProfileResolver().ResolveCompetition(CompetitionIds.Bundesliga2026_27);
        var request = new CompetitionProfileCollectionRequest(profile with { ContextSourceFeatures = new(true, false) },
            BundesligaContextSourceContract.DevelopmentCommunity, BundesligaContextSourceContract.DevelopmentCommunity, null, false, "", false, false, ContextSourceOnly: true);
        await Assert.That(await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(), executor.Object, request, default)).IsEqualTo(1);
        await Assert.That(ContextSourceCyclePreparation.Current).IsNull();
        await Assert.That(await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(), executor.Object,
            request with { Profile = profile, ContextSourceOnly = false }, default)).IsEqualTo(0);
        await Assert.That(selected.ToArray()).IsEquivalentTo([CompetitionCollector.Kicktipp, CompetitionCollector.ClubElo, CompetitionCollector.Rosters]);
        publications.Verify(value => value.PublishAsync(It.IsAny<DocumentPublicationDefinition>(), It.IsAny<DocumentPublicationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        seed.VerifyNoOtherCalls();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Collector_caller_cancellation_propagates_but_operation_timeout_fails_and_releases_ambient(bool callerCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        var preparation = new ContextSourceCyclePreparation(Preparation().Files,true);
        await ContextSourceBundleHandoff.WriteDevelopmentAsync(preparation.Files);
        var executor = new Mock<ICompetitionProfileCollectorExecutor>(MockBehavior.Strict);
        executor.Setup(value => value.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preparation);
        executor.Setup(value => value.ExecuteAsync(CompetitionCollector.ClubElo, It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns((CompetitionCollector _, CompetitionCollectorExecutionContext _, CancellationToken token) => {
                if (ContextSourceCyclePreparation.Current != preparation) throw new InvalidOperationException("Missing active preparation.");
                if (callerCancelled) cancellation.Cancel();
                throw new OperationCanceledException(token);
            });
        var profile = new CompetitionCollectionProfileResolver().ResolveCompetition(CompetitionIds.Bundesliga2026_27) with { ContextSourceFeatures = new(true,false) };
        var request = new CompetitionProfileCollectionRequest(profile, BundesligaContextSourceContract.DevelopmentCommunity,
            BundesligaContextSourceContract.DevelopmentCommunity,null,false,"",false,false,ContextSourceOnly:true);
        if (callerCancelled)
            await Assert.That(() => CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(), executor.Object,request,cancellation.Token)).Throws<OperationCanceledException>();
        else
            await Assert.That(await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(),executor.Object,request,cancellation.Token)).IsEqualTo(1);
        await Assert.That(ContextSourceCyclePreparation.Current).IsNull();
        await Assert.That(Directory.Exists(ContextSourceBundleHandoff.CreateDevelopmentDirectory(preparation.Files.Bundle.Cycle))).IsFalse();
        using var activation = preparation.Activate(); // A subsequent collection can acquire the ambient lease.
    }

    [Test]
    public async Task Source_only_missing_preparation_fails_without_collector_resolution()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        var profile = new CompetitionCollectionProfileResolver().ResolveCompetition(CompetitionIds.Bundesliga2026_27) with { ContextSourceFeatures = new(true, false) };
        var result = await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(), executor.Object,
            new(profile, BundesligaContextSourceContract.DevelopmentCommunity, BundesligaContextSourceContract.DevelopmentCommunity,
                null, false, "", false, false, ContextSourceOnly: true), default);
        await Assert.That(result).IsEqualTo(1);
        executor.Verify(value => value.ExecuteAsync(It.IsAny<CompetitionCollector>(), It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Disabled_registration_remains_inert_without_GitHub_or_Firebase_environment()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(new TestConsole());
        services.AddCollectContextDevCommandServices();
        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<ICompetitionProfileCollectorExecutor>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<ICompetitionCollectionProfileResolver>()).IsNotNull();
    }
}

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, "context-source-cycle-repository", "Telemetry" })]
public class CompetitionProfileSourceOnlyRuntimeSmokeTests(FirestoreFixture fixture)
{
    [Test]
    [Arguments("publication-transaction")]
    [Arguments("receipt-reconciliation")]
    [Arguments("persisted-receipt-replay")]
    [Arguments("projection-failure")]
    [Arguments("projection-result-persistence")]
    [Arguments("disposal-guard")]
    public async Task Late_refresh_fault_returns_nonzero_and_ordinary_collection_preserves_durable_state(string fault)
    {
        await fixture.ClearDocumentPublicationsAsync();
        var production = fault is "projection-failure" or "projection-result-persistence" or "disposal-guard";
        var identity = production
            ? BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27,123,DateTime.UtcNow.Ticks)
            : BundesligaContextSourceCycleIdentity.Development(CompetitionIds.Bundesliga2026_27,Guid.CreateVersion7().ToString("D"));
        var lanes = production ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        var finalLane = lanes[^1];
        var community = production ? "ehonda-ai-arena" : BundesligaContextSourceContract.DevelopmentCommunity;
        var now = BundesligaClubEloRefreshSourceTests.Now;
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db,new FakeLogger<FirebaseContextSourceCycleRepository>());
        var publications = new FirebaseDocumentPublicationRepository(fixture.Db,new FakeLogger<FirebaseDocumentPublicationRepository>(),identity.Competition);
        var provider = new Mock<IBundesligaContextSourceObservationProvider>(MockBehavior.Strict);
        provider.SetupGet(value => value.Source).Returns(BundesligaContextSource.ClubElo);
        provider.Setup(value => value.ObserveAsync(identity,It.IsAny<CancellationToken>()))
            .ReturnsAsync(CollectContextClubEloCommandTests.PreparedObservation(identity,"Eligible"));
        var projector = new Mock<IBundesligaContextSourceIssueProjector>(MockBehavior.Strict);
        projector.Setup(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(),It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceHealth health,CancellationToken _) => Task.FromResult(
                fault == "projection-failure" ? BundesligaContextSourceIssueProjectionAttempt.Failed(BundesligaContextSourceIssueError.GithubIssueListFailed)
                    : BundesligaContextSourceIssueProjectionAttempt.Synchronized(health.DesiredIssueProjection!.BodySha256)));
        var coordinator = new ContextSourceCycleCoordinator(cycles,[provider.Object],projector.Object);
        var artifacts = new FixtureArtifactStore();
        var requestedCycle = new BundesligaContextSourceOuterCycle(identity,now,now,lanes[0],lanes,[BundesligaContextSource.ClubElo],BundesligaContextSourceCycleStatus.Claiming);
        var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        factory.Setup(value => value.CreateDocumentPublicationRepository(identity.Competition)).Returns(publications);
        using var ordinaryServices = new ServiceCollection().BuildServiceProvider();
        var ordinary = new CollectContextClubEloCommand(new TestConsole(),factory.Object,new BundesligaClubEloSeedSource(),
            new FakeLogger<CollectContextClubEloCommand>(),ordinaryServices);
        await Assert.That(await ordinary.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,CommunityContext = community })).IsEqualTo(0);
        var preexisting = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,community))!;

        // Production projection is reached only by the real final receipt after all seven prior lanes.
        for (var index = 0; production && index < lanes.Count - 1; index++)
        {
            await using var prior = await coordinator.PrepareAsync(new(requestedCycle,[BundesligaContextSource.ClubElo],false,lanes[index]),artifacts);
            using var activation = prior.Activate();
            using var priorServices = new ServiceCollection().AddSingleton(coordinator).BuildServiceProvider();
            var command = new CollectContextClubEloCommand(new TestConsole(),factory.Object,new BundesligaClubEloSeedSource(),
                new FakeLogger<CollectContextClubEloCommand>(),priorServices);
            var priorCommunity = index == 0 ? "pes-squad" : index == 1 ? "schadensfresse" : index == 2 ? "relaxdays-tippt" : "ehonda-ai-arena";
            await Assert.That(await command.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,CommunityContext = priorCommunity })).IsEqualTo(0);
        }

        var preparation = await coordinator.PrepareAsync(new(requestedCycle,[BundesligaContextSource.ClubElo],false,lanes[^1]),production ? artifacts : null);
        var healthBefore = await cycles.GetHealthAsync(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo);
        var faultyCycles = new Mock<IBundesligaContextSourceCycleRepository>(MockBehavior.Strict);
        faultyCycles.Setup(value => value.GetReceiptAsync(identity,BundesligaContextSource.ClubElo,finalLane,It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceCycleIdentity cycle,BundesligaContextSource source,string lane,CancellationToken token) => cycles.GetReceiptAsync(cycle,source,lane,token));
        faultyCycles.Setup(value => value.GetCycleAsync(identity,It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceCycleIdentity cycle,CancellationToken token) => cycles.GetCycleAsync(cycle,token));
        faultyCycles.Setup(value => value.GetSourceCycleAsync(identity,BundesligaContextSource.ClubElo,It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceCycleIdentity cycle,BundesligaContextSource source,CancellationToken token) => cycles.GetSourceCycleAsync(cycle,source,token));
        faultyCycles.Setup(value => value.RecordReceiptAsync(It.IsAny<BundesligaContextSourceReceiptRequest>(),It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceReceiptRequest receipt,CancellationToken token) => fault is "receipt-reconciliation" or "persisted-receipt-replay"
                ? Task.FromException<BundesligaContextSourceReceipt>(new IOException("receipt reconciliation unavailable")) : cycles.RecordReceiptAsync(receipt,token));
        faultyCycles.Setup(value => value.GetHealthAsync(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo,It.IsAny<CancellationToken>()))
            .Returns((string competition,BundesligaContextSourceScope scope,BundesligaContextSource source,CancellationToken token) => cycles.GetHealthAsync(competition,scope,source,token));
        faultyCycles.Setup(value => value.UpdateIssueProjectionAsync(It.IsAny<BundesligaContextSourceHealth>(),It.IsAny<BundesligaContextSourceIssueProjection>(),It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceHealth health,BundesligaContextSourceIssueProjection projection,CancellationToken token) => fault == "projection-result-persistence"
                ? Task.FromException<BundesligaContextSourceHealth>(new IOException("projection result unavailable")) : cycles.UpdateIssueProjectionAsync(health,projection,token));
        var faultyCoordinator = new ContextSourceCycleCoordinator(faultyCycles.Object,[provider.Object],projector.Object);
        var faultyPublications = new Mock<IDocumentPublicationRepository>(MockBehavior.Strict);
        faultyPublications.Setup(value => value.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,community,It.IsAny<CancellationToken>()))
            .Returns((DocumentPublicationDefinition definition,string context,CancellationToken token) => publications.GetLastKnownGoodAsync(definition,context,token));
        faultyPublications.Setup(value => value.PublishAsync(BundesligaDocumentPublication.ClubElo,It.IsAny<DocumentPublicationRequest>(),It.IsAny<CancellationToken>()))
            .Returns((DocumentPublicationDefinition definition,DocumentPublicationRequest request,CancellationToken token) => fault == "publication-transaction"
                ? Task.FromException<DocumentPublicationResult>(new IOException("publication transaction unavailable")) : publications.PublishAsync(definition,request,token));
        var faultyFactory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        faultyFactory.Setup(value => value.CreateDocumentPublicationRepository(identity.Competition)).Returns(faultyPublications.Object);
        using var services = new ServiceCollection().AddSingleton(faultyCoordinator).AddSingleton<IBundesligaContextSourceCycleRepository>(cycles).BuildServiceProvider();
        var collector = new CollectContextClubEloCommand(new TestConsole(),faultyFactory.Object,new BundesligaClubEloSeedSource(),
            new FakeLogger<CollectContextClubEloCommand>(),services);
        if (fault == "persisted-receipt-replay")
        {
            // Commit with the real command first, then fail the runner's exact-receipt replay callback.
            using (preparation.Activate())
            {
                using var commitServices = new ServiceCollection().AddSingleton(coordinator).BuildServiceProvider();
                var commit = new CollectContextClubEloCommand(new TestConsole(),factory.Object,new BundesligaClubEloSeedSource(),new FakeLogger<CollectContextClubEloCommand>(),commitServices);
                await Assert.That(await commit.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,CommunityContext = community })).IsEqualTo(0);
            }
            var receipt = (await cycles.GetReceiptAsync(identity,BundesligaContextSource.ClubElo,lanes[^1]))!;
            preparation = new(preparation.Files,true,lanes[^1],await cycles.GetCycleAsync(identity),
                new Dictionary<BundesligaContextSource,BundesligaContextSourceReceipt> { [BundesligaContextSource.ClubElo] = receipt },
                async (value,token) => { await faultyCoordinator.RecordReceiptAsync(value.Request,token); });
        }
        if (fault == "disposal-guard")
        {
            // Exercise the real DisposeAsync safety failure: a production lease must never use local development cleanup.
            preparation = new(preparation.Files,true,preparation.CurrentLaneId,preparation.PersistedCycle,preparation.PersistedReceipts);
        }
        var executor = new Mock<ICompetitionProfileCollectorExecutor>(MockBehavior.Strict);
        executor.Setup(value => value.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(),It.IsAny<CancellationToken>())).ReturnsAsync(preparation);
        executor.Setup(value => value.ExecuteAsync(CompetitionCollector.ClubElo,It.IsAny<CompetitionCollectorExecutionContext>(),It.IsAny<CancellationToken>()))
            .Returns((CompetitionCollector _,CompetitionCollectorExecutionContext _,CancellationToken token) => collector.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,CommunityContext = community },token));
        var profile = new CompetitionCollectionProfileResolver().ResolveCompetition(identity.Competition) with { ContextSourceFeatures = new(true,false) };
        var invocation = production ? new CompetitionContextSourceCycleInvocation(identity,lanes[^1],lanes[0],lanes) : null;
        var request = new CompetitionProfileCollectionRequest(profile,community,community,null,false,"",false,false,ContextSourceCycle:invocation,ContextSourceOnly:true);
        await Assert.That(await CompetitionProfileCollectionRunner.ExecuteAsync(new TestConsole(),executor.Object,request,default,services)).IsEqualTo(1);
        await Assert.That(ContextSourceCyclePreparation.Current).IsNull();
        using (preparation.Activate()) { } // Failure must release the ambient lease even when disposal throws.

        var durableHead = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,community))!;
        var durableReceipt = await cycles.GetReceiptAsync(identity,BundesligaContextSource.ClubElo,lanes[^1]);
        var durableHealth = await cycles.GetHealthAsync(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo);
        if (fault == "publication-transaction")
        {
            await Assert.That(durableHead.Snapshot.SnapshotId).IsEqualTo(preexisting.Snapshot.SnapshotId);
            await Assert.That(durableHead.Snapshot.MetadataJson).IsEqualTo(preexisting.Snapshot.MetadataJson);
            await Assert.That(durableReceipt).IsNull();
            await Assert.That(Serialize(durableHealth)).IsEqualTo(Serialize(healthBefore));
        }
        else
        {
            await Assert.That(durableReceipt).IsNotNull();
            await Assert.That(durableReceipt!.Request.SelectedSnapshotId).IsEqualTo(durableHead.Snapshot.SnapshotId);
            await Assert.That(durableReceipt.Request.Identity).IsEqualTo(identity);
            await Assert.That(durableHealth!.Watermark.CycleId).IsEqualTo(identity.CycleId);
        }
        // Executable ordinary source-off continuation reads and validates full original provenance.
        for (var repetition = 0; repetition < 5; repetition++)
            await Assert.That(await ordinary.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,CommunityContext = community })).IsEqualTo(0);
        var retainedHead = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo,community))!;
        await Assert.That(Serialize(retainedHead)).IsEqualTo(Serialize(durableHead));
        await Assert.That(Serialize(await cycles.GetReceiptAsync(identity,BundesligaContextSource.ClubElo,lanes[^1]))).IsEqualTo(Serialize(durableReceipt));
        await Assert.That(Serialize(await cycles.GetHealthAsync(identity.Competition,identity.Scope,BundesligaContextSource.ClubElo))).IsEqualTo(Serialize(durableHealth));
    }

    private static string Serialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

    private sealed class FixtureArtifactStore : IContextSourceBundleArtifactStore
    {
        private IReadOnlyList<ContextSourceArtifactEntry>? _entries;
        public Task<ContextSourceArtifactProbe> ProbeAsync(string artifactName,CancellationToken cancellationToken = default)
            => Task.FromResult(new ContextSourceArtifactProbe(_entries is null ? ContextSourceArtifactProbeDisposition.Absent : ContextSourceArtifactProbeDisposition.Present,_entries ?? []));
        public Task UploadAsync(string artifactName,IReadOnlyList<ContextSourceArtifactEntry> entries,bool overwrite,int compressionLevel,int retentionDays,CancellationToken cancellationToken = default)
        { _entries = entries; return Task.CompletedTask; }
    }

    [Test]
    public async Task Source_only_CLI_executes_real_DI_coordinator_publication_and_receipt_against_emulator_without_credentials()
    {
        var publications = new FirebaseDocumentPublicationRepository(fixture.Db, new FakeLogger<FirebaseDocumentPublicationRepository>(), CompetitionIds.Bundesliga2026_27);
        var factory = new Mock<IFirebaseServiceFactory>(MockBehavior.Strict);
        factory.SetupGet(value => value.FirestoreDb).Returns(fixture.Db);
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(publications);
        var credentials = new Mock<ICommunityKicktippCredentialLoader>(MockBehavior.Strict);
        var observationProvider = new Mock<IBundesligaContextSourceObservationProvider>(MockBehavior.Strict);
        observationProvider.SetupGet(value => value.Source).Returns(BundesligaContextSource.ClubElo);
        BundesligaContextSourceCycleIdentity? identity = null;
        observationProvider.Setup(value => value.ObserveAsync(It.IsAny<BundesligaContextSourceCycleIdentity>(), It.IsAny<CancellationToken>()))
            .Returns((BundesligaContextSourceCycleIdentity cycle, CancellationToken _) => {
                identity = cycle; return Task.FromResult(CollectContextClubEloCommandTests.PreparedObservation(cycle, "Eligible"));
            });
        var (app, console) = CreateCommandApp<CollectContextProfileCommand>("profile", firebaseServiceFactory: factory,
            configureServices: new Action<IServiceCollection>(services => {
                services.AddCollectContextDevCommandServices();
                services.RemoveAll<IBundesligaContextSourceObservationProvider>();
                services.AddSingleton(observationProvider.Object);
                services.AddSingleton(credentials.Object);
            }));
        var result = await RunCommandAsync(app, console, "profile", "--competition", CompetitionIds.Bundesliga2026_27,
            "--community-context", BundesligaContextSourceContract.DevelopmentCommunity, "--context-source-only", "--enable-club-elo-source");
        await Assert.That(result.ExitCode).IsEqualTo(0);
        credentials.VerifyNoOtherCalls();
        await Assert.That(identity).IsNotNull();
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>());
        var cycle = (await cycles.GetCycleAsync(identity!))!;
        await Assert.That(cycle.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        var receipt = (await cycles.GetReceiptAsync(identity!, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane))!;
        await Assert.That(receipt.Request.Identity).IsEqualTo(identity);
        var head = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, BundesligaContextSourceContract.DevelopmentCommunity))!;
        await Assert.That(head.Snapshot.SnapshotId).IsEqualTo(receipt.Request.SelectedSnapshotId);
        await Assert.That(BundesligaClubEloPublication.ReconstructLastKnownGood(head).RatedAt).IsEqualTo(new DateOnly(2026,9,6));
        var healthBefore = await cycles.GetHealthAsync(identity!.Competition, identity.Scope, BundesligaContextSource.ClubElo);
        var ordinary = new CollectContextClubEloCommand(new TestConsole(), factory.Object, new Mock<IBundesligaClubEloSource>(MockBehavior.Strict).Object,
            new FakeLogger<CollectContextClubEloCommand>(), new Mock<IServiceProvider>(MockBehavior.Strict).Object);
        for (var repetition = 0; repetition < 5; repetition++)
            await Assert.That(await ordinary.ExecuteWithSettingsAsync(new() { Competition = identity.Competition,
                CommunityContext = BundesligaContextSourceContract.DevelopmentCommunity })).IsEqualTo(0);
        var retained = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, BundesligaContextSourceContract.DevelopmentCommunity))!;
        await Assert.That(retained.Snapshot.SnapshotId).IsEqualTo(head.Snapshot.SnapshotId);
        await Assert.That(retained.Snapshot.MetadataJson).IsEqualTo(head.Snapshot.MetadataJson);
        await Assert.That(retained.Documents.Select(value => value.Content).ToArray()).IsEquivalentTo(head.Documents.Select(value => value.Content).ToArray());
        await Assert.That((await cycles.GetHealthAsync(identity.Competition, identity.Scope, BundesligaContextSource.ClubElo))!.Watermark).IsEqualTo(healthBefore!.Watermark);
        var retainedReceipt = await cycles.GetReceiptAsync(identity, BundesligaContextSource.ClubElo, BundesligaContextSourceContract.DevelopmentLane);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(retainedReceipt))
            .IsEqualTo(System.Text.Json.JsonSerializer.Serialize(receipt));
    }
}
