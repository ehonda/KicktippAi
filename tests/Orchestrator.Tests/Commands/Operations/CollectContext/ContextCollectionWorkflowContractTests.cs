using System.Text.RegularExpressions;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class ContextCollectionWorkflowContractTests
{
    [Test]
    public async Task Optional_refresh_is_bounded_and_ordinary_collection_uses_default_success_without_sources()
    {
        var workflow = await ReadWorkflow("base-context-collection.yml");
        await Assert.That(workflow).Contains("timeout-minutes: ${{ inputs.enable_club_elo_source && !inputs.context_source_only && 52 || 45 }}")
            .And.Contains("id: source-setup").And.Contains("timeout-minutes: 2")
            .And.Contains("id: source-refresh").And.Contains("timeout-minutes: 5")
            .And.Contains("continue-on-error: ${{ !inputs.context_source_only }}")
            .And.Contains("steps.source-setup.outcome == 'success'")
            .And.Contains("steps.source-refresh.outcome").And.DoesNotContain("steps.source-refresh.conclusion");
        var ordinary = Regex.Match(workflow, @"(?ms)      - name: Run context collection\r?\n(?<body>.*?)(?=      - name:)").Groups["body"].Value;
        await Assert.That(ordinary).Contains("if: ${{ !inputs.context_source_only }}")
            .And.Contains("collect-context profile").And.DoesNotContain("always()")
            .And.DoesNotContain("--enable-club-elo-source").And.DoesNotContain("--context-source-cycle")
            .And.DoesNotContain("continue-on-error");
    }

    [Test]
    public async Task Validation_and_continuation_dispatches_are_mutually_exclusive_with_normal_jobs_and_have_no_model_secrets()
    {
        var outer = await ReadWorkflow("buli2627-production-live-matchday.yml");
        await Assert.That(Regex.Matches(outer, @"(?m)^    uses: \./\.github/workflows/base-context-collection.yml\r?$").Count).IsEqualTo(9);
        var normalJobs = Regex.Match(outer, @"(?ms)^jobs:\r?\n(?<body>.*?)^  club-elo-validation:").Groups["body"].Value;
        await Assert.That(Regex.Matches(normalJobs, @"(?m)^      enable_club_elo_source: true\r?$").Count).IsEqualTo(8);
        await Assert.That(Regex.Matches(outer, @"(?m)^      enable_club_elo_source: true\r?$").Count).IsEqualTo(9);
        await Assert.That(outer).DoesNotContain("enable_club_elo_source: false");
        await Assert.That(outer).Contains("github.event_name != 'workflow_dispatch' || inputs.club_elo_validation == 'off'")
            .And.Contains("github.event_name == 'workflow_dispatch' && (inputs.club_elo_validation == 'development' || inputs.club_elo_validation == 'production')")
            .And.Contains("github.event_name == 'workflow_dispatch' && inputs.club_elo_validation == 'continuation'")
            .And.Contains("cron: \"7 2,9 * * *\"").And.Contains("cancel-in-progress: false").And.DoesNotContain("always(");
        var continuation = Regex.Match(outer, @"(?ms)^  club-elo-continuation:\r?\n(?<body>.*)\z").Groups["body"].Value;
        await Assert.That(continuation).Contains("uses: ./.github/workflows/base-context-collection.yml")
            .And.Contains("enable_club_elo_source: true")
            .And.Contains("context_source_only: false")
            .And.Contains("context_source_continuation_probe: true")
            .And.Contains("community_context: \"pes-squad\"")
            .And.Contains("context_source_scope: \"production-live\"")
            .And.DoesNotContain("base-matchday-predictions.yml")
            .And.DoesNotContain("openai_api_key").And.DoesNotContain("langfuse_secret_key");
        var baseWorkflow = await ReadWorkflow("base-context-collection.yml");
        await Assert.That(baseWorkflow).Contains("if [[ \"$CONTINUATION_PROBE\" == true && ( \"$GITHUB_EVENT_NAME\" != workflow_dispatch")
            .And.Contains("\"$SOURCE_ENABLED\" != true || \"$SOURCE_ONLY\" == true || \"$SCOPE\" != production-live || \"$COMMUNITY\" != pes-squad || \"$LANE\" != pes-squad-context")
            .And.Contains("scope: ${{ inputs.context_source_continuation_probe && 'continuation-probe-invalid' || inputs.context_source_scope }}");
        var validation = await ReadWorkflow("buli2627-club-elo-validation.yml");
        await Assert.That(validation).DoesNotContain("openai").And.DoesNotContain("langfuse").And.DoesNotContain("kicktipp_")
            .And.DoesNotContain("schedule:").And.DoesNotContain("workflow_dispatch:").And.DoesNotContain("predictions.yml");
        await Assert.That(Regex.Matches(validation, @"(?m)^      context_source_only: true\r?$").Count).IsEqualTo(9);
        for (var index = 1; index < BundesligaContextSourceContract.ProductionConsumers.Count; index++)
            await Assert.That(validation).Contains($"needs: {BundesligaContextSourceContract.ProductionConsumers[index - 1]}");
    }
    private static readonly string WorkflowsDirectory = Path.Combine(
        SolutionPathUtility.FindSolutionRoot(),
        ".github",
        "workflows");

    [Test]
    public async Task Base_workflow_gates_the_exact_launch_overlay_before_normal_profile_collection()
    {
        var workflow = await ReadWorkflow("base-context-collection.yml");

        await Assert.That(Regex.IsMatch(
            workflow,
            @"(?ms)^      competition:\s*\r?\n        description:.*\r?\n        required: true\s*\r?\n        type: string\s*$"))
            .IsTrue();
        await Assert.That(Regex.IsMatch(
            workflow,
            @"(?ms)^      publish_launch_roster_overlay:\s*\r?\n        description:.*\r?\n        required: false\s*\r?\n        default: false\s*\r?\n        type: boolean\s*$"))
            .IsTrue();
        await Assert.That(workflow)
            .Contains("if: ${{ !inputs.context_source_only && inputs.publish_launch_roster_overlay }}")
            .And.Contains("https://pub-e682421888d945d684bcae8890b0ec20.r2.dev/data/transfermarkt-datasets.duckdb")
            .And.Contains("collect-context rosters")
            .And.Contains("--duckdb-revision \"154367dfa6d6eb0b86332e332f9df0a080c7ddce\"")
            .And.Contains("--duckdb-snapshot-date \"2026-08-13\"")
            .And.Contains("--duckdb-sha256 \"808959f5b5b16bb698180c348b269d9ec26e1d1a5538767ffe9d971b96796d1c\"")
            .And.Contains("--require-launch-coverage")
            .And.Contains("--launch-enrichment-overlay")
            .And.Contains("collect-context profile")
            .And.Contains("--community-context \"${{ inputs.community_context }}\"")
            .And.Contains("--competition \"${{ inputs.competition }}\"")
            .And.Contains("--markdown-summary-output \"$GITHUB_STEP_SUMMARY\"")
            .And.DoesNotContain("include_fifa_rankings")
            .And.DoesNotContain("include_lineups")
            .And.DoesNotContain("wm26-recent-history")
            .And.DoesNotContain("collect-context fifa")
            .And.DoesNotContain("collect-context lineups")
            .And.DoesNotContain("collect-context transfers");

        await Assert.That(workflow.IndexOf("collect-context rosters", StringComparison.Ordinal))
            .IsLessThan(workflow.IndexOf("collect-context profile", StringComparison.Ordinal));
    }

    [Test]
    [Arguments("pes-squad-context-collection.yml", "pes-squad")]
    [Arguments("schadensfresse-context-collection.yml", "schadensfresse")]
    [Arguments("relaxdays-tippt-context-collection.yml", "relaxdays-tippt")]
    public async Task Production_community_callers_pin_the_current_competition_and_launch_overlay(
        string fileName,
        string communityContext)
    {
        var workflow = await ReadWorkflow(fileName);

        await Assert.That(workflow)
            .Contains("uses: ./.github/workflows/base-context-collection.yml")
            .And.Contains($"community_context: \"{communityContext}\"")
            .And.Contains($"competition: \"{CompetitionIds.Bundesliga2026_27}\"")
            .And.Contains("publish_launch_roster_overlay: true")
            .And.DoesNotContain(CompetitionIds.Bundesliga2025_26)
            .And.DoesNotContain(CompetitionIds.FifaWorldCup2026)
            .And.DoesNotContain("include_fifa_rankings")
            .And.DoesNotContain("include_lineups");
    }

    [Test]
    [Arguments("buli2627-ehonda-ai-arena-context-collection.yml")]
    [Arguments("buli2627-ehonda-ai-arena-gpt-5-6-sol-xhigh-context-collection.yml")]
    [Arguments("buli2627-ehonda-ai-arena-gpt-5-6-sol-high-context-collection.yml")]
    [Arguments("buli2627-ehonda-ai-arena-gpt-5-6-luna-medium-context-collection.yml")]
    [Arguments("buli2627-ehonda-ai-arena-gpt-5-6-terra-xhigh-context-collection.yml")]
    public async Task Arena_callers_preserve_the_existing_enriched_lkg_without_redownloading(string fileName)
    {
        var workflow = await ReadWorkflow(fileName);

        await Assert.That(workflow).DoesNotContain("publish_launch_roster_overlay");
    }

    [Test]
    [Arguments("rabetrabauken2026-context-collection.yml", "rabetrabauken2026")]
    [Arguments("wm26-ehonda-ai-arena-context-collection.yml", "ehonda-ai-arena")]
    public async Task Historical_wm26_callers_pin_the_wm26_profile_without_collector_booleans(
        string fileName,
        string communityContext)
    {
        var workflow = await ReadWorkflow(fileName);

        await Assert.That(workflow)
            .Contains("workflow_call:")
            .And.DoesNotContain("workflow_dispatch:")
            .And.DoesNotContain("schedule:")
            .And.Contains("uses: ./.github/workflows/base-context-collection.yml")
            .And.Contains($"community_context: \"{communityContext}\"")
            .And.Contains($"competition: \"{CompetitionIds.FifaWorldCup2026}\"")
            .And.DoesNotContain("include_fifa_rankings")
            .And.DoesNotContain("include_lineups");
    }

    [Test]
    public async Task Bundesliga_profile_composes_history_in_kicktipp_and_excludes_wm26_and_transfers()
    {
        var profile = new CompetitionCollectionProfileResolver()
            .ResolveCompetition(CompetitionIds.Bundesliga2026_27);

        await Assert.That(profile.Collectors).IsEquivalentTo([
            new CompetitionCollectorStep(CompetitionCollector.Kicktipp),
            new CompetitionCollectorStep(
                CompetitionCollector.BundesligaHistoryPlayedDates,
                CompetitionCollectorExecutionMode.IncludedInPrevious),
            new CompetitionCollectorStep(CompetitionCollector.ClubElo),
            new CompetitionCollectorStep(CompetitionCollector.Rosters)
        ]);
        await Assert.That(profile.Collectors.Any(step => step.Collector is
            CompetitionCollector.Wm26HistoryPlayedDates or
            CompetitionCollector.FifaRankings or
            CompetitionCollector.NationalLineups)).IsFalse();
        await Assert.That(profile.ContextFeatures.Transfers).IsFalse();
    }

    private static Task<string> ReadWorkflow(string fileName)
    {
        return File.ReadAllTextAsync(Path.Combine(WorkflowsDirectory, fileName));
    }
}
