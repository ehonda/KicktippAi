using Microsoft.Extensions.FileProviders;
using Moq;
using TestUtilities;
using TUnit.Core;

namespace OpenAiIntegration.Tests.InstructionsTemplateProviderTests;

/// <summary>
/// Base class for InstructionsTemplateProvider tests providing shared helper functionality
/// </summary>
public abstract class InstructionsTemplateProviderTests_Base
{
    protected Mock<IFileProvider> MockFileProvider = null!;
    protected InstructionsTemplateProvider InstructionsTemplateProvider = null!;

    [Before(Test)]
    public void SetupMockFileProviderAndInstructionsTemplateProvider()
    {
        MockFileProvider = CreateMockFileProvider();
        InstructionsTemplateProvider = new InstructionsTemplateProvider(MockFileProvider.Object);
    }

    /// <summary>
    /// Creates a mock IFileProvider that simulates a prompts directory structure
    /// </summary>
    protected static Mock<IFileProvider> CreateMockFileProvider()
    {
        // Define the content for each file
        var fileContents = new Dictionary<string, string>
        {
            ["gpt-5/match.md"] = "GPT-5 Match Template",
            ["gpt-5/match.justification.md"] = "GPT-5 Match Template with Justification",
            ["gpt-5/bonus.md"] = "GPT-5 Bonus Template",
            ["o3/match.md"] = "O3 Match Template",
            ["o3/bonus.md"] = "O3 Bonus Template"
        };

        return MockFileProviderHelpers.CreateMockFileProvider(fileContents);
    }

    /// <summary>
    /// Creates a mock IFileProvider that includes a custom model directory
    /// </summary>
    protected static Mock<IFileProvider> CreateMockFileProviderWithCustomModel(string customModel, string matchContent, string? bonusContent = null)
    {
        var mockFileProvider = CreateMockFileProvider();

        // Add custom model files
        var customMatchPath = $"{customModel}/match.md";
        var mockMatchFileInfo = MockFileProviderHelpers.CreateMockFileInfo(matchContent, customMatchPath);
        mockFileProvider.Setup(fp => fp.GetFileInfo(customMatchPath)).Returns(mockMatchFileInfo.Object);

        if (bonusContent != null)
        {
            var customBonusPath = $"{customModel}/bonus.md";
            var mockBonusFileInfo = MockFileProviderHelpers.CreateMockFileInfo(bonusContent, customBonusPath);
            mockFileProvider.Setup(fp => fp.GetFileInfo(customBonusPath)).Returns(mockBonusFileInfo.Object);
        }

        return mockFileProvider;
    }
}
