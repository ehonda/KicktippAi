using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orchestrator.Commands.Operations.CollectContext;
using Orchestrator.Commands.Operations.Dev;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using static Orchestrator.Tests.Infrastructure.OrchestratorTestFactories;
using Match = EHonda.KicktippAi.Core.Match;

namespace Orchestrator.Tests.Commands.Operations.Dev;

public class CompetitionCollectionProfileTests
{
    private readonly CompetitionCollectionProfileResolver _resolver = new();

    [Test]
    public async Task Bundesliga_profile_declares_the_complete_strict_collection_contract()
    {
        var profile = _resolver.ResolveForDevelopment("ehonda-dev-buli-2627");

        await Assert.That(profile.Competition).IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(profile.Collectors.Select(step => step.Collector).SequenceEqual(
        [
            CompetitionCollector.Kicktipp,
            CompetitionCollector.BundesligaHistoryPlayedDates,
            CompetitionCollector.ClubElo,
            CompetitionCollector.Rosters
        ])).IsTrue();
        await Assert.That(profile.Collectors[1].ExecutionMode)
            .IsEqualTo(CompetitionCollectorExecutionMode.IncludedInPrevious);
        await Assert.That(profile.ExpectedTeamCount).IsEqualTo(18);
        await Assert.That(profile.ExpectedMatchCount).IsEqualTo(306);
        await Assert.That(profile.ExpectedMatchesPerMatchday).IsEqualTo(9);
        await Assert.That(profile.SeasonStartsOn).IsEqualTo(new DateOnly(2026, 8, 28));
        await Assert.That(profile.SeasonEndsOn).IsEqualTo(new DateOnly(2027, 5, 22));
        await Assert.That(profile.RequiredMatchDocumentTemplates).IsEquivalentTo(
        [
            "bundesliga-standings.csv",
            "community-rules-{community-context}.md",
            "recent-history-{home-team-slug}.csv",
            "recent-history-{away-team-slug}.csv",
            "home-history-{home-team-slug}.csv",
            "away-history-{away-team-slug}.csv",
            "head-to-head-{home-team-slug}-vs-{away-team-slug}.csv",
            "roster-{home-team-slug}",
            "roster-{away-team-slug}",
            "club-elo-{home-team-slug}.csv",
            "club-elo-{away-team-slug}.csv"
        ]);
        await Assert.That(profile.RequiredAggregateContextDocuments)
            .IsEquivalentTo([BundesligaRosterPublicationContract.AggregateRosterDocumentName]);
        await Assert.That(profile.RequiredKpiDocuments).IsEquivalentTo(
        [
            BundesligaDocumentPublication.ClubEloRankingsDocumentName,
            BundesligaRosterPublicationContract.SquadSummaryDocumentName
        ]);
        await Assert.That(profile.ContextFeatures)
            .IsEqualTo(new CompetitionContextFeatures(true, true, false, false));
        await Assert.That(profile.PromptRoute.MatchPromptName)
            .IsEqualTo(CompetitionResolver.BundesligaMatchPromptName);
        await Assert.That(profile.PromptRoute.MatchPromptVersion)
            .IsEqualTo(CompetitionResolver.BundesligaMatchPromptVersion);
        await Assert.That(profile.PromptRoute.BonusPromptName)
            .IsEqualTo(CompetitionResolver.BundesligaBonusPromptName);
        await Assert.That(profile.PromptRoute.BonusPromptVersion)
            .IsEqualTo(CompetitionResolver.BundesligaBonusPromptVersion);
        await Assert.That(profile.PromptRoute.Label)
            .IsEqualTo(CompetitionResolver.DefaultBundesligaPromptLabel);
        await Assert.That(RenderRequiredMatchDocuments(
                profile,
                "ehonda-dev-buli-2627",
                "FC Bayern München",
                "Borussia Dortmund",
                knockout: false)
            .SequenceEqual(MatchContextDocumentCatalog.ForMatch(
                "FC Bayern München",
                "Borussia Dortmund",
                "ehonda-dev-buli-2627",
                CompetitionIds.Bundesliga2026_27).RequiredDocumentNames)).IsTrue();
        await Assert.That(profile.ValidationCommands).Count().IsEqualTo(2);
        await Assert.That(profile.ValidationCommands[0]).Contains("--full-season");
    }

