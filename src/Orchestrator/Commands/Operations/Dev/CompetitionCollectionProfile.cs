using System.Collections.ObjectModel;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Infrastructure;

namespace Orchestrator.Commands.Operations.Dev;

public enum CompetitionCollector
{
    Kicktipp,
    BundesligaHistoryPlayedDates,
    ClubElo,
    Rosters,
    Wm26HistoryPlayedDates,
    FifaRankings,
    NationalLineups
}

public enum CompetitionCollectorExecutionMode
{
    Direct,
    IncludedInPrevious
}

public sealed record CompetitionCollectorStep(
    CompetitionCollector Collector,
    CompetitionCollectorExecutionMode ExecutionMode = CompetitionCollectorExecutionMode.Direct);

public sealed record CompetitionPromptRoute(
    string Source,
    string MatchPromptName,
    int? MatchPromptVersion,
    string BonusPromptName,
    int? BonusPromptVersion,
    string Label,
    string FallbackModel);

public sealed record CompetitionContextFeatures(
    bool HomeAwayHistory,
    bool HeadToHeadHistory,
    bool KnockoutRules,
    bool Transfers);

public sealed record CompetitionCollectionProfile(
    string Competition,
    string DisplayName,
    IReadOnlyList<string> SupportedDevelopmentCommunities,
    IReadOnlyList<CompetitionCollectorStep> Collectors,
    IReadOnlyList<string> RequiredMatchDocumentTemplates,
    IReadOnlyList<string> RequiredAggregateContextDocuments,
    IReadOnlyList<string> RequiredKpiDocuments,
    int ExpectedTeamCount,
    int ExpectedMatchCount,
    int? ExpectedMatchesPerMatchday,
    DateOnly SeasonStartsOn,
    DateOnly SeasonEndsOn,
    CompetitionPromptRoute PromptRoute,
    CompetitionContextFeatures ContextFeatures,
    IReadOnlyList<string> ValidationCommands);

public interface ICompetitionCollectionProfileResolver
{
    IReadOnlyList<string> SupportedDevelopmentCommunities { get; }

    CompetitionCollectionProfile ResolveForDevelopment(string community, string? competition = null);

    CompetitionCollectionProfile ResolveCompetition(string competition);
}

public sealed class CompetitionCollectionProfileResolver : ICompetitionCollectionProfileResolver
{
    private static readonly CompetitionCollectionProfile BundesligaProfile = CreateProfile(
        CompetitionIds.Bundesliga2026_27,
        "Bundesliga 2026/27",
        [CompetitionResolver.BundesligaDevelopmentCommunity],
        [
            new(CompetitionCollector.Kicktipp),
            new(CompetitionCollector.BundesligaHistoryPlayedDates, CompetitionCollectorExecutionMode.IncludedInPrevious),
            new(CompetitionCollector.ClubElo),
            new(CompetitionCollector.Rosters)
        ],
        [
            "bundesliga-standings.csv",
            "community-rules-{community-context}.md",
            "recent-history-{home-team-slug}.csv",
            "recent-history-{away-team-slug}.csv",
            "home-history-{home-team-slug}.csv",
            "away-history-{away-team-slug}.csv",
            "head-to-head-{home-team-slug}-vs-{away-team-slug}.csv",
            "roster-{home-team-slug}",
            "roster-{away-team-slug}",
            "club-elo-{home-team-slug}.csv",
            "club-elo-{away-team-slug}.csv"
        ],
        [BundesligaRosterPublicationContract.AggregateRosterDocumentName],
        [BundesligaDocumentPublication.ClubEloRankingsDocumentName, BundesligaRosterPublicationContract.SquadSummaryDocumentName],
        BundesligaTeamManifest.ExpectedTeamCount,
        306,
        9,
        new DateOnly(2026, 8, 28),
        new DateOnly(2027, 5, 22),
        new CompetitionPromptRoute(
            CompetitionResolver.LangfusePromptSource,
            CompetitionResolver.BundesligaMatchPromptName,
            CompetitionResolver.BundesligaMatchPromptVersion,
            CompetitionResolver.BundesligaBonusPromptName,
            CompetitionResolver.BundesligaBonusPromptVersion,
            CompetitionResolver.DefaultBundesligaPromptLabel,
            CompetitionResolver.BundesligaFallbackPromptModel),
        new CompetitionContextFeatures(
            HomeAwayHistory: true,
            HeadToHeadHistory: true,
            KnockoutRules: false,
            Transfers: false),
        [
            "dotnet run --project src/Orchestrator -- collect-context-dev --community ehonda-dev-buli-2627 --competition bundesliga-2026-27 --full-season --dry-run --verbose",
            "dotnet run --project src/Orchestrator -- bundesliga-history audit --community-context ehonda-dev-buli-2627 --competition bundesliga-2026-27"
        ]);

