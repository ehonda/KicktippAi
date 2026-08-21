using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Infrastructure;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;

namespace Orchestrator.Tests.Commands.Operations.CollectContext;

public class CollectContextProfileCommandTests
{
    [Test]
    public async Task Bundesliga_profile_runs_only_its_direct_collectors_and_writes_stable_summary()
    {
        var executed = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(executed);
        var (app, console) = CreateApp(executor);
        var summaryPath = CreateSummaryPath();

        try
        {
            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-profile",
                "--community-context", "pes-squad",
                "--competition", CompetitionIds.Bundesliga2026_27,
                "--markdown-summary-output", summaryPath,
                "--verbose");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(string.Join(",", executed.Select(call => call.Collector)))
                .IsEqualTo("Kicktipp,ClubElo,Rosters");
            await Assert.That(executed.All(call =>
                call.Context.Profile.Competition == CompetitionIds.Bundesliga2026_27
                && call.Context.CommunityContext == "pes-squad")).IsTrue();
            await Assert.That(output)
                .Contains("Kicktipp -> BundesligaHistoryPlayedDates -> ClubElo -> Rosters")
                .And.Contains("BundesligaHistoryPlayedDates: IncludedInPrevious")
                .And.DoesNotContain("Running collector: FifaRankings")
                .And.DoesNotContain("Running collector: NationalLineups")
                .And.DoesNotContain("Running collector: Wm26HistoryPlayedDates");

            var summary = await File.ReadAllTextAsync(summaryPath);
            await Assert.That(summary)
                .Contains("## Context Collection Profile Results")
                .And.Contains("**Resolved profile:** Bundesliga 2026/27 (`bundesliga-2026-27`)")
                .And.Contains("`Kicktipp`: `Succeeded`")
                .And.Contains("`BundesligaHistoryPlayedDates`: `IncludedInPrevious`")
                .And.Contains("`ClubElo`: `Succeeded`")
                .And.Contains("`Rosters`: `Succeeded`")
                .And.DoesNotContain("FifaRankings")
                .And.DoesNotContain("NationalLineups")
                .And.DoesNotContain("Wm26HistoryPlayedDates")
                .And.DoesNotContain("Transfers");
        }
        finally
        {
            File.Delete(summaryPath);
        }
    }

    [Test]
    public async Task Wm26_profile_remains_callable_through_the_same_explicit_contract()
    {
        var executed = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(executed);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "rabetrabauken2026",
            "--competition", CompetitionIds.FifaWorldCup2026,
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(string.Join(",", executed.Select(call => call.Collector)))
            .IsEqualTo("Kicktipp,Wm26HistoryPlayedDates,FifaRankings,NationalLineups");
        await Assert.That(executed.All(call => call.Context.DryRun)).IsTrue();
        await Assert.That(NormalizeWhitespace(output))
            .Contains("FIFA World Cup 2026")
            .And.Contains("Kicktipp -> Wm26HistoryPlayedDates -> FifaRankings -> NationalLineups")
            .And.DoesNotContain("Running collector: ClubElo")
            .And.DoesNotContain("Running collector: Rosters");
    }

    [Test]
    public async Task Collector_failure_is_summarized_and_short_circuits_all_later_collectors()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        executor.Setup(instance => instance.ExecuteAsync(
                CompetitionCollector.Kicktipp,
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var (app, console) = CreateApp(executor);
        var summaryPath = CreateSummaryPath();

        try
        {
            var (exitCode, output) = await RunCommandAsync(
                app,
                console,
                "collect-context-profile",
                "--community-context", "schadensfresse",
                "--competition", CompetitionIds.Bundesliga2026_27,
                "--markdown-summary-output", summaryPath);

            await Assert.That(exitCode).IsEqualTo(7);
            executor.Verify(instance => instance.ExecuteAsync(
                It.IsAny<CompetitionCollector>(),
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()), Times.Once);
            await Assert.That(output)
                .Contains("Kicktipp: Failed")
                .And.Contains("BundesligaHistoryPlayedDates: SkippedAfterFailure")
                .And.Contains("ClubElo: SkippedAfterFailure")
                .And.Contains("Rosters: SkippedAfterFailure");

            var summary = await File.ReadAllTextAsync(summaryPath);
            await Assert.That(summary)
                .Contains("**Result:** failed (exit code 7)")
                .And.Contains("`Kicktipp`: `Failed`")
                .And.Contains("`BundesligaHistoryPlayedDates`: `SkippedAfterFailure`")
                .And.Contains("`ClubElo`: `SkippedAfterFailure`")
                .And.Contains("`Rosters`: `SkippedAfterFailure`");
        }
        finally
        {
            File.Delete(summaryPath);
        }
    }

    [Test]
    public async Task Explicit_cross_competition_dev_context_fails_before_any_collector()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", CompetitionResolver.BundesligaDevelopmentCommunity,
            "--competition", CompetitionIds.FifaWorldCup2026);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output).Contains("conflicts with the resolved competition");
        executor.Verify(instance => instance.ExecuteAsync(
            It.IsAny<CompetitionCollector>(),
            It.IsAny<CompetitionCollectorExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Missing_explicit_competition_fails_settings_validation_before_any_collector()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "pes-squad");

        await Assert.That(exitCode).IsNotEqualTo(0);
        await Assert.That(output).Contains("--competition is required");
        executor.Verify(instance => instance.ExecuteAsync(
            It.IsAny<CompetitionCollector>(),
            It.IsAny<CompetitionCollectorExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ICompetitionProfileCollectorExecutor> CreateExecutor(
        ICollection<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)> calls)
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        executor.Setup(instance => instance.ExecuteAsync(
                It.IsAny<CompetitionCollector>(),
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<CompetitionCollector, CompetitionCollectorExecutionContext, CancellationToken>(
                (collector, context, _) => calls.Add((collector, context)))
            .ReturnsAsync(0);
        return executor;
    }

    private static (Spectre.Console.Cli.CommandApp App, Spectre.Console.Testing.TestConsole Console) CreateApp(
        Mock<ICompetitionProfileCollectorExecutor> executor)
    {
        return CreateCommandApp<CollectContextProfileCommand>(
            "collect-context-profile",
            configureServices: new Action<IServiceCollection>(services =>
            {
                services.AddSingleton<ICompetitionCollectionProfileResolver, CompetitionCollectionProfileResolver>();
                services.AddSingleton(executor.Object);
            }));
    }

    private static string CreateSummaryPath()
    {
        return Path.Combine(Path.GetTempPath(), $"kicktippai-context-profile-{Guid.NewGuid():N}.md");
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
