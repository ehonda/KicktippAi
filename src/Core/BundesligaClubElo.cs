namespace EHonda.KicktippAi.Core;

public enum BundesligaClubEloSnapshotOrigin
{
    LaunchSeed,
    LastKnownGood,
    NetworkCandidate
}

public enum BundesligaClubEloSelectionDisposition
{
    NetworkAccepted,
    NetworkDisabled,
    NetworkCandidateRejected,
    NetworkCandidateStale,
    NetworkCandidateNotNewer
}

public sealed record BundesligaClubEloEntry(
    BundesligaTeamManifestEntry Team,
    int GlobalRank,
    int Elo);

public sealed class BundesligaClubEloSnapshot
{
    private BundesligaClubEloSnapshot(
        IReadOnlyList<BundesligaClubEloEntry> entries,
        DateOnly ratedAt,
        DateTimeOffset collectedAt,
        Uri sourceUrl,
        BundesligaClubEloSnapshotOrigin origin)
    {
        Entries = entries;
        RatedAt = ratedAt;
        CollectedAt = collectedAt;
        SourceUrl = sourceUrl;
        Origin = origin;
    }

    public IReadOnlyList<BundesligaClubEloEntry> Entries { get; }

    public DateOnly RatedAt { get; }

    public DateTimeOffset CollectedAt { get; }

    public Uri SourceUrl { get; }

    public BundesligaClubEloSnapshotOrigin Origin { get; }

    public static BundesligaClubEloSnapshot Create(
        IReadOnlyList<BundesligaClubEloEntry> entries,
        DateOnly ratedAt,
        DateTimeOffset collectedAt,
        Uri sourceUrl,
        BundesligaClubEloSnapshotOrigin origin,
        IReadOnlyList<BundesligaTeamManifestEntry>? expectedTeams = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sourceUrl);
        expectedTeams ??= BundesligaTeamManifest.Default.Entries;

        if (expectedTeams.Count == 0)
        {
            throw new ArgumentException("At least one expected team is required.", nameof(expectedTeams));
        }

        if (!sourceUrl.IsAbsoluteUri
            || !string.Equals(sourceUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A Bundesliga Club Elo snapshot requires an absolute HTTPS source URL.");
        }

        if (collectedAt.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Bundesliga Club Elo Collected_At must be a UTC timestamp.");
        }

        if (ratedAt > DateOnly.FromDateTime(collectedAt.UtcDateTime))
        {
            throw new InvalidDataException("Bundesliga Club Elo Rated_At must not be later than Collected_At.");
        }

        var expectedBySlug = expectedTeams.ToDictionary(team => team.TeamSlug, StringComparer.Ordinal);
        var expectedSlugs = expectedBySlug.Keys.Order(StringComparer.Ordinal).ToArray();
        var actualSlugs = entries.Select(entry => entry.Team.TeamSlug).ToArray();
        if (!actualSlugs.SequenceEqual(expectedSlugs, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Bundesliga Club Elo entries must contain exactly [{string.Join(',', expectedSlugs)}] in Team_Slug order; " +
                $"found [{string.Join(',', actualSlugs)}].");
        }

        foreach (var entry in entries)
        {
            if (!expectedBySlug.TryGetValue(entry.Team.TeamSlug, out var expectedTeam)
                || !Equals(entry.Team, expectedTeam))
            {
                throw new InvalidDataException(
                    $"Bundesliga Club Elo identity for Team_Slug '{entry.Team.TeamSlug}' does not match the manifest.");
            }

            if (entry.GlobalRank <= 0)
            {
                throw new InvalidDataException(
                    $"Bundesliga Club Elo Global_Rank for '{entry.Team.TeamSlug}' must be positive.");
            }

            if (entry.Elo <= 0)
            {
                throw new InvalidDataException(
                    $"Bundesliga Club Elo ELO for '{entry.Team.TeamSlug}' must be positive.");
            }
        }

        var duplicateRank = entries
            .GroupBy(entry => entry.GlobalRank)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRank is not null)
        {
            throw new InvalidDataException(
                $"Bundesliga Club Elo Global_Rank {duplicateRank.Key} must be unique.");
        }

        return new BundesligaClubEloSnapshot(
            Array.AsReadOnly(entries.ToArray()),
            ratedAt,
            collectedAt,
            sourceUrl,
            origin);
    }
}

