using System.Collections.Immutable;
using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class DocumentPublicationContractTests
{
    private const string SnapshotA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SnapshotB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SnapshotC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Test]
    public async Task Bundesliga_definitions_are_canonical_and_reserved_names_cannot_be_redefined()
    {
        await Assert.That(BundesligaDocumentPublication.Rosters.RequiredDocuments.Length).IsEqualTo(20);
        await Assert.That(BundesligaDocumentPublication.ClubElo.RequiredDocuments.Length).IsEqualTo(19);

        var redefined = new DocumentPublicationDefinition(
            BundesligaDocumentPublication.RosterPublicationSet,
            [new DocumentPublicationKey(DocumentPublicationKind.Context, "custom")]);
        var request = new DocumentPublicationRequest("pes-squad", null, [new DocumentPublicationPayload(
            DocumentPublicationKind.Context, "custom", "fixture\r\n")]);

        await Assert.That(() => DocumentPublicationContract.ValidateRequest(
                CompetitionIds.Bundesliga2026_27,
                redefined,
                request))
            .Throws<ArgumentException>();

        var genericDefinition = new DocumentPublicationDefinition(
            "fixture",
            [new DocumentPublicationKey(DocumentPublicationKind.Context, "custom")]);
        DocumentPublicationContract.ValidateRequest(CompetitionIds.Bundesliga2026_27, genericDefinition, request);
    }

    [Test]
    public async Task Alternate_set_definitions_cannot_contain_reserved_roster_or_elo_keys()
    {
        var alternateDefinitions = new[]
        {
            new DocumentPublicationDefinition("alternate-roster-team", [new DocumentPublicationKey(DocumentPublicationKind.Context, "roster-b04")]),
            new DocumentPublicationDefinition("alternate-roster-aggregate", [new DocumentPublicationKey(DocumentPublicationKind.Kpi, "team-squad-summary")]),
            new DocumentPublicationDefinition("alternate-elo-team", [new DocumentPublicationKey(DocumentPublicationKind.Context, "club-elo-b04.csv")]),
            new DocumentPublicationDefinition("alternate-elo-aggregate", [new DocumentPublicationKey(DocumentPublicationKind.Kpi, "club-elo-rankings")])
        };

        foreach (var definition in alternateDefinitions)
        {
            var key = definition.RequiredDocuments[0];
            var request = new DocumentPublicationRequest(
                "pes-squad",
                null,
                [new DocumentPublicationPayload(
                    key.Kind,
                    key.Name,
                    "fixture\r\n",
                    key.Kind == DocumentPublicationKind.Kpi ? "Fixture aggregate" : null)]);

            await Assert.That(() => DocumentPublicationContract.ValidateRequest(
                    CompetitionIds.Bundesliga2026_27,
                    definition,
                    request))
                .Throws<ArgumentException>();
        }
    }

    [Test]
    public async Task Content_snapshot_is_scope_independent_while_head_and_metadata_ids_are_scope_qualified()
    {
        var documents = CreateDocuments();
        var snapshotId = DocumentPublicationContract.ComputeSnapshotId(documents);
        var firstScope = new DocumentPublicationScope("bundesliga-2026-27", "pes-squad", "fixture");
        var secondScope = new DocumentPublicationScope("bundesliga-2026-27", "ehonda-ai-arena", "fixture");

        await Assert.That(DocumentPublicationContract.ComputeSnapshotId(documents.Reverse().ToArray())).IsEqualTo(snapshotId);
        await Assert.That(DocumentPublicationContract.ComputeHeadId(firstScope)).IsNotEqualTo(DocumentPublicationContract.ComputeHeadId(secondScope));
        await Assert.That(DocumentPublicationContract.ComputeSnapshotMetadataId(firstScope, snapshotId))
            .IsNotEqualTo(DocumentPublicationContract.ComputeSnapshotMetadataId(secondScope, snapshotId));
    }

    [Test]
    public async Task Snapshot_identity_ignores_metadata_only_document_description_changes()
    {
        var documents = CreateDocuments();
        var metadataOnlyChange = documents.Select(document => document.Kind == DocumentPublicationKind.Kpi
            ? document with { Description = "Reworded summary" }
            : document).ToArray();

        await Assert.That(DocumentPublicationContract.ComputeSnapshotId(metadataOnlyChange))
            .IsEqualTo(DocumentPublicationContract.ComputeSnapshotId(documents));
    }

    [Test]
    public async Task Request_and_returned_models_copy_mutable_input_collections()
    {
        var keys = new List<DocumentPublicationKey>
        {
            new(DocumentPublicationKind.Context, "fixture")
        };
        var definition = new DocumentPublicationDefinition("fixture", keys);
        keys[0] = new DocumentPublicationKey(DocumentPublicationKind.Context, "mutated");
        keys.Clear();

        var payloads = new List<DocumentPublicationPayload>
        {
            new(DocumentPublicationKind.Context, "fixture", "fixture\r\n")
        };
        var request = new DocumentPublicationRequest("pes-squad", null, payloads);
        payloads[0] = new DocumentPublicationPayload(DocumentPublicationKind.Context, "mutated", "mutated\r\n");
        payloads.Clear();

        await Assert.That(definition.RequiredDocuments.Length).IsEqualTo(1);
        await Assert.That(definition.RequiredDocuments[0].Name).IsEqualTo("fixture");
        await Assert.That(request.Documents.Length).IsEqualTo(1);
        await Assert.That(request.Documents[0].Content).IsEqualTo("fixture\r\n");

        var snapshot = CreateSnapshot(definition, request.Documents);
        var rows = CreateRows(snapshot, request.Documents).ToList();
        var loaded = new LoadedDocumentPublication(snapshot, rows);
        rows.Clear();

        await Assert.That(loaded.Documents.Length).IsEqualTo(1);
        await Assert.That(loaded.Documents[0].Content).IsEqualTo("fixture\r\n");
    }

    [Test]
    public async Task Loaded_validation_fails_closed_for_scope_set_shape_order_version_hash_and_snapshot_id()
    {
        var definition = FixtureDefinition;
        var documents = CreateDocuments();
        var snapshot = CreateSnapshot(definition, documents);
        var rows = CreateRows(snapshot, documents);

        DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, rows);

        var wrongScopeRows = rows.Select((row, index) => index == 0
            ? row with { CommunityContext = "other" }
            : row).ToImmutableArray();
        var wrongSetRows = rows.Select((row, index) => index == 0
            ? row with { PublicationSet = "other" }
            : row).ToImmutableArray();
        var wrongVersionRows = rows.Select((row, index) => index == 0
            ? row with { Version = 99 }
            : row).ToImmutableArray();
        var wrongOrderRows = rows.Reverse().ToImmutableArray();
        var missingRows = rows.Take(1).ToImmutableArray();
        var extraRows = rows.Append(rows[0]).ToImmutableArray();
        var wrongScopeSnapshot = CopySnapshot(snapshot, communityContext: "other");
        var wrongSetSnapshot = CopySnapshot(snapshot, publicationSet: "other");
        var wrongHashSnapshot = CopySnapshot(snapshot, documents: snapshot.Documents.SetItem(0,
            snapshot.Documents[0] with { ContentSha256 = SnapshotC }));
        var wrongIdSnapshot = CopySnapshot(snapshot, snapshotId: SnapshotC);

        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, wrongScopeRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, wrongSetRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, wrongVersionRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, wrongOrderRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, missingRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, snapshot, extraRows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, wrongScopeSnapshot, rows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, wrongSetSnapshot, rows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, wrongHashSnapshot, rows)).Throws<InvalidDataException>();
        await Assert.That(() => DocumentPublicationContract.ValidateLoaded(CompetitionIds.Bundesliga2026_27, "pes-squad", definition, wrongIdSnapshot, rows)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Cas_transition_checks_expected_head_before_noop_or_reactivation()
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, "pes-squad", "fixture");

        await Assert.That(DocumentPublicationContract.DecideTransition(scope, null, null, SnapshotA, false))
            .IsEqualTo(DocumentPublicationDisposition.Published);
        await Assert.That(() => DocumentPublicationContract.DecideTransition(scope, null, SnapshotA, SnapshotB, false))
            .Throws<DocumentPublicationConcurrencyException>();
        await Assert.That(() => DocumentPublicationContract.DecideTransition(scope, SnapshotA, SnapshotB, SnapshotC, false))
            .Throws<DocumentPublicationConcurrencyException>();
        await Assert.That(DocumentPublicationContract.DecideTransition(scope, SnapshotA, SnapshotA, SnapshotA, true))
            .IsEqualTo(DocumentPublicationDisposition.Unchanged);
        await Assert.That(DocumentPublicationContract.DecideTransition(scope, SnapshotA, SnapshotA, SnapshotB, true))
            .IsEqualTo(DocumentPublicationDisposition.Reactivated);
        await Assert.That(DocumentPublicationContract.DecideTransition(scope, SnapshotA, SnapshotA, SnapshotB, false))
            .IsEqualTo(DocumentPublicationDisposition.Published);
    }

    [Test]
    public async Task Bundesliga_roster_and_elo_names_are_reserved_for_generic_mutation()
    {
        await Assert.That(() => BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
                CompetitionIds.Bundesliga2026_27, DocumentPublicationKind.Context, "roster-b04"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
                CompetitionIds.Bundesliga2026_27, DocumentPublicationKind.Context, "club-elo-b04.csv"))
            .Throws<InvalidOperationException>();
        await Assert.That(() => BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
                CompetitionIds.Bundesliga2026_27, DocumentPublicationKind.Kpi, "club-elo-rankings"))
            .Throws<InvalidOperationException>();

        BundesligaDocumentPublication.ThrowIfReservedForGenericMutation(
            CompetitionIds.Bundesliga2026_27, DocumentPublicationKind.Context, "recent-results");
    }

    private static DocumentPublicationDefinition FixtureDefinition { get; } = new(
        "fixture",
        [
            new DocumentPublicationKey(DocumentPublicationKind.Context, "fixture-context"),
            new DocumentPublicationKey(DocumentPublicationKind.Kpi, "fixture-kpi")
        ]);

    private static DocumentPublicationPayload[] CreateDocuments() =>
    [
        new(DocumentPublicationKind.Kpi, "fixture-kpi", "summary\r\n", "Fixture summary"),
        new(DocumentPublicationKind.Context, "fixture-context", "context\r\n")
    ];

    private static DocumentPublicationSnapshot CreateSnapshot(
        DocumentPublicationDefinition definition,
        IEnumerable<DocumentPublicationPayload> documents)
    {
        var ordered = DocumentPublicationContract.ValidateAndOrder(documents);
        return new DocumentPublicationSnapshot(
            CompetitionIds.Bundesliga2026_27,
            "pes-squad",
            definition.PublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(ordered),
            previousSnapshotId: null,
            createdAt: new DateTimeOffset(2026, 8, 18, 8, 0, 0, TimeSpan.Zero),
            metadataJson: "{}",
            documents: ordered.Select((document, index) => new DocumentPublicationEntry(
                document.Kind,
                document.Name,
                index,
                DocumentPublicationContract.ComputeContentSha256(document.Content))));
    }

    private static ImmutableArray<PublishedDocument> CreateRows(
        DocumentPublicationSnapshot snapshot,
        IEnumerable<DocumentPublicationPayload> documents)
    {
        return DocumentPublicationContract.ValidateAndOrder(documents)
            .Select((document, index) => new PublishedDocument(
                snapshot.Competition,
                snapshot.CommunityContext,
                snapshot.PublicationSet,
                document.Kind,
                document.Name,
                index,
                document.Content,
                document.Description,
                snapshot.CreatedAt))
            .ToImmutableArray();
    }

    private static DocumentPublicationSnapshot CopySnapshot(
        DocumentPublicationSnapshot snapshot,
        string? snapshotId = null,
        string? communityContext = null,
        string? publicationSet = null,
        IEnumerable<DocumentPublicationEntry>? documents = null)
    {
        return new DocumentPublicationSnapshot(
            snapshot.Competition,
            communityContext ?? snapshot.CommunityContext,
            publicationSet ?? snapshot.PublicationSet,
            snapshotId ?? snapshot.SnapshotId,
            snapshot.PreviousSnapshotId,
            snapshot.CreatedAt,
            snapshot.MetadataJson,
            documents ?? snapshot.Documents);
    }
}
