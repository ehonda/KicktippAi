using TUnit.Core;

namespace OpenAiIntegration.Tests.InstructionsTemplateProviderTests;

/// <summary>
/// Tests for the InstructionsTemplateProvider LoadMatchTemplate method
/// </summary>
public class InstructionsTemplateProvider_LoadMatchTemplate_Tests : InstructionsTemplateProviderTests_Base
{
    [Test]
    public async Task Loading_match_template_without_justification_returns_correct_content()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("gpt-5", includeJustification: false);

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Match Template");
        await Assert.That(path).Contains("gpt-5");
        await Assert.That(path).Contains("match.md");
    }

    [Test]
    public async Task Loading_match_template_with_justification_returns_justification_content()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("gpt-5", includeJustification: true);

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Match Template with Justification");
        await Assert.That(path).Contains("gpt-5");
        await Assert.That(path).Contains("match.justification.md");
    }

    [Test]
    public async Task Loading_match_template_with_justification_falls_back_to_regular_when_justification_missing()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act - o3 doesn't have justification file
        var (template, path) = sut.LoadMatchTemplate("o3", includeJustification: true);

        // Assert
        await Assert.That(template).IsEqualTo("O3 Match Template");
        await Assert.That(path).Contains("o3");
        await Assert.That(path).Contains("match.md");
    }

    [Test]
    public async Task Loading_match_template_for_gpt_5_mini_uses_gpt_5_prompts()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("gpt-5-mini", includeJustification: false);

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Match Template");
        await Assert.That(path).Contains("gpt-5");
    }

    [Test]
    public async Task Loading_match_template_for_gpt_5_nano_uses_gpt_5_prompts()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("gpt-5-nano", includeJustification: false);

        // Assert
        await Assert.That(template).IsEqualTo("GPT-5 Match Template");
        await Assert.That(path).Contains("gpt-5");
    }

    [Test]
    public async Task Loading_match_template_for_o4_mini_uses_o3_prompts()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("o4-mini", includeJustification: false);

        // Assert
        await Assert.That(template).IsEqualTo("O3 Match Template");
        await Assert.That(path).Contains("o3");
    }

    [Test]
    public async Task Loading_match_template_for_unknown_model_uses_model_name_as_directory()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProviderWithCustomModel("custom-model", "Custom Model Template");
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act
        var (template, path) = sut.LoadMatchTemplate("custom-model", includeJustification: false);

        // Assert
        await Assert.That(template).IsEqualTo("Custom Model Template");
        await Assert.That(path).Contains("custom-model");
    }

    [Test]
    public async Task Loading_match_template_throws_when_file_not_found()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act & Assert
        await Assert.That(() => sut.LoadMatchTemplate("nonexistent-model", includeJustification: false))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task Loading_match_template_with_justification_throws_when_neither_file_exists()
    {
        // Arrange
        var mockFileProvider = CreateMockFileProvider();
        var sut = new InstructionsTemplateProvider(mockFileProvider.Object);

        // Act & Assert
        await Assert.That(() => sut.LoadMatchTemplate("nonexistent-model", includeJustification: true))
            .Throws<FileNotFoundException>();
    }
}
