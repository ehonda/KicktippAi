using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify.VerifyBonusCommandTests;

/// <summary>
/// Tests for <see cref="VerifyBonusCommand"/> CLI settings and argument parsing.
/// </summary>
public class VerifyBonusCommand_Settings_Tests : VerifyBonusCommandTests_Base
{
    [Test]
    public async Task Model_argument_is_required()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "-c", "test");

        // Assert - should fail due to missing model argument
        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("MODEL");
    }

    [Test]
    public async Task Community_is_passed_to_kicktipp_client()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "my-community");

        // Assert
        ctx.KicktippClient.Verify(c => c.GetOpenBonusQuestionsAsync("my-community"), Times.Once);
    }

    [Test]
    public async Task Community_context_defaults_to_community_name()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var ctx = CreateVerifyBonusCommandApp(bonusQuestions: new List<BonusQuestion> { question });

        // Act
        await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "my-community");

        // Assert - prediction lookup uses community as context
        ctx.PredictionRepository.Verify(r => r.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            "gpt-4o",
            "my-community",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Community_context_can_differ_from_community()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var ctx = CreateVerifyBonusCommandApp(bonusQuestions: new List<BonusQuestion> { question });

        // Act
        await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "my-community", "--community-context", "different-context");

        // Assert - prediction lookup uses explicit community context
        ctx.PredictionRepository.Verify(r => r.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            "gpt-4o",
            "different-context",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Model_is_passed_to_prediction_repository()
    {
        // Arrange
        var question = CreateTestBonusQuestion(formFieldName: "bonus_q1");
        var ctx = CreateVerifyBonusCommandApp(bonusQuestions: new List<BonusQuestion> { question });

        // Act
        await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "o4-mini", "-c", "test");

        // Assert
        ctx.PredictionRepository.Verify(r => r.GetBonusPredictionByTextAsync(
            It.IsAny<string>(),
            "o4-mini",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Verbose_and_agent_flags_can_be_combined()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v", "--agent");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Agent mode enabled");
    }

    [Test]
    public async Task All_flags_can_be_combined()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "test", "-v", "--agent", "--init-matchday", "--check-outdated");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Agent mode enabled");
        await Assert.That(output).Contains("Init bonus mode enabled");
        await Assert.That(output).Contains("Outdated check enabled");
    }

    [Test]
    public async Task Community_and_community_context_displayed_in_output()
    {
        // Arrange
        var ctx = CreateVerifyBonusCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-bonus", "gpt-4o", "-c", "my-community", "--community-context", "my-context");

        // Assert
        await Assert.That(output).Contains("Using community:").And.Contains("my-community");
        await Assert.That(output).Contains("Using community context:").And.Contains("my-context");
    }
}
