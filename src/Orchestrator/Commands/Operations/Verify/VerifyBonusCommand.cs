using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;
using Spectre.Console;
using KicktippIntegration;
using Orchestrator.Commands.Shared;
using Orchestrator.Commands.Operations.Bonus;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Factories;

namespace Orchestrator.Commands.Operations.Verify;

public class VerifyBonusCommand : AsyncCommand<VerifyBonusSettings>
{
    private readonly IAnsiConsole _console;
    private readonly IFirebaseServiceFactory _firebaseServiceFactory;
    private readonly IKicktippClientFactory _kicktippClientFactory;
    private readonly ICommunityKicktippCredentialLoader _credentialLoader;
    private readonly ILogger<VerifyBonusCommand> _logger;

    public VerifyBonusCommand(
        IAnsiConsole console,
        IFirebaseServiceFactory firebaseServiceFactory,
        IKicktippClientFactory kicktippClientFactory,
        ICommunityKicktippCredentialLoader credentialLoader,
        ILogger<VerifyBonusCommand> logger)
    {
        _console = console;
        _firebaseServiceFactory = firebaseServiceFactory;
        _kicktippClientFactory = kicktippClientFactory;
        _credentialLoader = credentialLoader;
        _logger = logger;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, VerifyBonusSettings settings, CancellationToken cancellationToken)
    {
        
        try
        {
            SchadensfressePrimaryRouteGate.EnsureAvailable(settings.Community);

            _console.MarkupLine($"[green]Verify bonus command initialized[/]");
            
            if (settings.Verbose)
            {
                _console.MarkupLine("[dim]Verbose mode enabled[/]");
            }
            
            if (settings.Agent)
            {
                _console.MarkupLine("[blue]Agent mode enabled - prediction details will be hidden[/]");
            }
            
            if (settings.InitMatchday)
            {
                _console.MarkupLine("[cyan]Init bonus mode enabled - will return error if no predictions exist[/]");
            }
            
            if (settings.CheckOutdated)
            {
                _console.MarkupLine("[cyan]Outdated check enabled - predictions will be checked against latest context documents[/]");
            }
            
            // Execute the verification workflow
            var hasDiscrepancies = await ExecuteVerificationWorkflow(settings);
            
            return hasDiscrepancies ? 1 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing verify bonus command");
            _console.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
    
    private async Task<bool> ExecuteVerificationWorkflow(VerifyBonusSettings settings)
    {
        string communityContext = settings.CommunityContext ?? settings.Community;
        var competition = CompetitionResolver.ResolveCompetition(settings.Competition, settings.Community, communityContext);
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
        if (string.IsNullOrWhiteSpace(settings.KicktippCredentialProfile))
        {
            _credentialLoader.Load(settings.Community);
        }
        else
        {
            _credentialLoader.Load(settings.Community, settings.KicktippCredentialProfile);
        }
        var kicktippClient = _kicktippClientFactory.CreateClient();
        var isBundesliga = string.Equals(
            competition,
            CompetitionIds.Bundesliga2026_27,
            StringComparison.Ordinal);
        var isReferenceCopyMode = isBundesliga
            && !string.Equals(settings.Community, communityContext, StringComparison.Ordinal);
        // Try to get the prediction repository (may be null if Firebase is not configured)
        var predictionRepository = _firebaseServiceFactory.CreatePredictionRepository(competition);
        if (predictionRepository == null)
        {
            _console.MarkupLine("[red]Error: Database not configured. Cannot verify predictions without database access.[/]");
            _console.MarkupLine("[yellow]Hint: Set FIREBASE_PROJECT_ID and FIREBASE_SERVICE_ACCOUNT_JSON environment variables[/]");
            return true; // Consider this a failure
        }
        
        // Get KPI repository for outdated checks (required for bonus predictions)
        var kpiRepository = isBundesliga
            ? null
            : _firebaseServiceFactory.CreateKpiRepository(competition);
        var publicationRepository = isBundesliga
            ? _firebaseServiceFactory.CreateDocumentPublicationRepository(competition)
            : null;
        var bonusPredictionCopyRepository = isReferenceCopyMode
            ? predictionRepository as IBonusPredictionCopyRepository
              ?? throw new InvalidOperationException(
                  "Bundesliga reference bonus verification requires a compatibility-capable prediction repository.")
            : null;
        
        _console.MarkupLine($"[blue]Using community:[/] [yellow]{settings.Community}[/]");
        _console.MarkupLine($"[blue]Using community context:[/] [yellow]{communityContext}[/]");
        _console.MarkupLine($"[blue]Using competition:[/] [yellow]{competition}[/]");
        _console.MarkupLine($"[blue]Using model config:[/] [yellow]{modelConfig.DisplayName}[/]");
        _console.MarkupLine("[blue]Getting open bonus questions from Kicktipp...[/]");
        
        // Step 1: Get open bonus questions from Kicktipp
        var openBonusQuestions = await kicktippClient.GetOpenBonusQuestionsAsync(settings.Community);
        var bonusQuestions = BonusQuestionExecutionScope.SelectAtOrBefore(
            openBonusQuestions,
            settings.BonusDeadlineAtOrBefore);

        if (!string.IsNullOrWhiteSpace(settings.BonusDeadlineAtOrBefore))
        {
            _console.MarkupLine(
                $"[blue]Selected {bonusQuestions.Count} of {openBonusQuestions.Count} open bonus questions with deadline at or before[/] [yellow]{Markup.Escape(settings.BonusDeadlineAtOrBefore)}[/]");
        }

        if (!string.IsNullOrEmpty(settings.BonusDeadlineAtOrBefore) && bonusQuestions.Count == 0)
        {
            _console.MarkupLine(
                $"[red]The explicit bonus deadline ceiling '{Markup.Escape(settings.BonusDeadlineAtOrBefore)}' selected zero open questions.[/]");
            return true;
        }
        
        if (!bonusQuestions.Any())
        {
            _console.MarkupLine("[yellow]No bonus questions found on Kicktipp[/]");
            return false;
        }
        
        _console.MarkupLine($"[green]Found {bonusQuestions.Count} bonus questions on Kicktipp[/]");
        
        _console.MarkupLine("[blue]Getting placed bonus predictions from Kicktipp...[/]");
        
        // Step 1.5: Get currently placed predictions from Kicktipp
        var placedPredictions = await kicktippClient.GetPlacedBonusPredictionsAsync(settings.Community);
        
        _console.MarkupLine("[blue]Retrieving predictions from database...[/]");
        
        var hasDiscrepancies = false;
        var totalQuestions = 0;
        var questionsWithDatabasePredictions = 0;
        var validPredictions = 0;
        
        // Step 2: For each bonus question, check if we have a prediction in database
        foreach (var question in bonusQuestions)
        {
            totalQuestions++;
            
            try
            {
                // Get the exact independently stored prediction, or reconstruct the target
                // selection from the compatible reference prediction without a model call.
                if (settings.Verbose)
                {
                    _console.MarkupLine($"[dim]  Looking up: {Markup.Escape(question.Text)}[/]");
                }

                BonusPrediction? databasePrediction;
                bool isOutdated;
                if (isReferenceCopyMode)
                {
                    var resolved = await ResolveReferenceCopyPredictionAsync(
                        predictionRepository,
                        bonusPredictionCopyRepository!,
                        publicationRepository!,
                        question,
                        modelConfig,
                        settings.Community,
                        communityContext,
                        settings.Verbose);
                    databasePrediction = resolved.Prediction;
                    isOutdated = resolved.IsOutdated;
                }
                else
                {
                    databasePrediction = await predictionRepository.GetBonusPredictionByTextAsync(
                        question.Text,
                        modelConfig,
                        communityContext);
                    isOutdated = databasePrediction is not null
                        && settings.CheckOutdated
                        && await CheckBonusPredictionOutdated(
                            predictionRepository,
                            kpiRepository,
                            publicationRepository,
                            question,
                            databasePrediction,
                            modelConfig,
                            communityContext,
                            isBundesliga,
                            settings.Verbose);
                }
                var kicktippPrediction = placedPredictions.GetValueOrDefault(question.FormFieldName ?? question.Text);
                
                if (databasePrediction != null)
                {
                    questionsWithDatabasePredictions++;
                    
                    // Validate the prediction against the question
                    var isValidPrediction = ValidateBonusPrediction(question, databasePrediction);
                    
                    // Compare database prediction with Kicktipp placed prediction
                    var predictionsMatch = CompareBonusPredictions(databasePrediction, kicktippPrediction);
                    
                    // Consider prediction valid if it passes validation, matches Kicktipp, and is not outdated
                    var isPredictionValid = isValidPrediction && predictionsMatch && !isOutdated;
                    
                    if (isPredictionValid)
                    {
                        validPredictions++;
                        
                        if (settings.Verbose)
                        {
                            if (settings.Agent)
                            {
                                _console.MarkupLine($"[green]✓ {Markup.Escape(question.Text)}[/] [dim](valid)[/]");
                            }
                            else
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                _console.MarkupLine($"[green]✓ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](valid)[/]");
                            }
                        }
                    }
                    else
                    {
                        hasDiscrepancies = true;
                        
                        if (settings.Agent)
                        {
                            var status = !isValidPrediction ? "invalid prediction" : 
                                        !predictionsMatch ? "mismatch with Kicktipp" : "outdated";
                            _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}[/] [dim]({status})[/]");
                        }
                        else
                        {
                            if (!isValidPrediction)
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](invalid prediction)[/]");
                            }
                            else if (!predictionsMatch)
                            {
                                // Show mismatch details
                                var databaseTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                var kicktippTexts = kicktippPrediction != null 
                                    ? question.Options
                                        .Where(o => kicktippPrediction.SelectedOptionIds.Contains(o.Id))
                                        .Select(o => o.Text)
                                    : new List<string>();
                                
                                _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/]");
                                _console.MarkupLine($"  [yellow]Database:[/] {string.Join(", ", databaseTexts)}");
                                _console.MarkupLine($"  [yellow]Kicktipp:[/] {(kicktippTexts.Any() ? string.Join(", ", kicktippTexts) : "no prediction")}");
                            }
                            else if (isOutdated)
                            {
                                var optionTexts = question.Options
                                    .Where(o => databasePrediction.SelectedOptionIds.Contains(o.Id))
                                    .Select(o => o.Text);
                                _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] {string.Join(", ", optionTexts)} [dim](outdated)[/]");
                                _console.MarkupLine($"  [yellow]Status:[/] Outdated (context updated after prediction)");
                            }
                        }
                    }
                }
                else
                {
                    hasDiscrepancies = true;
                    
                    if (settings.Verbose)
                    {
                        if (settings.Agent)
                        {
                            _console.MarkupLine($"[yellow]○ {Markup.Escape(question.Text)}[/] [dim](no prediction)[/]");
                        }
                        else
                        {
                            _console.MarkupLine($"[yellow]○ {Markup.Escape(question.Text)}:[/] [dim](no prediction)[/]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                hasDiscrepancies = true;
                _logger.LogError(ex, "Error verifying bonus prediction for question '{QuestionText}'", question.Text);
                
                if (settings.Agent)
                {
                    _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}[/] [dim](error)[/]");
                }
                else
                {
                    _console.MarkupLine($"[red]✗ {Markup.Escape(question.Text)}:[/] Error during verification");
                }
            }
        }
        
        // Step 3: Display summary
        _console.WriteLine();
        _console.MarkupLine("[bold]Verification Summary:[/]");
        _console.MarkupLine($"  Total bonus questions: {totalQuestions}");
        _console.MarkupLine($"  Questions with database predictions: {questionsWithDatabasePredictions}");
        _console.MarkupLine($"  Valid predictions: {validPredictions}");
        
        // Check for init-bonus mode first
        if (settings.InitMatchday && questionsWithDatabasePredictions == 0)
        {
            _console.MarkupLine("[yellow]  Init bonus detected - no database predictions exist[/]");
            _console.MarkupLine("[red]Returning error to trigger initial prediction workflow[/]");
            return true; // Return error to trigger workflow
        }
        
        if (hasDiscrepancies)
        {
            _console.MarkupLine($"[red]  Missing or invalid predictions: {totalQuestions - validPredictions}[/]");
            _console.MarkupLine("[red]Verification failed - some predictions are missing or invalid[/]");
        }
        else
        {
            _console.MarkupLine("[green]  All predictions are valid - verification successful[/]");
        }
        
        return hasDiscrepancies;
    }

    private async Task<ResolvedVerificationPrediction> ResolveReferenceCopyPredictionAsync(
        IPredictionRepository predictionRepository,
        IBonusPredictionCopyRepository bonusPredictionCopyRepository,
        IDocumentPublicationRepository publicationRepository,
        BonusQuestion targetQuestion,
        PredictionModelConfig modelConfig,
        string targetCommunityContext,
        string sourceCommunityContext,
        bool verbose)
    {
        // Validate the target before inspecting source provenance. An ambiguous target is
        // never an ordinary copy mismatch and must fail closed.
        _ = BonusQuestionCompatibilityManifest.Create(targetQuestion);
        var referenceProjection = BonusQuestionExecutionScope.ResolveReferenceProjection(
            CompetitionIds.Bundesliga2026_27,
            targetCommunityContext,
            sourceCommunityContext,
            targetQuestion);
        var referenceQuestion = referenceProjection.Question;

        var copyCandidate = await bonusPredictionCopyRepository.GetBonusPredictionCopyCandidateAsync(
            referenceQuestion,
            modelConfig,
            sourceCommunityContext);

        if (copyCandidate is not null
            && !string.IsNullOrWhiteSpace(copyCandidate.PredictionIdentity)
            && copyCandidate.QuestionCompatibilityManifest is not null)
        {
            try
            {
                var compatibility = copyCandidate.QuestionCompatibilityManifest.TryMapPrediction(
                    referenceQuestion,
                    copyCandidate.BonusPrediction,
                    out var mappedPrediction,
                    out _);

                if (compatibility == BonusPredictionCopyCompatibility.Compatible)
                {
                    var isOutdated = await CheckBundesligaBonusPredictionMetadataOutdated(
                        publicationRepository,
                        referenceQuestion,
                        copyCandidate.BonusPrediction,
                        copyCandidate,
                        sourceCommunityContext,
                        verbose);
                    return new ResolvedVerificationPrediction(
                        mappedPrediction
                        ?? throw new InvalidDataException(
                            "Compatible Bundesliga bonus verification did not produce a mapped target prediction."),
                        isOutdated);
                }
            }
            catch (InvalidDataException)
            {
                // Malformed source compatibility provenance is an ordinary incompatibility.
                // Verify the exact independently generated target fallback below.
            }
        }

        var targetPrediction = await predictionRepository.GetBonusPredictionByTextAsync(
            targetQuestion.Text,
            modelConfig,
            targetCommunityContext);
        if (targetPrediction is null)
        {
            return new ResolvedVerificationPrediction(null, true);
        }

        var targetMetadata = await predictionRepository.GetBonusPredictionMetadataByTextAsync(
            targetQuestion.Text,
            modelConfig,
            targetCommunityContext);
        if (targetMetadata is null
            || string.IsNullOrWhiteSpace(targetMetadata.PredictionIdentity)
            || targetMetadata.QuestionCompatibilityManifest is null
            || !BonusPredictionContentEquality.Equals(targetPrediction, targetMetadata.BonusPrediction))
        {
            return new ResolvedVerificationPrediction(targetPrediction, true);
        }

        try
        {
            var compatibility = targetMetadata.QuestionCompatibilityManifest.TryMapPrediction(
                targetQuestion,
                targetMetadata.BonusPrediction,
                out var mappedTargetPrediction,
                out _);
            if (compatibility != BonusPredictionCopyCompatibility.Compatible
                || mappedTargetPrediction is null)
            {
                return new ResolvedVerificationPrediction(targetPrediction, true);
            }

            var isOutdated = await CheckBundesligaBonusPredictionMetadataOutdated(
                publicationRepository,
                targetQuestion,
                targetPrediction,
                targetMetadata,
                targetCommunityContext,
                verbose);
            return new ResolvedVerificationPrediction(mappedTargetPrediction, isOutdated);
        }
        catch (InvalidDataException)
        {
            return new ResolvedVerificationPrediction(targetPrediction, true);
        }
    }
    
    private static bool ValidateBonusPrediction(BonusQuestion question, BonusPrediction prediction)
    {
        // Check if all selected option IDs exist in the question
        var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
        var allOptionsValid = prediction.SelectedOptionIds.All(id => validOptionIds.Contains(id));
        
        if (!allOptionsValid)
        {
            return false;
        }
        
        // Check if the number of selections is valid
        var selectionCount = prediction.SelectedOptionIds.Count;
        if (selectionCount < 1 || selectionCount > question.MaxSelections)
        {
            return false;
        }
        
        // Check for duplicates
        var uniqueSelections = prediction.SelectedOptionIds.Distinct().Count();
        if (uniqueSelections != selectionCount)
        {
            return false;
        }
        
        return true;
    }
    
    private async Task<bool> CheckBonusPredictionOutdated(
        IPredictionRepository predictionRepository,
        IKpiRepository? kpiRepository,
        IDocumentPublicationRepository? publicationRepository,
        BonusQuestion question,
        BonusPrediction databasePrediction,
        PredictionModelConfig modelConfig,
        string communityContext,
        bool isBundesliga,
        bool verbose)
    {
        try
        {
            // Get prediction metadata (includes creation timestamp and context document names)
            var predictionMetadata = await predictionRepository.GetBonusPredictionMetadataByTextAsync(
                question.Text, modelConfig, communityContext);
            
            if (predictionMetadata == null)
            {
                return isBundesliga;
            }

            if (isBundesliga)
            {
                return await CheckBundesligaBonusPredictionMetadataOutdated(
                    publicationRepository
                    ?? throw new InvalidOperationException(
                        "Bundesliga bonus outdated checks require a publication repository."),
                    question,
                    databasePrediction,
                    predictionMetadata,
                    communityContext,
                    verbose);
            }
            
            // Check if any KPI document has been updated after the prediction was created
            foreach (var contextDocumentName in predictionMetadata.ContextDocumentNames)
            {
                var kpiDocument = await (kpiRepository
                    ?? throw new InvalidOperationException("Legacy bonus outdated checks require a KPI repository."))
                    .GetKpiDocumentAsync(contextDocumentName, communityContext);
                if (kpiDocument != null)
                {
                    // Compare the creation timestamps
                    // Note: We need to be careful about timezone handling here
                    // Both timestamps should be in UTC for proper comparison
                    if (kpiDocument.CreatedAt > predictionMetadata.CreatedAt)
                    {
                        if (verbose)
                        {
                            _console.MarkupLine($"[yellow]KPI document '{contextDocumentName}' updated after prediction was created[/]");
                            _console.MarkupLine($"  [dim]Prediction created:[/] {predictionMetadata.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                            _console.MarkupLine($"  [dim]KPI document created:[/] {kpiDocument.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                        return true; // Prediction is outdated
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
            
            return false; // No KPI documents are newer than the prediction
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                _console.MarkupLine($"[yellow]Warning: Could not check if prediction is outdated: {ex.Message}[/]");
            }
            return isBundesliga;
        }
    }

    private async Task<bool> CheckBundesligaBonusPredictionMetadataOutdated(
        IDocumentPublicationRepository publicationRepository,
        BonusQuestion question,
        BonusPrediction prediction,
        BonusPredictionMetadata predictionMetadata,
        string communityContext,
        bool verbose)
    {
        if (!BonusPredictionContentEquality.Equals(prediction, predictionMetadata.BonusPrediction))
        {
            if (verbose)
            {
                _console.MarkupLine("[yellow]Stored Bundesliga bonus prediction does not match its immutable provenance metadata[/]");
            }

            return true;
        }

        try
        {
            return await BundesligaBonusPredictionOutdatedChecker.IsOutdatedAsync(
                publicationRepository,
                question,
                communityContext,
                predictionMetadata);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            if (verbose)
            {
                _console.MarkupLine($"[yellow]Stored Bundesliga bonus provenance is invalid: {Markup.Escape(ex.Message)}[/]");
            }

            return true;
        }
    }
    
    private static bool CompareBonusPredictions(BonusPrediction? databasePrediction, BonusPrediction? kicktippPrediction)
    {
        // Both null - match
        if (databasePrediction == null && kicktippPrediction == null)
        {
            return true;
        }
        
        // One null, other not - mismatch
        if (databasePrediction == null || kicktippPrediction == null)
        {
            return false;
        }
        
        // Both have values - compare selected option IDs
        var databaseOptions = databasePrediction.SelectedOptionIds.OrderBy(x => x).ToList();
        var kicktippOptions = kicktippPrediction.SelectedOptionIds.OrderBy(x => x).ToList();
        
        return databaseOptions.SequenceEqual(kicktippOptions);
    }

    private sealed record ResolvedVerificationPrediction(
        BonusPrediction? Prediction,
        bool IsOutdated);
}