    [Test]
    public async Task Bundesliga_profile_rules_document_exists_with_the_verified_live_scoring_contract()
    {
        var profile = _resolver.ResolveForDevelopment(CompetitionResolver.BundesligaDevelopmentCommunity);
        var rulesDocumentName = profile.RequiredMatchDocumentTemplates
            .Single(template => template == "community-rules-{community-context}.md")
            .Replace(
                "{community-context}",
                CompetitionResolver.BundesligaDevelopmentCommunity,
                StringComparison.Ordinal);
        var rulesDirectory = Path.Combine(SolutionPathUtility.FindSolutionRoot(), "community-rules");
        var rulesPath = Path.Combine(rulesDirectory, rulesDocumentName["community-rules-".Length..]);

        await Assert.That(File.Exists(rulesPath)).IsTrue();
        var content = await File.ReadAllTextAsync(rulesPath);
        await Assert.That(content)
            .Contains("| Win         | 2        | 3               | 4            |")
            .And.Contains("| Draw        | 2        | -               | 4            |")
            .And.DoesNotContain("| Draw        | 3");
        await Assert.That(content)
            .IsEqualTo(await File.ReadAllTextAsync(Path.Combine(rulesDirectory, "pes-squad.md")));
        await Assert.That(content)
            .IsEqualTo(await File.ReadAllTextAsync(Path.Combine(rulesDirectory, "ehonda-ai-arena.md")));
    }

    [Test]
    public async Task Wm26_profile_preserves_its_separate_collector_and_document_contract()
    {
        var profile = _resolver.ResolveForDevelopment("EHONDA-DEV-WM26");

        await Assert.That(profile.Competition).IsEqualTo(CompetitionIds.FifaWorldCup2026);
        await Assert.That(profile.Collectors.Select(step => step.Collector).SequenceEqual(
        [
            CompetitionCollector.Kicktipp,
            CompetitionCollector.Wm26HistoryPlayedDates,
            CompetitionCollector.FifaRankings,
            CompetitionCollector.NationalLineups
        ])).IsTrue();
        await Assert.That(profile.Collectors.All(step => step.ExecutionMode == CompetitionCollectorExecutionMode.Direct)).IsTrue();
        await Assert.That(profile.ExpectedTeamCount).IsEqualTo(48);
        await Assert.That(profile.ExpectedMatchCount).IsEqualTo(104);
        await Assert.That(profile.ExpectedMatchesPerMatchday).IsNull();
        await Assert.That(profile.SeasonStartsOn).IsEqualTo(new DateOnly(2026, 6, 11));
        await Assert.That(profile.SeasonEndsOn).IsEqualTo(new DateOnly(2026, 7, 19));
        await Assert.That(profile.RequiredKpiDocuments).IsEquivalentTo(
        [
            CollectContextFifaCommand.FifaRankingsDocumentName,
            CollectContextLineupsCommand.LineupsDocumentName
        ]);
        await Assert.That(profile.RequiredMatchDocumentTemplates)
            .Contains("community-rules-{community-context}-knockout.md")
            .And.Contains("fifa-ranking-{home-team-slug}.csv")
            .And.Contains("lineup-{away-team-slug}.csv")
            .And.DoesNotContain("home-history-{home-team-slug}.csv")
            .And.DoesNotContain("roster-{home-team-slug}");
        await Assert.That(profile.ContextFeatures)
            .IsEqualTo(new CompetitionContextFeatures(false, false, true, false));
        await Assert.That(profile.PromptRoute.MatchPromptName)
            .IsEqualTo(CompetitionResolver.WorldCupMatchPromptName);
        await Assert.That(profile.PromptRoute.MatchPromptVersion).IsNull();
        await Assert.That(profile.PromptRoute.BonusPromptVersion).IsNull();
        await Assert.That(profile.PromptRoute.Label)
            .IsEqualTo(CompetitionResolver.DefaultWorldCupPromptLabel);
        await Assert.That(RenderRequiredMatchDocuments(
                profile,
                "ehonda-dev-wm26",
                "Germany",
                "Brazil",
                knockout: false)
            .SequenceEqual(MatchContextDocumentCatalog.ForMatch(
                "Germany",
                "Brazil",
                "ehonda-dev-wm26",
                CompetitionIds.FifaWorldCup2026).RequiredDocumentNames)).IsTrue();

        var knockoutMatch = new Match("Germany", "Brazil", default, 37)
        {
            CompetitionSpecificData = new FifaWorldCup2026MatchData(
                "Sechzehntelfinale",
                FifaWorldCup2026KnockoutStage.RoundOf32,
                FifaWorldCup2026ResultBasis.FinalScoreIncludingExtraTimeAndPenaltyShootout)
        };
        await Assert.That(RenderRequiredMatchDocuments(
                profile,
                "ehonda-dev-wm26",
                "Germany",
                "Brazil",
                knockout: true)
            .SequenceEqual(MatchContextDocumentCatalog.ForMatch(
                knockoutMatch,
                "ehonda-dev-wm26",
                CompetitionIds.FifaWorldCup2026).RequiredDocumentNames)).IsTrue();
    }

