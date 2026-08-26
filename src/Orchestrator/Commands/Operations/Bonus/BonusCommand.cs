using System.Text.Json;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using OpenAiIntegration;
using Orchestrator.Commands.Operations.Matchday;
using Orchestrator.Commands.Shared;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;
using Orchestrator.Infrastructure.Langfuse;

namespace Orchestrator.Commands.Operations.Bonus;

public class BonusCommand : AsyncCommand<BonusSettings>
{
    private const string FifaRankingsDocumentName = "fifa-rankings";

    private sealed class BundesligaBonusSafetyException : InvalidOperationException
    {
        public BundesligaBonusSafetyException(string message)
            : base(message)
        {
        }

        public BundesligaBonusSafetyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly IOpenAiServiceFactory _openAiServiceFactory;
    private readonly IContextProviderFactory _contextProviderFactory;
    private readonly ICommunityKicktippCredentialLoader _credentialLoader;
    private readonly ILogger<BonusCommand> _logger;
    private readonly ILangfusePublicApiClient? _langfuseClient;

    public BonusCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        IOpenAiServiceFactory openAiServiceFactory,
        IContextProviderFactory contextProviderFactory,
        ICommunityKicktippCredentialLoader credentialLoader,
        ILogger<BonusCommand> logger,
        ILangfusePublicApiClient? langfuseClient = null)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _openAiServiceFactory = openAiServiceFactory;
        _contextProviderFactory = contextProviderFactory;
        _credentialLoader = credentialLoader;
        _logger = logger;
        _langfuseClient = langfuseClient;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, BonusSettings settings, CancellationToken cancellationToken)
    {
        return await ExecuteWithSettingsAsync(settings, cancellationToken);
    }

    internal async Task<int> ExecuteWithSettingsAsync(BaseSettings settings, CancellationToken cancellationToken = default)
    {
        
        try
        {
            var bonusContextBudget = ResolveBonusContextBudget(settings);
            var initialModel = string.IsNullOrWhiteSpace(settings.Model) ? "(competition default)" : settings.Model;
            _console.MarkupLine($"[green]Bonus command initialized with model:[/] [yellow]{initialModel}[/]");
            
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
            
            // Execute the bonus prediction workflow
            await ExecuteBonusWorkflow(settings, bonusContextBudget);
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing bonus command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    /// <summary>
    /// Communities that have production workflows invoking the bonus command.
    /// Update this set when adding or removing community bonus workflows in .github/workflows/.
    /// See .github/workflows/AGENTS.md for details.
    /// </summary>
    private static readonly HashSet<string> ProductionCommunities = new(StringComparer.OrdinalIgnoreCase)
    {
        "schadensfresse",
        "pes-squad",
        "relaxdays-tippt",
        "ehonda-ai-arena",
        "rabetrabauken2026"
    };

    private async Task ExecuteBonusWorkflow(BaseSettings settings, BonusContextBudget bonusContextBudget)
    {
        // Start root OTel activity for Langfuse trace
        using var activity = Telemetry.Source.StartActivity("bonus");

        // Set Langfuse environment based on community
        var environment = ProductionCommunities.Contains(settings.Community) ? "production" : "development";
        LangfuseActivityPropagation.SetEnvironment(activity, environment);

        string communityContext = settings.CommunityContext ?? settings.Community;
        var competition = CompetitionResolver.ResolveCompetition(settings.Competition, settings.Community, communityContext);
        var isBundesliga = string.Equals(
            competition,
            CompetitionIds.Bundesliga2026_27,
            StringComparison.Ordinal);
        var isReferenceCopyMode = isBundesliga
            && !string.Equals(settings.Community, communityContext, StringComparison.Ordinal);
        var generationCommunityContext = isReferenceCopyMode
            ? settings.Community
            : communityContext;
        var modelConfig = PredictionServiceCommandSupport.CreateModelConfig(
            settings.Model,
            settings.ReasoningEffort,
            competition,
            settings.Community,
            communityContext,
            settings.PromptSource,
            settings.LangfusePromptName,
            settings.LangfusePromptLabel,
            settings.LangfusePromptVersion,
            settings.MaxOutputTokenCount,
            bonusPrompt: true);
        var model = modelConfig.Model;
        // Set Langfuse trace-level attributes
        var sessionId = $"bonus-{settings.Community}";
        var traceTags = new[] { settings.Community, model, competition };
        LangfuseActivityPropagation.SetSessionId(activity, sessionId);
        LangfuseActivityPropagation.SetTraceTags(activity, traceTags);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "community", settings.Community);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "competition", competition);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "model", model);
        if (modelConfig.ReasoningEffort is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "reasoningEffort", modelConfig.ReasoningEffort);
        }
        SetPinnedModelConfigTraceMetadata(activity, modelConfig);
        LangfuseActivityPropagation.SetTraceMetadata(activity, "repredictMode", settings.IsRepredictMode ? "true" : "false");
        LangfuseActivityPropagation.SetTraceMetadata(
            activity,
            "bonusPredictionMode",
            isReferenceCopyMode ? "reference-copy-with-independent-fallback" : "independent");

        // Note: trace input is set after bonus questions are fetched

        if (string.IsNullOrWhiteSpace(settings.KicktippCredentialProfile))
        {
            _credentialLoader.Load(settings.Community);
        }
        else
        {
            _credentialLoader.Load(settings.Community, settings.KicktippCredentialProfile);
        }

        // Create services using factories
        var kicktippClient = _kicktippClientFactory.CreateClient();
        IPredictionService? predictionService = null;
        IPredictionService GetPredictionService()
        {
            if (predictionService is not null)
            {
                return predictionService;
            }

            predictionService = PredictionServiceCommandSupport.CreatePredictionService(
                _openAiServiceFactory,
                _langfuseClient,
                _console,
                model,
                competition,
                settings.Community,
                generationCommunityContext,
                settings.PromptSource,
                settings.LangfusePromptName,
                settings.LangfusePromptLabel,
                settings.LangfusePromptVersion,
                modelConfig.ReasoningEffort,
                settings.MaxOutputTokenCount,
                bonusPrompt: true,
                requireHostedPrompt: settings.RequireHostedPrompt);

            if (settings.Verbose)
            {
                _console.MarkupLine($"[dim]Bonus prompt:[/] [blue]{predictionService.GetBonusPromptPath()}[/]");
            }

            return predictionService;
        }
        
        // Create KPI Context Provider for bonus predictions using factory
        var kpiContextProvider = _contextProviderFactory.CreateKpiContextProvider(competition);
        var resolvedBonusContextProvider = isBundesliga
            ? kpiContextProvider as IResolvedBonusContextProvider
              ?? throw new InvalidOperationException(
                  "Bundesliga bonus prediction requires a resolved bonus-context provider.")
            : null;
        if (CompetitionResolver.IsWorldCupCompetition(competition))
        {
            await EnsureWorldCupRankingKpiPresentAsync(kpiContextProvider, communityContext);
        }
        
        var tokenUsageTracker = _openAiServiceFactory.GetTokenUsageTracker();
        
        // Create repositories
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository(competition);
        var resolvedBonusPredictionRepository = isBundesliga
            ? predictionRepository as IResolvedBonusContextPredictionRepository
              ?? throw new InvalidOperationException(
                   "Bundesliga bonus prediction requires a provenance-capable prediction repository.")
            : null;
        var bonusPredictionCopyRepository = isReferenceCopyMode
            ? predictionRepository as IBonusPredictionCopyRepository
              ?? throw new InvalidOperationException(
                  "Bundesliga reference bonus copying requires a compatibility-capable prediction repository.")
            : null;
        var publicationRepository = isBundesliga
            ? _firebaseServiceFactory.CreateDocumentPublicationRepository(competition)
            : null;
        var kpiRepository = isBundesliga
            ? null
            : _firebaseServiceFactory.CreateKpiRepository(competition);
        var databaseEnabled = true;
        
        // Reset token usage tracker for this workflow
        tokenUsageTracker.Reset();
        
        LangfuseActivityPropagation.SetTraceMetadata(activity, "communityContext", communityContext);
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
        _console.MarkupLine("[blue]Getting open bonus questions from Kicktipp...[/]");
        
        // Step 1: Get open bonus questions from Kicktipp
        var bonusQuestions = await kicktippClient.GetOpenBonusQuestionsAsync(settings.Community);
        
        if (!bonusQuestions.Any())
        {
            _console.MarkupLine("[yellow]No open bonus questions found[/]");
            return;
        }
        
        _console.MarkupLine($"[green]Found {bonusQuestions.Count} open bonus questions[/]");

        // Set trace input now that we know the questions
        var traceInput = new
        {
            community = settings.Community,
            model,
            competition,
            questions = bonusQuestions.Select(q => q.Text).ToArray()
        };
        activity?.SetTag("langfuse.trace.input", JsonSerializer.Serialize(traceInput));
        
        if (databaseEnabled)
        {
            _console.MarkupLine("[blue]Database enabled - checking for existing predictions...[/]");
        }
        
        var predictions = new Dictionary<string, BonusPrediction>();
        var traceRepredictionIndices = new HashSet<string>(StringComparer.Ordinal);
        var copyCompatibilityHashes = new HashSet<string>(StringComparer.Ordinal);
        var copySourcePredictionIdentities = new HashSet<string>(StringComparer.Ordinal);
        var copyFallbackReasons = new HashSet<string>(StringComparer.Ordinal);
        var copiedPredictionCount = 0;
        var independentFallbackCount = 0;
        
        // Step 2: For each question, check database first, then predict if needed
        foreach (var question in bonusQuestions)
        {
            _console.MarkupLine($"[cyan]Processing:[/] {Markup.Escape(question.Text)}");
            
            try
            {
                BonusPrediction? prediction = null;
                bool fromDatabase = false;
                bool shouldPredict = false;
                int? predictionRepredictionIndex = settings.IsRepredictMode ? null : 0;
                var predictionCommunityContext = communityContext;
                BonusQuestionCompatibilityManifest? targetCompatibilityManifest = null;
                if (isBundesliga)
                {
                    try
                    {
                        targetCompatibilityManifest = BonusQuestionCompatibilityManifest.Create(question);
                    }
                    catch (InvalidDataException ex)
                    {
                        throw new BundesligaBonusSafetyException(
                            "Bundesliga target bonus question has an invalid compatibility definition.",
                            ex);
                    }
                }

                if (isReferenceCopyMode)
                {
                    var copyCandidate = await ReadCachedValueSafelyAsync(
                        () => bonusPredictionCopyRepository!.GetBonusPredictionCopyCandidateAsync(
                            question,
                            modelConfig,
                            communityContext),
                        question,
                        isBundesliga);
                    var fallbackReason = "source_prediction_not_found";

                    if (copyCandidate is not null
                        && string.IsNullOrWhiteSpace(copyCandidate.PredictionIdentity))
                    {
                        fallbackReason = "source_prediction_identity_missing";
                    }
                    else if (copyCandidate is not null)
                    {
                        copySourcePredictionIdentities.Add(copyCandidate.PredictionIdentity!);
                        if (copyCandidate.QuestionCompatibilityManifest is null)
                        {
                            fallbackReason = "source_option_provenance_missing_or_invalid";
                        }
                        else
                        {
                            try
                            {
                                var compatibility = copyCandidate.QuestionCompatibilityManifest.TryMapPrediction(
                                    question,
                                    copyCandidate.BonusPrediction,
                                    out var mappedPrediction,
                                    out var mappedTargetManifest);
                                targetCompatibilityManifest = mappedTargetManifest;
                                fallbackReason = ToCopyFallbackReason(compatibility);

                                if (compatibility == BonusPredictionCopyCompatibility.Compatible)
                                {
                                    if (await CheckBonusPredictionMetadataOutdated(
                                            publicationRepository!,
                                            question,
                                            copyCandidate,
                                            communityContext,
                                            settings.Verbose))
                                    {
                                        throw new BundesligaBonusSafetyException(
                                            "Stored Bundesliga reference bonus prediction lacks current immutable provenance.");
                                    }

                                    prediction = mappedPrediction
                                        ?? throw new BundesligaBonusSafetyException(
                                            "Compatible Bundesliga bonus copy did not produce a mapped target prediction.");
                                    fromDatabase = true;
                                    copiedPredictionCount++;
                                    copyCompatibilityHashes.Add(targetCompatibilityManifest.CompatibilitySha256);
                                    _console.MarkupLine(
                                        "[green]  ✓ Reused compatible reference prediction[/] [dim](mapped to target options)[/]");
                                }
                            }
                            catch (InvalidDataException)
                            {
                                fallbackReason = "source_option_provenance_missing_or_invalid";
                            }
                        }
                    }

                    if (prediction is null)
                    {
                        predictionCommunityContext = settings.Community;
                        shouldPredict = true;
                        predictionRepredictionIndex = settings.IsRepredictMode ? null : 0;
                        independentFallbackCount++;
                        copyFallbackReasons.Add(fallbackReason);
                        LangfuseActivityPropagation.SetTraceMetadata(
                            activity,
                            "bonusEffectiveGenerationContext",
                            predictionCommunityContext);
                        _console.MarkupLine(
                            $"[yellow]  → Reference copy incompatible; generating independently in target context ({fallbackReason})[/]");
                    }
                }
                
                // Check if we have an existing prediction in the database
                if (!isReferenceCopyMode
                    && databaseEnabled
                    && !settings.OverrideDatabase
                    && !settings.IsRepredictMode)
                {
                    // Look for prediction by question text, model, and community context
                    prediction = await ReadCachedValueSafelyAsync(
                        () => predictionRepository!.GetBonusPredictionByTextAsync(question.Text, modelConfig, communityContext),
                        question,
                        isBundesliga);
                    if (prediction != null)
                    {
                        if (isBundesliga && await CheckBonusPredictionOutdated(
                                predictionRepository,
                                kpiRepository,
                                publicationRepository,
                                question,
                                prediction,
                                modelConfig,
                                communityContext,
                                isBundesliga,
                                settings.Verbose))
                        {
                            throw new BundesligaBonusSafetyException(
                                "Stored Bundesliga bonus prediction lacks current immutable provenance; use repredict or an explicit database override.");
                        }

                        fromDatabase = true;
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]  ✓ Found existing prediction[/] [dim](from database)[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            _console.MarkupLine($"[green]  ✓ Found existing prediction:[/] {string.Join(", ", optionTexts)} [dim](from database)[/]");
                        }
                    }
                }
                
                // Handle reprediction logic
                if (!isReferenceCopyMode && settings.IsRepredictMode && databaseEnabled)
                {
                    var currentRepredictionIndex = await ReadCachedValueSafelyAsync(
                        () => predictionRepository!.GetBonusRepredictionIndexAsync(question.Text, modelConfig, communityContext),
                        question,
                        isBundesliga);
                    
                    if (currentRepredictionIndex == -1)
                    {
                        // No prediction exists yet - create first prediction
                        shouldPredict = true;
                        predictionRepredictionIndex = 0;
                        _console.MarkupLine($"[yellow]  → No existing prediction found, creating first prediction...[/]");
                    }
                    else
                    {
                        var cachedPrediction = await ReadCachedValueSafelyAsync(
                            () => predictionRepository!.GetBonusPredictionByTextAsync(
                                question.Text,
                                modelConfig,
                                communityContext),
                            question,
                            isBundesliga);

                        // Check if we can create another reprediction
                        var maxAllowed = settings.MaxRepredictions ?? int.MaxValue;
                        var nextIndex = currentRepredictionIndex + 1;
                        var mustCheckOutdated = nextIndex <= maxAllowed || isBundesliga;
                        var isOutdated = mustCheckOutdated && await CheckBonusPredictionOutdated(
                            predictionRepository!,
                            kpiRepository,
                            publicationRepository,
                            question,
                            cachedPrediction,
                            modelConfig,
                            communityContext,
                            isBundesliga,
                            settings.Verbose);
                        
                        if (nextIndex <= maxAllowed)
                        {
                            if (isOutdated)
                            {
                                shouldPredict = true;
                                predictionRepredictionIndex = nextIndex;
                                _console.MarkupLine($"[yellow]  → Creating reprediction {nextIndex} (current: {currentRepredictionIndex}, max: {maxAllowed}) - prediction is outdated[/]");
                            }
                            else
                            {
                                traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                                _console.MarkupLine($"[green]  ✓ Skipped reprediction - current prediction is up-to-date[/]");

                                prediction = cachedPrediction;
                                if (prediction != null)
                                {
                                    fromDatabase = true;
                                    if (!settings.Agent)
                                    {
                                        var optionTexts = question.Options
                                            .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                            .Select(o => o.Text);
                                        _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {string.Join(", ", optionTexts)} [dim](reprediction {currentRepredictionIndex})[/]");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (isBundesliga && isOutdated)
                            {
                                throw new BundesligaBonusSafetyException(
                                    "Stored Bundesliga bonus prediction lacks current immutable provenance and cannot be reused at the reprediction limit.");
                            }

                            traceRepredictionIndices.Add(currentRepredictionIndex.ToString());
                            _console.MarkupLine($"[yellow]  ✗ Skipped - already at max repredictions ({currentRepredictionIndex}/{maxAllowed})[/]");
                            
                            // Get the latest prediction for display purposes
                            prediction = cachedPrediction;
                            if (prediction != null)
                            {
                                fromDatabase = true;
                                if (!settings.Agent)
                                {
                                    var optionTexts = question.Options
                                        .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                        .Select(o => o.Text);
                                    _console.MarkupLine($"[green]  ✓ Latest prediction:[/] {string.Join(", ", optionTexts)} [dim](reprediction {currentRepredictionIndex})[/]");
                                }
                            }
                        }
                    }
                }
                
                // If no existing prediction (normal mode) or we need to predict (reprediction mode), generate a new one
                if (prediction == null || shouldPredict)
                {
                    _console.MarkupLine($"[yellow]  → Generating new prediction...[/]");
                    
                    // Step 3: Get competition-aware context for bonus predictions.
                    var contextDocuments = new List<DocumentContext>();
                    ResolvedBonusContext? resolvedBonusContext = null;
                    
                    if (isBundesliga)
                    {
                        try
                        {
                            resolvedBonusContext = await resolvedBonusContextProvider!.ResolveBonusQuestionContextAsync(
                                question,
                                predictionCommunityContext,
                                budget: bonusContextBudget);
                        }
                        catch (Exception ex)
                        {
                            throw new BundesligaBonusSafetyException(
                                $"Failed to resolve immutable Bundesliga bonus provenance for '{question.Text}': {ex.Message}",
                                ex);
                        }

                        contextDocuments.AddRange(resolvedBonusContext.Documents);
                    }
                    else
                    {
                        await foreach (var context in kpiContextProvider.GetBonusQuestionContextAsync(question, communityContext))
                        {
                            contextDocuments.Add(context);
                        }
                    }
                    
                    if (settings.Verbose)
                    {
                        _console.MarkupLine($"[dim]    Using {contextDocuments.Count} bonus context documents[/]");
                        if (resolvedBonusContext is not null)
                        {
                            var selection = resolvedBonusContext.Selection;
                            _console.MarkupLine(
                                $"[dim]    Category {selection.Category}; estimated {selection.EstimatedUtf8Bytes} UTF-8 bytes/{selection.EstimatedTokens} tokens; budgets {selection.Budget.MaximumDocuments}/{selection.Budget.MaximumEstimatedTokens}; excluded {string.Join(',', selection.ExcludedDocuments.Select(exclusion => $"{exclusion.Document.Name}={exclusion.Reason}"))}[/]");
                        }
                    }

                    var bonusSelection = resolvedBonusContext?.Selection;
                    var telemetryMetadata = new PredictionTelemetryMetadata(
                        RepredictionIndex: predictionRepredictionIndex,
                        Competition: competition,
                        ContextDocumentNames: contextDocuments.Select(document => document.Name).ToArray(),
                        RosterPublicationSnapshotId: resolvedBonusContext?.Manifest.RosterPublicationSnapshotId,
                        ClubEloPublicationSnapshotId: resolvedBonusContext?.Manifest.ClubEloPublicationSnapshotId,
                        BonusContextCategory: bonusSelection?.Category.ToString(),
                        BonusContextSelectedDocuments: bonusSelection?.SelectedDocumentNames,
                        BonusContextExcludedDocuments: bonusSelection?.ExcludedDocuments.Select(exclusion =>
                            $"{exclusion.Document.Name}={exclusion.Reason}").ToArray(),
                        BonusContextEstimatedUtf8Bytes: bonusSelection?.EstimatedUtf8Bytes,
                        BonusContextEstimatedTokens: bonusSelection?.EstimatedTokens,
                        BonusContextDocumentBudget: bonusSelection?.Budget.MaximumDocuments,
                        BonusContextEstimatedTokenBudget: bonusSelection?.Budget.MaximumEstimatedTokens);
                    
                    // Predict the bonus question
                    prediction = await GetPredictionService().PredictBonusQuestionAsync(
                        question,
                        contextDocuments,
                        telemetryMetadata);

                    if (isBundesliga)
                    {
                        prediction = ValidateGeneratedBundesligaPrediction(question, prediction);
                    }
                    
                    if (prediction != null)
                    {
                        if (predictionRepredictionIndex.HasValue)
                        {
                            traceRepredictionIndices.Add(predictionRepredictionIndex.Value.ToString());
                        }

                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[green]  ✓ Generated prediction[/]");
                        }
                        else
                        {
                            var optionTexts = question.Options
                                .Where(o => prediction.SelectedOptionIds.Contains(o.Id))
                                .Select(o => o.Text);
                            _console.MarkupLine($"[green]  ✓ Generated prediction:[/] {string.Join(", ", optionTexts)}");
                        }
                        
                        // Save to database immediately if enabled
                        if (databaseEnabled && !settings.DryRun)
                        {
                            try
                            {
                                // Get token usage and cost information
                                var cost = (double)tokenUsageTracker.GetLastCost(); // Get the cost for this individual question
                                // Use the new GetLastUsageJson method to get full JSON
                                var tokenUsageJson = tokenUsageTracker.GetLastUsageJson() ?? "{}";
                                
                                if (settings.IsRepredictMode)
                                {
                                    // Save as reprediction with specific index
                                    var currentIndex = await predictionRepository!.GetBonusRepredictionIndexAsync(
                                        question.Text,
                                        modelConfig,
                                        predictionCommunityContext);
                                    var nextIndex = currentIndex == -1 ? 0 : currentIndex + 1;
                                    
                                    if (isBundesliga)
                                    {
                                        await resolvedBonusPredictionRepository!.SaveBonusRepredictionWithResolvedContextAsync(
                                            question,
                                            prediction,
                                            modelConfig,
                                            tokenUsageJson,
                                            cost,
                                            predictionCommunityContext,
                                            contextDocuments.Select(document => document.Name),
                                            nextIndex,
                                            resolvedBonusContext!.Manifest);
                                    }
                                    else
                                    {
                                        await predictionRepository!.SaveBonusRepredictionAsync(
                                            question,
                                            prediction,
                                            modelConfig,
                                            tokenUsageJson,
                                            cost,
                                            predictionCommunityContext,
                                            contextDocuments.Select(document => document.Name),
                                            nextIndex);
                                    }
                                        
                                    if (settings.Verbose)
                                    {
                                        _console.MarkupLine($"[dim]    ✓ Saved as reprediction {nextIndex} to database[/]");
                                    }
                                }
                                else
                                {
                                    // Save normally (override or new prediction)
                                    if (isBundesliga)
                                    {
                                        await resolvedBonusPredictionRepository!.SaveBonusPredictionWithResolvedContextAsync(
                                            question,
                                            prediction,
                                            modelConfig,
                                            tokenUsageJson,
                                            cost,
                                            predictionCommunityContext,
                                            contextDocuments.Select(document => document.Name),
                                            resolvedBonusContext!.Manifest,
                                            overrideCreatedAt: settings.OverrideDatabase);
                                    }
                                    else
                                    {
                                        await predictionRepository!.SaveBonusPredictionAsync(
                                            question,
                                            prediction,
                                            modelConfig,
                                            tokenUsageJson,
                                            cost,
                                            predictionCommunityContext,
                                            contextDocuments.Select(document => document.Name),
                                            overrideCreatedAt: settings.OverrideDatabase);
                                    }
                                        
                                    if (settings.Verbose)
                                    {
                                        _console.MarkupLine($"[dim]    ✓ Saved to database[/]");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (isBundesliga)
                                {
                                    throw new BundesligaBonusSafetyException(
                                        $"Failed to persist immutable Bundesliga bonus provenance for '{question.Text}': {ex.Message}",
                                        ex);
                                }

                                _logger.LogError(ex, "Failed to save bonus prediction for question '{QuestionText}'", question.Text);
                                _console.MarkupLine($"[red]    ✗ Failed to save to database: {ex.Message}[/]");
                            }
                        }
                        else if (databaseEnabled && settings.DryRun && settings.Verbose)
                        {
                            _console.MarkupLine($"[dim]    (Dry run - skipped database save)[/]");
                        }
                        
                        // Show individual question token usage in verbose mode
                        if (settings.Verbose)
                        {
                            var questionUsage = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
                                ? tokenUsageTracker.GetLastUsageCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
                                : tokenUsageTracker.GetLastUsageCompactSummary();
                            _console.MarkupLine($"[dim]    Token usage: {questionUsage}[/]");
                        }
                    }
                    else
                    {
                        _console.MarkupLine($"[red]  ✗ Failed to generate prediction[/]");
                        continue;
                    }
                }
                
                predictions[question.FormFieldName ?? question.Text] = prediction;
                
                if (!fromDatabase && settings.Verbose)
                {
                    _console.MarkupLine($"[dim]    Ready for Kicktipp placement[/]");
                }
            }
            catch (BundesligaBonusSafetyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bonus question '{QuestionText}'", question.Text);
                _console.MarkupLine($"[red]  ✗ Error processing question: {ex.Message}[/]");
            }
        }

        if (traceRepredictionIndices.Count > 0)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "repredictionIndices", PredictionTelemetryMetadata.BuildDelimitedFilterValue(traceRepredictionIndices), propagateToObservations: false);
            LangfuseActivityPropagation.SetTraceMetadata(activity, "hasRepredictions", traceRepredictionIndices.Any(index => index != "0") ? "true" : "false", propagateToObservations: false);
        }

        if (isReferenceCopyMode)
        {
            LangfuseActivityPropagation.SetTraceMetadata(
                activity,
                "bonusCopySourceCommunityContext",
                communityContext,
                propagateToObservations: false);
            LangfuseActivityPropagation.SetTraceMetadata(
                activity,
                "bonusCopiedPredictionCount",
                copiedPredictionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                propagateToObservations: false);
            LangfuseActivityPropagation.SetTraceMetadata(
                activity,
                "bonusIndependentFallbackCount",
                independentFallbackCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                propagateToObservations: false);
            if (copyCompatibilityHashes.Count > 0)
            {
                LangfuseActivityPropagation.SetTraceMetadata(
                    activity,
                    "bonusCopyCompatibilityHashes",
                    PredictionTelemetryMetadata.BuildDelimitedFilterValue(copyCompatibilityHashes),
                    propagateToObservations: false);
            }

            if (copySourcePredictionIdentities.Count > 0)
            {
                LangfuseActivityPropagation.SetTraceMetadata(
                    activity,
                    "bonusCopySourcePredictionIdentities",
                    PredictionTelemetryMetadata.BuildDelimitedFilterValue(copySourcePredictionIdentities),
                    propagateToObservations: false);
            }

            if (copyFallbackReasons.Count > 0)
            {
                LangfuseActivityPropagation.SetTraceMetadata(
                    activity,
                    "bonusCopyFallbackReasons",
                    PredictionTelemetryMetadata.BuildDelimitedFilterValue(copyFallbackReasons),
                    propagateToObservations: false);
            }
        }
        
        if (!predictions.Any())
        {
            _console.MarkupLine("[yellow]No predictions available, nothing to place[/]");
            activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(new { error = "No predictions available" }));
            return;
        }

        // Set trace output with all bonus predictions
        var traceOutput = predictions.Select(p => new
        {
            question = p.Key,
            selectedOptionIds = p.Value.SelectedOptionIds
        }).ToArray();
        activity?.SetTag("langfuse.trace.output", JsonSerializer.Serialize(traceOutput));
        
        // Step 4: Place all predictions using PlaceBonusPredictionsAsync
        _console.MarkupLine($"[blue]Placing {predictions.Count} bonus predictions to Kicktipp...[/]");
        
        if (settings.DryRun)
        {
            _console.MarkupLine($"[magenta]✓ Dry run mode - would have placed {predictions.Count} bonus predictions (no actual changes made)[/]");
        }
        else
        {
            var success = await kicktippClient.PlaceBonusPredictionsAsync(settings.Community, predictions, overridePredictions: settings.OverrideKicktipp);
            
            if (success)
            {
                _console.MarkupLine($"[green]✓ Successfully placed all {predictions.Count} bonus predictions![/]");
            }
            else
            {
                _console.MarkupLine("[red]✗ Failed to place some or all bonus predictions[/]");
            }
        }
        
        // Display token usage summary
        var summary = !string.IsNullOrEmpty(settings.EstimatedCostsModel)
            ? tokenUsageTracker.GetCompactSummaryWithEstimatedCosts(settings.EstimatedCostsModel)
            : tokenUsageTracker.GetCompactSummary();
        _console.MarkupLine($"[dim]Token usage (uncached/cached/reasoning/output/$cost): {summary}[/]");
    }

    private static void SetPinnedModelConfigTraceMetadata(
        System.Diagnostics.Activity? activity,
        PredictionModelConfig modelConfig)
    {
        LangfuseActivityPropagation.SetTraceMetadata(activity, "modelConfigKey", modelConfig.IdentityKey);
        if (modelConfig.MaxOutputTokenCount is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "maxOutputTokens", modelConfig.MaxOutputTokenCount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (modelConfig.PromptName is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "promptName", modelConfig.PromptName);
        }

        if (modelConfig.PromptVersion is not null)
        {
            LangfuseActivityPropagation.SetTraceMetadata(activity, "promptVersion", modelConfig.PromptVersion.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static BonusContextBudget ResolveBonusContextBudget(BaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings is not BonusSettings bonusSettings)
        {
            return BonusContextBudget.Default;
        }

        return new BonusContextBudget(
            bonusSettings.BonusContextDocumentBudget ?? BonusContextBudget.DefaultMaximumDocuments,
            bonusSettings.BonusContextEstimatedTokenBudget ?? BonusContextBudget.DefaultMaximumEstimatedTokens);
    }

    private static async Task EnsureWorldCupRankingKpiPresentAsync(
        IKpiContextProvider kpiContextProvider,
        string communityContext)
    {
        await foreach (var context in kpiContextProvider.GetContextAsync(communityContext))
        {
            if (string.Equals(context.Name, FifaRankingsDocumentName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Missing required WM26 KPI context document 'fifa-rankings'. " +
            "Run collect-context fifa for this community context.");
    }

    private async Task<bool> CheckBonusPredictionOutdated(
        IPredictionRepository predictionRepository,
        IKpiRepository? kpiRepository,
        IDocumentPublicationRepository? publicationRepository,
        BonusQuestion question,
        BonusPrediction? cachedPrediction,
        PredictionModelConfig modelConfig,
        string communityContext,
        bool isBundesliga,
        bool verbose)
    {
        try
        {
            var predictionMetadata = await predictionRepository.GetBonusPredictionMetadataByTextAsync(
                question.Text, modelConfig, communityContext);

            if (predictionMetadata == null)
            {
                return isBundesliga;
            }

            if (isBundesliga)
            {
                if (!BonusPredictionContentEquality.Equals(cachedPrediction, predictionMetadata.BonusPrediction))
                {
                    throw new BundesligaBonusSafetyException(
                        "Stored Bundesliga bonus prediction and immutable provenance metadata do not describe the same cached value.");
                }

                return await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
                    publicationRepository
                    ?? throw new InvalidOperationException(
                        "Bundesliga bonus outdated checks require a publication repository."),
                    question,
                    communityContext,
                    predictionMetadata);
            }

            foreach (var contextDocumentName in predictionMetadata.ContextDocumentNames)
            {
                var kpiDocument = await (kpiRepository
                    ?? throw new InvalidOperationException("Legacy bonus outdated checks require a KPI repository."))
                    .GetKpiDocumentAsync(contextDocumentName, communityContext);
                if (kpiDocument != null)
                {
                    if (kpiDocument.CreatedAt > predictionMetadata.CreatedAt)
                    {
                        if (verbose)
                        {
                            _console.MarkupLine($"[yellow]KPI document '{contextDocumentName}' updated after prediction was created[/]");
                            _console.MarkupLine($"  [dim]Prediction created:[/] {predictionMetadata.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                            _console.MarkupLine($"  [dim]KPI document created:[/] {kpiDocument.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                        }

                        return true;
                    }

                    if (verbose)
                    {
                        _console.MarkupLine($"[dim]KPI document '{contextDocumentName}' found, version {kpiDocument.Version} is latest[/]");
                    }
                }
                else if (verbose)
                {
                    _console.MarkupLine($"[yellow]Warning: KPI document '{contextDocumentName}' not found[/]");
                }
            }

            return false;
        }
        catch (BundesligaBonusSafetyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                _console.MarkupLine($"[yellow]Warning: Could not check if prediction is outdated: {ex.Message}[/]");
            }

            if (isBundesliga)
            {
                throw new BundesligaBonusSafetyException(
                    $"Could not validate immutable Bundesliga bonus provenance for '{question.Text}'.",
                    ex);
            }

            return false;
        }
    }

    private async Task<bool> CheckBonusPredictionMetadataOutdated(
        IDocumentPublicationRepository publicationRepository,
        BonusQuestion question,
        BonusPredictionMetadata predictionMetadata,
        string communityContext,
        bool verbose)
    {
        try
        {
            return await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
                publicationRepository,
                question,
                communityContext,
                predictionMetadata);
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                _console.MarkupLine(
                    $"[yellow]Warning: Could not validate source bonus provenance: {ex.Message}[/]");
            }

            throw new BundesligaBonusSafetyException(
                $"Could not validate immutable Bundesliga reference bonus provenance for '{question.Text}'.",
                ex);
        }
    }

    private static string ToCopyFallbackReason(BonusPredictionCopyCompatibility compatibility)
    {
        return compatibility switch
        {
            BonusPredictionCopyCompatibility.Compatible => "compatible",
            BonusPredictionCopyCompatibility.QuestionMismatch => "question_mismatch",
            BonusPredictionCopyCompatibility.MaxSelectionsMismatch => "max_selections_mismatch",
            BonusPredictionCopyCompatibility.OptionSetMismatch => "option_set_mismatch",
            BonusPredictionCopyCompatibility.InvalidSourceSelection => "invalid_source_selection",
            _ => throw new ArgumentOutOfRangeException(nameof(compatibility), compatibility, null)
        };
    }

    private static BonusPrediction ValidateGeneratedBundesligaPrediction(
        BonusQuestion question,
        BonusPrediction? prediction)
    {
        if (prediction is null)
        {
            throw new BundesligaBonusSafetyException(
                "Bundesliga bonus prediction service returned no prediction.");
        }

        var selectedOptionIds = prediction.SelectedOptionIds;
        if (selectedOptionIds is null || selectedOptionIds.Count != question.MaxSelections)
        {
            throw new BundesligaBonusSafetyException(
                $"Bundesliga bonus prediction must select exactly {question.MaxSelections} target options.");
        }

        if (selectedOptionIds.Distinct(StringComparer.Ordinal).Count() != selectedOptionIds.Count)
        {
            throw new BundesligaBonusSafetyException(
                "Bundesliga bonus prediction contains duplicate target option IDs.");
        }

        var targetOptionIds = question.Options
            .Select(option => option.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedOptionIds.Any(optionId => !targetOptionIds.Contains(optionId)))
        {
            throw new BundesligaBonusSafetyException(
                "Bundesliga bonus prediction contains an unknown target option ID.");
        }

        return prediction;
    }

    private static async Task<T> ReadCachedValueSafelyAsync<T>(
        Func<Task<T>> read,
        BonusQuestion question,
        bool isBundesliga)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (isBundesliga)
        {
            throw new BundesligaBonusSafetyException(
                $"Failed to read a coherent cached Bundesliga bonus prediction for '{question.Text}': {ex.Message}",
                ex);
        }
    }
}
