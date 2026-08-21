using System.Runtime.CompilerServices;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;

namespace FirebaseAdapter;

/// <summary>
/// Firebase-based context provider for bonus predictions. Bundesliga reserved documents are
/// resolved through their headed publication sets; legacy competitions retain generic KPI reads.
/// </summary>
public class FirebaseKpiContextProvider : IKpiContextProvider, IResolvedBonusContextProvider
{
    private const string FifaRankingsDocumentName = "fifa-rankings";
    private const string LineupsDocumentName = "lineups";
    private const string TopScorerTeamQuestion = "Welche Mannschaft stellt den Spieler mit den meisten Toren?";

    private readonly string? _competition;
    private readonly IKpiRepository _kpiRepository;
    private readonly IDocumentPublicationRepository? _publicationRepository;
    private readonly ILogger<FirebaseKpiContextProvider> _logger;

    /// <summary>
    /// Compatibility constructor for non-Bundesliga callers that still use generic KPI context.
    /// Live composition uses the competition-bound constructor.
    /// </summary>
    public FirebaseKpiContextProvider(
        IKpiRepository kpiRepository,
        ILogger<FirebaseKpiContextProvider> logger)
    {
        _kpiRepository = kpiRepository ?? throw new ArgumentNullException(nameof(kpiRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FirebaseKpiContextProvider(
        string competition,
        IKpiRepository kpiRepository,
        IDocumentPublicationRepository? publicationRepository,
        ILogger<FirebaseKpiContextProvider> logger)
    {
        _competition = CompetitionIds.Canonicalize(competition);
        _kpiRepository = kpiRepository ?? throw new ArgumentNullException(nameof(kpiRepository));
        _publicationRepository = publicationRepository;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal)
            && _publicationRepository is null)
        {
            throw new ArgumentNullException(
                nameof(publicationRepository),
                "Bundesliga bonus context requires the headed document-publication repository.");
        }
    }

    public async IAsyncEnumerable<DocumentContext> GetContextAsync(
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (IsCurrentBundesliga)
        {
            foreach (var context in (await GetBundesligaContextAsync(null, communityContext, cancellationToken)).Documents)
            {
                yield return context;
            }

            yield break;
        }

        await foreach (var context in GetGenericContextAsync(communityContext, cancellationToken))
        {
            yield return context;
        }
    }

    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextByCommunityAsync(
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var context in GetContextAsync(communityContext, cancellationToken))
        {
            yield return context;
        }
    }

    /// <summary>
    /// Compatibility overload for legacy callers. Bundesliga targeting requires the full question.
    /// </summary>
    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(
        string questionText,
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (IsCurrentBundesliga)
        {
            throw new InvalidOperationException(
                "Bundesliga bonus context selection requires the complete BonusQuestion, including its options.");
        }

        await foreach (var context in GetLegacyBonusQuestionContextAsync(questionText, communityContext, cancellationToken))
        {
            yield return context;
        }
    }

    public async IAsyncEnumerable<DocumentContext> GetBonusQuestionContextAsync(
        BonusQuestion question,
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (IsCurrentBundesliga)
        {
            foreach (var context in (await ResolveBonusQuestionContextAsync(
                         question,
                         communityContext,
                         cancellationToken)).Documents)
            {
                yield return context;
            }

            yield break;
        }

        await foreach (var context in GetLegacyBonusQuestionContextAsync(question.Text, communityContext, cancellationToken))
        {
            yield return context;
        }
    }

    private bool IsCurrentBundesliga =>
        string.Equals(_competition, CompetitionIds.Bundesliga2026_27, StringComparison.Ordinal);

    public Task<ResolvedBonusContext> ResolveBonusQuestionContextAsync(
        BonusQuestion question,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (!IsCurrentBundesliga)
        {
            throw new InvalidOperationException(
                "Resolved bonus-context provenance is available only for bundesliga-2026-27.");
        }

        return GetBundesligaContextAsync(question, communityContext, cancellationToken);
    }

    private async Task<ResolvedBonusContext> GetBundesligaContextAsync(
        BonusQuestion? question,
        string communityContext,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        var repository = _publicationRepository
            ?? throw new InvalidOperationException("Bundesliga bonus context publication repository is not configured.");

        var elo = await repository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.ClubElo,
            communityContext,
            cancellationToken);
        if (elo is null)
        {
            throw new InvalidOperationException(
                $"Missing required Bundesliga Club Elo publication for community context '{communityContext}'. " +
                "Run 'collect-context club-elo' before bonus prediction.");
        }

        var rosters = await repository.GetLastKnownGoodAsync(
            BundesligaDocumentPublication.Rosters,
            communityContext,
            cancellationToken);
        if (rosters is null)
        {
            throw new InvalidOperationException(
                $"Missing required Bundesliga roster publication for community context '{communityContext}'. " +
                "Run 'collect-context rosters' before bonus prediction.");
        }

