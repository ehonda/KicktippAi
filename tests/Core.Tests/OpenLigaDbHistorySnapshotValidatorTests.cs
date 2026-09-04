using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class OpenLigaDbHistorySnapshotValidatorTests
{
    [Test]
    [Arguments("openligadb-bl2-2025.json", OpenLigaDbHistorySnapshotKind.SecondBundesliga, 306, 306)]
    [Arguments("openligadb-rel-2025.json", OpenLigaDbHistorySnapshotKind.Relegation, 2, 2)]
    [Arguments("openligadb-dfb-2025.json", OpenLigaDbHistorySnapshotKind.DfbPokal, 63, 63)]
    [Arguments("openligadb-dfb-2026.json", OpenLigaDbHistorySnapshotKind.DfbPokal2026LiveCompletion, 32, 32)]
    public async Task Frozen_snapshot_has_expected_hash_completion_results_dates_and_identities(
        string fileName,
        OpenLigaDbHistorySnapshotKind kind,
        int expectedMatchCount,
        int expectedCompletedMatchCount)
    {
        var path = Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "sources", fileName);
        var content = await File.ReadAllBytesAsync(path);

        var validation = OpenLigaDbHistorySnapshotValidator.Validate(content, kind, fileName);

        await Assert.That(validation.MatchCount).IsEqualTo(expectedMatchCount);
        await Assert.That(validation.CompletedMatchCount).IsEqualTo(expectedCompletedMatchCount);
        await Assert.That(validation.MatchIds.Count).IsEqualTo(expectedMatchCount);
    }

    [Test]
    public async Task Snapshot_bytes_are_immutable()
    {
        var path = Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "sources", "openligadb-rel-2025.json");
        var content = await File.ReadAllBytesAsync(path);
        content[^1] = content[^1] == (byte)'\n' ? (byte)' ' : (byte)'\n';

        await Assert.That(() => OpenLigaDbHistorySnapshotValidator.Validate(
            content,
            OpenLigaDbHistorySnapshotKind.Relegation,
            "mutated.json")).Throws<InvalidDataException>();
    }

    [Test]
    public async Task Dfb2026_snapshot_fixture_fact_mutation_is_rejected()
    {
        var path = Path.Combine(SolutionPathUtility.FindSolutionRoot(), "data", "bundesliga-2026-27", "history", "sources", "openligadb-dfb-2026.json");
        var content = await File.ReadAllTextAsync(path);
        var mutated = content.Replace("SSV Jeddeloh II", "SSV Jeddeloh 2", StringComparison.Ordinal);

        await Assert.That(mutated).IsNotEqualTo(content);
        await Assert.That(() => OpenLigaDbHistorySnapshotValidator.Validate(
            System.Text.Encoding.UTF8.GetBytes(mutated),
            OpenLigaDbHistorySnapshotKind.DfbPokal2026LiveCompletion,
            "mutated-dfb-2026.json")).Throws<InvalidDataException>();
    }
}
