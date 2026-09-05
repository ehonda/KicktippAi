using EHonda.KicktippAi.Core;

namespace Core.Tests;

public sealed class SchadensfresseChampionsLeagueBonusProfileTests
{
    [Test]
    public async Task Exact_frozen_invocation_is_the_only_admitted_zero_context_tuple()
    {
        var accepted = SchadensfresseChampionsLeagueBonusProfile.IsExactInvocation(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "schadensfresse",
            "langfuse",
            SchadensfresseChampionsLeagueBonusProfile.PromptName,
            "production",
            1,
            "gpt-5.6-sol",
            "xhigh",
            10_000,
            0,
            0,
            "2026-09-08T16:45:00Z");

        var wrongContext = SchadensfresseChampionsLeagueBonusProfile.IsExactInvocation(
            CompetitionIds.Bundesliga2026_27,
            "schadensfresse",
            "pes-squad",
            "langfuse",
            SchadensfresseChampionsLeagueBonusProfile.PromptName,
            "production",
            1,
            "gpt-5.6-sol",
            "xhigh",
            10_000,
            0,
            0,
            "2026-09-08T16:45:00Z");

        await Assert.That(accepted).IsTrue();
        await Assert.That(wrongContext).IsFalse();
    }

    [Test]
    public async Task Manifest_requires_the_exact_model_identity_and_empty_documents()
    {
        var config = PredictionModelConfig.Create(
            "gpt-5.6-sol", "xhigh", 10_000,
            SchadensfresseChampionsLeagueBonusProfile.PromptName, 1);
        var manifest = new SchadensfresseChampionsLeagueBonusManifest
        {
            KicktippQuestionId = "1662326752",
            QuestionDefinitionSha256 = "642d2f1fa973fe8f32a5dfebcc8945615fa2dd27e24613b4552b99b47cc9e6d6",
            ModelConfigKey = config.IdentityKey
        };

        manifest.Validate(config);
        var invalid = manifest with { Documents = ["club-elo-rankings"] };
        await Assert.That(() => invalid.Validate(config)).Throws<InvalidDataException>();
    }
}
