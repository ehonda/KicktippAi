using EHonda.KicktippAi.Core;

namespace Core.Tests;

public class PredictionModelConfigTests
{
    [Test]
    public async Task Exact_runtime_identity_includes_reasoning_cap_and_numbered_prompt()
    {
        var config = PredictionModelConfig.Create(
            " gpt-5.6-luna ",
            " NONE ",
            maxOutputTokenCount: 10_000,
            promptName: " kicktippai/bundesliga-2026-27/predict-one-match ",
            promptVersion: 2);

        await Assert.That(config.Model).IsEqualTo("gpt-5.6-luna");
        await Assert.That(config.ReasoningEffort).IsEqualTo("none");
        await Assert.That(config.MaxOutputTokenCount).IsEqualTo(10_000);
        await Assert.That(config.PromptName).IsEqualTo("kicktippai/bundesliga-2026-27/predict-one-match");
        await Assert.That(config.PromptVersion).IsEqualTo(2);
        await Assert.That(config.IdentityKey).IsEqualTo(
            "gpt-5.6-luna:reasoning-effort:none:max-output-tokens:10000:" +
            "prompt-name:kicktippai%2Fbundesliga-2026-27%2Fpredict-one-match:prompt-version:2");
        await Assert.That(config.HasPinnedRuntimeIdentity).IsTrue();
        await Assert.That(config.AllowsLegacyModelOnlyLookup).IsFalse();
        await Assert.That(config.AllowsReasoningEffortOnlyLookup).IsFalse();
    }

    [Test]
    public async Task Official_luna_max_reasoning_effort_is_valid()
    {
        var config = PredictionModelConfig.Create("gpt-5.6-luna", "max");

        await Assert.That(config.ReasoningEffort).IsEqualTo("max");
    }

    [Test]
    public async Task Prompt_version_requires_prompt_name()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PredictionModelConfig.Create("gpt-5.6-luna", "none", 10_000, promptVersion: 2));

        await Assert.That(exception.Message).Contains("Prompt name is required");
    }

    [Test]
    public async Task Prompt_name_requires_exact_prompt_version()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PredictionModelConfig.Create(
                "gpt-5.6-luna",
                "none",
                10_000,
                "kicktippai/bundesliga-2026-27/custom-candidate"));

        await Assert.That(exception.Message).Contains("Prompt version is required");
    }
}
