using Orchestrator.Commands.Observability.AnalyzeMatch;

namespace Orchestrator.Tests.Commands.Observability.AnalyzeMatchTests;

public class AnalyzeMatchSettings_Validation_Tests : AnalyzeMatchTests_Base
{
    [Test]
    public async Task Missing_community_context_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var exitCode = await context.App.RunAsync(
            ["detailed", DefaultModel,
                "--home", HomeTeam, "--away", AwayTeam, "--matchday", "1"]);

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(context.Console.Output).Contains("--community-context is required");
    }

    [Test]
    public async Task Missing_home_team_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var exitCode = await context.App.RunAsync(
            ["detailed", DefaultModel,
                "--community-context", DefaultCommunityContext, "--away", AwayTeam, "--matchday", "1"]);

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(context.Console.Output).Contains("--home must be provided");
    }

    [Test]
    public async Task Missing_away_team_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var exitCode = await context.App.RunAsync(
            ["detailed", DefaultModel,
                "--community-context", DefaultCommunityContext, "--home", HomeTeam, "--matchday", "1"]);

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(context.Console.Output).Contains("--away must be provided");
    }

    [Test]
    public async Task Missing_matchday_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var exitCode = await context.App.RunAsync(
            ["detailed", DefaultModel,
                "--community-context", DefaultCommunityContext, "--home", HomeTeam, "--away", AwayTeam]);

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(context.Console.Output).Contains("--matchday must be provided");
    }

    [Test]
    public async Task Runs_zero_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "0");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("--runs must be greater than 0");
    }

    [Test]
    public async Task Runs_negative_returns_error()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--runs", "-1");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("--runs must be greater than 0");
    }

    [Test]
    public async Task Valid_settings_succeeds()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, _) = await RunDetailedAsync(context, "--runs", "1", "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Comparison_valid_settings_succeeds()
    {
        var context = CreateComparisonCommandApp();
        var (exitCode, _) = await RunComparisonAsync(context, "--runs", "1");

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task Default_runs_is_three()
    {
        var context = CreateDetailedCommandApp();
        var (exitCode, output) = await RunDetailedAsync(context, "--no-live-estimates");

        await Assert.That(exitCode).IsEqualTo(0);
        // Should see "Run 3/3" since default is 3 runs
        await Assert.That(output).Contains("Run 3/3");
    }
}
