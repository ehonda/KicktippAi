using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

public class FirebaseContextRepository_SaveContextDocumentsAtomicallyAsync_Tests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    private const string Community = "atomic-community";

    [Test]
    public async Task Complete_batch_is_versioned_atomically_and_same_content_is_a_no_op()
    {
        var repository = CreateRepository();
        var writes = new[]
        {
            new ContextDocumentWrite("away-history-bvb.csv", "away-v0"),
            new ContextDocumentWrite("recent-history-fcb.csv", "recent-v0")
        };

        var saved = await repository.SaveContextDocumentsAtomicallyAsync(writes, Community);
        var unchanged = await repository.SaveContextDocumentsAtomicallyAsync(writes, Community);

        await Assert.That(saved.Select(result => (result.DocumentName, result.Version)))
            .IsEquivalentTo(new[]
            {
                ("away-history-bvb.csv", (int?)0),
                ("recent-history-fcb.csv", (int?)0)
            });
        await Assert.That(unchanged.All(result => result.Version is null)).IsTrue();
        await Assert.That((await repository.GetLatestContextDocumentAsync("away-history-bvb.csv", Community))!.Content)
            .IsEqualTo("away-v0");
        await Assert.That((await repository.GetLatestContextDocumentAsync("recent-history-fcb.csv", Community))!.Content)
            .IsEqualTo("recent-v0");
    }

    [Test]
    public async Task Validation_failure_on_later_document_rolls_back_the_entire_batch()
    {
        var repository = CreateRepository();
        await repository.SaveContextDocumentAsync("away-history-bvb.csv", "away-last-known-good", Community);
        await Fixture.Db.Collection("context-documents").Document("deliberately-wrong-id")
            .SetAsync(new FirestoreContextDocument
            {
                Id = "deliberately-wrong-id",
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = Community,
                DocumentName = "recent-history-fcb.csv",
                Content = "corrupt",
                Version = 0,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            });

        await Assert.That(() => repository.SaveContextDocumentsAtomicallyAsync(
            [
                new ContextDocumentWrite("away-history-bvb.csv", "away-new"),
                new ContextDocumentWrite("recent-history-fcb.csv", "recent-new")
            ],
            Community)).Throws<InvalidDataException>();

        var rawAwayRows = await Fixture.Db.Collection("context-documents")
            .WhereEqualTo("competition", CompetitionIds.Bundesliga2026_27)
            .WhereEqualTo("communityContext", Community)
            .WhereEqualTo("documentName", "away-history-bvb.csv")
            .GetSnapshotAsync();
        await Assert.That(rawAwayRows.Count).IsEqualTo(1);
        await Assert.That(rawAwayRows.Documents[0].GetValue<string>("content")).IsEqualTo("away-last-known-good");
    }

    [Test]
    public async Task Concurrent_overlapping_batches_publish_coherent_versions_without_partial_latest_sets()
    {
        var repository = CreateRepository();
        await repository.SaveContextDocumentsAtomicallyAsync(
            [
                new ContextDocumentWrite("away-history-bvb.csv", "base"),
                new ContextDocumentWrite("recent-history-fcb.csv", "base")
            ],
            Community);

        var first = repository.SaveContextDocumentsAtomicallyAsync(
            [
                new ContextDocumentWrite("away-history-bvb.csv", "first"),
                new ContextDocumentWrite("recent-history-fcb.csv", "first")
            ],
            Community);
        var second = repository.SaveContextDocumentsAtomicallyAsync(
            [
                new ContextDocumentWrite("away-history-bvb.csv", "second"),
                new ContextDocumentWrite("recent-history-fcb.csv", "second")
            ],
            Community);
        await Task.WhenAll(first, second);

        var away = await repository.GetContextDocumentVersionsAsync("away-history-bvb.csv", Community);
        var recent = await repository.GetContextDocumentVersionsAsync("recent-history-fcb.csv", Community);
        await Assert.That(away.Select(document => document.Version)).IsEquivalentTo([0, 1, 2]);
        await Assert.That(recent.Select(document => document.Version)).IsEquivalentTo([0, 1, 2]);
        await Assert.That(away.OrderBy(document => document.Version).Select(document => document.Content))
            .IsEquivalentTo(recent.OrderBy(document => document.Version).Select(document => document.Content));
        await Assert.That(away[^1].Content).IsEqualTo(recent[^1].Content);
    }

    [Test]
    public async Task Publication_payload_versions_raise_the_batch_ceiling_without_becoming_ordinary_no_ops()
    {
        await Fixture.Db.Collection("context-documents").Document("publication-payload")
            .SetAsync(new FirestoreContextDocument
            {
                Competition = CompetitionIds.Bundesliga2026_27,
                CommunityContext = Community,
                PublicationSet = "diagnostics",
                DocumentName = "recent-history-fcb.csv",
                Content = "same",
                Version = 4,
                CreatedAt = Timestamp.GetCurrentTimestamp()
            });
        var repository = CreateRepository();

        var saved = await repository.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("recent-history-fcb.csv", "same")], Community);
        var unchanged = await repository.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("recent-history-fcb.csv", "same")], Community);

        await Assert.That(saved[0].Version).IsEqualTo(5);
        await Assert.That(unchanged[0].Version).IsNull();
    }

    [Test]
    public async Task Atomic_batches_are_isolated_by_exact_competition_and_community()
    {
        var bundesliga = CreateRepository();
        var worldCup = CreateRepository(competition: CompetitionIds.FifaWorldCup2026);

        var bundesligaResult = await bundesliga.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("shared.csv", "bundesliga-a")], "community-a");
        var otherCommunityResult = await bundesliga.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("shared.csv", "bundesliga-b")], "community-b");
        var worldCupResult = await worldCup.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("shared.csv", "world-cup")], "community-a");

        await Assert.That(bundesligaResult[0].Version).IsEqualTo(0);
        await Assert.That(otherCommunityResult[0].Version).IsEqualTo(0);
        await Assert.That(worldCupResult[0].Version).IsEqualTo(0);
        await Assert.That((await bundesliga.GetLatestContextDocumentAsync("shared.csv", "community-a"))!.Content)
            .IsEqualTo("bundesliga-a");
        await Assert.That((await bundesliga.GetLatestContextDocumentAsync("shared.csv", "community-b"))!.Content)
            .IsEqualTo("bundesliga-b");
        await Assert.That((await worldCup.GetLatestContextDocumentAsync("shared.csv", "community-a"))!.Content)
            .IsEqualTo("world-cup");
    }

    [Test]
    public async Task Reserved_key_or_cancellation_prevents_every_batch_write()
    {
        var repository = CreateRepository();
        await Assert.That(() => repository.SaveContextDocumentsAtomicallyAsync(
            [
                new ContextDocumentWrite("away-history-bvb.csv", "allowed"),
                new ContextDocumentWrite("roster-b04", "reserved")
            ], Community)).Throws<InvalidOperationException>();
        await Assert.That(await repository.GetLatestContextDocumentAsync("away-history-bvb.csv", Community)).IsNull();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.That(() => repository.SaveContextDocumentsAtomicallyAsync(
            [new ContextDocumentWrite("away-history-bvb.csv", "cancelled")],
            Community,
            cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(await repository.GetLatestContextDocumentAsync("away-history-bvb.csv", Community)).IsNull();
    }
}
