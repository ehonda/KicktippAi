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
        console.MarkupLine(
            $"[yellow]{Markup.Escape(commandLabel)} dev preset enabled - will override database and Kicktipp predictions for {Markup.Escape(community)}[/]");

        baseSettings = new BaseSettings
        {
            Model = PredictionServiceCommandSupport.WorldCupDevDefaultModel,
            ReasoningEffort = PredictionServiceCommandSupport.WorldCupDevDefaultReasoningEffort,
            Community = community,
            CommunityContext = string.IsNullOrWhiteSpace(settings.CommunityContext)
                ? null
                : settings.CommunityContext.Trim(),
            Competition = string.IsNullOrWhiteSpace(settings.Competition)
                ? null
                : settings.Competition.Trim(),
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
