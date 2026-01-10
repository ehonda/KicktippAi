using KicktippIntegration;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using static TestUtilities.CoreTestFactories;

namespace Orchestrator.Tests.Commands.Operations.Verify;

/// <summary>
/// Tests for <see cref="VerifyMatchdayCommand"/> verbose, agent, and init-matchday mode behaviors.
/// </summary>
public class VerifyMatchdayCommand_Modes_Tests : VerifyMatchdayCommandTests_Base
{
    [Test]
    public async Task Verbose_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Verbose mode enabled");
    }

    [Test]
    public async Task Verbose_mode_displays_match_lookup_details()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "FC Bayern", awayTeam: "BVB");
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Looking up: FC Bayern vs BVB");
    }

    [Test]
    public async Task Verbose_mode_displays_database_prediction_values()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 3, awayGoals: 2)),
            databasePrediction: CreatePrediction(homeGoals: 3, awayGoals: 2));

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("Found database prediction: 3:2");
    }

    [Test]
    public async Task Verbose_mode_displays_no_database_prediction_message()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: (EHonda.KicktippAi.Core.Prediction?)null);

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("No database prediction found");
    }

    [Test]
    public async Task Verbose_mode_displays_valid_prediction_with_score()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1));

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v");

        // Assert
        await Assert.That(output).Contains("✓").And.Contains("2:1").And.Contains("valid");
    }

    [Test]
    public async Task Agent_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("Agent mode enabled - prediction details will be hidden");
    }

    [Test]
    public async Task Agent_mode_hides_prediction_scores_in_verbose()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 3, awayGoals: 2)),
            databasePrediction: CreatePrediction(homeGoals: 3, awayGoals: 2));

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "-v", "--agent");

        // Assert
        await Assert.That(output).Contains("Team A vs Team B");
        await Assert.That(output).Contains("(valid)");
        await Assert.That(output).DoesNotContain("3:2");
    }

    [Test]
    public async Task Agent_mode_shows_abbreviated_mismatch_status()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 0, awayGoals: 0));

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--agent");

        // Assert
        await Assert.That(output).Contains("✗ Team A vs Team B");
        await Assert.That(output).Contains("(mismatch)");
        await Assert.That(output).DoesNotContain("Kicktipp:");
        await Assert.That(output).DoesNotContain("Database:");
    }

    [Test]
    public async Task Non_agent_mode_shows_detailed_mismatch_info()
    {
        // Arrange
        var match = CreateTestMatch(homeTeam: "Team A", awayTeam: "Team B");
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 0, awayGoals: 0));

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test");

        // Assert
        await Assert.That(output).Contains("✗ Team A vs Team B:");
        await Assert.That(output).Contains("Kicktipp:").And.Contains("2:1");
        await Assert.That(output).Contains("Database:").And.Contains("0:0");
    }

    [Test]
    public async Task Init_matchday_mode_displays_mode_indicator()
    {
        // Arrange
        var ctx = CreateVerifyMatchdayCommandApp();

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(output).Contains("Init matchday mode enabled");
    }

    [Test]
    public async Task Init_matchday_returns_error_when_no_database_predictions_exist()
    {
        // Arrange - matches exist on Kicktipp but no predictions in database
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, (BetPrediction?)null),
            databasePrediction: (EHonda.KicktippAi.Core.Prediction?)null);

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("Init matchday detected - no database predictions exist");
        await Assert.That(output).Contains("Returning error to trigger initial prediction workflow");
    }

    [Test]
    public async Task Init_matchday_returns_success_when_database_predictions_exist()
    {
        // Arrange - matches exist with matching predictions
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction(homeGoals: 2, awayGoals: 1)),
            databasePrediction: CreatePrediction(homeGoals: 2, awayGoals: 1));

        // Act
        var (exitCode, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--init-matchday");

        // Assert
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).DoesNotContain("Init matchday detected");
    }

    [Test]
    public async Task Check_outdated_mode_displays_mode_indicator()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", "-c", "test", "--check-outdated");

        // Assert
        await Assert.That(output).Contains("Outdated check enabled");
    }

    [Test]
    public async Task Community_context_can_be_specified_separately()
    {
        // Arrange
        var match = CreateTestMatch();
        var ctx = CreateVerifyMatchdayCommandApp(
            placedPredictions: CreatePlacedPredictions(match, CreateBetPrediction()),
            databasePrediction: CreatePrediction());

        // Act
        var (_, output) = await RunCommandAsync(ctx.App, ctx.Console, "verify-matchday", "gpt-4o", 
            "-c", "my-community", "--community-context", "different-context");

        // Assert
        await Assert.That(output).Contains("Using community:").And.Contains("my-community");
        await Assert.That(output).Contains("Using community context:").And.Contains("different-context");
    }
}
