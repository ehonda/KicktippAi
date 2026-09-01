using EHonda.KicktippAi.Core;

namespace OpenAiIntegration;

public partial class PredictionService
{
    private readonly IObservedInstructionsTemplateProvider? _observedTemplateProvider;
    private readonly PredictionModelConfig? _observedModelConfig;

    public PredictionService(
        OpenAI.Responses.ResponsesClient responsesClient,
        Microsoft.Extensions.Logging.ILogger<PredictionService> logger,
        ICostCalculationService costCalculationService,
        ITokenUsageTracker tokenUsageTracker,
        IInstructionsTemplateProvider templateProvider,
        IObservedInstructionsTemplateProvider observedTemplateProvider,
        PredictionModelConfig modelConfig,
        PredictionServiceOptions? options = null)
        : this(responsesClient, logger, costCalculationService, tokenUsageTracker, templateProvider,
            modelConfig?.Model ?? throw new ArgumentNullException(nameof(modelConfig)), options)
    {
        _observedTemplateProvider = observedTemplateProvider
            ?? throw new ArgumentNullException(nameof(observedTemplateProvider));
        _observedModelConfig = modelConfig;
        RequireObservedRuntimeBinding(modelConfig);
    }

    public async Task<ObservedMatchPredictionResult> PredictObservedMatchAsync(
        Match match,
        IEnumerable<DocumentContext> contextDocuments,
        PredictionPromptExecutionRequirement requirement,
        bool includeJustification = false,
        PredictionTelemetryMetadata? telemetryMetadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(contextDocuments);
        ValidateObservedRequirement(requirement);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await _observedTemplateProvider!.LoadObservedMatchTemplateAsync(
                requirement, includeJustification, cancellationToken);
            requirement.RequireResolved(resolved);
            cancellationToken.ThrowIfCancellationRequested();
            var documents = contextDocuments.ToArray();
            var instructions = PredictionPromptComposer.BuildSystemPrompt(
                resolved.Template, documents, includeJustification);
            var messages = new List<PredictionRequestMessage>
            {
                new("system", instructions),
                new("user", PredictionPromptComposer.CreateMatchJson(match))
            };
            var completion = await CompleteMatchResponseAsync(messages, includeJustification, cancellationToken);
            var prediction = ParsePrediction(completion.PredictionJson);
            var evidence = CreateObservedEvidence(requirement, resolved, completion);
            SetLangfuseGenerationAttributes(null, messages, completion.PredictionJson,
                completion.Usage, telemetryMetadata, completion.ExecutionTelemetry);
            return ObservedMatchPredictionResult.Create(prediction, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ObservedPredictionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ObservedPredictionException("Observed match prediction failed atomically.", exception);
        }
    }

    public async Task<ObservedBonusPredictionResult> PredictObservedBonusQuestionAsync(
        BonusQuestion question,
        IEnumerable<DocumentContext> contextDocuments,
        PredictionPromptExecutionRequirement requirement,
        PredictionTelemetryMetadata? telemetryMetadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(contextDocuments);
        ValidateObservedRequirement(requirement);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await _observedTemplateProvider!.LoadObservedBonusTemplateAsync(
                requirement, cancellationToken);
            requirement.RequireResolved(resolved);
            cancellationToken.ThrowIfCancellationRequested();
            var documents = contextDocuments.ToArray();
            var instructions = PredictionPromptComposer.BuildSystemPrompt(resolved.Template, documents);
            var messages = new List<PredictionRequestMessage>
            {
                new("system", instructions),
                new("user", PredictionPromptComposer.CreateBonusQuestionJson(question))
            };
            var completion = await CompleteBonusResponseAsync(messages, question, cancellationToken);
            var prediction = ParseSingleBonusPrediction(completion.PredictionJson, question)
                ?? throw new ObservedPredictionException("Observed bonus response was not a valid exact selection.");
            var evidence = CreateObservedEvidence(requirement, resolved, completion);
            SetLangfuseGenerationAttributes(null, messages, completion.PredictionJson,
                completion.Usage, telemetryMetadata, completion.ExecutionTelemetry);
            return ObservedBonusPredictionResult.Create(prediction, evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ObservedPredictionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ObservedPredictionException("Observed bonus prediction failed atomically.", exception);
        }
    }

    private void ValidateObservedRequirement(PredictionPromptExecutionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (_observedTemplateProvider is null || _observedModelConfig is null
            || requirement.ModelConfig != _observedModelConfig)
            throw new ObservedPredictionException("Observed service is not bound to this exact execution requirement.");
    }

    private void RequireObservedRuntimeBinding(PredictionModelConfig modelConfig)
    {
        if (!modelConfig.HasPinnedRuntimeIdentity || modelConfig.ReasoningEffort is null
            || modelConfig.MaxOutputTokenCount is null
            || !string.Equals(_model, modelConfig.Model, StringComparison.Ordinal)
            || !string.Equals(_options.ReasoningEffort, modelConfig.ReasoningEffort, StringComparison.Ordinal)
            || _options.MaxOutputTokenCount != modelConfig.MaxOutputTokenCount)
            throw new InvalidDataException("Observed service runtime does not match its exact model configuration.");
    }

    private ObservedPredictionCallEvidence CreateObservedEvidence(
        PredictionPromptExecutionRequirement requirement,
        ResolvedPredictionPromptTemplate prompt,
        OpenAiResponseResult completion)
    {
        if (string.IsNullOrWhiteSpace(completion.FinalServiceTier))
            throw new ObservedPredictionException("OpenAI response omitted the final service tier.");

        var finalTier = completion.FinalServiceTier;
        var fallback = completion.ExecutionTelemetry?.FallbackUsed == true;
        var requestedTier = _options.DisableFlexProcessing ? DefaultServiceTier : FlexServiceTier;
        var service = PredictionServiceTierProvenanceV2.Create(
            requestedTier, finalTier, fallback,
            fallback ? "flex-resource-unavailable-standard-fallback" : null);
        var cost = _costCalculationService.CalculateCost(_model, completion.Usage, finalTier)
            ?? throw new ObservedPredictionException("Exact prediction cost is unavailable.");
        var usage = new PredictionGenerationUsageV2(
            completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount, cost);
        return ObservedPredictionCallEvidence.Create(requirement.ModelConfig, prompt, service, usage);
    }
}