    private static readonly CompetitionCollectionProfile WorldCupProfile = CreateProfile(
        CompetitionIds.FifaWorldCup2026,
        "FIFA World Cup 2026",
        [CompetitionResolver.WorldCupDevelopmentCommunity],
        [
            new(CompetitionCollector.Kicktipp),
            new(CompetitionCollector.Wm26HistoryPlayedDates),
            new(CompetitionCollector.FifaRankings),
            new(CompetitionCollector.NationalLineups)
        ],
        [
            "fifa-world-cup-2026-standings.csv",
            "community-rules-{community-context}.md",
            "community-rules-{community-context}-knockout.md",
            "recent-history-{home-team-slug}.csv",
            "recent-history-{away-team-slug}.csv",
            "fifa-ranking-{home-team-slug}.csv",
            "fifa-ranking-{away-team-slug}.csv",
            "lineup-{home-team-slug}.csv",
            "lineup-{away-team-slug}.csv"
        ],
        [],
        [CollectContextFifaCommand.FifaRankingsDocumentName, CollectContextLineupsCommand.LineupsDocumentName],
        48,
        104,
        null,
        new DateOnly(2026, 6, 11),
        new DateOnly(2026, 7, 19),
        new CompetitionPromptRoute(
            CompetitionResolver.LangfusePromptSource,
            CompetitionResolver.WorldCupMatchPromptName,
            null,
            CompetitionResolver.WorldCupBonusPromptName,
            null,
            CompetitionResolver.DefaultWorldCupPromptLabel,
            CompetitionResolver.WorldCupFallbackPromptModel),
        new CompetitionContextFeatures(
            HomeAwayHistory: false,
            HeadToHeadHistory: false,
            KnockoutRules: true,
            Transfers: false),
        [
            "dotnet run --project tests/Orchestrator.Tests -- --treenode-filter \"/*/*/CollectContextDevCommandTests/*\"",
            "dotnet run --project tests/Orchestrator.Tests -- --treenode-filter \"/*/*/Wm26RecentHistoryCommandTests/*\""
        ]);

    private static readonly IReadOnlyDictionary<string, CompetitionCollectionProfile> ProfilesByCompetition =
        new ReadOnlyDictionary<string, CompetitionCollectionProfile>(
            new Dictionary<string, CompetitionCollectionProfile>(StringComparer.Ordinal)
            {
                [BundesligaProfile.Competition] = BundesligaProfile,
                [WorldCupProfile.Competition] = WorldCupProfile
            });

    private static readonly IReadOnlyDictionary<string, CompetitionCollectionProfile> ProfilesByDevelopmentCommunity =
        CreateDevelopmentProfiles();

    private static readonly IReadOnlyList<string> DevelopmentCommunities = Array.AsReadOnly(
        ProfilesByDevelopmentCommunity.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray());

    public IReadOnlyList<string> SupportedDevelopmentCommunities => DevelopmentCommunities;

    public CompetitionCollectionProfile ResolveForDevelopment(string community, string? competition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(community);
        var normalizedCommunity = community.Trim();

        if (!ProfilesByDevelopmentCommunity.TryGetValue(normalizedCommunity, out var communityProfile))
        {
            throw new NotSupportedException(
                $"Development community '{normalizedCommunity}' has no collection profile. " +
                $"Supported communities: {string.Join(", ", SupportedDevelopmentCommunities)}.");
        }

        var expectedCompetition = CompetitionResolver.ResolveDevelopmentCompetition(
            normalizedCommunity,
            competition);
        if (!string.Equals(expectedCompetition, communityProfile.Competition, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Development community '{normalizedCommunity}' is mapped to '{expectedCompetition}', " +
                $"but its collection profile declares '{communityProfile.Competition}'.");
        }

        return communityProfile;
    }

    public CompetitionCollectionProfile ResolveCompetition(string competition)
    {
        var canonicalCompetition = CompetitionIds.Canonicalize(competition);
        return ProfilesByCompetition.TryGetValue(canonicalCompetition, out var profile)
            ? profile
            : throw new NotSupportedException($"Competition '{canonicalCompetition}' has no collection profile.");
    }

