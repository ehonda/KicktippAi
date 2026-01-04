using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;
using FirebaseAdapter;
using OpenAiIntegration;

namespace Orchestrator.Commands.Observability.AnalyzeMatch;

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
                var usageSummary = tokenUsageTracker.GetLastUsageCompactSummary();

                AnsiConsole.MarkupLine($"[green]  ✓ Prediction:[/] [yellow]{prediction.HomeGoals}:{prediction.AwayGoals}[/]");
                AnsiConsole.MarkupLine($"[magenta]  ↳ Cost:[/] [cyan]{FormatCurrencyValue(lastCost)}[/] [grey]({usageSummary})[/]");

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
