using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace EHonda.KicktippAi.Core;

public enum BundesligaBonusQuestionCategory
{
    Champion,
    Relegation,
    TopScorer,
    Coach,
    Unknown
}

public enum BonusContextExclusionReason
{
    ProhibitedAggregate,
    CategoryDoesNotUseRoster,
    NoExactIdentity
}

public sealed record BonusContextDocumentExclusion(
    DocumentPublicationKey Document,
    BonusContextExclusionReason Reason);

/// <summary>Whole-selection limits for one resolved Bundesliga bonus context.</summary>
public sealed record BonusContextBudget
{
    public const int DefaultMaximumDocuments = 20;
    public const int DefaultMaximumEstimatedTokens = 32_000;
    public const int MinimumMaximumDocuments = 2;
    public const int MinimumMaximumEstimatedTokens = 256;

    public BonusContextBudget(int maximumDocuments, int maximumEstimatedTokens)
    {
        if (maximumDocuments < MinimumMaximumDocuments)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDocuments),
                $"The bonus-context document budget must be at least {MinimumMaximumDocuments}.");
        }

        if (maximumEstimatedTokens < MinimumMaximumEstimatedTokens)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEstimatedTokens),
                $"The bonus-context estimated-token budget must be at least {MinimumMaximumEstimatedTokens}.");
        }

        MaximumDocuments = maximumDocuments;
        MaximumEstimatedTokens = maximumEstimatedTokens;
    }

    public int MaximumDocuments { get; }
    public int MaximumEstimatedTokens { get; }

    public static BonusContextBudget Default { get; } = new(
        DefaultMaximumDocuments,
        DefaultMaximumEstimatedTokens);
}

public sealed record BonusContextMeasurement(int Utf8Bytes, int EstimatedTokens);

/// <summary>
/// Deterministic estimate of the exact context section rendered by PredictionPromptComposer.
/// </summary>
public static class BonusContextBudgetEstimator
{
    public static BonusContextMeasurement Measure(IEnumerable<DocumentContext> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var utf8Bytes = 0;
        foreach (var document in documents)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(document.Name);
            ArgumentNullException.ThrowIfNull(document.Content);

            // Exact rendering: "---\n{name}\n\n{content}\n" per document.
            utf8Bytes = checked(utf8Bytes
                                + Encoding.UTF8.GetByteCount("---\n")
                                + Encoding.UTF8.GetByteCount(document.Name)
                                + Encoding.UTF8.GetByteCount("\n\n")
                                + Encoding.UTF8.GetByteCount(document.Content)
                                + Encoding.UTF8.GetByteCount("\n"));
        }

        // PredictionPromptComposer closes the section once after all documents.
        utf8Bytes = checked(utf8Bytes + Encoding.UTF8.GetByteCount("---"));
        return new BonusContextMeasurement(utf8Bytes, checked((utf8Bytes + 3) / 4));
    }

    public static void EnsureFits(
        int selectedDocumentCount,
        BonusContextMeasurement measurement,
        BonusContextBudget budget)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(budget);

        if (selectedDocumentCount > budget.MaximumDocuments)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus context requires all {selectedDocumentCount} selected documents, " +
                $"which exceeds the configured document budget of {budget.MaximumDocuments}; no documents were truncated.");
        }

        if (measurement.EstimatedTokens > budget.MaximumEstimatedTokens)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus context requires an estimated {measurement.EstimatedTokens} context tokens " +
                $"({measurement.Utf8Bytes} UTF-8 bytes), which exceeds the configured budget of " +
                $"{budget.MaximumEstimatedTokens}; no documents were truncated.");
        }
    }
}

/// <summary>The competition-owned selection plan produced before document retrieval.</summary>
public sealed record BundesligaBonusContextSelection(
    BundesligaBonusQuestionCategory Category,
    ImmutableArray<DocumentPublicationKey> RequiredDocuments,
    ImmutableArray<string> TargetedTeamSlugs,
    ImmutableArray<BonusContextDocumentExclusion> ExcludedDocuments);

/// <summary>Auditable selection and budget result for the exact resolved documents.</summary>
public sealed record ResolvedBonusContextSelection(
    BundesligaBonusQuestionCategory Category,
    ImmutableArray<string> SelectedDocumentNames,
    ImmutableArray<BonusContextDocumentExclusion> ExcludedDocuments,
    int EstimatedUtf8Bytes,
    int EstimatedTokens,
    BonusContextBudget Budget);

