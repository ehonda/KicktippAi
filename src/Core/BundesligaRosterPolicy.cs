namespace EHonda.KicktippAi.Core;

public static class BundesligaRosterPolicy
{
    public const int MinimumPlayerCount = 20;
    public const int MaximumPlayerCount = 40;
    public const int MaximumDuckDbSnapshotAgeDays = 14;
    public const int ProductionActivationMaximumMembershipAgeDays = 30;
    public const int MinimumReferenceOverlapPercent = 50;
    public const int MaximumReferenceCountChangePercent = 25;
    public const int AgeCoverageWarningPercent = 80;
    public const int PositionCoverageWarningPercent = 80;
    public const int MarketValueCoverageWarningPercent = 50;

    public static BundesligaRosterDuckDbEvaluation EvaluateDuckDbCandidate(
        BundesligaRosterDuckDbCandidate? candidate,
        DateOnly evaluationDate,
        DateOnly referenceMembershipAsOf,
        IReadOnlyList<BundesligaRosterIdentity> referencePlayers)
    {
        ArgumentNullException.ThrowIfNull(referencePlayers);

        if (candidate is null)
        {
            return new BundesligaRosterDuckDbEvaluation(
                BundesligaRosterDuckDbGateResult.NotAvailable,
                ["DUCKDB_NOT_AVAILABLE"]);
        }

        var diagnostics = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(candidate.SourceRevision))
        {
            diagnostics.Add("MISSING_SOURCE_REVISION");
        }

        var snapshotAge = evaluationDate.DayNumber - candidate.SnapshotAsOf.DayNumber;
        if (snapshotAge < 0)
        {
            diagnostics.Add("FUTURE_SNAPSHOT");
        }
        else if (snapshotAge > MaximumDuckDbSnapshotAgeDays)
        {
            diagnostics.Add("STALE_SNAPSHOT");
        }

        if (candidate.SnapshotAsOf < referenceMembershipAsOf)
        {
            diagnostics.Add("SNAPSHOT_OLDER_THAN_REFERENCE");
        }

        BundesligaTeamManifestEntry? manifestTeam = null;
        try
        {
            manifestTeam = BundesligaTeamManifest.Default.GetByTeamSlug(candidate.TeamSlug);
        }
        catch (KeyNotFoundException)
        {
            diagnostics.Add("UNKNOWN_MANIFEST_TEAM");
        }

        if (candidate.ManifestClubId is null or <= 0 || manifestTeam?.TransfermarktClubId is null)
        {
            diagnostics.Add("MANIFEST_CLUB_ID_MISSING");
        }
        else if (candidate.ManifestClubId != manifestTeam.TransfermarktClubId)
        {
            diagnostics.Add("MANIFEST_CLUB_ID_MISMATCH");
        }

        if (candidate.MatchingClubRowCount != 1)
        {
            diagnostics.Add("CLUB_RECORD_COUNT");
        }

        if (!string.Equals(candidate.CompetitionId, "L1", StringComparison.Ordinal))
        {
            diagnostics.Add("WRONG_COMPETITION");
        }

        if (candidate.LastSeason != 2026)
        {
            diagnostics.Add("WRONG_SEASON");
        }

        if (string.IsNullOrWhiteSpace(candidate.HeadCoach))
        {
            diagnostics.Add("MISSING_COACH");
        }

        var players = candidate.Players ?? [];
        if (players.Count is < MinimumPlayerCount or > MaximumPlayerCount)
        {
            diagnostics.Add("PLAYER_COUNT_OUT_OF_RANGE");
        }

        if (candidate.DeclaredSquadSize is null or <= 0
            || candidate.DeclaredSquadSize != players.Count)
        {
            diagnostics.Add("DECLARED_SQUAD_SIZE_MISMATCH");
        }

        if (players.Any(player => player.TransfermarktPlayerId <= 0))
        {
            diagnostics.Add("INVALID_PLAYER_ID");
        }

        if (players.Any(player => string.IsNullOrWhiteSpace(player.Name)))
        {
            diagnostics.Add("MISSING_PLAYER_NAME");
        }

        if (players.GroupBy(player => player.TransfermarktPlayerId).Any(group => group.Count() > 1))
        {
            diagnostics.Add("DUPLICATE_PLAYER_ID");
        }

