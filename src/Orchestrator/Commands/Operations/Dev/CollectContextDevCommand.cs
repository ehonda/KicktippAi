using Spectre.Console;
using Spectre.Console.Cli;

namespace Orchestrator.Commands.Operations.Dev;

public sealed class CollectContextDevCommand : AsyncCommand<CollectContextDevSettings>
{
    private readonly IAnsiConsole _console;
    private readonly ICompetitionCollectionProfileResolver _profileResolver;
    private readonly ICompetitionProfileCollectorExecutor _collectorExecutor;

    public CollectContextDevCommand(
        IAnsiConsole console,
        ICompetitionCollectionProfileResolver profileResolver,
        ICompetitionProfileCollectorExecutor collectorExecutor)
    {
        _console = console;
        _profileResolver = profileResolver;
        _collectorExecutor = collectorExecutor;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CollectContextDevSettings settings,
        CancellationToken cancellationToken)
    {
        CompetitionCollectionProfile profile;
        try
        {
            profile = _profileResolver.ResolveForDevelopment(settings.Community, settings.Competition);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _console.MarkupLine($"[red]Error:[/] {Markup.Escape(exception.Message)}");
            return 1;
        }

        var community = settings.Community.Trim();
        var communityContext = string.IsNullOrWhiteSpace(settings.CommunityContext)
            ? community
            : settings.CommunityContext.Trim();
        var executionContext = new CompetitionCollectorExecutionContext(
            profile,
            community,
            communityContext,
            settings.Matchdays,
            settings.RecentHistoryDateMap,
            settings.DryRun,
            settings.Verbose);

        PrintProfile(profile, community, communityContext);

        var dispositions = new List<(CompetitionCollector Collector, string Disposition)>();
        for (var index = 0; index < profile.Collectors.Count; index++)
        {
            var step = profile.Collectors[index];
            if (step.ExecutionMode == CompetitionCollectorExecutionMode.IncludedInPrevious)
            {
                var previousCollector = profile.Collectors[index - 1].Collector;
                var disposition = settings.DryRun ? "IncludedInPreviousDryRun" : "IncludedInPrevious";
                dispositions.Add((step.Collector, disposition));
                _console.MarkupLine(
                    $"[green]Collector {step.Collector}:[/] [yellow]{disposition}[/] " +
                    $"(completed inside immediately preceding {previousCollector})");
                continue;
            }

            _console.MarkupLine($"[blue]Running collector:[/] [yellow]{step.Collector}[/]");
            int exitCode;
            try
            {
                exitCode = await _collectorExecutor.ExecuteAsync(step.Collector, executionContext, cancellationToken);
            }
            catch (Exception exception)
            {
                _console.MarkupLine(
                    $"[red]Collector {step.Collector}: Failed[/] ({Markup.Escape(exception.Message)})");
                dispositions.Add((step.Collector, "Failed"));
                PrintSkippedCollectors(profile.Collectors.Skip(index + 1), dispositions);
                PrintSummary(dispositions);
                return 1;
            }

            if (exitCode != 0)
            {
                _console.MarkupLine($"[red]Collector {step.Collector}: Failed[/] (exit code {exitCode})");
                dispositions.Add((step.Collector, "Failed"));
                PrintSkippedCollectors(profile.Collectors.Skip(index + 1), dispositions);
                PrintSummary(dispositions);
                return exitCode;
            }

            var succeededDisposition = settings.DryRun ? "DryRunValidated" : "Succeeded";
            dispositions.Add((step.Collector, succeededDisposition));
            _console.MarkupLine($"[green]Collector {step.Collector}:[/] [yellow]{succeededDisposition}[/]");
        }

        PrintSummary(dispositions);
        _console.MarkupLine(
            settings.DryRun
                ? "[magenta]✓ Competition profile dry run completed; every selected collector was validated without writes[/]"
                : "[green]✓ Competition profile collection completed[/]");
        return 0;
    }

