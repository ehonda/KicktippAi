using TestUtilities;
using Moq;
using TUnit.Core;
using EHonda.Optional.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService GetMatchPromptPath and GetBonusPromptPath methods
/// </summary>
public class PredictionService_GetPromptPath_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task Getting_match_prompt_path_without_justification_returns_correct_path()
    {
        // Arrange
        var service = CreateService();

        // Act
        var promptPath = service.GetMatchPromptPath(includeJustification: false);

        // Assert
        await Assert.That(promptPath).IsNotNull();
        await Assert.That(promptPath).Contains("prompts");
        await Assert.That(promptPath).Contains("gpt-5");
        await Assert.That(promptPath).Contains("match.md");
    }

    [Test]
    public async Task Getting_match_prompt_path_with_justification_returns_correct_path()
    {
        // Arrange
        var service = CreateService();

        // Act
        var promptPath = service.GetMatchPromptPath(includeJustification: true);

        // Assert
        await Assert.That(promptPath).IsNotNull();
        await Assert.That(promptPath).Contains("prompts");
        await Assert.That(promptPath).Contains("gpt-5");
        await Assert.That(promptPath).Contains("match.justification.md");
    }

    [Test]
    public async Task Getting_bonus_prompt_path_returns_correct_path()
    {
        // Arrange
        var service = CreateService();

        // Act
        var promptPath = service.GetBonusPromptPath();

        // Assert
        await Assert.That(promptPath).IsNotNull();
        await Assert.That(promptPath).Contains("prompts");
        await Assert.That(promptPath).Contains("gpt-5");
        await Assert.That(promptPath).Contains("bonus.md");
    }

    [Test]
    public async Task Getting_match_prompt_path_for_o3_model_uses_o3_prompts()
    {
        // Arrange
        var templateProvider = CreateMockTemplateProvider("o3");
        var service = CreateService(model: "o3", templateProvider: Option.Some(templateProvider.Object));

        // Act
        var promptPath = service.GetMatchPromptPath();

        // Assert
        await Assert.That(promptPath).Contains("o3");
        await Assert.That(promptPath).Contains("match.md");
    }

    [Test]
    public async Task Getting_match_prompt_path_for_o4_mini_model_uses_o3_prompts()
    {
        // Arrange
        var templateProvider = CreateMockTemplateProvider("o3");
        var service = CreateService(model: "o4-mini", templateProvider: Option.Some(templateProvider.Object));

        // Act
        var promptPath = service.GetMatchPromptPath();

        // Assert
        await Assert.That(promptPath).Contains("o3");
        await Assert.That(promptPath).Contains("match.md");
    }

    [Test]
    public async Task Getting_match_prompt_path_for_gpt_5_mini_uses_gpt_5_prompts()
    {
        // Arrange
        var templateProvider = CreateMockTemplateProvider("gpt-5");
        var service = CreateService(model: "gpt-5-mini", templateProvider: Option.Some(templateProvider.Object));

        // Act
        var promptPath = service.GetMatchPromptPath();

        // Assert
        await Assert.That(promptPath).Contains("gpt-5");
        await Assert.That(promptPath).Contains("match.md");
    }

    [Test]
    public async Task Getting_match_prompt_path_for_gpt_5_nano_uses_gpt_5_prompts()
    {
        // Arrange
        var templateProvider = CreateMockTemplateProvider("gpt-5");
        var service = CreateService(model: "gpt-5-nano", templateProvider: Option.Some(templateProvider.Object));

        // Act
        var promptPath = service.GetMatchPromptPath();

        // Assert
        await Assert.That(promptPath).Contains("gpt-5");
        await Assert.That(promptPath).Contains("match.md");
    }
}
