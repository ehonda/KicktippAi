using System.Text.RegularExpressions;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class ContextCollectionWorkflowContractTests
{
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
            .Contains("if: ${{ inputs.publish_launch_roster_overlay }}")
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
