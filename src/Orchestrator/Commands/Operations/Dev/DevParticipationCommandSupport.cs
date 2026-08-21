using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Spectre.Console;

namespace Orchestrator.Commands.Operations.Dev;

internal static class DevParticipationCommandSupport
{
    public static bool TryCreateBaseSettings(
        DevParticipationSettings settings,
        IAnsiConsole console,
        string commandLabel,
        bool bonusPrompt,
        bool showContextDocuments,
        out BaseSettings baseSettings)
    {
        baseSettings = null!;

        if (!CompetitionResolver.IsDevCommunity(settings.Community))
        {
            var supportedCommunities = string.Join(", ", CompetitionResolver.SupportedDevCommunities);
            console.MarkupLine(
                $"[red]Error:[/] {Markup.Escape(commandLabel)} is only available for supported development communities: [yellow]{Markup.Escape(supportedCommunities)}[/]");
            return false;
        }

        var community = settings.Community.Trim();
        string competition;
        try
        {
            competition = CompetitionResolver.ResolveDevelopmentCompetition(community, settings.Competition);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return false;
        }
        var usesBundesligaValidationIdentity = string.Equals(
            competition,
            CompetitionIds.Bundesliga2026_27,
            StringComparison.Ordinal);
        console.MarkupLine(
            $"[yellow]{Markup.Escape(commandLabel)} dev preset enabled - will override database and Kicktipp predictions for {Markup.Escape(community)}[/]");

        if (usesBundesligaValidationIdentity)
        {
            var promptName = bonusPrompt
                ? CompetitionResolver.BundesligaBonusPromptName
                : CompetitionResolver.BundesligaMatchPromptName;
            var promptVersion = bonusPrompt
                ? CompetitionResolver.BundesligaBonusPromptVersion
                : CompetitionResolver.BundesligaMatchPromptVersion;
            console.MarkupLine(
                $"[blue]Bundesliga validation identity:[/] [yellow]model={CompetitionResolver.BundesligaValidationModel}; " +
                $"reasoning={CompetitionResolver.BundesligaValidationReasoningEffort}; " +
                $"max-output-tokens={CompetitionResolver.BundesligaValidationMaxOutputTokenCount}; " +
                $"prompt={promptName}; prompt-version={promptVersion}[/]");
        }

        baseSettings = new BaseSettings
        {
            Model = usesBundesligaValidationIdentity
                ? CompetitionResolver.BundesligaValidationModel
                : PredictionServiceCommandSupport.WorldCupDevDefaultModel,
            ReasoningEffort = usesBundesligaValidationIdentity
                ? CompetitionResolver.BundesligaValidationReasoningEffort
                : PredictionServiceCommandSupport.WorldCupDevDefaultReasoningEffort,
            MaxOutputTokenCount = usesBundesligaValidationIdentity
                ? CompetitionResolver.BundesligaValidationMaxOutputTokenCount
                : null,
            Community = community,
            CommunityContext = string.IsNullOrWhiteSpace(settings.CommunityContext)
                ? null
                : settings.CommunityContext.Trim(),
            Competition = competition,
            PromptSource = usesBundesligaValidationIdentity ? CompetitionResolver.LangfusePromptSource : null,
            LangfusePromptName = usesBundesligaValidationIdentity
                ? bonusPrompt
                    ? CompetitionResolver.BundesligaBonusPromptName
                    : CompetitionResolver.BundesligaMatchPromptName
                : null,
            LangfusePromptLabel = usesBundesligaValidationIdentity
                ? CompetitionResolver.DefaultBundesligaPromptLabel
                : null,
            LangfusePromptVersion = usesBundesligaValidationIdentity
                ? bonusPrompt
                    ? CompetitionResolver.BundesligaBonusPromptVersion
                    : CompetitionResolver.BundesligaMatchPromptVersion
                : null,
            Verbose = settings.Verbose,
            OverrideKicktipp = true,
            OverrideDatabase = true,
            DryRun = false,
            Agent = false,
            ShowContextDocuments = showContextDocuments,
            WithJustification = false
        };

        return true;
    }
}
