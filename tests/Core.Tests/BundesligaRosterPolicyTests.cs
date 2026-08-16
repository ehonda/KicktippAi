using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class BundesligaRosterPolicyTests
{
    private static readonly DateOnly EvaluationDate = new(2026, 8, 16);
    private static readonly DateOnly ReferenceDate = new(2026, 8, 1);

    [Test]
    public async Task Duckdb_candidate_passes_all_inclusive_boundary_gates()
    {
        var reference = CreateReference(20);
        var candidate = CreateCandidate(20, 20) with
        {
            SnapshotAsOf = EvaluationDate.AddDays(-BundesligaRosterPolicy.MaximumDuckDbSnapshotAgeDays)
        };

        var evaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            candidate,
            EvaluationDate,
            ReferenceDate,
            reference);

        await Assert.That(evaluation.Result).IsEqualTo(BundesligaRosterDuckDbGateResult.Pass);
        await Assert.That(evaluation.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task Duckdb_candidate_rejects_stale_wrong_season_identity_and_coach_data()
    {
        var candidate = CreateCandidate(20, 20) with
        {
            SnapshotAsOf = EvaluationDate.AddDays(-15),
            SourceRevision = " ",
            CompetitionId = "GB1",
            LastSeason = 2025,
            MatchingClubRowCount = 2,
            HeadCoach = null,
            DeclaredSquadSize = 19,
            Players = CreatePlayers(20, 20)
                .Select((player, index) => index == 0
                    ? player with { CurrentClubId = 999, LastSeason = 2025 }
                    : player)
                .ToArray()
        };

        var evaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            candidate,
            EvaluationDate,
            ReferenceDate,
            CreateReference(20));

        await Assert.That(evaluation.Result).IsEqualTo(BundesligaRosterDuckDbGateResult.Rejected);
        await Assert.That(evaluation.Diagnostics).Contains("STALE_SNAPSHOT");
        await Assert.That(evaluation.Diagnostics).Contains("MISSING_SOURCE_REVISION");
        await Assert.That(evaluation.Diagnostics).Contains("WRONG_COMPETITION");
        await Assert.That(evaluation.Diagnostics).Contains("WRONG_SEASON");
        await Assert.That(evaluation.Diagnostics).Contains("CLUB_RECORD_COUNT");
        await Assert.That(evaluation.Diagnostics).Contains("MISSING_COACH");
        await Assert.That(evaluation.Diagnostics).Contains("DECLARED_SQUAD_SIZE_MISMATCH");
        await Assert.That(evaluation.Diagnostics).Contains("PLAYER_CLUB_MISMATCH");
        await Assert.That(evaluation.Diagnostics).Contains("PLAYER_SEASON_MISMATCH");
    }

    [Test]
    public async Task Reference_count_change_uses_25_percent_inclusive_bounds()
    {
        var reference = CreateReference(32);
        var atMinimum = CreateCandidate(24, 24) with { DeclaredSquadSize = 24 };
        var belowMinimum = CreateCandidate(23, 23) with { DeclaredSquadSize = 23 };

        var passing = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            atMinimum,
            EvaluationDate,
            ReferenceDate,
            reference);
        var rejected = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            belowMinimum,
            EvaluationDate,
            ReferenceDate,
            reference);

        await Assert.That(passing.Result).IsEqualTo(BundesligaRosterDuckDbGateResult.Pass);
        await Assert.That(rejected.Diagnostics).Contains("COUNT_CHANGE_EXCEEDS_25_PERCENT");
    }

    [Test]
    public async Task Absolute_player_count_accepts_40_and_rejects_19_or_41()
    {
        var reference = CreateReference(32);
        var maximum = CreateCandidate(40, 32) with { DeclaredSquadSize = 40 };
        var belowMinimum = CreateCandidate(19, 19) with { DeclaredSquadSize = 19 };
        var aboveMaximum = CreateCandidate(41, 32) with { DeclaredSquadSize = 41 };

        var maximumEvaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(maximum, EvaluationDate, ReferenceDate, reference);
        var belowEvaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(belowMinimum, EvaluationDate, ReferenceDate, reference);
        var aboveEvaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(aboveMaximum, EvaluationDate, ReferenceDate, reference);

        await Assert.That(maximumEvaluation.Result).IsEqualTo(BundesligaRosterDuckDbGateResult.Pass);
        await Assert.That(belowEvaluation.Diagnostics).Contains("PLAYER_COUNT_OUT_OF_RANGE");
        await Assert.That(aboveEvaluation.Diagnostics).Contains("PLAYER_COUNT_OUT_OF_RANGE");
    }

    [Test]
    public async Task Candidate_rejects_manifest_mismatch_and_duplicate_member_identity()
    {
        var players = CreatePlayers(20, 20).ToArray();
        players[1] = players[1] with
        {
            TransfermarktPlayerId = players[0].TransfermarktPlayerId,
            Name = players[0].Name
        };
        var candidate = CreateCandidate(20, 20) with
        {
            ManifestClubId = 999,
            Players = players
        };

        var evaluation = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            candidate,
            EvaluationDate,
            ReferenceDate,
            CreateReference(20));

        await Assert.That(evaluation.Diagnostics).Contains("MANIFEST_CLUB_ID_MISMATCH");
        await Assert.That(evaluation.Diagnostics).Contains("DUPLICATE_PLAYER_ID");
        await Assert.That(evaluation.Diagnostics).Contains("DUPLICATE_PLAYER_NAME");
    }

    [Test]
    public async Task Reference_identity_overlap_accepts_exactly_50_percent_and_rejects_less()
    {
        var reference = CreateReference(20);
        var exactlyHalf = CreateCandidate(20, 10);
        var belowHalf = CreateCandidate(20, 9);

        var passing = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            exactlyHalf,
            EvaluationDate,
            ReferenceDate,
            reference);
        var rejected = BundesligaRosterPolicy.EvaluateDuckDbCandidate(
            belowHalf,
            EvaluationDate,
            ReferenceDate,
            reference);

        await Assert.That(passing.Result).IsEqualTo(BundesligaRosterDuckDbGateResult.Pass);
        await Assert.That(rejected.Diagnostics).Contains("IDENTITY_OVERLAP_BELOW_50_PERCENT");
    }

    [Test]
    public async Task Selection_prefers_valid_duckdb_otherwise_newest_reference_with_lkg_tie_break()
    {
        var seed = Candidate(BundesligaRosterMembershipSource.FallbackSeed, new DateOnly(2026, 8, 10));
        var olderLkg = Candidate(BundesligaRosterMembershipSource.LastKnownGood, new DateOnly(2026, 8, 9), "old");
        var tiedLkg = olderLkg with { MembershipAsOf = seed.MembershipAsOf, SnapshotId = "tie" };
        var newerLkg = olderLkg with { MembershipAsOf = new DateOnly(2026, 8, 11), SnapshotId = "new" };
        var duckDb = Candidate(BundesligaRosterMembershipSource.DuckDb, new DateOnly(2026, 8, 16));
        var pass = new BundesligaRosterDuckDbEvaluation(BundesligaRosterDuckDbGateResult.Pass, []);
        var rejected = new BundesligaRosterDuckDbEvaluation(
            BundesligaRosterDuckDbGateResult.Rejected,
            ["WRONG_SEASON"]);

        var selectedDuck = BundesligaRosterPolicy.SelectMembership(seed, newerLkg, duckDb, pass);
        var selectedSeed = BundesligaRosterPolicy.SelectMembership(seed, olderLkg, duckDb, rejected);
        var selectedTie = BundesligaRosterPolicy.SelectMembership(seed, tiedLkg, duckDb, rejected);
        var selectedNewLkg = BundesligaRosterPolicy.SelectMembership(seed, newerLkg, duckDb, rejected);

        await Assert.That(selectedDuck.Selected.Source).IsEqualTo(BundesligaRosterMembershipSource.DuckDb);
        await Assert.That(selectedSeed.Selected.Source).IsEqualTo(BundesligaRosterMembershipSource.FallbackSeed);
        await Assert.That(selectedTie.Selected.Source).IsEqualTo(BundesligaRosterMembershipSource.LastKnownGood);
        await Assert.That(selectedNewLkg.Selected.Source).IsEqualTo(BundesligaRosterMembershipSource.LastKnownGood);
        await Assert.That(selectedNewLkg.SelectionReason).IsEqualTo("DUCKDB_REJECTED_USE_LAST_KNOWN_GOOD");
    }

    [Test]
    public async Task Freshness_and_enrichment_thresholds_have_exact_boundaries()
    {
        await Assert.That(BundesligaRosterPolicy.GetFreshnessDiagnostics(EvaluationDate.AddDays(-14), EvaluationDate)).IsEmpty();
        await Assert.That(BundesligaRosterPolicy.GetFreshnessDiagnostics(EvaluationDate.AddDays(-15), EvaluationDate))
            .IsEquivalentTo(["STALE_MEMBERSHIP_GT_14_DAYS"]);
        await Assert.That(BundesligaRosterPolicy.GetFreshnessDiagnostics(EvaluationDate.AddDays(-30), EvaluationDate))
            .IsEquivalentTo(["STALE_MEMBERSHIP_GT_14_DAYS"]);
        await Assert.That(BundesligaRosterPolicy.GetFreshnessDiagnostics(EvaluationDate.AddDays(-31), EvaluationDate))
            .IsEquivalentTo(["STALE_MEMBERSHIP_GT_30_DAYS"]);
        await Assert.That(BundesligaRosterPolicy.IsFreshForProductionActivation(EvaluationDate.AddDays(-30), EvaluationDate)).IsTrue();
        await Assert.That(BundesligaRosterPolicy.IsFreshForProductionActivation(EvaluationDate.AddDays(-31), EvaluationDate)).IsFalse();

        await Assert.That(BundesligaRosterPolicy.GetEnrichmentCoverageDiagnostics(20, 16, 16, 10)).IsEmpty();
        await Assert.That(BundesligaRosterPolicy.GetEnrichmentCoverageDiagnostics(20, 15, 15, 9)).IsEquivalentTo(
        [
            "AGE_COVERAGE_BELOW_80_PERCENT",
            "MARKET_VALUE_COVERAGE_BELOW_50_PERCENT",
            "POSITION_COVERAGE_BELOW_80_PERCENT"
        ]);
    }

    private static BundesligaRosterMembershipCandidate Candidate(
        BundesligaRosterMembershipSource source,
        DateOnly date,
        string? snapshotId = null)
    {
        return new BundesligaRosterMembershipCandidate("b04", source, date, true, snapshotId);
    }

    private static IReadOnlyList<BundesligaRosterIdentity> CreateReference(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new BundesligaRosterIdentity(index, $"Player {index:00}"))
            .ToArray();
    }

    private static BundesligaRosterDuckDbCandidate CreateCandidate(int count, int matchingReferenceCount)
    {
        return new BundesligaRosterDuckDbCandidate(
            "b04",
            15,
            1,
            "L1",
            2026,
            EvaluationDate,
            "transfermarkt-datasets@abc123",
            count,
            "Coach Alpha",
            CreatePlayers(count, matchingReferenceCount));
    }

    private static IReadOnlyList<BundesligaRosterDuckDbPlayer> CreatePlayers(int count, int matchingReferenceCount)
    {
        return Enumerable.Range(1, count)
            .Select(index => index <= matchingReferenceCount
                ? new BundesligaRosterDuckDbPlayer(index, $"Player {index:00}", 15, 2026)
                : new BundesligaRosterDuckDbPlayer(10_000 + index, $"New Player {index:00}", 15, 2026))
            .ToArray();
    }
}
