using TUnit.Core;

namespace OpenAiIntegration.Tests.InstructionsTemplateProviderTests;

/// <summary>
/// Tests for the InstructionsTemplateProvider LoadBonusTemplate method
/// </summary>
public class InstructionsTemplateProvider_LoadBonusTemplate_Tests : InstructionsTemplateProviderTests_Base
{
    [Test]
    public async Task Loading_bonus_template_returns_correct_content()
    {
        // Act
        var (template, path) = InstructionsTemplateProvider.LoadBonusTemplate("gpt-5");

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Bonus Template");
        await Assert.That(path).Contains("gpt-5");
        await Assert.That(path).Contains("bonus.md");
    }

    [Test]
    public async Task Loading_bonus_template_for_gpt_5_mini_uses_gpt_5_prompts()
    {
        // Act
        var (template, path) = InstructionsTemplateProvider.LoadBonusTemplate("gpt-5-mini");

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Bonus Template");
        await Assert.That(path).Contains("gpt-5");
    }

    [Test]
    public async Task Loading_bonus_template_for_gpt_5_nano_uses_gpt_5_prompts()
    {
        // Act
        var (template, path) = InstructionsTemplateProvider.LoadBonusTemplate("gpt-5-nano");

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Bonus Template");
        await Assert.That(path).Contains("gpt-5");
    }

    [Test]
    public async Task Loading_bonus_template_for_o4_mini_uses_o3_prompts()
    {
        // Act
        var (template, path) = InstructionsTemplateProvider.LoadBonusTemplate("o4-mini");

        // Assert
        await Assert.That(template).IsEqualTo("O3 Bonus Template");
        await Assert.That(path).Contains("o3");
    }

    [Test]
    public async Task Loading_bonus_template_for_unknown_model_uses_model_name_as_directory()
    {
        // Arrange
        MockFileProvider = CreateMockFileProviderWithCustomModel("custom-model", "Custom Model Template", "Custom Model Bonus Template");
        InstructionsTemplateProvider = new InstructionsTemplateProvider(MockFileProvider.Object);

        // Act
        var (template, path) = InstructionsTemplateProvider.LoadBonusTemplate("custom-model");

        // Assert
        await Assert.That(template).IsEqualTo("Custom Model Bonus Template");
        await Assert.That(path).Contains("custom-model");
    }

    [Test]
    public async Task Loading_bonus_template_throws_when_file_not_found()
    {
        // Act & Assert
        await Assert.That(() => InstructionsTemplateProvider.LoadBonusTemplate("nonexistent-model"))
            .Throws<FileNotFoundException>();
    }
}