    [Test]
    public async Task Explicit_competition_must_match_the_development_community_profile()
    {
        await Assert.That(() => _resolver.ResolveForDevelopment(
                "ehonda-dev-buli-2627",
                CompetitionIds.FifaWorldCup2026))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Unknown_development_community_fails_before_a_profile_is_selected()
    {
        await Assert.That(() => _resolver.ResolveForDevelopment("pes-squad"))
            .Throws<NotSupportedException>();
        await Assert.That(_resolver.SupportedDevelopmentCommunities)
            .IsEquivalentTo(["ehonda-dev-buli-2627", "ehonda-dev-wm26"]);
    }

    [Test]
    public async Task Profile_and_shared_development_resolvers_cover_the_exact_same_community_pairs()
    {
        await Assert.That(_resolver.SupportedDevelopmentCommunities)
            .IsEquivalentTo(CompetitionResolver.SupportedDevCommunities);

        foreach (var community in CompetitionResolver.SupportedDevCommunities)
        {
            var profile = _resolver.ResolveForDevelopment(community);
            await Assert.That(CompetitionResolver.ResolveDevelopmentCompetition(community))
                .IsEqualTo(profile.Competition);

            var otherCompetition = profile.Competition == CompetitionIds.Bundesliga2026_27
                ? CompetitionIds.FifaWorldCup2026
                : CompetitionIds.Bundesliga2026_27;
            await Assert.That(() => _resolver.ResolveForDevelopment(community, otherCompetition))
                .Throws<InvalidOperationException>();
        }
    }

    private static IReadOnlyList<string> RenderRequiredMatchDocuments(
        CompetitionCollectionProfile profile,
        string communityContext,
        string homeTeam,
        string awayTeam,
        bool knockout)
    {
        var homeSlug = MatchContextDocumentCatalog.GetTeamAbbreviation(homeTeam, profile.Competition);
        var awaySlug = MatchContextDocumentCatalog.GetTeamAbbreviation(awayTeam, profile.Competition);

        return profile.RequiredMatchDocumentTemplates
            .Where(template => knockout
                ? template != "community-rules-{community-context}.md"
                : template != "community-rules-{community-context}-knockout.md")
            .Select(template => template
                .Replace("{community-context}", communityContext, StringComparison.Ordinal)
                .Replace("{home-team-slug}", homeSlug, StringComparison.Ordinal)
                .Replace("{away-team-slug}", awaySlug, StringComparison.Ordinal))
            .ToArray();
    }
}

public class CollectContextDevProfileOrchestrationTests
{
    [Test]
    public async Task Bundesliga_executes_direct_collectors_in_order_and_reports_embedded_history()
    {
        var calls = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(calls);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-buli-2627",
            "--verbose");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(calls.Select(call => call.Collector).SequenceEqual(
        [
            CompetitionCollector.Kicktipp,
            CompetitionCollector.ClubElo,
            CompetitionCollector.Rosters
        ])).IsTrue();
        await Assert.That(calls.All(call => call.Context.Profile.Competition == CompetitionIds.Bundesliga2026_27)).IsTrue();
        await Assert.That(calls.Single(call => call.Collector == CompetitionCollector.Kicktipp)
            .Context.Profile.ExpectedMatchesPerMatchday).IsEqualTo(9);
        await Assert.That(NormalizeWhitespace(output))
            .Contains("Kicktipp -> BundesligaHistoryPlayedDates -> ClubElo -> Rosters")
            .And.Contains("BundesligaHistoryPlayedDates: IncludedInPrevious")
            .And.Contains("completed inside immediately preceding Kicktipp")
            .And.Contains("match-version=2")
            .And.Contains("bonus-version=1")
            .And.Contains("club-elo-rankings")
            .And.Contains("team-squad-summary")
            .And.DoesNotContain("Wm26HistoryPlayedDates");
    }

