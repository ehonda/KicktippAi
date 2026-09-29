using System.Text;
using Orchestrator.Commands.Operations.CollectContext;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
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
    string? MarkdownSummaryOutput = null,
    CompetitionContextSourceCycleInvocation? ContextSourceCycle = null,
    bool ContextSourceOnly = false);

internal static class CompetitionProfileCollectionRunner
{
    public static async Task<int> ExecuteAsync(
        IAnsiConsole console,
        ICompetitionProfileCollectorExecutor collectorExecutor,
        CompetitionProfileCollectionRequest request,
        CancellationToken cancellationToken,
        IServiceProvider? services = null)
    {
        try
        {
            if (request.ContextSourceOnly)
            {
                ValidateSourceOnly(request.Profile, request.Matchdays, request.FullSeason);
                if (request.ContextSourceCycle is null)
                    BundesligaContextSourceContract.ValidateConsumerAuthority(
                        BundesligaContextSourceScope.Development,
                        BundesligaContextSourceContract.DevelopmentLane, request.CommunityContext);
                request = request with { Profile = request.Profile with { Collectors = [new(CompetitionCollector.ClubElo)] } };
            }
            var result = await ExecutePreparedAsync(console, collectorExecutor, request, cancellationToken);
            if (result == 0 && request.ContextSourceOnly && !request.DryRun
                && request.ContextSourceCycle is { Identity.Scope: BundesligaContextSourceScope.ProductionLive } invocation)
                await ValidateProductionCompletionAsync(request, invocation, services, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            console.MarkupLine("[red]Context collection failed during preparation, reconciliation or cleanup.[/]");
            return 1;
        }
    }

    internal static async Task ValidateProductionCompletionAsync(CompetitionProfileCollectionRequest request,
        CompetitionContextSourceCycleInvocation invocation, IServiceProvider? services, CancellationToken cancellationToken)
    {
        var repository = services?.GetRequiredService<IBundesligaContextSourceCycleRepository>()
            ?? throw new InvalidOperationException("Production source-only reconciliation requires its durable repository.");
        var receipt = await repository.GetReceiptAsync(invocation.Identity, BundesligaContextSource.ClubElo, invocation.CurrentLaneId, cancellationToken)
            ?? throw new InvalidDataException("Source-only collection did not persist the exact lane receipt.");
        receipt.Validate();
        if (receipt.Request.Identity != invocation.Identity || receipt.Request.ConsumerLaneId != invocation.CurrentLaneId
            || receipt.Request.CommunityContext != request.CommunityContext || receipt.Request.Source != BundesligaContextSource.ClubElo)
            throw new InvalidDataException("Source-only receipt contradicts the requested identity.");
        var cycle = await repository.GetCycleAsync(invocation.Identity, cancellationToken)
            ?? throw new InvalidDataException("Source-only cycle is missing.");
        cycle.Validate();
        if (cycle.Identity != invocation.Identity || !cycle.ExpectedConsumers.SequenceEqual(invocation.ExpectedConsumers)
            || cycle.ProducerLaneId != invocation.ProducerLaneId
            || cycle.BundleSha256 != receipt.Request.BundleDigest
            || !cycle.EnabledSources.SequenceEqual([BundesligaContextSource.ClubElo])
            || cycle.Status is not (BundesligaContextSourceCycleStatus.HandoffReady or BundesligaContextSourceCycleStatus.Complete))
            throw new InvalidDataException("Source-only cycle contradicts the requested handoff.");
        if (cycle.Status != BundesligaContextSourceCycleStatus.Complete) return;
        var health = await repository.GetHealthAsync(invocation.Identity.Competition, invocation.Identity.Scope,
            BundesligaContextSource.ClubElo, cancellationToken)
            ?? throw new InvalidDataException("Completed source-only cycle has no health.");
        health.Validate();
        if (health.Competition != invocation.Identity.Competition || health.Scope != invocation.Identity.Scope
            || health.Source != BundesligaContextSource.ClubElo)
            throw new InvalidDataException("Source-only health contradicts the requested identity.");
        // Older exact receipt replays preserve newer durable health; never re-promote the old cycle.
        if (health.Watermark.Sequence > invocation.Identity.Sequence) return;
        if (health.Watermark.Sequence != invocation.Identity.Sequence || health.Watermark.CycleId != invocation.Identity.CycleId
            || health.LastCompletedCycleId != invocation.Identity.CycleId
            || health.DesiredIssueProjection?.SynchronizationStatus != BundesligaContextSourceIssueSynchronization.Synchronized)
            throw new InvalidDataException("Completed source-only refresh has pending issue synchronization.");
    }

    internal static void ValidateSourceOnly(CompetitionCollectionProfile profile, string? matchdays, bool fullSeason, string? credentialProfile = null)
    {
        if (profile.Competition != EHonda.KicktippAi.Core.CompetitionIds.Bundesliga2026_27
            || !profile.ContextSourceFeatures.ClubEloEnabled || profile.ContextSourceFeatures.RostersEnabled
            || matchdays is not null || fullSeason || credentialProfile is not null)
            throw new InvalidOperationException("Source-only collection requires Bundesliga 2026/27 and Club Elo only, without matchday, full-season or Kicktipp credential-profile options.");
    }

    private static async Task<int> ExecutePreparedAsync(IAnsiConsole console,
        ICompetitionProfileCollectorExecutor collectorExecutor, CompetitionProfileCollectionRequest request,
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
            request.Verbose,
            request.MarkdownSummaryOutput,
            request.ContextSourceCycle);

        // The executor performs the flag check before resolving any coordinator,
        // provider, repository, or handoff service. The lease owns exact local
        // cleanup and remains ambient for source-specific collectors.
        ContextSourceCyclePreparation? preparedSources = null;
        if (request.Profile.ContextSourceFeatures.AnyEnabled)
        {
            preparedSources = await collectorExecutor.PrepareContextSourcesAsync(executionContext, cancellationToken);
        }
        if (request.ContextSourceOnly && preparedSources is null)
            throw new InvalidDataException("Source-only collection requires a prepared source cycle.");
        await using var sourcePreparation = preparedSources;
        using var sourcePreparationActivation = preparedSources?.Activate();

        if (preparedSources is not null)
            console.MarkupLine($"[blue]Source cycle:[/] {Markup.Escape(preparedSources.Files.Bundle.Cycle.CycleId)}; [blue]lane:[/] {Markup.Escape(preparedSources.CurrentLaneId)}");

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

            if (TryGetSource(step.Collector, out var source)
                && preparedSources?.TryGetPersistedReceipt(source, out var persistedReceipt) == true)
            {
                await preparedSources.ReconcilePersistedReceiptAsync(persistedReceipt!, cancellationToken);
                const string disposition = "PersistedReceiptReplayed";
                dispositions.Add((step.Collector, disposition));
                console.MarkupLine(
                    $"[green]Collector {step.Collector}:[/] [yellow]{disposition}[/] " +
                    $"(cycle {Markup.Escape(persistedReceipt!.Request.Identity.CycleId)}, lane {Markup.Escape(preparedSources.CurrentLaneId)})");
                continue;
            }

            console.MarkupLine($"[blue]Running collector:[/] [yellow]{step.Collector}[/]");
            int exitCode;
            try
            {
                exitCode = await collectorExecutor.ExecuteAsync(step.Collector, executionContext, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
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
        console.MarkupLine($"[blue]Context source refresh:[/] [yellow]club-elo={profile.ContextSourceFeatures.ClubEloEnabled}, rosters={profile.ContextSourceFeatures.RostersEnabled}[/]");
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

    private static bool TryGetSource(CompetitionCollector collector, out EHonda.KicktippAi.Core.BundesligaContextSource source)
    {
        switch (collector)
        {
            case CompetitionCollector.ClubElo:
                source = EHonda.KicktippAi.Core.BundesligaContextSource.ClubElo;
                return true;
            case CompetitionCollector.Rosters:
                source = EHonda.KicktippAi.Core.BundesligaContextSource.Rosters;
                return true;
            default:
                source = default;
                return false;
        }
    }
}