public sealed record BundesligaClubEloSourceResult(
    BundesligaClubEloSnapshot? Snapshot,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsComplete => Snapshot is not null && Diagnostics.Count == 0;

    public static BundesligaClubEloSourceResult Complete(BundesligaClubEloSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new BundesligaClubEloSourceResult(snapshot, []);
    }

    public static BundesligaClubEloSourceResult Rejected(params string[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Length == 0 || diagnostics.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one nonblank diagnostic is required.", nameof(diagnostics));
        }

        return new BundesligaClubEloSourceResult(
            null,
            Array.AsReadOnly(diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }
}

public interface IBundesligaClubEloSource
{
    Task<BundesligaClubEloSourceResult> GetLatestAsync(CancellationToken cancellationToken = default);
}

public sealed record BundesligaClubEloSelection(
    BundesligaClubEloSnapshot Selected,
    BundesligaClubEloSelectionDisposition Disposition,
    IReadOnlyList<string> Diagnostics);

public static class BundesligaClubEloPolicy
{
    public const int MaximumNetworkCandidateAgeDays = 7;

    public static BundesligaClubEloSelection Select(
        BundesligaClubEloSnapshot launchSeed,
        BundesligaClubEloSnapshot? lastKnownGood,
        BundesligaClubEloSourceResult? networkCandidate,
        bool unattendedNetworkUseAllowed)
    {
        ArgumentNullException.ThrowIfNull(launchSeed);
        EnsureOrigin(launchSeed, BundesligaClubEloSnapshotOrigin.LaunchSeed, nameof(launchSeed));
        if (lastKnownGood is not null)
        {
            EnsureOrigin(lastKnownGood, BundesligaClubEloSnapshotOrigin.LastKnownGood, nameof(lastKnownGood));
        }

        var retained = IsFresher(lastKnownGood, launchSeed) ? lastKnownGood! : launchSeed;
        if (!unattendedNetworkUseAllowed)
        {
            return new BundesligaClubEloSelection(
                retained,
                BundesligaClubEloSelectionDisposition.NetworkDisabled,
                ["UNATTENDED_NETWORK_USE_NOT_APPROVED"]);
        }

        if (networkCandidate is null || !networkCandidate.IsComplete)
        {
            var diagnostics = networkCandidate?.Diagnostics.Count > 0
                ? networkCandidate.Diagnostics
                : ["NETWORK_CANDIDATE_UNAVAILABLE"];
            return new BundesligaClubEloSelection(
                retained,
                BundesligaClubEloSelectionDisposition.NetworkCandidateRejected,
                diagnostics);
        }

        var candidate = networkCandidate.Snapshot!;
        EnsureOrigin(candidate, BundesligaClubEloSnapshotOrigin.NetworkCandidate, nameof(networkCandidate));
        var candidateAgeDays = DateOnly.FromDateTime(candidate.CollectedAt.UtcDateTime).DayNumber
                               - candidate.RatedAt.DayNumber;
        if (candidateAgeDays > MaximumNetworkCandidateAgeDays)
        {
            return new BundesligaClubEloSelection(
                retained,
                BundesligaClubEloSelectionDisposition.NetworkCandidateStale,
                [$"NETWORK_CANDIDATE_STALE:AGE_DAYS={candidateAgeDays}:MAX_DAYS={MaximumNetworkCandidateAgeDays}"]);
        }

        if (candidate.RatedAt <= retained.RatedAt)
        {
            return new BundesligaClubEloSelection(
                retained,
                BundesligaClubEloSelectionDisposition.NetworkCandidateNotNewer,
                [$"NETWORK_RATED_AT_NOT_NEWER:{candidate.RatedAt:yyyy-MM-dd}"]);
        }

        return new BundesligaClubEloSelection(
            candidate,
            BundesligaClubEloSelectionDisposition.NetworkAccepted,
            []);
    }

    private static bool IsFresher(
        BundesligaClubEloSnapshot? candidate,
        BundesligaClubEloSnapshot baseline)
    {
        return candidate is not null
               && (candidate.RatedAt > baseline.RatedAt
                   || (candidate.RatedAt == baseline.RatedAt
                       && candidate.CollectedAt > baseline.CollectedAt));
    }

    private static void EnsureOrigin(
        BundesligaClubEloSnapshot snapshot,
        BundesligaClubEloSnapshotOrigin expected,
        string parameterName)
    {
        if (snapshot.Origin != expected)
        {
            throw new ArgumentException(
                $"Bundesliga Club Elo {parameterName} must have origin {expected}, not {snapshot.Origin}.",
                parameterName);
        }
    }
}
