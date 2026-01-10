using KicktippIntegration;
using Moq;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Tests for <see cref="VerifyMatchdayCommand"/> settings parsing and validation.
/// </summary>
public class VerifyMatchdayCommand_Settings_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Model_argument_is_required()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act - run without model argument
        var (exitCode, _) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "-c", "test");

        // Assert
        await Assert.That(exitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task Community_is_passed_to_kicktipp_client()
    {
        // Arrange - verify the community value is actually used
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "my-test-community");

        // Assert - verify the community was passed to the kicktipp client
        ctx.KicktippClient.Verify(c => c.GetPlacedPredictionsAsync("my-test-community"), Times.Once());
    }

    [Test]
    public async Task Community_context_defaults_to_community_when_not_specified()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "my-community");

        // Assert - both community and context should show same value
        await Assert.That(output).Contains("Using community:").And.Contains("my-community");
        await Assert.That(output).Contains("Using community context:").And.Contains("my-community");
    }

    [Test]
    public async Task Community_context_can_be_different_from_community()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", 
                "-c", "my-community", "--community-context", "different-context");

        // Assert
        await Assert.That(output).Contains("Using community:").And.Contains("my-community");
        await Assert.That(output).Contains("Using community context:").And.Contains("different-context");
    }

    [Test]
    public async Task Verbose_flag_short_form_works()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Verbose_flag_long_form_works()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--verbose");

        // Assert
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Multiple_flags_can_be_combined()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", 
                "-v", "--agent", "--check-outdated");

        // Assert
        await Assert.That(output).Contains("Verbose mode enabled");
        await Assert.That(output).Contains("Agent mode enabled");
        await Assert.That(output).Contains("Outdated check enabled");
    }

    [Test]
    public async Task Model_value_is_used_for_database_lookup()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        await Orchestrator.Tests.Infrastructure.OrchestratorTestFactories
            .RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "custom-model-name", "-c", "test");

        // Assert - verify the model was passed to the repository
        ctx.PredictionRepository.Verify(r => r.GetPredictionAsync(
            It.IsAny<EHonda.KicktippAi.Core.Match>(),
            "custom-model-name",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), 
            Moq.Times.AtLeastOnce());
    }
}
