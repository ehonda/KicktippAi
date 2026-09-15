using System.Text;
using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console.Testing;
using TestUtilities;
using TUnit.Core;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(new[] { FirestoreFixture.PublicationPayloadsParallelKey, "context-source-cycle-repository" })]
public sealed class CollectContextClubEloCommandFirestoreTests(FirestoreFixture fixture)
{
    [Before(Test)]
    public async Task ClearAsync()
    {
        await fixture.ClearDocumentPublicationsAsync();
        foreach (var collection in new[] { "context-source-cycles", "context-source-cycle-observations", "context-source-cycle-receipts", "context-source-health" })
        {
            var documents = await fixture.Db.Collection(collection).GetSnapshotAsync();
            foreach (var document in documents.Documents) await document.Reference.DeleteAsync();
        }
    }

    [Test]
    public async Task Prepared_html_publication_retention_and_receipts_roundtrip_in_firestore_without_development_issue_work()
    {
        var community = BundesligaContextSourceContract.DevelopmentCommunity;
        var publications = new FirebaseDocumentPublicationRepository(fixture.Db, new FakeLogger<FirebaseDocumentPublicationRepository>(), CompetitionIds.Bundesliga2026_27);
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedClock());
        var projector = new Mock<IBundesligaContextSourceIssueProjector>();
        projector.Setup(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BundesligaContextSourceIssueProjectionAttempt.Failed(BundesligaContextSourceIssueError.GithubIssueListFailed));
        var coordinator = new ContextSourceCycleCoordinator(cycles, [], projector.Object, new FixedClock());

