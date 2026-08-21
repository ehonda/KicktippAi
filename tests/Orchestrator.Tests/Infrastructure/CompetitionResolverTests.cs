using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;

namespace Orchestrator.Tests.Infrastructure;

public class CompetitionResolverTests
{
    [Test]
    public async Task Resolves_dev_world_cup_community_to_world_cup_competition()
    {
        var competition = CompetitionResolver.ResolveCompetition(
            competition: null,
            community: "ehonda-dev-wm26",
            communityContext: null);

        await Assert.That(competition).IsEqualTo(CompetitionIds.FifaWorldCup2026);
    }

    [Test]
    [Arguments("rabetrabauken2026", null)]
    [Arguments(null, "rabetrabauken2026")]
    [Arguments("ehonda-ai-arena", null)]
    [Arguments(null, "ehonda-ai-arena")]
    public async Task Resolves_world_cup_community_or_context_to_world_cup_competition(
        string? community,
        string? communityContext)
    {
        var competition = CompetitionResolver.ResolveCompetition(
            competition: null,
            community: community,
            communityContext: communityContext);

        await Assert.That(competition).IsEqualTo(CompetitionIds.FifaWorldCup2026);
    }

    [Test]
    public async Task Defaults_existing_bundesliga_communities_to_current_bundesliga_competition()
    {
        var competition = CompetitionResolver.ResolveCompetition(
            competition: null,
            community: "pes-squad",
            communityContext: null);

        await Assert.That(competition).IsEqualTo(CompetitionIds.Bundesliga2026_27);
    }

    [Test]
    [Arguments(CompetitionIds.Bundesliga2026_27)]
    [Arguments(CompetitionIds.FifaWorldCup2026)]
    public async Task Preserves_explicit_current_competition(string explicitCompetition)
    {
        var competition = CompetitionResolver.ResolveCompetition(
            competition: explicitCompetition,
            community: "ehonda-dev-wm26",
            communityContext: null);

        await Assert.That(competition).IsEqualTo(explicitCompetition);
    }

    [Test]
    public async Task Current_kicktipp_season_metadata_uses_current_bundesliga_competition()
    {
        await Assert.That(KicktippSeasonMetadata.Current)
            .IsEqualTo(CompetitionIds.Bundesliga2026_27);
    }

    [Test]
    public async Task World_cup_runtime_defaults_to_langfuse_latest_prompt_and_local_fallback_model()
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition: null,
            community: "ehonda-dev-wm26",
            communityContext: null,
            promptSource: null,
            langfusePromptName: null,
            langfusePromptLabel: null,
            bonusPrompt: false);

        await Assert.That(metadata.PromptSource).IsEqualTo(CompetitionResolver.LangfusePromptSource);
        await Assert.That(metadata.PromptName).IsEqualTo(CompetitionResolver.WorldCupMatchPromptName);
        await Assert.That(metadata.PromptLabel).IsEqualTo(CompetitionResolver.DefaultWorldCupPromptLabel);
        await Assert.That(metadata.FallbackPromptModel).IsEqualTo(CompetitionResolver.WorldCupFallbackPromptModel);
    }

    [Test]
    [Arguments(false, CompetitionResolver.BundesligaMatchPromptName)]
    [Arguments(true, CompetitionResolver.BundesligaBonusPromptName)]
    public async Task Bundesliga_runtime_defaults_to_hosted_production_prompt_and_season_fallback(
        bool bonusPrompt,
        string expectedPromptName)
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition: CompetitionIds.Bundesliga2026_27,
            community: "pes-squad",
            communityContext: "pes-squad",
            promptSource: null,
            langfusePromptName: null,
            langfusePromptLabel: null,
            bonusPrompt);

        await Assert.That(metadata.PromptSource).IsEqualTo(CompetitionResolver.LangfusePromptSource);
        await Assert.That(metadata.PromptName).IsEqualTo(expectedPromptName);
        await Assert.That(metadata.PromptLabel).IsEqualTo(CompetitionResolver.DefaultBundesligaPromptLabel);
        await Assert.That(metadata.FallbackPromptModel).IsEqualTo(CompetitionResolver.BundesligaFallbackPromptModel);
    }

    [Test]
    public async Task Bundesliga_candidate_route_preserves_explicit_staging_label()
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition: CompetitionIds.Bundesliga2026_27,
            community: "ehonda-dev-buli-2627",
            communityContext: "ehonda-dev-buli-2627",
            promptSource: "langfuse",
            langfusePromptName: null,
            langfusePromptLabel: "staging",
            bonusPrompt: false);

        await Assert.That(metadata.PromptName).IsEqualTo(CompetitionResolver.BundesligaMatchPromptName);
        await Assert.That(metadata.PromptLabel).IsEqualTo("staging");
    }

    [Test]
    public async Task Missing_model_is_rejected_for_standard_commands()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PredictionServiceCommandSupport.ResolveModel(model: null));

        await Assert.That(exception.Message).Contains("MODEL is required");
    }

    [Test]
    public async Task Recognizes_only_supported_development_communities_for_dev_shortcuts()
    {
        await Assert.That(CompetitionResolver.IsDevCommunity("ehonda-dev-wm26")).IsTrue();
        await Assert.That(CompetitionResolver.IsDevCommunity("rabetrabauken2026")).IsFalse();
        await Assert.That(CompetitionResolver.IsDevCommunity("pes-squad")).IsFalse();
    }
}
