using EHonda.KicktippAi.Core;

namespace Orchestrator.Infrastructure;

public sealed record CompetitionRuntimeMetadata(
    string Competition,
    string PromptSource,
    string PromptName,
    string PromptLabel,
    int? PromptVersion,
    string FallbackPromptModel);

public static class CompetitionResolver
{
    public const string BundesligaDevelopmentCommunity = "ehonda-dev-buli-2627";
    public const string WorldCupDevelopmentCommunity = "ehonda-dev-wm26";

    private static readonly IReadOnlyDictionary<string, string> KnownDevCommunityCompetitions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BundesligaDevelopmentCommunity] = CompetitionIds.Bundesliga2026_27,
            [WorldCupDevelopmentCommunity] = CompetitionIds.FifaWorldCup2026
        };
    private static readonly string[] KnownDevCommunities =
        KnownDevCommunityCompetitions.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static readonly string[] KnownWorldCupCommunities = [WorldCupDevelopmentCommunity, "rabetrabauken2026", "ehonda-ai-arena"];

    public const string LocalPromptSource = "local";
    public const string LangfusePromptSource = "langfuse";
    public const string WorldCupMatchPromptName = "kicktippai/wm26/predict-one-match";
    public const string WorldCupBonusPromptName = "kicktippai/wm26/predict-bonus";
    public const string DefaultWorldCupPromptLabel = "latest";
    public const string WorldCupFallbackPromptModel = "wm26";
    public const string BundesligaMatchPromptName = "kicktippai/bundesliga-2026-27/predict-one-match";
    public const string BundesligaBonusPromptName = "kicktippai/bundesliga-2026-27/predict-bonus";
    public const string DefaultBundesligaPromptLabel = "production";
    public const int BundesligaMatchPromptVersion = 2;
    public const int BundesligaBonusPromptVersion = 1;
    public const string BundesligaFallbackPromptModel = "bundesliga-2026-27";
    public const string BundesligaValidationModel = "gpt-5.6-luna";
    public const string BundesligaValidationReasoningEffort = "none";
    public const int BundesligaValidationMaxOutputTokenCount = 10_000;

    public static string ResolveCompetition(
        string? competition,
        string? community = null,
        string? communityContext = null)
    {
        if (!string.IsNullOrWhiteSpace(competition))
        {
            return CompetitionIds.Canonicalize(competition);
        }

        if (IsWorldCupCommunity(community) || IsWorldCupCommunity(communityContext))
        {
            return CompetitionIds.FifaWorldCup2026;
        }

        return CompetitionIds.Bundesliga2026_27;
    }

    public static string ResolveTargetCompetition(
        string? competition,
        string communityContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityContext);
        var normalizedCommunityContext = communityContext.Trim();
        var resolvedCompetition = ResolveCompetition(
            competition,
            communityContext: normalizedCommunityContext);
        if (KnownDevCommunityCompetitions.TryGetValue(normalizedCommunityContext, out var expectedCompetition)
            && !string.Equals(resolvedCompetition, expectedCompetition, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Target community context '{normalizedCommunityContext}' belongs to '{expectedCompetition}' and " +
                $"conflicts with the resolved competition '{resolvedCompetition}'.");
        }

        return resolvedCompetition;
    }

    public static CompetitionRuntimeMetadata ResolveRuntimeMetadata(
        string? competition,
        string? community,
        string? communityContext,
        string? promptSource,
        string? langfusePromptName,
        string? langfusePromptLabel,
        bool bonusPrompt)
    {
        var resolvedCompetition = ResolveCompetition(competition, community, communityContext);
        var isWorldCup = string.Equals(resolvedCompetition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase);
        var isBundesliga2026_27 = string.Equals(
            resolvedCompetition,
            CompetitionIds.Bundesliga2026_27,
            StringComparison.OrdinalIgnoreCase);
        var resolvedPromptSource = string.IsNullOrWhiteSpace(promptSource)
            ? LangfusePromptSource
            : promptSource.Trim().ToLowerInvariant();

        var defaultPromptName = isWorldCup
            ? bonusPrompt ? WorldCupBonusPromptName : WorldCupMatchPromptName
            : bonusPrompt ? BundesligaBonusPromptName : BundesligaMatchPromptName;
        var promptName = string.IsNullOrWhiteSpace(langfusePromptName)
            ? defaultPromptName
            : langfusePromptName.Trim();

        var promptLabel = string.IsNullOrWhiteSpace(langfusePromptLabel)
            ? isWorldCup ? DefaultWorldCupPromptLabel : DefaultBundesligaPromptLabel
            : langfusePromptLabel.Trim();
        int? promptVersion = isBundesliga2026_27 &&
                             string.Equals(promptLabel, DefaultBundesligaPromptLabel, StringComparison.OrdinalIgnoreCase)
            ? promptName switch
            {
                BundesligaMatchPromptName => BundesligaMatchPromptVersion,
                BundesligaBonusPromptName => BundesligaBonusPromptVersion,
                _ => null
            }
            : null;

        return new CompetitionRuntimeMetadata(
            resolvedCompetition,
            resolvedPromptSource,
            promptName,
            promptLabel,
            promptVersion,
            isWorldCup ? WorldCupFallbackPromptModel : BundesligaFallbackPromptModel);
    }

    public static bool IsWorldCupCompetition(string competition)
    {
        return string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> SupportedDevCommunities => KnownDevCommunities;

    public static string ResolveDevelopmentCompetition(string community, string? competition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(community);
        var normalizedCommunity = community.Trim();
        if (!KnownDevCommunityCompetitions.TryGetValue(normalizedCommunity, out var expectedCompetition))
        {
            throw new NotSupportedException(
                $"Development community '{normalizedCommunity}' is not supported. " +
                $"Supported communities: {string.Join(", ", SupportedDevCommunities)}.");
        }

        if (string.IsNullOrWhiteSpace(competition))
        {
            return expectedCompetition;
        }

        string canonicalCompetition;
        try
        {
            canonicalCompetition = CompetitionIds.Canonicalize(competition);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new NotSupportedException(
                $"Competition '{competition.Trim()}' is not supported for development commands.",
                exception);
        }
        if (!string.Equals(canonicalCompetition, expectedCompetition, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Development community '{normalizedCommunity}' uses competition '{expectedCompetition}', " +
                $"not '{canonicalCompetition}'.");
        }

        return expectedCompetition;
    }

    public static bool IsDevCommunity(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && KnownDevCommunityCompetitions.ContainsKey(value.Trim());
    }

    private static bool IsWorldCupCommunity(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && KnownWorldCupCommunities.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
