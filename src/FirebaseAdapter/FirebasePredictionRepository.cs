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
    IBundesligaSeasonTypedBonusPredictionRepository,
    IBundesligaSeasonTypedCancelledMatchPredictionRepository
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

    private FirestoreMatchPrediction? SelectLatestTypedAwareForModelConfig(
        IEnumerable<FirestoreMatchPrediction> predictions,
        Match match,
        PredictionModelConfig modelConfig)
    {
        var candidates = predictions.ToArray();
        if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedMatch(match))
        {
            EnsureOneTypedMatchRowPerIndex(
                candidates
                    .Where(prediction => GetConfigMatchKind(prediction, modelConfig) == PredictionConfigMatchKind.Exact)
                    .ToArray(),
                match);
        }

        return SelectLatestForModelConfig(candidates, modelConfig);
    }

    private FirestoreBonusPrediction? SelectLatestTypedAwareForModelConfig(
        IEnumerable<FirestoreBonusPrediction> predictions,
        BonusQuestion question,
        PredictionModelConfig modelConfig)
    {
        var candidates = predictions.ToArray();
        if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(question))
        {
            EnsureOneTypedBonusRowPerIndex(
                candidates
                    .Where(prediction => GetConfigMatchKind(prediction, modelConfig) == PredictionConfigMatchKind.Exact)
                    .ToArray(),
                question,
                BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(question));
        }

        return SelectLatestForModelConfig(candidates, modelConfig);
    }

    private bool IsBundesliga2026_27 => string.Equals(
        _competition,
        CompetitionIds.Bundesliga2026_27,
        StringComparison.Ordinal);

    private void ValidateCurrentMatchIdentity(Match match) =>
        BundesligaSeasonStorageIdentity.ValidateMatch(_competition, match);

    private bool CanLookupCurrentMatchIdentity(Match match)
    {
        if (!IsBundesliga2026_27)
        {
            ValidateCurrentMatchIdentity(match);
            return true;
        }

        if (!BundesligaSeasonStorageIdentity.IsTypedMatch(match))
        {
            return true;
        }

        ValidateCurrentMatchIdentity(match);
        return true;
    }

    private void ValidateCurrentBonusIdentity(BonusQuestion question) =>
        BundesligaSeasonStorageIdentity.ValidateBonusQuestion(_competition, question);

    private void ValidateTypedContextAvailability(Match match)
    {
        if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedMatch(match)
            && match.BundesligaSeasonSubcompetition != BundesligaSeasonSubcompetition.Bundesliga)
        {
            throw new InvalidOperationException(
                "Typed DFB-Pokal and Champions League match rows require resolvedTypedContextManifest support before they can be current or persisted.");
        }
    }

    private void ValidateTypedContextAvailability(BonusQuestion question)
    {
        if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(question)
            && question.BundesligaSeasonSubcompetition != BundesligaSeasonSubcompetition.Bundesliga)
        {
            throw new InvalidOperationException(
                "Typed DFB-Pokal and Champions League bonus rows require resolvedTypedContextManifest support before they can be current or persisted.");
        }
    }

    private Query AddCurrentMatchIdentityFilters(Query query, Match match)
    {
        if (!IsBundesliga2026_27 || !BundesligaSeasonStorageIdentity.IsTypedMatch(match))
        {
            return query;
        }

        return query
            .WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId)
            .WhereEqualTo("kicktippRoundName", match.KicktippRoundName)
            .WhereEqualTo("resultBasis", match.ResultBasis!.Value.ToSerializedValue())
            .WhereEqualTo("bundesligaSeasonSubcompetition", match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue());
    }

    private Query AddCurrentBonusIdentityFilters(Query query, BonusQuestion question)
    {
        if (!IsBundesliga2026_27)
        {
            return query;
        }

        return query
            .WhereEqualTo("kicktippQuestionId", question.KicktippQuestionId)
            .WhereEqualTo("bundesligaSeasonSubcompetition", question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue());
    }

    private bool MatchesCurrentMatchIdentity(FirestoreMatchPrediction stored, Match requested)
    {
        if (!IsBundesliga2026_27)
        {
            return true;
        }

        if (!BundesligaSeasonStorageIdentity.IsTypedMatch(requested))
        {
            return IsLegacyBundesligaMatchRow(stored);
        }

        return string.Equals(stored.KicktippFixtureId, requested.KicktippFixtureId, StringComparison.Ordinal)
            && string.Equals(stored.KicktippRoundName, requested.KicktippRoundName, StringComparison.Ordinal)
            && string.Equals(stored.ResultBasis, requested.ResultBasis!.Value.ToSerializedValue(), StringComparison.Ordinal)
            && string.Equals(stored.BundesligaSeasonSubcompetition,
                requested.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(), StringComparison.Ordinal);
    }

    private bool IsLegacyBundesligaMatchRow(FirestoreMatchPrediction stored) =>
        !IsBundesliga2026_27 ||
        string.IsNullOrWhiteSpace(stored.KicktippFixtureId)
        && string.IsNullOrWhiteSpace(stored.KicktippRoundName)
        && string.IsNullOrWhiteSpace(stored.ResultBasis)
        && string.IsNullOrWhiteSpace(stored.BundesligaSeasonSubcompetition);

    private bool MatchesCurrentBonusIdentity(FirestoreBonusPrediction stored, BonusQuestion requested) =>
        !IsBundesliga2026_27 ||
        string.Equals(stored.KicktippQuestionId, requested.KicktippQuestionId, StringComparison.Ordinal)
        && string.Equals(stored.BundesligaSeasonSubcompetition,
            requested.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(), StringComparison.Ordinal)
        && string.Equals(stored.QuestionText, requested.Text, StringComparison.Ordinal)
        && string.Equals(stored.BundesligaSeasonBonusIdentitySha256,
            BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(requested),
            StringComparison.Ordinal);

    private bool IsLegacyBundesligaBonusRow(FirestoreBonusPrediction stored) =>
        !IsBundesliga2026_27 ||
        string.IsNullOrWhiteSpace(stored.KicktippQuestionId)
        && string.IsNullOrWhiteSpace(stored.BundesligaSeasonSubcompetition)
        && string.IsNullOrWhiteSpace(stored.BundesligaSeasonBonusIdentitySha256);

    private bool HasCompleteTypedMatchProvenance(
        FirestoreMatchPrediction stored,
        Match match,
        string communityContext)
    {
        if (!IsBundesliga2026_27 || !BundesligaSeasonStorageIdentity.IsTypedMatch(match))
        {
            return true;
        }

        try
        {
            var manifest = DeserializeResolvedContextManifest(stored.ResolvedContextManifest);
            if (manifest is null)
            {
                return false;
            }

            ValidateResolvedContextManifest(match, communityContext, stored.ContextDocumentNames ?? [], manifest);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool HasCompleteTypedBonusProvenance(
        FirestoreBonusPrediction stored,
        BonusQuestion question,
        string communityContext)
    {
        try
        {
            if (!IsBundesliga2026_27 || !BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(question))
            {
                return true;
            }

            var manifest = DeserializeResolvedBonusContextManifest(stored.ResolvedBonusContextManifest);
            if (manifest is null)
            {
                return false;
            }

            ValidateResolvedBonusContextManifestForWrite(
                communityContext, stored.ContextDocumentNames ?? [], manifest);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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
            ValidateCurrentMatchIdentity(match);
            ValidateTypedContextAvailability(match);
            ValidateResolvedContextManifest(match, communityContext, contextDocumentNames, resolvedContextManifest);
            if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedMatch(match))
            {
                await SaveTypedInitialMatchPredictionAsync(
                    match, prediction, modelConfig, tokenUsage, cost, communityContext,
                    contextDocumentNames, resolvedContextManifest!, overrideCreatedAt, cancellationToken);
                return;
            }

            var now = Timestamp.GetCurrentTimestamp();

            // Check if a prediction already exists for this match, model, and community context
            // Order by repredictionIndex descending to get the latest version for updating
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentMatchIdentityFilters(query, match).OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            int repredictionIndex = 0;

            var existingDoc = snapshot.Documents
                .FirstOrDefault(document =>
                {
                    var stored = document.ConvertTo<FirestoreMatchPrediction>();
                    return MatchesCurrentMatchIdentity(stored, match)
                        && GetConfigMatchKind(stored, modelConfig) == PredictionConfigMatchKind.Exact;
                });

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
                KicktippFixtureId = match.KicktippFixtureId,
                KicktippRoundName = match.KicktippRoundName,
                ResultBasis = match.ResultBasis?.ToSerializedValue(),
                BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition?.ToSerializedValue(),
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
        var metadata = await GetPredictionMetadataAsync(match, modelConfig, communityContext, cancellationToken);
        return metadata?.Prediction;
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(IsLegacyBundesligaMatchRow),
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
                .OrderByDescending("startsAt");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = snapshot.Documents
                .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                .Where(IsLegacyBundesligaMatchRow)
                .OrderByDescending(prediction => prediction.StartsAt.ToDateTimeOffset())
                .ThenBy(prediction => prediction.Id, StringComparer.Ordinal)
                .FirstOrDefault();

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
            if (!CanLookupCurrentMatchIdentity(match))
            {
                return null;
            }
            ValidateTypedContextAvailability(match);
            // Query by match characteristics, model, community context, and competition.
            // Order by repredictionIndex descending to keep metadata reads aligned with latest prediction retrieval.
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentMatchIdentityFilters(query, match).OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestTypedAwareForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
                    .Where(prediction => HasCompleteTypedMatchProvenance(prediction, match, communityContext)),
                match,
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
            if (!CanLookupCurrentMatchIdentity(match))
            {
                return false;
            }
            ValidateTypedContextAvailability(match);
            // Query by match characteristics, model, and community context instead of using deterministic ID
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentMatchIdentityFilters(query, match);

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var candidates = snapshot.Documents
                .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
                .Where(prediction => HasCompleteTypedMatchProvenance(prediction, match, communityContext));
            return SelectLatestTypedAwareForModelConfig(candidates, match, modelConfig) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if prediction exists for match {HomeTeam} vs {AwayTeam} using model {Model} and community context {CommunityContext}",
                match.HomeTeam, match.AwayTeam, modelConfig.DisplayName, communityContext);
            throw;
        }
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
            ValidateCurrentBonusIdentity(bonusQuestion);
            ValidateTypedContextAvailability(bonusQuestion);
            var documentNames = contextDocumentNames?.ToArray()
                ?? throw new ArgumentNullException(nameof(contextDocumentNames));
            ValidateResolvedBonusContextManifestForWrite(
                communityContext,
                documentNames,
                resolvedContextManifest);
            if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(bonusQuestion))
            {
                await SaveTypedInitialBonusPredictionAsync(
                    bonusQuestion, bonusPrediction, modelConfig, tokenUsage, cost, communityContext,
                    documentNames, resolvedContextManifest!, overrideCreatedAt, cancellationToken);
                return;
            }

            var now = Timestamp.GetCurrentTimestamp();

            // Check if a prediction already exists for this question, model, and community context
            // Order by repredictionIndex descending to get the latest version for updating
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("questionText", bonusQuestion.Text)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentBonusIdentityFilters(query, bonusQuestion).OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);

            DocumentReference docRef;
            bool isUpdate = false;
            Timestamp? existingCreatedAt = null;
            int repredictionIndex = 0;

            var existingDoc = snapshot.Documents
                .FirstOrDefault(document =>
                {
                    var stored = document.ConvertTo<FirestoreBonusPrediction>();
                    return MatchesCurrentBonusIdentity(stored, bonusQuestion)
                        && GetConfigMatchKind(stored, modelConfig) == PredictionConfigMatchKind.Exact;
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
                KicktippQuestionId = bonusQuestion.KicktippQuestionId,
                BundesligaSeasonSubcompetition = bonusQuestion.BundesligaSeasonSubcompetition?.ToSerializedValue(),
                BundesligaSeasonBonusIdentitySha256 = IsBundesliga2026_27
                    ? BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(bonusQuestion)
                    : null,
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(IsLegacyBundesligaBonusRow),
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(IsLegacyBundesligaBonusRow),
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(IsLegacyBundesligaBonusRow),
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

    private async Task SaveTypedInitialBonusPredictionAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IReadOnlyList<string> contextDocumentNames,
        ResolvedBonusContextManifest resolvedContextManifest,
        bool overrideCreatedAt,
        CancellationToken cancellationToken)
    {
        var identitySha256 = BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(bonusQuestion);
        var serializedManifest = SerializeResolvedBonusContextManifest(resolvedContextManifest);
        var compatibilityManifest = SerializeBonusQuestionCompatibilityManifest(
            BonusQuestionCompatibilityManifest.Create(bonusQuestion));
        var optionTextsLookup = bonusQuestion.Options.ToDictionary(option => option.Id, option => option.Text);
        var selectedOptionTexts = bonusPrediction.SelectedOptionIds
            .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
            .ToArray();

        var savedIndex = await _firestoreDb.RunTransactionAsync(async transaction =>
        {
            var candidates = await transaction.GetSnapshotAsync(BuildTypedBonusSemanticQuery(
                bonusQuestion, modelConfig, communityContext));
            var exactConfigRows = candidates.Documents
                .Select(document => (Document: document, Row: document.ConvertTo<FirestoreBonusPrediction>()))
                .Where(candidate => GetConfigMatchKind(candidate.Row, modelConfig) == PredictionConfigMatchKind.Exact)
                .ToArray();
            EnsureOneTypedBonusRowPerIndex(
                exactConfigRows.Select(candidate => candidate.Row).ToArray(), bonusQuestion, identitySha256);

            var current = exactConfigRows
                .OrderByDescending(candidate => candidate.Row.RepredictionIndex)
                .ThenByDescending(candidate => candidate.Row.CreatedAt.ToDateTimeOffset())
                .ThenBy(candidate => candidate.Row.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            var repredictionIndex = current.Row?.RepredictionIndex ?? 0;
            var docRef = current.Document?.Reference
                ?? _firestoreDb.Collection(_bonusPredictionsCollection).Document(
                    BuildTypedBonusDocumentId(bonusQuestion, modelConfig, communityContext, repredictionIndex));
            var deterministicSnapshot = current.Document is null
                ? await transaction.GetSnapshotAsync(docRef)
                : null;
            if (deterministicSnapshot?.Exists == true)
            {
                throw new InvalidOperationException(
                    "Typed Bundesliga bonus storage identity collides with an existing document outside its semantic scope.");
            }

            var now = Timestamp.GetCurrentTimestamp();
            var stored = new FirestoreBonusPrediction
            {
                Id = docRef.Id,
                QuestionText = bonusQuestion.Text,
                KicktippQuestionId = bonusQuestion.KicktippQuestionId,
                BundesligaSeasonSubcompetition = bonusQuestion.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                BundesligaSeasonBonusIdentitySha256 = identitySha256,
                SelectedOptionIds = bonusPrediction.SelectedOptionIds.ToArray(),
                SelectedOptionTexts = selectedOptionTexts,
                CreatedAt = overrideCreatedAt || current.Row is null ? now : current.Row.CreatedAt,
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
                ResolvedBonusContextManifest = serializedManifest,
                BonusQuestionCompatibilityManifest = compatibilityManifest,
                RepredictionIndex = repredictionIndex
            };
            if (current.Document is null)
            {
                transaction.Create(docRef, stored);
            }
            else
            {
                transaction.Set(docRef, stored);
            }

            return repredictionIndex;
        }, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Saved transactionally allocated typed Bundesliga bonus prediction for question {QuestionId} (reprediction index: {RepredictionIndex})",
            bonusQuestion.KicktippQuestionId, savedIndex);
    }

    private async Task SaveTypedInitialMatchPredictionAsync(
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IEnumerable<string> contextDocumentNames,
        ResolvedMatchContextManifest resolvedContextManifest,
        bool overrideCreatedAt,
        CancellationToken cancellationToken)
    {
        var storedNames = contextDocumentNames.ToArray();
        var serializedManifest = SerializeResolvedContextManifest(resolvedContextManifest);
        var savedIndex = await _firestoreDb.RunTransactionAsync(async transaction =>
        {
            var candidates = await transaction.GetSnapshotAsync(BuildTypedMatchSemanticQuery(
                match, modelConfig, communityContext));
            var exactConfigRows = candidates.Documents
                .Select(document => (Document: document, Row: document.ConvertTo<FirestoreMatchPrediction>()))
                .Where(candidate => GetConfigMatchKind(candidate.Row, modelConfig) == PredictionConfigMatchKind.Exact)
                .ToArray();
            EnsureOneTypedMatchRowPerIndex(exactConfigRows.Select(candidate => candidate.Row).ToArray(), match);

            var current = exactConfigRows
                .OrderByDescending(candidate => candidate.Row.RepredictionIndex)
                .ThenByDescending(candidate => candidate.Row.CreatedAt.ToDateTimeOffset())
                .ThenBy(candidate => candidate.Row.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            var repredictionIndex = current.Row?.RepredictionIndex ?? 0;
            var docRef = current.Document?.Reference
                ?? _firestoreDb.Collection(_predictionsCollection).Document(
                    BuildTypedMatchDocumentId(match, modelConfig, communityContext, repredictionIndex));
            var deterministicSnapshot = current.Document is null
                ? await transaction.GetSnapshotAsync(docRef)
                : null;
            if (deterministicSnapshot?.Exists == true)
            {
                throw new InvalidOperationException(
                    "Typed Bundesliga match storage identity collides with an existing document outside its semantic scope.");
            }

            var now = Timestamp.GetCurrentTimestamp();
            var stored = new FirestoreMatchPrediction
            {
                Id = docRef.Id,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                KicktippFixtureId = match.KicktippFixtureId,
                KicktippRoundName = match.KicktippRoundName,
                ResultBasis = match.ResultBasis!.Value.ToSerializedValue(),
                BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                CompetitionSpecificData = ToFirestoreCompetitionSpecificData(match.CompetitionSpecificData),
                HomeGoals = prediction.HomeGoals,
                AwayGoals = prediction.AwayGoals,
                Justification = SerializeJustification(prediction.Justification),
                CreatedAt = overrideCreatedAt || current.Row is null ? now : current.Row.CreatedAt,
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
                ResolvedContextManifest = serializedManifest,
                RepredictionIndex = repredictionIndex
            };
            if (current.Document is null)
            {
                transaction.Create(docRef, stored);
            }
            else
            {
                transaction.Set(docRef, stored);
            }

            return repredictionIndex;
        }, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Saved transactionally allocated typed Bundesliga prediction for match {HomeTeam} vs {AwayTeam} on matchday {Matchday} (reprediction index: {RepredictionIndex})",
            match.HomeTeam, match.AwayTeam, match.Matchday, savedIndex);
    }

    public async Task<BonusPrediction?> GetCurrentBonusPredictionAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetCurrentBonusPredictionMetadataAsync(
            question, modelConfig, communityContext, cancellationToken);
        return metadata?.BonusPrediction;
    }

    public async Task<BonusPredictionMetadata?> GetCurrentBonusPredictionMetadataAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateCurrentBonusIdentity(question);
            ValidateTypedContextAvailability(question);
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentBonusIdentityFilters(query, question).OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var stored = SelectLatestTypedAwareForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(prediction => MatchesCurrentBonusIdentity(prediction, question))
                    .Where(prediction => HasCompleteTypedBonusProvenance(prediction, question, communityContext)),
                question,
                modelConfig);
            return stored is null ? null : CreateBonusPredictionMetadata(stored, communityContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve typed current bonus prediction {QuestionId} using model {Model} and community context {CommunityContext}",
                question.KicktippQuestionId, modelConfig.DisplayName, communityContext);
            throw;
        }
    }

    public async Task<bool> HasCurrentBonusPredictionAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default) =>
        await GetCurrentBonusPredictionAsync(question, modelConfig, communityContext, cancellationToken) is not null;

    public async Task<int> GetCurrentBonusRepredictionIndexAsync(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var metadata = await GetCurrentBonusPredictionMetadataAsync(
            question, modelConfig, communityContext, cancellationToken);
        if (metadata?.PredictionIdentity is null)
        {
            return -1;
        }

        var document = await _firestoreDb.Collection(_bonusPredictionsCollection)
            .Document(metadata.PredictionIdentity)
            .GetSnapshotAsync(cancellationToken);
        return document.Exists
            ? document.ConvertTo<FirestoreBonusPrediction>().RepredictionIndex
            : -1;
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
            var typedTarget = IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(targetQuestion);
            if (typedTarget)
            {
                ValidateCurrentBonusIdentity(targetQuestion);
                ValidateTypedContextAvailability(targetQuestion);
            }
            var normalizedQuestionText = BonusQuestionCompatibilityManifest.NormalizeText(targetQuestion.Text);
            var query = _firestoreDb.Collection(_bonusPredictionsCollection)
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", sourceCommunityContext);
            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var candidates = snapshot.Documents
                .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                .Where(prediction => !typedTarget
                    ? IsLegacyBundesligaBonusRow(prediction)
                    : MatchesCurrentBonusIdentity(prediction, targetQuestion)
                       && HasCompleteTypedBonusProvenance(prediction, targetQuestion, sourceCommunityContext))
                .Where(prediction => string.Equals(
                    BonusQuestionCompatibilityManifest.NormalizeText(prediction.QuestionText),
                    normalizedQuestionText,
                    StringComparison.Ordinal));
            var firestoreBonusPrediction = typedTarget
                ? SelectLatestTypedAwareForModelConfig(candidates, targetQuestion, modelConfig)
                : SelectLatestForModelConfig(candidates, modelConfig);

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
                if (GetConfigMatchKind(firestoreBonusPrediction, modelConfig) == PredictionConfigMatchKind.None)
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
                .Any(prediction => IsLegacyBundesligaBonusRow(prediction)
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
            ValidateCurrentMatchIdentity(match);
            var documentId = Guid.NewGuid().ToString();

            var firestoreMatch = new FirestoreMatch
            {
                Id = documentId,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                StartsAt = ConvertToTimestamp(match.StartsAt),
                Matchday = match.Matchday,
                KicktippFixtureId = match.KicktippFixtureId,
                KicktippRoundName = match.KicktippRoundName,
                ResultBasis = match.ResultBasis?.ToSerializedValue(),
                BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition?.ToSerializedValue(),
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
            KicktippFixtureId = firestorePrediction.KicktippFixtureId,
            KicktippRoundName = firestorePrediction.KicktippRoundName,
            ResultBasis = ParseResultBasis(firestorePrediction.ResultBasis),
            BundesligaSeasonSubcompetition = ParseBundesligaSeasonSubcompetition(firestorePrediction.BundesligaSeasonSubcompetition),
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
            KicktippFixtureId = firestoreMatch.KicktippFixtureId,
            KicktippRoundName = firestoreMatch.KicktippRoundName,
            ResultBasis = ParseResultBasis(firestoreMatch.ResultBasis),
            BundesligaSeasonSubcompetition = ParseBundesligaSeasonSubcompetition(firestoreMatch.BundesligaSeasonSubcompetition),
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

    private static ResultBasis? ParseResultBasis(string? value) =>
        BundesligaSeasonRoutingIdentityValues.TryParseResultBasis(value, out var parsed) ? parsed : null;

    private static BundesligaSeasonSubcompetition? ParseBundesligaSeasonSubcompetition(string? value) =>
        BundesligaSeasonRoutingIdentityValues.TryParseBundesligaSeasonSubcompetition(value, out var parsed) ? parsed : null;

    public Task<int> GetMatchRepredictionIndexAsync(Match match, string model, string communityContext, CancellationToken cancellationToken = default)
    {
        return GetMatchRepredictionIndexAsync(match, PredictionModelConfig.Create(model), communityContext, cancellationToken);
    }

    public async Task<int> GetMatchRepredictionIndexAsync(Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!CanLookupCurrentMatchIdentity(match))
            {
                return -1;
            }
            ValidateTypedContextAvailability(match);
            // Query by match characteristics, model, community context, and competition
            // Order by repredictionIndex descending to get the latest version
            var query = _firestoreDb.Collection(_predictionsCollection)
                .WhereEqualTo("homeTeam", match.HomeTeam)
                .WhereEqualTo("awayTeam", match.AwayTeam)
                .WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt))
                .WhereEqualTo("competition", _competition)
                .WhereEqualTo("model", modelConfig.Model)
                .WhereEqualTo("communityContext", communityContext);
            query = AddCurrentMatchIdentityFilters(query, match).OrderByDescending("repredictionIndex");

            var snapshot = await query.GetSnapshotAsync(cancellationToken);
            var firestorePrediction = SelectLatestTypedAwareForModelConfig(
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
                    .Where(prediction => HasCompleteTypedMatchProvenance(prediction, match, communityContext)),
                match,
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

    public async Task<Prediction?> GetCurrentCancelledMatchPredictionAsync(
        Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        var metadata = await GetCurrentCancelledMatchPredictionMetadataAsync(match, modelConfig, communityContext, cancellationToken);
        return metadata?.Prediction;
    }

    public async Task<PredictionMetadata?> GetCurrentCancelledMatchPredictionMetadataAsync(
        Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        ValidateCurrentMatchIdentity(match);
        ValidateTypedContextAvailability(match);
        var query = _firestoreDb.Collection(_predictionsCollection)
            .WhereEqualTo("homeTeam", match.HomeTeam)
            .WhereEqualTo("awayTeam", match.AwayTeam)
            .WhereEqualTo("competition", _competition)
            .WhereEqualTo("model", modelConfig.Model)
            .WhereEqualTo("communityContext", communityContext);
        query = AddCurrentMatchIdentityFilters(query, match).OrderByDescending("repredictionIndex");
        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        var stored = SelectLatestTypedAwareForModelConfig(
            snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
                .Where(prediction => HasCompleteTypedMatchProvenance(prediction, match, communityContext)),
            match,
            modelConfig);
        if (stored is null)
        {
            return null;
        }

        var manifest = DeserializeResolvedContextManifest(stored.ResolvedContextManifest);
        return new PredictionMetadata(
            new Prediction(stored.HomeGoals, stored.AwayGoals, DeserializeJustification(stored.Justification)),
            stored.CreatedAt.ToDateTimeOffset(), stored.ContextDocumentNames?.ToList() ?? [], manifest);
    }

    public async Task<int> GetCurrentCancelledMatchRepredictionIndexAsync(
        Match match, PredictionModelConfig modelConfig, string communityContext, CancellationToken cancellationToken = default)
    {
        var metadata = await GetCurrentCancelledMatchPredictionMetadataAsync(match, modelConfig, communityContext, cancellationToken);
        if (metadata is null)
        {
            return -1;
        }

        // The semantic index is selected by the same typed predicate as metadata.
        var query = _firestoreDb.Collection(_predictionsCollection)
            .WhereEqualTo("homeTeam", match.HomeTeam).WhereEqualTo("awayTeam", match.AwayTeam)
            .WhereEqualTo("competition", _competition).WhereEqualTo("model", modelConfig.Model)
            .WhereEqualTo("communityContext", communityContext);
        query = AddCurrentMatchIdentityFilters(query, match);
        var snapshot = await query.GetSnapshotAsync(cancellationToken);
        return SelectLatestTypedAwareForModelConfig(snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
            .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
            .Where(prediction => HasCompleteTypedMatchProvenance(prediction, match, communityContext)), match, modelConfig)?.RepredictionIndex ?? -1;
    }

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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(IsLegacyBundesligaMatchRow),
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(IsLegacyBundesligaMatchRow),
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(IsLegacyBundesligaMatchRow),
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
                snapshot.Documents.Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                    .Where(IsLegacyBundesligaBonusRow),
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
        ValidateCurrentMatchIdentity(match);
        ValidateTypedContextAvailability(match);
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
                query = AddCurrentMatchIdentityFilters(query, match);
                if (!match.IsCancelled)
                {
                    query = query.WhereEqualTo("startsAt", ConvertToTimestamp(match.StartsAt));
                }

                // A transaction reads the whole exact candidate set, rather than trusting the
                // index observed before generation. Firestore retries on a concurrent matching
                // write, making this compare-and-swap serializable for this prediction identity.
                var candidates = await transaction.GetSnapshotAsync(query);
                var semanticRows = candidates.Documents
                    .Select(document => document.ConvertTo<FirestoreMatchPrediction>())
                    .Where(prediction => MatchesCurrentMatchIdentity(prediction, match))
                    .ToArray();
                var current = SelectLatestTypedAwareForModelConfig(semanticRows, match, modelConfig);
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
                    KicktippFixtureId = match.KicktippFixtureId,
                    KicktippRoundName = match.KicktippRoundName,
                    ResultBasis = match.ResultBasis?.ToSerializedValue(),
                    BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition?.ToSerializedValue(),
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
            ValidateCurrentMatchIdentity(match);
            ValidateTypedContextAvailability(match);
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
                KicktippFixtureId = match.KicktippFixtureId,
                KicktippRoundName = match.KicktippRoundName,
                ResultBasis = match.ResultBasis?.ToSerializedValue(),
                BundesligaSeasonSubcompetition = match.BundesligaSeasonSubcompetition?.ToSerializedValue(),
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
            ValidateCurrentBonusIdentity(bonusQuestion);
            ValidateTypedContextAvailability(bonusQuestion);
            var documentNames = contextDocumentNames?.ToArray()
                ?? throw new ArgumentNullException(nameof(contextDocumentNames));
            ValidateResolvedBonusContextManifestForWrite(
                communityContext,
                documentNames,
                resolvedContextManifest);
            if (IsBundesliga2026_27 && BundesligaSeasonStorageIdentity.IsTypedBonusQuestion(bonusQuestion))
            {
                await SaveTypedBonusRepredictionAsync(
                    bonusQuestion, bonusPrediction, modelConfig, tokenUsage, cost, communityContext,
                    documentNames, repredictionIndex, resolvedContextManifest!, cancellationToken);
                return;
            }

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
                KicktippQuestionId = bonusQuestion.KicktippQuestionId,
                BundesligaSeasonSubcompetition = bonusQuestion.BundesligaSeasonSubcompetition?.ToSerializedValue(),
                BundesligaSeasonBonusIdentitySha256 = IsBundesliga2026_27
                    ? BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(bonusQuestion)
                    : null,
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

    private async Task SaveTypedBonusRepredictionAsync(
        BonusQuestion bonusQuestion,
        BonusPrediction bonusPrediction,
        PredictionModelConfig modelConfig,
        string tokenUsage,
        double cost,
        string communityContext,
        IReadOnlyList<string> contextDocumentNames,
        int repredictionIndex,
        ResolvedBonusContextManifest resolvedContextManifest,
        CancellationToken cancellationToken)
    {
        if (repredictionIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(repredictionIndex),
                "A typed Bundesliga bonus reprediction index must be positive.");
        }

        var identitySha256 = BundesligaSeasonStorageIdentity.ComputeBonusQuestionIdentitySha256(bonusQuestion);
        var serializedManifest = SerializeResolvedBonusContextManifest(resolvedContextManifest);
        var compatibilityManifest = SerializeBonusQuestionCompatibilityManifest(
            BonusQuestionCompatibilityManifest.Create(bonusQuestion));
        var optionTextsLookup = bonusQuestion.Options.ToDictionary(option => option.Id, option => option.Text);
        var selectedOptionTexts = bonusPrediction.SelectedOptionIds
            .Select(id => optionTextsLookup.TryGetValue(id, out var text) ? text : $"Unknown option: {id}")
            .ToArray();

        await _firestoreDb.RunTransactionAsync(async transaction =>
        {
            var candidates = await transaction.GetSnapshotAsync(BuildTypedBonusSemanticQuery(
                bonusQuestion, modelConfig, communityContext));
            var exactConfigRows = candidates.Documents
                .Select(document => document.ConvertTo<FirestoreBonusPrediction>())
                .Where(row => GetConfigMatchKind(row, modelConfig) == PredictionConfigMatchKind.Exact)
                .ToArray();
            EnsureOneTypedBonusRowPerIndex(exactConfigRows, bonusQuestion, identitySha256);
            var currentIndex = exactConfigRows.Length == 0
                ? -1
                : exactConfigRows.Max(row => row.RepredictionIndex);
            if (currentIndex == int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Typed Bundesliga bonus reprediction index overflow: no index can be allocated after Int32.MaxValue.");
            }

            var nextIndex = checked(currentIndex + 1);
            if (nextIndex != repredictionIndex)
            {
                throw new InvalidOperationException(
                    $"Typed Bundesliga bonus reprediction concurrency conflict: requested index {repredictionIndex}, next available index is {nextIndex}.");
            }

            var docRef = _firestoreDb.Collection(_bonusPredictionsCollection).Document(
                BuildTypedBonusDocumentId(bonusQuestion, modelConfig, communityContext, nextIndex));
            var existing = await transaction.GetSnapshotAsync(docRef);
            if (existing.Exists)
            {
                throw new InvalidOperationException(
                    $"Typed Bundesliga bonus reprediction concurrency conflict: index {nextIndex} is already allocated.");
            }

            var now = Timestamp.GetCurrentTimestamp();
            transaction.Create(docRef, new FirestoreBonusPrediction
            {
                Id = docRef.Id,
                QuestionText = bonusQuestion.Text,
                KicktippQuestionId = bonusQuestion.KicktippQuestionId,
                BundesligaSeasonSubcompetition = bonusQuestion.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                BundesligaSeasonBonusIdentitySha256 = identitySha256,
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
                ContextDocumentNames = contextDocumentNames.ToArray(),
                ResolvedBonusContextManifest = serializedManifest,
                BonusQuestionCompatibilityManifest = compatibilityManifest,
                RepredictionIndex = nextIndex
            });
            return nextIndex;
        }, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Saved transactionally allocated typed Bundesliga bonus reprediction for question {QuestionId} (reprediction index: {RepredictionIndex})",
            bonusQuestion.KicktippQuestionId, repredictionIndex);
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
                if (doc.TryGetValue<string>("model", out var model) && !string.IsNullOrWhiteSpace(model))
                {
                    models.Add(model);
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
                AddModelConfigIfValid(modelConfigs, doc.ConvertTo<FirestoreBonusPrediction>());
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
                if (doc.TryGetValue<string>("communityContext", out var context) && !string.IsNullOrWhiteSpace(context))
                {
                    communityContexts.Add(context);
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

    private Query BuildTypedMatchSemanticQuery(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext) =>
        _firestoreDb.Collection(_predictionsCollection)
            .WhereEqualTo("competition", _competition)
            .WhereEqualTo("kicktippFixtureId", match.KicktippFixtureId)
            .WhereEqualTo("bundesligaSeasonSubcompetition", match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue())
            .WhereEqualTo("model", modelConfig.Model)
            .WhereEqualTo("communityContext", communityContext);

    private Query BuildTypedBonusSemanticQuery(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext) =>
        _firestoreDb.Collection(_bonusPredictionsCollection)
            .WhereEqualTo("competition", _competition)
            .WhereEqualTo("kicktippQuestionId", question.KicktippQuestionId)
            .WhereEqualTo("bundesligaSeasonSubcompetition", question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue())
            .WhereEqualTo("model", modelConfig.Model)
            .WhereEqualTo("communityContext", communityContext);

    private void EnsureOneTypedMatchRowPerIndex(
        IReadOnlyCollection<FirestoreMatchPrediction> rows,
        Match match)
    {
        if (rows.Any(row =>
                !MatchesCurrentMatchIdentity(row, match)
                || !string.Equals(row.HomeTeam, match.HomeTeam, StringComparison.Ordinal)
                || !string.Equals(row.AwayTeam, match.AwayTeam, StringComparison.Ordinal)
                || row.Matchday != match.Matchday
                || !match.IsCancelled && row.StartsAt != ConvertToTimestamp(match.StartsAt)))
        {
            throw new InvalidOperationException(
                "Stored typed Bundesliga match identity conflicts with the requested canonical fixture identity.");
        }

        if (rows.GroupBy(row => row.RepredictionIndex).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Stored typed Bundesliga match identity has duplicate semantic reprediction indices.");
        }
    }

    private void EnsureOneTypedBonusRowPerIndex(
        IReadOnlyCollection<FirestoreBonusPrediction> rows,
        BonusQuestion question,
        string identitySha256)
    {
        if (rows.Any(row =>
                !string.Equals(row.KicktippQuestionId, question.KicktippQuestionId, StringComparison.Ordinal)
                || !string.Equals(
                    row.BundesligaSeasonSubcompetition,
                    question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
                    StringComparison.Ordinal)
                || !string.Equals(row.QuestionText, question.Text, StringComparison.Ordinal)
                || !string.Equals(row.BundesligaSeasonBonusIdentitySha256, identitySha256, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Stored typed Bundesliga bonus identity conflicts with the requested canonical question identity.");
        }

        if (rows.GroupBy(row => row.RepredictionIndex).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Stored typed Bundesliga bonus identity has duplicate semantic reprediction indices.");
        }
    }

    private string BuildTypedMatchDocumentId(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        int repredictionIndex)
    {
        var identity = string.Join("\n", new[]
        {
            "typed-bundesliga-match-v1",
            _competition,
            match.KicktippFixtureId!,
            match.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
            modelConfig.IdentityKey,
            communityContext,
            repredictionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return $"bundesliga-typed-match-{DocumentPublicationContract.ComputeContentSha256(identity)}";
    }

    private string BuildTypedBonusDocumentId(
        BonusQuestion question,
        PredictionModelConfig modelConfig,
        string communityContext,
        int repredictionIndex)
    {
        var identity = string.Join("\n", new[]
        {
            "typed-bundesliga-bonus-v1",
            _competition,
            question.KicktippQuestionId!,
            question.BundesligaSeasonSubcompetition!.Value.ToSerializedValue(),
            modelConfig.IdentityKey,
            communityContext,
            repredictionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return $"bundesliga-typed-bonus-{DocumentPublicationContract.ComputeContentSha256(identity)}";
    }

    private string BuildBundesligaRepredictionDocumentId(
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext,
        int repredictionIndex)
    {
        return BuildTypedMatchDocumentId(match, modelConfig, communityContext, repredictionIndex);
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
