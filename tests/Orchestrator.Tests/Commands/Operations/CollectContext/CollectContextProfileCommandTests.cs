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
    public async Task Arena_profile_loads_exact_context_credentials_once_before_first_collector()
    {
        var calls = new List<string>();
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        credentialLoader
            .Setup(loader => loader.Load("ehonda-ai-arena"))
            .Callback(() => calls.Add("credentials"));
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        executor.Setup(instance => instance.ExecuteAsync(
                It.IsAny<CompetitionCollector>(),
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<CompetitionCollector, CompetitionCollectorExecutionContext, CancellationToken>(
                (collector, _, _) => calls.Add($"collector:{collector}"))
            .ReturnsAsync(0);
        var (app, console) = CreateApp(executor, credentialLoader);

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", " ehonda-ai-arena ",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(calls[0]).IsEqualTo("credentials");
        await Assert.That(calls[1]).StartsWith("collector:");
        credentialLoader.Verify(loader => loader.Load("ehonda-ai-arena"), Times.Once);
    }

    [Test]
    public async Task Explicit_self_contained_profile_uses_trimmed_context_as_credential_identity()
    {
        var executed = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(executed);
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        var (app, console) = CreateApp(executor, credentialLoader);

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", " standalone-community ",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        credentialLoader.Verify(loader => loader.Load("standalone-community"), Times.Once);
        await Assert.That(executed.All(call =>
            call.Context.Community == "standalone-community"
            && call.Context.CommunityContext == "standalone-community")).IsTrue();
    }

    [Test]
    public async Task Explicit_participant_profile_is_loaded_for_context_community()
    {
        var executor = CreateExecutor([]);
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        var (app, console) = CreateApp(executor, credentialLoader);

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "ehonda-ai-arena",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--kicktipp-credential-profile", "gpt-5-6-sol-xhigh",
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        credentialLoader.Verify(
            loader => loader.Load("ehonda-ai-arena", "gpt-5-6-sol-xhigh"),
            Times.Once);
        credentialLoader.Verify(loader => loader.Load("ehonda-ai-arena"), Times.Never);
    }

    [Test]
    public async Task Explicit_Bundesliga_profile_forwards_full_season_scope_to_every_lazy_collector()
    {
        var executed = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(executed);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "ehonda-ai-arena",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--full-season",
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(executed.All(call => call.Context.FullSeason)).IsTrue();
        await Assert.That(output).Contains("Kicktipp scope: full season");
    }

    [Test]
    public async Task Production_source_cycle_options_forward_the_exact_shared_GHA_lane_contract()
    {
        CompetitionCollectorExecutionContext? prepared = null;
        var executor = CreateExecutor([]);
        executor.Setup(instance => instance.PrepareContextSourcesAsync(
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<CompetitionCollectorExecutionContext, CancellationToken>((context, _) => prepared = context)
            .ReturnsAsync((ContextSourceCyclePreparation?)null);
        var (app, console) = CreateApp(executor);

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "pes-squad",
            "--competition", CompetitionIds.Bundesliga2026_27,
            "--enable-roster-source",
            "--context-source-scope", "production-live",
            "--context-source-cycle-id", "gha:123:456",
            "--context-source-current-lane", "pes-squad-context",
            "--context-source-producer-lane", "pes-squad-context",
            "--context-source-consumers", string.Join(',', BundesligaContextSourceContract.ProductionConsumers),
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(prepared).IsNotNull();
        await Assert.That(prepared!.ContextSourceCycle!.Identity.CycleId).IsEqualTo("gha:123:456");
        await Assert.That(prepared.ContextSourceCycle.Identity.Sequence).IsEqualTo(456);
        await Assert.That(prepared.ContextSourceCycle.CurrentLaneId).IsEqualTo("pes-squad-context");
        await Assert.That(prepared.ContextSourceCycle.ProducerLaneId).IsEqualTo("pes-squad-context");
        await Assert.That(prepared.ContextSourceCycle.ExpectedConsumers.SequenceEqual(BundesligaContextSourceContract.ProductionConsumers, StringComparer.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments("pes-squad", null)]
    [Arguments("pes-squad", "development")]
    [Arguments("ehonda-dev-buli-2627", "production-live")]
    public async Task Source_enabled_profiles_fail_closed_without_exact_scope_and_community_lane_authority(string community, string? scope)
    {
        var executor = CreateExecutor([]); var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>(); var (app, console) = CreateApp(executor, credentialLoader);
        var arguments = new List<string> { "collect-context-profile", "--community-context", community, "--competition", CompetitionIds.Bundesliga2026_27, "--enable-roster-source", "--dry-run" };
        if (scope is not null) arguments.AddRange(["--context-source-scope", scope]);
        if (scope == "production-live") arguments.AddRange(["--context-source-cycle-id", "gha:123:456", "--context-source-current-lane", "pes-squad-context", "--context-source-producer-lane", "pes-squad-context", "--context-source-consumers", string.Join(',', BundesligaContextSourceContract.ProductionConsumers)]);

        var (exitCode, _) = await RunCommandAsync(app, console, arguments.ToArray());

        await Assert.That(exitCode).IsEqualTo(1);
        credentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
        executor.Verify(instance => instance.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Exact_development_community_may_allocate_the_fixed_development_cycle()
    {
        CompetitionCollectorExecutionContext? prepared = null; var executor = CreateExecutor([]);
        executor.Setup(instance => instance.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<CompetitionCollectorExecutionContext, CancellationToken>((context, _) => prepared = context).ReturnsAsync((ContextSourceCyclePreparation?)null);
        var (app, console) = CreateApp(executor);

        var (exitCode, _) = await RunCommandAsync(app, console, "collect-context-profile", "--community-context", BundesligaContextSourceContract.DevelopmentCommunity, "--competition", CompetitionIds.Bundesliga2026_27, "--enable-roster-source", "--context-source-scope", "development", "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(prepared).IsNotNull();
        await Assert.That(prepared!.ContextSourceCycle).IsNull();
    }

    [Test]
    public async Task Disabled_sources_ignore_cycle_options_before_preparation_resolution()
    {
        var executor = CreateExecutor([]);
        var (app, console) = CreateApp(executor);
        var (exitCode, _) = await RunCommandAsync(app, console, "collect-context-profile",
            "--community-context", "pes-squad", "--competition", CompetitionIds.Bundesliga2026_27,
            "--context-source-scope", "not-a-scope", "--context-source-cycle-id", "not-a-cycle", "--dry-run");
        await Assert.That(exitCode).IsEqualTo(0);
        executor.Verify(instance => instance.PrepareContextSourcesAsync(It.IsAny<CompetitionCollectorExecutionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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
                && call.Context.CommunityContext == "pes-squad"
                && call.Context.MarkdownSummaryOutput == summaryPath)).IsTrue();
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
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        var (app, console) = CreateApp(executor, credentialLoader);

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
        credentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Missing_explicit_competition_fails_settings_validation_before_any_collector()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        var (app, console) = CreateApp(executor, credentialLoader);

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
        credentialLoader.Verify(loader => loader.Load(It.IsAny<string>()), Times.Never);
    }

    [Test]
    [Arguments("argument")]
    [Arguments("invalid-operation")]
    [Arguments("io")]
    [Arguments("unauthorized")]
    public async Task Credential_loader_failure_is_actionable_redacted_and_prevents_collection(string failureKind)
    {
        const string secretSentinel = "do-not-print-this-password";
        Exception failure = failureKind switch
        {
            "argument" => new ArgumentException(secretSentinel),
            "invalid-operation" => new InvalidOperationException(secretSentinel),
            "io" => new IOException(secretSentinel),
            "unauthorized" => new UnauthorizedAccessException(secretSentinel),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null)
        };
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        var credentialLoader = new Mock<ICommunityKicktippCredentialLoader>();
        credentialLoader
            .Setup(loader => loader.Load("ehonda-ai-arena"))
            .Throws(failure);
        var (app, console) = CreateApp(executor, credentialLoader);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-profile",
            "--community-context", "ehonda-ai-arena",
            "--competition", CompetitionIds.Bundesliga2026_27);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(output)
            .Contains("Unable to load Kicktipp credentials")
            .And.Contains("ehonda-ai-arena")
            .And.Contains("sibling credential file")
            .And.Contains("environment variables")
            .And.DoesNotContain(secretSentinel);
        credentialLoader.Verify(loader => loader.Load("ehonda-ai-arena"), Times.Once);
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
        Mock<ICompetitionProfileCollectorExecutor> executor,
        Mock<ICommunityKicktippCredentialLoader>? credentialLoader = null)
    {
        var resolvedCredentialLoader = credentialLoader ?? new Mock<ICommunityKicktippCredentialLoader>();
        return CreateCommandApp<CollectContextProfileCommand>(
            "collect-context-profile",
            configureServices: new Action<IServiceCollection>(services =>
            {
                services.AddSingleton<ICompetitionCollectionProfileResolver, CompetitionCollectionProfileResolver>();
                services.AddSingleton(executor.Object);
                services.AddSingleton(resolvedCredentialLoader.Object);
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