    [Test]
    public async Task Wm26_executes_only_its_four_direct_collectors_in_order()
    {
        var calls = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(calls);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-wm26");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(calls.Select(call => call.Collector).SequenceEqual(
        [
            CompetitionCollector.Kicktipp,
            CompetitionCollector.Wm26HistoryPlayedDates,
            CompetitionCollector.FifaRankings,
            CompetitionCollector.NationalLineups
        ])).IsTrue();
        await Assert.That(calls.Single(call => call.Collector == CompetitionCollector.Kicktipp)
            .Context.Profile.ExpectedMatchesPerMatchday).IsNull();
        await Assert.That(NormalizeWhitespace(output))
            .Contains("Kicktipp -> Wm26HistoryPlayedDates -> FifaRankings -> NationalLineups")
            .And.Contains("match-version=label-resolved")
            .And.Contains("bonus-version=label-resolved")
            .And.DoesNotContain("BundesligaHistoryPlayedDates")
            .And.DoesNotContain("club-elo-rankings");
    }

    [Test]
    public async Task Dry_run_reaches_every_direct_Bundesliga_collector_and_reports_embedded_history_without_an_extra_call()
    {
        var calls = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(calls);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-buli-2627",
            "--matchdays", "1,2",
            "--dry-run");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(calls).Count().IsEqualTo(3);
        await Assert.That(calls.All(call => call.Context.DryRun)).IsTrue();
        await Assert.That(calls.All(call => call.Context.Matchdays == "1,2")).IsTrue();
        await Assert.That(calls.Any(call => call.Collector == CompetitionCollector.BundesligaHistoryPlayedDates)).IsFalse();
        await Assert.That(NormalizeWhitespace(output))
            .Contains("BundesligaHistoryPlayedDates: IncludedInPreviousDryRun")
            .And.Contains("Competition profile dry run completed")
            .And.Contains("every selected collector was validated without writes");
    }

    [Test]
    public async Task Failure_short_circuits_and_reports_every_remaining_collector_as_skipped()
    {
        var calls = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(calls, failedCollector: CompetitionCollector.ClubElo, failureExitCode: 7);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(7);
        await Assert.That(calls.Select(call => call.Collector).SequenceEqual(
        [
            CompetitionCollector.Kicktipp,
            CompetitionCollector.ClubElo
        ])).IsTrue();
        await Assert.That(output)
            .Contains("Collector ClubElo: Failed")
            .And.Contains("Collector Rosters: SkippedAfterFailure")
            .And.DoesNotContain("Competition profile collection completed");
    }

    [Test]
    public async Task Full_season_kicktipp_failure_stops_before_elo_and_roster_construction()
    {
        var calls = new List<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)>();
        var executor = CreateExecutor(calls, failedCollector: CompetitionCollector.Kicktipp, failureExitCode: 9);
        var (app, console) = CreateApp(executor);

        var (exitCode, output) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-buli-2627",
            "--full-season");

        await Assert.That(exitCode).IsEqualTo(9);
        await Assert.That(calls).Count().IsEqualTo(1);
        await Assert.That(calls[0].Collector).IsEqualTo(CompetitionCollector.Kicktipp);
        await Assert.That(calls[0].Context.FullSeason).IsTrue();
        await Assert.That(output)
            .Contains("Collector ClubElo: SkippedAfterFailure")
            .And.Contains("Collector Rosters: SkippedAfterFailure")
            .And.DoesNotContain("Competition profile collection completed");
    }

    [Test]
    public async Task Profile_is_printed_before_the_first_collector_is_invoked()
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        string? outputAtFirstCall = null;
        TestConsole? capturedConsole = null;
        executor.Setup(value => value.ExecuteAsync(
                It.IsAny<CompetitionCollector>(),
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => outputAtFirstCall = capturedConsole!.Output)
            .ReturnsAsync(0);
        var (app, console) = CreateApp(executor);
        capturedConsole = console;

        var (exitCode, _) = await RunCommandAsync(
            app,
            console,
            "collect-context-dev",
            "--community", "ehonda-dev-buli-2627");

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(outputAtFirstCall)
            .Contains("Competition: bundesliga-2026-27")
            .And.Contains("Required match documents")
            .And.Contains("Validation commands")
            .And.Contains("Running collector: Kicktipp");
    }

    private static Mock<ICompetitionProfileCollectorExecutor> CreateExecutor(
        ICollection<(CompetitionCollector Collector, CompetitionCollectorExecutionContext Context)> calls,
        CompetitionCollector? failedCollector = null,
        int failureExitCode = 1)
    {
        var executor = new Mock<ICompetitionProfileCollectorExecutor>();
        executor.Setup(value => value.ExecuteAsync(
                It.IsAny<CompetitionCollector>(),
                It.IsAny<CompetitionCollectorExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompetitionCollector collector, CompetitionCollectorExecutionContext context, CancellationToken _) =>
            {
                calls.Add((collector, context));
                return collector == failedCollector ? failureExitCode : 0;
            });
        return executor;
    }

    private static (CommandApp App, TestConsole Console) CreateApp(
        Mock<ICompetitionProfileCollectorExecutor> executor)
    {
        return CreateCommandApp<CollectContextDevCommand>(
            "collect-context-dev",
            configureServices: new Action<IServiceCollection>(services =>
            {
                services.AddSingleton<ICompetitionCollectionProfileResolver, CompetitionCollectionProfileResolver>();
                services.AddSingleton(executor.Object);
            }));
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public class CompetitionCollectionProfileServiceRegistrationTests
{
    [Test]
    public async Task Collect_context_dev_services_register_the_profile_resolver_executor_and_all_profile_sources()
    {
        var services = new ServiceCollection();

        services.AddCollectContextDevCommandServices();

        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICompetitionCollectionProfileResolver)
            && descriptor.ImplementationType == typeof(CompetitionCollectionProfileResolver))).IsTrue();
        await Assert.That(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICompetitionProfileCollectorExecutor)
            && descriptor.ImplementationType == typeof(CompetitionProfileCollectorExecutor))).IsTrue();
        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IBundesligaClubEloSource))).IsTrue();
        await Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(IBundesligaRosterSource))).IsTrue();
    }
}

