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
        await Assert.That(metadata.PromptVersion).IsEqualTo(
            bonusPrompt
                ? CompetitionResolver.BundesligaBonusPromptVersion
                : CompetitionResolver.BundesligaMatchPromptVersion);
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
        await Assert.That(metadata.PromptVersion).IsNull();

        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(
            "gpt-5.6-luna",
            "none",
            metadata.Competition,
            "ehonda-dev-buli-2627",
            "ehonda-dev-buli-2627",
            metadata.PromptSource,
            metadata.PromptName,
            metadata.PromptLabel,
            langfusePromptVersion: null,
            maxOutputTokenCount: 10_000,
            bonusPrompt: false);

        await Assert.That(modelConfig.PromptName).IsNull();
        await Assert.That(modelConfig.PromptVersion).IsNull();
    }

    [Test]
    public async Task Bundesliga_candidate_route_uses_explicit_numbered_version_for_exact_identity()
    {
        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(
            "gpt-5.6-luna",
            "none",
            CompetitionIds.Bundesliga2026_27,
            "ehonda-dev-buli-2627",
            "ehonda-dev-buli-2627",
            CompetitionResolver.LangfusePromptSource,
            CompetitionResolver.BundesligaMatchPromptName,
            "staging",
            langfusePromptVersion: 7,
            maxOutputTokenCount: 10_000,
            bonusPrompt: false);

        await Assert.That(modelConfig.PromptName).IsEqualTo(CompetitionResolver.BundesligaMatchPromptName);
        await Assert.That(modelConfig.PromptVersion).IsEqualTo(7);
    }

    [Test]
    [Arguments(false, CompetitionResolver.BundesligaBonusPromptName, CompetitionResolver.BundesligaBonusPromptVersion)]
    [Arguments(true, CompetitionResolver.BundesligaMatchPromptName, CompetitionResolver.BundesligaMatchPromptVersion)]
    public async Task Bundesliga_known_prompt_version_follows_exact_prompt_name_not_command_kind(
        bool bonusPrompt,
        string promptName,
        int expectedVersion)
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition: CompetitionIds.Bundesliga2026_27,
            community: "ehonda-dev-buli-2627",
            communityContext: "ehonda-dev-buli-2627",
            promptSource: "langfuse",
            langfusePromptName: promptName,
            langfusePromptLabel: "production",
            bonusPrompt);

        await Assert.That(metadata.PromptVersion).IsEqualTo(expectedVersion);
    }

    [Test]
    public async Task Custom_bundesliga_hosted_prompt_has_no_implicit_numbered_version()
    {
        var metadata = CompetitionResolver.ResolveRuntimeMetadata(
            competition: CompetitionIds.Bundesliga2026_27,
            community: "ehonda-dev-buli-2627",
            communityContext: "ehonda-dev-buli-2627",
            promptSource: "langfuse",
            langfusePromptName: "kicktippai/bundesliga-2026-27/custom-candidate",
            langfusePromptLabel: "staging",
            bonusPrompt: false);

        await Assert.That(metadata.PromptVersion).IsNull();
        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(
            "gpt-5.6-luna",
            "none",
            metadata.Competition,
            "ehonda-dev-buli-2627",
            "ehonda-dev-buli-2627",
            metadata.PromptSource,
            metadata.PromptName,
            metadata.PromptLabel,
            langfusePromptVersion: null,
            maxOutputTokenCount: 10_000,
            bonusPrompt: false);

        await Assert.That(modelConfig.PromptName).IsNull();
        await Assert.That(modelConfig.PromptVersion).IsNull();
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
        await Assert.That(CompetitionResolver.IsDevCommunity("ehonda-dev-buli-2627")).IsTrue();
        await Assert.That(CompetitionResolver.IsDevCommunity("ehonda-dev-wm26")).IsTrue();
        await Assert.That(CompetitionResolver.SupportedDevCommunities)
            .IsEquivalentTo(["ehonda-dev-buli-2627", "ehonda-dev-wm26"]);
        await Assert.That(CompetitionResolver.IsDevCommunity("rabetrabauken2026")).IsFalse();
        await Assert.That(CompetitionResolver.IsDevCommunity("pes-squad")).IsFalse();
    }

    [Test]
    [Arguments("ehonda-dev-buli-2627", CompetitionIds.FifaWorldCup2026)]
    [Arguments("ehonda-dev-wm26", CompetitionIds.Bundesliga2026_27)]
    public async Task Development_community_rejects_an_explicit_mismatched_competition(
        string community,
        string competition)
    {
        await Assert.That(() => CompetitionResolver.ResolveDevelopmentCompetition(community, competition))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("ehonda-dev-buli-2627", CompetitionIds.FifaWorldCup2026)]
    [Arguments("ehonda-dev-wm26", CompetitionIds.Bundesliga2026_27)]
    public async Task Target_resolution_rejects_exact_known_community_conflicts(
        string communityContext,
        string competition)
    {
        await Assert.That(() => CompetitionResolver.ResolveTargetCompetition(competition, communityContext))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("ehonda-ai-arena", CompetitionIds.Bundesliga2026_27)]
    [Arguments("historical-community", CompetitionIds.Bundesliga2025_26)]
    [Arguments("unmapped-community", CompetitionIds.FifaWorldCup2026)]
    public async Task Target_resolution_preserves_multi_scope_unmapped_and_historical_targets(
        string communityContext,
        string competition)
    {
        var resolved = CompetitionResolver.ResolveTargetCompetition(competition, communityContext);

        await Assert.That(resolved).IsEqualTo(competition);
    }
}
