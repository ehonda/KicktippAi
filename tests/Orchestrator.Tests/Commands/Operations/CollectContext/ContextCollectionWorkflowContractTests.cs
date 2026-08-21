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
    public async Task Base_workflow_requires_explicit_competition_and_invokes_only_the_profile_command()
    {
        var workflow = await ReadWorkflow("base-context-collection.yml");

        await Assert.That(Regex.IsMatch(
            workflow,
            @"(?ms)^      competition:\s*\r?\n        description:.*\r?\n        required: true\s*\r?\n        type: string\s*$"))
            .IsTrue();
        await Assert.That(workflow)
            .Contains("collect-context profile")
            .And.Contains("--community-context \"${{ inputs.community_context }}\"")
            .And.Contains("--competition \"${{ inputs.competition }}\"")
            .And.Contains("--markdown-summary-output \"$GITHUB_STEP_SUMMARY\"")
            .And.DoesNotContain("include_fifa_rankings")
            .And.DoesNotContain("include_lineups")
            .And.DoesNotContain("wm26-recent-history")
            .And.DoesNotContain("collect-context fifa")
            .And.DoesNotContain("collect-context lineups")
            .And.DoesNotContain("transfer");
    }

    [Test]
    [Arguments("pes-squad-context-collection.yml", "pes-squad")]
    [Arguments("schadensfresse-context-collection.yml", "schadensfresse")]
    public async Task Bundesliga_callers_pin_the_current_competition(string fileName, string communityContext)
    {
        var workflow = await ReadWorkflow(fileName);

        await Assert.That(workflow)
            .Contains("uses: ./.github/workflows/base-context-collection.yml")
            .And.Contains($"community_context: \"{communityContext}\"")
            .And.Contains($"competition: \"{CompetitionIds.Bundesliga2026_27}\"")
            .And.DoesNotContain(CompetitionIds.Bundesliga2025_26)
            .And.DoesNotContain(CompetitionIds.FifaWorldCup2026)
            .And.DoesNotContain("include_fifa_rankings")
            .And.DoesNotContain("include_lineups");
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
