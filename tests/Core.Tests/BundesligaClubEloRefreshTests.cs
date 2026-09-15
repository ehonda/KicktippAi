using System.Text;
using System.Text.Json;
using EHonda.KicktippAi.Core;
using TestUtilities;

namespace Core.Tests;

public class BundesligaClubEloRefreshTests
{
    private static readonly DateTimeOffset Observed = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly BundesligaContextSourceCycleIdentity Cycle = BundesligaContextSourceCycleIdentity.Production(CompetitionIds.Bundesliga2026_27, 1, 100);
    private static readonly byte[] Raw = Encoding.UTF8.GetBytes("synthetic parser output");
    private static readonly BundesligaClubEloRefresh.Response Response = new(200, BundesligaClubEloRefresh.SourceUrl, 0, null, "text/html", "utf-8", [], Raw.Length);

    [Test]
    public async Task Mapping_is_the_exact_accepted_487_byte_artifact()
    {
        var expected = BundesligaClubEloRefresh.CanonicalMappingBytes();
        var actual = File.ReadAllBytes(Path.Combine(SolutionPathUtility.FindSolutionRoot(), BundesligaClubEloRefresh.MappingPath));
        await Assert.That(actual.SequenceEqual(expected)).IsTrue();
        await Assert.That(actual.Length).IsEqualTo(487);
        await Assert.That(BundesligaContextSourceHashing.Sha256(actual)).IsEqualTo("8799071a30dca0a921974ac387f18a8005863fdcbea85d3b74c9bda382ba29b7");
        await Assert.That(Encoding.UTF8.GetString(actual).IsNormalized()).IsTrue();
        await Assert.That(actual[^1]).IsNotEqualTo((byte)'\n');
    }

    [Test]
    [Arguments("2026-09-06", "Eligible")]
    [Arguments("2026-08-30", "Eligible")]
    [Arguments("2026-08-29", "StaleRejected")]
    [Arguments("2026-09-07", "DateRejected")]
    [Arguments("2026-02-30", "DateRejected")]
    [Arguments("2026-9-06", "DateRejected")]
    [Arguments(" 2026-09-06", "DateRejected")]
    [Arguments(null, "DateRejected")]
    public async Task Date_and_calendar_freshness_are_strict(string? date, string expected)
    {
        var result = Evaluate(date);
        await Assert.That(Evaluation(result)).IsEqualTo(expected);
        result.Validate();
        await Assert.That(result.PayloadBytes is not null).IsEqualTo(expected == "Eligible");
    }

    [Test]
    public async Task Mapping_and_coverage_precede_staleness_and_date_precedes_mapping()
    {
        var rows = Rows();
        rows[0] = rows[0] with { ProviderDisplayName = "Dortmund" };
        await Assert.That(Evaluation(Evaluate("2026-08-01", rows))).IsEqualTo("MappingRejected");
        await Assert.That(Evaluation(Evaluate("2026-08-01", Rows()[..^1]))).IsEqualTo("CoverageRejected");
        await Assert.That(Evaluation(Evaluate(null, rows, [0]))).IsEqualTo("DateRejected");
        await Assert.That(Evaluation(Evaluate("2026-09-06", Rows(), [0]))).IsEqualTo("MappingRejected");
    }

    [Test]
    public async Task Crossed_missing_and_duplicate_mapped_identity_reject_and_extra_german_clubs_are_ignored()
    {
        foreach (var replacement in new[] { Rows()[0] with { ProviderRoute = "/Bayern" }, Rows()[0] with { ProviderDisplayName = "Unknown" } })
        {
            var rows = Rows(); rows[0] = replacement;
            await Assert.That(Evaluation(Evaluate("2026-09-06", rows))).IsEqualTo("MappingRejected");
        }
        await Assert.That(Evaluation(Evaluate("2026-09-06", [.. Rows(), Rows()[0]]))).IsEqualTo("MappingRejected");
        var extra = Evaluate("2026-09-06", [.. Rows(), new("/Other", "Other", 500, 1400)]);
        await Assert.That(Evaluation(extra)).IsEqualTo("Eligible");
        using var descriptor = JsonDocument.Parse(extra.Observation.DescriptorJson);
        await Assert.That(descriptor.RootElement.GetProperty("sourceRows").GetArrayLength()).IsEqualTo(18);
    }

