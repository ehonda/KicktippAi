using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Core;
using FirebaseAdapter;
using OpenAiIntegration;
using KicktippIntegration;

namespace Orchestrator.Commands;

public class AnalyzeMatchDetailedCommand : AsyncCommand<AnalyzeMatchDetailedSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeMatchDetailedSettings settings)
    {
        var loggerFactory = AnalyzeMatchCommandHelpers.CreateLoggerFactory(settings.Debug);
        var logger = loggerFactory.CreateLogger<AnalyzeMatchDetailedCommand>();

        try
        {
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var validation = settings.Validate();
            if (!validation.Successful)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {validation.Message}");
                return 1;
            }

            var services = new ServiceCollection();
            AnalyzeMatchCommandHelpers.ConfigureServices(services, settings, logger);
            using var serviceProvider = services.BuildServiceProvider();

            var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
            var tokenUsageTracker = serviceProvider.GetRequiredService<ITokenUsageTracker>();
            var contextRepository = serviceProvider.GetService<IContextRepository>();

            var communityContext = settings.CommunityContext!;

            AnsiConsole.MarkupLine($"[green]Analyze match initialized with model:[/] [yellow]{settings.Model}[/]");
            AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
            AnsiConsole.MarkupLine($"[blue]Runs:[/] [yellow]{settings.Runs}[/]");

            if (settings.Debug)
            {
                AnsiConsole.MarkupLine("[dim]Debug logging enabled[/]");
            }

            var match = await AnalyzeMatchCommandHelpers.ResolveMatchAsync(settings, serviceProvider, logger, communityContext);
            if (match == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to resolve match details. Aborting.[/]");
                return 1;
            }

            var contextDocuments = new List<DocumentContext>();

            if (contextRepository != null)
            {
                var contextDocumentInfos = await AnalyzeMatchCommandHelpers.GetMatchContextDocumentsAsync(
                    contextRepository,
                    match.HomeTeam,
                    match.AwayTeam,
                    communityContext,
                    settings.Verbose);

                contextDocuments = contextDocumentInfos.Select(info => info.Document).ToList();

                if (contextDocumentInfos.Any())
                {
                    AnsiConsole.MarkupLine("[dim]Loaded context documents:[/]");
                    foreach (var info in contextDocumentInfos)
                    {
                        AnsiConsole.MarkupLine($"[grey]  • {info.Document.Name}[/] [dim](v{info.Version})[/]");

                        if (settings.ShowContextDocuments)
                        {
                            var lines = info.Document.Content.Split('\n');
                            foreach (var line in lines.Take(10))
                            {
                                AnsiConsole.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }

                            if (lines.Length > 10)
                            {
                                AnsiConsole.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No context documents retrieved; proceeding without additional context[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Context repository not configured. Proceeding without context documents.[/]");
            }

            tokenUsageTracker.Reset();

            var predictions = new List<Prediction>();
            var runMetrics = new List<RunMetric>();
            var enableLiveEstimates = !settings.NoLiveEstimates;

            string FormatCurrencyValue(decimal value) => $"${value.ToString("F4", CultureInfo.InvariantCulture)}";
            string FormatCurrencyOptional(decimal? value) => value.HasValue ? FormatCurrencyValue(value.Value) : "n/a";

            string FormatDurationValue(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
            string FormatDurationOptional(TimeSpan? value) => value.HasValue ? FormatDurationValue(value.Value) : "n/a";

            IRenderable BuildSummary()
            {
                var completedRuns = runMetrics.Count;
                var remainingRuns = Math.Max(settings.Runs - completedRuns, 0);
                var successfulRuns = runMetrics.Where(metric => metric.Success).ToList();
                var costValues = successfulRuns
                    .Where(metric => metric.Cost.HasValue)
                    .Select(metric => metric.Cost!.Value)
                    .ToList();

                var totalCostSoFar = costValues.Aggregate(0m, (sum, value) => sum + value);
                decimal? averageCost = costValues.Count > 0 ? totalCostSoFar / costValues.Count : (decimal?)null;
                decimal? projectedCost = averageCost.HasValue ? averageCost.Value * settings.Runs : (decimal?)null;

                var totalDuration = runMetrics.Aggregate(TimeSpan.Zero, (current, metric) => current + metric.Duration);
                TimeSpan? averageDuration = runMetrics.Count > 0
                    ? TimeSpan.FromTicks(totalDuration.Ticks / runMetrics.Count)
                    : (TimeSpan?)null;
                TimeSpan? estimatedRemaining = averageDuration.HasValue && remainingRuns > 0
                    ? TimeSpan.FromTicks(averageDuration.Value.Ticks * remainingRuns)
                    : (TimeSpan?)null;

                var table = new Table()
                    .Title("[bold yellow]Live Estimates[/]")
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[grey]Metric[/]").LeftAligned())
                    .AddColumn(new TableColumn("[grey]Value[/]").LeftAligned());

                table.AddRow("Completed runs", $"{completedRuns}/{settings.Runs}");
                table.AddRow("Successful predictions", $"{successfulRuns.Count}/{settings.Runs}");
                table.AddRow("Total cost so far", FormatCurrencyValue(totalCostSoFar));
                table.AddRow("Average cost", FormatCurrencyOptional(averageCost));
                table.AddRow("Projected total cost", FormatCurrencyOptional(projectedCost));
                table.AddRow("Average run time", FormatDurationOptional(averageDuration));
                table.AddRow("Estimated remaining time", FormatDurationOptional(estimatedRemaining));
                table.AddRow("Elapsed time", FormatDurationValue(totalDuration));

                return table;
            }

            Action refreshSummary = () => { };

            async Task ExecuteRunsAsync()
            {
                for (var run = 1; run <= settings.Runs; run++)
                {
                    var stopwatch = Stopwatch.StartNew();

                    AnsiConsole.MarkupLine($"[cyan]\nRun {run}/{settings.Runs}[/]");

                    var prediction = await predictionService.PredictMatchAsync(
                        match,
                        contextDocuments,
                        includeJustification: true);

                    stopwatch.Stop();

                    if (prediction == null)
                    {
                        AnsiConsole.MarkupLine("[red]  ✗ Prediction failed[/]");
                        runMetrics.Add(new RunMetric(run, stopwatch.Elapsed, false, null));
                        refreshSummary();
                        continue;
                    }

                    predictions.Add(prediction);

                    var lastCost = tokenUsageTracker.GetLastCost();
                    var usageSummary = tokenUsageTracker.GetLastUsageCompactSummary();

                    runMetrics.Add(new RunMetric(run, stopwatch.Elapsed, true, lastCost));

                    AnsiConsole.MarkupLine($"[green]  ✓ Prediction:[/] [yellow]{prediction.HomeGoals}:{prediction.AwayGoals}[/]");

                    JustificationConsoleWriter.WriteJustification(
                        prediction.Justification,
                        "[cyan]  ↳ Justification:[/]",
                        "      ",
                        "[yellow]  ↳ Justification: no explanation returned by model[/]");

                    AnsiConsole.MarkupLine($"[magenta]  ↳ Cost:[/] [cyan]{FormatCurrencyValue(lastCost)}[/] [grey]({usageSummary})[/]");

                    refreshSummary();
                }

                refreshSummary();
            }

            if (enableLiveEstimates)
            {
                await AnsiConsole.Live(BuildSummary())
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        refreshSummary = () =>
                        {
                            ctx.UpdateTarget(BuildSummary());
                            ctx.Refresh();
                        };

                        refreshSummary();
                        await ExecuteRunsAsync();
                    });
            }
            else
            {
                await ExecuteRunsAsync();
                AnsiConsole.Write(BuildSummary());
            }

            if (predictions.Any())
            {
                AnsiConsole.MarkupLine($"\n[blue]Total runs with predictions:[/] [yellow]{predictions.Count}/{settings.Runs}[/]");
                AnsiConsole.MarkupLine($"[blue]Total cost:[/] [yellow]{FormatCurrencyValue(tokenUsageTracker.GetTotalCost())}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]No successful predictions generated.[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing analyze-match command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            loggerFactory.Dispose();
        }
    }

    private sealed record RunMetric(int RunNumber, TimeSpan Duration, bool Success, decimal? Cost);
}

public class AnalyzeMatchComparisonCommand : AsyncCommand<AnalyzeMatchComparisonSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeMatchComparisonSettings settings)
    {
        var loggerFactory = AnalyzeMatchCommandHelpers.CreateLoggerFactory(settings.Debug);
        var logger = loggerFactory.CreateLogger<AnalyzeMatchComparisonCommand>();

        try
        {
            EnvironmentHelper.LoadEnvironmentVariables(logger);

            var validation = settings.Validate();
            if (!validation.Successful)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {validation.Message}");
                return 1;
            }

            var services = new ServiceCollection();
            AnalyzeMatchCommandHelpers.ConfigureServices(services, settings, logger);
            using var serviceProvider = services.BuildServiceProvider();

            var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
            var tokenUsageTracker = serviceProvider.GetRequiredService<ITokenUsageTracker>();
            var contextRepository = serviceProvider.GetService<IContextRepository>();

            var communityContext = settings.CommunityContext!;

            AnsiConsole.MarkupLine($"[green]Analyze match comparison initialized with model:[/] [yellow]{settings.Model}[/]");
            AnsiConsole.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
            AnsiConsole.MarkupLine($"[blue]Runs per mode:[/] [yellow]{settings.Runs}[/]");

            if (settings.Debug)
            {
                AnsiConsole.MarkupLine("[dim]Debug logging enabled[/]");
            }

            var match = await AnalyzeMatchCommandHelpers.ResolveMatchAsync(settings, serviceProvider, logger, communityContext);
            if (match == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to resolve match details. Aborting.[/]");
                return 1;
            }

            var contextDocuments = new List<DocumentContext>();

            if (contextRepository != null)
            {
                var contextDocumentInfos = await AnalyzeMatchCommandHelpers.GetMatchContextDocumentsAsync(
                    contextRepository,
                    match.HomeTeam,
                    match.AwayTeam,
                    communityContext,
                    settings.Verbose);

                contextDocuments = contextDocumentInfos.Select(info => info.Document).ToList();

                if (contextDocumentInfos.Any())
                {
                    AnsiConsole.MarkupLine("[dim]Loaded context documents:[/]");
                    foreach (var info in contextDocumentInfos)
                    {
                        AnsiConsole.MarkupLine($"[grey]  • {info.Document.Name}[/] [dim](v{info.Version})[/]");

                        if (settings.ShowContextDocuments)
                        {
                            var lines = info.Document.Content.Split('\n');
                            foreach (var line in lines.Take(10))
                            {
                                AnsiConsole.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }

                            if (lines.Length > 10)
                            {
                                AnsiConsole.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No context documents retrieved; proceeding without additional context[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Context repository not configured. Proceeding without context documents.[/]");
            }

            tokenUsageTracker.Reset();

            string FormatCurrencyValue(decimal value) => $"${value.ToString("F4", CultureInfo.InvariantCulture)}";
            string FormatCurrencyOptional(decimal? value) => value.HasValue ? FormatCurrencyValue(value.Value) : "n/a";
            string FormatDurationValue(TimeSpan value) => value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
            string FormatDurationOptional(TimeSpan? value) => value.HasValue ? FormatDurationValue(value.Value) : "n/a";
            string ModeLabel(bool includeJustification) => includeJustification ? "with justification" : "without justification";

            var runResults = new List<ComparisonRunResult>();

            for (var run = 1; run <= settings.Runs; run++)
            {
                runResults.Add(await ExecuteSingleRunAsync(run, includeJustification: true));
                runResults.Add(await ExecuteSingleRunAsync(run, includeJustification: false));
            }

            PrintSummary(runResults);

            return 0;

            async Task<ComparisonRunResult> ExecuteSingleRunAsync(int runNumber, bool includeJustification)
            {
                var label = ModeLabel(includeJustification);
                AnsiConsole.MarkupLine($"[cyan]\nRun {runNumber}/{settings.Runs} — {label}[/]");

                var stopwatch = Stopwatch.StartNew();
                var prediction = await predictionService.PredictMatchAsync(
                    match,
                    contextDocuments,
                    includeJustification);
                stopwatch.Stop();

                if (prediction == null)
                {
                    AnsiConsole.MarkupLine("[red]  ✗ Prediction failed[/]");
                    return new ComparisonRunResult(runNumber, includeJustification, null, stopwatch.Elapsed, null, false);
                }

                var lastCost = tokenUsageTracker.GetLastCost();

                AnsiConsole.MarkupLine($"[green]  ✓ Prediction:[/] [yellow]{prediction.HomeGoals}:{prediction.AwayGoals}[/]");
                AnsiConsole.MarkupLine($"[magenta]  ↳ Cost:[/] [cyan]{FormatCurrencyValue(lastCost)}[/]");

                return new ComparisonRunResult(runNumber, includeJustification, prediction, stopwatch.Elapsed, lastCost, true);
            }

            void PrintSummary(List<ComparisonRunResult> results)
            {
                AnsiConsole.MarkupLine("\n[bold yellow]Comparison Summary[/]");

                var summaryTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[grey]Mode[/]").LeftAligned())
                    .AddColumn(new TableColumn("[grey]Successful[/]").RightAligned())
                    .AddColumn(new TableColumn("[grey]Failed[/]").RightAligned())
                    .AddColumn(new TableColumn("[grey]Total cost[/]").RightAligned())
                    .AddColumn(new TableColumn("[grey]Average cost[/]").RightAligned())
                    .AddColumn(new TableColumn("[grey]Average duration[/]").RightAligned());

                foreach (var includeJustification in new[] { true, false })
                {
                    var modeResults = results.Where(r => r.IncludeJustification == includeJustification).ToList();
                    var successful = modeResults.Where(r => r.Success).ToList();
                    var failures = modeResults.Count - successful.Count;
                    var totalCost = successful.Where(r => r.Cost.HasValue).Sum(r => r.Cost!.Value);
                    var averageCost = successful.Count > 0 ? totalCost / successful.Count : (decimal?)null;
                    var totalDuration = modeResults.Aggregate(TimeSpan.Zero, (current, result) => current + result.Duration);
                    TimeSpan? averageDuration = modeResults.Count > 0
                        ? TimeSpan.FromTicks(totalDuration.Ticks / modeResults.Count)
                        : (TimeSpan?)null;

                    summaryTable.AddRow(
                        includeJustification ? "With justification" : "Without justification",
                        successful.Count.ToString(CultureInfo.InvariantCulture),
                        failures.ToString(CultureInfo.InvariantCulture),
                        FormatCurrencyValue(totalCost),
                        FormatCurrencyOptional(averageCost),
                        FormatDurationOptional(averageDuration));
                }

                summaryTable.AddRow(
                    "Combined",
                    results.Count(r => r.Success).ToString(CultureInfo.InvariantCulture),
                    results.Count(r => !r.Success).ToString(CultureInfo.InvariantCulture),
                    FormatCurrencyValue(tokenUsageTracker.GetTotalCost()),
                    "n/a",
                    "n/a");

                AnsiConsole.Write(summaryTable);

                var distributions = results
                    .Where(r => r.Success && r.Prediction != null)
                    .GroupBy(r => new
                    {
                        r.IncludeJustification,
                        Score = $"{r.Prediction!.HomeGoals}:{r.Prediction!.AwayGoals}"
                    })
                    .ToDictionary(group => group.Key, group => group.Count());

                if (!distributions.Any())
                {
                    AnsiConsole.MarkupLine("[yellow]No successful predictions to compare.[/]");
                    return;
                }

                var scores = distributions.Keys
                    .Select(key => key.Score)
                    .Distinct()
                    .OrderBy(score => score, StringComparer.Ordinal)
                    .ToList();

                var distributionTable = new Table()
                    .Title("[bold blue]Prediction distribution[/]")
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[grey]Score[/]").LeftAligned())
                    .AddColumn(new TableColumn("[grey]With justification[/]").RightAligned())
                    .AddColumn(new TableColumn("[grey]Without justification[/]").RightAligned());

                foreach (var score in scores)
                {
                    distributions.TryGetValue(new { IncludeJustification = true, Score = score }, out var withCount);
                    distributions.TryGetValue(new { IncludeJustification = false, Score = score }, out var withoutCount);

                    distributionTable.AddRow(
                        score,
                        withCount.ToString(CultureInfo.InvariantCulture),
                        withoutCount.ToString(CultureInfo.InvariantCulture));
                }

                AnsiConsole.Write(distributionTable);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing analyze-match comparison command");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            loggerFactory.Dispose();
        }
    }

    private sealed record ComparisonRunResult(int RunNumber, bool IncludeJustification, Prediction? Prediction, TimeSpan Duration, decimal? Cost, bool Success);
}
public class AnalyzeMatchBaseSettings : CommandSettings
{
    [CommandArgument(0, "<MODEL>")]
    [Description("The OpenAI model to use for prediction (e.g., gpt-4o-mini, o4-mini)")]
    public string Model { get; set; } = string.Empty;

    [CommandOption("--community-context")]
    [Description("The Kicktipp community identifier used for both schedule lookups and context documents")]
    public string CommunityContext { get; set; } = string.Empty;

    [CommandOption("--home")]
    [Description("Home team name")]
    public string HomeTeam { get; set; } = string.Empty;

    [CommandOption("--away")]
    [Description("Away team name")]
    public string AwayTeam { get; set; } = string.Empty;

    [CommandOption("--matchday")]
    [Description("Matchday number for the selected match")]
    public int? Matchday { get; set; }

    [CommandOption("-n|--runs")]
    [Description("Number of runs to execute")]
    [DefaultValue(3)]
    public int Runs { get; set; } = 3;

    [CommandOption("--verbose")]
    [Description("Enable verbose output for context retrieval")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }

    [CommandOption("--show-context-documents")]
    [Description("Print the list of loaded context documents")]
    [DefaultValue(false)]
    public bool ShowContextDocuments { get; set; }

    [CommandOption("--debug")]
    [Description("Enable detailed logging output")]
    [DefaultValue(false)]
    public bool Debug { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Model))
        {
            return ValidationResult.Error("Model is required");
        }

        if (string.IsNullOrWhiteSpace(CommunityContext))
        {
            return ValidationResult.Error("--community-context is required");
        }

        if (string.IsNullOrWhiteSpace(HomeTeam))
        {
            return ValidationResult.Error("--home must be provided");
        }

        if (string.IsNullOrWhiteSpace(AwayTeam))
        {
            return ValidationResult.Error("--away must be provided");
        }

        if (!Matchday.HasValue)
        {
            return ValidationResult.Error("--matchday must be provided");
        }

        if (Runs <= 0)
        {
            return ValidationResult.Error("--runs must be greater than 0");
        }

        return ValidationResult.Success();
    }
}

public class AnalyzeMatchDetailedSettings : AnalyzeMatchBaseSettings
{
    [CommandOption("--no-live-estimates")]
    [Description("Disable live cost and time estimates during execution")]
    [DefaultValue(false)]
    public bool NoLiveEstimates { get; set; }
}

public class AnalyzeMatchComparisonSettings : AnalyzeMatchBaseSettings
{
}

internal sealed record AnalyzeMatchContextDocumentInfo(DocumentContext Document, int Version);

internal static class AnalyzeMatchCommandHelpers
{
    public static ILoggerFactory CreateLoggerFactory(bool debug)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            });
            builder.SetMinimumLevel(debug ? LogLevel.Information : LogLevel.Error);
        });
    }

    public static void ConfigureServices(IServiceCollection services, AnalyzeMatchBaseSettings settings, ILogger logger)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
                options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
            });
            builder.SetMinimumLevel(settings.Debug ? LogLevel.Information : LogLevel.Error);
        });

        services.AddSingleton(logger);

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");
        }

        services.AddOpenAiPredictor(apiKey, settings.Model);

        var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        var firebaseServiceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");

        if (!string.IsNullOrWhiteSpace(firebaseProjectId) && !string.IsNullOrWhiteSpace(firebaseServiceAccountJson))
        {
            services.AddFirebaseDatabase(firebaseProjectId, firebaseServiceAccountJson, settings.CommunityContext);
            logger.LogInformation("Firebase database integration enabled for project: {ProjectId}", firebaseProjectId);
        }
        else
        {
            logger.LogWarning("Firebase credentials not found; context documents will not be loaded from database");
        }

        var username = Environment.GetEnvironmentVariable("KICKTIPP_USERNAME");
        var password = Environment.GetEnvironmentVariable("KICKTIPP_PASSWORD");

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            services.Configure<KicktippOptions>(options =>
            {
                options.Username = username;
                options.Password = password;
            });

            services.AddKicktippClient();
        }
    }

    public static async Task<Match?> ResolveMatchAsync(
        AnalyzeMatchBaseSettings settings,
        IServiceProvider serviceProvider,
        ILogger logger,
        string communityContext)
    {
        var kicktippClient = serviceProvider.GetService<IKicktippClient>();

        if (kicktippClient != null)
        {
            try
            {
                var matches = await kicktippClient.GetMatchesWithHistoryAsync(communityContext);
                var found = matches.FirstOrDefault(m =>
                    m.Match.Matchday == settings.Matchday &&
                    string.Equals(m.Match.HomeTeam, settings.HomeTeam, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Match.AwayTeam, settings.AwayTeam, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                {
                    AnsiConsole.MarkupLine("[dim]Using match metadata from Kicktipp schedule[/]");
                    return found.Match;
                }

                logger.LogWarning(
                    "Match not found via Kicktipp lookup for community {CommunityContext}, matchday {Matchday}, teams {HomeTeam} vs {AwayTeam}. Continuing with provided details.",
                    communityContext,
                    settings.Matchday,
                    settings.HomeTeam,
                    settings.AwayTeam);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch match metadata from Kicktipp; continuing with provided details");
            }
        }
        else
        {
            logger.LogWarning("Kicktipp client not configured; continuing with provided match details");
        }

        return new Match(
            settings.HomeTeam,
            settings.AwayTeam,
            default,
            settings.Matchday!.Value);
    }

    public static async Task<List<AnalyzeMatchContextDocumentInfo>> GetMatchContextDocumentsAsync(
        IContextRepository contextRepository,
        string homeTeam,
        string awayTeam,
        string communityContext,
        bool verbose)
    {
        var contextDocuments = new List<AnalyzeMatchContextDocumentInfo>();
        var homeAbbreviation = GetTeamAbbreviation(homeTeam);
        var awayAbbreviation = GetTeamAbbreviation(awayTeam);

        var requiredDocuments = new[]
        {
            "bundesliga-standings.csv",
            $"community-rules-{communityContext}.md",
            $"recent-history-{homeAbbreviation}.csv",
            $"recent-history-{awayAbbreviation}.csv",
            $"home-history-{homeAbbreviation}.csv",
            $"away-history-{awayAbbreviation}.csv",
            $"head-to-head-{homeAbbreviation}-vs-{awayAbbreviation}.csv"
        };

        var optionalDocuments = new[]
        {
            $"{homeAbbreviation}-transfers.csv",
            $"{awayAbbreviation}-transfers.csv"
        };

        if (verbose)
        {
            AnsiConsole.MarkupLine($"[dim]Looking for {requiredDocuments.Length} required context documents in database[/]");
        }

        foreach (var documentName in requiredDocuments)
        {
            var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
            if (contextDoc != null)
            {
                contextDocuments.Add(new AnalyzeMatchContextDocumentInfo(new DocumentContext(contextDoc.DocumentName, contextDoc.Content), contextDoc.Version));

                if (verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]  ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                }
            }
            else if (verbose)
            {
                AnsiConsole.MarkupLine($"[dim]  ✗ Missing {documentName}[/]");
            }
        }

        foreach (var documentName in optionalDocuments)
        {
            try
            {
                var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                if (contextDoc != null)
                {
                    contextDocuments.Add(new AnalyzeMatchContextDocumentInfo(new DocumentContext(contextDoc.DocumentName, contextDoc.Content), contextDoc.Version));

                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]  ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                    }
                }
                else if (verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]  · Missing optional {documentName}[/]");
                }
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    AnsiConsole.MarkupLine($"[dim]  · Failed optional {documentName}: {Markup.Escape(ex.Message)}[/]");
                }
            }
        }

        return contextDocuments;
    }

    private static string GetTeamAbbreviation(string teamName)
    {
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "1. FC Heidenheim 1846", "fch" },
            { "1. FC Köln", "fck" },
            { "1. FC Union Berlin", "fcu" },
            { "1899 Hoffenheim", "tsg" },
            { "Bayer 04 Leverkusen", "b04" },
            { "Bor. Mönchengladbach", "bmg" },
            { "Borussia Dortmund", "bvb" },
            { "Eintracht Frankfurt", "sge" },
            { "FC Augsburg", "fca" },
            { "FC Bayern München", "fcb" },
            { "FC St. Pauli", "fcs" },
            { "FSV Mainz 05", "m05" },
            { "Hamburger SV", "hsv" },
            { "RB Leipzig", "rbl" },
            { "SC Freiburg", "scf" },
            { "VfB Stuttgart", "vfb" },
            { "VfL Wolfsburg", "wob" },
            { "Werder Bremen", "svw" }
        };

        if (abbreviations.TryGetValue(teamName, out var abbreviation))
        {
            return abbreviation;
        }

        var words = teamName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();

        foreach (var word in words.Take(3))
        {
            if (word.Length > 0 && char.IsLetter(word[0]))
            {
                builder.Append(char.ToLowerInvariant(word[0]));
            }
        }

        return builder.Length > 0 ? builder.ToString() : "unknown";
    }
}
