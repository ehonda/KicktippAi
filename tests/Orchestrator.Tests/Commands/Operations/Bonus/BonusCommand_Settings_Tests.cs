using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Bonus;

/// <summary>
/// Tests for <see cref="BonusCommand"/> settings validation and display.
/// </summary>
public class BonusCommand_Settings_Tests : BonusCommandTests_Base
{
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
}