public class DevParticipationProfileDefaultsTests
{
    [Test]
    [Arguments(false, CompetitionResolver.BundesligaMatchPromptName, CompetitionResolver.BundesligaMatchPromptVersion)]
    [Arguments(true, CompetitionResolver.BundesligaBonusPromptName, CompetitionResolver.BundesligaBonusPromptVersion)]
    public async Task Bundesliga_dev_shortcuts_use_the_exact_accepted_validation_identity(
        bool bonusPrompt,
        string expectedPromptName,
        int expectedPromptVersion)
    {
        var console = new TestConsole();
        var settings = new DevParticipationSettings
        {
            Community = "ehonda-dev-buli-2627"
        };

        var accepted = DevParticipationCommandSupport.TryCreateBaseSettings(
            settings,
            console,
            bonusPrompt ? "bonus-dev" : "matchday-dev",
            bonusPrompt,
            showContextDocuments: false,
            out var baseSettings);

        await Assert.That(accepted).IsTrue();
        await Assert.That(baseSettings.Competition).IsEqualTo(CompetitionIds.Bundesliga2026_27);
        await Assert.That(baseSettings.Model).IsEqualTo(CompetitionResolver.BundesligaValidationModel);
        await Assert.That(baseSettings.ReasoningEffort).IsEqualTo(CompetitionResolver.BundesligaValidationReasoningEffort);
        await Assert.That(baseSettings.MaxOutputTokenCount)
            .IsEqualTo(CompetitionResolver.BundesligaValidationMaxOutputTokenCount);
        await Assert.That(baseSettings.PromptSource).IsEqualTo(CompetitionResolver.LangfusePromptSource);
        await Assert.That(baseSettings.LangfusePromptName).IsEqualTo(expectedPromptName);
        await Assert.That(baseSettings.LangfusePromptLabel)
            .IsEqualTo(CompetitionResolver.DefaultBundesligaPromptLabel);
        await Assert.That(baseSettings.LangfusePromptVersion).IsEqualTo(expectedPromptVersion);
        await Assert.That(NormalizeWhitespace(console.Output))
            .Contains("Bundesliga validation identity:")
            .And.Contains("model=gpt-5.6-luna")
            .And.Contains("reasoning=none")
            .And.Contains("max-output-tokens=10000")
            .And.Contains($"prompt={expectedPromptName}")
            .And.Contains($"prompt-version={expectedPromptVersion}");
    }

