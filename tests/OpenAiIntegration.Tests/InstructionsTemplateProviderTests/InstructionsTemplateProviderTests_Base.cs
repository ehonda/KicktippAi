using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Moq;
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
        var mockFileProvider = new Mock<IFileProvider>();

        // Define the content for each file
        var fileContents = new Dictionary<string, string>
        {
            ["gpt-5/match.md"] = "GPT-5 Match Template",
            ["gpt-5/match.justification.md"] = "GPT-5 Match Template with Justification",
            ["gpt-5/bonus.md"] = "GPT-5 Bonus Template",
            ["o3/match.md"] = "O3 Match Template",
            ["o3/bonus.md"] = "O3 Bonus Template"
        };

        // Setup GetFileInfo for each file
        foreach (var (path, content) in fileContents)
        {
            var mockFileInfo = CreateMockFileInfo(content, path);
            mockFileProvider.Setup(fp => fp.GetFileInfo(path)).Returns(mockFileInfo.Object);
        }

        // Setup non-existent files to return NotFoundFileInfo
        mockFileProvider.Setup(fp => fp.GetFileInfo(It.Is<string>(p => !fileContents.ContainsKey(p))))
            .Returns<string>(name => new NotFoundFileInfo(name));

        return mockFileProvider;
    }

    /// <summary>
    /// Creates a mock IFileProvider that includes a custom model directory
    /// </summary>
    protected static Mock<IFileProvider> CreateMockFileProviderWithCustomModel(string customModel, string matchContent, string? bonusContent = null)
    {
        var mockFileProvider = CreateMockFileProvider();

        // Add custom model files
        var customMatchPath = $"{customModel}/match.md";
        var mockMatchFileInfo = CreateMockFileInfo(matchContent, customMatchPath);
        mockFileProvider.Setup(fp => fp.GetFileInfo(customMatchPath)).Returns(mockMatchFileInfo.Object);

        if (bonusContent != null)
        {
            var customBonusPath = $"{customModel}/bonus.md";
            var mockBonusFileInfo = CreateMockFileInfo(bonusContent, customBonusPath);
            mockFileProvider.Setup(fp => fp.GetFileInfo(customBonusPath)).Returns(mockBonusFileInfo.Object);
        }

        return mockFileProvider;
    }

    /// <summary>
    /// Creates a mock IFileInfo that returns the specified content
    /// </summary>
    private static Mock<IFileInfo> CreateMockFileInfo(string content, string path)
    {
        var mockFileInfo = new Mock<IFileInfo>();
        mockFileInfo.Setup(fi => fi.Exists).Returns(true);
        mockFileInfo.Setup(fi => fi.PhysicalPath).Returns(path);
        mockFileInfo.Setup(fi => fi.CreateReadStream()).Returns(() => 
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        });
        
        return mockFileInfo;
    }
}
