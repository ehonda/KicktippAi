using EHonda.KicktippAi.Core;
using System.Diagnostics;
using System.Text.Json;

namespace OpenAiIntegration;

public partial class PredictionService
{
    private const string ObservedPredictionFailedDescription = "observed-prediction-failed";
    private const string ObservedPredictionCancelledDescription = "observed-prediction-cancelled";

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
        using var activity = Telemetry.Source.StartActivity("predict-match");
        telemetryMetadata?.ApplyToObservation(activity);
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
            var result = ObservedMatchPredictionResult.Create(prediction, evidence);
            CompleteObservedActivity(activity, messages, completion.PredictionJson, evidence);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionCancelledDescription);
            throw;
        }
        catch (ObservedPredictionException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionFailedDescription);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionFailedDescription);
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
        using var activity = Telemetry.Source.StartActivity("predict-bonus");
        telemetryMetadata?.ApplyToObservation(activity);
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
            var result = ObservedBonusPredictionResult.Create(prediction, evidence);
            CompleteObservedActivity(activity, messages, completion.PredictionJson, evidence);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionCancelledDescription);
            throw;
        }
        catch (ObservedPredictionException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionFailedDescription);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ObservedPredictionFailedDescription);
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
        return ObservedPredictionCallEvidence.Create(requirement, prompt, service, usage);
    }

    private static void CompleteObservedActivity(
        Activity? activity,
        IReadOnlyList<PredictionRequestMessage> messages,
        string responseJson,
        ObservedPredictionCallEvidence evidence)
    {
        if (activity is null)
            return;

        var requirement = evidence.PromptRequirement;
        var prompt = evidence.Prompt.Provenance;
        var model = requirement.ModelConfig;
        activity.SetTag("langfuse.observation.type", "generation");
        activity.SetTag("gen_ai.request.model", model.Model);
        activity.SetTag("gen_ai.request.max_tokens", model.MaxOutputTokenCount);
        activity.SetTag("gen_ai.request.reasoning_effort", model.ReasoningEffort);
        activity.SetTag("langfuse.observation.metadata.openaiModel", model.Model);
        activity.SetTag("langfuse.observation.metadata.openaiMaxOutputTokens", model.MaxOutputTokenCount);
        activity.SetTag("langfuse.observation.metadata.openaiReasoningEffort", model.ReasoningEffort);
        activity.SetTag("langfuse.observation.prompt.name", requirement.HostedName);
        activity.SetTag("langfuse.observation.prompt.version", requirement.HostedVersion);
        activity.SetTag("langfuse.observation.metadata.langfusePromptLabel", requirement.RequiredLabel);
        activity.SetTag("langfuse.observation.metadata.promptContentSha256",
            requirement.HostedNormalizedReadbackSha256);
        activity.SetTag("langfuse.observation.metadata.promptActualSource", prompt.ActualSource.ToString());
        activity.SetTag("langfuse.observation.metadata.langfusePromptFallback",
            prompt.ActualSource == PredictionPromptSourceV2.CheckedInFallback);
        activity.SetTag("langfuse.observation.metadata.promptFallbackPath", prompt.ActualFallbackFile);
        activity.SetTag("langfuse.observation.metadata.promptFallbackSha256", prompt.ActualFallbackSha256);
        activity.SetTag("gen_ai.request.service_tier", evidence.ServiceTier.RequestedTier);
        activity.SetTag("gen_ai.response.service_tier", evidence.ServiceTier.FinalTier);
        activity.SetTag("langfuse.observation.metadata.openaiRequestedServiceTier",
            evidence.ServiceTier.RequestedTier);
        activity.SetTag("langfuse.observation.metadata.openaiFinalServiceTier",
            evidence.ServiceTier.FinalTier);
        activity.SetTag("langfuse.observation.metadata.openaiExecutionStrategy",
            string.Equals(evidence.ServiceTier.RequestedTier, FlexServiceTier, StringComparison.Ordinal)
                ? "flex-first-standard-fallback"
                : "standard-only");
        activity.SetTag("langfuse.observation.metadata.openaiServiceTierFallbackUsed",
            evidence.ServiceTier.FallbackOccurred.ToString());
        activity.SetTag("langfuse.observation.metadata.openaiServiceTierFallbackReason",
            evidence.ServiceTier.FallbackReason);

        var input = messages.Select(message => new { role = message.Role, content = message.Content });
        activity.SetTag("langfuse.observation.input", JsonSerializer.Serialize(input));
        activity.SetTag("langfuse.observation.output", responseJson);
        activity.SetTag("langfuse.observation.usage_details", JsonSerializer.Serialize(new
        {
            input = evidence.Usage.InputTokens,
            output = evidence.Usage.OutputTokens,
            total = checked(evidence.Usage.InputTokens + evidence.Usage.OutputTokens)
        }));
        activity.SetTag("langfuse.observation.cost_details", JsonSerializer.Serialize(new
        {
            total = evidence.Usage.CostUsd
        }));
        activity.SetStatus(ActivityStatusCode.Ok);
    }
}
