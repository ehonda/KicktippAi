using EHonda.KicktippAi.Core;
using FirebaseAdapter;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure.Factories;
using Spectre.Console.Testing;
using TestUtilities;
using TUnit.Core;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.PublicationPayloadsParallelKey)]
public sealed class CollectContextRostersCommandFirestoreTests(FirestoreFixture fixture)
{
    [Before(Test)]
    public async Task ClearAsync() => await fixture.ClearDocumentPublicationsAsync();

    [Test]
    public async Task Real_firestore_command_preserves_expected_previous_and_dry_run_does_not_move_the_roster_head()
    {
        var community = $"roster-command-{Guid.NewGuid():N}";
        var repository = Repository();
        var factory = Factory(repository);
        var first = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource());
        var initial = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community);
        // The equal-date LKG tie-break is intentionally visible in the squad-summary source.
        // It creates one LastKnownGood-attributed snapshot before the next run becomes a no-op.
        var lkgSelected = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource());
        var lkgHead = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community);
        var unchanged = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource());

        var changedSeed = Path.GetTempFileName();
        try
        {
            var original = await File.ReadAllTextAsync(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaRosterSeed.RelativePath));
            await File.WriteAllTextAsync(changedSeed, original
                .Replace("Carles Martínez", "Carles Example", StringComparison.Ordinal)
                .Replace("2026-08-16", "2026-08-17", StringComparison.Ordinal));
            var changed = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource(), changedSeed);
            var advanced = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community);
            var dryRun = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource(), dryRun: true);
            var afterDryRun = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community);

            await Assert.That(first.ExitCode).IsEqualTo(0);
            await Assert.That(first.Output).Contains("publication Published");
            await Assert.That(lkgSelected.ExitCode).IsEqualTo(0);
            await Assert.That(lkgSelected.Output).Contains("publication Published");
            await Assert.That(lkgHead!.Snapshot.PreviousSnapshotId).IsEqualTo(initial!.Snapshot.SnapshotId);
            await Assert.That(unchanged.ExitCode).IsEqualTo(0);
            await Assert.That(unchanged.Output).Contains("publication Unchanged");
            await Assert.That(changed.ExitCode).IsEqualTo(0);
            await Assert.That(advanced!.Snapshot.PreviousSnapshotId).IsEqualTo(lkgHead!.Snapshot.SnapshotId);
            await Assert.That(dryRun.ExitCode).IsEqualTo(0);
            await Assert.That(afterDryRun!.Snapshot.SnapshotId).IsEqualTo(advanced.Snapshot.SnapshotId);
        }
        finally { File.Delete(changedSeed); }
    }

    [Test]
    public async Task Real_firestore_head_is_not_moved_by_retained_source_failure_or_reconstruction_corruption()
    {
        var community = $"roster-command-{Guid.NewGuid():N}";
        var repository = Repository();
        var factory = Factory(repository);
        await ExecuteAsync(factory.Object, community, new BundesligaRosterSource());
        var headed = (await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community))!;

        var retainedSource = new Mock<IBundesligaRosterSource>();
        retainedSource.Setup(value => value.CollectAsync(It.IsAny<BundesligaRosterSourceRequest>(), It.IsAny<BundesligaRosterLastKnownGood?>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BundesligaRosterSourceRequest _, BundesligaRosterLastKnownGood? lkg, DateOnly _, CancellationToken _) =>
                new BundesligaRosterCollection(lkg!.Snapshots, lkg.QualityRows, ["ENRICHMENT_UNAVAILABLE"], "seed", "manifest", "db", true));
        var retained = await ExecuteAsync(factory.Object, community, retainedSource.Object);
        var afterRetained = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community);
        await Assert.That(retained.ExitCode).IsEqualTo(0);
        await Assert.That(afterRetained!.Snapshot.SnapshotId).IsEqualTo(headed.Snapshot.SnapshotId);

        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, community, BundesligaDocumentPublication.Rosters.PublicationSet);
        var corrupted = headed.Snapshot.Documents.First(entry => entry.Kind == DocumentPublicationKind.Context);
        await fixture.Db.Collection("context-documents")
            .Document($"{DocumentPublicationContract.ComputeHeadId(scope)}_{corrupted.Name}_{corrupted.Version}")
            .UpdateAsync("content", "corrupt");
        var corruptAttempt = await ExecuteAsync(factory.Object, community, new BundesligaRosterSource());
        var head = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync();

        await Assert.That(corruptAttempt.ExitCode).IsEqualTo(1);
        await Assert.That(head.GetValue<string>("snapshotId")).IsEqualTo(headed.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Real_command_stale_cas_cannot_overwrite_a_competing_valid_roster_head()
    {
        var community = $"roster-command-race-{Guid.NewGuid():N}";
        var repository = Repository();
        var setupFactory = Factory(repository);
        await ExecuteAsync(setupFactory.Object, community, new BundesligaRosterSource());
        var initial = (await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community))!;
        var competingSnapshots = initial.Documents; // Pin the loaded graph before constructing the competing valid roster publication.
        var lkg = BundesligaRosterPublication.ReconstructLastKnownGood(initial);
        var competing = BundesligaRosterPublication.Build(
            lkg.Snapshots.Select(snapshot => snapshot with { MembershipAsOf = snapshot.MembershipAsOf.AddDays(1) }).ToArray(),
            lkg.QualityRows.Select(row => row with { MembershipAsOf = row.MembershipAsOf.AddDays(1) }).ToArray());
        var racing = new RacingRepository(repository, request => repository.PublishAsync(
            BundesligaDocumentPublication.Rosters,
            BundesligaRosterPublication.CreateRequest(community, request.ExpectedPreviousSnapshotId, competing)));
        var raced = await ExecuteAsync(Factory(racing).Object, community, new BundesligaRosterSource());
        var final = (await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, community))!;

        await Assert.That(competingSnapshots).Count().IsEqualTo(20);
        await Assert.That(raced.ExitCode).IsEqualTo(1);
        await Assert.That(final.Snapshot.SnapshotId).IsEqualTo(DocumentPublicationContract.ComputeSnapshotId(competing.Documents));
        await Assert.That(BundesligaRosterPublication.ReconstructLastKnownGood(final).Snapshots).Count().IsEqualTo(18);
    }

    private FirebaseDocumentPublicationRepository Repository() => new(
        fixture.Db, new FakeLogger<FirebaseDocumentPublicationRepository>(), CompetitionIds.Bundesliga2026_27);

    private static Mock<IFirebaseServiceFactory> Factory(IDocumentPublicationRepository repository)
    {
        var factory = new Mock<IFirebaseServiceFactory>();
        factory.Setup(value => value.CreateDocumentPublicationRepository(CompetitionIds.Bundesliga2026_27)).Returns(repository);
        return factory;
    }

    private static async Task<(int ExitCode, string Output)> ExecuteAsync(
        IFirebaseServiceFactory factory, string community, IBundesligaRosterSource source, string? seed = null, bool dryRun = false)
    {
        var console = new TestConsole();
        var command = new CollectContextRostersCommand(console, factory, source, TimeProvider.System, new FakeLogger<CollectContextRostersCommand>());
        var exitCode = await command.ExecuteWithSettingsAsync(new CollectContextRostersSettings
        {
            Competition = CompetitionIds.Bundesliga2026_27,
            CommunityContext = community,
            Seed = seed ?? Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaRosterSeed.RelativePath),
            Manifest = Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaTeamManifest.RelativePath),
            DryRun = dryRun
        });
        return (exitCode, console.Output);
    }

    private sealed class RacingRepository(
        IDocumentPublicationRepository inner,
        Func<DocumentPublicationRequest, Task<DocumentPublicationResult>> advance) : IDocumentPublicationRepository
    {
        private int _advanced;
        public string Competition => inner.Competition;
        public Task<LoadedDocumentPublication?> GetLastKnownGoodAsync(DocumentPublicationDefinition definition, string communityContext,
            CancellationToken cancellationToken = default) => inner.GetLastKnownGoodAsync(definition, communityContext, cancellationToken);
        public async Task<DocumentPublicationResult> PublishAsync(DocumentPublicationDefinition definition, DocumentPublicationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _advanced, 1) == 0) await advance(request);
            return await inner.PublishAsync(definition, request, cancellationToken);
        }
    }
}
