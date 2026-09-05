using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using FirebaseAdapter.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FirebaseAdapter;

/// <summary>
/// Firebase Firestore implementation of the prediction repository.
/// </summary>
public class FirebasePredictionRepository :
    IPredictionRepository,
    IResolvedMatchContextPredictionRepository,
    IResolvedBonusContextPredictionRepository,
    IBonusPredictionCopyRepository,
    ISchadensfresseChampionsLeagueBonusPredictionRepository
{
    private static readonly JsonSerializerOptions JustificationSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ResolvedContextManifestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FirestoreDb _firestoreDb;
    private readonly ILogger<FirebasePredictionRepository> _logger;
    private readonly string _predictionsCollection;
    private readonly string _matchesCollection;
    private readonly string _bonusPredictionsCollection;
    private readonly string _competition;

    private enum PredictionConfigMatchKind
    {
        None = 0,
        LegacyModelOnly = 1,
        Exact = 2
    }

    public FirebasePredictionRepository(
        FirestoreDb firestoreDb,
        ILogger<FirebasePredictionRepository> logger,
        string competition)
    {
        _firestoreDb = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(competition);

        // Use unified collection names (no longer community-specific)
        _predictionsCollection = "match-predictions";
        _matchesCollection = "matches";
        _bonusPredictionsCollection = "bonus-predictions";
        _competition = competition.Trim();

        _logger.LogInformation("Firebase repository initialized");
    }

    private static PredictionConfigMatchKind GetConfigMatchKind(FirestoreMatchPrediction prediction, PredictionModelConfig modelConfig)
    {
        return GetConfigMatchKind(prediction.ModelConfigKey, prediction.ReasoningEffort, modelConfig);
    }

    private static PredictionConfigMatchKind GetConfigMatchKind(FirestoreBonusPrediction prediction, PredictionModelConfig modelConfig)
    {
        return GetConfigMatchKind(prediction.ModelConfigKey, prediction.ReasoningEffort, modelConfig);
    }

    private static PredictionConfigMatchKind GetConfigMatchKind(
        string? storedModelConfigKey,
        string? storedReasoningEffort,
        PredictionModelConfig modelConfig)
    {
        if (!string.IsNullOrWhiteSpace(storedModelConfigKey))
        {
            return string.Equals(storedModelConfigKey.Trim(), modelConfig.IdentityKey, StringComparison.Ordinal)
                ? PredictionConfigMatchKind.Exact
                : PredictionConfigMatchKind.None;
        }

        if (!string.IsNullOrWhiteSpace(storedReasoningEffort))
        {
            if (!modelConfig.AllowsReasoningEffortOnlyLookup)
            {
                return PredictionConfigMatchKind.None;
            }

            if (!PredictionModelConfig.IsValidReasoningEffort(storedReasoningEffort))
            {
                return PredictionConfigMatchKind.None;
            }

            var normalizedReasoningEffort = PredictionModelConfig.NormalizeReasoningEffort(storedReasoningEffort);
            return string.Equals(normalizedReasoningEffort, modelConfig.ReasoningEffort, StringComparison.Ordinal)
                ? PredictionConfigMatchKind.Exact
                : PredictionConfigMatchKind.None;
        }

        return modelConfig.AllowsLegacyModelOnlyLookup
            ? PredictionConfigMatchKind.LegacyModelOnly
            : PredictionConfigMatchKind.None;
    }

    private static FirestoreMatchPrediction? SelectLatestForModelConfig(
        IEnumerable<FirestoreMatchPrediction> predictions,
        PredictionModelConfig modelConfig)
    {
        return predictions
            .Select(prediction => new
            {
                Prediction = prediction,
                MatchKind = GetConfigMatchKind(prediction, modelConfig)
            })
            .Where(candidate => candidate.MatchKind != PredictionConfigMatchKind.None)
            .OrderByDescending(candidate => candidate.MatchKind)
            .ThenByDescending(candidate => candidate.Prediction.RepredictionIndex)
            .ThenByDescending(candidate => candidate.Prediction.CreatedAt.ToDateTimeOffset())
            .ThenBy(candidate => candidate.Prediction.Id, StringComparer.Ordinal)
            .Select(candidate => candidate.Prediction)
            .FirstOrDefault();
    }

    private static FirestoreBonusPrediction? SelectLatestForModelConfig(
        IEnumerable<FirestoreBonusPrediction> predictions,
        PredictionModelConfig modelConfig)
    {
        return predictions
            .Where(IsOrdinaryBonusPrediction)
            .Select(prediction => new
            {
                Prediction = prediction,
                MatchKind = GetConfigMatchKind(prediction, modelConfig)
            })
            .Where(candidate => candidate.MatchKind != PredictionConfigMatchKind.None)
            .OrderByDescending(candidate => candidate.MatchKind)
            .ThenByDescending(candidate => candidate.Prediction.RepredictionIndex)
            .ThenByDescending(candidate => candidate.Prediction.CreatedAt.ToDateTimeOffset())
            .ThenBy(candidate => candidate.Prediction.Id, StringComparer.Ordinal)
            .Select(candidate => candidate.Prediction)
            .FirstOrDefault();
    }

    public Task SavePredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default)
    {
        return SavePredictionAsync(
            match,
            prediction,
            PredictionModelConfig.Create(model),
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            overrideCreatedAt,
            cancellationToken);
    }

    public async Task SavePredictionAsync(Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default)
    {
        await SavePredictionInternalAsync(match, prediction, modelConfig, tokenUsage, cost, communityContext, contextDocumentNames, null, overrideCreatedAt, cancellationToken);
    }

    public Task SavePredictionWithResolvedContextAsync(
        Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost,
        string communityContext, IEnumerable<string> contextDocumentNames, ResolvedMatchContextManifest resolvedContextManifest,
        bool overrideCreatedAt = false, CancellationToken cancellationToken = default) =>
        SavePredictionInternalAsync(match, prediction, modelConfig, tokenUsage, cost, communityContext, contextDocumentNames,
            resolvedContextManifest, overrideCreatedAt, cancellationToken);

    private async Task SavePredictionInternalAsync(Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, ResolvedMatchContextManifest? resolvedContextManifest, bool overrideCreatedAt, CancellationToken cancellationToken)
    {
        try
        {
            ValidateResolvedContextManifest(match, communityContext, contextDocumentNames, resolvedContextManifest);
            var now = Timestamp.GetCurrentTimestamp();

            // Check if a prediction already exists for this match, model, and community context
            // Order by repredictionIndex descending to get the latest version for updating
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            int repredictionIndex = 0;

            var existingDoc = snapshot.Documents
                .FirstOrDefault(document =>
                    GetConfigMatchKind(document.ConvertTo<FirestoreMatchPrediction>(), modelConfig) == PredictionConfigMatchKind.Exact);

            if (existingDoc is not null)
            {
                // Update existing document (latest reprediction)
                docRef = existingDoc.Reference;
                isUpdate = true;

                // Preserve the original values
                var existingData = existingDoc.ConvertTo<FirestoreMatchPrediction>();
                existingCreatedAt = existingData.CreatedAt;
                repredictionIndex = existingData.RepredictionIndex; // Keep same reprediction index for override

                _logger.LogDebug("Updating existing prediction for match {HomeTeam} vs {AwayTeam} (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                    match.HomeTeam, match.AwayTeam, existingDoc.Id, repredictionIndex);
            }
            else
            {
                // Create new document
                var documentId = Guid.NewGuid().ToString();
                docRef = _firestoreDb.Collection(_predictionsCollection).Document(documentId);
                repredictionIndex = 0; // First prediction

                _logger.LogDebug("Creating new prediction for match {HomeTeam} vs {AwayTeam} (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                    match.HomeTeam, match.AwayTeam, documentId, repredictionIndex);
            }

            var firestorePrediction = new FirestoreMatchPrediction
            {
                Id = docRef.Id,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                CompetitionSpecificData = ToFirestoreCompetitionSpecificData(match.CompetitionSpecificData),
                HomeGoals = prediction.HomeGoals,
                AwayGoals = prediction.AwayGoals,
                Justification = SerializeJustification(prediction.Justification),
                UpdatedAt = now,
                Competition = _competition,
                Model = modelConfig.Model,
                ModelConfigKey = modelConfig.IdentityKey,
                ReasoningEffort = modelConfig.ReasoningEffort,
                MaxOutputTokenCount = modelConfig.MaxOutputTokenCount,
                PromptName = modelConfig.PromptName,
                PromptVersion = modelConfig.PromptVersion,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext,
                ContextDocumentNames = contextDocumentNames.ToArray(),
                ResolvedContextManifest = resolvedContextManifest is null ? null : SerializeResolvedContextManifest(resolvedContextManifest),
                RepredictionIndex = repredictionIndex
            };

            // Set CreatedAt: preserve existing value for updates unless overrideCreatedAt is explicitly requested
            firestorePrediction.CreatedAt = (overrideCreatedAt || existingCreatedAt == null) ? now : existingCreatedAt.Value;

            await docRef.SetAsync(firestorePrediction, cancellationToken: cancellationToken);

            var action = isUpdate ? "Updated" : "Saved";
            _logger.LogInformation("{Action} prediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday} (reprediction index: {RepredictionIndex})",
                action, match.HomeTeam, match.AwayTeam, match.Matchday, repredictionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save prediction for match {HomeTeam} vs {AwayTeam}",
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    public Task<Prediction?> GetPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetPredictionAsync(match, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<Prediction?> GetPredictionAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        return await GetPredictionAsync(match.HomeTeam, match.AwayTeam, match.StartsAt, modelConfig, communityContext, cancellationToken);
    }

    public Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetPredictionAsync(homeTeam, awayTeam, startsAt, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<Prediction?> GetPredictionAsync(string homeTeam, string awayTeam, ZonedDateTime startsAt, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, community context, and competition
            // Order by repredictionIndex descending to get the latest version
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(startsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                return null;
            }

            return new Prediction(
                firestorePrediction.HomeGoals,
                firestorePrediction.AwayGoals,
                DeserializeJustification(firestorePrediction.Justification));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prediction for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                homeTeam, awayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public async Task<Match?> GetLatestPredictedMatchByTeamsAsync(
        string homeTeam,
        string awayTeam,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedHomeTeam = homeTeam.Trim();
            var normalizedAwayTeam = awayTeam.Trim();
            var normalizedCommunityContext = communityContext.Trim();

            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("communityContext", normalizedCommunityContext)
                .WhereEqualTo("homeTeam", normalizedHomeTeam)
                .WhereEqualTo("awayTeam", normalizedAwayTeam)
                .OrderByDescending("startsAt")
                .Limit(1);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = snapshot.Documents
                .FirstOrDefault()
                ?.ConvertTo<FirestoreMatchPrediction>();

            if (firestorePrediction is null)
            {
                _logger.LogDebug(
                    "No predicted match found for {HomeTeam} vs {AwayTeam} in community context {CommunityContext}",
                    normalizedHomeTeam,
                    normalizedAwayTeam,
                    normalizedCommunityContext);
                return null;
            }

            return ToMatch(firestorePrediction);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get latest predicted match for {HomeTeam} vs {AwayTeam} in community context {CommunityContext}",
                homeTeam,
                awayTeam,
                communityContext);
            throw;
        }
    }

    public Task<PredictionMetadata?> GetPredictionMetadataAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetPredictionMetadataAsync(match, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<PredictionMetadata?> GetPredictionMetadataAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, community context, and competition.
            // Order by repredictionIndex descending to keep metadata reads aligned with latest prediction retrieval.
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                return null;
            }

            var prediction = new Prediction(
                firestorePrediction.HomeGoals,
                firestorePrediction.AwayGoals,
                DeserializeJustification(firestorePrediction.Justification));
            var createdAt = firestorePrediction.CreatedAt.ToDateTimeOffset();
            var contextDocumentNames = firestorePrediction.ContextDocumentNames?.ToList() ?? new List<string>();

            var manifest = DeserializeResolvedContextManifest(firestorePrediction.ResolvedContextManifest);
            if (manifest is not null)
            {
                ValidateResolvedContextManifest(match, communityContext, contextDocumentNames, manifest);
            }
            return new PredictionMetadata(
                prediction,
                createdAt,
                contextDocumentNames,
                manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prediction metadata for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                match.HomeTeam, match.AwayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public async Task<ResolvedMatchContextManifest?> GetResolvedMatchContextManifestAsync(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetPredictionMetadataAsync(match, modelConfig, communityContext, cancellationToken);
        return metadata?.ResolvedContextManifest;
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
                .Select(ToMatch)
                .ToList();

            return matches.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matches for matchday {Matchday}", matchDay);
            throw;
        }
    }

    public Task<Match?> GetStoredMatchAsync(string homeTeam, string awayTeam, int matchDay, string? model = null, string? communityContext = null, CancellationToken cancellationToken = default)
    {
        var modelConfig = string.IsNullOrWhiteSpace(model)
            ? null
            : PredictionModelConfig.Create(model);
        return GetStoredMatchAsync(homeTeam, awayTeam, matchDay, modelConfig, communityContext, cancellationToken);
    }

    public async Task<Match?> GetStoredMatchAsync(string homeTeam, string awayTeam, int matchDay, PredictionModelConfig? modelConfig, string? communityContext = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var matchQuery = _firestoreDb.Collection(_matchesCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("matchday", matchDay)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam);

            var matchSnapshot = await matchQuery.GetSnapshotAsync(cancellationToken);

            if (matchSnapshot.Documents.Count > 0)
            {
                if (matchSnapshot.Documents.Count > 1)
                {
                    _logger.LogWarning("Found {Count} stored match documents for {HomeTeam} vs {AwayTeam} on matchday {Matchday}; selecting deterministically by startsAt", matchSnapshot.Documents.Count, homeTeam, awayTeam, matchDay);
                }

                return matchSnapshot.Documents
                    .Select(document => document.ConvertTo<FirestoreMatch>())
                    .Select(ToMatch)
                    .OrderBy(match => match.StartsAt.ToInstant())
                    .ThenBy(match => match.IsCancelled)
                    .First();
            }

            Query predictionQuery = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("matchday", matchDay)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam);

            if (modelConfig is not null)
            {
                predictionQuery = predictionQuery.WhereEqualTo("model", modelConfig.Model);
            }

            if (!string.IsNullOrWhiteSpace(communityContext))
            {
                predictionQuery = predictionQuery.WhereEqualTo("communityContext", communityContext);
            }

            var predictionSnapshot = await predictionQuery.GetSnapshotAsync(cancellationToken);

            if (predictionSnapshot.Documents.Count == 0)
            {
                return null;
            }

            if (predictionSnapshot.Documents.Count > 1)
            {
                _logger.LogWarning("Found {Count} stored prediction documents for {HomeTeam} vs {AwayTeam} on matchday {Matchday}; selecting deterministically by reprediction metadata", predictionSnapshot.Documents.Count, homeTeam, awayTeam, matchDay);
            }

            var predictions = predictionSnapshot.Documents
                .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                .Select(prediction => new
                {
                    Prediction = prediction,
                    MatchKind = modelConfig is null
                        ? PredictionConfigMatchKind.Exact
                        : GetConfigMatchKind(prediction, modelConfig)
                })
                .Where(candidate => candidate.MatchKind != PredictionConfigMatchKind.None)
                .ToList();

            if (predictions.Count == 0)
            {
                return null;
            }

            var firestorePrediction = predictions
                .OrderByDescending(candidate => candidate.MatchKind)
                .ThenByDescending(candidate => candidate.Prediction.RepredictionIndex)
                .ThenByDescending(candidate => candidate.Prediction.CreatedAt.ToDateTimeOffset())
                .ThenBy(candidate => candidate.Prediction.StartsAt.ToDateTimeOffset())
                .ThenBy(candidate => candidate.Prediction.Id, StringComparer.Ordinal)
                .Select(candidate => candidate.Prediction)
                .First();

            return ToMatch(firestorePrediction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stored match {HomeTeam} vs {AwayTeam} for matchday {Matchday}", homeTeam, awayTeam, matchDay);
            throw;
        }
    }

    public Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetMatchDayWithPredictionsAsync(matchDay, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchPrediction>> GetMatchDayWithPredictionsAsync(int matchDay, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all matches for the matchday
            var matches = await GetMatchDayAsync(matchDay, cancellationToken);

            // Get predictions for all matches using the specified model and community context
            var matchPredictions = new List<MatchPrediction>();

            foreach (var match in matches)
            {
                var prediction = await GetPredictionAsync(match, modelConfig, communityContext, cancellationToken);
                matchPredictions.Add(new MatchPrediction(match, prediction));
            }

            return matchPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get matches with predictions for matchday {Matchday} using model {Model} and community context {CommunityContext}", matchDay, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetAllPredictionsAsync(PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchPrediction>> GetAllPredictionsAsync(PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderBy("matchday");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            var matchPredictions = snapshot.Documents
                .Select(doc => doc.ConvertTo<FirestoreMatchPrediction>())
                .Where(fp => GetConfigMatchKind(fp, modelConfig) != PredictionConfigMatchKind.None)
                .Select(fp => new MatchPrediction(
                    ToMatch(fp),
                    new Prediction(
                        fp.HomeGoals,
                        fp.AwayGoals,
                        DeserializeJustification(fp.Justification))))
                .ToList();

            return matchPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all predictions for model {Model} and community context {CommunityContext}", modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<bool> HasPredictionAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return HasPredictionAsync(match, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<bool> HasPredictionAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, and community context instead of using deterministic ID
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents
                .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                .Any(prediction => GetConfigMatchKind(prediction, modelConfig) != PredictionConfigMatchKind.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if prediction exists for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                match.HomeTeam, match.AwayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public async Task<BonusPredictionMetadata?> GetCurrentAsync(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EnsureClRepositoryScope();
        var snapshot = await CreateClCandidateQuery(scope).GetSnapshotAsync(cancellationToken);
        var current = SelectCurrentClCandidate(snapshot.Documents, scope);
        return current is null ? null : CreateClMetadata(current.Value.Prediction, scope);
    }

    public async Task<int> GetCurrentRepredictionIndexAsync(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        EnsureClRepositoryScope();
        var snapshot = await CreateClCandidateQuery(scope).GetSnapshotAsync(cancellationToken);
        return SelectCurrentClCandidate(snapshot.Documents, scope)?.Prediction.RepredictionIndex ?? -1;
    }

    public async Task SaveAsync(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        BonusPrediction prediction,
        string promptProvider,
        string tokenUsage,
        double cost,
        bool overrideExisting,
        CancellationToken cancellationToken = default)
    {
        ValidateClWrite(scope, prediction, promptProvider, tokenUsage, cost);
        await _firestoreDb.RunTransactionAsync(async transaction =>
        {
            var candidates = await transaction.GetSnapshotAsync(CreateClCandidateQuery(scope));
            var current = SelectCurrentClCandidate(candidates.Documents, scope);
            if (current is not null && !overrideExisting)
            {
                throw new InvalidOperationException("A current CL lineage row appeared before initial save; explicit database override is required.");
            }

            var now = Timestamp.GetCurrentTimestamp();
            if (current is null)
            {
                var reference = _firestoreDb.Collection(_bonusPredictionsCollection).Document(Guid.NewGuid().ToString());
                transaction.Create(reference, CreateClFirestorePrediction(reference.Id, scope, prediction, promptProvider, tokenUsage, cost, 0, now, now));
            }
            else
            {
                transaction.Set(current.Value.Document.Reference, CreateClFirestorePrediction(
                    current.Value.Document.Id,
                    scope,
                    prediction,
                    promptProvider,
                    tokenUsage,
                    cost,
                    current.Value.Prediction.RepredictionIndex,
                    now,
                    now));
            }
            return 0;
        }, cancellationToken: cancellationToken);
    }

    public async Task SaveRepredictionAsync(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        BonusPrediction prediction,
        string promptProvider,
        string tokenUsage,
        double cost,
        int expectedCurrentRepredictionIndex,
        int maxRepredictions,
        CancellationToken cancellationToken = default)
    {
        ValidateClWrite(scope, prediction, promptProvider, tokenUsage, cost);
        if (expectedCurrentRepredictionIndex < -1 || maxRepredictions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCurrentRepredictionIndex));
        }

        await _firestoreDb.RunTransactionAsync(async transaction =>
        {
            var candidates = await transaction.GetSnapshotAsync(CreateClCandidateQuery(scope));
            var current = SelectCurrentClCandidate(candidates.Documents, scope);
            var actualIndex = current?.Prediction.RepredictionIndex ?? -1;
            if (actualIndex != expectedCurrentRepredictionIndex)
            {
                throw new InvalidOperationException($"CL reprediction concurrency conflict: expected {expectedCurrentRepredictionIndex}, found {actualIndex}.");
            }
            if (actualIndex == int.MaxValue || checked(actualIndex + 1) > maxRepredictions)
            {
                throw new InvalidOperationException("The CL reprediction limit does not permit another lineage row.");
            }

            var nextIndex = actualIndex + 1;
            var reference = _firestoreDb.Collection(_bonusPredictionsCollection).Document(Guid.NewGuid().ToString());
            var now = Timestamp.GetCurrentTimestamp();
            transaction.Create(reference, CreateClFirestorePrediction(reference.Id, scope, prediction, promptProvider, tokenUsage, cost, nextIndex, now, now));
            return nextIndex;
        }, cancellationToken: cancellationToken);
    }

    public Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default)
    {
        return SaveBonusPredictionAsync(
            bonusQuestion,
            bonusPrediction,
            PredictionModelConfig.Create(model),
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            overrideCreatedAt,
            cancellationToken);
    }

    public Task SaveBonusPredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, bool overrideCreatedAt = false, CancellationToken cancellationToken = default)
    {
        return SaveBonusPredictionInternalAsync(
            bonusQuestion,
            bonusPrediction,
            modelConfig,
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            null,
            overrideCreatedAt,
            cancellationToken);
    }

    public Task SaveBonusPredictionWithResolvedContextAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedBonusContextManifest resolvedContextManifest,
        bool overrideCreatedAt = false,
        CancellationToken cancellationToken = default)
    {
        return SaveBonusPredictionInternalAsync(
            bonusQuestion,
            bonusPrediction,
            modelConfig,
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            resolvedContextManifest,
            overrideCreatedAt,
            cancellationToken);
    }

    private async Task SaveBonusPredictionInternalAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedBonusContextManifest? resolvedContextManifest,
        bool overrideCreatedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var documentNames = contextDocumentNames?.ToArray()
                ?? throw new ArgumentNullException(nameof(contextDocumentNames));
            ValidateResolvedBonusContextManifestForWrite(
                communityContext,
                documentNames,
                resolvedContextManifest);
            var now = Timestamp.GetCurrentTimestamp();

            // Check if a prediction already exists for this question, model, and community context
            // Order by repredictionIndex descending to get the latest version for updating
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionText", bonusQuestion.Text)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            int repredictionIndex = 0;

            var existingDoc = snapshot.Documents
                .FirstOrDefault(document =>
                {
                    var prediction = document.ConvertTo<FirestoreBonusPrediction>();
                    return IsOrdinaryBonusPrediction(prediction)
                           && GetConfigMatchKind(prediction, modelConfig) == PredictionConfigMatchKind.Exact;
                });

            if (existingDoc is not null)
            {
                // Update existing document (latest reprediction)
                docRef = existingDoc.Reference;
                isUpdate = true;

                // Preserve the original values
                var existingData = existingDoc.ConvertTo<FirestoreBonusPrediction>();
                existingCreatedAt = existingData.CreatedAt;
                repredictionIndex = existingData.RepredictionIndex; // Keep same reprediction index for override

                _logger.LogDebug("Updating existing bonus prediction for question '{QuestionText}' (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                    bonusQuestion.Text, existingDoc.Id, repredictionIndex);
            }
            else
            {
                // Create new document
                var documentId = Guid.NewGuid().ToString();
                docRef = _firestoreDb.Collection(_bonusPredictionsCollection).Document(documentId);
                repredictionIndex = 0; // First prediction

                _logger.LogDebug("Creating new bonus prediction for question '{QuestionText}' (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                    bonusQuestion.Text, documentId, repredictionIndex);
            }

            // Extract selected option texts for observability
            var optionTextsLookup = bonusQuestion.Options.ToDictionary(o => o.Id, o => o.Text);
            var selectedOptionTexts = bonusPrediction.SelectedOptionIds
                .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
                .ToArray();

            var firestoreBonusPrediction = new FirestoreBonusPrediction
            {
                Id = docRef.Id,
                QuestionText = bonusQuestion.Text,
                SelectedOptionIds = bonusPrediction.SelectedOptionIds.ToArray(),
                SelectedOptionTexts = selectedOptionTexts,
                UpdatedAt = now,
                Competition = _competition,
                Model = modelConfig.Model,
                ModelConfigKey = modelConfig.IdentityKey,
                ReasoningEffort = modelConfig.ReasoningEffort,
                MaxOutputTokenCount = modelConfig.MaxOutputTokenCount,
                PromptName = modelConfig.PromptName,
                PromptVersion = modelConfig.PromptVersion,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext,
                ContextDocumentNames = documentNames,
                ResolvedBonusContextManifest = resolvedContextManifest is null
                    ? null
                    : SerializeResolvedBonusContextManifest(resolvedContextManifest),
                BonusQuestionCompatibilityManifest = SerializeBonusQuestionCompatibilityManifest(
                    BonusQuestionCompatibilityManifest.Create(bonusQuestion)),
                RepredictionIndex = repredictionIndex
            };

            // Set CreatedAt: preserve existing value for updates unless overrideCreatedAt is explicitly requested
            firestoreBonusPrediction.CreatedAt = (overrideCreatedAt || existingCreatedAt == null) ? now : existingCreatedAt.Value;

            await docRef.SetAsync(firestoreBonusPrediction, cancellationToken: cancellationToken);

            var action = isUpdate ? "Updated" : "Saved";
            _logger.LogDebug("{Action} bonus prediction for question '{QuestionText}' with selections: {SelectedOptions} (reprediction index: {RepredictionIndex})",
                action, bonusQuestion.Text, string.Join(", ", selectedOptionTexts), repredictionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bonus prediction for question: {QuestionText}",
                bonusQuestion.Text);
            throw;
        }
    }

    public Task<BonusPrediction?> GetBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetBonusPredictionAsync(questionId, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<BonusPrediction?> GetBonusPredictionAsync(string questionId, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionId, model, community context, and competition instead of using direct document lookup
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionId", questionId)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            if (snapshot.Documents.Count == 0)
            {
                return null;
            }

            var firestoreBonusPrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>()),
                modelConfig);

            if (firestoreBonusPrediction is null)
            {
                return null;
            }

            return new BonusPrediction(firestoreBonusPrediction.SelectedOptionIds.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bonus prediction for question {QuestionId} using model {Model} and community context {CommunityContext}", questionId, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<BonusPrediction?> GetBonusPredictionByTextAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetBonusPredictionByTextAsync(questionText, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<BonusPrediction?> GetBonusPredictionByTextAsync(string questionText, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionText, model, and community context
            // Order by repredictionIndex descending to get the latest version
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionText", questionText)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestoreBonusPrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>()),
                modelConfig);

            if (firestoreBonusPrediction is null)
            {
                _logger.LogDebug("No bonus prediction found for question text: {QuestionText} with model: {Model} and community context: {CommunityContext}", questionText, modelConfig.DisplayName, communityContext);
                return null;
            }

            var bonusPrediction = new BonusPrediction(firestoreBonusPrediction.SelectedOptionIds.ToList());

            _logger.LogDebug("Found bonus prediction for question text: {QuestionText} with model: {Model} and community context: {CommunityContext} (reprediction index: {RepredictionIndex})",
                questionText, modelConfig.DisplayName, communityContext, firestoreBonusPrediction.RepredictionIndex);

            return bonusPrediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve bonus prediction by text: {QuestionText} with model: {Model} and community context: {CommunityContext}", questionText, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<BonusPredictionMetadata?> GetBonusPredictionMetadataByTextAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetBonusPredictionMetadataByTextAsync(questionText, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<BonusPredictionMetadata?> GetBonusPredictionMetadataByTextAsync(string questionText, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionText, model, and community context.
            // Order by repredictionIndex descending to align metadata reads with latest bonus prediction retrieval.
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionText", questionText)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestoreBonusPrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>()),
                modelConfig);

            if (firestoreBonusPrediction is null)
            {
                _logger.LogDebug("No bonus prediction metadata found for question text: {QuestionText} with model: {Model} and community context: {CommunityContext}", questionText, modelConfig.DisplayName, communityContext);
                return null;
            }

            _logger.LogDebug("Found bonus prediction metadata for question text: {QuestionText} with model: {Model} and community context: {CommunityContext}",
                questionText, modelConfig.DisplayName, communityContext);

            return CreateBonusPredictionMetadata(firestoreBonusPrediction, communityContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve bonus prediction metadata by text: {QuestionText} with model: {Model} and community context: {CommunityContext}", questionText, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public async Task<BonusPredictionMetadata?> GetBonusPredictionCopyCandidateAsync(
        BonusQuestion targetQuestion,
        PredictionModelConfig modelConfig,
        string sourceCommunityContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetQuestion);
        ArgumentNullException.ThrowIfNull(modelConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCommunityContext);

        try
        {
            var normalizedQuestionText = BonusQuestionCompatibilityManifest.NormalizeText(targetQuestion.Text);
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", sourceCommunityContext);
            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestoreBonusPrediction = SelectLatestForModelConfig(
                snapshot.Documents
                    .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(prediction => string.Equals(
                        BonusQuestionCompatibilityManifest.NormalizeText(prediction.QuestionText),
                        normalizedQuestionText,
                        StringComparison.Ordinal)),
                modelConfig);

            if (firestoreBonusPrediction is null)
            {
                return null;
            }

            var metadata = CreateBonusPredictionMetadata(
                firestoreBonusPrediction,
                sourceCommunityContext,
                tolerateInvalidCompatibilityManifest: true);
            if (metadata.QuestionCompatibilityManifest is null
                && !string.IsNullOrWhiteSpace(firestoreBonusPrediction.BonusQuestionCompatibilityManifest))
            {
                _logger.LogWarning(
                    "Stored bonus prediction {DocumentId} has invalid compatibility provenance and cannot be copied.",
                    firestoreBonusPrediction.Id);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve bonus copy candidate using model {Model} and source community context {CommunityContext}",
                modelConfig.DisplayName,
                sourceCommunityContext);
            throw;
        }
    }

    public Task<IReadOnlyList<BonusPrediction>> GetAllBonusPredictionsAsync(string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetAllBonusPredictionsAsync(PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<IReadOnlyList<BonusPrediction>> GetAllBonusPredictionsAsync(PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderBy("createdAt");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            var bonusPredictions = new List<BonusPrediction>();
            foreach (var document in snapshot.Documents)
            {
                var firestoreBonusPrediction = document.ConvertTo<FirestoreBonusPrediction>();
                if (!IsOrdinaryBonusPrediction(firestoreBonusPrediction)
                    || GetConfigMatchKind(firestoreBonusPrediction, modelConfig) == PredictionConfigMatchKind.None)
                {
                    continue;
                }

                bonusPredictions.Add(new BonusPrediction(
                    firestoreBonusPrediction.SelectedOptionIds.ToList()));
            }

            return bonusPredictions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all bonus predictions for model {Model} and community context {CommunityContext}", modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<bool> HasBonusPredictionAsync(string questionId, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return HasBonusPredictionAsync(questionId, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<bool> HasBonusPredictionAsync(string questionId, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by questionId, model, and community context instead of using direct document lookup
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionId", questionId)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            return snapshot.Documents
                .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                .Any(prediction => IsOrdinaryBonusPrediction(prediction)
                                   && GetConfigMatchKind(prediction, modelConfig) != PredictionConfigMatchKind.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if bonus prediction exists for question {QuestionId} using model {Model} and community context {CommunityContext}", questionId, modelConfig.DisplayName, communityContext);
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
                Competition = _competition,
                IsCancelled = match.IsCancelled,
                CompetitionSpecificData = ToFirestoreCompetitionSpecificData(match.CompetitionSpecificData)
            };

            await _firestoreDb.Collection(_matchesCollection)
                .Document(documentId)
                .SetAsync(firestoreMatch, cancellationToken: cancellationToken);

            _logger.LogDebug("Stored match {HomeTeam} vs {AwayTeam} for matchday {Matchday}{Cancelled}",
                match.HomeTeam, match.AwayTeam, match.Matchday, match.IsCancelled ? " (CANCELLED)" : "");
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

    private static Match ToMatch(FirestoreMatchPrediction firestorePrediction)
    {
        return new Match(
            firestorePrediction.HomeTeam,
            firestorePrediction.AwayTeam,
            ConvertFromTimestamp(firestorePrediction.StartsAt),
            firestorePrediction.Matchday)
        {
            CompetitionSpecificData = FromFirestoreCompetitionSpecificData(
                firestorePrediction.CompetitionSpecificData)
        };
    }

    private static Match ToMatch(FirestoreMatch firestoreMatch)
    {
        return new Match(
            firestoreMatch.HomeTeam,
            firestoreMatch.AwayTeam,
            ConvertFromTimestamp(firestoreMatch.StartsAt),
            firestoreMatch.Matchday,
            firestoreMatch.IsCancelled)
        {
            CompetitionSpecificData = FromFirestoreCompetitionSpecificData(
                firestoreMatch.CompetitionSpecificData)
        };
    }

    private static FirestoreCompetitionSpecificMatchData? ToFirestoreCompetitionSpecificData(
        CompetitionSpecificMatchData? competitionSpecificData)
    {
        return competitionSpecificData is FifaWorldCup2026MatchData worldCupData
            ? new FirestoreCompetitionSpecificMatchData
            {
                Type = "fifaWorldCup2026",
                Competition = worldCupData.Competition,
                KicktippRoundName = worldCupData.KicktippRoundName,
                Stage = worldCupData.Stage.ToValue(),
                ResultBasis = FifaWorldCup2026MatchDataValues.FinalScoreIncludingExtraTimeAndPenaltyShootout
            }
            : null;
    }

    private static CompetitionSpecificMatchData? FromFirestoreCompetitionSpecificData(
        FirestoreCompetitionSpecificMatchData? firestoreData)
    {
        if (firestoreData is null ||
            !string.Equals(firestoreData.Type, "fifaWorldCup2026", StringComparison.Ordinal) ||
            !string.Equals(
                firestoreData.Competition,
                CompetitionIds.FifaWorldCup2026,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                firestoreData.ResultBasis,
                FifaWorldCup2026MatchDataValues.FinalScoreIncludingExtraTimeAndPenaltyShootout,
                StringComparison.Ordinal) ||
            !FifaWorldCup2026MatchDataValues.TryParseStage(firestoreData.Stage, out var stage))
        {
            return null;
        }

        return new FifaWorldCup2026MatchData(
            firestoreData.KicktippRoundName,
            stage,
            FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout);
    }

    private static ZonedDateTime ConvertFromTimestamp(Timestamp timestamp)
    {
        var dateTimeOffset = timestamp.ToDateTimeOffset();
        var instant = Instant.FromDateTimeOffset(dateTimeOffset);
        return instant.InUtc();
    }

    public Task<int> GetMatchRepredictionIndexAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetMatchRepredictionIndexAsync(match, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<int> GetMatchRepredictionIndexAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by match characteristics, model, community context, and competition
            // Order by repredictionIndex descending to get the latest version
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                return -1; // No prediction exists
            }

            return firestorePrediction.RepredictionIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reprediction index for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                match.HomeTeam, match.AwayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    // See IPredictionRepository.cs for detailed documentation on why these methods exist.
    // In short: cancelled matches have inconsistent startsAt values across different Kicktipp pages,
    // so we query by team names only to find predictions regardless of which startsAt was used.

    /// <inheritdoc />
    public Task<Prediction?> GetCancelledMatchPredictionAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetCancelledMatchPredictionAsync(homeTeam, awayTeam, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<Prediction?> GetCancelledMatchPredictionAsync(string homeTeam, string awayTeam, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by team names only (no startsAt), ordered by createdAt descending to get the most recent
            // We use repredictionIndex descending first to get the latest reprediction, then createdAt for tiebreaking
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("createdAt");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                _logger.LogDebug("No prediction found for cancelled match {HomeTeam} vs {AwayTeam} (team-names-only lookup)", homeTeam, awayTeam);
                return null;
            }

            _logger.LogDebug("Found prediction for cancelled match {HomeTeam} vs {AwayTeam} with startsAt={StartsAt} (team-names-only lookup)",
                homeTeam, awayTeam, firestorePrediction.StartsAt);

            return new Prediction(
                firestorePrediction.HomeGoals,
                firestorePrediction.AwayGoals,
                DeserializeJustification(firestorePrediction.Justification));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prediction for cancelled match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                homeTeam, awayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<PredictionMetadata?> GetCancelledMatchPredictionMetadataAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetCancelledMatchPredictionMetadataAsync(homeTeam, awayTeam, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<PredictionMetadata?> GetCancelledMatchPredictionMetadataAsync(string homeTeam, string awayTeam, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by team names only (no startsAt), ordered by repredictionIndex descending to get the latest reprediction.
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                _logger.LogDebug("No prediction metadata found for cancelled match {HomeTeam} vs {AwayTeam} (team-names-only lookup)", homeTeam, awayTeam);
                return null;
            }

            _logger.LogDebug("Found prediction metadata for cancelled match {HomeTeam} vs {AwayTeam} with startsAt={StartsAt} (team-names-only lookup)",
                homeTeam, awayTeam, firestorePrediction.StartsAt);

            var prediction = new Prediction(
                firestorePrediction.HomeGoals,
                firestorePrediction.AwayGoals,
                DeserializeJustification(firestorePrediction.Justification));
            var createdAt = firestorePrediction.CreatedAt.ToDateTimeOffset();
            var contextDocumentNames = firestorePrediction.ContextDocumentNames?.ToList() ?? new List<string>();

            var manifest = DeserializeResolvedContextManifest(firestorePrediction.ResolvedContextManifest);
            if (manifest is not null)
            {
                var storedMatch = new Match(
                    firestorePrediction.HomeTeam,
                    firestorePrediction.AwayTeam,
                    ConvertFromTimestamp(firestorePrediction.StartsAt),
                    firestorePrediction.Matchday,
                    true);
                ValidateResolvedContextManifest(storedMatch, communityContext, contextDocumentNames, manifest);
            }
            return new PredictionMetadata(prediction, createdAt, contextDocumentNames, manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get prediction metadata for cancelled match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                homeTeam, awayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<int> GetCancelledMatchRepredictionIndexAsync(string homeTeam, string awayTeam, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetCancelledMatchRepredictionIndexAsync(homeTeam, awayTeam, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<int> GetCancelledMatchRepredictionIndexAsync(string homeTeam, string awayTeam, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by team names only (no startsAt), ordered by repredictionIndex descending to get the highest
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", homeTeam)
                .WhereEqualTo("awayTeam", awayTeam)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                _logger.LogDebug("No reprediction index found for cancelled match {HomeTeam} vs {AwayTeam} (team-names-only lookup)", homeTeam, awayTeam);
                return -1;
            }

            _logger.LogDebug("Found reprediction index {Index} for cancelled match {HomeTeam} vs {AwayTeam} with startsAt={StartsAt} (team-names-only lookup)",
                firestorePrediction.RepredictionIndex, homeTeam, awayTeam, firestorePrediction.StartsAt);

            return firestorePrediction.RepredictionIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reprediction index for cancelled match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                homeTeam, awayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task<int> GetBonusRepredictionIndexAsync(string questionText, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetBonusRepredictionIndexAsync(questionText, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<int> GetBonusRepredictionIndexAsync(string questionText, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            // Query by question text, model, community context, and competition
            // Order by repredictionIndex descending to get the latest version
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionText", questionText)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext)
                .OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>()),
                modelConfig);

            if (firestorePrediction is null)
            {
                return -1; // No prediction exists
            }

            return firestorePrediction.RepredictionIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get reprediction index for bonus question '{QuestionText}' using model {Model} and community context {CommunityContext}",
                questionText, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public Task SaveRepredictionAsync(Match match, Prediction prediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default)
    {
        return SaveRepredictionAsync(
            match,
            prediction,
            PredictionModelConfig.Create(model),
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            repredictionIndex,
            cancellationToken);
    }

    public async Task SaveRepredictionAsync(Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default)
    {
        await SaveRepredictionInternalAsync(match, prediction, modelConfig, tokenUsage, cost, communityContext, contextDocumentNames, repredictionIndex, null, cancellationToken);
    }

    public Task SaveRepredictionWithResolvedContextAsync(
        Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost,
        string communityContext, IEnumerable<string> contextDocumentNames, int expectedCurrentRepredictionIndex,
        int maxRepredictions, ResolvedMatchContextManifest resolvedContextManifest,
        CancellationToken cancellationToken = default) =>
        SaveBundesligaRepredictionWithResolvedContextAsync(
            match,
            prediction,
            modelConfig,
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            expectedCurrentRepredictionIndex,
            maxRepredictions,
            resolvedContextManifest,
            cancellationToken);

    private async Task SaveBundesligaRepredictionWithResolvedContextAsync(
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        int expectedCurrentRepredictionIndex,
        int maxRepredictions,
        ResolvedMatchContextManifest resolvedContextManifest,
        CancellationToken cancellationToken)
    {
        ValidateResolvedContextManifest(match, communityContext, contextDocumentNames, resolvedContextManifest);
        if (expectedCurrentRepredictionIndex < -1 || maxRepredictions < 0 || expectedCurrentRepredictionIndex > maxRepredictions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCurrentRepredictionIndex),
                "The expected current reprediction index must be -1 through the configured nonnegative maximum.");
        }

        try
        {
            var storedNames = contextDocumentNames.ToArray();
            var savedIndex = await _firestoreDb.RunTransactionAsync(async transaction =>
            {
                Query query = _firestoreDb.Collection(_predictionsCollection)
                    .WhereEqualTo("homeTeam", match.HomeTeam)
                    .WhereEqualTo("awayTeam", match.AwayTeam)
                    .WhereEqualTo("competition", _competition)
                    .WhereEqualTo("model", modelConfig.Model)
                    .WhereEqualTo("communityContext", communityContext);
                if (!match.IsCancelled)
                {
                    query = query.WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt));
                }

                // A transaction reads the whole exact candidate set, rather than trusting the
                // index observed before generation. Firestore retries on a concurrent matching
                // write, making this compare-and-swap serializable for this prediction identity.
                var candidates = await transaction.GetSnapshotAsync(query);
                var current = SelectLatestForModelConfig(
                    candidates.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>()),
                    modelConfig);
                var actualCurrentIndex = current?.RepredictionIndex ?? -1;
                if (actualCurrentIndex != expectedCurrentRepredictionIndex)
                {
                    throw new InvalidOperationException(
                        $"Bundesliga reprediction concurrency conflict: expected current index {expectedCurrentRepredictionIndex}, found {actualCurrentIndex}.");
                }

                if (actualCurrentIndex == int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Bundesliga reprediction index overflow: no index can be allocated after Int32.MaxValue.");
                }

                var nextIndex = checked(actualCurrentIndex + 1);
                if (nextIndex > maxRepredictions)
                {
                    throw new InvalidOperationException(
                        $"Bundesliga reprediction maximum conflict: next index {nextIndex} exceeds configured maximum {maxRepredictions}.");
                }

                var documentId = BuildBundesligaRepredictionDocumentId(match, modelConfig, communityContext, nextIndex);
                var docRef = _firestoreDb.Collection(_predictionsCollection).Document(documentId);
                var existing = await transaction.GetSnapshotAsync(docRef);
                if (existing.Exists)
                {
                    throw new InvalidOperationException(
                        $"Bundesliga reprediction concurrency conflict: index {nextIndex} is already allocated.");
                }

                var now = Timestamp.GetCurrentTimestamp();
                transaction.Create(docRef, new FirestoreMatchPrediction
                {
                    Id = docRef.Id,
                    HomeTeam = match.HomeTeam,
                    AwayTeam = match.AwayTeam,
                    StartsAt = ConvertToTimestamp(match.StartsAt),
                    Matchday = match.Matchday,
                    CompetitionSpecificData = ToFirestoreCompetitionSpecificData(match.CompetitionSpecificData),
                    HomeGoals = prediction.HomeGoals,
                    AwayGoals = prediction.AwayGoals,
                    Justification = SerializeJustification(prediction.Justification),
                    CreatedAt = now,
                    UpdatedAt = now,
                    Competition = _competition,
                    Model = modelConfig.Model,
                    ModelConfigKey = modelConfig.IdentityKey,
                    ReasoningEffort = modelConfig.ReasoningEffort,
                    MaxOutputTokenCount = modelConfig.MaxOutputTokenCount,
                    PromptName = modelConfig.PromptName,
                    PromptVersion = modelConfig.PromptVersion,
                    TokenUsage = tokenUsage,
                    Cost = cost,
                    CommunityContext = communityContext,
                    ContextDocumentNames = storedNames,
                    ResolvedContextManifest = SerializeResolvedContextManifest(resolvedContextManifest),
                    RepredictionIndex = nextIndex
                });
                return nextIndex;
            }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Saved transactionally allocated Bundesliga reprediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday} (reprediction index: {RepredictionIndex})",
                match.HomeTeam,
                match.AwayTeam,
                match.Matchday,
                savedIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save transactionally allocated Bundesliga reprediction for match {HomeTeam} vs {AwayTeam}",
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    private async Task SaveRepredictionInternalAsync(Match match, Prediction prediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, ResolvedMatchContextManifest? resolvedContextManifest, CancellationToken cancellationToken)
    {
        try
        {
            ValidateResolvedContextManifest(match, communityContext, contextDocumentNames, resolvedContextManifest);
            var now = Timestamp.GetCurrentTimestamp();

            // Create new document for this reprediction
            var documentId = Guid.NewGuid().ToString();
            var docRef = _firestoreDb.Collection(_predictionsCollection).Document(documentId);

            _logger.LogDebug("Creating reprediction for match {HomeTeam} vs {AwayTeam} (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                match.HomeTeam, match.AwayTeam, documentId, repredictionIndex);

            var firestorePrediction = new FirestoreMatchPrediction
            {
                Id = docRef.Id,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                CompetitionSpecificData = ToFirestoreCompetitionSpecificData(match.CompetitionSpecificData),
                HomeGoals = prediction.HomeGoals,
                AwayGoals = prediction.AwayGoals,
                Justification = SerializeJustification(prediction.Justification),
                CreatedAt = now,
                UpdatedAt = now,
                Competition = _competition,
                Model = modelConfig.Model,
                ModelConfigKey = modelConfig.IdentityKey,
                ReasoningEffort = modelConfig.ReasoningEffort,
                MaxOutputTokenCount = modelConfig.MaxOutputTokenCount,
                PromptName = modelConfig.PromptName,
                PromptVersion = modelConfig.PromptVersion,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext,
                ContextDocumentNames = contextDocumentNames.ToArray(),
                ResolvedContextManifest = resolvedContextManifest is null ? null : SerializeResolvedContextManifest(resolvedContextManifest),
                RepredictionIndex = repredictionIndex
            };

            await docRef.SetAsync(firestorePrediction, cancellationToken: cancellationToken);

            _logger.LogInformation("Saved reprediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday} (reprediction index: {RepredictionIndex})",
                match.HomeTeam, match.AwayTeam, match.Matchday, repredictionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save reprediction for match {HomeTeam} vs {AwayTeam}",
                match.HomeTeam, match.AwayTeam);
            throw;
        }
    }

    public Task SaveBonusRepredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, string model, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default)
    {
        return SaveBonusRepredictionAsync(
            bonusQuestion,
            bonusPrediction,
            PredictionModelConfig.Create(model),
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            repredictionIndex,
            cancellationToken);
    }

    public Task SaveBonusRepredictionAsync(BonusQuestion bonusQuestion, BonusPrediction bonusPrediction, PredictionModelConfig modelConfig, string tokenUsage, double cost, string communityContext, IEnumerable<string> contextDocumentNames, int repredictionIndex, CancellationToken cancellationToken = default)
    {
        return SaveBonusRepredictionInternalAsync(
            bonusQuestion,
            bonusPrediction,
            modelConfig,
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            repredictionIndex,
            null,
            cancellationToken);
    }

    public Task SaveBonusRepredictionWithResolvedContextAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        int repredictionIndex,
        ResolvedBonusContextManifest resolvedContextManifest,
        CancellationToken cancellationToken = default)
    {
        return SaveBonusRepredictionInternalAsync(
            bonusQuestion,
            bonusPrediction,
            modelConfig,
            tokenUsage,
            cost,
            communityContext,
            contextDocumentNames,
            repredictionIndex,
            resolvedContextManifest,
            cancellationToken);
    }

    private async Task SaveBonusRepredictionInternalAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        int repredictionIndex,
        ResolvedBonusContextManifest? resolvedContextManifest,
        CancellationToken cancellationToken)
    {
        try
        {
            var documentNames = contextDocumentNames?.ToArray()
                ?? throw new ArgumentNullException(nameof(contextDocumentNames));
            ValidateResolvedBonusContextManifestForWrite(
                communityContext,
                documentNames,
                resolvedContextManifest);
            var now = Timestamp.GetCurrentTimestamp();

            // Create new document for this reprediction
            var documentId = Guid.NewGuid().ToString();
            var docRef = _firestoreDb.Collection(_bonusPredictionsCollection).Document(documentId);

            _logger.LogDebug("Creating bonus reprediction for question '{QuestionText}' (document: {DocumentId}, reprediction index: {RepredictionIndex})",
                bonusQuestion.Text, documentId, repredictionIndex);

            // Extract selected option texts for observability
            var optionTextsLookup = bonusQuestion.Options.ToDictionary(o => o.Id, o => o.Text);
            var selectedOptionTexts = bonusPrediction.SelectedOptionIds
                .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
                .ToArray();

            var firestoreBonusPrediction = new FirestoreBonusPrediction
            {
                Id = docRef.Id,
                QuestionText = bonusQuestion.Text,
                SelectedOptionIds = bonusPrediction.SelectedOptionIds.ToArray(),
                SelectedOptionTexts = selectedOptionTexts,
                CreatedAt = now,
                UpdatedAt = now,
                Competition = _competition,
                Model = modelConfig.Model,
                ModelConfigKey = modelConfig.IdentityKey,
                ReasoningEffort = modelConfig.ReasoningEffort,
                MaxOutputTokenCount = modelConfig.MaxOutputTokenCount,
                PromptName = modelConfig.PromptName,
                PromptVersion = modelConfig.PromptVersion,
                TokenUsage = tokenUsage,
                Cost = cost,
                CommunityContext = communityContext,
                ContextDocumentNames = documentNames,
                ResolvedBonusContextManifest = resolvedContextManifest is null
                    ? null
                    : SerializeResolvedBonusContextManifest(resolvedContextManifest),
                BonusQuestionCompatibilityManifest = SerializeBonusQuestionCompatibilityManifest(
                    BonusQuestionCompatibilityManifest.Create(bonusQuestion)),
                RepredictionIndex = repredictionIndex
            };

            await docRef.SetAsync(firestoreBonusPrediction, cancellationToken: cancellationToken);

            _logger.LogInformation("Saved bonus reprediction for question '{QuestionText}' (reprediction index: {RepredictionIndex})",
                bonusQuestion.Text, repredictionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bonus reprediction for question: {QuestionText}",
                bonusQuestion.Text);
            throw;
        }
    }

    /// <summary>
    /// Get match prediction costs and counts grouped by reprediction index for cost analysis.
    /// Used specifically by the cost command to include all repredictions.
    /// </summary>
    public Task<Dictionary<int, (double cost, int count)>> GetMatchPredictionCostsByRepredictionIndexAsync(
        string model,
        string communityContext,
        List<int>? matchdays = null,
        CancellationToken cancellationToken = default)
    {
        return GetMatchPredictionCostsByRepredictionIndexAsync(
            PredictionModelConfig.Create(model),
            communityContext,
            matchdays,
            cancellationToken);
    }

    public async Task<Dictionary<int, (double cost, int count)>> GetMatchPredictionCostsByRepredictionIndexAsync(
        PredictionModelConfig modelConfig,
        string communityContext,
        List<int>? matchdays = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var costsByIndex = new Dictionary<int, (double cost, int count)>();

            // Query for match predictions with cost data
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);

            // Add matchday filter if specified
            if (matchdays?.Count > 0)
            {
                query = query.WhereIn("matchday", matchdays.Cast<object>().ToArray());
            }

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var prediction = doc.ConvertTo<FirestoreMatchPrediction>();
                    if (GetConfigMatchKind(prediction, modelConfig) == PredictionConfigMatchKind.None)
                    {
                        continue;
                    }

                    var repredictionIndex = prediction.RepredictionIndex;

                    if (!costsByIndex.ContainsKey(repredictionIndex))
                    {
                        costsByIndex[repredictionIndex] = (0.0, 0);
                    }

                    var (currentCost, currentCount) = costsByIndex[repredictionIndex];
                    costsByIndex[repredictionIndex] = (currentCost + prediction.Cost, currentCount + 1);
                }
            }

            return costsByIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get match prediction costs by reprediction index for model {Model} and community context {CommunityContext}",
                modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    /// <summary>
    /// Get bonus prediction costs and counts grouped by reprediction index for cost analysis.
    /// Used specifically by the cost command to include all repredictions.
    /// </summary>
    public Task<Dictionary<int, (double cost, int count)>> GetBonusPredictionCostsByRepredictionIndexAsync(
        string model,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        return GetBonusPredictionCostsByRepredictionIndexAsync(
            PredictionModelConfig.Create(model),
            communityContext,
            cancellationToken);
    }

    public async Task<Dictionary<int, (double cost, int count)>> GetBonusPredictionCostsByRepredictionIndexAsync(
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var costsByIndex = new Dictionary<int, (double cost, int count)>();

            // Query for bonus predictions with cost data
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var prediction = doc.ConvertTo<FirestoreBonusPrediction>();
                    if (!IsOrdinaryBonusPrediction(prediction)
                        || GetConfigMatchKind(prediction, modelConfig) == PredictionConfigMatchKind.None)
                    {
                        continue;
                    }

                    var repredictionIndex = prediction.RepredictionIndex;

                    if (!costsByIndex.ContainsKey(repredictionIndex))
                    {
                        costsByIndex[repredictionIndex] = (0.0, 0);
                    }

                    var (currentCost, currentCount) = costsByIndex[repredictionIndex];
                    costsByIndex[repredictionIndex] = (currentCost + prediction.Cost, currentCount + 1);
                }
            }

            return costsByIndex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bonus prediction costs by reprediction index for model {Model} and community context {CommunityContext}",
                modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<int>> GetAvailableMatchdaysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var matchdays = new HashSet<int>();

            // Query match predictions for unique matchdays
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition);
            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            foreach (var doc in snapshot.Documents)
            {
                if (doc.TryGetValue<int>("matchday", out var matchday) && matchday > 0)
                {
                    matchdays.Add(matchday);
                }
            }

            return matchdays.OrderBy(m => m).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available matchdays");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = new HashSet<string>();

            // Query match predictions for unique models
            var matchQuery = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition);
            var matchSnapshot = await matchQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in matchSnapshot.Documents)
            {
                if (doc.TryGetValue<string>("model", out var model) && !string.IsNullOrWhiteSpace(model))
                {
                    models.Add(model);
                }
            }

            // Query bonus predictions for unique models
            var bonusQuery = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition);
            var bonusSnapshot = await bonusQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in bonusSnapshot.Documents)
            {
                var prediction = doc.ConvertTo<FirestoreBonusPrediction>();
                if (IsOrdinaryBonusPrediction(prediction) && !string.IsNullOrWhiteSpace(prediction.Model))
                {
                    models.Add(prediction.Model);
                }
            }

            return models.OrderBy(model => model, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available models");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<PredictionModelConfig>> GetAvailableModelConfigsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var modelConfigs = new Dictionary<string, PredictionModelConfig>(StringComparer.Ordinal);

            var matchQuery = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition);
            var matchSnapshot = await matchQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in matchSnapshot.Documents)
            {
                AddModelConfigIfValid(modelConfigs, doc.ConvertTo<FirestoreMatchPrediction>());
            }

            var bonusQuery = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition);
            var bonusSnapshot = await bonusQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in bonusSnapshot.Documents)
            {
                var prediction = doc.ConvertTo<FirestoreBonusPrediction>();
                if (IsOrdinaryBonusPrediction(prediction))
                {
                    AddModelConfigIfValid(modelConfigs, prediction);
                }
            }

            return modelConfigs.Values
                .OrderBy(config => config.Model, StringComparer.Ordinal)
                .ThenBy(config => config.ReasoningEffort is null ? string.Empty : config.ReasoningEffort, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available model configs");
            throw;
        }
    }

    private static void AddModelConfigIfValid(Dictionary<string, PredictionModelConfig> modelConfigs, FirestoreMatchPrediction prediction)
    {
        AddModelConfigIfValid(
            modelConfigs,
            prediction.Model,
            prediction.ReasoningEffort,
            prediction.MaxOutputTokenCount,
            prediction.PromptName,
            prediction.PromptVersion);
    }

    private static void AddModelConfigIfValid(Dictionary<string, PredictionModelConfig> modelConfigs, FirestoreBonusPrediction prediction)
    {
        AddModelConfigIfValid(
            modelConfigs,
            prediction.Model,
            prediction.ReasoningEffort,
            prediction.MaxOutputTokenCount,
            prediction.PromptName,
            prediction.PromptVersion);
    }

    private static void AddModelConfigIfValid(
        Dictionary<string, PredictionModelConfig> modelConfigs,
        string model,
        string? reasoningEffort,
        int? maxOutputTokenCount,
        string? promptName,
        int? promptVersion)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        if (!PredictionModelConfig.IsValidReasoningEffort(reasoningEffort))
        {
            return;
        }

        try
        {
            var modelConfig = PredictionModelConfig.Create(
                model,
                reasoningEffort,
                maxOutputTokenCount,
                promptName,
                promptVersion);
            modelConfigs.TryAdd(modelConfig.IdentityKey, modelConfig);
        }
        catch (ArgumentException)
        {
            // Ignore malformed historical rows when enumerating available filters.
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetAvailableCommunityContextsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var communityContexts = new HashSet<string>();

            // Query match predictions for unique community contexts
            var matchQuery = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("competition", _competition);
            var matchSnapshot = await matchQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in matchSnapshot.Documents)
            {
                if (doc.TryGetValue<string>("communityContext", out var context) && !string.IsNullOrWhiteSpace(context))
                {
                    communityContexts.Add(context);
                }
            }

            // Query bonus predictions for unique community contexts
            var bonusQuery = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition);
            var bonusSnapshot = await bonusQuery.GetSnapshotAsync(cancellationToken);

            foreach (var doc in bonusSnapshot.Documents)
            {
                var prediction = doc.ConvertTo<FirestoreBonusPrediction>();
                if (IsOrdinaryBonusPrediction(prediction) && !string.IsNullOrWhiteSpace(prediction.CommunityContext))
                {
                    communityContexts.Add(prediction.CommunityContext);
                }
            }

            return communityContexts.OrderBy(context => context, StringComparer.Ordinal).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available community contexts");
            throw;
        }
    }

    private Query CreateClCandidateQuery(SchadensfresseChampionsLeagueBonusPredictionScope scope) =>
        _firestoreDb.Collection(_bonusPredictionsCollection)
            .WhereEqualTo("questionText", scope.Question.Text)
            .WhereEqualTo("competition", SchadensfresseChampionsLeagueBonusProfile.Competition)
            .WhereEqualTo("model", scope.ModelConfig.Model)
            .WhereEqualTo("communityContext", SchadensfresseChampionsLeagueBonusProfile.Community);

    private static bool IsOrdinaryBonusPrediction(FirestoreBonusPrediction prediction) =>
        prediction.SchadensfresseChampionsLeagueBonusManifest is null;

    private (DocumentSnapshot Document, FirestoreBonusPrediction Prediction)? SelectCurrentClCandidate(
        IEnumerable<DocumentSnapshot> documents,
        SchadensfresseChampionsLeagueBonusPredictionScope scope)
    {
        var matching = documents
            .Select(document => (Document: document, Prediction: document.ConvertTo<FirestoreBonusPrediction>()))
            .Where(candidate => MatchesCurrentClLineage(candidate.Prediction, scope))
            .ToArray();
        var duplicateIndex = matching.GroupBy(candidate => candidate.Prediction.RepredictionIndex)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateIndex is not null)
        {
            throw new InvalidDataException($"Duplicate exact CL lineage rows exist at reprediction index {duplicateIndex.Key}.");
        }
        return matching.Length == 0
            ? null
            : matching.OrderByDescending(candidate => candidate.Prediction.RepredictionIndex).First();
    }

    private bool MatchesCurrentClLineage(
        FirestoreBonusPrediction candidate,
        SchadensfresseChampionsLeagueBonusPredictionScope scope)
    {
        try
        {
            if (!string.Equals(_competition, SchadensfresseChampionsLeagueBonusProfile.Competition, StringComparison.Ordinal)
                || !string.Equals(candidate.Competition, SchadensfresseChampionsLeagueBonusProfile.Competition, StringComparison.Ordinal)
                || !string.Equals(candidate.CommunityContext, SchadensfresseChampionsLeagueBonusProfile.Community, StringComparison.Ordinal)
                || !string.Equals(candidate.QuestionId, scope.SeedQuestion.KicktippQuestionId, StringComparison.Ordinal)
                || !string.Equals(candidate.QuestionText, scope.Question.Text, StringComparison.Ordinal)
                || !string.Equals(candidate.QuestionDeadline, SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc, StringComparison.Ordinal)
                || !string.Equals(candidate.Model, scope.ModelConfig.Model, StringComparison.Ordinal)
                || !string.Equals(candidate.ReasoningEffort, scope.ModelConfig.ReasoningEffort, StringComparison.Ordinal)
                || candidate.MaxOutputTokenCount != scope.ModelConfig.MaxOutputTokenCount
                || !string.Equals(candidate.PromptName, scope.ModelConfig.PromptName, StringComparison.Ordinal)
                || candidate.PromptVersion != scope.ModelConfig.PromptVersion
                || !string.Equals(candidate.ModelConfigKey, scope.ModelConfig.IdentityKey, StringComparison.Ordinal)
                || candidate.RepredictionIndex < 0
                || candidate.ContextDocumentNames is null || candidate.ContextDocumentNames.Length != 0
                || candidate.ResolvedBonusContextManifest is not null
                || candidate.SchadensfresseChampionsLeagueBonusManifest is null
                || string.IsNullOrWhiteSpace(candidate.BonusQuestionCompatibilityManifest)
                || candidate.SelectedOptionIds is null || candidate.SelectedOptionTexts is null
                || string.IsNullOrWhiteSpace(candidate.TokenUsage)
                || double.IsNaN(candidate.Cost) || double.IsInfinity(candidate.Cost) || candidate.Cost < 0)
            {
                return false;
            }

            SchadensfresseChampionsLeagueBonusProfile.ValidatePrediction(
                scope.Question,
                new BonusPrediction(candidate.SelectedOptionIds.ToList()));
            var optionTexts = scope.Question.Options.ToDictionary(option => option.Id, option => option.Text, StringComparer.Ordinal);
            if (!candidate.SelectedOptionTexts.SequenceEqual(candidate.SelectedOptionIds.Select(id => optionTexts[id]), StringComparer.Ordinal))
            {
                return false;
            }

            var compatibility = DeserializeBonusQuestionCompatibilityManifest(candidate.BonusQuestionCompatibilityManifest, tolerateInvalid: false);
            var expectedCompatibility = BonusQuestionCompatibilityManifest.Create(scope.Question);
            if (compatibility is null
                || !string.Equals(
                    SerializeBonusQuestionCompatibilityManifest(compatibility),
                    SerializeBonusQuestionCompatibilityManifest(expectedCompatibility),
                    StringComparison.Ordinal))
            {
                return false;
            }

            var manifest = DeserializeClManifest(candidate.SchadensfresseChampionsLeagueBonusManifest);
            manifest.Validate(scope);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException or KeyNotFoundException)
        {
            return false;
        }
    }

    private void ValidateClWrite(
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        BonusPrediction prediction,
        string promptProvider,
        string tokenUsage,
        double cost)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(prediction);
        EnsureClRepositoryScope();
        SchadensfresseChampionsLeagueBonusProfile.ValidatePrediction(scope.Question, prediction);
        var manifest = SchadensfresseChampionsLeagueBonusManifest.Create(scope, promptProvider);
        manifest.Validate(scope);
        if (string.IsNullOrWhiteSpace(tokenUsage) || double.IsNaN(cost) || double.IsInfinity(cost) || cost < 0)
        {
            throw new InvalidDataException("CL token usage and cost must be valid persisted metadata.");
        }
    }

    private void EnsureClRepositoryScope()
    {
        if (!string.Equals(_competition, SchadensfresseChampionsLeagueBonusProfile.Competition, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The specialized CL bonus repository is available only in bundesliga-2026-27.");
        }
    }

    private static FirestoreBonusPrediction CreateClFirestorePrediction(
        string documentId,
        SchadensfresseChampionsLeagueBonusPredictionScope scope,
        BonusPrediction prediction,
        string promptProvider,
        string tokenUsage,
        double cost,
        int repredictionIndex,
        Timestamp createdAt,
        Timestamp updatedAt)
    {
        var optionTexts = scope.Question.Options.ToDictionary(option => option.Id, option => option.Text, StringComparer.Ordinal);
        return new FirestoreBonusPrediction
        {
            Id = documentId,
            QuestionId = scope.SeedQuestion.KicktippQuestionId,
            QuestionText = scope.Question.Text,
            QuestionDeadline = SchadensfresseChampionsLeagueBonusProfile.DeadlineUtc,
            SelectedOptionIds = prediction.SelectedOptionIds.ToArray(),
            SelectedOptionTexts = prediction.SelectedOptionIds.Select(id => optionTexts[id]).ToArray(),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Competition = SchadensfresseChampionsLeagueBonusProfile.Competition,
            Model = scope.ModelConfig.Model,
            ModelConfigKey = scope.ModelConfig.IdentityKey,
            ReasoningEffort = scope.ModelConfig.ReasoningEffort,
            MaxOutputTokenCount = scope.ModelConfig.MaxOutputTokenCount,
            PromptName = scope.ModelConfig.PromptName,
            PromptVersion = scope.ModelConfig.PromptVersion,
            TokenUsage = tokenUsage,
            Cost = cost,
            CommunityContext = SchadensfresseChampionsLeagueBonusProfile.Community,
            ContextDocumentNames = [],
            ResolvedBonusContextManifest = null,
            SchadensfresseChampionsLeagueBonusManifest = SerializeClManifest(
                SchadensfresseChampionsLeagueBonusManifest.Create(scope, promptProvider)),
            BonusQuestionCompatibilityManifest = SerializeBonusQuestionCompatibilityManifest(
                BonusQuestionCompatibilityManifest.Create(scope.Question)),
            RepredictionIndex = repredictionIndex
        };
    }

    private BonusPredictionMetadata CreateClMetadata(
        FirestoreBonusPrediction prediction,
        SchadensfresseChampionsLeagueBonusPredictionScope scope)
    {
        var manifest = DeserializeClManifest(prediction.SchadensfresseChampionsLeagueBonusManifest);
        manifest.Validate(scope);
        return new BonusPredictionMetadata(
            new BonusPrediction(prediction.SelectedOptionIds.ToList()),
            prediction.CreatedAt.ToDateTimeOffset(),
            [],
            null,
            DeserializeBonusQuestionCompatibilityManifest(prediction.BonusQuestionCompatibilityManifest, tolerateInvalid: false),
            prediction.Id,
            manifest);
    }

    private static readonly string[] ClManifestProperties =
    [
        "schemaVersion", "profileId", "competition", "communityContext", "kicktippQuestionId", "deadline",
        "questionSetSha256", "questionDefinitionSha256", "sourceSnapshotSha256", "historicalEvidenceQuestionSetSha256",
        "promptName", "promptVersion", "promptLabel", "promptNormalizedSha256", "promptProvider", "model",
        "reasoningEffort", "maxOutputTokens", "modelConfigKey", "servicePolicyId", "documents"
    ];

    private static string SerializeClManifest(SchadensfresseChampionsLeagueBonusManifest manifest) =>
        JsonSerializer.Serialize(manifest, ResolvedContextManifestSerializerOptions);

    private static SchadensfresseChampionsLeagueBonusManifest DeserializeClManifest(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized)) throw new InvalidDataException("The specialized CL manifest is absent.");
        using var json = JsonDocument.Parse(serialized);
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.EnumerateObject().Select(property => property.Name).SequenceEqual(ClManifestProperties, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The specialized CL manifest has unknown, missing, duplicate, or out-of-order fields.");
        }
        var manifest = JsonSerializer.Deserialize<SchadensfresseChampionsLeagueBonusManifest>(serialized, ResolvedContextManifestSerializerOptions)
            ?? throw new InvalidDataException("The specialized CL manifest cannot be null.");
        if (!string.Equals(SerializeClManifest(manifest), serialized, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The specialized CL manifest is not canonical JSON.");
        }
        return manifest;
    }

    private static string SerializeResolvedBonusContextManifest(ResolvedBonusContextManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, ResolvedContextManifestSerializerOptions);
    }

    private BonusPredictionMetadata CreateBonusPredictionMetadata(
        FirestoreBonusPrediction firestoreBonusPrediction,
        string communityContext,
        bool tolerateInvalidCompatibilityManifest = false)
    {
        var bonusPrediction = new BonusPrediction(firestoreBonusPrediction.SelectedOptionIds.ToList());
        var createdAt = firestoreBonusPrediction.CreatedAt.ToDateTimeOffset();
        var contextDocumentNames = firestoreBonusPrediction.ContextDocumentNames?.ToList() ?? new List<string>();
        var resolvedContextManifest = DeserializeResolvedBonusContextManifest(
            firestoreBonusPrediction.ResolvedBonusContextManifest);
        if (resolvedContextManifest is not null)
        {
            ValidateResolvedBonusContextManifest(
                communityContext,
                contextDocumentNames,
                resolvedContextManifest);
        }

        var compatibilityManifest = DeserializeBonusQuestionCompatibilityManifest(
            firestoreBonusPrediction.BonusQuestionCompatibilityManifest,
            tolerateInvalidCompatibilityManifest);
        return new BonusPredictionMetadata(
            bonusPrediction,
            createdAt,
            contextDocumentNames,
            resolvedContextManifest,
            compatibilityManifest,
            firestoreBonusPrediction.Id);
    }

    private static string SerializeBonusQuestionCompatibilityManifest(
        BonusQuestionCompatibilityManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        return JsonSerializer.Serialize(manifest, ResolvedContextManifestSerializerOptions);
    }

    private static BonusQuestionCompatibilityManifest? DeserializeBonusQuestionCompatibilityManifest(
        string? serialized,
        bool tolerateInvalid)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BonusQuestionCompatibilityManifest>(
                               serialized,
                               ResolvedContextManifestSerializerOptions)
                           ?? throw new InvalidDataException(
                               "Stored bonus-question compatibility manifest cannot be null.");
            manifest.Validate();
            var canonical = SerializeBonusQuestionCompatibilityManifest(manifest);
            if (!string.Equals(canonical, serialized, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Stored bonus-question compatibility manifest is not canonical JSON.");
            }

            return manifest;
        }
        catch (Exception exception) when (
            tolerateInvalid
            && exception is JsonException or ArgumentException or InvalidDataException)
        {
            return null;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException(
                "Stored bonus-question compatibility manifest is invalid.",
                exception);
        }
    }

    private void ValidateResolvedBonusContextManifestForWrite(
        string communityContext,
        IReadOnlyList<string> contextDocumentNames,
        ResolvedBonusContextManifest? manifest)
    {
        if (manifest is null)
        {
            if (string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "New Bundesliga 2026/27 bonus prediction writes require an immutable resolved bonus-context manifest.");
            }

            return;
        }

        ValidateResolvedBonusContextManifest(communityContext, contextDocumentNames, manifest);
    }

    private void ValidateResolvedBonusContextManifest(
        string communityContext,
        IReadOnlyList<string> contextDocumentNames,
        ResolvedBonusContextManifest manifest)
    {
        if (!string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Resolved Bundesliga bonus-context manifests cannot be persisted outside the canonical Bundesliga competition scope.");
        }

        ResolvedBonusContextManifest.ValidateForCommunity(manifest, communityContext);
        if (!contextDocumentNames.SequenceEqual(
                manifest.Documents.Select(document => document.Name),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Bonus prediction context-document names do not match the immutable resolved bonus-context manifest.");
        }
    }

    private static ResolvedBonusContextManifest? DeserializeResolvedBonusContextManifest(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<ResolvedBonusContextManifest>(
                               serialized,
                               ResolvedContextManifestSerializerOptions)
                           ?? throw new InvalidDataException(
                               "Stored resolved bonus-context manifest cannot be null.");
            var canonical = SerializeResolvedBonusContextManifest(manifest);
            if (!string.Equals(canonical, serialized, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Stored resolved bonus-context manifest is not canonical JSON.");
            }

            return manifest;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException(
                "Stored resolved bonus-context manifest is invalid.",
                exception);
        }
    }

    private static string SerializeResolvedContextManifest(ResolvedMatchContextManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.Competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
            || manifest.Documents.Length != 11)
        {
            throw new ArgumentException("Only a complete Bundesliga resolved-context manifest may be persisted.", nameof(manifest));
        }

        return JsonSerializer.Serialize(manifest, ResolvedContextManifestSerializerOptions);
    }

    private void ValidateResolvedContextManifest(
        Match match,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedMatchContextManifest? manifest)
    {
        if (manifest is null)
        {
            if (string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "New Bundesliga 2026/27 prediction writes require an immutable resolved-context manifest.");
            }

            return;
        }

        if (!string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved Bundesliga context manifests cannot be persisted outside the canonical Bundesliga competition scope.");
        }

        ResolvedMatchContextManifest.ValidateForMatch(manifest, match, communityContext);
        var names = contextDocumentNames?.ToArray() ?? throw new ArgumentNullException(nameof(contextDocumentNames));
        if (!names.SequenceEqual(manifest.Documents.Select(document => document.Name), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Prediction context-document names do not match the immutable resolved-context manifest.");
        }
    }

    private string BuildBundesligaRepredictionDocumentId(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        int repredictionIndex)
    {
        var identity = string.Join("\n", new[]
        {
            _competition,
            match.HomeTeam,
            match.AwayTeam,
            match.IsCancelled ? "cancelled" : ConvertToTimestamp(match.StartsAt).ToString(),
            modelConfig.IdentityKey,
            communityContext,
            repredictionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return $"bundesliga-reprediction-{DocumentPublicationContract.ComputeContentSha256(identity)}";
    }

    private static ResolvedMatchContextManifest? DeserializeResolvedContextManifest(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(serialized);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Stored resolved-context manifest must be an object.");
            }
            var properties = json.RootElement.EnumerateObject().ToArray();
            var expectedRootNames = new[]
            {
                "competition", "communityContext", "documents", "rosterPublicationSnapshotId", "clubEloPublicationSnapshotId"
            };
            if (!properties.Select(property => property.Name).SequenceEqual(expectedRootNames, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Stored resolved-context manifest has an unknown, missing, duplicate, or noncanonical field.");
            }

            var documents = properties[2].Value.ValueKind == JsonValueKind.Array
                ? properties[2].Value.EnumerateArray().Select(ParseResolvedContextDocument).ToArray()
                : throw new InvalidDataException("Stored resolved-context manifest documents must be an array.");
            return ResolvedMatchContextManifest.Create(
                GetRequiredString(properties[0].Value, "competition"),
                GetRequiredString(properties[1].Value, "communityContext"),
                documents,
                GetRequiredString(properties[3].Value, "rosterPublicationSnapshotId"),
                GetRequiredString(properties[4].Value, "clubEloPublicationSnapshotId"));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Stored resolved-context manifest is invalid.", exception);
        }
    }

    private static ResolvedMatchContextDocument ParseResolvedContextDocument(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Stored resolved-context manifest document must be an object.");
        }
        var properties = element.EnumerateObject().ToArray();
        var expectedNames = new[] { "name", "version", "kind", "contentSha256" };
        if (!properties.Select(property => property.Name).SequenceEqual(expectedNames, StringComparer.Ordinal)
            || properties[1].Value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException("Stored resolved-context manifest document has an unknown, missing, duplicate, or noncanonical field.");
        }
        return new ResolvedMatchContextDocument(
            GetRequiredString(properties[0].Value, "name"),
            properties[1].Value.GetInt32(),
            GetRequiredString(properties[2].Value, "kind"),
            GetRequiredString(properties[3].Value, "contentSha256"));
    }

    private static string GetRequiredString(JsonElement element, string fieldName) =>
        element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!
            : throw new InvalidDataException($"Stored resolved-context manifest field '{fieldName}' must be a nonempty string.");

    private string? SerializeJustification(PredictionJustification? justification)
    {
        if (justification == null)
        {
            return null;
        }

        if (!HasJustificationContent(justification))
        {
            return null;
        }

        var stored = new StoredJustification
        {
            KeyReasoning = justification.KeyReasoning?.Trim() ?? string.Empty,
            ContextSources = new StoredContextSources
            {
                MostValuable = justification.ContextSources?.MostValuable?
                    .Where(entry => entry != null)
                    .Select(ToStoredContextSource)
                    .ToList() ?? new List<StoredContextSource>(),
                LeastValuable = justification.ContextSources?.LeastValuable?
                    .Where(entry => entry != null)
                    .Select(ToStoredContextSource)
                    .ToList() ?? new List<StoredContextSource>()
            },
            Uncertainties = justification.Uncertainties?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList() ?? new List<string>()
        };

        return JsonSerializer.Serialize(stored, JustificationSerializerOptions);
    }

    private static bool HasJustificationContent(PredictionJustification justification)
    {
        if (!string.IsNullOrWhiteSpace(justification.KeyReasoning))
        {
            return true;
        }

        if (justification.ContextSources?.MostValuable != null &&
            justification.ContextSources.MostValuable.Any(HasSourceContent))
        {
            return true;
        }

        if (justification.ContextSources?.LeastValuable != null &&
            justification.ContextSources.LeastValuable.Any(HasSourceContent))
        {
            return true;
        }

        return justification.Uncertainties != null &&
               justification.Uncertainties.Any(item => !string.IsNullOrWhiteSpace(item));
    }

    private static bool HasSourceContent(PredictionJustificationContextSource source)
    {
        return !string.IsNullOrWhiteSpace(source?.DocumentName) ||
               !string.IsNullOrWhiteSpace(source?.Details);
    }

    private PredictionJustification? DeserializeJustification(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        var trimmed = serialized.Trim();

        if (!trimmed.StartsWith("{"))
        {
            return new PredictionJustification(
                trimmed,
                new PredictionJustificationContextSources(
                    Array.Empty<PredictionJustificationContextSource>(),
                    Array.Empty<PredictionJustificationContextSource>()),
                Array.Empty<string>());
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredJustification>(trimmed, JustificationSerializerOptions);

            if (stored == null)
            {
                return null;
            }

            var contextSources = stored.ContextSources ?? new StoredContextSources();

            var mostValuable = contextSources.MostValuable?
                .Where(entry => entry != null)
                .Select(ToDomainContextSource)
                .ToList() ?? new List<PredictionJustificationContextSource>();

            var leastValuable = contextSources.LeastValuable?
                .Where(entry => entry != null)
                .Select(ToDomainContextSource)
                .ToList() ?? new List<PredictionJustificationContextSource>();

            var uncertainties = stored.Uncertainties?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList() ?? new List<string>();

            var justification = new PredictionJustification(
                stored.KeyReasoning?.Trim() ?? string.Empty,
                new PredictionJustificationContextSources(mostValuable, leastValuable),
                uncertainties);

            return HasJustificationContent(justification) ? justification : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse structured justification JSON; falling back to legacy text format");

            var fallbackJustification = new PredictionJustification(
                trimmed,
                new PredictionJustificationContextSources(
                    Array.Empty<PredictionJustificationContextSource>(),
                    Array.Empty<PredictionJustificationContextSource>()),
                Array.Empty<string>());

            return HasJustificationContent(fallbackJustification) ? fallbackJustification : null;
        }
    }

    private static StoredContextSource ToStoredContextSource(PredictionJustificationContextSource source)
    {
        return new StoredContextSource
        {
            DocumentName = source.DocumentName?.Trim() ?? string.Empty,
            Details = source.Details?.Trim() ?? string.Empty
        };
    }

    private static PredictionJustificationContextSource ToDomainContextSource(StoredContextSource source)
    {
        var documentName = source.DocumentName?.Trim() ?? string.Empty;
        var details = source.Details?.Trim() ?? string.Empty;
        return new PredictionJustificationContextSource(documentName, details);
    }

    private sealed class StoredJustification
    {
        public string? KeyReasoning { get; set; }
        public StoredContextSources? ContextSources { get; set; }
        public List<string>? Uncertainties { get; set; }
    }

    private sealed class StoredContextSources
    {
        public List<StoredContextSource>? MostValuable { get; set; }
        public List<StoredContextSource>? LeastValuable { get; set; }
    }

    private sealed class StoredContextSource
    {
        public string? DocumentName { get; set; }
        public string? Details { get; set; }
    }
}