        if (players
            .Where(player => !string.IsNullOrWhiteSpace(player.Name))
            .GroupBy(player => BundesligaRosterSeed.NormalizeName(player.Name), StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            diagnostics.Add("DUPLICATE_PLAYER_NAME");
        }

        if (!string.IsNullOrWhiteSpace(candidate.HeadCoach)
            && players.Any(player => !string.IsNullOrWhiteSpace(player.Name)
                && string.Equals(BundesligaRosterSeed.NormalizeName(player.Name), BundesligaRosterSeed.NormalizeName(candidate.HeadCoach), StringComparison.Ordinal)))
        {
            diagnostics.Add("COACH_PLAYER_NAME_COLLISION");
        }

        if (candidate.ManifestClubId is not null
            && players.Any(player => player.CurrentClubId != candidate.ManifestClubId.Value))
        {
            diagnostics.Add("PLAYER_CLUB_MISMATCH");
        }

        if (players.Any(player => player.LastSeason != 2026))
        {
            diagnostics.Add("PLAYER_SEASON_MISMATCH");
        }

        if (referencePlayers.Count > 0)
        {
            var minimumCount = Math.Max(
                MinimumPlayerCount,
                DivideRoundingUp(referencePlayers.Count * (100 - MaximumReferenceCountChangePercent), 100));
            var maximumCount = Math.Min(
                MaximumPlayerCount,
                referencePlayers.Count * (100 + MaximumReferenceCountChangePercent) / 100);

            if (players.Count < minimumCount || players.Count > maximumCount)
            {
                diagnostics.Add("COUNT_CHANGE_EXCEEDS_25_PERCENT");
            }

            var playerIds = players
                .Where(player => player.TransfermarktPlayerId > 0)
                .Select(player => player.TransfermarktPlayerId)
                .ToHashSet();
            var playerNames = players
                .Where(player => !string.IsNullOrWhiteSpace(player.Name))
                .Select(player => BundesligaRosterSeed.NormalizeName(player.Name))
                .ToHashSet(StringComparer.Ordinal);
            var overlap = referencePlayers.Count(reference =>
                reference.TransfermarktPlayerId is > 0
                    ? playerIds.Contains(reference.TransfermarktPlayerId.Value)
                    : playerNames.Contains(BundesligaRosterSeed.NormalizeName(reference.Name)));

            if (overlap * 100 < referencePlayers.Count * MinimumReferenceOverlapPercent)
            {
                diagnostics.Add("IDENTITY_OVERLAP_BELOW_50_PERCENT");
            }
        }

        var orderedDiagnostics = diagnostics.Order(StringComparer.Ordinal).ToArray();
        return new BundesligaRosterDuckDbEvaluation(
            orderedDiagnostics.Length == 0
                ? BundesligaRosterDuckDbGateResult.Pass
                : BundesligaRosterDuckDbGateResult.Rejected,
            orderedDiagnostics);
    }