    private static CompetitionCollectionProfile CreateProfile(
        string competition,
        string displayName,
        string[] supportedDevelopmentCommunities,
        CompetitionCollectorStep[] collectors,
        string[] requiredMatchDocumentTemplates,
        string[] requiredAggregateContextDocuments,
        string[] requiredKpiDocuments,
        int expectedTeamCount,
        int expectedMatchCount,
        int? expectedMatchesPerMatchday,
        DateOnly seasonStartsOn,
        DateOnly seasonEndsOn,
        CompetitionPromptRoute promptRoute,
        CompetitionContextFeatures contextFeatures,
        string[] validationCommands)
    {
        EnsureUniqueNonBlank(competition, "development communities", supportedDevelopmentCommunities, requireNonempty: true);
        EnsureUniqueNonBlank(competition, "required match-document templates", requiredMatchDocumentTemplates, requireNonempty: true);
        EnsureUniqueNonBlank(competition, "required aggregate context documents", requiredAggregateContextDocuments, requireNonempty: false);
        EnsureUniqueNonBlank(competition, "required KPI documents", requiredKpiDocuments, requireNonempty: false);
        EnsureUniqueNonBlank(competition, "validation commands", validationCommands, requireNonempty: true);

        if (collectors.Length == 0 || collectors.Select(step => step.Collector).Distinct().Count() != collectors.Length)
        {
            throw new InvalidOperationException($"Competition profile '{competition}' must declare a nonempty unique collector order.");
        }

        for (var index = 0; index < collectors.Length; index++)
        {
            if (collectors[index].ExecutionMode != CompetitionCollectorExecutionMode.IncludedInPrevious)
            {
                continue;
            }

            if (index == 0 || collectors[index - 1].ExecutionMode != CompetitionCollectorExecutionMode.Direct)
            {
                throw new InvalidOperationException(
                    $"Competition profile '{competition}' has an IncludedInPrevious collector without an immediately preceding direct collector.");
            }
        }

        if (expectedTeamCount <= 0 || expectedMatchCount <= 0 || expectedMatchesPerMatchday <= 0)
        {
            throw new InvalidOperationException($"Competition profile '{competition}' contains invalid expected counts.");
        }

        if (seasonStartsOn > seasonEndsOn)
        {
            throw new InvalidOperationException($"Competition profile '{competition}' has inverted season bounds.");
        }

        if (string.IsNullOrWhiteSpace(promptRoute.Source)
            || string.IsNullOrWhiteSpace(promptRoute.MatchPromptName)
            || string.IsNullOrWhiteSpace(promptRoute.BonusPromptName)
            || string.IsNullOrWhiteSpace(promptRoute.Label)
            || string.IsNullOrWhiteSpace(promptRoute.FallbackModel)
            || promptRoute.MatchPromptVersion is <= 0
            || promptRoute.BonusPromptVersion is <= 0)
        {
            throw new InvalidOperationException($"Competition profile '{competition}' contains an incomplete prompt route.");
        }

        return new CompetitionCollectionProfile(
            competition,
            displayName,
            Array.AsReadOnly(supportedDevelopmentCommunities),
            Array.AsReadOnly(collectors),
            Array.AsReadOnly(requiredMatchDocumentTemplates),
            Array.AsReadOnly(requiredAggregateContextDocuments),
            Array.AsReadOnly(requiredKpiDocuments),
            expectedTeamCount,
            expectedMatchCount,
            expectedMatchesPerMatchday,
            seasonStartsOn,
            seasonEndsOn,
            promptRoute,
            contextFeatures,
            Array.AsReadOnly(validationCommands));
    }

    private static IReadOnlyDictionary<string, CompetitionCollectionProfile> CreateDevelopmentProfiles()
    {
        var profiles = ProfilesByCompetition.Values
            .SelectMany(profile => profile.SupportedDevelopmentCommunities.Select(community => (community, profile)))
            .ToDictionary(pair => pair.community, pair => pair.profile, StringComparer.OrdinalIgnoreCase);

        foreach (var (community, profile) in profiles)
        {
            var configuredCompetition = CompetitionResolver.ResolveDevelopmentCompetition(community);
            if (!string.Equals(configuredCompetition, profile.Competition, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Development community '{community}' is mapped to '{configuredCompetition}', " +
                    $"but its collection profile declares '{profile.Competition}'.");
            }
        }

        if (!profiles.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(CompetitionResolver.SupportedDevCommunities))
        {
            throw new InvalidOperationException(
                "Competition collection profiles must cover the exact supported development-community set.");
        }

        return new ReadOnlyDictionary<string, CompetitionCollectionProfile>(profiles);
    }

    private static void EnsureUniqueNonBlank(
        string competition,
        string label,
        IReadOnlyList<string> values,
        bool requireNonempty)
    {
        if ((requireNonempty && values.Count == 0)
            || values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new InvalidOperationException(
                $"Competition profile '{competition}' must declare {(requireNonempty ? "a nonempty " : string.Empty)}unique, nonblank {label}.");
        }
    }
}