    [Test]
    public async Task Wm26_dev_shortcut_preserves_its_existing_label_resolved_defaults()
    {
        var console = new TestConsole();
        var settings = new DevParticipationSettings
        {
            Community = "ehonda-dev-wm26"
        };

        var accepted = DevParticipationCommandSupport.TryCreateBaseSettings(
            settings,
            console,
            "matchday-dev",
            bonusPrompt: false,
            showContextDocuments: false,
            out var baseSettings);

        await Assert.That(accepted).IsTrue();
        await Assert.That(baseSettings.Competition).IsEqualTo(CompetitionIds.FifaWorldCup2026);
        await Assert.That(baseSettings.PromptSource).IsNull();
        await Assert.That(baseSettings.LangfusePromptName).IsNull();
        await Assert.That(baseSettings.LangfusePromptVersion).IsNull();
        await Assert.That(baseSettings.Model).IsEqualTo(PredictionServiceCommandSupport.WorldCupDevDefaultModel);
        await Assert.That(baseSettings.ReasoningEffort)
            .IsEqualTo(PredictionServiceCommandSupport.WorldCupDevDefaultReasoningEffort);
        await Assert.That(baseSettings.MaxOutputTokenCount).IsNull();
        await Assert.That(console.Output).DoesNotContain("Bundesliga validation identity");
    }

    [Test]
    [Arguments("ehonda-dev-buli-2627", CompetitionIds.FifaWorldCup2026)]
    [Arguments("ehonda-dev-wm26", CompetitionIds.Bundesliga2026_27)]
    public async Task Dev_shortcut_rejects_a_mismatched_community_and_competition_before_creating_settings(
        string community,
        string competition)
    {
        var console = new TestConsole();
        var settings = new DevParticipationSettings
        {
            Community = community,
            Competition = competition
        };

        var accepted = DevParticipationCommandSupport.TryCreateBaseSettings(
            settings,
            console,
            "matchday-dev",
            bonusPrompt: false,
            showContextDocuments: false,
            out var baseSettings);

        await Assert.That(accepted).IsFalse();
        await Assert.That(baseSettings).IsNull();
        await Assert.That(console.Output)
            .Contains($"Development community '{community}' uses competition")
            .And.Contains($"not '{competition}'");
    }

    [Test]
    public async Task Dev_shortcut_reports_an_unknown_explicit_competition_without_creating_settings()
    {
        var console = new TestConsole();
        var settings = new DevParticipationSettings
        {
            Community = CompetitionResolver.BundesligaDevelopmentCommunity,
            Competition = "unknown-competition"
        };

        var accepted = DevParticipationCommandSupport.TryCreateBaseSettings(
            settings,
            console,
            "matchday-dev",
            bonusPrompt: false,
            showContextDocuments: false,
            out var baseSettings);

        await Assert.That(accepted).IsFalse();
        await Assert.That(baseSettings).IsNull();
        await Assert.That(NormalizeWhitespace(console.Output))
            .Contains("Competition 'unknown-competition' is not supported for development commands")
            .And.DoesNotContain("Parameter")
            .And.DoesNotContain("Actual value");
    }


    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