/// <summary>Pure ADR-0038 category and exact-identity selection policy.</summary>
public static class BonusContextSelectionPolicy
{
    private static readonly DocumentPublicationKey ClubEloRankings = new(
        DocumentPublicationKind.Kpi,
        BundesligaDocumentPublication.ClubEloRankingsDocumentName);

    private static readonly DocumentPublicationKey SquadSummary = new(
        DocumentPublicationKind.Kpi,
        BundesligaRosterPublicationContract.SquadSummaryDocumentName);

    private static readonly DocumentPublicationKey ProhibitedTeamRosters = new(
        DocumentPublicationKind.Context,
        "team-rosters");

    private static readonly string[] ChampionSignals =
    [
        "deutscher meister",
        "meisterschaft",
        "meister",
        "champion",
        "league winner",
        "win the league"
    ];

    private static readonly string[] RelegationSignals =
    [
        "abstieg",
        "absteiger",
        "abstiegsplatz",
        "absteigen",
        "steigen ab",
        "steigt ab",
        "steigen in die 2. liga ab",
        "steigen in die zweite liga ab",
        "relegation",
        "relegated",
        "bottom three",
        "bottom place",
        "places 16-18",
        "plätze 16-18"
    ];

    private static readonly string[] TopScorerSignals =
    [
        "torschützenkönig",
        "torschuetzenkoenig",
        "torschütze",
        "torjäger",
        "meisten tore",
        "meisten toren",
        "top scorer",
        "most goals",
        "golden boot"
    ];

    private static readonly string[] CoachSignals =
    [
        "trainer",
        "cheftrainer",
        "trainerwechsel",
        "entlassung",
        "entlassen",
        "manager",
        "coach",
        "head coach",
        "sacked",
        "dismissed"
    ];

    public static BundesligaBonusContextSelection SelectBundesliga(
        BonusQuestion? question,
        BundesligaRosterLastKnownGood rosters)
    {
        ArgumentNullException.ThrowIfNull(rosters);

        var category = question is null
            ? BundesligaBonusQuestionCategory.Unknown
            : Classify(question);
        var targets = SelectTargets(question, category, rosters);

        var required = ImmutableArray.CreateBuilder<DocumentPublicationKey>();
        required.Add(ClubEloRankings);
        required.Add(SquadSummary);
        required.AddRange(targets.Select(slug =>
            new DocumentPublicationKey(DocumentPublicationKind.Context, $"roster-{slug}")));

        var requiredDocuments = required.ToImmutable();

        return new BundesligaBonusContextSelection(
            category,
            requiredDocuments,
            targets,
            GetCanonicalExclusions(category, requiredDocuments.Select(document => document.Name)));
    }

    public static ImmutableArray<BonusContextDocumentExclusion> GetCanonicalExclusions(
        BundesligaBonusQuestionCategory category,
        IEnumerable<string> selectedDocumentNames)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        var selectedNames = selectedDocumentNames?.ToImmutableArray()
            ?? throw new ArgumentNullException(nameof(selectedDocumentNames));
        if (selectedNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Selected bonus-context document names must be nonempty.", nameof(selectedDocumentNames));
        }

        var canonicalRosterNames = BundesligaTeamManifest.Default.Entries
            .Select(entry => $"roster-{entry.TeamSlug}")
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var canonicalRosterNameSet = canonicalRosterNames.ToHashSet(StringComparer.Ordinal);
        var selectedRosterNames = selectedNames
            .Where(name => name.StartsWith("roster-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (!selectedRosterNames.IsSubsetOf(canonicalRosterNameSet))
        {
            throw new ArgumentException(
                "Selected Bundesliga roster documents must use canonical manifest slugs.",
                nameof(selectedDocumentNames));
        }

        var rosterExclusionReason = category is BundesligaBonusQuestionCategory.TopScorer
            or BundesligaBonusQuestionCategory.Coach
            ? BonusContextExclusionReason.NoExactIdentity
            : BonusContextExclusionReason.CategoryDoesNotUseRoster;
        var exclusions = ImmutableArray.CreateBuilder<BonusContextDocumentExclusion>();
        exclusions.Add(new BonusContextDocumentExclusion(
            ProhibitedTeamRosters,
            BonusContextExclusionReason.ProhibitedAggregate));
        foreach (var rosterName in canonicalRosterNames)
        {
            if (!selectedRosterNames.Contains(rosterName))
            {
                exclusions.Add(new BonusContextDocumentExclusion(
                    new DocumentPublicationKey(DocumentPublicationKind.Context, rosterName),
                    rosterExclusionReason));
            }
        }

        return exclusions.ToImmutable();
    }

    public static BundesligaBonusQuestionCategory Classify(BonusQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(question.Text);
        ArgumentNullException.ThrowIfNull(question.Options);

        var matches = new List<BundesligaBonusQuestionCategory>(4);
        if (!IsChampionsLeagueChampionReference(question.Text))
        {
            AddIfMatched(matches, question.Text, ChampionSignals, BundesligaBonusQuestionCategory.Champion);
        }
        AddIfMatched(matches, question.Text, RelegationSignals, BundesligaBonusQuestionCategory.Relegation);
        AddIfMatched(matches, question.Text, TopScorerSignals, BundesligaBonusQuestionCategory.TopScorer);
        AddIfMatched(matches, question.Text, CoachSignals, BundesligaBonusQuestionCategory.Coach);

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus question '{question.Text}' matches multiple context categories: " +
                $"{string.Join(", ", matches)}. Refine the policy instead of selecting by precedence.");
        }

