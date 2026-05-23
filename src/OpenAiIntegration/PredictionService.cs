using System.Collections.Generic;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Responses;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace OpenAiIntegration;

/// <summary>
/// Service for predicting match outcomes using OpenAI models
/// </summary>
public class PredictionService : IPredictionService
{
    private const int TransientOpenAiMaxRetryAttempts = 3;
    private const int RateLimitedOpenAiMaxRetryAttempts = 8;
    private const string FlexServiceTier = "flex";
    private const string DefaultServiceTier = "default";
    private static readonly TimeSpan TransientOpenAiRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RateLimitedOpenAiRetryBaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RateLimitedOpenAiRetryMaxDelay = TimeSpan.FromMinutes(2);

    private readonly ResponsesClient _responsesClient;
    private readonly ILogger<PredictionService> _logger;
    private readonly ICostCalculationService _costCalculationService;
    private readonly ITokenUsageTracker _tokenUsageTracker;
    private readonly IInstructionsTemplateProvider _templateProvider;
    private readonly PredictionServiceOptions _options;
    private readonly string _model;
    private readonly Lazy<(string Template, string Path)> _instructionsTemplate;
    private readonly Lazy<(string Template, string Path)> _instructionsTemplateWithJustification;
    private readonly Lazy<(string Template, string Path)> _bonusInstructionsTemplate;

    public PredictionService(
        ResponsesClient responsesClient,
        ILogger<PredictionService> logger,
        ICostCalculationService costCalculationService,
        ITokenUsageTracker tokenUsageTracker,
        IInstructionsTemplateProvider templateProvider,
        string model,
        PredictionServiceOptions? options = null)
    {
        _responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _costCalculationService = costCalculationService ?? throw new ArgumentNullException(nameof(costCalculationService));
        _tokenUsageTracker = tokenUsageTracker ?? throw new ArgumentNullException(nameof(tokenUsageTracker));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        _options = options ?? PredictionServiceOptions.Default;
        _model = model ?? throw new ArgumentNullException(nameof(model));

        _instructionsTemplate = new Lazy<(string Template, string Path)>(
            () => _templateProvider.LoadMatchTemplate(_model, includeJustification: false));
        _instructionsTemplateWithJustification = new Lazy<(string Template, string Path)>(
            () => _templateProvider.LoadMatchTemplate(_model, includeJustification: true));
        _bonusInstructionsTemplate = new Lazy<(string Template, string Path)>(
            () => _templateProvider.LoadBonusTemplate(_model));
    }

    public async Task<Prediction?> PredictMatchAsync(
        EHonda.KicktippAi.Core.Match match,
        IEnumerable<DocumentContext> contextDocuments, 
        bool includeJustification = false,
        PredictionTelemetryMetadata? telemetryMetadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating prediction for match: {HomeTeam} vs {AwayTeam} at {StartTime}", 
            match.HomeTeam, match.AwayTeam, match.StartsAt);

        try
        {
            // Build the instructions by combining template with context
            var instructions = BuildInstructions(contextDocuments, includeJustification);
            
            // Create match JSON
            var matchJson = PredictionPromptComposer.CreateMatchJson(match);
            
            _logger.LogDebug("Instructions length: {InstructionsLength} characters", instructions.Length);
            _logger.LogDebug("Context documents: {ContextCount}", contextDocuments.Count());
            _logger.LogDebug("Match JSON: {MatchJson}", matchJson);

            // Create input items for the response
            var messages = new List<PredictionRequestMessage>
            {
                new("system", instructions),
                new("user", matchJson)
            };

            _logger.LogDebug("Calling OpenAI API for prediction");

            // Start an OTel activity for Langfuse generation tracking
            using var activity = Telemetry.Source.StartActivity("predict-match");

            // Call OpenAI with structured output format
            var completion = await CompleteMatchResponseAsync(messages, includeJustification, cancellationToken);

            // Parse the structured response
            var predictionJson = completion.PredictionJson;
            _logger.LogDebug("Received prediction JSON: {PredictionJson}", predictionJson);

            var prediction = ParsePrediction(predictionJson);
            
            _logger.LogInformation("Prediction generated: {HomeGoals}-{AwayGoals} for {HomeTeam} vs {AwayTeam}", 
                prediction.HomeGoals, prediction.AwayGoals, match.HomeTeam, match.AwayTeam);

            // Log token usage and cost breakdown
            var usage = completion.Usage;
            _logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

            // Set Langfuse generation attributes on the activity
            SetLangfuseGenerationAttributes(activity, messages, predictionJson, usage, telemetryMetadata, completion.ExecutionTelemetry);

            // Add usage to tracker
            if (completion.ExecutionTelemetry is null)
            {
                _tokenUsageTracker.AddUsage(_model, usage);
            }
            else
            {
                _tokenUsageTracker.AddUsage(_model, usage, completion.ExecutionTelemetry.FinalServiceTier);
            }

            // Calculate and log costs
            if (completion.ExecutionTelemetry is null)
            {
                _costCalculationService.LogCostBreakdown(_model, usage);
            }
            else
            {
                _costCalculationService.LogCostBreakdown(_model, usage, completion.ExecutionTelemetry.FinalServiceTier);
            }

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prediction for match: {HomeTeam} vs {AwayTeam}", 
                match.HomeTeam, match.AwayTeam);
            Console.Error.WriteLine($"Prediction error for {match.HomeTeam} vs {match.AwayTeam}: {ex.Message}");
            
