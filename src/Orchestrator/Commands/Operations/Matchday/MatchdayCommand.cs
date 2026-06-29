using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using OpenAiIntegration;
using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;

namespace Orchestrator.Commands.Operations.Matchday;

public class MatchdayCommand : AsyncCommand<BaseSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly IContextProviderFactory _contextProviderFactory;
    private readonly ILogger<MatchdayCommand> _logger;
    private readonly ILangfusePublicApiClient? _langfuseClient;

    public MatchdayCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ILogger<MatchdayCommand> logger,
        ILangfusePublicApiClient? langfuseClient = null)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _contextProviderFactory = contextProviderFactory;
        _logger = logger;
        _langfuseClient = langfuseClient;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BaseSettings settings, CancellationToken cancellationToken)
    {
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(BaseSettings settings, CancellationToken cancellationToken = default)
    {

        try
        {
            var initialModel = string.IsNullOrWhiteSpace(settings.Model) ? "(competition default)" : settings.Model;
            _console.MarkupLine($"[green]Matchday command initialized with model:[/] [yellow]{initialModel}[/]");

            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }

            if (settings.OverrideKicktipp)
            {
                _console.MarkupLine("[yellow]Override mode enabled - will override existing Kicktipp predictions[/]");
            }

            if (settings.OverrideDatabase)
            {
                _console.MarkupLine("[yellow]Override database mode enabled - will override existing database predictions[/]");
            }

            if (settings.Agent)
            {
                _console.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }

            if (settings.DryRun)
            {
                _console.MarkupLine("[magenta]Dry run mode enabled - no changes will be made to database or Kicktipp[/]");
            }

            if (!string.IsNullOrEmpty(settings.EstimatedCostsModel))
            {
                _console.MarkupLine($"[cyan]Estimated costs will be calculated for model:[/] [yellow]{settings.EstimatedCostsModel}[/]");
            }

            if (settings.WithJustification)
            {
                if (settings.Agent)
                {
                    _console.MarkupLine("[red]Error:[/] --with-justification cannot be used with --agent");
                    return 1;
                }

                _console.MarkupLine("[green]Justification output enabled - model reasoning will be captured[/]");
            }

            // Validate reprediction settings
            if (settings.OverrideDatabase && settings.IsRepredictMode)
            {
                _console.MarkupLine($"[red]Error:[/] --override-database cannot be used with reprediction flags (--repredict or --max-repredictions)");
                return 1;
            }

            if (settings.MaxRepredictions.HasValue && settings.MaxRepredictions.Value < 0)
            {
                _console.MarkupLine($"[red]Error:[/] --max-repredictions must be 0 or greater");
                return 1;
            }

            if (settings.IsRepredictMode)
            {
                var maxValue = settings.MaxRepredictions ?? int.MaxValue;
                _console.MarkupLine($"[yellow]Reprediction mode enabled - max repredictions: {(settings.MaxRepredictions?.ToString() ?? "unlimited")}[/]");
            }

            // Execute the matchday workflow
            return await ExecuteMatchdayWorkflow(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing matchday command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Communities that have production workflows invoking the matchday command.
    /// Update this set when adding or removing community matchday workflows in .github/workflows/.
    /// See .github/workflows/AGENTS.md for details.
    /// </summary>
    private static readonly HashSet<string> ProductionCommunities = new(StringComparer.OrdinalIgnoreCase)
    {
        "schadensfresse",
        "pes-squad",
        "ehonda-ai-arena",
        "rabetrabauken2026"
    };


    private async Task<int> ExecuteMatchdayWorkflow(BaseSettings settings)
    {
        // Start root OTel activity for Langfuse trace
        using var activity = Telemetry.Source.StartActivity("matchday");

        // Set Langfuse environment based on community
        var environment = ProductionCommunities.Contains(settings.Community) ? "production" : "development";
        LangfuseActivityPropagation.SetEnvironment(activity, environment);

        string communityContext = settings.CommunityContext ?? settings.Community;
        var competition = CompetitionResolver.ResolveCompetition(settings.Competition, settings.Community, communityContext);
        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(settings.Model, settings.ReasoningEffort);
        var model = modelConfig.Model;
        var repositoryCompetition = CompetitionResolver.ToRepositoryCompetitionArgument(competition);

        if (settings.WithJustification && PredictionServiceCommandSupport.UsesLangfusePromptSource(
                competition,
                settings.Community,
                communityContext,
                settings.PromptSource,
                bonusPrompt: false))
        {
            throw new NotSupportedException(
                "WM 2026 hosted match prompts with justification are not supported yet. Use local prompts or omit --with-justification.");
        }

        // Create services using factories
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var predictionService = PredictionServiceCommandSupport.CreatePredictionService(
            _openAiServiceFactory,
            _langfuseClient,
            _console,
            model,
            competition,
            settings.Community,
            communityContext,
            settings.PromptSource,
            settings.LangfusePromptName,
            settings.LangfusePromptLabel,
            settings.LangfusePromptVersion,
            modelConfig.ReasoningEffort,
            settings.MaxOutputTokenCount,
            bonusPrompt: false);

        // Create context provider using factory
        var contextProvider = _contextProviderFactory.CreateKicktippContextProvider(
            kicktippClient, settings.Community, communityContext, repositoryCompetition);

        var tokenUsageTracker = _openAiServiceFactory.GetTokenUsageTracker();

        // Log the prompt paths being used
        if (settings.Verbose)
        {
            _console.MarkupLine($"[dim]Match prompt:[/] [blue]{predictionService.GetMatchPromptPath(settings.WithJustification)}[/]");
        }

        // Create repositories
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository(repositoryCompetition);
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);
        var databaseEnabled = true;

        // Reset token usage tracker for this workflow
        tokenUsageTracker.Reset();

        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
        _console.MarkupLine("[blue]Getting current matchday matches...[/]");

        // Step 1: Get current matchday via GetMatchesWithHistoryAsync
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.Community, competition);

        if (!matchesWithHistory.Any())
        {
            _console.MarkupLine("[yellow]No matches found for current matchday[/]");
            return 0;
        }

        // Set Langfuse trace-level attributes now that we know the matchday
        var matchday = matchesWithHistory.First().Match.Matchday;
        var sessionId = $"matchday-{matchday}-{settings.Community}";
        var traceTags = new[] { settings.Community, model, competition };
        LangfuseActivityPropagation.SetSessionId(activity, sessionId);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", settings.Community);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "communityContext", communityContext);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "competition", competition);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "matchday", matchday.ToString());
        LangfuseActivityPropagation.SetTraceMetadata(activity, "model", model);
        if (modelConfig.ReasoningEffort is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "reasoningEffort", modelConfig.ReasoningEffort);
        }
        LangfuseActivityPropagation.SetTraceMetadata(activity, "homeTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(matchesWithHistory.Select(m => m.Match.HomeTeam)), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "awayTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(matchesWithHistory.Select(m => m.Match.AwayTeam)), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "teams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(matchesWithHistory.SelectMany(m => new[] { m.Match.HomeTeam, m.Match.AwayTeam })), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "repredictMode", settings.IsRepredictMode ? "true" : "false");

        // Set trace input
        var traceInput = new
        {
            community = settings.Community,
            matchday,
            model,
            competition,
            matches = matchesWithHistory.Select(m => $"{m.Match.HomeTeam} vs {m.Match.AwayTeam}").ToArray()
        };
        activity?.SetTag("langfuse.trace.input", JsonSerializer.Serialize(traceInput));

        _console.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");

        if (databaseEnabled)
        {
            _console.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }

        var predictions = new Dictionary<Match, BetPrediction>();
        var blockedMatches = new List<BlockedMatch>();
        var traceRepredictionIndices = new HashSet<string>(StringComparer.Ordinal);

        // Step 2: For each match, check database first, then predict if needed
        foreach (var matchWithHistory in matchesWithHistory)
        {
            var match = matchWithHistory.Match;

            // Log warning for cancelled matches - they have inherited times which may affect database operations
            if (match.IsCancelled)
            {
                _console.MarkupLine($"[yellow]  ⚠ {match.HomeTeam} vs {match.AwayTeam} is cancelled (Abgesagt). " +
                    $"Processing with inherited time - prediction may need re-evaluation when rescheduled.[/]");
            }

            _console.MarkupLine($"[cyan]Processing:[/] {match.HomeTeam} vs {match.AwayTeam}{(match.IsCancelled ? " [yellow](CANCELLED)[/]" : "")}");

            try
            {
                Prediction? prediction = null;
                bool fromDatabase = false;
                bool shouldPredict = false;
                int? predictionRepredictionIndex = settings.IsRepredictMode ? null : 0;

                // Check if we have an existing prediction in the database
                if (databaseEnabled && !settings.OverrideDatabase && !settings.IsRepredictMode)
                {
                    prediction = await GetStoredPredictionAsync(predictionRepository!, match, modelConfig, communityContext);

                    if (prediction != null)
                    {
                        var storedValidation = MatchPredictionValidator.Validate(match, prediction);
                        if (!storedValidation.IsValid)
                        {
                            BlockMatch(
                                blockedMatches,
                                match,
                                "invalid_stored_prediction",
                                $"Stored prediction {FormatPrediction(prediction)} is invalid: {FormatValidationFailure(storedValidation)}");

                            if (settings.Agent)
                            {
                                _console.MarkupLine("[yellow]  ✗ Blocked - stored prediction is invalid for submission[/]");
                            }
                            else
                            {
                                _console.MarkupLine($"[yellow]  ✗ Blocked - stored prediction {FormatPrediction(prediction)} is invalid for submission ({FormatValidationFailure(storedValidation)})[/]");
                            }

                            continue;
                        }

                        fromDatabase = true;
                        if (settings.Agent)
                        {
                            _console.MarkupLine("[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[green]  ✓ Found existing prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](from database)[/]");
                            WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
                        }
                    }
                }

                // Handle reprediction logic
                if (settings.IsRepredictMode && databaseEnabled)
                {
                    var currentRepredictionIndex = await GetStoredRepredictionIndexAsync(
                        predictionRepository!,
                        match,
                        modelConfig,
                        communityContext);

                    if (currentRepredictionIndex == -1)
                    {
                        // No prediction exists yet - create first prediction
                        shouldPredict = true;
                        predictionRepredictionIndex = 0;
                        _console.MarkupLine("[yellow]  → No existing prediction found, creating first prediction...[/]");
                    }
                    else
                    {
                        var maxAllowed = settings.MaxRepredictions ?? int.MaxValue;
                        var nextIndex = currentRepredictionIndex + 1;
                        prediction = await GetStoredPredictionAsync(predictionRepository!, match, modelConfig, communityContext);

                        if (prediction == null)
                        {
                            if (nextIndex <= maxAllowed)
                            {
                                shouldPredict = true;
                                predictionRepredictionIndex = nextIndex;
                                _console.MarkupLine($"[yellow]  → Current prediction record missing, creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed})[/]");
                            }
                            else
                            {
                                traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                                BlockMatch(
                                    blockedMatches,
                                    match,
                                    "max_repredictions_reached",
                                    $"Prediction record missing while already at max repredictions ({currentRepredictionIndex}/{maxAllowed})");
                                _console.MarkupLine($"[yellow]  ✗ Blocked - prediction record missing and already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                                continue;
                            }
                        }
                        else
                        {
                            var storedValidation = MatchPredictionValidator.Validate(match, prediction);
                            if (!storedValidation.IsValid)
                            {
                                if (nextIndex <= maxAllowed)
                                {
                                    shouldPredict = true;
                                    predictionRepredictionIndex = nextIndex;

                                    if (settings.Agent)
                                    {
                                        _console.MarkupLine($"[yellow]  → Current prediction is invalid, creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed})[/]");
                                    }
                                    else
                                    {
                                        _console.MarkupLine($"[yellow]  → Current prediction {FormatPrediction(prediction)} is invalid ({FormatValidationFailure(storedValidation)}); creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed})[/]");
                                    }

                                    prediction = null;
                                }
                                else
                                {
                                    traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                                    BlockMatch(
                                        blockedMatches,
                                        match,
                                        "max_repredictions_reached",
                                        $"Stored prediction {FormatPrediction(prediction)} is invalid and already at max repredictions ({currentRepredictionIndex}/{maxAllowed})");

                                    if (settings.Agent)
                                    {
                                        _console.MarkupLine($"[yellow]  ✗ Blocked - stored prediction is invalid and already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                                    }
                                    else
                                    {
                                        _console.MarkupLine($"[yellow]  ✗ Blocked - stored prediction {FormatPrediction(prediction)} is invalid and already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                                    }

                                    continue;
                                }
                            }
                            else if (nextIndex <= maxAllowed)
                            {
                                // Before repredicting, check if the current prediction is actually outdated
                                var isOutdated = await CheckPredictionOutdated(predictionRepository!, contextRepository, match, modelConfig, communityContext, competition, settings.Verbose);

                                if (isOutdated)
                                {
                                    shouldPredict = true;
                                    predictionRepredictionIndex = nextIndex;
                                    prediction = null;
                                    _console.MarkupLine($"[yellow]  → Creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed}) - prediction is outdated[/]");
                                }
                                else
                                {
                                    traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                                    fromDatabase = true;
                                    _console.MarkupLine("[green]  ✓ Skipped reprediction - current prediction is up-to-date[/]");

                                    if (!settings.Agent)
                                    {
                                        _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                        WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
                                    }
                                }
                            }
                            else
                            {
                                traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                                fromDatabase = true;
                                _console.MarkupLine($"[yellow]  ✗ Skipped - already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");

                                if (!settings.Agent)
                                {
                                    _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals} [dim](reprediction {currentRepredictionIndex})[/]");
                                    WriteJustificationIfNeeded(prediction, settings.WithJustification, fromDatabase: true);
                                }
                            }
                        }
                    }
                }

                // If no existing prediction (normal mode) or we need to predict (reprediction mode), generate a new one
                if (prediction == null || shouldPredict)
                {
                    _console.MarkupLine("[yellow]  → Generating new prediction...[/]");

                    // Step 3: Get context using hybrid approach (database first, fallback to on-demand)
                    var contextDocuments = await GetHybridContextAsync(
                        contextRepository,
                        contextProvider,
                        match,
                        communityContext,
                        competition,
                        settings.Verbose);

                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]    Using {contextDocuments.Count} context documents[/]");
                    }

                    // Show context documents if requested
                    if (settings.ShowContextDocuments)
                    {
                        _console.MarkupLine($"[cyan]    Context documents for {match.HomeTeam} vs {match.AwayTeam}:[/]");
                        foreach (var doc in contextDocuments)
                        {
                            _console.MarkupLine($"[dim]    📄 {doc.Name}[/]");

                            // Show first few lines and total line count for readability
                            var lines = doc.Content.Split('\n');
                            var previewLines = lines.Take(10).ToArray();
                            var hasMore = lines.Length > 10;

                            foreach (var line in previewLines)
                            {
                                _console.MarkupLine($"[grey]      {line.EscapeMarkup()}[/]");
                            }

                            if (hasMore)
                            {
                                _console.MarkupLine($"[dim]      ... ({lines.Length - 10} more lines) ...[/]");
                            }

                            _console.MarkupLine($"[dim]      (Total: {lines.Length} lines, {doc.Content.Length} characters)[/]");
                            _console.WriteLine();
                        }
                    }

                    var telemetryMetadata = new PredictionTelemetryMetadata(
                        HomeTeam: match.HomeTeam,
                        AwayTeam: match.AwayTeam,
                        RepredictionIndex: predictionRepredictionIndex);

                    // Predict the match
                    prediction = await predictionService.PredictMatchAsync(match, contextDocuments, settings.WithJustification, telemetryMetadata);

                    if (prediction != null)
                    {
                        if (predictionRepredictionIndex.HasValue)
                        {
                            traceRepredictionIndices.Add(predictionRepredictionIndex.Value.ToString());
                        }

                        if (settings.Agent)
                        {
                            _console.MarkupLine("[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[green]  ✓ Generated prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals}");
                            WriteJustificationIfNeeded(prediction, settings.WithJustification);
                        }

                        var generatedValidation = MatchPredictionValidator.Validate(match, prediction);
                        if (!generatedValidation.IsValid)
                        {
                            if (databaseEnabled && settings.IsRepredictMode)
                            {
                                if (settings.DryRun)
                                {
                                    if (settings.Verbose)
                                    {
                                        _console.MarkupLine("[dim]    (Dry run - skipped reprediction save for invalid prediction)[/]");
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        await SaveMatchPredictionAsync(
                                            predictionRepository!,
                                            match,
                                            prediction,
                                            modelConfig,
                                            communityContext,
                                            contextDocuments,
                                            settings,
                                            tokenUsageTracker);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to save invalid reprediction attempt for match {Match}", match);
                                        _console.MarkupLine($"[red]    ✗ Failed to save invalid reprediction attempt to database: {ex.Message}[/]");
                                    }
                                }
                            }

                            BlockMatch(
                                blockedMatches,
                                match,
                                "invalid_generated_prediction",
                                $"Generated prediction {FormatPrediction(prediction)} is invalid: {FormatValidationFailure(generatedValidation)}");

                            if (settings.Agent)
                            {
                                _console.MarkupLine("[yellow]  ✗ Blocked - generated prediction is invalid for submission[/]");
                            }
                            else
                            {
                                _console.MarkupLine($"[yellow]  ✗ Blocked - generated prediction {FormatPrediction(prediction)} is invalid for submission ({FormatValidationFailure(generatedValidation)})[/]");
                            }

                            if (settings.Verbose)
                            {
                                var matchUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                    ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                    : tokenUsageTracker.GetLastUsageCompactSummary();
                                _console.MarkupLine($"[dim]    Token usage: {matchUsage}[/]");
                            }

                            continue;
                        }

                        // Save to database immediately if enabled
                        if (databaseEnabled && !settings.DryRun)
                        {
                            try
                            {
                                await SaveMatchPredictionAsync(
                                    predictionRepository!,
                                    match,
                                    prediction,
                                    modelConfig,
                                    communityContext,
                                    contextDocuments,
                                    settings,
                                    tokenUsageTracker);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to save prediction for match {Match}", match);
                                _console.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            _console.MarkupLine("[dim]    (Dry run - skipped database save)[/]");
                        }

                        // Show individual match token usage in verbose mode
                        if (settings.Verbose)
                        {
                            var matchUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            _console.MarkupLine($"[dim]    Token usage: {matchUsage}[/]");
                        }
                    }
                    else
                    {
                        _console.MarkupLine("[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }

                // Convert to BetPrediction for Kicktipp
                var betPrediction = new BetPrediction(prediction.HomeGoals, prediction.AwayGoals);
                predictions[match] = betPrediction;

                if (!fromDatabase && settings.Verbose && !settings.DryRun)
                {
                    _console.MarkupLine("[dim]    Already saved to database[/]");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing match {Match}", match);
                _console.MarkupLine($"[red]  ✗ Error processing match: {ex.Message}[/]");
            }
        }

        if (traceRepredictionIndices.Count > 0)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "repredictionIndices", PredictionTelemetryMetadata.BuildDelimitedFilterValue(traceRepredictionIndices), propagateToObservations: false);
            LangfuseActivityPropagation.SetTraceMetadata(activity, "hasRepredictions", traceRepredictionIndices.Any(index => index != "0") ? "true" : "false", propagateToObservations: false);
        }

        if (!predictions.Any())
        {
            if (blockedMatches.Count > 0)
            {
                WriteBlockedMatchSummary(blockedMatches);
                _console.MarkupLine("[red]No valid predictions available, nothing to place[/]");
                activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(new
                {
                    error = "No valid predictions available",
                    blockedMatches = blockedMatches.Select(blockedMatch => new
                    {
                        match = $"{blockedMatch.Match.HomeTeam} vs {blockedMatch.Match.AwayTeam}",
                        reason = blockedMatch.ReasonCode
                    }).ToArray()
                }));
                return 1;
            }

            _console.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(new { error = "No predictions available" }));
            return 0;
        }

        // Set trace output with all predictions
        var traceOutput = predictions.Select(p => new
        {
            match = $"{p.Key.HomeTeam} vs {p.Key.AwayTeam}",
            prediction = $"{p.Value.HomeGoals}:{p.Value.AwayGoals}"
        }).ToArray();
        activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(traceOutput));

        // Step 4: Place all predictions using PlaceBetsAsync
        _console.MarkupLine($"[blue]Placing {predictions.Count} predictions to Kicktipp...[/]");

        var submitSuccess = true;
        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} predictions (no actual changes made)[/]");
        }
        else
        {
            submitSuccess = await kicktippClient.PlaceBetsAsync(settings.Community, predictions, overrideBets: settings.OverrideKicktipp);

            if (submitSuccess)
            {
                _console.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} predictions![/]");
            }
            else
            {
                _console.MarkupLine("[red]✗ Failed to place some or all predictions[/]");
            }
        }

        if (blockedMatches.Count > 0)
        {
            WriteBlockedMatchSummary(blockedMatches);
        }

        if (blockedMatches.Count > 0 && submitSuccess)
        {
            _console.MarkupLine("[yellow]Matchday completed with blocked matches - only the valid subset was submitted[/]");
        }

        // Display token usage summary
        var summary = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
            ? tokenUsageTracker.GetCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
            : tokenUsageTracker.GetCompactSummary();
        _console.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");

        return blockedMatches.Count > 0 || !submitSuccess ? 1 : 0;
    }
    /// <summary>
    /// Retrieves all available context documents from the database for the given community context.
    /// </summary>
    private async Task<Dictionary<string, DocumentContext>> GetMatchContextDocumentsAsync(
        IContextRepository contextRepository,
        Match match,
        string communityContext,
        string competition,
        bool verbose = false)
    {
        var contextDocuments = new Dictionary<string, DocumentContext>();
        var selection = MatchContextDocumentCatalog.ForMatch(match, communityContext, competition);

        if (verbose)
        {
            _console.MarkupLine($"[dim]    Looking for {selection.RequiredDocumentNames.Count} specific context documents in database[/]");
        }

        try
        {
            // Retrieve each required document
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                if (contextDoc != null)
                {
                    contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);

                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]      ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                    }
                }
                else
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]      ✗ Missing {documentName}[/]");
                    }
                }
            }

            // Retrieve optional transfers documents (best-effort)
            foreach (var documentName in selection.OptionalDocumentNames)
            {
                try
                {
                    var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                    if (contextDoc != null)
                    {
                        // Display name suffix to distinguish optional docs in prediction metadata (helps debug)
                        contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);
                        if (verbose)
                        {
                            _console.MarkupLine($"[dim]      ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                        }
                    }
                    else if (verbose)
                    {
                        _console.MarkupLine($"[dim]      · Missing optional {documentName}[/]");
                    }
                }
                catch (Exception optEx)
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]      · Failed optional {documentName}: {optEx.Message}[/]");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]    Warning: Failed to retrieve context from database: {ex.Message}[/]");
        }

        return contextDocuments;
    }

    /// <summary>
    /// Gets context documents using database first, falling back to on-demand context provider if needed.
    /// </summary>
    private async Task<List<DocumentContext>> GetHybridContextAsync(
        IContextRepository contextRepository,
        IKicktippContextProvider contextProvider,
        Match match,
        string communityContext,
        string competition,
        bool verbose = false)
    {
        var contextDocuments = new List<DocumentContext>();
        // Step 1: Retrieve any database documents (required + optional)
        var databaseContexts = await GetMatchContextDocumentsAsync(
            contextRepository,
            match,
            communityContext,
            competition,
            verbose);

        var requiredDocuments = MatchContextDocumentCatalog
            .ForMatch(match, communityContext, competition)
            .RequiredDocumentNames;

        int requiredPresent = requiredDocuments.Count(d => databaseContexts.ContainsKey(d));
        int requiredTotal = requiredDocuments.Count;

        if (requiredPresent == requiredTotal)
        {
            // All required docs present; include every database doc (required + optional)
            if (verbose)
            {
                _console.MarkupLine($"[green]    Using {databaseContexts.Count} context documents from database (all required present)[/]");
            }
            contextDocuments.AddRange(databaseContexts.Values);
        }
        else
        {
            // Fallback: use on-demand provider but still include any database docs we already have (including optional transfers)
            _console.MarkupLine($"[yellow]    Warning: Only found {requiredPresent}/{requiredTotal} required context documents in database (have {databaseContexts.Count} total incl. optional). Falling back to on-demand context while preserving retrieved documents[/]");

            // Start with database docs
            contextDocuments.AddRange(databaseContexts.Values);

            // Add on-demand docs, skipping duplicates by name
            var existingNames = new HashSet<string>(contextDocuments.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            var matchContext = match.CompetitionSpecificData is FifaWorldCup2026MatchData
                ? contextProvider.GetMatchContextAsync(match)
                : contextProvider.GetMatchContextAsync(match.HomeTeam, match.AwayTeam);
            await foreach (var context in matchContext)
            {
                if (existingNames.Add(context.Name))
                {
                    contextDocuments.Add(context);
                }
            }

            if (verbose)
            {
                _console.MarkupLine($"[yellow]    Using {contextDocuments.Count} merged context documents (database + on-demand) [/]");
            }
        }

        if (CompetitionResolver.IsWorldCupCompetition(competition))
        {
            EnsureWorldCupRequiredContextPresent(contextDocuments, requiredDocuments);
        }

        return contextDocuments;
    }

    private static void EnsureWorldCupRequiredContextPresent(
        IReadOnlyList<DocumentContext> contextDocuments,
        IReadOnlyList<string> requiredDocuments)
    {
        var presentDocumentNames = new HashSet<string>(
            contextDocuments.Select(document => document.Name),
            StringComparer.OrdinalIgnoreCase);
        var missingDocumentNames = requiredDocuments
            .Where(documentName => !presentDocumentNames.Contains(documentName))
            .ToList();

        if (missingDocumentNames.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Missing required WM26 context documents after database and on-demand fallback: " +
            $"{string.Join(", ", missingDocumentNames)}. Seed FIFA rankings with collect-context fifa and lineups with collect-context lineups.");
    }

    private async Task<bool> CheckPredictionOutdated(IPredictionRepository predictionRepository, IContextRepository contextRepository, Match match, PredictionModelConfig modelConfig, string communityContext, string competition, bool verbose)
    {
        try
        {
            // Get prediction metadata with context document names and timestamps
            // For cancelled matches, use team-names-only lookup to handle startsAt inconsistencies
            PredictionMetadata? predictionMetadata;
            if (match.IsCancelled)
            {
                predictionMetadata = await predictionRepository.GetCancelledMatchPredictionMetadataAsync(
                    match.HomeTeam, match.AwayTeam, modelConfig, communityContext);
            }
            else
            {
                predictionMetadata = await predictionRepository.GetPredictionMetadataAsync(match, modelConfig, communityContext);
            }

            if (predictionMetadata == null || !predictionMetadata.ContextDocumentNames.Any())
            {
                // If no context documents were used, prediction can't be outdated based on context changes
                return false;
            }

            if (verbose)
            {
                _console.MarkupLine($"[dim]  Checking {predictionMetadata.ContextDocumentNames.Count} context documents for updates[/]");
            }

            // Check if any context document has been updated after the prediction was created
            foreach (var documentName in predictionMetadata.ContextDocumentNames)
            {
                // Strip any display suffix (e.g., " (kpi-context)") from the context document name
                // to get the actual document name stored in the repository
                var actualDocumentName = StripDisplaySuffix(documentName);

                var standingsDocumentName = MatchContextDocumentCatalog.GetStandingsDocumentName(competition);
                if (actualDocumentName.Equals(standingsDocumentName, StringComparison.OrdinalIgnoreCase))
                {
                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]  Skipping outdated check for '{actualDocumentName}' (excluded from cost optimization)[/]");
                    }
                    continue;
                }

                var latestContextDocument = await contextRepository.GetLatestContextDocumentAsync(actualDocumentName, communityContext);

                if (latestContextDocument != null && latestContextDocument.CreatedAt > predictionMetadata.CreatedAt)
                {
                    var predictionTimeContextDocument = await contextRepository.GetContextDocumentByTimestampAsync(
                        actualDocumentName,
                        predictionMetadata.CreatedAt,
                        communityContext);

                    if (predictionTimeContextDocument != null &&
                        string.Equals(
                            predictionTimeContextDocument.Content,
                            latestContextDocument.Content,
                            StringComparison.Ordinal))
                    {
                        if (verbose)
                        {
                            _console.MarkupLine(
                                $"[dim]  Context document '{actualDocumentName}' has newer versions after the prediction, but the latest content matches the prediction-time version[/]");
                        }

                        continue;
                    }

                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]  Context document '{actualDocumentName}' (stored as '{documentName}') updated after prediction (document: {latestContextDocument.CreatedAt}, prediction: {predictionMetadata.CreatedAt})[/]");
                    }
                    return true; // Prediction is outdated
                }
                else if (verbose && latestContextDocument == null)
                {
                    _console.MarkupLine($"[yellow]  Warning: Context document '{actualDocumentName}' not found in repository[/]");
                }
            }

            return false; // Prediction is up-to-date
        }
        catch (Exception ex)
        {
            // Log error but don't fail verification due to outdated check issues
            if (verbose)
            {
                _console.MarkupLine($"[yellow]  Warning: Failed to check outdated status: {ex.Message}[/]");
            }
            return false;
        }
    }

    private async Task<Prediction?> GetStoredPredictionAsync(
        IPredictionRepository predictionRepository,
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext)
    {
        if (match.IsCancelled)
        {
            return await predictionRepository.GetCancelledMatchPredictionAsync(
                match.HomeTeam,
                match.AwayTeam,
                modelConfig,
                communityContext);
        }

        return await predictionRepository.GetPredictionAsync(match, modelConfig, communityContext);
    }

    private async Task<int> GetStoredRepredictionIndexAsync(
        IPredictionRepository predictionRepository,
        Match match,
        PredictionModelConfig modelConfig,
        string communityContext)
    {
        if (match.IsCancelled)
        {
            return await predictionRepository.GetCancelledMatchRepredictionIndexAsync(
                match.HomeTeam,
                match.AwayTeam,
                modelConfig,
                communityContext);
        }

        return await predictionRepository.GetMatchRepredictionIndexAsync(match, modelConfig, communityContext);
    }

    private async Task SaveMatchPredictionAsync(
        IPredictionRepository predictionRepository,
        Match match,
        Prediction prediction,
        PredictionModelConfig modelConfig,
        string communityContext,
        IReadOnlyCollection<DocumentContext> contextDocuments,
        BaseSettings settings,
        ITokenUsageTracker tokenUsageTracker)
    {
        var cost = (double)tokenUsageTracker.GetLastCost();
        var tokenUsageJson = tokenUsageTracker.GetLastUsageJson() ?? "{}";

        if (settings.IsRepredictMode)
        {
            var currentIndex = await GetStoredRepredictionIndexAsync(
                predictionRepository,
                match,
                modelConfig,
                communityContext);
            var nextIndex = currentIndex == -1 ? 0 : currentIndex + 1;

            await predictionRepository.SaveRepredictionAsync(
                match,
                prediction,
                modelConfig,
                tokenUsageJson,
                cost,
                communityContext,
                contextDocuments.Select(document => document.Name),
                nextIndex);

            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]    ✓ Saved as reprediction {nextIndex} to database[/]");
            }

            return;
        }

        await predictionRepository.SavePredictionAsync(
            match,
            prediction,
            modelConfig,
            tokenUsageJson,
            cost,
            communityContext,
            contextDocuments.Select(document => document.Name),
            overrideCreatedAt: settings.OverrideDatabase);

        if (settings.Verbose)
        {
            _console.MarkupLine("[dim]    ✓ Saved to database[/]");
        }
    }

    private void BlockMatch(
        List<BlockedMatch> blockedMatches,
        Match match,
        string reasonCode,
        string detail)
    {
        blockedMatches.Add(new BlockedMatch(match, reasonCode, detail));
        _logger.LogWarning("Blocked match {Match} ({ReasonCode}): {Detail}", match, reasonCode, detail);
    }

    private void WriteBlockedMatchSummary(IReadOnlyList<BlockedMatch> blockedMatches)
    {
        _console.MarkupLine($"[yellow]Blocked matches: {blockedMatches.Count}[/]");
        foreach (var blockedMatch in blockedMatches)
        {
            _console.MarkupLine($"[yellow]  - {blockedMatch.Match.HomeTeam} vs {blockedMatch.Match.AwayTeam}: {blockedMatch.ReasonCode}[/]");
        }
    }

    private static string FormatPrediction(Prediction prediction)
    {
        return $"{prediction.HomeGoals}:{prediction.AwayGoals}";
    }

    private static string FormatValidationFailure(MatchPredictionValidationResult validation)
    {
        return $"{MatchPredictionValidator.DescribeFailure(validation.ReasonCode)} ({validation.ReasonCode})";
    }

    private sealed record BlockedMatch(Match Match, string ReasonCode, string Detail);

    private void WriteJustificationIfNeeded(Prediction? prediction, bool includeJustification, bool fromDatabase = false)
    {
        if (!includeJustification || prediction == null)
        {
            return;
        }

        var sourceLabel = fromDatabase ? "stored prediction" : "model response";

        var justificationWriter = new JustificationConsoleWriter(_console);
        justificationWriter.WriteJustification(
            prediction.Justification,
            "[dim]    ↳ Justification:[/]",
            "        ",
            $"[yellow]    ↳ No justification available for this {sourceLabel}[/]");
    }

    /// <summary>
    /// Strips display suffixes like " (kpi-context)" from context document names
    /// to get the actual document name used in the repository.
    /// </summary>
    /// <param name="displayName">The display name that may contain a suffix</param>
    /// <returns>The actual document name without any display suffix</returns>
    private static string StripDisplaySuffix(string displayName)
    {
        // Look for patterns like " (some-text)" at the end and remove them
        var lastParenIndex = displayName.LastIndexOf(" (");
        if (lastParenIndex > 0 && displayName.EndsWith(")"))
        {
            return displayName.Substring(0, lastParenIndex);
        }
        return displayName;
    }
}
