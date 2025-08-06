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
    private readonly string _predictionsCollection;
    private readonly string _matchesCollection;
    private readonly string _bonusPredictionsCollection;
    private readonly string _competition;
    private readonly string _community;

    public FirebasePredictionRepository(FirestoreDb firestoreDb, ILogger<FirebasePredictionRepository> logger, string community)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (string.IsNullOrWhiteSpace(community))
            throw new ArgumentException("Community cannot be null or empty", nameof(community));
            
        _community = community;
        
        // Make collection names community-specific
        _predictionsCollection = $"predictions-{community}";
        _matchesCollection = $"matches-{community}";
        _bonusPredictionsCollection = $"bonusPredictions-{community}";
        _competition = $"bundesliga-2025-26-{community}";
        
        _logger.LogInformation("Firebase repository initialized for community: {Community}", community);
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
                Competition = _competition
            };

            // Check if prediction already exists to set created timestamp
            var existingDoc = await _firestoreDb.Collection(_predictionsCollection)
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

            await _firestoreDb.Collection(_predictionsCollection)
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
            // We need to find the document by querying, since we don't know the matchday
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(startsAt))
                .WhereEqualTo("competition", _competition);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            if (snapshot.Documents.Count == 0)
            {
                return null;
            }

            var firestorePrediction = snapshot.Documents.First().ConvertTo<FirestoreMatchPrediction>();
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
            var query = _firestoreDb.Collection(_matchesCollection)
                .WhereEqualTo("competition", _competition)
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
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
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
            var doc = await _firestoreDb.Collection(_predictionsCollection)
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

    public async Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, CancellationToken cancellationToken = default)
    {
        try
        {
            var documentId = bonusPrediction.QuestionId;
            var now = Timestamp.GetCurrentTimestamp();
            
            // Extract selected option texts for observability
            var optionTextsLookup = bonusQuestion.Options.ToDictionary(o => o.Id, o => o.Text);
            var selectedOptionTexts = bonusPrediction.SelectedOptionIds
                .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
                .ToArray();
            
            var firestoreBonusPrediction = new FirestoreBonusPrediction
            {
                Id = documentId,
                QuestionId = bonusPrediction.QuestionId,
                QuestionText = bonusQuestion.Text,
                SelectedOptionIds = bonusPrediction.SelectedOptionIds.ToArray(),
                SelectedOptionTexts = selectedOptionTexts,
                UpdatedAt = now,
                Competition = _competition
            };

            // Check if bonus prediction already exists to set created timestamp
            var existingDoc = await _firestoreDb.Collection(_bonusPredictionsCollection)
                .Document(documentId)
                .GetSnapshotAsync(cancellationToken);

            if (existingDoc.Exists)
            {
                var existing = existingDoc.ConvertTo<FirestoreBonusPrediction>();
                firestoreBonusPrediction.CreatedAt = existing.CreatedAt;
                _logger.LogDebug("Updating existing bonus prediction for question {QuestionId}: {QuestionText}", 
                    bonusPrediction.QuestionId, bonusQuestion.Text);
            }
            else
            {
                firestoreBonusPrediction.CreatedAt = now;
                _logger.LogDebug("Creating new bonus prediction for question {QuestionId}: {QuestionText}", 
                    bonusPrediction.QuestionId, bonusQuestion.Text);
            }

            await _firestoreDb.Collection(_bonusPredictionsCollection)
                .Document(documentId)
                .SetAsync(firestoreBonusPrediction, cancellationToken: cancellationToken);

            _logger.LogDebug("Saved bonus prediction for question '{QuestionText}' with selections: {SelectedOptions}", 
                bonusQuestion.Text, string.Join(", ", selectedOptionTexts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bonus prediction for question {QuestionId}: {QuestionText}", 
                bonusPrediction.QuestionId, bonusQuestion.Text);
            throw;
        }
    }

    public async Task<BonusPrediction?> GetBonusPredictionAsync(string questionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _firestoreDb.Collection(_bonusPredictionsCollection)
                .Document(questionId)
                .GetSnapshotAsync(cancellationToken);

            if (!document.Exists)
            {
                return null;
            }

            var firestoreBonusPrediction = document.ConvertTo<FirestoreBonusPrediction>();
            return new BonusPrediction(firestoreBonusPrediction.QuestionId, firestoreBonusPrediction.SelectedOptionIds.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bonus prediction for question {QuestionId}", questionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<BonusPrediction>> GetAllBonusPredictionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .OrderBy("createdAt");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            var bonusPredictions = new List<BonusPrediction>();
            foreach (var document in snapshot.Documents)
            {
                var firestoreBonusPrediction = document.ConvertTo<FirestoreBonusPrediction>();
                bonusPredictions.Add(new BonusPrediction(
                    firestoreBonusPrediction.QuestionId, 
                    firestoreBonusPrediction.SelectedOptionIds.ToList()));
            }

            return bonusPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all bonus predictions");
            throw;
        }
    }

    public async Task<bool> HasBonusPredictionAsync(string questionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _firestoreDb.Collection(_bonusPredictionsCollection)
                .Document(questionId)
                .GetSnapshotAsync(cancellationToken);

            return document.Exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if bonus prediction exists for question {QuestionId}", questionId);
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
                Competition = _competition
            };

            await _firestoreDb.Collection(_matchesCollection)
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
