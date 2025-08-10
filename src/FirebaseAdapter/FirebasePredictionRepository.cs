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
        
        // Use unified collection names (no longer community-specific)
        _predictionsCollection = "match-predictions";
        _matchesCollection = "matches";
        _bonusPredictionsCollection = "bonus-predictions";
        _competition = "bundesliga-2025-26"; // Remove community suffix
        
        _logger.LogInformation("Firebase repository initialized for community: {Community}", community);
    }

    public async Task SavePredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = Timestamp.GetCurrentTimestamp();
            
            // Check if a prediction already exists for this match, model, and community context
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            
            if (snapshot.Documents.Count > 0)
            {
                // Update existing document
                var existingDoc = snapshot.Documents.First();
                docRef = existingDoc.Reference;
                isUpdate = true;
                
                // Preserve the original createdAt value
                var existingData = existingDoc.ConvertTo<FirestoreMatchPrediction>();
                existingCreatedAt = existingData.CreatedAt;
                
                _logger.LogDebug("Updating existing prediction for match {HomeTeam} vs {AwayTeam} (document: {DocumentId})", 
                    match.HomeTeam, match.AwayTeam, existingDoc.Id);
            }
            else
            {
                // Create new document
                var documentId = Guid.NewGuid().ToString();
                docRef = _firestoreDb.Collection(_predictionsCollection).Document(documentId);
                
                _logger.LogDebug("Creating new prediction for match {HomeTeam} vs {AwayTeam} (document: {DocumentId})", 
                    match.HomeTeam, match.AwayTeam, documentId);
            }
            
            var firestorePrediction = new FirestoreMatchPrediction
            {
                Id = docRef.Id,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                HomeGoals = prediction.HomeGoals,
                AwayGoals = prediction.AwayGoals,
                UpdatedAt = now,
                Competition = _competition,
                Model = model,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext
            };

            // Set CreatedAt: preserve existing value for updates, set current time for new documents
            firestorePrediction.CreatedAt = existingCreatedAt ?? now;

            await docRef.SetAsync(firestorePrediction, cancellationToken: cancellationToken);

            var action = isUpdate ? "Updated" : "Saved";
            _logger.LogInformation("{Action} prediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday}", 
                action, match.HomeTeam, match.AwayTeam, match.Matchday);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save prediction for match {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    public async Task<Prediction?> GetPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return await GetPredictionAsync(match.HomeTeam, match.AwayTeam, match.StartsAt, model, communityContext, cancellationToken);
    }

    public async Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, community context, and competition
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(startsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

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
            _logger.LogError(ex, "Failed to get prediction for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}", 
                homeTeam, awayTeam, model, communityContext);
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

    public async Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all matches for the matchday
            var matches = await GetMatchDayAsync(matchDay, cancellationToken);
            
            // Get predictions for all matches using the specified model and community context
            var matchPredictions = new List<MatchPrediction>();
            
            foreach (var match in matches)
            {
                var prediction = await GetPredictionAsync(match, model, communityContext, cancellationToken);
                matchPredictions.Add(new MatchPrediction(match, prediction));
            }

            return matchPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matches with predictions for matchday {Matchday} using model {Model} and community context {CommunityContext}", matchDay, model, communityContext);
            throw;
        }
    }

    public async Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext)
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
            _logger.LogError(ex, "Failed to get all predictions for model {Model} and community context {CommunityContext}", model, communityContext);
            throw;
        }
    }

    public async Task<bool> HasPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, and community context instead of using deterministic ID
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if prediction exists for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}", 
                match.HomeTeam, match.AwayTeam, model, communityContext);
            throw;
        }
    }

    public async Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = Timestamp.GetCurrentTimestamp();
            
            // Check if a prediction already exists for this question, model, and community context
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionId", bonusPrediction.QuestionId)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            
            if (snapshot.Documents.Count > 0)
            {
                // Update existing document
                var existingDoc = snapshot.Documents.First();
                docRef = existingDoc.Reference;
                isUpdate = true;
                
                // Preserve the original createdAt value
                var existingData = existingDoc.ConvertTo<FirestoreBonusPrediction>();
                existingCreatedAt = existingData.CreatedAt;
                
                _logger.LogDebug("Updating existing bonus prediction for question {QuestionId} (document: {DocumentId})", 
                    bonusPrediction.QuestionId, existingDoc.Id);
            }
            else
            {
                // Create new document
                var documentId = Guid.NewGuid().ToString();
                docRef = _firestoreDb.Collection(_bonusPredictionsCollection).Document(documentId);
                
                _logger.LogDebug("Creating new bonus prediction for question {QuestionId} (document: {DocumentId})", 
                    bonusPrediction.QuestionId, documentId);
            }
            
            // Extract selected option texts for observability
            var optionTextsLookup = bonusQuestion.Options.ToDictionary(o => o.Id, o => o.Text);
            var selectedOptionTexts = bonusPrediction.SelectedOptionIds
                .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
                .ToArray();
            
            var firestoreBonusPrediction = new FirestoreBonusPrediction
            {
                Id = docRef.Id,
                QuestionId = bonusPrediction.QuestionId,
                QuestionText = bonusQuestion.Text,
                SelectedOptionIds = bonusPrediction.SelectedOptionIds.ToArray(),
                SelectedOptionTexts = selectedOptionTexts,
                UpdatedAt = now,
                Competition = _competition,
                Model = model,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext
            };

            // Set CreatedAt: preserve existing value for updates, set current time for new documents
            firestoreBonusPrediction.CreatedAt = existingCreatedAt ?? now;

            await docRef.SetAsync(firestoreBonusPrediction, cancellationToken: cancellationToken);

            var action = isUpdate ? "Updated" : "Saved";
            _logger.LogDebug("{Action} bonus prediction for question '{QuestionText}' with selections: {SelectedOptions}", 
                action, bonusQuestion.Text, string.Join(", ", selectedOptionTexts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bonus prediction for question {QuestionId}: {QuestionText}", 
                bonusPrediction.QuestionId, bonusQuestion.Text);
            throw;
        }
    }

    public async Task<BonusPrediction?> GetBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionId, model, community context, and competition instead of using direct document lookup
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionId", questionId)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            
            if (snapshot.Documents.Count == 0)
            {
                return null;
            }

            var firestoreBonusPrediction = snapshot.Documents.First().ConvertTo<FirestoreBonusPrediction>();
            return new BonusPrediction(firestoreBonusPrediction.QuestionId, firestoreBonusPrediction.SelectedOptionIds.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bonus prediction for question {QuestionId} using model {Model} and community context {CommunityContext}", questionId, model, communityContext);
            throw;
        }
    }

    public async Task<IReadOnlyList<BonusPrediction>> GetAllBonusPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext)
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
            _logger.LogError(ex, "Failed to get all bonus predictions for model {Model} and community context {CommunityContext}", model, communityContext);
            throw;
        }
    }

    public async Task<bool> HasBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionId, model, and community context instead of using direct document lookup
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionId", questionId)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if bonus prediction exists for question {QuestionId} using model {Model} and community context {CommunityContext}", questionId, model, communityContext);
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
            var documentId = Guid.NewGuid().ToString();
            
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
