using System.Text;
using Spectre.Console;

namespace Orchestrator.Commands.Operations.Dev;

internal sealed record CompetitionProfileCollectionRequest(
    CompetitionCollectionProfile Profile,
    string Community,
    string CommunityContext,
    string? Matchdays,
    bool FullSeason,
    string RecentHistoryDateMap,
    bool DryRun,
    bool Verbose,
    string? MarkdownSummaryOutput = null);

internal static class CompetitionProfileCollectionRunner
{
    public static async Task<int> ExecuteAsync(
        IAnsiConsole console,
        ICompetitionProfileCollectorExecutor collectorExecutor,
        CompetitionProfileCollectionRequest request,
        CancellationToken cancellationToken)
    {
        var executionContext = new CompetitionCollectorExecutionContext(
            request.Profile,
            request.Community,
            request.CommunityContext,
            request.Matchdays,
            request.FullSeason,
            request.RecentHistoryDateMap,
            request.DryRun,
            request.Verbose);

        PrintProfile(console, request.Profile, request.Community, request.CommunityContext);
        console.MarkupLine(
            $"[blue]Kicktipp scope:[/] [yellow]{(request.FullSeason ? "full season" : "current or explicit matchday")}[/]");

        var dispositions = new List<(CompetitionCollector Collector, string Disposition)>();
        for (var index = 0; index < request.Profile.Collectors.Count; index++)
        {
            var step = request.Profile.Collectors[index];
            if (step.ExecutionMode == CompetitionCollectorExecutionMode.IncludedInPrevious)
            {
                var previousCollector = request.Profile.Collectors[index - 1].Collector;
                var disposition = request.DryRun ? "IncludedInPreviousDryRun" : "IncludedInPrevious";
                dispositions.Add((step.Collector, disposition));
                console.MarkupLine(
                    $"[green]Collector {step.Collector}:[/] [yellow]{disposition}[/] " +
                    $"(completed inside immediately preceding {previousCollector})");
                continue;
            }

            console.MarkupLine($"[blue]Running collector:[/] [yellow]{step.Collector}[/]");
            int exitCode;
            try
            {
                exitCode = await collectorExecutor.ExecuteAsync(step.Collector, executionContext, cancellationToken);
            }
            catch (Exception exception)
            {
                console.MarkupLine(
                    $"[red]Collector {step.Collector}: Failed[/] ({Markup.Escape(exception.Message)})");
                dispositions.Add((step.Collector, "Failed"));
                PrintSkippedCollectors(console, request.Profile.Collectors.Skip(index + 1), dispositions);
                PrintSummary(console, dispositions);
                return AppendMarkdownSummary(console, request, dispositions, 1);
            }

            if (exitCode != 0)
            {
                console.MarkupLine($"[red]Collector {step.Collector}: Failed[/] (exit code {exitCode})");
                dispositions.Add((step.Collector, "Failed"));
                PrintSkippedCollectors(console, request.Profile.Collectors.Skip(index + 1), dispositions);
                PrintSummary(console, dispositions);
                return AppendMarkdownSummary(console, request, dispositions, exitCode);
            }

            var succeededDisposition = request.DryRun ? "DryRunValidated" : "Succeeded";
            dispositions.Add((step.Collector, succeededDisposition));
            console.MarkupLine($"[green]Collector {step.Collector}:[/] [yellow]{succeededDisposition}[/]");
        }

        PrintSummary(console, dispositions);
        console.MarkupLine(
            request.DryRun
                ? "[magenta]✓ Competition profile dry run completed; every selected collector was validated without writes[/]"
                : "[green]✓ Competition profile collection completed[/]");
        return AppendMarkdownSummary(console, request, dispositions, 0);
    }

