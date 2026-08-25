using EHonda.KicktippAi.Core;
using Moq;
using NodaTime;
using Match = EHonda.KicktippAi.Core.Match;

namespace Core.Tests;

public sealed class HistoricalExperimentContextTests
{
    private const string Community = "pes-squad";
    private static readonly DateTimeOffset EvaluationTimestamp =
        new(2026, 4, 10, 8, 30, 0, TimeSpan.FromHours(2));
    private static readonly Match Match = new(
        "FC Bayern München",
        "Borussia Dortmund",
        Instant.FromDateTimeOffset(EvaluationTimestamp.AddHours(12)).InZone(DateTimeZone.Utc),
        29);

    [Test]
    public async Task Preparation_resolution_freezes_the_canonical_seven_document_legacy_id_contract()
    {
        var reader = CreateReader();

        var resolved = await new Bundesliga2025_26HistoricalExperimentContextResolver(reader.Object)
            .ResolveAtTimestampAsync(Match, Community, EvaluationTimestamp);

        var expectedNames = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            Match,
            Community).RequiredDocumentNames;
        await Assert.That(resolved.Documents.Select(document => document.Name).SequenceEqual(expectedNames, StringComparer.Ordinal)).IsTrue();
        await Assert.That(resolved.Manifest.Documents.Count).IsEqualTo(7);
        await Assert.That(resolved.Manifest.Documents.Select(document => document.Name).SequenceEqual(expectedNames, StringComparer.Ordinal)).IsTrue();
        await Assert.That(resolved.Manifest.Documents.All(document =>
            document.SourceDocumentId == $"{document.Name}_{Community}_{document.Version}")).IsTrue();
        await Assert.That(DocumentPublicationContract.IsLowercaseSha256(resolved.Manifest.ManifestSha256)).IsTrue();
        reader.Verify(repository => repository.GetContextDocumentAtOrBeforeAsync(
            It.IsAny<string>(), Community, EvaluationTimestamp, It.IsAny<CancellationToken>()), Times.Exactly(7));
    }

    [Test]
    public async Task Recorded_resolution_exact_reads_every_version_and_rejects_content_drift()
    {
        var reader = CreateReader();
        var resolver = new Bundesliga2025_26HistoricalExperimentContextResolver(reader.Object);
        var manifest = (await resolver.ResolveAtTimestampAsync(Match, Community, EvaluationTimestamp)).Manifest;
        var changed = manifest.Documents[0];
        reader.Setup(repository => repository.GetContextDocumentAsync(
                changed.Name, changed.Version, Community, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(changed.Name, "changed", changed.Version, changed.CreatedAt));

        await Assert.That(() => resolver.ResolveRecordedAsync(Match, manifest))
            .Throws<InvalidDataException>()
            .WithMessageContaining("drifted");
    }

    [Test]
    public async Task Manifest_validation_rejects_wrong_legacy_id_order_and_hash()
    {
        var manifest = (await new Bundesliga2025_26HistoricalExperimentContextResolver(CreateReader().Object)
            .ResolveAtTimestampAsync(Match, Community, EvaluationTimestamp)).Manifest;
        var wrongIdDocuments = manifest.Documents.ToArray();
        wrongIdDocuments[0] = wrongIdDocuments[0] with { SourceDocumentId = "wrong" };

        await Assert.That(() => ResolvedHistoricalExperimentContextManifest.Validate(
                manifest with { Documents = wrongIdDocuments }))
            .Throws<InvalidDataException>();
        await Assert.That(() => ResolvedHistoricalExperimentContextManifest.ValidateForMatch(
                manifest with { Documents = manifest.Documents.Reverse().ToArray() },
                Match,
                Community))
            .Throws<InvalidDataException>();
        await Assert.That(() => ResolvedHistoricalExperimentContextManifest.Validate(
                manifest with { ManifestSha256 = new string('a', 64) }))
            .Throws<InvalidDataException>();
        await Assert.That(() => ResolvedHistoricalExperimentContextManifest.Validate(
                manifest with { Documents = null! }))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Historical_catalog_preserves_exact_producer_era_fcs_and_fck_names()
    {
        var selection = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            "FC St. Pauli",
            "1. FC Köln",
            Community);

        await Assert.That(selection.RequiredDocumentNames).IsEquivalentTo(
        [
            "bundesliga-standings.csv",
            "community-rules-pes-squad.md",
            "recent-history-fcs.csv",
            "recent-history-fck.csv",
            "home-history-fcs.csv",
            "away-history-fck.csv",
            "head-to-head-fcs-vs-fck.csv"
        ]);
        await Assert.That(selection.RequiredDocumentNames.SequenceEqual(
        [
            "bundesliga-standings.csv",
            "community-rules-pes-squad.md",
            "recent-history-fcs.csv",
            "recent-history-fck.csv",
            "home-history-fcs.csv",
            "away-history-fck.csv",
            "head-to-head-fcs-vs-fck.csv"
        ], StringComparer.Ordinal)).IsTrue();
        await Assert.That(() => Bundesliga2025_26HistoricalExperimentDocumentCatalog.GetTeamAlias("FC St Pauli"))
            .Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("1. FC Heidenheim 1846", "fch")]
    [Arguments("1. FC Köln", "fck")]
    [Arguments("1. FC Union Berlin", "fcu")]
    [Arguments("1899 Hoffenheim", "tsg")]
    [Arguments("Bayer 04 Leverkusen", "b04")]
    [Arguments("Bor. Mönchengladbach", "bmg")]
    [Arguments("Borussia Dortmund", "bvb")]
    [Arguments("Eintracht Frankfurt", "sge")]
    [Arguments("FC Augsburg", "fca")]
    [Arguments("FC Bayern München", "fcb")]
    [Arguments("FC St. Pauli", "fcs")]
    [Arguments("FSV Mainz 05", "m05")]
    [Arguments("Hamburger SV", "hsv")]
    [Arguments("RB Leipzig", "rbl")]
    [Arguments("SC Freiburg", "scf")]
    [Arguments("VfB Stuttgart", "vfb")]
    [Arguments("VfL Wolfsburg", "wob")]
    [Arguments("Werder Bremen", "svw")]
    public async Task Historical_catalog_freezes_every_producer_era_team_alias(
        string teamName,
        string expectedAlias)
    {
        await Assert.That(Bundesliga2025_26HistoricalExperimentDocumentCatalog.GetTeamAlias(teamName))
            .IsEqualTo(expectedAlias);
    }

    [Test]
    public async Task Try_resolution_returns_null_only_for_a_genuinely_absent_exact_document()
    {
        var names = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(Match, Community).RequiredDocumentNames;
        var reader = CreateReader();
        reader.Setup(repository => repository.GetContextDocumentAtOrBeforeAsync(
                names[2], Community, EvaluationTimestamp, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextDocument?)null);

        var result = await new Bundesliga2025_26HistoricalExperimentContextResolver(reader.Object)
            .TryResolveAtTimestampAsync(Match, Community, EvaluationTimestamp);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Try_resolution_does_not_treat_malformed_identity_as_ineligible()
    {
        var names = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(Match, Community).RequiredDocumentNames;
        var reader = CreateReader();
        reader.Setup(repository => repository.GetContextDocumentAtOrBeforeAsync(
                names[2], Community, EvaluationTimestamp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextDocument(
                "recent-history-wrong.csv",
                "content",
                1,
                EvaluationTimestamp.AddMinutes(-1)));

        await Assert.That(() => new Bundesliga2025_26HistoricalExperimentContextResolver(reader.Object)
                .TryResolveAtTimestampAsync(Match, Community, EvaluationTimestamp))
            .Throws<InvalidDataException>()
            .WithMessageContaining("invalid identity or timestamp");
    }

    private static Mock<IHistoricalExperimentContextReader> CreateReader()
    {
        var reader = new Mock<IHistoricalExperimentContextReader>(MockBehavior.Strict);
        var names = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            Match,
            Community).RequiredDocumentNames;
        foreach (var (name, index) in names.Select((name, index) => (name, index)))
        {
            var document = new ContextDocument(
                name,
                $"content-{index}",
                index,
                EvaluationTimestamp.AddMinutes(-(index + 1)));
            reader.Setup(repository => repository.GetContextDocumentAtOrBeforeAsync(
                    name, Community, EvaluationTimestamp, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
            reader.Setup(repository => repository.GetContextDocumentAsync(
                    name, index, Community, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);
        }

        return reader;
    }
}