        return matches.Count == 0 ? BundesligaBonusQuestionCategory.Unknown : matches[0];
    }

    private static ImmutableArray<string> SelectTargets(
        BonusQuestion? question,
        BundesligaBonusQuestionCategory category,
        BundesligaRosterLastKnownGood rosters)
    {
        if (question is null
            || category is not (BundesligaBonusQuestionCategory.TopScorer
                or BundesligaBonusQuestionCategory.Coach))
        {
            return [];
        }

        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var snapshot in rosters.Snapshots)
        {
            if (MatchesTeam(question, snapshot.Team))
            {
                targets.Add(snapshot.Team.TeamSlug);
                continue;
            }

            var relevantRole = category == BundesligaBonusQuestionCategory.TopScorer
                ? BundesligaRosterRole.Player
                : BundesligaRosterRole.Coach;
            if (snapshot.Members
                .Where(member => member.Role == relevantRole)
                .Any(member => MatchesIdentity(question, member.Name)))
            {
                targets.Add(snapshot.Team.TeamSlug);
            }
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                $"Bundesliga bonus question '{question.Text}' has category {category} and requires targeted roster context, " +
                "but neither its text nor its options map exactly to a current manifest team or relevant roster member.");
        }

        return targets.Order(StringComparer.Ordinal).ToImmutableArray();
    }

    private static bool MatchesTeam(BonusQuestion question, BundesligaTeamManifestEntry team) =>
        MatchesIdentity(question, team.KicktippName)
        || MatchesIdentity(question, team.OfficialName)
        || MatchesIdentity(question, team.ClubEloName);

    private static bool MatchesIdentity(BonusQuestion question, string identity) =>
        ContainsWholePhrase(question.Text, identity)
        || question.Options.Any(option =>
            string.Equals(option.Text.Trim(), identity, StringComparison.OrdinalIgnoreCase));

    private static void AddIfMatched(
        ICollection<BundesligaBonusQuestionCategory> matches,
        string text,
        IEnumerable<string> signals,
        BundesligaBonusQuestionCategory category)
    {
        if (signals.Any(signal => ContainsWholePhrase(text, signal)))
        {
            matches.Add(category);
        }
    }

    private static bool ContainsWholePhrase(string value, string phrase)
    {
        var searchStart = 0;
        while (searchStart <= value.Length - phrase.Length)
        {
            var index = value.IndexOf(phrase, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var beforeIsBoundary = index == 0 || !IsLetterOrDigitBefore(value, index);
            var afterIndex = index + phrase.Length;
            var afterIsBoundary = afterIndex == value.Length || !IsLetterOrDigitAt(value, afterIndex);
            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }

            searchStart = index + 1;
        }

        return false;
    }

    private static bool IsChampionsLeagueChampionReference(string value)
    {
        var normalized = value.Replace('-', ' ');
        return ContainsWholePhrase(normalized, "champions league meister")
               || ContainsWholePhrase(normalized, "champions league champion");
    }

    private static bool IsLetterOrDigitBefore(string value, int index)
    {
        var status = Rune.DecodeLastFromUtf16(value.AsSpan(0, index), out var rune, out _);
        return status == OperationStatus.Done
            ? Rune.IsLetterOrDigit(rune)
            : char.IsLetterOrDigit(value[index - 1]);
    }

    private static bool IsLetterOrDigitAt(string value, int index)
    {
        var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out _);
        return status == OperationStatus.Done
            ? Rune.IsLetterOrDigit(rune)
            : char.IsLetterOrDigit(value[index]);
    }
}
