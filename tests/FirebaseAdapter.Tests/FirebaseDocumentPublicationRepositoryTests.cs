using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;
using TUnit.Core;

namespace FirebaseAdapter.Tests;

[ClassDataSource<FirestoreFixture>(Shared = SharedType.Keyed, Key = FirestoreFixture.SharedKey)]
[NotInParallel(FirestoreFixture.PublicationPayloadsParallelKey)]
public sealed class FirebaseDocumentPublicationRepositoryTests(FirestoreFixture fixture)
{
    private string Community { get; } = $"publication-{Guid.NewGuid():N}";
    private static readonly DocumentPublicationDefinition Definition = new(
        "fixture-publication",
        [
            new DocumentPublicationKey(DocumentPublicationKind.Context, "fixture-context"),
            new DocumentPublicationKey(DocumentPublicationKind.Kpi, "fixture-kpi")
        ]);

    [Before(Test)]
    public async Task ClearAsync() => await fixture.ClearDocumentPublicationsAsync();

    [Test]
    public async Task Initial_mixed_publish_creates_one_headed_snapshot_and_exact_read()
    {
        var repository = CreateRepository();
        var result = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));
        var loaded = await repository.GetLastKnownGoodAsync(Definition, Community);

        await Assert.That(result.Disposition).IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(result.Snapshot.Documents.Length).IsEqualTo(2);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Documents.Select(document => document.Content)).Contains("context-v1");
        await Assert.That(loaded.Documents.Select(document => document.Content)).Contains("kpi-v1");
    }

    [Test]
    public async Task Unchanged_and_stale_cas_are_decided_before_writes()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-v1", "kpi-v1")))
            .Throws<DocumentPublicationConcurrencyException>();
        var unchanged = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1", first.Snapshot.SnapshotId));

        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(unchanged.Snapshot.SnapshotId).IsEqualTo(first.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Changed_document_increments_while_unchanged_headed_document_reuses_its_version()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));
        var second = await repository.PublishAsync(Definition, Request("context-v2", "kpi-v1", first.Snapshot.SnapshotId));

        await Assert.That(second.Snapshot.Documents.Single(entry => entry.Name == "fixture-context").Version).IsEqualTo(1);
        await Assert.That(second.Snapshot.Documents.Single(entry => entry.Name == "fixture-kpi").Version).IsEqualTo(0);
    }

    [Test]
    public async Task Changed_document_allocates_above_an_unheaded_existing_maximum_version()
    {
        var repository = CreateRepository();
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var prefix = DocumentPublicationContract.ComputeHeadId(scope);
        for (var version = 0; version < 2; version++)
        {
            await fixture.Db.Collection("context-documents").Document($"{prefix}_fixture-context_{version}").SetAsync(
                new FirestoreContextDocument
                {
                    Competition = scope.Competition,
                    CommunityContext = scope.CommunityContext,
                    PublicationSet = scope.PublicationSet,
                    DocumentName = "fixture-context",
                    Content = $"unheaded-{version}",
                    Version = version,
                    CreatedAt = Timestamp.GetCurrentTimestamp()
                });
        }

        var published = await repository.PublishAsync(Definition, Request("context-v1", "kpi-v1"));

        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Name == "fixture-context").Version).IsEqualTo(2);
    }

    [Test]
    public async Task A_to_b_to_a_reactivation_preserves_immutable_ancestry_entries_and_timestamp()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var second = await repository.PublishAsync(Definition, Request("context-b", "kpi-b", first.Snapshot.SnapshotId));
        var reactivated = await repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId));

        await Assert.That(reactivated.Disposition).IsEqualTo(DocumentPublicationDisposition.Reactivated);
        await Assert.That(reactivated.Snapshot.SnapshotId).IsEqualTo(first.Snapshot.SnapshotId);
        await Assert.That(reactivated.Snapshot.PreviousSnapshotId).IsNull();
        await Assert.That(reactivated.Snapshot.CreatedAt.ToUnixTimeMilliseconds())
            .IsEqualTo(first.Snapshot.CreatedAt.ToUnixTimeMilliseconds());
        await Assert.That(reactivated.Snapshot.Documents).IsEquivalentTo(first.Snapshot.Documents);
    }

    [Test]
    public async Task Current_and_reactivation_misfiled_metadata_fail_closed_without_moving_the_head()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var second = await repository.PublishAsync(Definition, Request("context-b", "kpi-b", first.Snapshot.SnapshotId));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);

        await CorruptStoredSnapshotIdAsync(scope, second.Snapshot.SnapshotId, first.Snapshot.SnapshotId);
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId)))
            .Throws<InvalidDataException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(second.Snapshot.SnapshotId);

        await CorruptStoredSnapshotIdAsync(scope, second.Snapshot.SnapshotId, second.Snapshot.SnapshotId);
        await CorruptStoredSnapshotIdAsync(scope, first.Snapshot.SnapshotId, second.Snapshot.SnapshotId);
        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a", second.Snapshot.SnapshotId)))
            .Throws<InvalidDataException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(second.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Stale_cas_wins_over_a_corrupt_current_snapshot_graph()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        await CorruptStoredSnapshotIdAsync(scope, first.Snapshot.SnapshotId, new string('a', DocumentPublicationContract.Sha256HexLength));

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-b", "kpi-b")))
            .Throws<DocumentPublicationConcurrencyException>();
        await Assert.That(await HeadSnapshotIdAsync(scope)).IsEqualTo(first.Snapshot.SnapshotId);
    }

    [Test]
    public async Task Reserved_generic_exact_reads_prefer_the_canonical_publication_scoped_payload_ids()
    {
        var rosterContext = new DocumentPublicationKey(
            DocumentPublicationKind.Context,
            BundesligaRosterPublicationContract.AggregateRosterDocumentName);
        var eloKpi = new DocumentPublicationKey(DocumentPublicationKind.Kpi, BundesligaDocumentPublication.ClubEloRankingsDocumentName);
        var rosterScope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, BundesligaDocumentPublication.Rosters.PublicationSet);
        var eloScope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, BundesligaDocumentPublication.ClubElo.PublicationSet);
        await fixture.Db.Collection("context-documents")
            .Document(PublicationPayloadId(rosterScope, rosterContext.Name, 7))
            .SetAsync(new FirestoreContextDocument
            {
                Competition = rosterScope.Competition, CommunityContext = Community, PublicationSet = rosterScope.PublicationSet,
                DocumentName = rosterContext.Name, Content = "published roster", Version = 7, CreatedAt = Timestamp.GetCurrentTimestamp()
            });
        await fixture.Db.Collection("kpi-documents")
            .Document(PublicationPayloadId(eloScope, eloKpi.Name, 3))
            .SetAsync(new FirestoreKpiDocument
            {
                Competition = eloScope.Competition, CommunityContext = Community, PublicationSet = eloScope.PublicationSet,
                DocumentName = eloKpi.Name, Content = "published elo", Description = "Elo", Version = 3, CreatedAt = Timestamp.GetCurrentTimestamp()
            });

        var context = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), CompetitionIds.Bundesliga2026_27);
        var kpi = new FirebaseKpiRepository(fixture.Db, new FakeLogger<FirebaseKpiRepository>(), CompetitionIds.Bundesliga2026_27);
        var loadedContext = await context.GetContextDocumentAsync(rosterContext.Name, 7, Community);
        var loadedKpi = await kpi.GetKpiDocumentAsync(eloKpi.Name, Community, 3);

        await Assert.That(loadedContext!.Content).IsEqualTo("published roster");
        await Assert.That(loadedKpi!.Content).IsEqualTo("published elo");
    }

    [Test]
    public async Task Canonical_roster_and_elo_definitions_publish_and_read_all_required_documents()
    {
        var repository = CreateRepository();
        var roster = await repository.PublishAsync(BundesligaDocumentPublication.Rosters, CanonicalRequest(BundesligaDocumentPublication.Rosters));
        var elo = await repository.PublishAsync(BundesligaDocumentPublication.ClubElo, CanonicalRequest(BundesligaDocumentPublication.ClubElo));
        var loadedRoster = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.Rosters, Community);
        var loadedElo = await repository.GetLastKnownGoodAsync(BundesligaDocumentPublication.ClubElo, Community);

        await Assert.That(roster.Snapshot.Documents.Length).IsEqualTo(20);
        await Assert.That(elo.Snapshot.Documents.Length).IsEqualTo(19);
        await Assert.That(loadedRoster!.Documents.Length).IsEqualTo(20);
        await Assert.That(loadedElo!.Documents.Length).IsEqualTo(19);
    }

    [Test]
    public async Task Same_expected_concurrent_publishers_have_one_winner_and_no_loser_graph()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var leftRequest = Request("context-left", "kpi-left", first.Snapshot.SnapshotId);
        var rightRequest = Request("context-right", "kpi-right", first.Snapshot.SnapshotId);
        var leftTask = repository.PublishAsync(Definition, leftRequest);
        var rightTask = repository.PublishAsync(Definition, rightRequest);
        var left = await CaptureAsync(leftTask);
        var right = await CaptureAsync(rightTask);

        await Assert.That(new[] { left.Result, right.Result }.Count(result => result is not null)).IsEqualTo(1);
        await Assert.That(new[] { left.Exception, right.Exception }.Single(exception => exception is not null))
            .IsTypeOf<DocumentPublicationConcurrencyException>();
        var winner = left.Result ?? right.Result!;
        var loserRequest = left.Result is null ? leftRequest : rightRequest;
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var loserSnapshot = DocumentPublicationContract.ComputeSnapshotId(loserRequest.Documents);
        var loaded = await repository.GetLastKnownGoodAsync(Definition, Community);

        await Assert.That(loaded!.Snapshot.SnapshotId).IsEqualTo(winner.Snapshot.SnapshotId);
        var loserMetadata = await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, loserSnapshot)).GetSnapshotAsync();
        await Assert.That(loserMetadata.Exists).IsFalse();
    }

    [Test]
    public async Task Payload_create_collision_rolls_back_without_a_head_or_partial_peer_payload()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var collidingId = PublicationPayloadId(scope, "fixture-context", 0);
        await fixture.Db.Collection("context-documents").Document(collidingId).SetAsync(new Dictionary<string, object>
        {
            ["collision"] = true
        });
        var repository = CreateRepository();

        await Assert.That(() => repository.PublishAsync(Definition, Request("context-a", "kpi-a")))
            .Throws<Exception>();
        var head = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync();
        var kpi = await fixture.Db.Collection("kpi-documents")
            .Document(PublicationPayloadId(scope, "fixture-kpi", 0)).GetSnapshotAsync();
        await Assert.That(head.Exists).IsFalse();
        await Assert.That(kpi.Exists).IsFalse();
    }

    [Test]
    public async Task Snapshot_metadata_collision_fails_closed_without_a_head_or_payloads()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var request = Request("context-a", "kpi-a");
        var target = DocumentPublicationContract.ComputeSnapshotId(request.Documents);
        await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, target))
            .SetAsync(new FirestoreDocumentPublicationSnapshot
            {
                Competition = scope.Competition,
                CommunityContext = scope.CommunityContext,
                PublicationSet = scope.PublicationSet,
                SnapshotId = target,
                CreatedAt = Timestamp.GetCurrentTimestamp(),
                MetadataJson = "{}",
                Documents = []
            });

        await Assert.That(() => CreateRepository().PublishAsync(Definition, request)).Throws<InvalidDataException>();
        var head = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope)).GetSnapshotAsync();
        var context = await fixture.Db.Collection("context-documents")
            .Document(PublicationPayloadId(scope, "fixture-context", 0)).GetSnapshotAsync();
        await Assert.That(head.Exists).IsFalse();
        await Assert.That(context.Exists).IsFalse();
    }

    [Test]
    public async Task Headed_payload_scope_version_and_hash_corruption_fail_closed_for_context_and_kpi()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var context = first.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Context);
        var kpi = first.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Kpi);

        await AssertPayloadFailureAsync("context-documents", scope, context, "competition", "wrong-competition", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "communityContext", "wrong-community", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "publicationSet", "wrong-set", repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "version", 42, repository);
        await AssertPayloadFailureAsync("context-documents", scope, context, "content", "wrong-content", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "competition", "wrong-competition", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "communityContext", "wrong-community", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "publicationSet", "wrong-set", repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "version", 42, repository);
        await AssertPayloadFailureAsync("kpi-documents", scope, kpi, "content", "wrong-content", repository);
    }

    [Test]
    public async Task Missing_and_wrong_scope_head_or_snapshot_envelopes_fail_closed()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var head = fixture.Db.Collection("document-publication-heads").Document(DocumentPublicationContract.ComputeHeadId(scope));
        var metadata = fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, first.Snapshot.SnapshotId));

        await head.UpdateAsync("communityContext", "wrong-community");
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await head.UpdateAsync("communityContext", Community);
        await metadata.UpdateAsync("publicationSet", "wrong-set");
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await metadata.UpdateAsync("publicationSet", Definition.PublicationSet);
        await metadata.DeleteAsync();
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Version_allocation_includes_gaps_legacy_unheaded_and_other_set_rows_for_both_kinds()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        await SeedVersionAsync("context-documents", "legacy-context", scope, "fixture-context", 2, string.Empty);
        await SeedVersionAsync("context-documents", "unheaded-context", scope, "fixture-context", 7, "other-set");
        await SeedVersionAsync("kpi-documents", "legacy-kpi", scope, "fixture-kpi", 3, string.Empty);
        await SeedVersionAsync("kpi-documents", "unheaded-kpi", scope, "fixture-kpi", 9, "other-set");

        var published = await CreateRepository().PublishAsync(Definition, Request("context-a", "kpi-a"));

        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Context).Version).IsEqualTo(8);
        await Assert.That(published.Snapshot.Documents.Single(entry => entry.Kind == DocumentPublicationKind.Kpi).Version).IsEqualTo(10);
    }

    [Test]
    public async Task Noop_metadata_and_kpi_description_do_not_rewrite_rows_and_new_rows_share_one_timestamp()
    {
        var repository = CreateRepository();
        var first = await repository.PublishAsync(Definition, Request("context-a", "kpi-a"));
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet);
        var before = await repository.GetLastKnownGoodAsync(Definition, Community);
        var unchanged = await repository.PublishAsync(Definition, new DocumentPublicationRequest(
            Community, first.Snapshot.SnapshotId,
            [new(DocumentPublicationKind.Context, "fixture-context", "context-a"), new(DocumentPublicationKind.Kpi, "fixture-kpi", "kpi-a", "Changed description")],
            "{\"changedMetadata\":true}"));
        var after = await repository.GetLastKnownGoodAsync(Definition, Community);
        var snapshotTimestamp = (await fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, first.Snapshot.SnapshotId)).GetSnapshotAsync())
            .GetValue<Timestamp>("createdAt").ToDateTimeOffset().ToUnixTimeMilliseconds();

        await Assert.That(unchanged.Disposition).IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(after!.Snapshot.MetadataJson).IsEqualTo(before!.Snapshot.MetadataJson);
        await Assert.That(after.Documents.Select(document => document.CreatedAt.ToUnixTimeMilliseconds()).Distinct()).IsEquivalentTo(new[] { snapshotTimestamp });
    }

    [Test]
    public async Task Scope_isolation_uses_distinct_deterministic_head_and_snapshot_paths()
    {
        var first = await CreateRepository().PublishAsync(Definition, Request("context-a", "kpi-a"));
        var secondCommunity = $"{Community}-two";
        var otherDefinition = new DocumentPublicationDefinition("fixture-other", Definition.RequiredDocuments);
        var otherCommunityResult = await CreateRepository().PublishAsync(
            Definition,
            new DocumentPublicationRequest(secondCommunity, null, Request("context-b", "kpi-b").Documents, "{\"fixture\":true}"));
        var otherSetResult = await CreateRepository().PublishAsync(
            otherDefinition,
            new DocumentPublicationRequest(Community, null, Request("context-c", "kpi-c").Documents, "{\"fixture\":true}"));
        var scopes = new[]
        {
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, Definition.PublicationSet),
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, secondCommunity, Definition.PublicationSet),
            new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, Community, otherDefinition.PublicationSet)
        };

        await Assert.That(scopes.Select(DocumentPublicationContract.ComputeHeadId).Distinct().Count()).IsEqualTo(3);
        await Assert.That(new[]
        {
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[0], first.Snapshot.SnapshotId),
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[1], otherCommunityResult.Snapshot.SnapshotId),
            DocumentPublicationContract.ComputeSnapshotMetadataId(scopes[2], otherSetResult.Snapshot.SnapshotId)
        }.Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task Generic_repositories_reject_reserved_bundesliga_writes_but_allow_other_competitions()
    {
        var context = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), CompetitionIds.Bundesliga2026_27);
        var kpi = new FirebaseKpiRepository(fixture.Db, new FakeLogger<FirebaseKpiRepository>(), CompetitionIds.Bundesliga2026_27);

        await Assert.That(() => context.SaveContextDocumentAsync("roster-b04", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.SaveContextDocumentAsync("team-rosters", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.SaveContextDocumentAsync("club-elo-b04.csv", "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.UpdateContextDocumentVersionAsync("team-rosters", 0, "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => context.UpdateContextDocumentVersionAsync("roster-b04", 0, "content", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => kpi.SaveKpiDocumentAsync("team-squad-summary", "content", "description", "community"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => kpi.SaveKpiDocumentAsync("club-elo-rankings", "content", "description", "community"))
            .Throws<InvalidOperationException>();

        var historical = new FirebaseContextRepository(fixture.Db, new FakeLogger<FirebaseContextRepository>(), "bundesliga-2025-26");
        await historical.SaveContextDocumentAsync("roster-b04", "historical", "community");
        await context.SaveContextDocumentAsync("nonreserved", "allowed", "community");
        var wm26 = new FirebaseKpiRepository(
            fixture.Db,
            new FakeLogger<FirebaseKpiRepository>(),
            CompetitionIds.FifaWorldCup2026);
        await wm26.SaveKpiDocumentAsync("club-elo-rankings", "allowed", "description", "community");
    }

    private FirebaseDocumentPublicationRepository CreateRepository() => new(
        fixture.Db,
        new FakeLogger<FirebaseDocumentPublicationRepository>(),
        CompetitionIds.Bundesliga2026_27);

    private DocumentPublicationRequest Request(string context, string kpi, string? expected = null) => new(
        Community,
        expected,
        [
            new DocumentPublicationPayload(DocumentPublicationKind.Context, "fixture-context", context),
            new DocumentPublicationPayload(DocumentPublicationKind.Kpi, "fixture-kpi", kpi, "Fixture KPI")
        ],
        "{\"fixture\":true}");

    private DocumentPublicationRequest CanonicalRequest(DocumentPublicationDefinition definition) => new(
        Community,
        null,
        definition.RequiredDocuments.Select(key => new DocumentPublicationPayload(
            key.Kind,
            key.Name,
            $"payload:{key.Kind}:{key.Name}",
            key.Kind == DocumentPublicationKind.Kpi ? $"Description for {key.Name}" : null)),
        "{\"fixture\":true}");

    private async Task CorruptStoredSnapshotIdAsync(DocumentPublicationScope scope, string metadataId, string storedSnapshotId)
    {
        var reference = fixture.Db.Collection("document-publication-snapshots")
            .Document(DocumentPublicationContract.ComputeSnapshotMetadataId(scope, metadataId));
        await reference.UpdateAsync("snapshotId", storedSnapshotId);
    }

    private async Task<string> HeadSnapshotIdAsync(DocumentPublicationScope scope)
    {
        var snapshot = await fixture.Db.Collection("document-publication-heads")
            .Document(DocumentPublicationContract.ComputeHeadId(scope))
            .GetSnapshotAsync();
        return snapshot.GetValue<string>("snapshotId");
    }

    private static string PublicationPayloadId(DocumentPublicationScope scope, string name, int version) =>
        $"{DocumentPublicationContract.ComputeHeadId(scope)}_{name}_{version}";

    private async Task AssertPayloadFailureAsync(
        string collection,
        DocumentPublicationScope scope,
        DocumentPublicationEntry entry,
        string field,
        object invalidValue,
        FirebaseDocumentPublicationRepository repository)
    {
        var reference = fixture.Db.Collection(collection).Document(PublicationPayloadId(scope, entry.Name, entry.Version));
        var original = (await reference.GetSnapshotAsync()).GetValue<object>(field);
        await reference.UpdateAsync(field, invalidValue);
        await Assert.That(() => repository.GetLastKnownGoodAsync(Definition, Community)).Throws<InvalidDataException>();
        await reference.UpdateAsync(field, original);
    }

    private async Task SeedVersionAsync(
        string collection,
        string id,
        DocumentPublicationScope scope,
        string name,
        int version,
        string publicationSet)
    {
        var values = new Dictionary<string, object>
        {
            ["competition"] = scope.Competition,
            ["communityContext"] = scope.CommunityContext,
            ["documentName"] = name,
            ["version"] = version,
            ["content"] = $"seed-{version}",
            ["createdAt"] = Timestamp.GetCurrentTimestamp(),
            ["publicationSet"] = publicationSet
        };
        if (collection == "kpi-documents")
        {
            values["description"] = "seed";
        }

        await fixture.Db.Collection(collection).Document(id).SetAsync(values);
    }

    private static async Task<(DocumentPublicationResult? Result, Exception? Exception)> CaptureAsync(Task<DocumentPublicationResult> task)
    {
        try
        {
            return (await task, null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }
}
