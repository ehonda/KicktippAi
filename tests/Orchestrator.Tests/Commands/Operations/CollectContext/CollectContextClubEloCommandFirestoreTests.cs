using System.Text;
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
public sealed class CollectContextClubEloCommandFirestoreTests(FirestoreFixture fixture)
{
    [Before(Test)]
    public async Task ClearAsync() => await fixture.ClearDocumentPublicationsAsync();

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