        var first = await PrepareAsync(cycles, "TransportRejected", 0);
        using (first.Activate())
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community)).IsEqualTo(0);
        var seed = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!;
        await Assert.That(seed.Snapshot.MetadataJson).Contains("club-elo-publication-v1");
        var seedReceipt = (await cycles.GetReceiptAsync(first.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, first.CurrentLaneId))!;
        await Assert.That(seedReceipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Published);
        await Assert.That(seedReceipt.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LaunchSeed);
        projector.Verify(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()), Times.Never);

        var accepted = await PrepareAsync(cycles, "Eligible", 1);
        using (accepted.Activate())
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community)).IsEqualTo(0);
        var html = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!;
        await Assert.That(html.Snapshot.MetadataJson).Contains("sourceDescriptor").And.Contains("club-elo/source.html");
        await Assert.That(html.Snapshot.PreviousSnapshotId).IsEqualTo(seed.Snapshot.SnapshotId);
        await Assert.That(BundesligaClubEloPublication.ReconstructLastKnownGood(html).RatedAt).IsEqualTo(new DateOnly(2026, 9, 6));
        var acceptedReceipt = (await cycles.GetReceiptAsync(accepted.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, accepted.CurrentLaneId))!;
        await Assert.That(acceptedReceipt.Request.SelectionDisposition).IsEqualTo(BundesligaContextSourceSelectionDisposition.NetworkAccepted);

        var rejected = await PrepareAsync(cycles, "DateRejected", 2);
        using (rejected.Activate())
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community)).IsEqualTo(0);
        var retained = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!;
        await Assert.That(retained.Snapshot.SnapshotId).IsEqualTo(html.Snapshot.SnapshotId);
        await Assert.That(retained.Snapshot.MetadataJson).IsEqualTo(html.Snapshot.MetadataJson);
        await Assert.That(retained.Snapshot.CreatedAt).IsEqualTo(html.Snapshot.CreatedAt);
        await Assert.That(retained.Snapshot.PreviousSnapshotId).IsEqualTo(html.Snapshot.PreviousSnapshotId);
        var retainedReceipt = (await cycles.GetReceiptAsync(rejected.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, rejected.CurrentLaneId))!;
        await Assert.That(retainedReceipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.NotAttempted);
        await Assert.That(retainedReceipt.Request.SelectedOrigin).IsEqualTo(BundesligaContextSourceSelectedOrigin.LastKnownGood);
        await Assert.That(retainedReceipt.Request.SourceDates.RatedAt).IsEqualTo(new DateOnly(2026, 9, 6));

        var replay = new ContextSourceCyclePreparation(rejected.Files, false, rejected.CurrentLaneId,
            await cycles.GetCycleAsync(rejected.Files.Bundle.Cycle),
            new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt> { [BundesligaContextSource.ClubElo] = retainedReceipt });
        using (replay.Activate())
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community)).IsEqualTo(0);
        var afterReplay = (await cycles.GetReceiptAsync(rejected.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, rejected.CurrentLaneId))!;
        await Assert.That(afterReplay.RecordedAtUtc).IsEqualTo(retainedReceipt.RecordedAtUtc);
        var health = (await cycles.GetHealthAsync(CompetitionIds.Bundesliga2026_27, BundesligaContextSourceScope.Development, BundesligaContextSource.ClubElo))!;
        await Assert.That(health.ConsecutiveFailures.Acquisition).IsEqualTo(1);
        await Assert.That(health.CommunitySelections.Single().RatedAt).IsEqualTo(new DateOnly(2026, 9, 6));
        projector.Verify(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Production_bundle_completes_all_eight_independent_lanes_with_one_shared_payload()
    {
        var publications = new FirebaseDocumentPublicationRepository(fixture.Db, new FakeLogger<FirebaseDocumentPublicationRepository>(), CompetitionIds.Bundesliga2026_27);
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedClock());
        var projector = new Mock<IBundesligaContextSourceIssueProjector>();
        projector.Setup(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BundesligaContextSourceIssueProjectionAttempt.Failed(BundesligaContextSourceIssueError.GithubIssueListFailed));
        var coordinator = new ContextSourceCycleCoordinator(cycles, [], projector.Object, new FixedClock());
        var prepared = await PrepareAsync(cycles, "Eligible", 0, production: true);
        foreach (var lane in BundesligaContextSourceContract.ProductionConsumers)
        {
            var preparation = new ContextSourceCyclePreparation(prepared.Files, false, lane, prepared.PersistedCycle,
                new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>());
            using (preparation.Activate())
                await Assert.That(await ExecutePreparedAsync(publications, coordinator, BundesligaClubEloRefreshSource.Community(BundesligaContextSourceScope.ProductionLive, lane))).IsEqualTo(0);
            var receipt = (await cycles.GetReceiptAsync(prepared.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, lane))!;
            await Assert.That(receipt.Request.SourceDates.RatedAt).IsEqualTo(new DateOnly(2026, 9, 6));
            await Assert.That(receipt.Request.ObservationDigest).IsEqualTo(prepared.Files.Bundle.Observations.Single().ObservationDigest);
        }
        var complete = (await cycles.GetCycleAsync(prepared.Files.Bundle.Cycle))!;
        await Assert.That(complete.Status).IsEqualTo(BundesligaContextSourceCycleStatus.Complete);
        var health = (await cycles.GetHealthAsync(CompetitionIds.Bundesliga2026_27, BundesligaContextSourceScope.ProductionLive, BundesligaContextSource.ClubElo))!;
        await Assert.That(health.CommunitySelections.Count).IsEqualTo(8);
        await Assert.That(health.ConsecutiveFailures.Acquisition).IsEqualTo(0);
        await Assert.That(health.CommunitySelections.Select(value => value.ConsumerLaneId).ToHashSet()
            .SetEquals(BundesligaContextSourceContract.ProductionConsumers)).IsTrue();
        projector.Verify(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()), Times.Once);
        var lastLane = BundesligaContextSourceContract.ProductionConsumers[^1];
        var persisted = (await cycles.GetReceiptAsync(complete.Identity, BundesligaContextSource.ClubElo, lastLane))!;
        var replay = new ContextSourceCyclePreparation(prepared.Files, false, lastLane, complete,
            new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt> { [BundesligaContextSource.ClubElo] = persisted });
        using (replay.Activate())
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, "ehonda-ai-arena")).IsEqualTo(0);
        projector.Verify(value => value.ProjectAsync(It.IsAny<BundesligaContextSourceHealth>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Rejected_seed_reactivates_original_snapshot_and_dry_run_adds_no_receipt()
    {
        var community = BundesligaContextSourceContract.DevelopmentCommunity;
        var publications = new FirebaseDocumentPublicationRepository(fixture.Db, new FakeLogger<FirebaseDocumentPublicationRepository>(), CompetitionIds.Bundesliga2026_27);
        var factory = new Mock<IFirebaseServiceFactory>();
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(publications);
        await ExecuteAsync(factory.Object, community, null, false);
        var original = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!;
        var headId = DocumentPublicationContract.ComputeHeadId(original.Snapshot.Scope);
        await fixture.Db.Collection("document-publication-heads").Document(headId).DeleteAsync();
        var cycles = new FirebaseContextSourceCycleRepository(fixture.Db, new FakeLogger<FirebaseContextSourceCycleRepository>(), new FixedClock());
        var coordinator = new ContextSourceCycleCoordinator(cycles, [], timeProvider: new FixedClock());
        var preparation = await PrepareAsync(cycles, "TransportRejected", 0);
        using (preparation.Activate())
        {
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community, dryRun: true)).IsEqualTo(0);
            await Assert.That(await cycles.GetReceiptAsync(preparation.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, preparation.CurrentLaneId)).IsNull();
            await Assert.That(await ExecutePreparedAsync(publications, coordinator, community)).IsEqualTo(0);
        }
        var receipt = (await cycles.GetReceiptAsync(preparation.Files.Bundle.Cycle, BundesligaContextSource.ClubElo, preparation.CurrentLaneId))!;
        await Assert.That(receipt.Request.PublicationDisposition).IsEqualTo(BundesligaContextSourcePublicationDisposition.Reactivated);
        var reactivated = (await publications.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!;
        await Assert.That(reactivated.Snapshot.MetadataJson).IsEqualTo(original.Snapshot.MetadataJson);
        await Assert.That(reactivated.Snapshot.CreatedAt).IsEqualTo(original.Snapshot.CreatedAt);
    }

    private static async Task<ContextSourceCyclePreparation> PrepareAsync(FirebaseContextSourceCycleRepository cycles,
        string evaluation, int sequence, bool production = false)
    {
        var now = BundesligaClubEloRefreshSourceTests.Now;
        var identity = production ? BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27, 1, sequence + 100)
            : BundesligaContextSourceCycleIdentity.Development(CompetitionIds.Bundesliga2026_27, $"0198f865-{0x1467 + sequence:x4}-7000-8000-000000000007");
        var consumers = production ? BundesligaContextSourceContract.ProductionConsumers : BundesligaContextSourceContract.DevelopmentConsumers;
        var outer = new BundesligaContextSourceOuterCycle(identity, now, now, consumers[0], consumers,
            [BundesligaContextSource.ClubElo], BundesligaContextSourceCycleStatus.Claiming);
        var result = CollectContextClubEloCommandTests.PreparedObservation(identity, evaluation);
        var bundle = new BundesligaContextSourceBundle(identity, now, now, consumers[0], consumers, [result.Observation]);
        var payloads = new Dictionary<string, byte[]>(); if (result.PayloadBytes is not null) payloads.Add(result.Observation.Payload!.Path, result.PayloadBytes);
        var files = new ContextSourceBundleFiles(bundle, payloads);
        await cycles.CreateOrResumeCycleAsync(outer);
        var token = BundesligaContextSourceContract.NewClaimToken();
        await cycles.ClaimSourceAsync(identity, BundesligaContextSource.ClubElo, token, now);
        await cycles.FinalizeSourceAsync(identity, BundesligaContextSource.ClubElo, token, result.Observation, now.AddMinutes(1));
        await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.ObservationsFinalized, BundesligaContextSourceCycleStatus.BundleVerified, files.Digest);
        var artifact = production ? $"bundesliga-context-source-bundle-{identity.StorageId}" : null;
        if (production)
            await cycles.TransitionCycleAsync(identity, BundesligaContextSourceCycleStatus.BundleVerified, BundesligaContextSourceCycleStatus.UploadReserved, files.Digest, artifact);
        var ready = await cycles.TransitionCycleAsync(identity, production ? BundesligaContextSourceCycleStatus.UploadReserved : BundesligaContextSourceCycleStatus.BundleVerified,
            BundesligaContextSourceCycleStatus.HandoffReady, files.Digest, artifact);
        return new(files, false, consumers[0], ready, new Dictionary<BundesligaContextSource, BundesligaContextSourceReceipt>());
    }

    private static async Task<int> ExecutePreparedAsync(IDocumentPublicationRepository repository, ContextSourceCycleCoordinator coordinator, string community, bool dryRun = false)
    {
        var factory = new Mock<IFirebaseServiceFactory>();
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(repository);
        var seed = new Mock<IBundesligaClubEloSource>();
        seed.Setup(value => value.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Default));
        var services = new ServiceCollection().AddSingleton(coordinator).BuildServiceProvider();
        var console = new TestConsole();
        var logger = new FakeLogger<CollectContextClubEloCommand>();
        var command = new CollectContextClubEloCommand(console, factory.Object, seed.Object, logger, services);
        var result = await command.ExecuteWithSettingsAsync(new() { Competition = CompetitionIds.Bundesliga2026_27, CommunityContext = community, DryRun = dryRun });
        if (result != 0) throw new InvalidOperationException(console.Output, logger.Collector.GetSnapshot().LastOrDefault()?.Exception);
        return result;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => BundesligaClubEloRefreshSourceTests.Now.AddMinutes(2);
    }

    [Test]
    public async Task Real_firestore_lifecycle_is_atomic_and_dry_run_never_moves_a_head()
    {
        var community = $"club-elo-command-{Guid.NewGuid():N}";
        var repository = new FirebaseDocumentPublicationRepository(
            fixture.Db,
            new FakeLogger<FirebaseDocumentPublicationRepository>(),
            CompetitionIds.Bundesliga2026_27);
        var factory = new Mock<IFirebaseServiceFactory>();
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27))
            .Returns(repository);

        var first = await ExecuteAsync(factory.Object, community, seed: null, dryRun: false);
        var loadedInitial = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community);
        var second = await ExecuteAsync(factory.Object, community, seed: null, dryRun: false);

        var customSeed = Path.GetTempFileName();
        try
        {
            var content = File.ReadAllText(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaClubEloSeed.RelativePath), Encoding.UTF8)
                .Replace("b04,Leverkusen,16,1847", "b04,Leverkusen,16,1848", StringComparison.Ordinal);
            await File.WriteAllBytesAsync(customSeed, new UTF8Encoding(false).GetBytes(content));
            var changed = await ExecuteAsync(factory.Object, community, customSeed, dryRun: false);
            var loadedChanged = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community);
            var headBeforeDryRun = loadedChanged!.Snapshot.SnapshotId;

            var dryRun = await ExecuteAsync(factory.Object, community, seed: null, dryRun: true);
            var headAfterDryRun = (await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, community))!.Snapshot.SnapshotId;

            await Assert.That(first.ExitCode).IsEqualTo(0);
            await Assert.That(first.Output).Contains("publication Published");
            await Assert.That(loadedInitial).IsNotNull();
            await Assert.That(second.ExitCode).IsEqualTo(0);
            await Assert.That(second.Output).Contains("publication Unchanged");
            await Assert.That(changed.ExitCode).IsEqualTo(0);
            await Assert.That(changed.Output).Contains("publication Published");
            await Assert.That(loadedChanged.Snapshot.PreviousSnapshotId).IsEqualTo(loadedInitial!.Snapshot.SnapshotId);
            await Assert.That(dryRun.ExitCode).IsEqualTo(0);
            await Assert.That(dryRun.Output).Contains("Dry run completed");
            await Assert.That(headAfterDryRun).IsEqualTo(headBeforeDryRun);

            var otherCommunity = $"{community}-isolated";
            var isolated = await ExecuteAsync(factory.Object, otherCommunity, seed: null, dryRun: false);
            var isolatedHead = (await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, otherCommunity))!.Snapshot.SnapshotId;
            await Assert.That(isolated.ExitCode).IsEqualTo(0);
            await Assert.That(isolatedHead).IsNotEqualTo(string.Empty);
            await Assert.That(DocumentPublicationContract.ComputeHeadId(new DocumentPublicationScope(
                CompetitionIds.Bundesliga2026_27, community, BundesligaDocumentPublication.ClubEloPublicationSet)))
                .IsNotEqualTo(DocumentPublicationContract.ComputeHeadId(new DocumentPublicationScope(
                    CompetitionIds.Bundesliga2026_27, otherCommunity, BundesligaDocumentPublication.ClubEloPublicationSet)));

            var corrupted = loadedChanged.Snapshot.Documents.Single(entry => entry.Name == "club-elo-b04.csv");
            var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, community, BundesligaDocumentPublication.ClubEloPublicationSet);
            await fixture.Db.Collection("context-documents")
                .Document($"{DocumentPublicationContract.ComputeHeadId(scope)}_{corrupted.Name}_{corrupted.Version}")
                .UpdateAsync("content", "corrupt");
            var corruptAttempt = await ExecuteAsync(factory.Object, community, seed: null, dryRun: false);
            var corruptHead = (await fixture.Db.Collection("document-publication-heads")
                .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync()).GetValue<string>("snapshotId");

            await Assert.That(corruptAttempt.ExitCode).IsEqualTo(1);
            await Assert.That(corruptHead).IsEqualTo(headBeforeDryRun);
        }
        finally
        {
            File.Delete(customSeed);
        }
    }

    private static async Task<(int ExitCode, string Output)> ExecuteAsync(
        IFirebaseServiceFactory factory,
        string community,
        string? seed,
        bool dryRun)
    {
        var console = new TestConsole();
        var source = new Mock<IBundesligaClubEloSource>();
        source.Setup(value => value.GetLatestAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BundesligaClubEloSourceResult.Complete(BundesligaClubEloSeed.Default));
        var command = new CollectContextClubEloCommand(
            console,
            factory,
            source.Object,
            new FakeLogger<CollectContextClubEloCommand>());
        var result = await command.ExecuteWithSettingsAsync(new CollectContextClubEloSettings
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = community,
            Seed = seed ?? BundesligaClubEloSeed.RelativePath,
            DryRun = dryRun
        });
        return (result, console.Output);
    }
}
