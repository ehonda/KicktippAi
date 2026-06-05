using EHonda.KicktippAi.Core;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> settings validation and display.
/// </summary>
public class BonusCommand_Settings_Tests : BonusCommandTests_Base
{
    [Test]
    public async Task Model_argument_is_required()
    {
        var context = CreateBonusCommandApp();

        var exitCode = await context.App.RunAsync(["bonus", "--community", "test"]);
        var output = context.Console.Output;

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("MODEL");
    }

    [Test]
    public async Task Running_command_displays_model_name()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("test-model");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_verbose_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_bonus_prompt_path()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--verbose"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Bonus prompt:");
        await Assert.That(output).Contains("prompts/bonus-prompt.md");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_shows_override_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-kicktipp"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override mode enabled");
    }

    [Test]
    public async Task Running_command_with_override_database_shows_override_database_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-database"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override database mode enabled");
    }

    [Test]
    public async Task Running_command_with_agent_shows_agent_mode_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--agent"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Agent mode enabled");
    }

    [Test]
    public async Task Running_command_with_dry_run_shows_dry_run_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--dry-run"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode enabled");
    }

    [Test]
    public async Task Running_command_with_estimated_costs_shows_estimated_costs_model()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--estimated-costs", "o3"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Estimated costs will be calculated for model:");
        await Assert.That(output).Contains("o3");
    }

    [Test]
    public async Task Running_command_displays_community_and_community_context()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test-community"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("test-community");
    }

    [Test]
    public async Task Running_command_with_community_context_uses_separate_context()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "main", "--community-context", "test-context"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("main");
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("test-context");
    }

    [Test]
    public async Task Running_command_with_repredict_shows_reprediction_mode_message()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--repredict"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
        await Assert.That(output).Contains("unlimited");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_shows_max_value()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "5"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
        await Assert.That(output).Contains("5");
    }

    [Test]
    public async Task Running_command_with_override_database_and_repredict_returns_error()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-database", "--repredict"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_override_database_and_max_repredictions_returns_error()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--override-database", "--max-repredictions", "3"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_negative_max_repredictions_returns_error()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-repredictions", "-1"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--max-repredictions must be 0 or greater");
    }

    [Test]
    public async Task Running_command_with_zero_max_output_tokens_returns_error()
    {
        var context = CreateBonusCommandApp();

        var exitCode = await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-output-tokens", "0"]);
        var output = context.Console.Output;

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--max-output-tokens must be at least 1");
    }

    [Test]
    public async Task Running_command_with_max_output_tokens_passes_cap_to_prediction_service()
    {
        var context = CreateBonusCommandApp();

        await context.App.RunAsync(["bonus", "test-model", "--community", "test", "--max-output-tokens", "40000"]);

        context.OpenAiServiceFactory.Verify(
            factory => factory.CreatePredictionService(
                "test-model",
                It.Is<PredictionServiceOptions>(options => options.MaxOutputTokenCount == 40_000)),
            Times.Once);
    }

    [Test]
    public async Task Running_bonus_dev_for_supported_dev_community_uses_override_defaults()
    {
        // Arrange
        var context = CreateBonusCommandApp(
            kpiContextDocuments: new List<DocumentContext>
            {
                new DocumentContext(
                    "fifa-rankings",
                    "Rank,Team,ELO,Data_Collected_At\n8,Marokko,1755.87,2026-05-25")
            });

        // Act
        var exitCode = await context.App.RunAsync(["bonus-dev", "-c", "ehonda-dev-wm26"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("bonus-dev dev preset enabled");
        await Assert.That(output).Contains("Override mode enabled");
        await Assert.That(output).Contains("Override database mode enabled");
        await Assert.That(output).Contains("fifa-world-cup-2026");

        context.OpenAiServiceFactory.Verify(
            factory => factory.CreatePredictionService(
                "gpt-5-nano",
                It.Is<PredictionServiceOptions>(options => options.ReasoningEffort == "minimal"),
                It.IsAny<IInstructionsTemplateProvider>()),
            Times.Once);

        context.PredictionRepository.Verify(
            repository => repository.SaveBonusPredictionAsync(
                It.IsAny<BonusQuestion>(),
                It.IsAny<BonusPrediction>(),
                It.Is<PredictionModelConfig>(config =>
                    config.Model == "gpt-5-nano" &&
                    config.ReasoningEffort == "minimal"),
                It.IsAny<string>(),
                It.IsAny<double>(),
                "ehonda-dev-wm26",
                It.IsAny<IEnumerable<string>>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);

        context.KicktippClient.Verify(
            client => client.PlaceBonusPredictionsAsync(
                "ehonda-dev-wm26",
                It.IsAny<Dictionary<string, BonusPrediction>>(),
                true),
            Times.Once);
    }

    [Test]
    public async Task Running_bonus_dev_for_non_dev_community_returns_error_without_running_workflow()
    {
        // Arrange
        var context = CreateBonusCommandApp();

        // Act
        var exitCode = await context.App.RunAsync(["bonus-dev", "-c", "pes-squad"]);
        var output = context.Console.Output;

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("only available for supported development communities");
        await Assert.That(output).Contains("ehonda-dev-wm26");

        context.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }
}
