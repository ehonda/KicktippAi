using Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FirebaseAdapter;

/// <summary>
/// Firebase Firestore implementation of the prediction repository.
/// </summary>
public class FirebasePredictionRepository : IPredictionRepository
{
    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebasePredictionRepository> _logger;
    private const string PredictionsCollection = "predictions";
    private const string MatchesCollection = "matches";
    private const string Competition = "bundesliga-2025-26";

    public FirebasePredictionRepository(FirestoreDb firestoreDb, ILogger<FirebasePredictionRepository> logger)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SavePredictionAsync(Match match, Prediction prediction, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = GenerateMatchId(match);
            var now = Timestamp.GetCurrentTimestamp();
            
            var firestorePrediction = new FirestoreMatchPrediction
            {
                Id = documentId,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                HomeGoals = prediction.HomeGoals,
                AwayGoals = prediction.AwayGoals,
                UpdatedAt = now,
                Competition = Competition
            };

            // Check if prediction already exists to set created timestamp
            var existingDoc = await _firestoreDb.Collection(PredictionsCollection)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            if (!existingDoc.Exists)
            {
                firestorePrediction.CreatedAt = now;
            }
            else
            {
                // Preserve original creation time
                var existing = existingDoc.ConvertTo<FirestoreMatchPrediction>();
                firestorePrediction.CreatedAt = existing.CreatedAt;
            }

            await _firestoreDb.Collection(PredictionsCollection)
                .Document(documentId)
                .SetAsync(firestorePrediction, cancellationToken: cancellationToken);

            _logger.LogInformation("Saved prediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday}", 
                match.HomeTeam, match.AwayTeam, match.Matchday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save prediction for match {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    public async Task<Prediction?> GetPredictionAsync(Match match, CancellationToken cancellationToken = default)
    {
        return await GetPredictionAsync(match.HomeTeam, match.AwayTeam, match.StartsAt, cancellationToken);
    }

    public async Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, CancellationToken cancellationToken = default)
    {
        try
        {
            var match = new Match(homeTeam, awayTeam, startsAt);
            var documentId = GenerateMatchId(match);

            var doc = await _firestoreDb.Collection(PredictionsCollection)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            if (!doc.Exists)
            {
                return null;
            }

            var firestorePrediction = doc.ConvertTo<FirestoreMatchPrediction>();
            return new Prediction(firestorePrediction.HomeGoals, firestorePrediction.AwayGoals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prediction for match {HomeTeam} vs {AwayTeam}", 
                homeTeam, awayTeam);
            throw;
        }
    }

    public async Task<IReadOnlyList<Match>> GetMatchDayAsync(int matchDay, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(MatchesCollection)
                .WhereEqualTo("competition", Competition)
                .WhereEqualTo("matchday", matchDay)
                .OrderBy("startsAt");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var matches = snapshot.Documents
                .Select(doc => doc.ConvertTo<FirestoreMatch>())
                .Select(fm => new Match(
                    fm.HomeTeam,
                    fm.AwayTeam,
                    ConvertFromTimestamp(fm.StartsAt),
                    fm.Matchday))
                .ToList();

            return matches.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matches for matchday {Matchday}", matchDay);
            throw;
        }
    }

    public async Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all matches for the matchday
            var matches = await GetMatchDayAsync(matchDay, cancellationToken);
            
            // Get predictions for all matches
            var matchPredictions = new List<MatchPrediction>();
            
            foreach (var match in matches)
            {
                var prediction = await GetPredictionAsync(match, cancellationToken);
                matchPredictions.Add(new MatchPrediction(match, prediction));
            }

            return matchPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matches with predictions for matchday {Matchday}", matchDay);
            throw;
        }
    }

    public async Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(PredictionsCollection)
                .WhereEqualTo("competition", Competition)
                .OrderBy("matchday");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var matchPredictions = snapshot.Documents
                .Select(doc => doc.ConvertTo<FirestoreMatchPrediction>())
                .Select(fp => new MatchPrediction(
                    new Match(fp.HomeTeam, fp.AwayTeam, ConvertFromTimestamp(fp.StartsAt), fp.Matchday),
                    new Prediction(fp.HomeGoals, fp.AwayGoals)))
                .ToList();

            return matchPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all predictions");
            throw;
        }
    }

    public async Task<bool> HasPredictionAsync(Match match, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = GenerateMatchId(match);
            var doc = await _firestoreDb.Collection(PredictionsCollection)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            return doc.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if prediction exists for match {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    /// <summary>
    /// Stores a match in the matches collection for matchday management.
    /// This is typically called when importing match schedules.
    /// </summary>
    public async Task StoreMatchAsync(Match match, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = GenerateMatchId(match);
            
            var firestoreMatch = new FirestoreMatch
            {
                Id = documentId,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                Competition = Competition
            };

            await _firestoreDb.Collection(MatchesCollection)
                .Document(documentId)
                .SetAsync(firestoreMatch, cancellationToken: cancellationToken);

            _logger.LogDebug("Stored match {HomeTeam} vs {AwayTeam} for matchday {Matchday}", 
                match.HomeTeam, match.AwayTeam, match.Matchday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store match {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    private static string GenerateMatchId(Match match)
    {
        // Create a deterministic ID from match details
        var homeTeamSafe = match.HomeTeam.Replace(" ", "_").Replace(".", "");
        var awayTeamSafe = match.AwayTeam.Replace(" ", "_").Replace(".", "");
        var ticksSafe = match.StartsAt.ToInstant().ToUnixTimeTicks();
        
        return $"{homeTeamSafe}_{awayTeamSafe}_{ticksSafe}_{match.Matchday}";
    }

    private static Timestamp ConvertToTimestamp(ZonedDateTime zonedDateTime)
    {
        var instant = zonedDateTime.ToInstant();
        return Timestamp.FromDateTimeOffset(instant.ToDateTimeOffset());
    }

    private static ZonedDateTime ConvertFromTimestamp(Timestamp timestamp)
    {
        var dateTimeOffset = timestamp.ToDateTimeOffset();
        var instant = Instant.FromDateTimeOffset(dateTimeOffset);
        return instant.InUtc();
    }
}
