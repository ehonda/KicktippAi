using EHonda.KicktippAi.Core;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Testing;
using TestUtilities;

namespace FirebaseAdapter.Tests.FirebaseContextRepositoryTests;

public sealed class Bundesliga2025_26HistoricalExperimentContextReaderTests(FirestoreFixture fixture)
    : FirebaseContextRepositoryTests_Base(fixture)
{
    private const string Community = "pes-squad";
    private const string Name = "bundesliga-standings.csv";

    [Test]
    public async Task Timestamp_read_selects_latest_eligible_legacy_identity()
    {
        var cutoff = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedAsync(Name, 0, "old", cutoff.AddHours(-2));
        await SeedAsync(Name, 1, "selected", cutoff.AddHours(-1));
        await SeedAsync(Name, 2, "future", cutoff.AddHours(1));

        var result = await CreateReader().GetContextDocumentAtOrBeforeAsync(Name, Community, cutoff);

        await Assert.That(result).IsNotNull()
            .And.Member(document => document!.Version, version => version.IsEqualTo(1))
            .And.Member(document => document!.Content, content => content.IsEqualTo("selected"));
    }

    [Test]
    public async Task Timestamp_read_skips_a_newer_publication_scoped_row()
    {
        var cutoff = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedAsync(Name, 1, "selected", cutoff.AddHours(-2));
        await SeedAsync(Name, 2, "publication", cutoff.AddHours(-1), publicationSet: "historical-publication");

        var result = await CreateReader().GetContextDocumentAtOrBeforeAsync(Name, Community, cutoff);

        await Assert.That(result).IsNotNull()
            .And.Member(document => document!.Version, version => version.IsEqualTo(1))
            .And.Member(document => document!.Content, content => content.IsEqualTo("selected"));
    }

    [Test]
    public async Task Exact_read_uses_legacy_id_and_rejects_corrupt_scope()
    {
        await SeedAsync(Name, 3, "content", DateTimeOffset.UtcNow, competition: CompetitionIds.Bundesliga2026_27);

        await Assert.That(() => CreateReader().GetContextDocumentAsync(Name, 3, Community))
            .Throws<InvalidDataException>()
            .WithMessageContaining("legacy identity");
    }

    [Test]
    public async Task Exact_read_rejects_a_publication_scoped_row()
    {
        await SeedAsync(Name, 4, "content", DateTimeOffset.UtcNow, publicationSet: "rosters");

        await Assert.That(() => CreateReader().GetContextDocumentAsync(Name, 4, Community))
            .Throws<InvalidDataException>()
            .WithMessageContaining("legacy identity");
    }

    [Test]
    public async Task Exact_reads_support_the_producer_era_fcs_and_fck_document_route()
    {
        var names = Bundesliga2025_26HistoricalExperimentDocumentCatalog.ForMatch(
            "FC St. Pauli",
            "1. FC Köln",
            Community).RequiredDocumentNames;
        var reader = CreateReader();
        var createdAt = new DateTimeOffset(2026, 4, 13, 0, 54, 0, TimeSpan.Zero);

        foreach (var (name, index) in names.Select((name, index) => (name, index)))
        {
            var version = 700 + index;
            await SeedAsync(name, version, $"content-{index}", createdAt.AddSeconds(index));

            var result = await reader.GetContextDocumentAsync(name, version, Community);

            await Assert.That(result).IsNotNull()
                .And.Member(document => document!.DocumentName, actual => actual.IsEqualTo(name))
                .And.Member(document => document!.Version, actual => actual.IsEqualTo(version));
        }

        await Assert.That(names).Contains("recent-history-fcs.csv")
            .And.Contains("recent-history-fck.csv")
            .And.Contains("head-to-head-fcs-vs-fck.csv");
    }

    [Test]
    public async Task Reader_contract_exposes_no_write_operations()
    {
        var publicMethods = typeof(IHistoricalExperimentContextReader).GetMethods();

        await Assert.That(publicMethods.All(method => method.Name.StartsWith("Get", StringComparison.Ordinal))).IsTrue();
    }

    private Bundesliga2025_26HistoricalExperimentContextReader CreateReader() =>
        new(Fixture.Db, new FakeLogger<Bundesliga2025_26HistoricalExperimentContextReader>());

    private async Task SeedAsync(
        string name,
        int version,
        string content,
        DateTimeOffset createdAt,
        string competition = CompetitionIds.Bundesliga2025_26,
        string publicationSet = "")
    {
        var id = ResolvedHistoricalExperimentContextManifest.BuildLegacyDocumentId(name, Community, version);
        await Fixture.Db.Collection("context-documents").Document(id).SetAsync(new Dictionary<string, object>
        {
            ["id"] = id,
            ["documentName"] = name,
            ["content"] = content,
            ["version"] = version,
            ["createdAt"] = Timestamp.FromDateTime(createdAt.UtcDateTime),
            ["competition"] = competition,
            ["communityContext"] = Community,
            ["publicationSet"] = publicationSet
        });
    }
}