    public static BundesligaRosterSelection SelectMembership(
        BundesligaRosterMembershipCandidate? fallbackSeed,
        BundesligaRosterMembershipCandidate? lastKnownGood,
        BundesligaRosterMembershipCandidate? duckDb,
        BundesligaRosterDuckDbEvaluation duckDbEvaluation)
    {
        ArgumentNullException.ThrowIfNull(duckDbEvaluation);

        var teamSlug = fallbackSeed?.TeamSlug ?? lastKnownGood?.TeamSlug ?? duckDb?.TeamSlug
            ?? throw new InvalidOperationException("At least one membership candidate is required.");
        EnsureCandidateSource(fallbackSeed, BundesligaRosterMembershipSource.FallbackSeed, teamSlug);
        EnsureCandidateSource(lastKnownGood, BundesligaRosterMembershipSource.LastKnownGood, teamSlug);
        EnsureCandidateSource(duckDb, BundesligaRosterMembershipSource.DuckDb, teamSlug);

        if (duckDbEvaluation.Passed)
        {
            if (duckDb is not { StructurallyValid: true })
            {
                throw new InvalidOperationException("A passing DuckDB evaluation requires a structurally valid DuckDB candidate.");
            }

            return new BundesligaRosterSelection(duckDb, duckDbEvaluation, "DUCKDB_GATES_PASSED");
        }

        var trustedReference = new[] { fallbackSeed, lastKnownGood }
            .Where(candidate => candidate is { StructurallyValid: true })
            .Cast<BundesligaRosterMembershipCandidate>()
            .OrderByDescending(candidate => candidate.MembershipAsOf)
            .ThenBy(candidate => candidate.Source == BundesligaRosterMembershipSource.LastKnownGood ? 0 : 1)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Team '{teamSlug}' has no valid DuckDB, fallback-seed, or last-known-good membership candidate.");

        var gateReason = duckDbEvaluation.Result is
            BundesligaRosterDuckDbGateResult.NotAvailable or BundesligaRosterDuckDbGateResult.NotEvaluated
            ? "DUCKDB_NOT_AVAILABLE"
            : "DUCKDB_REJECTED";
        var sourceReason = trustedReference.Source == BundesligaRosterMembershipSource.LastKnownGood
            ? "USE_LAST_KNOWN_GOOD"
            : "USE_FALLBACK_SEED";
        return new BundesligaRosterSelection(
            trustedReference,
            duckDbEvaluation,
            $"{gateReason}_{sourceReason}");
    }

    public static IReadOnlyList<string> GetFreshnessDiagnostics(DateOnly membershipAsOf, DateOnly evaluationDate)
    {
        var age = evaluationDate.DayNumber - membershipAsOf.DayNumber;
        if (age < 0)
        {
            return ["FUTURE_MEMBERSHIP_AS_OF"];
        }

        if (age > ProductionActivationMaximumMembershipAgeDays)
        {
            return ["STALE_MEMBERSHIP_GT_30_DAYS"];
        }

        return age > MaximumDuckDbSnapshotAgeDays
            ? ["STALE_MEMBERSHIP_GT_14_DAYS"]
            : [];
    }

    public static bool IsFreshForProductionActivation(DateOnly membershipAsOf, DateOnly evaluationDate)
    {
        var age = evaluationDate.DayNumber - membershipAsOf.DayNumber;
        return age is >= 0 and <= ProductionActivationMaximumMembershipAgeDays;
    }

    public static IReadOnlyList<string> GetEnrichmentCoverageDiagnostics(
        int playerCount,
        int knownAgeCount,
        int knownPositionCount,
        int valuedPlayerCount)
    {
        if (playerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        ValidateCoverageCount(knownAgeCount, playerCount, nameof(knownAgeCount));
        ValidateCoverageCount(knownPositionCount, playerCount, nameof(knownPositionCount));
        ValidateCoverageCount(valuedPlayerCount, playerCount, nameof(valuedPlayerCount));

        var diagnostics = new List<string>();
        if (knownAgeCount * 100 < playerCount * AgeCoverageWarningPercent)
        {
            diagnostics.Add("AGE_COVERAGE_BELOW_80_PERCENT");
        }

        if (knownPositionCount * 100 < playerCount * PositionCoverageWarningPercent)
        {
            diagnostics.Add("POSITION_COVERAGE_BELOW_80_PERCENT");
        }

        if (valuedPlayerCount * 100 < playerCount * MarketValueCoverageWarningPercent)
        {
            diagnostics.Add("MARKET_VALUE_COVERAGE_BELOW_50_PERCENT");
        }

        return diagnostics.Order(StringComparer.Ordinal).ToArray();
    }

    private static int DivideRoundingUp(int numerator, int denominator)
    {
        return (numerator + denominator - 1) / denominator;
    }

    private static void EnsureCandidateSource(
        BundesligaRosterMembershipCandidate? candidate,
        BundesligaRosterMembershipSource expectedSource,
        string expectedTeamSlug)
    {
        if (candidate is null)
        {
            return;
        }

        if (candidate.Source != expectedSource || !string.Equals(candidate.TeamSlug, expectedTeamSlug, StringComparison.Ordinal))
        {
            throw new ArgumentException("Membership candidate source or team slug does not match its selector slot.");
        }
    }

    private static void ValidateCoverageCount(int count, int playerCount, string parameterName)
    {
        if (count < 0 || count > playerCount)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