    private void PrintProfile(CompetitionCollectionProfile profile, string community, string communityContext)
    {
        _console.MarkupLine($"[yellow]Collection profile:[/] {Markup.Escape(profile.DisplayName)}");
        _console.MarkupLine($"[blue]Target community:[/] [yellow]{Markup.Escape(community)}[/]");
        _console.MarkupLine($"[blue]Community context:[/] [yellow]{Markup.Escape(communityContext)}[/]");
        _console.MarkupLine($"[blue]Competition:[/] [yellow]{Markup.Escape(profile.Competition)}[/]");
        _console.MarkupLine(
            $"[blue]Collectors:[/] [yellow]{Markup.Escape(string.Join(" -> ", profile.Collectors.Select(step => step.Collector)))}[/]");
        _console.MarkupLine(
            $"[blue]Expected scope:[/] [yellow]{profile.ExpectedTeamCount} teams, {profile.ExpectedMatchCount} matches, " +
            $"{(profile.ExpectedMatchesPerMatchday?.ToString() ?? "variable")} per matchday[/]");
        _console.MarkupLine(
            $"[blue]Season:[/] [yellow]{profile.SeasonStartsOn:yyyy-MM-dd} through {profile.SeasonEndsOn:yyyy-MM-dd}[/]");
        _console.MarkupLine(
            $"[blue]Prompt route:[/] [yellow]{Markup.Escape(profile.PromptRoute.Source)}; " +
            $"match={Markup.Escape(profile.PromptRoute.MatchPromptName)}; " +
            $"match-version={RenderPromptVersion(profile.PromptRoute.MatchPromptVersion)}; " +
            $"bonus={Markup.Escape(profile.PromptRoute.BonusPromptName)}; " +
            $"bonus-version={RenderPromptVersion(profile.PromptRoute.BonusPromptVersion)}; " +
            $"label={Markup.Escape(profile.PromptRoute.Label)}; " +
            $"fallback={Markup.Escape(profile.PromptRoute.FallbackModel)}[/]");
        _console.MarkupLine(
            $"[blue]Context features:[/] [yellow]home-away={profile.ContextFeatures.HomeAwayHistory}, " +
            $"head-to-head={profile.ContextFeatures.HeadToHeadHistory}, " +
            $"knockout={profile.ContextFeatures.KnockoutRules}, transfers={profile.ContextFeatures.Transfers}[/]");
        PrintList("Required match documents", profile.RequiredMatchDocumentTemplates);
        PrintList("Required aggregate context documents", profile.RequiredAggregateContextDocuments);
        PrintList("Required KPI documents", profile.RequiredKpiDocuments);
        PrintList("Validation commands", profile.ValidationCommands);
    }

    private static string RenderPromptVersion(int? version)
    {
        return version?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "label-resolved";
    }

    private void PrintList(string label, IReadOnlyList<string> values)
    {
        var rendered = values.Count == 0 ? "<none>" : string.Join(", ", values);
        _console.MarkupLine($"[blue]{label}:[/] [yellow]{Markup.Escape(rendered)}[/]");
    }

    private void PrintSkippedCollectors(
        IEnumerable<CompetitionCollectorStep> remainingSteps,
        ICollection<(CompetitionCollector Collector, string Disposition)> dispositions)
    {
        foreach (var remainingStep in remainingSteps)
        {
            dispositions.Add((remainingStep.Collector, "SkippedAfterFailure"));
            _console.MarkupLine(
                $"[yellow]Collector {remainingStep.Collector}: SkippedAfterFailure[/] (an earlier collector failed)");
        }
    }

    private void PrintSummary(IEnumerable<(CompetitionCollector Collector, string Disposition)> dispositions)
    {
        _console.MarkupLine("[blue]Collector dispositions:[/]");
        foreach (var (collector, disposition) in dispositions)
        {
            _console.MarkupLine($"[blue]  {collector}:[/] [yellow]{disposition}[/]");
        }
    }
}