        try
        {
            _ = BundesligaClubEloPublication.ReconstructLastKnownGood(elo);
            var reconstructedRosters = BundesligaRosterPublication.ReconstructLastKnownGood(rosters);
            var selection = BonusContextSelectionPolicy.SelectBundesliga(question, reconstructedRosters);
            var byKey = elo.Documents
                .Concat(rosters.Documents)
                .ToDictionary(document => document.Key);

            var selectedDocuments = selection.RequiredDocuments.Select(key =>
            {
                if (!byKey.TryGetValue(key, out var document))
                {
                    throw new InvalidDataException(
                        $"Required Bundesliga bonus context document '{key.Kind}:{key.Name}' is missing from its headed publication.");
                }

                return document;
            }).ToArray();
            var contexts = selectedDocuments
                .Select(document => new DocumentContext(document.Name, document.Content))
                .ToArray();
            var manifest = ResolvedBonusContextManifest.Create(
                CompetitionIds.Bundesliga2026_27,
                communityContext,
                selectedDocuments.Select(document => new ResolvedBonusContextDocument(
                    document.Kind.ToString(),
                    document.Name,
                    document.Version,
                    DocumentPublicationContract.ComputeContentSha256(document.Content))),
                rosters.Snapshot.SnapshotId,
                elo.Snapshot.SnapshotId);

            _logger.LogInformation(
                "Selected {DocumentCount} Bundesliga bonus context documents for community {CommunityContext}: {DocumentNames}",
                contexts.Length,
                communityContext,
                string.Join(',', contexts.Select(context => context.Name)));
            return new ResolvedBonusContext(contexts, manifest);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus context for community '{communityContext}' failed headed publication validation. " +
                "Refresh Club Elo and roster context before bonus prediction.",
                exception);
        }
    }

    private async IAsyncEnumerable<DocumentContext> GetGenericContextAsync(
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Retrieving generic KPI documents for competition {Competition} and community {CommunityContext}",
            _competition ?? "legacy-unbound",
            communityContext);

        IReadOnlyList<KpiDocument> kpiDocuments;
        try
        {
            kpiDocuments = await _kpiRepository.GetAllKpiDocumentsAsync(communityContext, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve KPI documents for community {CommunityContext}",
                communityContext);
            throw;
        }

        foreach (var kpiDocument in kpiDocuments)
        {
            yield return new DocumentContext(kpiDocument.DocumentName, kpiDocument.Content);
        }
    }

    private async IAsyncEnumerable<DocumentContext> GetLegacyBonusQuestionContextAsync(
        string questionText,
        string communityContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var context in GetGenericContextAsync(communityContext, cancellationToken))
        {
            if (IsAlwaysIncludedLegacyBonusDocument(context.Name)
                || IsTopScorerTeamQuestion(questionText)
                && string.Equals(context.Name, LineupsDocumentName, StringComparison.OrdinalIgnoreCase)
                || IsTrainerChangeQuestion(questionText)
                && context.Name.Contains("manager-data", StringComparison.OrdinalIgnoreCase)
                || IsRelegationQuestion(questionText)
                && context.Name.Contains("manager-data", StringComparison.OrdinalIgnoreCase))
            {
                yield return context;
            }
        }
    }

    private static bool IsAlwaysIncludedLegacyBonusDocument(string documentName) =>
        documentName.Contains("team-data", StringComparison.OrdinalIgnoreCase)
        || string.Equals(documentName, FifaRankingsDocumentName, StringComparison.OrdinalIgnoreCase);

    private static bool IsTopScorerTeamQuestion(string questionText) =>
        string.Equals(questionText, TopScorerTeamQuestion, StringComparison.Ordinal);

    private static bool IsTrainerChangeQuestion(string questionText)
    {
        if (string.IsNullOrWhiteSpace(questionText))
        {
            return false;
        }

        var lowerText = questionText.ToLowerInvariant();
        return lowerText.Contains("trainerwechsel")
               || lowerText.Contains("trainer")
               || lowerText.Contains("cheftrainer")
               || lowerText.Contains("entlassung")
               || lowerText.Contains("entlassen")
               || lowerText.Contains("manager")
               || lowerText.Contains("coach");
    }

    private static bool IsRelegationQuestion(string questionText)
    {
        if (string.IsNullOrWhiteSpace(questionText))
        {
            return false;
        }

        var lowerText = questionText.ToLowerInvariant();
        return lowerText.Contains("16-18")
               || lowerText.Contains("plätze 16-18")
               || lowerText.Contains("abstieg")
               || lowerText.Contains("relegation")
               || lowerText.Contains("abstiegsplätze")
               || lowerText.Contains("absteiger");
    }

    public async Task<DocumentContext?> GetKpiDocumentContextAsync(
        string documentId,
        string communityContext,
        CancellationToken cancellationToken = default)
    {
        var kpiDocument = await _kpiRepository.GetKpiDocumentAsync(documentId, communityContext, cancellationToken);
        return kpiDocument is null
            ? null
            : new DocumentContext(kpiDocument.DocumentName, kpiDocument.Content);
    }
}
