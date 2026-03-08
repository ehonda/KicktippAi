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

namespace Orchestrator.Commands.Operations.RandomMatch;

public class RandomMatchCommand : AsyncCommand<RandomMatchSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly IContextProviderFactory _contextProviderFactory;
    private readonly ILogger<RandomMatchCommand> _logger;

    public RandomMatchCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ILogger<RandomMatchCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _contextProviderFactory = contextProviderFactory;
        _logger = logger;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RandomMatchSettings settings)
    {
        try
        {
            _console.MarkupLine($"[green]Random match command initialized with model:[/] [yellow]{settings.Model}[/]");

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

        // Create services using factories
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var predictionService = _openAiServiceFactory.CreatePredictionService(settings.Model);

        // Create context provider using factory (community is used as context)
        var contextProvider = _contextProviderFactory.CreateKicktippContextProvider(
            kicktippClient, settings.Community, settings.Community);

        var tokenUsageTracker = _openAiServiceFactory.GetTokenUsageTracker();

        _console.MarkupLine($"[dim]Match prompt:[/] [blue]{predictionService.GetMatchPromptPath(settings.WithJustification)}[/]");

        // Create context repository for hybrid context lookup
        var contextRepository = _firebaseServiceFactory.CreateContextRepository();

        // Reset token usage tracker for this workflow
        tokenUsageTracker.Reset();

        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
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
        var traceTags = new[] { settings.Community, settings.Model, "random-match" };
        LangfuseActivityPropagation.SetSessionId(activity, sessionId);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", settings.Community);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "kicktipp-season", KicktippSeasonMetadata.Current);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "matchday", matchday.ToString());
        LangfuseActivityPropagation.SetTraceMetadata(activity, "model", settings.Model);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "homeTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.HomeTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "awayTeams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.AwayTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "teams", PredictionTelemetryMetadata.BuildDelimitedFilterValue(new[] { match.HomeTeam, match.AwayTeam }), propagateToObservations: false);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "selectedMatch", $"{match.HomeTeam} vs {match.AwayTeam}", propagateToObservations: false);

        // Set trace input
        var traceInput = new
        {
            community = settings.Community,
            matchday,
            model = settings.Model,
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
            settings.Community);

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
        string communityContext)
    {
        var contextDocuments = new Dictionary<string, DocumentContext>();
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

        _console.MarkupLine($"[dim]    Looking for {requiredDocuments.Length} specific context documents in database[/]");

        try
        {
            foreach (var documentName in requiredDocuments)
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

            foreach (var documentName in optionalDocuments)
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
        string communityContext)
    {
        var contextDocuments = new List<DocumentContext>();
        var databaseContexts = await GetMatchContextDocumentsAsync(
            contextRepository,
            homeTeam,
            awayTeam,
            communityContext);

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

        int requiredPresent = requiredDocuments.Count(d => databaseContexts.ContainsKey(d));
        int requiredTotal = requiredDocuments.Length;

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

        return contextDocuments;
    }

    /// <summary>
    /// Gets a team abbreviation for file naming.
    /// </summary>
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
        var abbr = new System.Text.StringBuilder();

        foreach (var word in words.Take(3))
        {
            if (word.Length > 0 && char.IsLetter(word[0]))
            {
                abbr.Append(char.ToLowerInvariant(word[0]));
            }
        }

        return abbr.Length > 0 ? abbr.ToString() : "unknown";
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