    private static void PrintProfile(
        IAnsiConsole console,
        CompetitionCollectionProfile profile,
        string community,
        string communityContext)
    {
        console.MarkupLine($"[yellow]Collection profile:[/] {Markup.Escape(profile.DisplayName)}");
        console.MarkupLine($"[blue]Target community:[/] [yellow]{Markup.Escape(community)}[/]");
        console.MarkupLine($"[blue]Community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
        console.MarkupLine($"[blue]Competition:[/] [yellow]{Markup.Escape(profile.Competition)}[/]");
        console.MarkupLine(
            $"[blue]Collectors:[/] [yellow]{Markup.Escape(string.Join(" -> ", profile.Collectors.Select(step => step.Collector)))}[/]");
        console.MarkupLine(
            $"[blue]Expected scope:[/] [yellow]{profile.ExpectedTeamCount} teams, {profile.ExpectedMatchCount} matches, " +
            $"{(profile.ExpectedMatchesPerMatchday?.ToString() ?? "variable")} per matchday[/]");
        console.MarkupLine(
            $"[blue]Season:[/] [yellow]{profile.SeasonStartsOn:yyyy-MM-dd} through {profile.SeasonEndsOn:yyyy-MM-dd}[/]");
        console.MarkupLine(
            $"[blue]Prompt route:[/] [yellow]{Markup.Escape(profile.PromptRoute.Source)}; " +
            $"match={Markup.Escape(profile.PromptRoute.MatchPromptName)}; " +
            $"match-version={RenderPromptVersion(profile.PromptRoute.MatchPromptVersion)}; " +
            $"bonus={Markup.Escape(profile.PromptRoute.BonusPromptName)}; " +
            $"bonus-version={RenderPromptVersion(profile.PromptRoute.BonusPromptVersion)}; " +
            $"label={Markup.Escape(profile.PromptRoute.Label)}; " +
            $"fallback={Markup.Escape(profile.PromptRoute.FallbackModel)}[/]");
        console.MarkupLine(
            $"[blue]Context features:[/] [yellow]home-away={profile.ContextFeatures.HomeAwayHistory}, " +
            $"head-to-head={profile.ContextFeatures.HeadToHeadHistory}, " +
            $"knockout={profile.ContextFeatures.KnockoutRules}, transfers={profile.ContextFeatures.Transfers}[/]");
        PrintList(console, "Required match documents", profile.RequiredMatchDocumentTemplates);
        PrintList(console, "Required aggregate context documents", profile.RequiredAggregateContextDocuments);
        PrintList(console, "Required KPI documents", profile.RequiredKpiDocuments);
        PrintList(console, "Validation commands", profile.ValidationCommands);
    }

    private static string RenderPromptVersion(int? version)
    {
        return version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "label-resolved";
    }

    private static void PrintList(IAnsiConsole console, string label, IReadOnlyList<string> values)
    {
        var rendered = values.Count == 0 ? "<none>" : string.Join(", ", values);
        console.MarkupLine($"[blue]{label}:[/] [yellow]{Markup.Escape(rendered)}[/]");
    }

    private static void PrintSkippedCollectors(
        IAnsiConsole console,
        IEnumerable<CompetitionCollectorStep> remainingSteps,
        ICollection<(CompetitionCollector Collector, string Disposition)> dispositions)
    {
        foreach (var remainingStep in remainingSteps)
        {
            dispositions.Add((remainingStep.Collector, "SkippedAfterFailure"));
            console.MarkupLine(
                $"[yellow]Collector {remainingStep.Collector}: SkippedAfterFailure[/] (an earlier collector failed)");
        }
    }

    private static void PrintSummary(
        IAnsiConsole console,
        IEnumerable<(CompetitionCollector Collector, string Disposition)> dispositions)
    {
        console.MarkupLine("[blue]Collector dispositions:[/]");
        foreach (var (collector, disposition) in dispositions)
        {
            console.MarkupLine($"[blue]  {collector}:[/] [yellow]{disposition}[/]");
        }
    }

    private static int AppendMarkdownSummary(
        IAnsiConsole console,
        CompetitionProfileCollectionRequest request,
        IReadOnlyList<(CompetitionCollector Collector, string Disposition)> dispositions,
        int exitCode)
    {
        if (string.IsNullOrWhiteSpace(request.MarkdownSummaryOutput))
        {
            return exitCode;
        }

        var lines = new List<string>
        {
            "## Context Collection Profile Results",
            string.Empty,
            $"- **Resolved profile:** {EscapeMarkdown(request.Profile.DisplayName)} (`{EscapeCode(request.Profile.Competition)}`)",
            $"- **Community context:** `{EscapeCode(request.CommunityContext)}`",
            $"- **Mode:** {(request.DryRun ? "dry run" : "write")}",
            $"- **Kicktipp scope:** {(request.FullSeason ? "full season" : "current or explicit matchday")}",
            $"- **Result:** {(exitCode == 0 ? "succeeded" : $"failed (exit code {exitCode})")}",
            "- **Collector results:**"
        };
        lines.AddRange(dispositions.Select(disposition =>
            $"  - `{disposition.Collector}`: `{disposition.Disposition}`"));
        lines.Add(string.Empty);

        try
        {
            File.AppendAllLines(request.MarkdownSummaryOutput.Trim(), lines, new UTF8Encoding(false));
            return exitCode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            console.MarkupLine(
                $"[red]Error:[/] Could not write collection summary: {Markup.Escape(exception.Message)}");
            return exitCode == 0 ? 1 : exitCode;
        }
    }

    private static string EscapeMarkdown(string value) => value.Replace("`", "\\`", StringComparison.Ordinal);

    private static string EscapeCode(string value) => value.Replace("`", "\\`", StringComparison.Ordinal);
}
