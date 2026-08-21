using EHonda.KicktippAi.Core;

namespace Orchestrator.Infrastructure;

public sealed record CompetitionRuntimeMetadata(
    string Competition,
    string PromptSource,
    string PromptName,
    string PromptLabel,
    string FallbackPromptModel);

public static class CompetitionResolver
{
    private static readonly string[] KnownDevCommunities = ["ehonda-dev-wm26"];
    private static readonly string[] KnownWorldCupCommunities = ["ehonda-dev-wm26", "rabetrabauken2026", "ehonda-ai-arena"];

    public const string LocalPromptSource = "local";
    public const string LangfusePromptSource = "langfuse";
    public const string WorldCupMatchPromptName = "kicktippai/wm26/predict-one-match";
    public const string WorldCupBonusPromptName = "kicktippai/wm26/predict-bonus";
    public const string DefaultWorldCupPromptLabel = "latest";
    public const string WorldCupFallbackPromptModel = "wm26";
    public const string BundesligaMatchPromptName = "kicktippai/bundesliga-2026-27/predict-one-match";
    public const string BundesligaBonusPromptName = "kicktippai/bundesliga-2026-27/predict-bonus";
    public const string DefaultBundesligaPromptLabel = "production";
    public const string BundesligaFallbackPromptModel = "bundesliga-2026-27";

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

        return new CompetitionRuntimeMetadata(
            resolvedCompetition,
            resolvedPromptSource,
            promptName,
            promptLabel,
            isWorldCup ? WorldCupFallbackPromptModel : BundesligaFallbackPromptModel);
    }

    public static bool IsWorldCupCompetition(string competition)
    {
        return string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> SupportedDevCommunities => KnownDevCommunities;

    public static bool IsDevCommunity(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && KnownDevCommunities.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsWorldCupCommunity(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && KnownWorldCupCommunities.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
