using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using OpenAiIntegration;
using ContextProviders.Kicktipp;
using EHonda.KicktippAi.Core;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;

namespace Orchestrator.Commands.Operations.RandomMatch;

public class RandomMatchCommand : AsyncCommand<RandomMatchSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly IContextProviderFactory _contextProviderFactory;
    private readonly ILogger<RandomMatchCommand> _logger;
    private readonly ILangfusePublicApiClient? _langfuseClient;

    public RandomMatchCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ILogger<RandomMatchCommand> logger,
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

    protected override async Task<int> ExecuteAsync(CommandContext context, RandomMatchSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var initialModel = string.IsNullOrWhiteSpace(settings.Model) ? "(competition default)" : settings.Model;
            _console.MarkupLine($"[green]Random match command initialized with model:[/] [yellow]{initialModel}[/]");

            if (settings.WithJustification)
            {
                _console.MarkupLine("[green]Justification output enabled - model reasoning will be captured[/]");
            }

            await ExecuteRandomMatchWorkflow(settings);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing random-match command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task ExecuteRandomMatchWorkflow(RandomMatchSettings settings)
    {
        // Start root OTel activity for Langfuse trace
        using var activity = Telemetry.Source.StartActivity("random-match");

        // RandomMatch is always a development trace
        LangfuseActivityPropagation.SetEnvironment(activity, "development");

        var communityContext = settings.CommunityContext ?? settings.Community;
        var competition = CompetitionResolver.ResolveCompetition(settings.Competition, settings.Community, communityContext);
        var model = PredictionServiceCommandSupport.ResolveModel(settings.Model, competition);
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
            settings.ReasoningEffort,
            bonusPrompt: false);

        // Create context provider using factory (community is used as context)
        var contextProvider = _contextProviderFactory.CreateKicktippContextProvider(
            kicktippClient, settings.Community, communityContext, repositoryCompetition);

        var tokenUsageTracker = _openAiServiceFactory.GetTokenUsageTracker();

        _console.MarkupLine($"[dim]Match prompt:[/] [blue]{predictionService.GetMatchPromptPath(settings.WithJustification)}[/]");

        // Create context repository for hybrid context lookup
        var contextRepository = _firebaseServiceFactory.CreateContextRepository(repositoryCompetition);

        // Reset token usage tracker for this workflow
        tokenUsageTracker.Reset();

        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
        _console.MarkupLine("[blue]Getting current matchday matches...[/]");

        // Step 1: Get current matchday matches
        var matchesWithHistory = await kicktippClient.GetMatchesWithHistoryAsync(settings.Community);

        if (!matchesWithHistory.Any())
        {
            _console.MarkupLine("[yellow]No matches found for current matchday[/]");
            return;
        }

        // Step 2: Pick a random match
        var randomIndex = Random.Shared.Next(matchesWithHistory.Count);
        var selectedMatchWithHistory = matchesWithHistory[randomIndex];
        var match = selectedMatchWithHistory.Match;

        _console.MarkupLine($"[green]Found {matchesWithHistory.Count} matches for current matchday[/]");
        _console.MarkupLine($"[cyan]Randomly selected match {randomIndex + 1}/{matchesWithHistory.Count}:[/] [yellow]{match.HomeTeam} vs {match.AwayTeam}[/]");

        // Set Langfuse trace-level attributes
        var matchday = match.Matchday;
        var sessionId = $"random-match-{matchday}-{settings.Community}";
        var traceTags = new[] { settings.Community, model, competition, "random-match" };
        LangfuseActivityPropagation.SetSessionId(activity, sessionId);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", settings.Community);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "communityContext", communityContext);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "competition", competition);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "matchday", matchday.ToString());
        LangfuseActivityPropagation.SetTraceMetadata(activity, "model", model);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "homeTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.HomeTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "awayTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.AwayTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "teams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.HomeTeam, match.AwayTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "selectedMatch", $"{match.HomeTeam} vs {match.AwayTeam}", propagateToObservations: false);

        // Set trace input
        var traceInput = new
        {
            community = settings.Community,
            matchday,
            model,
            competition,
            match = $"{match.HomeTeam} vs {match.AwayTeam}"
        };
        activity?.SetTag("langfuse.trace.input", JsonSerializer.Serialize(traceInput));

        _console.MarkupLine($"[dim]Matchday: {matchday}[/]");

        if (match.IsCancelled)
        {
            _console.MarkupLine($"[yellow]  ⚠ {match.HomeTeam} vs {match.AwayTeam} is cancelled (Abgesagt). " +
                $"Processing with inherited time - prediction may need re-evaluation when rescheduled.[/]");
        }

        // Step 3: Generate prediction
        _console.MarkupLine($"[yellow]  → Generating prediction...[/]");

        // Get context using hybrid approach (database first, fallback to on-demand)
        var contextDocuments = await GetHybridContextAsync(
            contextRepository,
            contextProvider,
            match.HomeTeam,
            match.AwayTeam,
            communityContext,
            competition);

        _console.MarkupLine($"[dim]    Using {contextDocuments.Count} context documents[/]");

        // Predict the match
        var telemetryMetadata = new PredictionTelemetryMetadata(
            HomeTeam: match.HomeTeam,
            AwayTeam: match.AwayTeam,
            RepredictionIndex: 0);

        var prediction = await predictionService.PredictMatchAsync(match, contextDocuments, settings.WithJustification, telemetryMetadata);

        if (prediction != null)
        {
            _console.MarkupLine($"[green]  ✓ Prediction:[/] {prediction.HomeGoals}:{prediction.AwayGoals}");
            WriteJustificationIfNeeded(prediction, settings.WithJustification);

            // Set trace output
            var traceOutput = new
            {
                match = $"{match.HomeTeam} vs {match.AwayTeam}",
                prediction = $"{prediction.HomeGoals}:{prediction.AwayGoals}"
            };
            activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(traceOutput));
        }
        else
        {
            _console.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
            activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(new { error = "Failed to generate prediction" }));
        }

        // Display token usage summary
        var summary = tokenUsageTracker.GetCompactSummary();
        _console.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");
    }

    /// <summary>
    /// Retrieves all available context documents from the database for the given community context.
    /// </summary>
    private async Task<Dictionary<string, DocumentContext>> GetMatchContextDocumentsAsync(
        IContextRepository contextRepository,
        string homeTeam,
        string awayTeam,
        string communityContext,
        string competition)
    {
        var contextDocuments = new Dictionary<string, DocumentContext>();
        var selection = MatchContextDocumentCatalog.ForMatch(homeTeam, awayTeam, communityContext, competition);

        _console.MarkupLine($"[dim]    Looking for {selection.RequiredDocumentNames.Count} specific context documents in database[/]");

        try
        {
            foreach (var documentName in selection.RequiredDocumentNames)
            {
                var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                if (contextDoc != null)
                {
                    contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);
                    _console.MarkupLine($"[dim]      ✓ Retrieved {documentName} (version {contextDoc.Version})[/]");
                }
                else
                {
                    _console.MarkupLine($"[dim]      ✗ Missing {documentName}[/]");
                }
            }

            foreach (var documentName in selection.OptionalDocumentNames)
            {
                try
                {
                    var contextDoc = await contextRepository.GetLatestContextDocumentAsync(documentName, communityContext);
                    if (contextDoc != null)
                    {
                        contextDocuments[documentName] = new DocumentContext(contextDoc.DocumentName, contextDoc.Content);
                        _console.MarkupLine($"[dim]      ✓ Retrieved optional {documentName} (version {contextDoc.Version})[/]");
                    }
                    else
                    {
                        _console.MarkupLine($"[dim]      · Missing optional {documentName}[/]");
                    }
                }
                catch (Exception optEx)
                {
                    _console.MarkupLine($"[dim]      · Failed optional {documentName}: {optEx.Message}[/]");
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
        string homeTeam,
        string awayTeam,
        string communityContext,
        string competition)
    {
        var contextDocuments = new List<DocumentContext>();
        var databaseContexts = await GetMatchContextDocumentsAsync(
            contextRepository,
            homeTeam,
            awayTeam,
            communityContext,
            competition);

        var requiredDocuments = MatchContextDocumentCatalog
            .ForMatch(homeTeam, awayTeam, communityContext, competition)
            .RequiredDocumentNames;

        int requiredPresent = requiredDocuments.Count(d => databaseContexts.ContainsKey(d));
        int requiredTotal = requiredDocuments.Count;

        if (requiredPresent == requiredTotal)
        {
            _console.MarkupLine($"[green]    Using {databaseContexts.Count} context documents from database (all required present)[/]");
            contextDocuments.AddRange(databaseContexts.Values);
        }
        else
        {
            _console.MarkupLine($"[yellow]    Warning: Only found {requiredPresent}/{requiredTotal} required context documents in database (have {databaseContexts.Count} total incl. optional). Falling back to on-demand context while preserving retrieved documents[/]");

            contextDocuments.AddRange(databaseContexts.Values);

            var existingNames = new HashSet<string>(contextDocuments.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
            await foreach (var context in contextProvider.GetMatchContextAsync(homeTeam, awayTeam))
            {
                if (existingNames.Add(context.Name))
                {
                    contextDocuments.Add(context);
                }
            }

            _console.MarkupLine($"[yellow]    Using {contextDocuments.Count} merged context documents (database + on-demand) [/]");
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
}