    [Test]
    public async Task Proper_subset_rows_are_canonical_and_empty_coverage_is_represented()
    {
        var empty = Evaluate("2026-09-06", []);
        await Assert.That(Evaluation(empty)).IsEqualTo("CoverageRejected");
        using var descriptor = JsonDocument.Parse(empty.Observation.DescriptorJson);
        await Assert.That(descriptor.RootElement.GetProperty("sourceRows").GetArrayLength()).IsEqualTo(0);
        var partial = Evaluate("2026-09-06", Rows().Reverse().Skip(1).ToArray());
        using var partialDescriptor = JsonDocument.Parse(partial.Observation.DescriptorJson);
        var slugs = partialDescriptor.RootElement.GetProperty("sourceRows").EnumerateArray().Select(row => row.GetProperty("teamSlug").GetString()!).ToArray();
        await Assert.That(slugs.SequenceEqual(slugs.Order(StringComparer.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Shared_not_newer_requires_all_eight_verified_selections_and_uses_the_minimum_date()
    {
        var retained = Retained(new DateOnly(2026, 9, 6));
        await Assert.That(Evaluation(Evaluate("2026-09-06", retained: retained))).IsEqualTo("NotNewer");
        retained[BundesligaContextSourceContract.ProductionConsumers[^1]] = BundesligaClubEloSeed.Default;
        var candidate = Evaluate("2026-09-06", retained: retained);
        await Assert.That(Evaluation(candidate)).IsEqualTo("Eligible");
        await Assert.That(candidate.PayloadBytes).IsNotNull();
        retained.Remove(BundesligaContextSourceContract.ProductionConsumers[^1]);
        await Assert.That(() => Evaluate("2026-09-06", retained: retained)).Throws<InvalidDataException>();
    }

    [Test]
    public async Task One_shared_candidate_advances_lagging_lane_but_preserves_newer_head_and_tied_elo()
    {
        var observation = Evaluate("2026-09-06").Observation;
        var selected = BundesligaClubEloRefresh.Select(observation, BundesligaClubEloSeed.Default);
        await Assert.That(selected.Disposition).IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkAccepted);
        await Assert.That(selected.Selected.Entries.Select(row => row.Elo).Distinct().Count()).IsEqualTo(1);
        await Assert.That(selected.Selected.CollectedAt).IsEqualTo(Observed);
        var retained = Snapshot(new DateOnly(2026, 9, 6));
        var unchanged = BundesligaClubEloRefresh.Select(observation, retained);
        await Assert.That(unchanged.Disposition).IsEqualTo(BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer);
        await Assert.That(ReferenceEquals(unchanged.Selected, retained)).IsTrue();
    }

    [Test]
    public async Task Consumption_rejects_a_not_newer_contradiction_and_preserves_legacy_fallback_provenance()
    {
        var notNewer = Evaluate("2026-09-06", retained: Retained(new DateOnly(2026, 9, 6))).Observation;
        await Assert.That(() => BundesligaClubEloRefresh.Select(notNewer, BundesligaClubEloSeed.Default)).Throws<InvalidDataException>();
        foreach (var date in new string?[] { null, "2026-08-29" })
        {
            var rejected = Evaluate(date).Observation;
            var selection = BundesligaClubEloRefresh.Select(rejected, BundesligaClubEloSeed.Default);
            var publication = BundesligaClubEloPublication.Build(selection);
            await Assert.That(publication.MetadataJson).Contains("club-elo-publication-v1");
            await Assert.That(publication.MetadataJson).DoesNotContain("sourceDescriptor");
            await Assert.That(rejected.Payload).IsNull();
        }
    }

    private static BundesligaContextSourceObservationResult Evaluate(string? date, BundesligaClubEloRefresh.Row[]? rows = null,
        byte[]? mapping = null, IReadOnlyDictionary<string, BundesligaClubEloSnapshot>? retained = null) =>
        BundesligaClubEloRefresh.Evaluate(Cycle, Observed, Response, Raw, date, rows ?? Rows(), mapping ?? BundesligaClubEloRefresh.CanonicalMappingBytes(), retained ?? Retained());
    private static string Evaluation(BundesligaContextSourceObservationResult result)
    {
        using var document = JsonDocument.Parse(result.Observation.DescriptorJson);
        return document.RootElement.GetProperty("evaluation").GetString()!;
    }
    private static BundesligaClubEloRefresh.Row[] Rows() => Encoding.UTF8.GetString(BundesligaClubEloRefresh.CanonicalMappingBytes())
        .Split("\r\n").Skip(1).Select((line, index) => { var fields = line.Split(','); return new BundesligaClubEloRefresh.Row(fields[1], fields[2], index + 1, 1500); }).ToArray();
    private static BundesligaClubEloSnapshot Snapshot(DateOnly date) => BundesligaClubEloSnapshot.Create(BundesligaClubEloSeed.Default.Entries, date, Observed,
        new Uri(BundesligaClubEloRefresh.SourceUrl), BundesligaClubEloSnapshotOrigin.LastKnownGood);
    private static Dictionary<string, BundesligaClubEloSnapshot> Retained(DateOnly? date = null) => BundesligaContextSourceContract.ProductionConsumers
        .ToDictionary(lane => lane, _ => date is null ? BundesligaClubEloSeed.Default : Snapshot(date.Value), StringComparer.Ordinal);
}
