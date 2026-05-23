using EHonda.KicktippAi.Core;
using KicktippIntegration;
using Moq;
using OpenAiIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Matchday;

/// <summary>
/// Tests for <see cref="Orchestrator.Commands.Operations.Matchday.MatchdayCommand"/> settings validation.
/// </summary>
public class MatchdayCommand_Settings_Tests : MatchdayCommandTests_Base
{
    [Test]
    public async Task Running_command_displays_model_name()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("gpt-4o");
    }

    [Test]
    public async Task Running_command_with_verbose_shows_verbose_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_without_verbose_does_not_show_verbose_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Verbose mode enabled");
    }

    [Test]
    public async Task Running_command_with_override_kicktipp_shows_override_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-kicktipp");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override mode enabled");
    }

    [Test]
    public async Task Running_command_with_override_database_shows_override_database_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-database");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Override database mode enabled");
    }

    [Test]
    public async Task Running_command_with_agent_mode_shows_agent_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--agent");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Agent mode enabled");
    }

    [Test]
    public async Task Running_command_with_dry_run_shows_dry_run_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Dry run mode enabled");
    }

    [Test]
    public async Task Running_command_with_estimated_costs_shows_estimated_costs_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--estimated-costs", "o3");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Estimated costs will be calculated");
        await Assert.That(output).Contains("o3");
    }

    [Test]
    public async Task Running_command_with_justification_shows_justification_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Justification output enabled");
    }

    [Test]
    public async Task Running_command_with_repredict_shows_reprediction_mode_message()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--repredict");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
    }

    [Test]
    public async Task Running_command_with_max_repredictions_shows_max_repredictions_value()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "3");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Reprediction mode enabled");
        await Assert.That(output).Contains("max repredictions: 3");
    }

    [Test]
    public async Task Running_command_with_justification_and_agent_returns_error()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--with-justification", "--agent");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--with-justification cannot be used with --agent");
    }

    [Test]
    public async Task Running_world_cup_hosted_match_prompt_with_justification_returns_clear_error()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(
            ctx.App,
            ctx.Console,
            "matchday",
            "-c",
            "ehonda-dev-wm26",
            "--with-justification");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("hosted match prompts with justification are not supported yet");
    }

    [Test]
    public async Task Running_command_with_override_database_and_repredict_returns_error()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-database", "--repredict");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_override_database_and_max_repredictions_returns_error()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--override-database", "--max-repredictions", "2");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--override-database cannot be used with reprediction flags");
    }

    [Test]
    public async Task Running_command_with_negative_max_repredictions_returns_error()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "test-community", "--max-repredictions", "-1");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("--max-repredictions must be 0 or greater");
    }

    [Test]
    public async Task Running_command_displays_community_name()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "my-test-community");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("my-test-community");
    }

    [Test]
    public async Task Running_command_with_community_context_displays_both_community_and_context()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday", "gpt-4o", "-c", "my-community", "--community-context", "shared-context");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("Using community:");
        await Assert.That(output).Contains("my-community");
        await Assert.That(output).Contains("Using community context:");
        await Assert.That(output).Contains("shared-context");
    }

    [Test]
    public async Task Running_matchday_dev_for_supported_dev_community_uses_override_defaults()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday-dev", "-c", "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("matchday-dev dev preset enabled");
        await Assert.That(output).Contains("Override mode enabled");
        await Assert.That(output).Contains("Override database mode enabled");
        await Assert.That(output).Contains("fifa-world-cup-2026");

        ctx.OpenAiServiceFactory.Verify(
            factory => factory.CreatePredictionService(
                "gpt-5-nano",
                It.Is<PredictionServiceOptions>(options => options.ReasoningEffort == "minimal"),
                It.IsAny<IInstructionsTemplateProvider>()),
            Times.Once);

        ctx.PredictionRepository.Verify(
            repository => repository.SavePredictionAsync(
                It.IsAny<Match>(),
                It.IsAny<Prediction>(),
                "gpt-5-nano",
                It.IsAny<string>(),
                It.IsAny<double>(),
                "ehonda-dev-wm26",
                It.IsAny<IEnumerable<string>>(),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);

        ctx.KicktippClient.Verify(
            client => client.PlaceBetsAsync(
                "ehonda-dev-wm26",
                It.IsAny<Dictionary<Match, BetPrediction>>(),
                true),
            Times.Once);
    }

    [Test]
    public async Task Running_matchday_dev_for_non_dev_community_returns_error_without_running_workflow()
    {
        var ctx = CreateMatchdayCommandApp();

        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "matchday-dev", "-c", "pes-squad");

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("only available for supported development communities");
        await Assert.That(output).Contains("ehonda-dev-wm26");

        ctx.KicktippClientFactory.Verify(factory => factory.CreateClient(), Times.Never);
    }
}
