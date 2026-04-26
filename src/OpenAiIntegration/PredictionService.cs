using System.Collections.Generic;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using EHonda.KicktippAi.Core;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace OpenAiIntegration;

/// <summary>
/// Service for predicting match outcomes using OpenAI models
/// </summary>
public class PredictionService : IPredictionService
{
    private static readonly JsonSerializerOptions ProtocolJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ChatClient _chatClient;
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
        ChatClient chatClient, 
        ILogger<PredictionService> logger,
        ICostCalculationService costCalculationService,
        ITokenUsageTracker tokenUsageTracker,
        IInstructionsTemplateProvider templateProvider,
        string model,
        PredictionServiceOptions? options = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
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
        Match match, 
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

            // Create messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(instructions),
                new UserChatMessage(matchJson)
            };

            _logger.LogDebug("Calling OpenAI API for prediction");

            // Start an OTel activity for Langfuse generation tracking
            using var activity = Telemetry.Source.StartActivity("predict-match");

            // Call OpenAI with structured output format
            var completion = await CompleteMatchChatAsync(messages, includeJustification, cancellationToken);

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
            _tokenUsageTracker.AddUsage(_model, usage);

            // Calculate and log costs
            _costCalculationService.LogCostBreakdown(_model, usage);

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

    private async Task<MatchCompletionResult> CompleteMatchChatAsync(
        List<ChatMessage> messages,
        bool includeJustification,
        CancellationToken cancellationToken)
    {
        if (!_options.UseFlexProcessingWithStandardFallback)
        {
            var response = await _chatClient.CompleteChatAsync(
                messages,
                CreateMatchCompletionOptions(includeJustification),
                cancellationToken);

            return new MatchCompletionResult(
                response.Value.Content[0].Text,
                response.Value.Usage,
                null);
        }

        string? requestedServiceTier = "flex";
        var usedFallback = false;
        var pipeline = new ResiliencePipelineBuilder<MatchCompletionResult>()
            .AddRetry(new RetryStrategyOptions<MatchCompletionResult>
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                ShouldHandle = args => ValueTask.FromResult(IsFlexCapacityFailure(args.Outcome.Exception, args.Context.CancellationToken)),
                OnRetry = args =>
                {
                    usedFallback = true;
                    requestedServiceTier = null;
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "OpenAI flex processing failed with a capacity-style failure; retrying prediction with standard processing.");
                    return default;
                }
            })
            .Build();

        var result = await pipeline.ExecuteAsync(
            async ct =>
            {
                var completion = await CompleteProtocolMatchChatAsync(
                    messages,
                    includeJustification,
                    requestedServiceTier,
                    ct);

                var finalServiceTier = string.IsNullOrWhiteSpace(completion.FinalServiceTier)
                    ? requestedServiceTier ?? "standard"
                    : completion.FinalServiceTier;

                return completion with
                {
                    ExecutionTelemetry = new PredictionExecutionTelemetry(
                        "flex-first-standard-fallback",
                        usedFallback ? "standard" : "flex",
                        finalServiceTier,
                        usedFallback)
                };
            },
            cancellationToken);

        return result;
    }

    private static ChatCompletionOptions CreateMatchCompletionOptions(bool includeJustification)
    {
        return new ChatCompletionOptions
        {
            MaxOutputTokenCount = 10_000, // Safeguard against high costs
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "match_prediction",
                jsonSchema: BinaryData.FromBytes(BuildPredictionJsonSchema(includeJustification)),
                jsonSchemaIsStrict: true)
        };
    }

    private async Task<MatchCompletionResult> CompleteProtocolMatchChatAsync(
        IReadOnlyList<ChatMessage> messages,
        bool includeJustification,
        string? serviceTier,
        CancellationToken cancellationToken)
    {
        var requestPayload = CreateProtocolMatchRequestPayload(messages, includeJustification, serviceTier);
        using var content = BinaryContent.Create(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(requestPayload, ProtocolJsonOptions)));
        var response = await _chatClient.CompleteChatAsync(
            content,
            new RequestOptions { CancellationToken = cancellationToken });

        var protocolResponse = ParseProtocolChatCompletionResponse(response.GetRawResponse().Content);
        var predictionJson = protocolResponse.Choices.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(predictionJson))
        {
            throw new InvalidOperationException("OpenAI chat completion response did not contain message content.");
        }

        var usage = protocolResponse.Usage?.ToChatTokenUsage()
                    ?? throw new InvalidOperationException("OpenAI chat completion response did not contain token usage.");

        return new MatchCompletionResult(
            predictionJson!,
            usage,
            new PredictionExecutionTelemetry(
                "flex-first-standard-fallback",
                serviceTier ?? "standard",
                protocolResponse.ServiceTier ?? serviceTier ?? "standard",
                FallbackUsed: serviceTier is null),
            protocolResponse.ServiceTier);
    }

    private object CreateProtocolMatchRequestPayload(
        IReadOnlyList<ChatMessage> messages,
        bool includeJustification,
        string? serviceTier)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>(BuildPredictionJsonSchema(includeJustification));

        return new
        {
            model = _model,
            messages = messages.Select(CreateProtocolMessage).ToArray(),
            max_completion_tokens = 10_000,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "match_prediction",
                    schema,
                    strict = true
                }
            },
            service_tier = serviceTier
        };
    }

    private static object CreateProtocolMessage(ChatMessage message)
    {
        return message switch
        {
            SystemChatMessage system => new { role = "system", content = system.Content[0].Text },
            UserChatMessage user => new { role = "user", content = user.Content[0].Text },
            _ => throw new InvalidOperationException($"Unsupported chat message type '{message.GetType().Name}'.")
        };
    }

    private static ProtocolChatCompletionResponse ParseProtocolChatCompletionResponse(BinaryData content)
    {
        return JsonSerializer.Deserialize<ProtocolChatCompletionResponse>(content, ProtocolJsonOptions)
               ?? throw new InvalidOperationException("OpenAI chat completion response could not be deserialized.");
    }

    private static bool IsFlexCapacityFailure(Exception? exception, CancellationToken cancellationToken)
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
            ClientResultException { Status: 429 } clientException => IsFlexResourceUnavailableFailure(clientException),
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false
        };
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

            // Create messages for the chat completion
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(instructions),
                new UserChatMessage(questionJson)
            };

            _logger.LogDebug("Calling OpenAI API for bonus prediction");

            // Create JSON schema based on the question
            var jsonSchema = CreateSingleBonusPredictionJsonSchema(bonusQuestion);

            // Start an OTel activity for Langfuse generation tracking
            using var activity = Telemetry.Source.StartActivity("predict-bonus");

            // Call OpenAI with structured output format
            var response = await _chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 10_000, // Standard limit for single question
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "bonus_prediction",
                        jsonSchema: BinaryData.FromBytes(jsonSchema),
                        jsonSchemaIsStrict: true)
                },
                cancellationToken);

            // Parse the structured response
            var predictionJson = response.Value.Content[0].Text;
            _logger.LogDebug("Received bonus prediction JSON: {PredictionJson}", predictionJson);

            var prediction = ParseSingleBonusPrediction(predictionJson, bonusQuestion);
            
            if (prediction != null)
            {
                _logger.LogInformation("Generated prediction for bonus question: {SelectedOptions}", 
                    string.Join(", ", prediction.SelectedOptionIds));
            }

            // Log token usage and cost breakdown
            var usage = response.Value.Usage;
            _logger.LogDebug("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);

            // Set Langfuse generation attributes on the activity
            SetLangfuseGenerationAttributes(activity, messages, predictionJson, usage, telemetryMetadata);

            // Add usage to tracker
            _tokenUsageTracker.AddUsage(_model, usage);

            // Calculate and log costs
            _costCalculationService.LogCostBreakdown(_model, usage);

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
        List<ChatMessage> messages,
        string responseJson,
        ChatTokenUsage usage,
        PredictionTelemetryMetadata? telemetryMetadata,
        PredictionExecutionTelemetry? executionTelemetry = null)
    {
        if (activity is null)
            return;

        activity.SetTag("langfuse.observation.type", "generation");
        activity.SetTag("gen_ai.request.model", _model);
        if (_options.LangfusePromptTraceMetadata is { } promptTraceMetadata)
        {
            activity.SetTag("langfuse.observation.prompt.name", promptTraceMetadata.Name);
            activity.SetTag("langfuse.observation.prompt.version", promptTraceMetadata.Version);
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
            role = m switch
            {
                SystemChatMessage => "system",
                UserChatMessage => "user",
                _ => "unknown"
            },
            content = m switch
            {
                SystemChatMessage s => s.Content[0].Text,
                UserChatMessage u => u.Content[0].Text,
                _ => string.Empty
            }
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
            reasoning_tokens = usage.OutputTokenDetails?.ReasoningTokenCount ?? 0
        };
        activity.SetTag("langfuse.observation.usage_details", JsonSerializer.Serialize(usageDetails));

        // Cost details are intentionally omitted here: Langfuse automatically infers costs from the
        // model name and usage_details using its maintained pricing tables, which are more up-to-date
        // than the prices kept in this repository. Explicitly ingesting cost_details would override
        // that inference (ingested values take priority over inferred ones).
    }

    private sealed record MatchCompletionResult(
        string PredictionJson,
        ChatTokenUsage Usage,
        PredictionExecutionTelemetry? ExecutionTelemetry,
        string? FinalServiceTier = null);

    private sealed record PredictionExecutionTelemetry(
        string Strategy,
        string RequestedServiceTier,
        string FinalServiceTier,
        bool FallbackUsed);

    private sealed class ProtocolChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public ProtocolChatCompletionChoice[] Choices { get; set; } = [];

        [JsonPropertyName("usage")]
        public ProtocolChatCompletionUsage? Usage { get; set; }

        [JsonPropertyName("service_tier")]
        public string? ServiceTier { get; set; }
    }

    private sealed class ProtocolChatCompletionChoice
    {
        [JsonPropertyName("message")]
        public ProtocolChatCompletionMessage? Message { get; set; }
    }

    private sealed class ProtocolChatCompletionMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ProtocolChatCompletionUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("prompt_tokens_details")]
        public ProtocolPromptTokenDetails? PromptTokenDetails { get; set; }

        [JsonPropertyName("completion_tokens_details")]
        public ProtocolCompletionTokenDetails? CompletionTokenDetails { get; set; }

        public ChatTokenUsage ToChatTokenUsage()
        {
            var cachedTokenCount = PromptTokenDetails?.CachedTokens ?? 0;
            var reasoningTokenCount = CompletionTokenDetails?.ReasoningTokens ?? 0;
            var inputDetails = cachedTokenCount > 0
                ? OpenAIChatModelFactory.ChatInputTokenUsageDetails(cachedTokenCount: cachedTokenCount)
                : null;
            var outputDetails = reasoningTokenCount > 0
                ? OpenAIChatModelFactory.ChatOutputTokenUsageDetails(reasoningTokenCount: reasoningTokenCount)
                : null;

            return OpenAIChatModelFactory.ChatTokenUsage(
                inputTokenCount: PromptTokens,
                outputTokenCount: CompletionTokens,
                inputTokenDetails: inputDetails,
                outputTokenDetails: outputDetails);
        }
    }

    private sealed class ProtocolPromptTokenDetails
    {
        [JsonPropertyName("cached_tokens")]
        public int CachedTokens { get; set; }
    }

    private sealed class ProtocolCompletionTokenDetails
    {
        [JsonPropertyName("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
    }
}
