using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FirebaseAdapter;

/// <summary>
/// Read-only adapter for canonical Bundesliga 2025/26 completed fixtures whose Firestore
/// document identities predate competition-prefixed IDs. It is intentionally unavailable
/// through <see cref="IMatchOutcomeRepository"/> so historical experiments cannot collect or
/// mutate live outcomes and do not require a current-season matchday completion policy.
/// </summary>
public sealed class Bundesliga2025_26HistoricalExperimentFixtureReader : IHistoricalExperimentFixtureReader
{
    private const string MatchOutcomesCollection = "match-outcomes";
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<Bundesliga2025_26HistoricalExperimentFixtureReader> _logger;

    public Bundesliga2025_26HistoricalExperimentFixtureReader(
        FirestoreDb firestoreDb,
        ILogger<Bundesliga2025_26HistoricalExperimentFixtureReader> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PersistedMatchOutcome>> GetCompletedMatchdayFixturesAsync(
        int matchday,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        if (matchday < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(matchday), matchday, "Historical matchday must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);

        try
        {
            var query = _firestoreDb.Collection(MatchOutcomesCollection)
                .WhereEqualTo("communityContext", communityContext)
                .WhereEqualTo("competition", CompetitionIds.Bundesliga2025_26)
                .WhereEqualTo("matchday", matchday);
            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents
                .Select(document => ConvertAndValidate(document, communityContext, matchday))
                .Where(outcome => outcome.HasOutcome)
                .OrderBy(outcome => outcome.HomeTeam, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to read Bundesliga 2025/26 historical experiment fixtures for matchday {Matchday} and {CommunityContext}",
                matchday,
                communityContext);
            throw;
        }
    }

    private static PersistedMatchOutcome ConvertAndValidate(
        DocumentSnapshot document,
        string communityContext,
        int matchday)
    {
        var value = document.ConvertTo<FirestoreMatchOutcome>();
        if (string.IsNullOrWhiteSpace(value.TippSpielId)
            || !string.Equals(document.Id, value.TippSpielId, StringComparison.Ordinal)
            || !string.Equals(value.Competition, CompetitionIds.Bundesliga2025_26, StringComparison.Ordinal)
            || !string.Equals(value.CommunityContext, communityContext, StringComparison.Ordinal)
            || value.Matchday != matchday
            || string.IsNullOrWhiteSpace(value.HomeTeam)
            || string.IsNullOrWhiteSpace(value.AwayTeam)
            || !Enum.TryParse<MatchOutcomeAvailability>(value.Availability, ignoreCase: false, out var availability))
        {
            throw new InvalidDataException(
                "Bundesliga 2025/26 historical experiment fixture scope or exact legacy identity is corrupt.");
        }

        if (availability == MatchOutcomeAvailability.Completed
            && (value.HomeGoals is null || value.AwayGoals is null))
        {
            throw new InvalidDataException(
                $"Completed Bundesliga 2025/26 historical experiment fixture '{value.TippSpielId}' is missing its score.");
        }

        return new PersistedMatchOutcome(
            value.CommunityContext,
            value.Competition,
            value.HomeTeam,
            value.AwayTeam,
            Instant.FromDateTimeOffset(value.StartsAt.ToDateTimeOffset()).InUtc(),
            value.Matchday,
            value.HomeGoals,
            value.AwayGoals,
            availability,
            value.TippSpielId,
            value.CreatedAt.ToDateTimeOffset(),
            value.UpdatedAt.ToDateTimeOffset());
    }
}
