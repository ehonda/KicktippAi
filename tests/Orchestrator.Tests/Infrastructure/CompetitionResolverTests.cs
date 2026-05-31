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
    public async Task Defaults_existing_communities_to_bundesliga()
    {
        var competition = CompetitionResolver.ResolveCompetition(
            competition: null,
            community: "pes-squad",
            communityContext: null);

        await Assert.That(competition).IsEqualTo(CompetitionIds.Bundesliga2025_26);
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
        await Assert.That(CompetitionResolver.IsDevCommunity("pes-squad")).IsFalse();
    }
}
