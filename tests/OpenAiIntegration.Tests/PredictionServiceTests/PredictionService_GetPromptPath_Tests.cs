using Moq;
using TUnit.Core;

namespace OpenAiIntegration.Tests.PredictionServiceTests;

/// <summary>
/// Tests for the PredictionService GetMatchPromptPath and GetBonusPromptPath methods
/// </summary>
public class PredictionService_GetPromptPath_Tests : PredictionServiceTests_Base
{
    [Test]
    public async Task GetMatchPromptPath_without_justification_returns_correct_path()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "gpt-5");

            // Act
            var promptPath = service.GetMatchPromptPath(includeJustification: false);

            // Assert
            await Assert.That(promptPath).IsNotNull();
            await Assert.That(promptPath).Contains("prompts");
            await Assert.That(promptPath).Contains("gpt-5");
            await Assert.That(promptPath).Contains("match.md");
            await Assert.That(File.Exists(promptPath)).IsTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetMatchPromptPath_with_justification_returns_correct_path()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "gpt-5");

            // Act
            var promptPath = service.GetMatchPromptPath(includeJustification: true);

            // Assert
            await Assert.That(promptPath).IsNotNull();
            await Assert.That(promptPath).Contains("prompts");
            await Assert.That(promptPath).Contains("gpt-5");
            await Assert.That(promptPath).Contains("match.justification.md");
            await Assert.That(File.Exists(promptPath)).IsTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetBonusPromptPath_returns_correct_path()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles();
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "gpt-5");

            // Act
            var promptPath = service.GetBonusPromptPath();

            // Assert
            await Assert.That(promptPath).IsNotNull();
            await Assert.That(promptPath).Contains("prompts");
            await Assert.That(promptPath).Contains("gpt-5");
            await Assert.That(promptPath).Contains("bonus.md");
            await Assert.That(File.Exists(promptPath)).IsTrue();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetMatchPromptPath_for_o3_model_uses_o3_prompts()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles("o3");
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "o3");

            // Act
            var promptPath = service.GetMatchPromptPath();

            // Assert
            await Assert.That(promptPath).Contains("o3");
            await Assert.That(promptPath).Contains("match.md");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetMatchPromptPath_for_o4_mini_model_uses_o3_prompts()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles("o3");
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "o4-mini");

            // Act
            var promptPath = service.GetMatchPromptPath();

            // Assert
            await Assert.That(promptPath).Contains("o3");
            await Assert.That(promptPath).Contains("match.md");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetMatchPromptPath_for_gpt_5_mini_uses_gpt_5_prompts()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles("gpt-5");
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "gpt-5-mini");

            // Act
            var promptPath = service.GetMatchPromptPath();

            // Assert
            await Assert.That(promptPath).Contains("gpt-5");
            await Assert.That(promptPath).Contains("match.md");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }

    [Test]
    public async Task GetMatchPromptPath_for_gpt_5_nano_uses_gpt_5_prompts()
    {
        // Arrange
        var tempDir = CreateTestPromptFiles("gpt-5");
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(tempDir);

        try
        {
            var mockChatClient = CreateMockChatClient("{}", CreateChatTokenUsage(0, 0));
            var logger = CreateMockLogger();
            var costCalc = CreateMockCostCalculationService();
            var tokenTracker = CreateMockTokenUsageTracker();

            var service = new PredictionService(
                mockChatClient,
                logger.Object,
                costCalc.Object,
                tokenTracker.Object,
                "gpt-5-nano");

            // Act
            var promptPath = service.GetMatchPromptPath();

            // Assert
            await Assert.That(promptPath).Contains("gpt-5");
            await Assert.That(promptPath).Contains("match.md");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            CleanupTestDirectory(tempDir);
        }
    }
}
