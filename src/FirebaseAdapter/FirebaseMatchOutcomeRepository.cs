using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FirebaseAdapter;

public class FirebaseMatchOutcomeRepository : IMatchOutcomeRepository
{
    private const int ExpectedMatchesPerMatchday = 9;

    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebaseMatchOutcomeRepository> _logger;
    private readonly string _matchOutcomesCollection;
    private readonly string _competition;

    public FirebaseMatchOutcomeRepository(FirestoreDb firestoreDb, ILogger<FirebaseMatchOutcomeRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _matchOutcomesCollection = "match-outcomes";
        _competition = "bundesliga-2025-26";
    }

    public async Task<MatchOutcomeUpsertResult> UpsertMatchOutcomeAsync(
        CollectedMatchOutcome outcome,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var documentId = BuildDocumentId(outcome);
        var docRef = _firestoreDb.Collection(_matchOutcomesCollection).Document(documentId);
        var snapshot = await docRef.GetSnapshotAsync(cancellationToken);
        var now = Timestamp.GetCurrentTimestamp();

        if (!snapshot.Exists)
        {
            var firestoreOutcome = ToFirestoreMatchOutcome(outcome, communityContext, documentId, now, now);
            await docRef.SetAsync(firestoreOutcome, cancellationToken: cancellationToken);

            return new MatchOutcomeUpsertResult(
                MatchOutcomeWriteDisposition.Created,
                ConvertToPersistedMatchOutcome(firestoreOutcome));
        }

        var existing = snapshot.ConvertTo<FirestoreMatchOutcome>();
        if (!NeedsUpdate(existing, outcome))
        {
            return new MatchOutcomeUpsertResult(
                MatchOutcomeWriteDisposition.Unchanged,
                ConvertToPersistedMatchOutcome(existing));
        }

        var updated = ToFirestoreMatchOutcome(outcome, communityContext, documentId, existing.CreatedAt, now);
        await docRef.SetAsync(updated, cancellationToken: cancellationToken);

        return new MatchOutcomeUpsertResult(
            MatchOutcomeWriteDisposition.Updated,
            ConvertToPersistedMatchOutcome(updated));
    }

    public async Task<IReadOnlyList<int>> GetIncompleteMatchdaysAsync(
        string communityContext,
        int currentMatchday,
        CancellationToken cancellationToken = default)
    {
        var query = _firestoreDb.Collection(_matchOutcomesCollection)
            .WhereEqualTo("communityContext", communityContext)
            .WhereEqualTo("competition", _competition)
            .WhereLessThanOrEqualTo("matchday", currentMatchday);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var groupedOutcomes = snapshot.Documents
            .Select(doc => doc.ConvertTo<FirestoreMatchOutcome>())
            .GroupBy(outcome => outcome.Matchday)
            .ToDictionary(group => group.Key, group => group.ToList());

        var incompleteMatchdays = new List<int>();
        for (var matchday = 1; matchday <= currentMatchday; matchday++)
        {
            if (!groupedOutcomes.TryGetValue(matchday, out var outcomes))
            {
                incompleteMatchdays.Add(matchday);
                continue;
            }

            var isComplete = outcomes.Count >= ExpectedMatchesPerMatchday &&
                             outcomes.All(outcome => string.Equals(outcome.Availability, nameof(MatchOutcomeAvailability.Completed), StringComparison.Ordinal));

            if (!isComplete)
            {
                incompleteMatchdays.Add(matchday);
            }
        }

        return incompleteMatchdays.AsReadOnly();
    }

    public async Task<IReadOnlyList<PersistedMatchOutcome>> GetMatchdayOutcomesAsync(
        int matchday,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var query = _firestoreDb.Collection(_matchOutcomesCollection)
            .WhereEqualTo("communityContext", communityContext)
            .WhereEqualTo("competition", _competition)
            .WhereEqualTo("matchday", matchday);

        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return snapshot.Documents
            .Select(doc => doc.ConvertTo<FirestoreMatchOutcome>())
            .Select(ConvertToPersistedMatchOutcome)
            .OrderBy(outcome => outcome.HomeTeam)
            .ToList()
            .AsReadOnly();
    }

    private static bool NeedsUpdate(FirestoreMatchOutcome existing, CollectedMatchOutcome outcome)
    {
        return existing.HomeGoals != outcome.HomeGoals ||
               existing.AwayGoals != outcome.AwayGoals ||
               !string.Equals(existing.Availability, outcome.Availability.ToString(), StringComparison.Ordinal) ||
               existing.TippSpielId != outcome.TippSpielId ||
               existing.StartsAt.ToDateTimeOffset() != outcome.StartsAt.ToInstant().ToDateTimeOffset();
    }

    private FirestoreMatchOutcome ToFirestoreMatchOutcome(
        CollectedMatchOutcome outcome,
        string communityContext,
        string documentId,
        Timestamp createdAt,
        Timestamp updatedAt)
    {
        return new FirestoreMatchOutcome
        {
            Id = documentId,
            HomeTeam = outcome.HomeTeam,
            AwayTeam = outcome.AwayTeam,
            StartsAt = ConvertToTimestamp(outcome.StartsAt),
            Matchday = outcome.Matchday,
            HomeGoals = outcome.HomeGoals,
            AwayGoals = outcome.AwayGoals,
            Availability = outcome.Availability.ToString(),
            TippSpielId = outcome.TippSpielId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Competition = _competition,
            CommunityContext = communityContext
        };
    }

    private PersistedMatchOutcome ConvertToPersistedMatchOutcome(FirestoreMatchOutcome firestoreOutcome)
    {
        return new PersistedMatchOutcome(
            firestoreOutcome.CommunityContext,
            firestoreOutcome.Competition,
            firestoreOutcome.HomeTeam,
            firestoreOutcome.AwayTeam,
            ConvertFromTimestamp(firestoreOutcome.StartsAt),
            firestoreOutcome.Matchday,
            firestoreOutcome.HomeGoals,
            firestoreOutcome.AwayGoals,
            Enum.Parse<MatchOutcomeAvailability>(firestoreOutcome.Availability, ignoreCase: false),
            firestoreOutcome.TippSpielId,
            firestoreOutcome.CreatedAt.ToDateTimeOffset(),
            firestoreOutcome.UpdatedAt.ToDateTimeOffset());
    }

    private static string BuildDocumentId(CollectedMatchOutcome outcome)
    {
        return outcome.TippSpielId ?? throw new InvalidOperationException(
            $"Cannot persist match outcome for {outcome.HomeTeam} vs {outcome.AwayTeam} on matchday {outcome.Matchday} because tippspielId is missing.");
    }

    private static Timestamp ConvertToTimestamp(ZonedDateTime zonedDateTime)
    {
        var instant = zonedDateTime.ToInstant();
        return Timestamp.FromDateTimeOffset(instant.ToDateTimeOffset());
    }

    private static ZonedDateTime ConvertFromTimestamp(Timestamp timestamp)
    {
        var instant = Instant.FromDateTimeOffset(timestamp.ToDateTimeOffset());
        return instant.InUtc();
    }
}
