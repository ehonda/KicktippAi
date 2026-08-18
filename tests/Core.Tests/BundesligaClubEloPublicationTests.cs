using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaClubEloPublicationTests
{
    [Test]
    public async Task Renderer_uses_exact_csv_contract_and_deterministic_elo_tie_order()
    {
        var entries = BundesligaClubEloSeed.Default.Entries
            .Select(entry => entry.Team.TeamSlug is "fcb" or "b04"
                ? entry with { Elo = 2000, GlobalRank = entry.Team.TeamSlug == "fcb" ? 4 : 2 }
                : entry)
            .OrderBy(entry => entry.Team.TeamSlug, StringComparer.Ordinal)
            .ToArray();
        var snapshot = BundesligaClubEloSnapshot.Create(
            entries, new DateOnly(2026, 8, 14), new DateTimeOffset(2026, 8, 16, 10, 44, 16, TimeSpan.Zero),
            new Uri("https://clubelo.com/GER"), BundesligaClubEloSnapshotOrigin.LaunchSeed);
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            snapshot, BundesligaClubEloSelectionDisposition.NetworkDisabled, ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));

        var aggregate = build.Documents.Single(document => document.Kind == DocumentPublicationKind.Kpi).Content;
        var lines = aggregate.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines[0]).IsEqualTo(BundesligaClubEloPublication.CsvHeader);
        await Assert.That(lines.Length).IsEqualTo(19);
        await Assert.That(lines[1]).StartsWith("2,1,Leverkusen,2000,2026-08-14", StringComparison.Ordinal);
        await Assert.That(lines[2]).StartsWith("4,2,Bayern,2000,2026-08-14", StringComparison.Ordinal);
        await Assert.That(aggregate).EndsWith("\r\n", StringComparison.Ordinal);
        await Assert.That(aggregate.Replace("\r\n", string.Empty, StringComparison.Ordinal)).DoesNotContain("\r").And.DoesNotContain("\n");
        await Assert.That(build.Documents.Count).IsEqualTo(19);
        await Assert.That(build.Documents.Count(document => document.Kind == DocumentPublicationKind.Context)).IsEqualTo(18);
    }

    [Test]
    public async Task Exact_headed_documents_and_metadata_reconstruct_last_known_good()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var loaded = CreateLoaded(build);

        var reconstructed = BundesligaClubEloPublication.ReconstructLastKnownGood(loaded);

        await Assert.That(reconstructed.Origin).IsEqualTo(BundesligaClubEloSnapshotOrigin.LastKnownGood);
        await Assert.That(reconstructed.Entries).IsEquivalentTo(BundesligaClubEloSeed.Default.Entries);
        await Assert.That(reconstructed.RatedAt).IsEqualTo(BundesligaClubEloSeed.Default.RatedAt);
    }

    [Test]
    public async Task Reconstruction_rejects_lexically_noncanonical_headed_payloads()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var leadingZero = build.Documents.Select(document => document.Name == "club-elo-b04.csv"
            ? document with { Content = document.Content.Replace("16,", "016,", StringComparison.Ordinal) }
            : document).ToArray();
        var lf = build.Documents.Select(document => document.Name == "club-elo-b04.csv"
            ? document with { Content = document.Content.Replace("\r\n", "\n", StringComparison.Ordinal) }
            : document).ToArray();
        var reorderedAggregate = build.Documents.Select(document => document.Kind == DocumentPublicationKind.Kpi
            ? document with { Content = SwapAggregateRows(document.Content) }
            : document).ToArray();

        var leadingZeroFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, leadingZero)));
        var lfFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, lf)));
        var aggregateFailure = CaptureInvalid(() => BundesligaClubEloPublication.ReconstructLastKnownGood(CreateLoaded(build, reorderedAggregate)));

        await Assert.That(leadingZeroFailure.Message).Contains("exact canonical single-row CSV");
        await Assert.That(lfFailure.Message).Contains("strict CSV line endings");
        await Assert.That(aggregateFailure.Message).Contains("exact canonical aggregate CSV");
    }

    [Test]
    public async Task Metadata_parser_rejects_noncanonical_enum_diagnostics_and_semantic_contradictions()
    {
        var build = BundesligaClubEloPublication.Build(new BundesligaClubEloSelection(
            BundesligaClubEloSeed.Default, BundesligaClubEloSelectionDisposition.NetworkDisabled,
            ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]));
        var invalid = new[]
        {
            build.MetadataJson.Replace("\"LaunchSeed\"", "\"0\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"LaunchSeed\"", "\"launchseed\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("\"NetworkDisabled\"", "\"NetworkAccepted\"", StringComparison.Ordinal),
            build.MetadataJson.Replace("[\"UNATTENDED_NETWORK_USE_NOT_APPROVED\"]", "[\"B\",\"A\"]", StringComparison.Ordinal),
            build.MetadataJson.Replace("https://clubelo.com/GER", "http://clubelo.com/GER", StringComparison.Ordinal)
        };

        foreach (var metadata in invalid)
        {
            await Assert.That(() => BundesligaClubEloPublication.ParseMetadata(metadata)).Throws<InvalidDataException>();
        }
    }

    [Test]
    public async Task Build_rejects_selection_combinations_that_cannot_be_reconstructed()
    {
        var invalid = new[]
        {
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkAccepted, []),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkDisabled, []),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkDisabled, ["OTHER"]),
            new BundesligaClubEloSelection(BundesligaClubEloSeed.Default,
                BundesligaClubEloSelectionDisposition.NetworkCandidateRejected, ["B", "A"])
        };

        foreach (var selection in invalid)
        {
            await Assert.That(() => BundesligaClubEloPublication.Build(selection)).Throws<InvalidDataException>();
        }
    }

    private static LoadedDocumentPublication CreateLoaded(
        BundesligaClubEloPublicationBuild build,
        IReadOnlyList<DocumentPublicationPayload>? documents = null)
    {
        var scope = new DocumentPublicationScope(CompetitionIds.Bundesliga2026_27, "ehonda-dev-buli-2627", BundesligaDocumentPublication.ClubEloPublicationSet);
        var ordered = DocumentPublicationContract.ValidateAndOrder(documents ?? build.Documents);
        var snapshot = new DocumentPublicationSnapshot(
            scope.Competition, scope.CommunityContext, scope.PublicationSet,
            DocumentPublicationContract.ComputeSnapshotId(ordered), null, DateTimeOffset.UtcNow, build.MetadataJson,
            ordered.Select((document, version) => new DocumentPublicationEntry(
                document.Kind, document.Name, version, DocumentPublicationContract.ComputeContentSha256(document.Content))));
        return new LoadedDocumentPublication(snapshot, ordered.Select((document, version) => new PublishedDocument(
            scope.Competition, scope.CommunityContext, scope.PublicationSet, document.Kind, document.Name, version,
            document.Content, document.Description, DateTimeOffset.UtcNow)));
    }

    private static string SwapAggregateRows(string content)
    {
        var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        (lines[1], lines[2]) = (lines[2], lines[1]);
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static InvalidDataException CaptureInvalid(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected InvalidDataException.");
    }
}
