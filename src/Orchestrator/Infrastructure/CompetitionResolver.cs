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
    public const string LocalPromptSource = "local";
    public const string LangfusePromptSource = "langfuse";
    public const string WorldCupMatchPromptName = "kicktippai/wm26/predict-one-match";
    public const string WorldCupBonusPromptName = "kicktippai/wm26/predict-bonus";
    public const string DefaultWorldCupPromptLabel = "latest";
    public const string WorldCupFallbackPromptModel = "wm26";

    public static string ResolveCompetition(
        string? competition,
        string? community = null,
        string? communityContext = null)
    {
        if (!string.IsNullOrWhiteSpace(competition))
        {
            return competition.Trim();
        }

        if (IsWorldCupCommunity(community) || IsWorldCupCommunity(communityContext))
        {
            return CompetitionIds.FifaWorldCup2026;
        }

        return CompetitionIds.Bundesliga2025_26;
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
            ? isWorldCup ? LangfusePromptSource : LocalPromptSource
            : promptSource.Trim().ToLowerInvariant();

        var defaultPromptName = bonusPrompt ? WorldCupBonusPromptName : WorldCupMatchPromptName;
        var promptName = string.IsNullOrWhiteSpace(langfusePromptName)
            ? isWorldCup ? defaultPromptName : string.Empty
            : langfusePromptName.Trim();

        var promptLabel = string.IsNullOrWhiteSpace(langfusePromptLabel)
            ? isWorldCup ? DefaultWorldCupPromptLabel : string.Empty
            : langfusePromptLabel.Trim();

        return new CompetitionRuntimeMetadata(
            resolvedCompetition,
            resolvedPromptSource,
            promptName,
            promptLabel,
            isWorldCup ? WorldCupFallbackPromptModel : string.Empty);
    }

    public static string? ToRepositoryCompetitionArgument(string competition)
    {
        return string.Equals(competition, CompetitionIds.Bundesliga2025_26, StringComparison.OrdinalIgnoreCase)
            ? null
            : competition;
    }

    public static bool IsWorldCupCompetition(string competition)
    {
        return string.Equals(competition, CompetitionIds.FifaWorldCup2026, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorldCupCommunity(string? value)
    {
        return string.Equals(value, "ehonda-dev-wm26", StringComparison.OrdinalIgnoreCase);
    }
}