            return null;
        }
    }

    private Task<OpenAiResponseResult> CompleteMatchResponseAsync(
        IReadOnlyList<PredictionRequestMessage> messages,
        bool includeJustification,
        CancellationToken cancellationToken)
    {
        return CompleteStructuredResponseAsync(
            completeResponseAsync: (serviceTier, ct) => CompleteResponseAsync(
                CreateMatchResponseOptions(
                    messages,
                    includeJustification,
                    serviceTier,
                    _options.ReasoningEffort),
                serviceTier,
                ct),
            cancellationToken);
    }

    private Task<OpenAiResponseResult> CompleteBonusResponseAsync(
        IReadOnlyList<PredictionRequestMessage> messages,
        BonusQuestion bonusQuestion,
        CancellationToken cancellationToken)
    {
        return CompleteStructuredResponseAsync(
            completeResponseAsync: (serviceTier, ct) => CompleteResponseAsync(
                CreateBonusResponseOptions(
                    messages,
                    bonusQuestion,
                    serviceTier,
                    _options.ReasoningEffort),
                serviceTier,
                ct),
            cancellationToken);
    }

    private async Task<OpenAiResponseResult> CompleteStructuredResponseAsync(
        Func<string?, CancellationToken, Task<OpenAiResponseResult>> completeResponseAsync,
        CancellationToken cancellationToken)
    {
        if (_options.DisableFlexProcessing)
        {
            return await completeResponseAsync(null, cancellationToken);
        }

        string? requestedServiceTier = FlexServiceTier;
        var usedFallback = false;
        var pipeline = new ResiliencePipelineBuilder<OpenAiResponseResult>()
            // OpenAI documents that Flex processing can return 429 Resource Unavailable
            // when resources are insufficient, and recommends retrying with standard
            // processing when occasional higher cost is acceptable:
            // https://developers.openai.com/api/docs/guides/flex-processing
            // The Responses API reference documents service_tier=default as standard
            // pricing/performance, so flex 429 retries switch to that tier:
            // https://platform.openai.com/docs/api-reference/responses/create
            .AddRetry(new RetryStrategyOptions<OpenAiResponseResult>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = args => ValueTask.FromResult(
                    IsFlexProcessingRequest(requestedServiceTier) &&
                    IsFlexFallbackFailure(args.Outcome.Exception, args.Context.CancellationToken)),
                OnRetry = args =>
                {
                    usedFallback = true;
                    requestedServiceTier = DefaultServiceTier;
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "OpenAI flex processing failed with a retryable failure; retrying prediction with default processing.");
                    return default;
                }
            })
            .Build();

        var result = await pipeline.ExecuteAsync(
            async ct =>
            {
                var completion = await completeResponseAsync(requestedServiceTier, ct);

                var finalServiceTier = string.IsNullOrWhiteSpace(completion.FinalServiceTier)
                    ? requestedServiceTier ?? "standard"
                    : completion.FinalServiceTier;

                return completion with
                {
                    ExecutionTelemetry = new PredictionExecutionTelemetry(
                        "flex-first-standard-fallback",
                        usedFallback ? DefaultServiceTier : FlexServiceTier,
                        finalServiceTier,
                        usedFallback)
                };
            },
            cancellationToken);

        return result;
    }

    private async Task<OpenAiResponseResult> CompleteResponseAsync(
        CreateResponseOptions options,
        string? serviceTier,
        CancellationToken cancellationToken)
    {
        var response = await CreateResponseWithTransientRetryAsync(options, serviceTier, cancellationToken);
        var responseResult = response.Value;
        var predictionJson = responseResult.GetOutputText();
        if (predictionJson is null)
        {
            throw new InvalidOperationException("OpenAI response did not contain output text.");
        }

        var usage = responseResult.Usage is null
                    ? null
                    : ToChatTokenUsage(responseResult.Usage);
        if (usage is null)
        {
            throw new InvalidOperationException("OpenAI response did not contain token usage.");
        }

        return new OpenAiResponseResult(
            predictionJson,
            usage,
            null,
            NormalizeResponseServiceTier(responseResult.ServiceTier));
    }

    private async Task<ClientResult<ResponseResult>> CreateResponseWithTransientRetryAsync(
        CreateResponseOptions options,
        string? requestedServiceTier,
        CancellationToken cancellationToken)
    {
        var pipeline = new ResiliencePipelineBuilder<ClientResult<ResponseResult>>()
            // OpenAI documents 429 rate-limit errors as pacing problems and recommends bounded
            // random exponential backoff. It also documents x-ratelimit-* response headers
            // for reset timing:
            // https://platform.openai.com/docs/guides/rate-limits
            // https://platform.openai.com/docs/guides/error-codes
            // https://platform.openai.com/docs/api-reference
            // These references document x-ratelimit-* reset headers, not Retry-After.
            .AddRetry(new RetryStrategyOptions<ClientResult<ResponseResult>>
            {
                MaxRetryAttempts = RateLimitedOpenAiMaxRetryAttempts,
                DelayGenerator = args => new ValueTask<TimeSpan?>(
                    ResolveOpenAiRateLimitDelay(args.Outcome.Exception, args.AttemptNumber)),
                ShouldHandle = args => ValueTask.FromResult(
                    !IsFlexProcessingRequest(requestedServiceTier) &&
                    IsRetryableOpenAiRateLimitFailure(args.Outcome.Exception, args.Context.CancellationToken)),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "OpenAI request hit a rate limit; retrying prediction request ({RetryAttempt}/{MaxRetryAttempts}) after {RetryDelay}.",
                        args.AttemptNumber + 1,
                        RateLimitedOpenAiMaxRetryAttempts,
                        args.RetryDelay);
                    return default;
                }
            })
            .AddRetry(new RetryStrategyOptions<ClientResult<ResponseResult>>
            {
                MaxRetryAttempts = TransientOpenAiMaxRetryAttempts,
                Delay = TransientOpenAiRetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    IsTransientOpenAiServerFailure(args.Outcome.Exception, args.Context.CancellationToken)),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "OpenAI request failed with a transient server error; retrying prediction request ({RetryAttempt}/{MaxRetryAttempts}).",
                        args.AttemptNumber + 1,
                        TransientOpenAiMaxRetryAttempts);
                    return default;
                }
            })
            .Build();

        return await pipeline.ExecuteAsync(
            async ct => await _responsesClient.CreateResponseAsync(options, ct),
            cancellationToken);
    }

    private CreateResponseOptions CreateMatchResponseOptions(
        IReadOnlyList<PredictionRequestMessage> messages,
        bool includeJustification,
        string? serviceTier,
        string? reasoningEffort)
    {
        return CreateResponseOptions(
            messages,
            "match_prediction",
            BinaryData.FromBytes(BuildPredictionJsonSchema(includeJustification)),
            serviceTier,
            reasoningEffort);
    }

    private CreateResponseOptions CreateBonusResponseOptions(
        IReadOnlyList<PredictionRequestMessage> messages,
        BonusQuestion bonusQuestion,
        string? serviceTier,
        string? reasoningEffort)
    {
        return CreateResponseOptions(
            messages,
            "bonus_prediction",
            BinaryData.FromBytes(CreateSingleBonusPredictionJsonSchema(bonusQuestion)),
            serviceTier,
            reasoningEffort);
    }

    private CreateResponseOptions CreateResponseOptions(
        IReadOnlyList<PredictionRequestMessage> messages,
        string schemaName,
        BinaryData schema,
        string? serviceTier,
        string? reasoningEffort)
    {
        var options = new CreateResponseOptions
        {
            Model = _model,
            MaxOutputTokenCount = _options.MaxOutputTokenCount, // Safeguard against high costs
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: schemaName,
                    jsonSchema: schema,
                    jsonSchemaIsStrict: true)
            }
        };

        foreach (var message in messages)
        {
            options.InputItems.Add(CreateResponseMessage(message));
        }

        var normalizedServiceTier = NormalizeServiceTier(serviceTier);
        if (normalizedServiceTier is not null)
        {
            options.ServiceTier = new ResponseServiceTier(normalizedServiceTier);
        }

        var normalizedReasoningEffort = NormalizeReasoningEffort(reasoningEffort);
        if (normalizedReasoningEffort is not null)
        {
            options.ReasoningOptions = new ResponseReasoningOptions
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(normalizedReasoningEffort)
            };
        }

        return options;
    }

    private static string? NormalizeReasoningEffort(string? reasoningEffort)
    {
        return string.IsNullOrWhiteSpace(reasoningEffort)
            ? null
            : reasoningEffort.Trim().ToLowerInvariant();
    }

    private static string? NormalizeServiceTier(string? serviceTier)
    {
        return string.IsNullOrWhiteSpace(serviceTier)
            ? null
            : serviceTier.Trim().ToLowerInvariant();
    }

    private static string? NormalizeResponseServiceTier(ResponseServiceTier? serviceTier)
    {
        return string.IsNullOrWhiteSpace(serviceTier?.ToString())
            ? null
            : serviceTier.Value.ToString().Trim().ToLowerInvariant();
    }

    private static ResponseItem CreateResponseMessage(PredictionRequestMessage message)
    {
        return message.Role switch
        {
            "system" => ResponseItem.CreateSystemMessageItem(message.Content),
            "user" => ResponseItem.CreateUserMessageItem(message.Content),
            _ => throw new InvalidOperationException($"Unsupported response message role '{message.Role}'.")
        };
    }

    private static bool IsFlexProcessingRequest(string? requestedServiceTier)
    {
        return string.Equals(
            NormalizeServiceTier(requestedServiceTier),
            FlexServiceTier,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFlexFallbackFailure(Exception? exception, CancellationToken cancellationToken)
    {
        if (exception is null)
        {
            return false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            ClientResultException { Status: 408 } => true,
            ClientResultException { Status: 429 } clientException => IsFlexResourceUnavailableFailure(clientException)
                || IsRetryableOpenAiRateLimitFailure(clientException, cancellationToken),
            TimeoutRejectedException => true,
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false
        };
    }

    private static bool IsTransientOpenAiServerFailure(Exception? exception, CancellationToken cancellationToken)
    {
        if (exception is null || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is ClientResultException { Status: >= 500 and <= 599 };
    }

    private static bool IsRetryableOpenAiRateLimitFailure(Exception? exception, CancellationToken cancellationToken)
    {
        if (exception is not ClientResultException { Status: 429 } clientException ||
            cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return !IsFlexResourceUnavailableFailure(clientException)
               && !ContainsQuotaExhaustedMarker(clientException.Message)
               && !ContainsQuotaExhaustedMarker(clientException.GetRawResponse()?.ReasonPhrase)
               && !ContainsQuotaExhaustedMarker(clientException.GetRawResponse()?.Content.ToString());
    }

    private static TimeSpan ResolveOpenAiRateLimitDelay(Exception? exception, int attemptNumber)
    {
        if (exception is ClientResultException clientException &&
            TryGetOpenAiRateLimitResetDelay(clientException.GetRawResponse(), out var resetDelay))
        {
            return ClampOpenAiRateLimitDelay(resetDelay);
        }

        var cappedExponentialMilliseconds = Math.Min(
            RateLimitedOpenAiRetryMaxDelay.TotalMilliseconds,
            RateLimitedOpenAiRetryBaseDelay.TotalMilliseconds * Math.Pow(2, Math.Max(0, attemptNumber)));
        var jitterFloorMilliseconds = cappedExponentialMilliseconds / 2;
        var jitteredMilliseconds = jitterFloorMilliseconds + Random.Shared.NextDouble() * jitterFloorMilliseconds;

        return ClampOpenAiRateLimitDelay(TimeSpan.FromMilliseconds(jitteredMilliseconds));
    }

    private static bool TryGetOpenAiRateLimitResetDelay(PipelineResponse? response, out TimeSpan delay)
    {
        delay = default;
        if (response is null)
        {
            return false;
        }

        var exhaustedDelays = new List<TimeSpan>();
        var availableDelays = new List<TimeSpan>();

        AddRateLimitResetDelay(response, "requests", exhaustedDelays, availableDelays);
        AddRateLimitResetDelay(response, "tokens", exhaustedDelays, availableDelays);

        if (exhaustedDelays.Count > 0)
        {
            delay = exhaustedDelays.Max();
            return true;
        }

        if (availableDelays.Count > 0)
        {
            delay = availableDelays.Max();
            return true;
        }

        return false;
    }

    private static void AddRateLimitResetDelay(
        PipelineResponse response,
        string dimension,
        List<TimeSpan> exhaustedDelays,
        List<TimeSpan> availableDelays)
    {
        if (!TryGetOpenAiRateLimitHeader(response, $"x-ratelimit-reset-{dimension}", out var resetText) ||
            !TryParseOpenAiRateLimitReset(resetText, out var resetDelay))
        {
            return;
        }

        if (TryGetOpenAiRateLimitHeader(response, $"x-ratelimit-remaining-{dimension}", out var remainingText) &&
            decimal.TryParse(remainingText, NumberStyles.Number, CultureInfo.InvariantCulture, out var remaining) &&
            remaining <= 0)
        {
            exhaustedDelays.Add(resetDelay);
            return;
        }

        availableDelays.Add(resetDelay);
    }

    private static bool TryGetOpenAiRateLimitHeader(PipelineResponse response, string name, out string value)
    {
        if (response.Headers is not null && response.Headers.TryGetValue(name, out var headerValue))
        {
            value = headerValue ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseOpenAiRateLimitReset(string text, out TimeSpan delay)
    {
        delay = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var matches = Regex.Matches(
            text.Trim(),
            @"(?<value>\d+(?:\.\d+)?)(?<unit>ms|s|m|h)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (matches.Count == 0)
        {
            return false;
        }

        var totalMilliseconds = 0.0;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            totalMilliseconds += match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "ms" => value,
                "s" => value * 1_000,
                "m" => value * 60_000,
                "h" => value * 3_600_000,
                _ => 0
            };
        }

        delay = TimeSpan.FromMilliseconds(Math.Max(0, totalMilliseconds));
        return true;
    }

    private static TimeSpan ClampOpenAiRateLimitDelay(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > RateLimitedOpenAiRetryMaxDelay
            ? RateLimitedOpenAiRetryMaxDelay
            : delay;
    }

    private static bool IsFlexResourceUnavailableFailure(ClientResultException exception)
    {
        var rawResponse = exception.GetRawResponse();
        return ContainsFlexResourceUnavailableMarker(exception.Message)
               || ContainsFlexResourceUnavailableMarker(rawResponse?.ReasonPhrase)
               || ContainsFlexResourceUnavailableMarker(rawResponse?.Content.ToString());
    }

    private static bool ContainsFlexResourceUnavailableMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("resource_unavailable", StringComparison.OrdinalIgnoreCase)
               || text.Contains("resource unavailable", StringComparison.OrdinalIgnoreCase)
               || text.Contains("resources unavailable", StringComparison.OrdinalIgnoreCase)
               || text.Contains("insufficient resources", StringComparison.OrdinalIgnoreCase)
               || text.Contains("capacity", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsQuotaExhaustedMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)
               || text.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase)
               || text.Contains("check your plan and billing", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BonusPrediction?> PredictBonusQuestionAsync(
        BonusQuestion bonusQuestion,
        IEnumerable<DocumentContext> contextDocuments,
        PredictionTelemetryMetadata? telemetryMetadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating prediction for bonus question: {QuestionText}", bonusQuestion.Text);

        try
        {
            // Build the instructions by combining template with context
            var instructions = BuildBonusInstructions(contextDocuments);
            
            // Create bonus question JSON
            var questionJson = PredictionPromptComposer.CreateBonusQuestionJson(bonusQuestion);
            
            _logger.LogDebug("Instructions length: {InstructionsLength} characters", instructions.Length);
            _logger.LogDebug("Context documents: {ContextCount}", contextDocuments.Count());
            _logger.LogDebug("Question JSON: {QuestionJson}", questionJson);

            // Create input items for the response
            var messages = new List<PredictionRequestMessage>
            {
                new("system", instructions),
                new("user", questionJson)
            };

            _logger.LogDebug("Calling OpenAI API for bonus prediction");

            // Start an OTel activity for Langfuse generation tracking
            using var activity = Telemetry.Source.StartActivity("predict-bonus");

            // Call OpenAI with structured output format
            var completion = await CompleteBonusResponseAsync(messages, bonusQuestion, cancellationToken);

            // Parse the structured response
            var predictionJson = completion.PredictionJson;
            _logger.LogDebug("Received bonus prediction JSON: {PredictionJson}", predictionJson);

            var prediction = ParseSingleBonusPrediction(predictionJson, bonusQuestion);
            
            if (prediction != null)
            {
                _logger.LogInformation("Generated prediction for bonus question: {SelectedOptions}", 
                    string.Join(", ", prediction.SelectedOptionIds));
            }

            // Log token usage and cost breakdown
            var usage = completion.Usage;
            _logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

            // Set Langfuse generation attributes on the activity
            SetLangfuseGenerationAttributes(activity, messages, predictionJson, usage, telemetryMetadata, completion.ExecutionTelemetry);

            // Add usage to tracker
            if (completion.ExecutionTelemetry is null)
            {
                _tokenUsageTracker.AddUsage(_model, usage);
            }
            else
            {
                _tokenUsageTracker.AddUsage(_model, usage, completion.ExecutionTelemetry.FinalServiceTier);
            }

            // Calculate and log costs
            if (completion.ExecutionTelemetry is null)
            {
                _costCalculationService.LogCostBreakdown(_model, usage);
            }
            else
            {
                _costCalculationService.LogCostBreakdown(_model, usage, completion.ExecutionTelemetry.FinalServiceTier);
            }

            return prediction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating bonus prediction for question: {QuestionText}", bonusQuestion.Text);
            return null;
        }
    }

    private string BuildInstructions(IEnumerable<DocumentContext> contextDocuments, bool includeJustification)
    {
        var template = includeJustification
            ? _instructionsTemplateWithJustification.Value.Template
            : _instructionsTemplate.Value.Template;

        var contextList = contextDocuments.ToList();
        if (contextList.Any())
        {
            _logger.LogDebug("Added {ContextCount} context documents to instructions", contextList.Count);
        }
        else
        {
            _logger.LogDebug("No context documents provided");
        }

        return PredictionPromptComposer.BuildSystemPrompt(template, contextList);
    }

    private static byte[] BuildPredictionJsonSchema(bool includeJustification)
    {
        var properties = new Dictionary<string, object?>
        {
            ["home"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "Predicted goals for the home team"
            },
            ["away"] = new Dictionary<string, object?>
            {
                ["type"] = "integer",
                ["description"] = "Predicted goals for the away team"
            }
        };

        var required = new List<string> { "home", "away" };

        if (includeJustification)
        {
            var mostValuableContextSourceItem = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["documentName"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the context document referenced"
                    },
                    ["details"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Brief summary of why the document or parts of it were useful"
                    }
                },
                ["required"] = new[] { "documentName", "details" },
                ["additionalProperties"] = false
            };

            var leastValuableContextSourceItem = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["documentName"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the context document referenced"
                    },
                    ["details"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Brief summary explaining why the document or parts of it offered limited insight"
                    }
                },
                ["required"] = new[] { "documentName", "details" },
                ["additionalProperties"] = false
            };

            var contextSources = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["mostValuable"] = new Dictionary<string, object?>
                    {
                        ["type"] = "array",
                        ["items"] = mostValuableContextSourceItem,
                        ["description"] = "Context documents that most influenced the prediction",
                        ["minItems"] = 0
                    },
                    ["leastValuable"] = new Dictionary<string, object?>
                    {
                        ["type"] = "array",
                        ["items"] = leastValuableContextSourceItem,
                        ["description"] = "Context documents that provided limited or no valuable insight",
                        ["minItems"] = 0
                    }
                },
                ["required"] = new[] { "leastValuable", "mostValuable" },
                ["additionalProperties"] = false
            };

            properties["justification"] = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["keyReasoning"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = "Concise analytic summary motivating the predicted scoreline"
                    },
                    ["contextSources"] = contextSources,
                    ["uncertainties"] = new Dictionary<string, object?>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object?>
                        {
                            ["type"] = "string",
                            ["description"] = "Single uncertainty or external factor affecting confidence"
                        },
                        ["description"] = "Factors that could alter the predicted outcome",
                        ["minItems"] = 0
                    }
                },
                ["required"] = new[] { "contextSources", "keyReasoning", "uncertainties" },
                ["additionalProperties"] = false
            };
            required.Add("justification");
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return JsonSerializer.SerializeToUtf8Bytes(schema);
    }

    private Prediction ParsePrediction(string predictionJson)
    {
        try
        {
            _logger.LogDebug("Parsing prediction JSON: {PredictionJson}", predictionJson);
            
            var predictionResponse = JsonSerializer.Deserialize<PredictionResponse>(predictionJson);
            if (predictionResponse == null)
            {
                LogRawModelResponse(predictionJson);
                throw new InvalidOperationException("Failed to deserialize prediction response");
            }

            _logger.LogDebug("Parsed prediction response - Home: {Home}, Away: {Away}", predictionResponse.Home, predictionResponse.Away);

            PredictionJustification? justification = null;

            if (predictionResponse.Justification != null)
            {
                var justificationResponse = predictionResponse.Justification;

                var mostValuable = justificationResponse.ContextSources?.MostValuable?
                    .Where(entry => entry != null)
                    .Select(entry => new PredictionJustificationContextSource(
                        entry!.DocumentName?.Trim() ?? string.Empty,
                        entry.Details?.Trim() ?? string.Empty))
                    .ToList() ?? new List<PredictionJustificationContextSource>();

                var leastValuable = justificationResponse.ContextSources?.LeastValuable?
                    .Where(entry => entry != null)
                    .Select(entry => new PredictionJustificationContextSource(
                        entry!.DocumentName?.Trim() ?? string.Empty,
                        entry.Details?.Trim() ?? string.Empty))
                    .ToList() ?? new List<PredictionJustificationContextSource>();

                var uncertainties = justificationResponse.Uncertainties?
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList() ?? new List<string>();

                justification = new PredictionJustification(
                    justificationResponse.KeyReasoning?.Trim() ?? string.Empty,
                    new PredictionJustificationContextSources(mostValuable, leastValuable),
                    uncertainties);

                _logger.LogDebug(
                    "Parsed justification with key reasoning: {KeyReasoning}; Most valuable sources: {MostValuableCount}; Least valuable sources: {LeastValuableCount}; Uncertainties: {UncertaintiesCount}",
                    justification.KeyReasoning,
                    justification.ContextSources.MostValuable.Count,
                    justification.ContextSources.LeastValuable.Count,
                    justification.Uncertainties.Count);
            }

            return new Prediction(predictionResponse.Home, predictionResponse.Away, justification);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse prediction JSON: {PredictionJson}", predictionJson);
            LogRawModelResponse(predictionJson);
            throw new InvalidOperationException($"Failed to parse prediction response: {ex.Message}", ex);
        }
    }

    private void LogRawModelResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            const string message = "Raw model response from OpenAI was empty or whitespace.";
            _logger.LogError(message);
            Console.Error.WriteLine(message);
            return;
        }

        _logger.LogError("Raw model response from OpenAI: {RawResponse}", rawResponse);
        Console.Error.WriteLine("Raw model response from OpenAI:");
        Console.Error.WriteLine(rawResponse);
    }

    private string BuildBonusInstructions(IEnumerable<DocumentContext> contextDocuments)
    {
        // Use the pre-loaded bonus instructions template
        var bonusInstructionsTemplate = _bonusInstructionsTemplate.Value.Template;
        
        var contextList = contextDocuments.ToList();
        if (contextList.Any())
        {
            _logger.LogDebug("Added {ContextCount} context documents to bonus instructions", contextList.Count);
        }
        else
        {
            _logger.LogDebug("No context documents provided for bonus predictions");
        }

        return PredictionPromptComposer.BuildSystemPrompt(bonusInstructionsTemplate, contextList);
    }

    private static byte[] CreateSingleBonusPredictionJsonSchema(BonusQuestion question)
    {
        // For multi-selection questions, require exactly MaxSelections answers
        // For single-selection questions, require exactly 1 answer
        var requiredSelections = question.MaxSelections;
        
        var schema = new
        {
            type = "object",
            properties = new
            {
                selectedOptionIds = new
                {
                    type = "array",
                    items = new { type = "string", @enum = question.Options.Select(o => o.Id).ToArray() },
                    minItems = requiredSelections,
                    maxItems = requiredSelections
                }
            },
            required = new[] { "selectedOptionIds" },
            additionalProperties = false
        };

        return JsonSerializer.SerializeToUtf8Bytes(schema);
    }

    private BonusPrediction? ParseSingleBonusPrediction(string predictionJson, BonusQuestion question)
    {
        try
        {
            _logger.LogDebug("Parsing single bonus prediction JSON: {PredictionJson}", predictionJson);
            
            var response = JsonSerializer.Deserialize<SingleBonusPredictionResponse>(predictionJson);
            if (response?.SelectedOptionIds?.Any() != true)
            {
                throw new InvalidOperationException("Failed to deserialize bonus prediction response or no options selected");
            }

            // Validate that all selected options exist for this question
            var validOptionIds = question.Options.Select(o => o.Id).ToHashSet();
            var invalidOptions = response.SelectedOptionIds.Where(id => !validOptionIds.Contains(id)).ToArray();
            
            if (invalidOptions.Any())
            {
                _logger.LogWarning("Invalid option IDs for question '{QuestionText}': {InvalidOptions}", 
                    question.Text, string.Join(", ", invalidOptions));
                return null;
            }

            // Validate no duplicate selections
            var duplicateOptions = response.SelectedOptionIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
                
            if (duplicateOptions.Any())
            {
                _logger.LogWarning("Duplicate option IDs for question '{QuestionText}': {DuplicateOptions}", 
                    question.Text, string.Join(", ", duplicateOptions));
                return null;
            }

            // Validate selection count - must match exactly MaxSelections for full predictions
            if (response.SelectedOptionIds.Length != question.MaxSelections)
            {
                _logger.LogWarning("Invalid selection count for question '{QuestionText}': expected exactly {MaxSelections}, got {ActualCount}", 
                    question.Text, question.MaxSelections, response.SelectedOptionIds.Length);
                return null;
            }

            var prediction = new BonusPrediction(response.SelectedOptionIds.ToList());
            
            _logger.LogDebug("Parsed prediction: {SelectedOptions}", 
                string.Join(", ", response.SelectedOptionIds));

            return prediction;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse bonus prediction JSON: {PredictionJson}", predictionJson);
            return null;
        }
    }

    /// <summary>
    /// Gets the file path of the match prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the match prompt file</returns>
    public string GetMatchPromptPath(bool includeJustification = false)
    {
        return includeJustification
            ? _instructionsTemplateWithJustification.Value.Path
            : _instructionsTemplate.Value.Path;
    }

    /// <summary>
    /// Gets the file path of the bonus question prediction prompt being used by this service
    /// </summary>
    /// <returns>The absolute file path to the bonus prompt file</returns>
    public string GetBonusPromptPath() => _bonusInstructionsTemplate.Value.Path;

    /// <summary>
    /// Internal class for deserializing the structured prediction response
    /// </summary>
    private class PredictionResponse
    {
        [JsonPropertyName("home")]
        public int Home { get; set; }
        
        [JsonPropertyName("away")]
        public int Away { get; set; }

        [JsonPropertyName("justification")]
        public JustificationResponse? Justification { get; set; }
    }

    private class JustificationResponse
    {
        [JsonPropertyName("keyReasoning")]
        public string KeyReasoning { get; set; } = string.Empty;

        [JsonPropertyName("contextSources")]
        public JustificationContextSourcesResponse ContextSources { get; set; } = new();

        [JsonPropertyName("uncertainties")]
        public string[] Uncertainties { get; set; } = Array.Empty<string>();
    }

    private class JustificationContextSourcesResponse
    {
        [JsonPropertyName("mostValuable")]
        public JustificationContextSourceEntry[] MostValuable { get; set; } = Array.Empty<JustificationContextSourceEntry>();

        [JsonPropertyName("leastValuable")]
        public JustificationContextSourceEntry[] LeastValuable { get; set; } = Array.Empty<JustificationContextSourceEntry>();
    }

    private class JustificationContextSourceEntry
    {
        [JsonPropertyName("documentName")]
        public string DocumentName { get; set; } = string.Empty;

        [JsonPropertyName("details")]
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>
    /// Internal class for deserializing the bonus predictions response
    /// </summary>
    private class BonusPredictionsResponse
    {
        [JsonPropertyName("predictions")]
        public BonusPredictionEntry[]? Predictions { get; set; }
    }

    /// <summary>
    /// Internal class for deserializing individual bonus prediction entries
    /// </summary>
    private class BonusPredictionEntry
    {
        [JsonPropertyName("questionId")]
        public string QuestionId { get; set; } = string.Empty;
        
        [JsonPropertyName("selectedOptionIds")]
        public string[] SelectedOptionIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Internal class for deserializing single bonus prediction response
    /// </summary>
    private class SingleBonusPredictionResponse
    {
        [JsonPropertyName("selectedOptionIds")]
        public string[] SelectedOptionIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Sets Langfuse-mapped OpenTelemetry attributes on the given activity.
    /// If <paramref name="activity"/> is <c>null</c> (no OTel listener registered), this is a no-op.
    /// </summary>
    private void SetLangfuseGenerationAttributes(
        Activity? activity,
        IReadOnlyList<PredictionRequestMessage> messages,
        string responseJson,
        ChatTokenUsage usage,
        PredictionTelemetryMetadata? telemetryMetadata,
        PredictionExecutionTelemetry? executionTelemetry = null)
    {
        if (activity is null)
            return;

        activity.SetTag("langfuse.observation.type", "generation");
        activity.SetTag("gen_ai.request.model", _model);
        var providerPromptMetadata = (_templateProvider as IPromptTemplateTelemetryMetadataProvider)
            ?.GetPromptTemplateTelemetryMetadata();

        if (providerPromptMetadata?.LangfusePromptName is { } providerPromptName &&
            providerPromptMetadata.LangfusePromptVersion is { } providerPromptVersion)
        {
            activity.SetTag("langfuse.observation.prompt.name", providerPromptName);
            activity.SetTag("langfuse.observation.prompt.version", providerPromptVersion);
        }
        else if (_options.LangfusePromptTraceMetadata is { } promptTraceMetadata)
        {
            activity.SetTag("langfuse.observation.prompt.name", promptTraceMetadata.Name);
            activity.SetTag("langfuse.observation.prompt.version", promptTraceMetadata.Version);
        }

        if (providerPromptMetadata is not null)
        {
            activity.SetTag("langfuse.observation.metadata.langfusePromptFallback", providerPromptMetadata.IsFallback);
            activity.SetTag("langfuse.observation.metadata.promptTemplatePath", providerPromptMetadata.PromptPath);
        }
        else if (_options.LangfusePromptTraceMetadata is { IsFallback: true })
        {
            activity.SetTag("langfuse.observation.metadata.langfusePromptFallback", true);
        }

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            var reasoningEffort = _options.ReasoningEffort.Trim().ToLowerInvariant();
            activity.SetTag("gen_ai.request.reasoning_effort", reasoningEffort);
            activity.SetTag("langfuse.observation.metadata.openaiReasoningEffort", reasoningEffort);
        }

        if (executionTelemetry is not null)
        {
            activity.SetTag("gen_ai.request.service_tier", executionTelemetry.RequestedServiceTier);
            activity.SetTag("gen_ai.response.service_tier", executionTelemetry.FinalServiceTier);
            activity.SetTag("langfuse.observation.metadata.openaiExecutionStrategy", executionTelemetry.Strategy);
            activity.SetTag("langfuse.observation.metadata.openaiRequestedServiceTier", executionTelemetry.RequestedServiceTier);
            activity.SetTag("langfuse.observation.metadata.openaiFinalServiceTier", executionTelemetry.FinalServiceTier);
            activity.SetTag("langfuse.observation.metadata.openaiServiceTierFallbackUsed", executionTelemetry.FallbackUsed.ToString());
        }

        // Serialize messages as input (system prompt + user message)
        var inputMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        });
        activity.SetTag("langfuse.observation.input", JsonSerializer.Serialize(inputMessages));
        activity.SetTag("langfuse.observation.output", responseJson);
        telemetryMetadata?.ApplyToObservation(activity);

        // Token usage details
        var usageDetails = new
        {
            input = usage.InputTokenCount,
            output = usage.OutputTokenCount,
            cache_read_input_tokens = usage.InputTokenDetails?.CachedTokenCount ?? 0,
            reasoning_tokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0,
            total = usage.InputTokenCount + usage.OutputTokenCount
        };
        activity.SetTag("langfuse.observation.usage_details", JsonSerializer.Serialize(usageDetails));

        if (executionTelemetry is not null &&
            _costCalculationService.CalculateCostBreakdown(_model, usage, executionTelemetry.FinalServiceTier) is { } costBreakdown)
        {
            var costDetails = new
            {
                input = costBreakdown.Input,
                cache_read_input_tokens = costBreakdown.CachedInput,
                output = costBreakdown.Output,
                total = costBreakdown.Total
            };
            activity.SetTag("langfuse.observation.cost_details", JsonSerializer.Serialize(costDetails));
        }
    }

    private sealed record PredictionRequestMessage(string Role, string Content);

    private sealed record OpenAiResponseResult(
        string PredictionJson,
        ChatTokenUsage Usage,
        PredictionExecutionTelemetry? ExecutionTelemetry,
        string? FinalServiceTier = null);

    private sealed record PredictionExecutionTelemetry(
        string Strategy,
        string RequestedServiceTier,
        string FinalServiceTier,
        bool FallbackUsed);

    private static ChatTokenUsage ToChatTokenUsage(ResponseTokenUsage usage)
    {
        var cachedTokenCount = usage.InputTokenDetails?.CachedTokenCount ?? 0;
        var reasoningTokenCount = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0;
        var inputDetails = cachedTokenCount > 0
            ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedTokenCount)
            : null;
        var outputDetails = reasoningTokenCount > 0
            ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: reasoningTokenCount)
            : null;

        return OpenAIChatModelFactory.ChatTokenUsage(
            inputTokenCount: usage.InputTokenCount,
            outputTokenCount: usage.OutputTokenCount,
            inputTokenDetails: inputDetails,
            outputTokenDetails: outputDetails);
    }
}
