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
using Spectre.Console.Rendering;
using FirebaseAdapter;
using OpenAiIntegration;
using Orchestrator.Commands.Shared;

namespace Orchestrator.Commands.Observability.AnalyzeMatch;

public class AnalyzeMatchDetailedCommand : AsyncCommand<AnalyzeMatchDetailedSettings>
{
    private readonly IAnsiConsole _console;

    public AnalyzeMatchDetailedCommand(IAnsiConsole console)
    {
        _console = console;
    }

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
                _console.MarkupLine($"[red]Error:[/] {validation.Message}");
                return 1;
            }

            var services = new ServiceCollection();
            AnalyzeMatchCommandHelpers.ConfigureServices(services, settings, logger);
            using var serviceProvider = services.BuildServiceProvider();

            var predictionService = serviceProvider.GetRequiredService<IPredictionService>();
            var tokenUsageTracker = serviceProvider.GetRequiredService<ITokenUsageTracker>();
            var contextRepository = serviceProvider.GetService<IContextRepository>();

            var communityContext = settings.CommunityContext!;

            _console.MarkupLine($"[green]Analyze match initialized with model:[/] [yellow]{settings.Model}[/]");
            _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
            _console.MarkupLine($"[blue]Runs:[/] [yellow]{settings.Runs}[/]");

            if (settings.Debug)
            {
                _console.MarkupLine("[dim]Debug logging enabled[/]");
            }

            var match = await AnalyzeMatchCommandHelpers.ResolveMatchAsync(settings, serviceProvider, logger, communityContext);
            if (match == null)
            {
                _console.MarkupLine("[red]Failed to resolve match details. Aborting.[/]");
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
                    _console.MarkupLine("[dim]Loaded context documents:[/]");
                    foreach (var info in contextDocumentInfos)
                    {
                        _console.MarkupLine($"[grey]  • {info.Document.Name}[/] [dim](v{info.Version})[/]");

                        if (settings.ShowContextDocuments)
                        {
                            var lines = info.Document.Content.Split('\n');
                            foreach (var line in lines.Take(10))
                            {
                                _console.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }

                            if (lines.Length > 10)
                            {
                                _console.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }
                        }
                    }
                }
                else
                {
                    _console.MarkupLine("[yellow]No context documents retrieved; proceeding without additional context[/]");
                }
            }
            else
            {
                _console.MarkupLine("[yellow]Context repository not configured. Proceeding without context documents.[/]");
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

                    _console.MarkupLine($"[cyan]\nRun {run}/{settings.Runs}[/]");

                    var prediction = await predictionService.PredictMatchAsync(
                        match,
                        contextDocuments,
                        includeJustification: true);

                    stopwatch.Stop();

                    if (prediction == null)
                    {
                        _console.MarkupLine("[red]  ✗ Prediction failed[/]");
                        runMetrics.Add(new RunMetric(run, stopwatch.Elapsed, false, null));
                        refreshSummary();
                        continue;
                    }

                    predictions.Add(prediction);

                    var lastCost = tokenUsageTracker.GetLastCost();
                    var usageSummary = tokenUsageTracker.GetLastUsageCompactSummary();

                    runMetrics.Add(new RunMetric(run, stopwatch.Elapsed, true, lastCost));

                    _console.MarkupLine($"[green]  ✓ Prediction:[/] [yellow]{prediction.HomeGoals}:{prediction.AwayGoals}[/]");

                    var justificationWriter = new JustificationConsoleWriter(_console);
                    justificationWriter.WriteJustification(
                        prediction.Justification,
                        "[cyan]  ↳ Justification:[/]",
                        "      ",
                        "[yellow]  ↳ Justification: no explanation returned by model[/]");

                    _console.MarkupLine($"[magenta]  ↳ Cost:[/] [cyan]{FormatCurrencyValue(lastCost)}[/] [grey]({usageSummary})[/]");

                    refreshSummary();
                }

                refreshSummary();
            }

            if (enableLiveEstimates)
            {
                await _console.Live(BuildSummary())
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
                _console.Write(BuildSummary());
            }

            if (predictions.Any())
            {
                _console.MarkupLine($"\n[blue]Total runs with predictions:[/] [yellow]{predictions.Count}/{settings.Runs}[/]");
                _console.MarkupLine($"[blue]Total cost:[/] [yellow]{FormatCurrencyValue(tokenUsageTracker.GetTotalCost())}[/]");
            }
            else
            {
                _console.MarkupLine("[red]No successful predictions generated.[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing analyze-match command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
        finally
        {
            loggerFactory.Dispose();
        }
    }

    private sealed record RunMetric(int RunNumber, TimeSpan Duration, bool Success, decimal? Cost);
}
